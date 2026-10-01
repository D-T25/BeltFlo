using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using BeltFlo.Classes;
using BeltFlo.Database;
using BeltFlo.Language;

namespace BeltFlo.Forms
{
    // Zero and span for the active harvester's scale, worked out from the module's
    // raw converter counts.
    //
    // Zero Scale averages the belt running empty — over one full belt turn when
    // pulses per turn is known, so a heavy or dirty patch of belt can't set the zero.
    // Known Weight averages a weight of known size resting on the stopped section
    // and sets the span: pounds per count above zero.
    //
    // Nothing changes until Save, and the span changes only here, when the user asks.
    // A span change starts a new calibration revision (earlier points can then be
    // rescaled by the revision they carry); a zero alone updates the current one.
    public partial class frmMenuScaleCal : Form
    {
        private bool _dragging;
        private Point _dragStart;
        private bool _padOpen;

        // Latest saved revision, and what has been measured but not yet saved
        private ConveyorConfig _cfg;
        private double? _newZero;
        private double? _newSpan;

        // Readings: the last StableWindowSec for the live readout, and everything
        // since a Zero or Known Weight run started for its average.
        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 100 };
        private DateTime _lastSampleUtc = DateTime.MinValue;
        private readonly Queue<(DateTime utc, int raw)> _recent = new Queue<(DateTime utc, int raw)>();
        private readonly List<int> _samples = new List<int>();

        private enum RunMode { Idle, Zeroing, Weighing }
        private RunMode _mode = RunMode.Idle;
        private DateTime _modeStartUtc;
        private uint     _modeStartPulses;
        private double   _knownLb;

        private const double StableWindowSec = 2.0;
        private const double ZeroMinSec      = 10.0;   // at least this long, even on a short belt
        private const double ZeroFallbackSec = 30.0;   // when pulses per turn isn't known
        private const double WeighSec        = 5.0;
        private const int    MinSpanCounts   = 100;    // a known weight must move the reading at least this much

        // "Stable" = the last 2 s spread no more than this. First guesses, to be set
        // from bench readings with the real cells.
        private const double StableMinCounts = 50;
        private const double StableFraction  = 0.01;   // of the reading above zero

        public frmMenuScaleCal()
        {
            InitializeComponent();
        }

        private void frmMenuScaleCal_Load(object sender, EventArgs e)
        {
            ApplyTheme();
            FormPositions.Restore(this);
            this.FormClosed += (s2, ev2) => FormPositions.Save(this);
            foreach (Control c in new Control[] { pnlTitle, lblTitle })
            {
                c.MouseDown += (s, ev) => { if (ev.Button == MouseButtons.Left) { _dragging = true; _dragStart = ev.Location; } };
                c.MouseMove += (s, ev) => { if (_dragging) { Left += ev.X - _dragStart.X; Top += ev.Y - _dragStart.Y; } };
                c.MouseUp   += (s, ev) => _dragging = false;
            }

            var profile = Core.Database.Profiles.GetById(Core.ActiveProfileId);
            if (profile != null) lblTitle.Text = $"{Lang.lgTitleScaleCal} — {profile.Name}";

            _cfg = Core.Database.ConveyorConfigs.GetLatest(Core.ActiveProfileId)
                   ?? new ConveyorConfig { ProfileId = Core.ActiveProfileId };

            ShowValues();
            ShowLive();
            _timer.Tick += Timer_Tick;
            _timer.Start();
            this.FormClosed += (s, ev) => { _timer.Stop(); _timer.Dispose(); };
        }

        private void ApplyTheme()
        {
            var back = Properties.Settings.Default.MainBackColour;
            var fore = Properties.Settings.Default.MainForeColour;
            var ctrl = Color.FromArgb(60, 60, 60);
            pnlTitle.BackColor   = back;
            pnlContent.BackColor = back;
            lblTitle.ForeColor   = Color.FromArgb(180, 200, 220);
            foreach (Control c in pnlContent.Controls)
            {
                c.ForeColor = fore;
                if (c is Button b) { b.BackColor = ctrl; b.ForeColor = Color.White; }
            }
            foreach (var box in new[] { lblRawVal, lblWeightVal, lblStableVal, lblZeroVal, lblSpanVal })
            {
                box.BackColor = ctrl;
                box.ForeColor = fore;
            }
            btnSave.BackColor = Color.FromArgb(0, 90, 0);
        }

        // ── Readings ──────────────────────────────────────────────────────────

