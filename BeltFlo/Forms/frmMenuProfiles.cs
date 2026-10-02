using System;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;
using BeltFlo.Classes;
using BeltFlo.Database;
using BeltFlo.Language;

namespace BeltFlo.Forms
{
    // The harvester. Root-crop harvesters are built for a fixed number of rows, so
    // the digging width — rows × row spacing — lives here rather than on a
    // swappable header. Numbers are plain boxes that open the numpad.
    public partial class frmMenuProfiles : Form
    {
        private bool _dragging;
        private Point _dragStart;
        private List<HarvesterProfile> _profiles;
        private int _editingId = -1;
        private bool _padOpen;

        // The profile being edited, in stored units (metres)
        private int    _rows          = 4;
        private double _spacingM      = 0.9144;
        private double _aheadOfPivotM = 0;
        private string _scaleLocation = HarvesterProfile.DirectToTruck;

        private const double M_PER_IN = 0.0254;
        private const double M_PER_FT = 0.3048;
        private const int    MaxRows  = 24;

        private static readonly Color ActiveColour   = Color.FromArgb(0, 80, 160);
        private static readonly Color InactiveColour = Color.FromArgb(60, 60, 60);

        public frmMenuProfiles()
        {
            InitializeComponent();
        }

        private void frmMenuProfiles_Load(object sender, EventArgs e)
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
            LoadList();
            int activeIdx = _profiles?.FindIndex(p => p.Id == Core.ActiveProfileId) ?? -1;
            if (activeIdx >= 0)
                lbProfiles.SelectedIndex = activeIdx;
            else if (lbProfiles.Items.Count > 0)
                lbProfiles.SelectedIndex = 0;
            else
                ClearEdit();
            this.Shown += frmMenuProfiles_Shown;
        }

        private void frmMenuProfiles_Shown(object sender, EventArgs e)
        {
            KeyboardHelper.Wire(this, txtProfileName, "Profile Name");
            KeyboardHelper.Wire(this, txtCombineId,   "Harvester ID");
            btnSave.Focus();
        }

        private void ApplyTheme()
        {
            var back = Properties.Settings.Default.MainBackColour;
            var fore = Properties.Settings.Default.MainForeColour;
            var ctrl = Color.FromArgb(60, 60, 60);
            pnlTitle.BackColor   = back;
            pnlContent.BackColor = back;
            lblTitle.ForeColor   = Color.FromArgb(180, 200, 220);
            lbProfiles.BackColor = ctrl;
            lbProfiles.ForeColor = fore;
            pnlEdit.BackColor    = back;
            foreach (Control c in pnlEdit.Controls)
            {
                c.ForeColor = fore;
                if (c is TextBox tb) { tb.BackColor = ctrl; tb.ForeColor = fore; }
            }
            foreach (var box in new[] { lblRowsVal, lblSpacingVal, lblPivotVal })
            {
                box.BackColor = ctrl;
                box.ForeColor = fore;
            }
            btnTruck.ForeColor  = Color.White;
            btnTank.ForeColor   = Color.White;
            btnNew.BackColor    = Color.FromArgb(60, 60, 60); btnNew.ForeColor    = Color.White;
            btnSave.BackColor   = Color.FromArgb(0, 90, 0);   btnSave.ForeColor   = Color.White;
            btnDelete.BackColor = Color.FromArgb(100, 0, 0);  btnDelete.ForeColor = Color.White;
            btnProfilesClose.BackColor = Color.FromArgb(60, 60, 60); btnProfilesClose.ForeColor = Color.White;
        }

        // ── Units ─────────────────────────────────────────────────────────────
        // Row spacing in inches or centimetres; the pivot distance in feet or metres,
        // as AgOpenGPS shows it.

        private static string SpacingUnit => Props.IsMetric ? "cm" : "in";
        private static string PivotUnit   => Props.IsMetric ? "m"  : "ft";

        private static double SpacingToDisplay(double m)   => Props.IsMetric ? m * 100.0 : m / M_PER_IN;
        private static double SpacingFromDisplay(double v) => Props.IsMetric ? v / 100.0 : v * M_PER_IN;
        private static double PivotToDisplay(double m)     => Props.IsMetric ? m : m / M_PER_FT;
        private static double PivotFromDisplay(double v)   => Props.IsMetric ? v : v * M_PER_FT;

        private static string WidthText(double m) => Props.IsMetric ? $"{m:F2} m" : $"{m / M_PER_FT:F1} ft";

        // ── List and edit fields ──────────────────────────────────────────────

        private void LoadList()
        {
            _profiles = Core.Database.Profiles.GetAll();
            lbProfiles.Items.Clear();
            foreach (var p in _profiles)
                lbProfiles.Items.Add($"{p.Name}  –  {p.Rows} × {SpacingToDisplay(p.RowSpacingM):F0} {SpacingUnit}  –  {WidthText(p.WidthM(0))}");
        }

        private void ClearEdit()
        {
            _editingId = -1;
            txtProfileName.Text = "";
            txtCombineId.Text   = "";
            var d = new HarvesterProfile();
            _rows          = d.Rows;
            _spacingM      = d.RowSpacingM;
            _aheadOfPivotM = d.AheadOfPivotM;
            _scaleLocation = d.ScaleLocation;
            ShowValues();
            lbProfiles.ClearSelected();
        }

