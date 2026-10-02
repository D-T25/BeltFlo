using BeltFlo.Language;

namespace BeltFlo.Forms
{
    partial class frmMenuProfiles
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.pnlTitle         = new System.Windows.Forms.Panel();
            this.lblTitle         = new System.Windows.Forms.Label();
            this.pnlContent       = new System.Windows.Forms.Panel();
            this.lbProfiles       = new System.Windows.Forms.ListBox();
            this.pnlEdit          = new System.Windows.Forms.Panel();
            this.lblProfileName   = new System.Windows.Forms.Label();
            this.txtProfileName   = new System.Windows.Forms.TextBox();
            this.lblCombineId     = new System.Windows.Forms.Label();
            this.txtCombineId     = new System.Windows.Forms.TextBox();
            this.lblRows          = new System.Windows.Forms.Label();
            this.lblRowsVal       = new System.Windows.Forms.Label();
            this.lblSpacing       = new System.Windows.Forms.Label();
            this.lblSpacingVal    = new System.Windows.Forms.Label();
            this.lblSpacingUnit   = new System.Windows.Forms.Label();
            this.lblPivot         = new System.Windows.Forms.Label();
            this.lblPivotVal      = new System.Windows.Forms.Label();
            this.lblPivotUnit     = new System.Windows.Forms.Label();
            this.lblScaleLocation = new System.Windows.Forms.Label();
            this.btnTruck         = new System.Windows.Forms.Button();
            this.btnTank          = new System.Windows.Forms.Button();
            this.lblWidth         = new System.Windows.Forms.Label();
            this.btnNew           = new System.Windows.Forms.Button();
            this.btnSave          = new System.Windows.Forms.Button();
            this.btnDelete        = new System.Windows.Forms.Button();
            this.btnProfilesClose = new System.Windows.Forms.Button();

            this.SuspendLayout();

            // ── Title bar ────────────────────────────────────────────────────
            this.pnlTitle.Dock   = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Height = 48;

            this.lblTitle.Text      = Lang.lgTitleProfiles;
            this.lblTitle.Font      = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(180, 200, 220);
            this.lblTitle.Dock      = System.Windows.Forms.DockStyle.Fill;
            this.lblTitle.AutoSize  = false;
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;

            this.pnlTitle.Controls.Add(this.lblTitle);

            // ── Content ───────────────────────────────────────────────────────
            this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;

            this.lbProfiles.Location = new System.Drawing.Point(4, 4);
            this.lbProfiles.Size     = new System.Drawing.Size(552, 130);
            this.lbProfiles.Font     = new System.Drawing.Font("Microsoft Sans Serif", 14.25F);
            this.lbProfiles.SelectedIndexChanged += new System.EventHandler(this.lbProfiles_SelectedIndexChanged);

            this.pnlEdit.Location = new System.Drawing.Point(4, 140);
            this.pnlEdit.Size     = new System.Drawing.Size(552, 280);

