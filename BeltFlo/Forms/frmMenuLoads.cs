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
    // Truck loads and their certified (ticket) weights.
    //
    // Tickets come back from the scale house later, often after the job is
    // finished, so the list holds the loads of every job, oldest first.
    //
    // Direct-to-truck loads own their scale pounds and can be corrected per load.
    // With a scale before a holding tank, field pounds/map points stay job-level;
    // truck records are tickets only and their total corrects/calibrates the job.
    //
    // A load's monitor weight is never overwritten. Points are always rescaled
    // from the factor they carry now to the one wanted, so entering a different
    // ticket later, or going back to Save Weight Only, never compounds.
    public partial class frmMenuLoads : Form
    {
        private bool _dragging;
        private Point _dragStart;
        private bool _padOpen;

        private readonly List<LoadRecord> _loads = new List<LoadRecord>();
        private readonly Dictionary<int, int> _loadNumbers = new Dictionary<int, int>();   // load id → number within its job
        private readonly Dictionary<int, (string name, int profileId, double pounds)> _jobs
            = new Dictionary<int, (string, int, double)>();

        private double? _ticketLb;   // entered for the selected load, not yet saved
        private int _seenJobId = -2, _seenLoadId = -2;

        private readonly System.Windows.Forms.Timer _timer = new System.Windows.Forms.Timer { Interval = 1000 };

        // Stored in English so reports read the same in any language.
        private static readonly string[] Flags = { "", "Wet", "Spoiled", "Trash", "Stones" };
        private Button[] _flagButtons;

        private static readonly Color CtrlBack = Color.FromArgb(60, 60, 60);
        private static readonly Color FlagOnBack = Color.FromArgb(0, 90, 140);

        public frmMenuLoads()
        {
            InitializeComponent();
        }

        private void frmMenuLoads_Load(object sender, EventArgs e)
        {
            _flagButtons = new[] { btnFlagNone, btnFlagWet, btnFlagSpoiled, btnFlagTrash, btnFlagStones };
            for (int i = 0; i < _flagButtons.Length; i++)
            {
                string flag = Flags[i];
                _flagButtons[i].Click += (s, ev) => SetFlag(flag);
            }

            ApplyTheme();
            FormPositions.Restore(this);
            this.FormClosed += (s2, ev2) => FormPositions.Save(this);
            foreach (Control c in new Control[] { pnlTitle, lblTitle })
            {
                c.MouseDown += (s, ev) => { if (ev.Button == MouseButtons.Left) { _dragging = true; _dragStart = ev.Location; } };
                c.MouseMove += (s, ev) => { if (_dragging) { Left += ev.X - _dragStart.X; Top += ev.Y - _dragStart.Y; } };
                c.MouseUp   += (s, ev) => _dragging = false;
            }

            _seenJobId  = Core.Collector.ActiveJobId;
            _seenLoadId = Core.Collector.ActiveLoadId;
            LoadList(false);
            SelectInitialLoad();

            Core.JobStateChanged += Core_JobStateChanged;
            _timer.Tick += Timer_Tick;
            _timer.Start();
            this.FormClosed += (s, ev) =>
            {
                Core.JobStateChanged -= Core_JobStateChanged;
                _timer.Stop();
                _timer.Dispose();
            };
        }

        private void ApplyTheme()
        {
            var back = Properties.Settings.Default.MainBackColour;
            var fore = Properties.Settings.Default.MainForeColour;
            pnlTitle.BackColor   = back;
            pnlContent.BackColor = back;
            lblTitle.ForeColor   = Color.FromArgb(180, 200, 220);
            foreach (Control c in pnlContent.Controls)
            {
                c.ForeColor = fore;
                if (c is Button b)    { b.BackColor  = CtrlBack; b.ForeColor  = Color.White; }
                if (c is ListView lv) { lv.BackColor = CtrlBack; lv.ForeColor = Color.White; }
            }
            foreach (var box in new[] { lblMonitorVal, lblTicketVal, lblDiffVal, lblFactorVal })
            {
                box.BackColor = CtrlBack;
                box.ForeColor = Color.White;
            }
            btnCorrectLoad.BackColor = Color.FromArgb(0, 90, 0);
            btnCorrectJob.BackColor  = Color.FromArgb(0, 90, 0);
            btnUpdateCal.BackColor   = Color.FromArgb(110, 70, 0);
            btnDelete.BackColor      = Color.FromArgb(100, 0, 0);
        }

        // ── Keeping up with the run screen ────────────────────────────────────

        // Loads started, finished or reopened from the run screen. Only those
        // changes rebuild the list — pauses and headland auto-pauses raise the same
        // event and must not throw away a ticket being typed in.
        private void Core_JobStateChanged(object sender, EventArgs e)
        {
            if (this.IsDisposed || !this.IsHandleCreated) return;
            this.BeginInvoke((Action)(() =>
            {
                int activeLoad = Core.Collector.ActiveLoadId;
                if (Core.Collector.ActiveJobId == _seenJobId && activeLoad == _seenLoadId) return;

                // A load just started (or reopened) is brought into view, unless a
                // ticket is being typed in for another load.
                bool loadStarted = activeLoad > 0 && activeLoad != _seenLoadId;
                var sel = SelectedLoad;
                bool typing = sel != null && _ticketLb.HasValue && _ticketLb != sel.CertifiedLb;

                _seenJobId  = Core.Collector.ActiveJobId;
                _seenLoadId = activeLoad;
                LoadList(true);
                if (loadStarted && !typing) SelectLoad(activeLoad);
            }));
        }

        // The open load's weight climbs while the screen is up.
        private void Timer_Tick(object sender, EventArgs e)
        {
            int idx = _loads.FindIndex(l => l.Id == Core.Collector.ActiveLoadId);
            if (idx < 0) return;
            lvLoads.Items[idx].SubItems[3].Text = Props.DisplayLoad(Core.Collector.CurrentLoadLb).ToString("F0");
            if (SelectedLoad?.Id == _loads[idx].Id) ShowNumbers(_loads[idx]);
        }

        // ── List ──────────────────────────────────────────────────────────────

        private LoadRecord SelectedLoad =>
            lvLoads.SelectedIndices.Count > 0 && lvLoads.SelectedIndices[0] < _loads.Count
                ? _loads[lvLoads.SelectedIndices[0]]
                : null;

        private bool _rebuilding;

        private void LoadList(bool keepTicket)
        {
            int selId = SelectedLoad?.Id ?? -1;
            double? pending = _ticketLb;

            _jobs.Clear();
            foreach (var j in Core.Database.Jobs.GetAll())
                _jobs[j.id] = (j.name, j.profileId, j.volume);

            _loads.Clear();
            _loads.AddRange(Core.Database.Loads.GetAll().OrderBy(l => l.Id));   // oldest first

            // The number the operator and the ticket use: 1 for a job's first truck.
            _loadNumbers.Clear();
            foreach (var g in _loads.GroupBy(l => l.JobId))
            {
                int n = 0;
                foreach (var l in g.OrderBy(l => l.Id)) _loadNumbers[l.Id] = ++n;
            }

            _rebuilding = true;
            lvLoads.BeginUpdate();
            lvLoads.Items.Clear();
            foreach (var l in _loads)
            {
                var item = new ListViewItem(_loadNumbers[l.Id].ToString());
                item.SubItems.Add(JobName(l.JobId));
                item.SubItems.Add(l.Truck);
                item.SubItems.Add(Props.DisplayLoad(MonitorLb(l)).ToString("F0"));
                item.SubItems.Add(l.CertifiedLb.HasValue ? Props.DisplayLoad(l.CertifiedLb.Value).ToString("F0") : "");
                item.SubItems.Add(l.CertifiedLb.HasValue && l.MonitorLb > 0
                    ? ((l.CertifiedLb.Value / l.MonitorLb - 1) * 100).ToString("+0.0;-0.0;0.0") : "");
                item.SubItems.Add(StatusText(l));
                lvLoads.Items.Add(item);
            }
            int idx = _loads.FindIndex(l => l.Id == selId);
            if (idx >= 0) lvLoads.Items[idx].Selected = true;
            lvLoads.EndUpdate();
            _rebuilding = false;
            FitStatusColumn();

            if (idx >= 0) lvLoads.Items[idx].EnsureVisible();
            ShowSelected();
            if (keepTicket && idx >= 0 && pending.HasValue)
            {
                _ticketLb = pending;
                ShowNumbers(_loads[idx]);
            }
        }

        // Status takes whatever width is left, so no empty column shows after it.
        // Redone after each rebuild: the scroll bar comes and goes with the rows.
        private void FitStatusColumn()
        {
            int others = 0;
            for (int i = 0; i < lvLoads.Columns.Count - 1; i++) others += lvLoads.Columns[i].Width;
            lvLoads.Columns[lvLoads.Columns.Count - 1].Width = Math.Max(80, lvLoads.ClientSize.Width - others);
        }

        // The load being filled, else the oldest one still waiting for its ticket,
        // else the newest load.
        private void SelectInitialLoad()
        {
            if (_loads.Count == 0) return;
            int idx = _loads.FindIndex(l => l.Id == Core.Collector.ActiveLoadId);
            if (idx < 0) idx = _loads.FindIndex(l => l.Status == LoadRecord.StatusWaiting);
            if (idx < 0) idx = _loads.Count - 1;
            SelectLoad(_loads[idx].Id);
        }

        private void SelectLoad(int loadId)
        {
            int idx = _loads.FindIndex(l => l.Id == loadId);
            if (idx < 0) return;
            lvLoads.Items[idx].Selected = true;
            lvLoads.Items[idx].EnsureVisible();
        }

        private void lvLoads_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (_rebuilding) return;
            ShowSelected();
        }

        private string JobName(int jobId) => _jobs.TryGetValue(jobId, out var j) ? j.name : "";

        private double MonitorLb(LoadRecord l) =>
            l.Id == Core.Collector.ActiveLoadId ? Core.Collector.CurrentLoadLb : l.MonitorLb;

        private static string StatusText(LoadRecord l)
        {
            string s = l.Status switch
            {
                LoadRecord.StatusActive    => Lang.lgLoadActive,
                LoadRecord.StatusWaiting   => Lang.lgLoadWaiting,
                LoadRecord.StatusWeighed   => Lang.lgLoadWeighed,
                LoadRecord.StatusCorrected => Lang.lgLoadCorrected,
                _                          => l.Status
            };
            string flag = FlagText(l.Flag);
            return flag.Length > 0 ? s + " · " + flag : s;
        }

        private static string FlagText(string flag) => flag switch
        {
            "Wet"    => Lang.lgFlagWet,
            "Spoiled" => Lang.lgFlagSpoiled,
            "Trash"  => Lang.lgFlagTrash,
            "Stones" => Lang.lgFlagStones,
            _        => ""
        };

        private bool IsBeforeTankJob(int jobId)
        {
            if (!_jobs.TryGetValue(jobId, out var j)) return false;
            return Core.Database.Profiles.GetById(j.profileId)?.IsBeforeHoldingTank == true;
        }

        private static DateTime Local(DateTime utc) => DateTime.SpecifyKind(utc, DateTimeKind.Utc).ToLocalTime();

        // ── Selected load ─────────────────────────────────────────────────────

        private void ShowSelected()
        {
            var l = SelectedLoad;
            _ticketLb = l?.CertifiedLb;

            bool has        = l != null;
            bool active     = has && l.Status == LoadRecord.StatusActive;
            bool beforeTank = has && IsBeforeTankJob(l.JobId);

            if (!has)
            {
                lblLoadInfo.Text = _loads.Count == 0 ? Lang.lgNoLoads : "";
            }
            else
            {
                string times = Local(l.OpenedAt).ToString("MMM d HH:mm")
                             + (l.ClosedAt.HasValue ? " – " + Local(l.ClosedAt.Value).ToString("HH:mm") : "");
                lblLoadInfo.Text = $"{Lang.lgLoad} {_loadNumbers[l.Id]}  ·  {JobName(l.JobId)}  ·  {times}";
            }

            foreach (var b in _flagButtons) b.Enabled = has;
            ShowFlag(l);
            btnRename.Enabled = has;

            lblTicketVal.Enabled   = has && !active;
            btnWeightOnly.Enabled  = has && !active;
            btnCorrectLoad.Visible = !beforeTank;
            btnUpdateCal.Visible   = has;
            btnCorrectJob.Visible  = beforeTank;
            btnCorrectLoad.Enabled = has && !active && !beforeTank;
            btnUpdateCal.Enabled   = beforeTank
                ? CanCorrectJob(l.JobId, out _, out _, out _)
                : has && !active;
            btnCorrectJob.Enabled  = beforeTank && CanCorrectJob(l.JobId, out _, out _, out _);

            btnReopen.Enabled = has && l.Status == LoadRecord.StatusWaiting
                                && l.JobId == Core.Collector.ActiveJobId
                                && Core.Collector.ActiveLoadId <= 0;
            btnDelete.Enabled = has && !active;

            ShowNumbers(l);

            if (!has) lblStatus.Text = "";
            else if (active && beforeTank)
                lblStatus.Text = "Truck ticket record is open. The pre-tank scale continues measuring the job independently. Finish this record with ⏹ before entering its ticket.";
            else if (active)
                lblStatus.Text = "This load is still filling. Finish it with ⏹ before entering its ticket.";
            else if (beforeTank)
                lblStatus.Text = BeforeTankStatus(l.JobId);
            else
                lblStatus.Text = l.CertifiedLb.HasValue ? "" : "Tap the ticket box to enter the certified weight.";
        }

        private void ShowNumbers(LoadRecord l)
        {
            if (l == null)
            {
                lblMonitorVal.Text = lblTicketVal.Text = lblDiffVal.Text = lblFactorVal.Text = "--";
                return;
            }

            double mon = MonitorLb(l);
            string unit = Props.LoadUnit;
            bool beforeTank = IsBeforeTankJob(l.JobId);

            lblMonitorVal.Text = beforeTank ? "--" : $"{Props.DisplayLoad(mon):F0} {unit}";
            lblTicketVal.Text  = _ticketLb.HasValue ? $"{Props.DisplayLoad(_ticketLb.Value):F0} {unit}" : "--";

            if (beforeTank)
            {
                if (CanCorrectJob(l.JobId, out double ticketsLb, out double jobLb, out _))
                {
                    double diff = ticketsLb - jobLb;
                    lblDiffVal.Text   = $"{Props.DisplayLoad(diff):+0;-0;0} {unit}  {diff / jobLb * 100:+0.0;-0.0;0.0}%";
                    lblFactorVal.Text = (ticketsLb / jobLb).ToString("F4");
                }
                else
                {
                    lblDiffVal.Text = "--";
                    lblFactorVal.Text = "--";
                }
            }
            else if (_ticketLb.HasValue && mon > 0)
            {
                double diff = _ticketLb.Value - mon;
                lblDiffVal.Text   = $"{Props.DisplayLoad(diff):+0;-0;0} {unit}  {diff / mon * 100:+0.0;-0.0;0.0}%";
                lblFactorVal.Text = (_ticketLb.Value / mon).ToString("F4");
            }
            else
            {
                lblDiffVal.Text   = "--";
                lblFactorVal.Text = "--";
            }
        }

        private void ShowFlag(LoadRecord l)
        {
            string flag = l?.Flag ?? "";
            for (int i = 0; i < _flagButtons.Length; i++)
                _flagButtons[i].BackColor = l != null && Flags[i] == flag ? FlagOnBack : CtrlBack;
        }

        // ── Ticket ────────────────────────────────────────────────────────────

        private void lblTicketVal_Click(object sender, EventArgs e)
        {
            var l = SelectedLoad;
            if (l == null || l.Status == LoadRecord.StatusActive || _padOpen) return;
            _padOpen = true;
            try
            {
                double current = Math.Round(Props.DisplayLoad(_ticketLb ?? l.MonitorLb));
                using var pad = new frmNumpad(1, 1000000, current, 0, $"{Lang.lgTicketWeight} ({Props.LoadUnit})");
                if (pad.ShowDialog(this) != DialogResult.OK) return;
                _ticketLb = Props.LoadToLb(Math.Max(1, pad.ReturnValue));
                ShowNumbers(l);
                lblStatus.Text = IsBeforeTankJob(l.JobId)
                    ? "Press Save Weight Only to keep this truck ticket. Pre-tank scale pounds stay with the job."
                    : "Save Weight Only keeps the ticket. Correct This Load also rescales the load's map. Update Calibration also changes the span.";
            }
            finally { _padOpen = false; }
        }

        /// <summary>The selected closed load with a ticket entered, or null with the reason shown.</summary>
        private LoadRecord TicketReady(bool needMonitor)
        {
            var l = SelectedLoad;
            if (l == null || l.Status == LoadRecord.StatusActive) return null;
            if (!_ticketLb.HasValue)
            {
                lblStatus.Text = "Tap the ticket box to enter the certified weight first.";
                return null;
            }
            if (needMonitor && l.MonitorLb <= 0)
            {
                lblStatus.Text = "The monitor weighed nothing on this load, so there is nothing to correct.";
                return null;
            }
            return l;
        }

        private double FactorFor(LoadRecord l) => l.MonitorLb > 0 ? _ticketLb.Value / l.MonitorLb : 1.0;

        /// <summary>
        /// Rescales the load's map points from the factor they carry now to the one
        /// wanted: 1 puts them back as measured. Keeps the running job's in-memory
        /// total in step. Returns the points rewritten.
        /// </summary>
        private int ApplyToPoints(LoadRecord l, double wanted)
        {
            double current = l.Status == LoadRecord.StatusCorrected ? l.Factor : 1.0;
            double ratio = wanted / current;
            if (Math.Abs(ratio - 1.0) < 1e-9) return 0;

            var (rows, total) = Core.Database.YieldData.RescaleJob(l.JobId, ratio, l.Id);
            Core.Collector.SyncTotalPounds(l.JobId, total);
            return rows;
        }

        private void btnWeightOnly_Click(object sender, EventArgs e)
        {
            var l = TicketReady(false);
            if (l == null) return;

            int n = _loadNumbers[l.Id];
            bool beforeTank = IsBeforeTankJob(l.JobId);
            if (!beforeTank)
                ApplyToPoints(l, 1.0);   // undoes an earlier direct-load correction
            Core.Database.Loads.SetCertified(l.Id, _ticketLb.Value,
                beforeTank ? 1.0 : FactorFor(l), LoadRecord.StatusWeighed, l.Flag);
            Props.WriteActivityLog($"Load {n} of {JobName(l.JobId)} ticket {_ticketLb.Value:F0} lb saved (monitor {l.MonitorLb:F0} lb)");

            LoadList(false);
            lblStatus.Text = IsBeforeTankJob(l.JobId)
                ? $"Truck {n} ticket saved. It is added to this job's ticket total; the pre-tank map is unchanged."
                : $"Load {n} ticket saved. The map is left as measured.";
        }

        private void btnCorrectLoad_Click(object sender, EventArgs e)
        {
            var l = TicketReady(true);
            if (l == null) return;

            int n = _loadNumbers[l.Id];
            double factor = FactorFor(l);
            int rows = ApplyToPoints(l, factor);
            Core.Database.Loads.SetCertified(l.Id, _ticketLb.Value, factor, LoadRecord.StatusCorrected, l.Flag);
            Props.WriteActivityLog($"Load {n} of {JobName(l.JobId)} corrected by {factor:F4} (ticket {_ticketLb.Value:F0} lb, monitor {l.MonitorLb:F0} lb, {rows} points)");

            LoadList(false);
            lblStatus.Text = $"Load {n} corrected by {factor:F4}: its map points and the job total were rescaled.";
        }

        // The span the load was weighed with, times the load's factor. Worked from
        // the load's own revision, not the latest, so a span already changed since
        // is not corrected twice.
        private void btnUpdateCal_Click(object sender, EventArgs e)
        {
            var selected = SelectedLoad;
            if (selected != null && IsBeforeTankJob(selected.JobId))
            {
                UpdateCalibrationFromJob(selected.JobId);
                return;
            }

            var l = TicketReady(true);
            if (l == null) return;

            int n = _loadNumbers[l.Id];
            double factor = FactorFor(l);
            int profileId = _jobs.TryGetValue(l.JobId, out var j) ? j.profileId : -1;
            var weighedWith = Core.Database.ConveyorConfigs.GetById(l.CalRev);
            var latest      = profileId > 0 ? Core.Database.ConveyorConfigs.GetLatest(profileId) : null;
            if (weighedWith == null || latest == null)
            {
                lblStatus.Text = "The calibration this load was weighed with is not on record, so the span can't be worked out.";
                return;
            }

            double oldSpan = weighedWith.SpanLbPerCount;
            double newSpan = oldSpan * factor;
            using (var dlg = new frmMsgBox(string.Format(Lang.lgUpdateCalPrompt,
                oldSpan.ToString("G5"), newSpan.ToString("G5"), ((factor - 1) * 100).ToString("+0.0;-0.0;0.0") + "%")))
            {
                dlg.ShowDialog(this);
                if (!dlg.Result) return;
            }

            var edited = new ConveyorConfig
            {
                ProfileId        = profileId,
                ZeroCounts       = latest.ZeroCounts,
                SpanLbPerCount   = newSpan,
                ZeroSetAt        = latest.ZeroSetAt,
                PulsesPerRev     = latest.PulsesPerRev,
                InchesPerPulse   = latest.InchesPerPulse,
                SectionLenIn     = latest.SectionLenIn,
                FlowThresholdLbS = latest.FlowThresholdLbS,
                BeltStopTimeoutS = latest.BeltStopTimeoutS,
                DelaySec         = latest.DelaySec
            };
            int revision = Core.SaveConveyorConfig(edited);

            ApplyToPoints(l, factor);
            Core.Database.Loads.SetCertified(l.Id, _ticketLb.Value, factor, LoadRecord.StatusCorrected, l.Flag);
            Props.WriteActivityLog($"Span updated from load {n} of {JobName(l.JobId)}: {oldSpan:G5} → {newSpan:G5} lb per count, revision {revision}");

            LoadList(false);
            lblStatus.Text = $"Span {oldSpan:G5} → {newSpan:G5}, calibration revision {revision}. Load {n} corrected by {factor:F4}.";
        }

        // ── Finished-job correction (scale before holding tank) ───────────────

        // Only once the job is finished and every truck record has its ticket.
        // The operator must also empty/clean out the tank so all crop from this
        // job is represented by those tickets.
        private bool CanCorrectJob(int jobId, out double ticketsLb, out double jobLb, out string why)
        {
            ticketsLb = 0;
            jobLb     = _jobs.TryGetValue(jobId, out var j) ? j.pounds : 0;
            why       = "";
            var loads = _loads.Where(x => x.JobId == jobId).ToList();

            if (jobId == Core.Collector.ActiveJobId)      { why = "Finish the job first.";                   return false; }
            if (loads.Any(x => !x.CertifiedLb.HasValue)) { why = "Every load of this job needs its ticket."; return false; }
            if (loads.Count == 0 || jobLb <= 0)          { why = "The job has no weight to correct.";        return false; }

            ticketsLb = loads.Sum(x => x.CertifiedLb.Value);
            return true;
        }

        private string BeforeTankStatus(int jobId)
        {
            var loads = _loads.Where(x => x.JobId == jobId).ToList();
            int withTicket = loads.Count(x => x.CertifiedLb.HasValue);
            double tickets = loads.Where(x => x.CertifiedLb.HasValue).Sum(x => x.CertifiedLb ?? 0);
            double measured = _jobs.TryGetValue(jobId, out var j) ? j.pounds : 0;
            string unit = Props.LoadUnit;

            string totals = $"{withTicket}/{loads.Count} tickets = {Props.DisplayLoad(tickets):F0} {unit}; "
                          + $"job measured {Props.DisplayLoad(measured):F0} {unit}.";
            CanCorrectJob(jobId, out _, out _, out string why);
            return "Scale is before the holding tank. Truck tickets are not matched to individual scale pounds. "
                 + totals + (why.Length > 0 ? " " + why : " Correct Job or Update Calibration can now use the ticket total.");
        }

        private void UpdateCalibrationFromJob(int jobId)
        {
            if (!CanCorrectJob(jobId, out double ticketsLb, out double jobLb, out string why))
            {
                lblStatus.Text = why;
                return;
            }

            int profileId = _jobs.TryGetValue(jobId, out var j) ? j.profileId : -1;
            var latest = profileId > 0 ? Core.Database.ConveyorConfigs.GetLatest(profileId) : null;
            if (latest == null || !latest.IsCalibrated)
            {
                lblStatus.Text = "The profile does not have a valid saved scale calibration.";
                return;
            }

            double factor = ticketsLb / jobLb;
            double oldSpan = latest.SpanLbPerCount;
            double newSpan = oldSpan * factor;
            using (var dlg = new frmMsgBox(string.Format(Lang.lgUpdateJobCalPrompt,
                oldSpan.ToString("G5"), newSpan.ToString("G5"),
                ((factor - 1) * 100).ToString("+0.0;-0.0;0.0") + "%")))
            {
                dlg.ShowDialog(this);
                if (!dlg.Result) return;
            }

            var edited = new ConveyorConfig
            {
                ProfileId        = profileId,
                ZeroCounts       = latest.ZeroCounts,
                SpanLbPerCount   = newSpan,
                ZeroSetAt        = latest.ZeroSetAt,
                PulsesPerRev     = latest.PulsesPerRev,
                InchesPerPulse   = latest.InchesPerPulse,
                SectionLenIn     = latest.SectionLenIn,
                FlowThresholdLbS = latest.FlowThresholdLbS,
                BeltStopTimeoutS = latest.BeltStopTimeoutS,
                DelaySec         = latest.DelaySec
            };
            int revision = Core.SaveConveyorConfig(edited);

            var (rows, total) = Core.Database.YieldData.RescaleJob(jobId, factor);
            Core.Collector.SyncTotalPounds(jobId, total);
            string name = JobName(jobId);
            Props.WriteActivityLog($"Span updated from finished job {name}: {oldSpan:G5} → {newSpan:G5} lb/count, "
                                 + $"factor {factor:F4}, tickets {ticketsLb:F0} lb, measured {jobLb:F0} lb, "
                                 + $"revision {revision}, {rows} points");

            LoadList(false);
            lblStatus.Text = $"{name}: span {oldSpan:G5} → {newSpan:G5}, revision {revision}; whole job corrected by {factor:F4}.";
        }

        // Measured against the job's current total, so pressing it again after a
        // correction finds a factor of 1 and changes nothing.
        private void btnCorrectJob_Click(object sender, EventArgs e)
        {
            var l = SelectedLoad;
            if (l == null) return;
            if (!CanCorrectJob(l.JobId, out double ticketsLb, out double jobLb, out string why))
            {
                lblStatus.Text = why;
                return;
            }

            double factor = ticketsLb / jobLb;
            string name = JobName(l.JobId);
            using (var dlg = new frmMsgBox(string.Format(Lang.lgCorrectJobPrompt, name,
                factor.ToString("F4") + " (" + ((factor - 1) * 100).ToString("+0.0;-0.0;0.0") + "%)")))
            {
                dlg.ShowDialog(this);
                if (!dlg.Result) return;
            }

            var (rows, total) = Core.Database.YieldData.RescaleJob(l.JobId, factor);
            Core.Collector.SyncTotalPounds(l.JobId, total);
            Props.WriteActivityLog($"Job {name} corrected by {factor:F4} (tickets {ticketsLb:F0} lb, job {jobLb:F0} lb, {rows} points)");

            LoadList(false);
            lblStatus.Text = $"{name} corrected by {factor:F4}: {Props.DisplayLoad(jobLb):F0} → {Props.DisplayLoad(total):F0} {Props.LoadUnit}.";
        }

        // ── Flag / Rename / Reopen / Delete ───────────────────────────────────

        private void SetFlag(string flag)
        {
            var l = SelectedLoad;
            if (l == null || l.Flag == flag) return;
            Core.Database.Loads.SetFlag(l.Id, flag);
            LoadList(true);
        }

        private void btnRename_Click(object sender, EventArgs e)
        {
            var l = SelectedLoad;
            if (l == null) return;
            using var kb = new frmKeyboard(l.Truck, Lang.lgTruck);
            if (kb.ShowDialog(this) != DialogResult.OK) return;
            Core.Database.Loads.Rename(l.Id, kb.ReturnValue.Trim());
            LoadList(true);
        }

        private void btnReopen_Click(object sender, EventArgs e)
        {
            var l = SelectedLoad;
            if (l == null) return;
            int n = _loadNumbers[l.Id];
            if (!Core.Collector.ReopenLoad(l.Id))
            {
                lblStatus.Text = "Only a load of the running job that is waiting for its ticket can be reopened, with no other load open.";
                return;
            }
            // JobStateChanged has already rebuilt the list.
            lblStatus.Text = $"Load {n} reopened: its weight carries on from {Props.DisplayLoad(l.MonitorLb):F0} {Props.LoadUnit}.";
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            var l = SelectedLoad;
            if (l == null || l.Status == LoadRecord.StatusActive) return;
            int n = _loadNumbers[l.Id];
            string name = JobName(l.JobId);
            using (var dlg = new frmMsgBox(string.Format(Lang.lgDeleteLoadPrompt, n, name)))
            {
                dlg.ShowDialog(this);
                if (!dlg.Result) return;
            }
            Core.Database.Loads.Delete(l.Id);
            Props.WriteActivityLog($"Load {n} of {name} deleted (id {l.Id})");
            LoadList(false);
        }

        // ── List drawing ──────────────────────────────────────────────────────

        private void lvLoads_DrawColumnHeader(object sender, DrawListViewColumnHeaderEventArgs e) => e.DrawDefault = true;

        // The load being filled stands out in green.
        private void lvLoads_DrawSubItem(object sender, DrawListViewSubItemEventArgs e)
        {
            bool selected = e.Item.Selected;
            bool filling  = e.ItemIndex < _loads.Count && _loads[e.ItemIndex].Status == LoadRecord.StatusActive;
            Color back = selected ? SystemColors.Highlight : lvLoads.BackColor;
            Color fore = selected ? SystemColors.HighlightText : filling ? OkabeIto.BluishGreen : lvLoads.ForeColor;
            using (var brush = new SolidBrush(back))
                e.Graphics.FillRectangle(brush, e.Bounds);
            var align = e.ColumnIndex >= 3 && e.ColumnIndex <= 5 ? TextFormatFlags.Right : TextFormatFlags.Left;
            TextRenderer.DrawText(e.Graphics, e.SubItem.Text, lvLoads.Font, e.Bounds, fore,
                align | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private void btnClose_Click(object sender, EventArgs e) => this.Close();
    }
}