        private void lbProfiles_SelectedIndexChanged(object sender, EventArgs e)
        {
            int idx = lbProfiles.SelectedIndex;
            if (idx < 0 || _profiles == null || idx >= _profiles.Count) return;
            var p = _profiles[idx];
            _editingId          = p.Id;
            txtProfileName.Text = p.Name;
            txtCombineId.Text   = p.HarvesterId;
            _rows          = p.Rows;
            _spacingM      = p.RowSpacingM;
            _aheadOfPivotM = p.AheadOfPivotM;
            _scaleLocation = p.ScaleLocation;
            ShowValues();
        }

        private void ShowValues()
        {
            lblRowsVal.Text     = _rows.ToString();
            lblSpacingVal.Text  = SpacingToDisplay(_spacingM).ToString("F1");
            lblSpacingUnit.Text = SpacingUnit;
            lblPivotVal.Text    = PivotToDisplay(_aheadOfPivotM).ToString(Props.IsMetric ? "F2" : "F1");
            lblPivotUnit.Text   = PivotUnit;

            bool beforeTank = _scaleLocation == HarvesterProfile.BeforeHoldingTank;
            btnTruck.BackColor = beforeTank ? InactiveColour : ActiveColour;
            btnTank.BackColor  = beforeTank ? ActiveColour   : InactiveColour;

            lblWidth.Text = string.Format(Lang.lgDiggingWidth, WidthText(_rows * _spacingM));
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

        private void lblRowsVal_Click(object sender, EventArgs e)
        {
            if (AskNumber(1, MaxRows, _rows, 0, "Rows", out double v))
            {
                _rows = (int)Math.Round(v);
                ShowValues();
            }
        }

        private void lblSpacingVal_Click(object sender, EventArgs e)
        {
            double min = Props.IsMetric ? 25 : 10;
            double max = Props.IsMetric ? 150 : 60;
            if (AskNumber(min, max, Math.Round(SpacingToDisplay(_spacingM), 1), 1, $"Row Spacing ({SpacingUnit})", out double v))
            {
                _spacingM = SpacingFromDisplay(v);
                ShowValues();
            }
        }

        // AgOpenGPS's pivot-to-implement distance: positive ahead of the pivot,
        // negative behind it, as on a towed harvester.
        private void lblPivotVal_Click(object sender, EventArgs e)
        {
            double min = Props.IsMetric ? -30 : -100;
            double max = Props.IsMetric ?  30 :  100;
            int    dec = Props.IsMetric ?   2 :    1;
            if (AskNumber(min, max, Math.Round(PivotToDisplay(_aheadOfPivotM), dec), dec,
                          $"Ahead of Pivot ({PivotUnit}), negative if behind", out double v))
            {
                _aheadOfPivotM = PivotFromDisplay(v);
                ShowValues();
            }
        }

        private void btnTruck_Click(object sender, EventArgs e) { _scaleLocation = HarvesterProfile.DirectToTruck; ShowValues(); }
        private void btnTank_Click(object sender, EventArgs e)  { _scaleLocation = HarvesterProfile.BeforeHoldingTank;  ShowValues(); }

        private void btnNew_Click(object sender, EventArgs e) => ClearEdit();

        private void btnSave_Click(object sender, EventArgs e)
        {
            string name = txtProfileName.Text.Trim();
            if (string.IsNullOrEmpty(name)) { Props.ShowMessage(Lang.lgEnterProfileName, "", 2000, true); return; }

            var p = new HarvesterProfile
            {
                Id            = _editingId,
                Name          = name,
                HarvesterId   = txtCombineId.Text.Trim(),
                Rows          = _rows,
                RowSpacingM   = _spacingM,
                AheadOfPivotM = _aheadOfPivotM,
                ScaleLocation = _scaleLocation
            };

            int savedId;
            if (_editingId < 0)
            {
                savedId = Core.Database.Profiles.Create(p);
                // Every profile carries a conveyor configuration from the start, so a
                // packet always has a revision to be checked against.
                Core.Database.ConveyorConfigs.Save(new ConveyorConfig { ProfileId = savedId });
            }
            else
            {
                Core.Database.Profiles.Update(p);
                savedId = _editingId;
            }

            // The active harvester's width, offset and scale location take effect now.
            if (savedId == Core.ActiveProfileId)
                Core.LoadJobConfig(Core.ActiveProfileId, Core.ActiveCropId, Core.ActiveRowsHarvested);

            Core.RaiseProfileListChanged();
            LoadList();

            // Keep the saved profile selected, found by id — the list is sorted by name.
            int idx = _profiles.FindIndex(x => x.Id == savedId);
            if (idx >= 0) lbProfiles.SelectedIndex = idx;
            else ClearEdit();
        }

        private void btnDelete_Click(object sender, EventArgs e)
        {
            if (_editingId < 0) return;
            if (_profiles.Count <= 1) { Props.ShowMessage(Lang.lgMustHaveOneProfile, "", 2000, true); return; }
            using var dlg = new frmMsgBox(Lang.lgDeleteProfilePrompt);
            dlg.ShowDialog(this);
            if (!dlg.Result) return;
            try { Core.Database.Profiles.Delete(_editingId); }
            catch (ItemInUseException) { Props.ShowMessage(Lang.lgItemInUseByJob, "", 3000, true); return; }
            Core.RaiseProfileListChanged();
            LoadList();
            ClearEdit();
        }

        private void btnProfilesClose_Click(object sender, EventArgs e) => this.Close();
    }
}