            var lf = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            var vf = new System.Drawing.Font("Microsoft Sans Serif", 14.25F);
            var bf = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);

            // Name
            this.lblProfileName.Text     = Lang.lgName; this.lblProfileName.Font = lf;
            this.lblProfileName.Location = new System.Drawing.Point(8, 4); this.lblProfileName.AutoSize = false; this.lblProfileName.Size = new System.Drawing.Size(220, 32);
            this.txtProfileName.Font     = vf;
            this.txtProfileName.Location = new System.Drawing.Point(236, 4); this.txtProfileName.Width = 310; this.txtProfileName.Height = 32;

            // Harvester ID
            this.lblCombineId.Text     = Lang.lgCombineId; this.lblCombineId.Font = lf;
            this.lblCombineId.Location = new System.Drawing.Point(8, 44); this.lblCombineId.AutoSize = false; this.lblCombineId.Size = new System.Drawing.Size(220, 32);
            this.txtCombineId.Font     = vf;
            this.txtCombineId.Location = new System.Drawing.Point(236, 44); this.txtCombineId.Width = 310; this.txtCombineId.Height = 32;

            // Numbers are plain boxes that open the numpad — no spinner arrows in the cab.
            // Rows
            this.lblRows.Text     = Lang.lgRows; this.lblRows.Font = lf;
            this.lblRows.Location = new System.Drawing.Point(8, 84); this.lblRows.AutoSize = false; this.lblRows.Size = new System.Drawing.Size(220, 32);
            this.lblRowsVal.Font        = vf;
            this.lblRowsVal.Location    = new System.Drawing.Point(236, 84); this.lblRowsVal.AutoSize = false; this.lblRowsVal.Size = new System.Drawing.Size(120, 32);
            this.lblRowsVal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblRowsVal.TextAlign   = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblRowsVal.Cursor      = System.Windows.Forms.Cursors.Hand;
            this.lblRowsVal.Click      += new System.EventHandler(this.lblRowsVal_Click);

            // Row spacing
            this.lblSpacing.Text     = Lang.lgRowSpacing; this.lblSpacing.Font = lf;
            this.lblSpacing.Location = new System.Drawing.Point(8, 124); this.lblSpacing.AutoSize = false; this.lblSpacing.Size = new System.Drawing.Size(220, 32);
            this.lblSpacingVal.Font        = vf;
            this.lblSpacingVal.Location    = new System.Drawing.Point(236, 124); this.lblSpacingVal.AutoSize = false; this.lblSpacingVal.Size = new System.Drawing.Size(120, 32);
            this.lblSpacingVal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblSpacingVal.TextAlign   = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblSpacingVal.Cursor      = System.Windows.Forms.Cursors.Hand;
            this.lblSpacingVal.Click      += new System.EventHandler(this.lblSpacingVal_Click);
            this.lblSpacingUnit.Font     = vf;
            this.lblSpacingUnit.Location = new System.Drawing.Point(364, 124); this.lblSpacingUnit.AutoSize = false; this.lblSpacingUnit.Size = new System.Drawing.Size(60, 32);
            this.lblSpacingUnit.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Ahead of pivot (AgOpenGPS's pivot-to-implement distance)
            this.lblPivot.Text     = Lang.lgAheadOfPivot; this.lblPivot.Font = lf;
            this.lblPivot.Location = new System.Drawing.Point(8, 164); this.lblPivot.AutoSize = false; this.lblPivot.Size = new System.Drawing.Size(220, 32);
            this.lblPivotVal.Font        = vf;
            this.lblPivotVal.Location    = new System.Drawing.Point(236, 164); this.lblPivotVal.AutoSize = false; this.lblPivotVal.Size = new System.Drawing.Size(120, 32);
            this.lblPivotVal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblPivotVal.TextAlign   = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblPivotVal.Cursor      = System.Windows.Forms.Cursors.Hand;
            this.lblPivotVal.Click      += new System.EventHandler(this.lblPivotVal_Click);
            this.lblPivotUnit.Font     = vf;
            this.lblPivotUnit.Location = new System.Drawing.Point(364, 164); this.lblPivotUnit.AutoSize = false; this.lblPivotUnit.Size = new System.Drawing.Size(60, 32);
            this.lblPivotUnit.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            // Scale arrangement: direct to truck, or before an on-machine holding tank.
            this.lblScaleLocation.Text     = Lang.lgScalePosition; this.lblScaleLocation.Font = lf;
            this.lblScaleLocation.Location = new System.Drawing.Point(8, 206); this.lblScaleLocation.AutoSize = false; this.lblScaleLocation.Size = new System.Drawing.Size(184, 32);
            this.btnTruck.Text      = Lang.lgDirectToTruck; this.btnTruck.Font = bf; this.btnTruck.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTruck.FlatAppearance.BorderSize = 0;
            this.btnTruck.Location  = new System.Drawing.Point(200, 204); this.btnTruck.Size = new System.Drawing.Size(165, 36);
            this.btnTruck.Click    += new System.EventHandler(this.btnTruck_Click);
            this.btnTank.Text       = Lang.lgBeforeTank; this.btnTank.Font = bf; this.btnTank.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnTank.FlatAppearance.BorderSize = 0;
            this.btnTank.Location   = new System.Drawing.Point(373, 204); this.btnTank.Size = new System.Drawing.Size(173, 36);
            this.btnTank.Click     += new System.EventHandler(this.btnTank_Click);

            // Resulting digging width
            this.lblWidth.Font     = vf;
            this.lblWidth.Location = new System.Drawing.Point(8, 248); this.lblWidth.AutoSize = false; this.lblWidth.Size = new System.Drawing.Size(538, 28);
            this.lblWidth.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

            this.pnlEdit.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblProfileName, txtProfileName, lblCombineId, txtCombineId,
                lblRows, lblRowsVal, lblSpacing, lblSpacingVal, lblSpacingUnit,
                lblPivot, lblPivotVal, lblPivotUnit,
                lblScaleLocation, btnTruck, btnTank, lblWidth });

            this.pnlContent.Controls.Add(this.lbProfiles);
            this.pnlContent.Controls.Add(this.pnlEdit);

            // Buttons
            this.btnNew.Text      = Lang.lgNew;    this.btnNew.Font    = bf; this.btnNew.FlatStyle    = System.Windows.Forms.FlatStyle.Flat;
            this.btnNew.Location  = new System.Drawing.Point(8, 428); this.btnNew.Size = new System.Drawing.Size(130, 44);
            this.btnNew.Click    += new System.EventHandler(this.btnNew_Click);

            this.btnSave.Text      = Lang.lgSave;   this.btnSave.Font   = bf; this.btnSave.FlatStyle   = System.Windows.Forms.FlatStyle.Flat;
            this.btnSave.Location  = new System.Drawing.Point(146, 428); this.btnSave.Size = new System.Drawing.Size(130, 44);
            this.btnSave.Click    += new System.EventHandler(this.btnSave_Click);

            this.btnDelete.Text      = Lang.lgDelete; this.btnDelete.Font = bf; this.btnDelete.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnDelete.Location  = new System.Drawing.Point(284, 428); this.btnDelete.Size = new System.Drawing.Size(130, 44);
            this.btnDelete.Click    += new System.EventHandler(this.btnDelete_Click);

            this.btnProfilesClose.Text      = Lang.lgClose; this.btnProfilesClose.Font = bf; this.btnProfilesClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnProfilesClose.Location  = new System.Drawing.Point(422, 428); this.btnProfilesClose.Size = new System.Drawing.Size(130, 44);
            this.btnProfilesClose.Click    += new System.EventHandler(this.btnProfilesClose_Click);

            this.pnlContent.Controls.AddRange(new System.Windows.Forms.Control[] {
                btnNew, btnSave, btnDelete, btnProfilesClose });

            // ── Form ──────────────────────────────────────────────────────────
            this.ClientSize      = new System.Drawing.Size(564, 532);
            this.MinimumSize     = new System.Drawing.Size(564, 532);
            this.MaximumSize     = new System.Drawing.Size(564, 532);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Padding         = new System.Windows.Forms.Padding(2);
            this.BackColor       = System.Drawing.Color.White;
            this.TopMost         = true;
            this.ShowInTaskbar   = false;
            this.StartPosition   = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Font            = new System.Drawing.Font("Microsoft Sans Serif", 14.25F);
            this.Name            = "frmMenuProfiles";
            this.Text            = "Profiles";
            this.Controls.Add(this.pnlContent);
            this.Controls.Add(this.pnlTitle);
            this.Load += new System.EventHandler(this.frmMenuProfiles_Load);

            this.ResumeLayout(false);
        }

        private System.Windows.Forms.Panel   pnlTitle;
        private System.Windows.Forms.Label   lblTitle;
        private System.Windows.Forms.Panel   pnlContent;
        private System.Windows.Forms.ListBox lbProfiles;
        private System.Windows.Forms.Panel   pnlEdit;
        private System.Windows.Forms.Label   lblProfileName;
        private System.Windows.Forms.TextBox txtProfileName;
        private System.Windows.Forms.Label   lblCombineId;
        private System.Windows.Forms.TextBox txtCombineId;
        private System.Windows.Forms.Label   lblRows;
        private System.Windows.Forms.Label   lblRowsVal;
        private System.Windows.Forms.Label   lblSpacing;
        private System.Windows.Forms.Label   lblSpacingVal;
        private System.Windows.Forms.Label   lblSpacingUnit;
        private System.Windows.Forms.Label   lblPivot;
        private System.Windows.Forms.Label   lblPivotVal;
        private System.Windows.Forms.Label   lblPivotUnit;
        private System.Windows.Forms.Label   lblScaleLocation;
        private System.Windows.Forms.Button  btnTruck;
        private System.Windows.Forms.Button  btnTank;
        private System.Windows.Forms.Label   lblWidth;
        private System.Windows.Forms.Button  btnNew;
        private System.Windows.Forms.Button  btnSave;
        private System.Windows.Forms.Button  btnDelete;
        private System.Windows.Forms.Button  btnProfilesClose;
    }
}