        private void Timer_Tick(object sender, EventArgs e)
        {
            if (Core.ModuleConnected && Core.LastModuleReceive != _lastSampleUtc)
            {
                _lastSampleUtc = Core.LastModuleReceive;
                int raw = Core.LastScaleRaw;
                _recent.Enqueue((_lastSampleUtc, raw));
                if (_mode != RunMode.Idle) _samples.Add(raw);
            }
            while (_recent.Count > 0 && (DateTime.UtcNow - _recent.Peek().utc).TotalSeconds > StableWindowSec)
                _recent.Dequeue();

            if (_mode == RunMode.Zeroing)       CheckZeroDone();
            else if (_mode == RunMode.Weighing) CheckWeighDone();

            ShowLive();
        }

        private double Zero => _newZero ?? _cfg.ZeroCounts;
        private double Span => _newSpan ?? _cfg.SpanLbPerCount;

        private void ShowLive()
        {
            if (!Core.ModuleConnected || _recent.Count == 0)
            {
                lblRawVal.Text         = "--";
                lblWeightVal.Text      = "--";
                lblStableVal.Text      = "No module";
                lblStableVal.ForeColor = Color.Silver;
                return;
            }

            double avg = _recent.Average(r => r.raw);
            int    min = _recent.Min(r => r.raw);
            int    max = _recent.Max(r => r.raw);

            lblRawVal.Text    = avg.ToString("F0");
            bool spanReady = _newSpan.HasValue || _cfg.IsCalibrated;
            lblWeightVal.Text = spanReady
                ? $"{Props.DisplayLoad((avg - Zero) * Span):F1} {Props.LoadUnit}"
                : "--";

            double tolerance = Math.Max(StableMinCounts, StableFraction * Math.Abs(avg - Zero));
            bool stable = _recent.Count >= 3 && (max - min) <= tolerance;
            lblStableVal.Text      = stable ? Lang.lgCalStable : Lang.lgCalUnstable;
            lblStableVal.ForeColor = stable ? OkabeIto.BluishGreen : OkabeIto.Orange;
        }

        private void ShowValues()
        {
            lblZeroVal.Text  = Zero.ToString("F0");
            lblZeroInfo.Text = _newZero.HasValue ? "new — not saved"
                             : _cfg.ZeroSetAt.HasValue ? "set " + _cfg.ZeroSetAt.Value.ToLocalTime().ToString("MMM d HH:mm")
                             : "never set";
            lblSpanVal.Text  = Span.ToString("G5");
            lblSpanInfo.Text = _newSpan.HasValue ? "new — not saved" : "lb per count";
        }

        // ── Zero Scale ────────────────────────────────────────────────────────

        private void btnZero_Click(object sender, EventArgs e)
        {
            if (_mode == RunMode.Zeroing) { EndRun("Zero cancelled."); return; }
            if (_mode != RunMode.Idle || !ModuleReady()) return;

            _samples.Clear();
            _mode            = RunMode.Zeroing;
            _modeStartUtc    = DateTime.UtcNow;
            _modeStartPulses = Core.LastCumPulses;
            btnZero.Text      = Lang.lgStop;
            btnWeight.Enabled = false;
            lblStatus.Text = Core.LastBeltRunning
                ? "Zeroing — keep the belt running empty."
                : "Zeroing — run the belt empty so the whole belt is averaged.";
        }

        private void CheckZeroDone()
        {
            double sec    = (DateTime.UtcNow - _modeStartUtc).TotalSeconds;
            uint   pulses = unchecked(Core.LastCumPulses - _modeStartPulses);
            int    ppr    = _cfg.PulsesPerRev;

            bool done = ppr > 0 ? sec >= ZeroMinSec && pulses >= ppr : sec >= ZeroFallbackSec;
            lblStatus.Text = ppr > 0
                ? $"Zeroing: {sec:F0} s, {pulses} of {ppr} pulses (one belt turn). Belt running empty."
                : $"Zeroing: {sec:F0} of {ZeroFallbackSec:F0} s. Belt running empty. (Set pulses per turn in Conveyor Setup to zero over one turn.)";
            if (!done) return;

            if (_samples.Count == 0) { EndRun("No readings from the module."); return; }
            _newZero = _samples.Average();
            EndRun($"New zero {_newZero:F0} counts from {_samples.Count} readings. Press Save to keep it.");
        }

        // ── Known Weight ──────────────────────────────────────────────────────

        private void btnWeight_Click(object sender, EventArgs e)
        {
            if (_mode == RunMode.Weighing) { EndRun("Known weight cancelled."); return; }
            if (_mode != RunMode.Idle || !ModuleReady()) return;

            // A known-weight span without a real zero is meaningless. A zero
            // measured in this same visit is fine; both can be saved together.
            if (!_newZero.HasValue && !_cfg.ZeroSetAt.HasValue)
            {
                lblStatus.Text = "Zero the empty belt first, then apply the known weight.";
                return;
            }

            if (!AskNumber(0.1, 100000, 0, 1, $"Known Weight ({Props.LoadUnit})", out double w)) return;

            _knownLb = Props.LoadToLb(w);
            _samples.Clear();
            _mode         = RunMode.Weighing;
            _modeStartUtc = DateTime.UtcNow;
            btnWeight.Text  = Lang.lgStop;
            btnZero.Enabled = false;
            lblStatus.Text  = "Weighing — belt stopped, weight resting on the section, nothing else touching it.";
        }

        private void CheckWeighDone()
        {
            double sec = (DateTime.UtcNow - _modeStartUtc).TotalSeconds;
            lblStatus.Text = $"Weighing: {sec:F0} of {WeighSec:F0} s. Belt stopped, weight on the section.";
            if (sec < WeighSec) return;

            if (_samples.Count == 0) { EndRun("No readings from the module."); return; }
            double counts = _samples.Average() - Zero;
            if (Math.Abs(counts) < MinSpanCounts)
            {
                EndRun($"The reading only moved {counts:F0} counts — check the weight is on the section and the scale was zeroed first.");
                return;
            }
            _newSpan = _knownLb / counts;
            EndRun($"{Props.DisplayLoad(_knownLb):F1} {Props.LoadUnit} over {counts:F0} counts: new span {_newSpan:G5} lb per count. Press Save to keep it.");
        }

        // ── Shared ────────────────────────────────────────────────────────────

        private bool ModuleReady()
        {
            if (Core.ModuleConnected) return true;
            lblStatus.Text = "No module connected — the readings come from the module.";
            return false;
        }

        private void EndRun(string message)
        {
            _mode = RunMode.Idle;
            btnZero.Text      = Lang.lgCalZeroScale;
            btnWeight.Text    = Lang.lgCalKnownWeight;
            btnZero.Enabled   = true;
            btnWeight.Enabled = true;
            lblStatus.Text    = message;
            ShowValues();
        }

        private bool AskNumber(double min, double max, double current, int decimals, string title, out double value)
        {
            value = current;
            if (_padOpen) return false;
            _padOpen = true;
            try
            {
                using var pad = new frmNumpad(min, max, current, decimals, title);
                if (pad.ShowDialog(this) != DialogResult.OK) return false;
                value = Math.Min(max, Math.Max(min, pad.ReturnValue));
                return true;
            }
            finally { _padOpen = false; }
        }

        // ── Save / Close ──────────────────────────────────────────────────────

        private void btnSave_Click(object sender, EventArgs e)
        {
            if (!_newZero.HasValue && !_newSpan.HasValue)
            {
                lblStatus.Text = "Nothing new to save — use Zero Scale or Known Weight first.";
                return;
            }
            if (Core.Collector.ActiveJobId > 0)
            {
                Props.ShowMessage(Lang.lgConveyorJobRunning, "", 3000, true);
                return;
            }

            var edited = new ConveyorConfig
            {
                ProfileId        = Core.ActiveProfileId,
                ZeroCounts       = Zero,
                SpanLbPerCount   = Span,
                ZeroSetAt        = _newZero.HasValue ? DateTime.UtcNow : _cfg.ZeroSetAt,
                PulsesPerRev     = _cfg.PulsesPerRev,
                InchesPerPulse   = _cfg.InchesPerPulse,
                SectionLenIn     = _cfg.SectionLenIn,
                FlowThresholdLbS = _cfg.FlowThresholdLbS,
                BeltStopTimeoutS = _cfg.BeltStopTimeoutS,
                DelaySec         = _cfg.DelaySec
            };
            int revision = Core.SaveConveyorConfig(edited);

            _cfg = Core.Database.ConveyorConfigs.GetLatest(Core.ActiveProfileId) ?? edited;
            _newZero = null;
            _newSpan = null;
            ShowValues();
            lblStatus.Text = $"Saved — calibration revision {revision}. The module has been sent the new values.";
            Props.ShowMessage(Lang.lgSettingsSaved);
        }

        private void btnClose_Click(object sender, EventArgs e) => this.Close();
    }
}
