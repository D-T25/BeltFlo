namespace ModuleSimulator
{
    partial class frmSimulator
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && components != null) components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            this.lblTitle           = new System.Windows.Forms.Label();
            this.lblLoadSlider      = new System.Windows.Forms.Label();
            this.trkLoad            = new System.Windows.Forms.TrackBar();
            this.lblBeltSlider      = new System.Windows.Forms.Label();
            this.trkBelt            = new System.Windows.Forms.TrackBar();
            this.chkSections        = new System.Windows.Forms.CheckBox();
            this.chkSineWave        = new System.Windows.Forms.CheckBox();
            this.lblVariationSlider = new System.Windows.Forms.Label();
            this.trkVariation       = new System.Windows.Forms.TrackBar();
            this.lblFaults          = new System.Windows.Forms.Label();
            this.chkModuleOffline   = new System.Windows.Forms.CheckBox();
            this.chkScaleFault      = new System.Windows.Forms.CheckBox();
            this.chkOverload        = new System.Windows.Forms.CheckBox();
            this.chkBeltStopped     = new System.Windows.Forms.CheckBox();
            this.chkBeltSensorDead  = new System.Windows.Forms.CheckBox();
            this.chkNotZeroed       = new System.Windows.Forms.CheckBox();
            this.chkIgnoreSettings  = new System.Windows.Forms.CheckBox();
            this.chkApplyCal        = new System.Windows.Forms.CheckBox();
            this.lblFlow            = new System.Windows.Forms.Label();
            this.lblTotal           = new System.Windows.Forms.Label();
            this.lblPulses          = new System.Windows.Forms.Label();
            this.lblSettings        = new System.Windows.Forms.Label();
            this.lblStatus          = new System.Windows.Forms.Label();
            ((System.ComponentModel.ISupportInitialize)(this.trkLoad)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkBelt)).BeginInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkVariation)).BeginInit();
            this.SuspendLayout();

            // lblTitle
            this.lblTitle.Text     = "BeltFlo Conveyor Simulator";
            this.lblTitle.Font     = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Bold);
            this.lblTitle.Location = new System.Drawing.Point(10, 10);
            this.lblTitle.AutoSize = true;

            // Section load. 6 lb on a 3 ft section at 180 ft/min is 12 lb/s — a
            // digger doing about 20 t/h.
            this.lblLoadSlider.Text     = "Load on weigh section (0–50 lb):";
            this.lblLoadSlider.Location = new System.Drawing.Point(10, 55);
            this.lblLoadSlider.AutoSize = true;

            this.trkLoad.Location      = new System.Drawing.Point(10, 75);
            this.trkLoad.Size          = new System.Drawing.Size(360, 45);
            this.trkLoad.Minimum       = 0;
            this.trkLoad.Maximum       = 500;
            this.trkLoad.Value         = 60;    // 6.0 lb
            this.trkLoad.TickFrequency = 50;

            // Belt speed
            this.lblBeltSlider.Text     = "Belt speed (0–300 ft/min):";
            this.lblBeltSlider.Location = new System.Drawing.Point(10, 130);
            this.lblBeltSlider.AutoSize = true;

            this.trkBelt.Location      = new System.Drawing.Point(10, 150);
            this.trkBelt.Size          = new System.Drawing.Size(360, 45);
            this.trkBelt.Minimum       = 0;
            this.trkBelt.Maximum       = 300;
            this.trkBelt.Value         = 180;
            this.trkBelt.TickFrequency = 30;

            // Harvesting checkbox — crop on the belt or not
            this.chkSections.Text          = "Harvesting (crop on the belt)";
            this.chkSections.Location      = new System.Drawing.Point(10, 207);
            this.chkSections.AutoSize      = true;
            this.chkSections.Checked       = false;
            this.chkSections.Font          = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.chkSections.ForeColor     = System.Drawing.Color.Red;
            this.chkSections.CheckedChanged += new System.EventHandler(this.chkSections_CheckedChanged);

            // Sine wave checkbox + variation
            this.chkSineWave.Text     = "Yield variation";
            this.chkSineWave.Location = new System.Drawing.Point(10, 235);
            this.chkSineWave.AutoSize = true;
            this.chkSineWave.Checked  = true;

            this.lblVariationSlider.Text     = "Variation: 5%";
            this.lblVariationSlider.Location = new System.Drawing.Point(200, 235);
            this.lblVariationSlider.AutoSize = true;

            this.trkVariation.Location      = new System.Drawing.Point(200, 252);
            this.trkVariation.Size          = new System.Drawing.Size(180, 45);
            this.trkVariation.Minimum       = 1;
            this.trkVariation.Maximum       = 50;
            this.trkVariation.Value         = 5;
            this.trkVariation.TickFrequency = 5;

            // Faults — one per way the PC app can lose the scale, so each branch of
            // its status bar can be reached without unplugging hardware.
            this.lblFaults.Text     = "Faults";
            this.lblFaults.Font     = new System.Drawing.Font("Microsoft Sans Serif", 10F, System.Drawing.FontStyle.Bold);
            this.lblFaults.Location = new System.Drawing.Point(10, 305);
            this.lblFaults.AutoSize = true;

            this.chkModuleOffline.Text     = "Module offline — send nothing";
            this.chkModuleOffline.Location = new System.Drawing.Point(10, 329);
            this.chkModuleOffline.AutoSize = true;

            this.chkScaleFault.Text     = "Scale fault flag — ScaleOK = 0";
            this.chkScaleFault.Location = new System.Drawing.Point(10, 353);
            this.chkScaleFault.AutoSize = true;

            this.chkOverload.Text     = "Scale overload flag — Overload = 1";
            this.chkOverload.Location = new System.Drawing.Point(10, 377);
            this.chkOverload.AutoSize = true;

            this.chkBeltStopped.Text     = "Belt stopped — pulses frozen";
            this.chkBeltStopped.Location = new System.Drawing.Point(10, 401);
            this.chkBeltStopped.AutoSize = true;

            this.chkBeltSensorDead.Text     = "Belt sensor dead — belt runs, no pulses";
            this.chkBeltSensorDead.Location = new System.Drawing.Point(10, 425);
            this.chkBeltSensorDead.AutoSize = true;

            this.chkNotZeroed.Text     = "Not zeroed — Tared = 0";
            this.chkNotZeroed.Location = new System.Drawing.Point(10, 449);
            this.chkNotZeroed.AutoSize = true;

            // Old firmware: ignores the settings message, so its checksum never matches
            this.chkIgnoreSettings.Text     = "Ignore settings — old firmware";
            this.chkIgnoreSettings.Location = new System.Drawing.Point(10, 473);
            this.chkIgnoreSettings.AutoSize = true;

            // Weigh with the app's zero and span, for testing Scale Calibration
            this.chkApplyCal.Text     = "Apply app zero and span (calibration test)";
            this.chkApplyCal.Location = new System.Drawing.Point(10, 497);
            this.chkApplyCal.AutoSize = true;

            // Readout labels
            this.lblFlow.Text     = "Flow: 0.00 lb/s";
            this.lblFlow.Font     = new System.Drawing.Font("Courier New", 11F);
            this.lblFlow.Location = new System.Drawing.Point(10, 531);
            this.lblFlow.AutoSize = true;

            this.lblTotal.Text     = "Total: 0.0 lb";
            this.lblTotal.Font     = new System.Drawing.Font("Courier New", 11F);
            this.lblTotal.Location = new System.Drawing.Point(10, 553);
            this.lblTotal.AutoSize = true;

            this.lblPulses.Text     = "Pulses: 0";
            this.lblPulses.Font     = new System.Drawing.Font("Courier New", 11F);
            this.lblPulses.Location = new System.Drawing.Point(10, 575);
            this.lblPulses.AutoSize = true;

            this.lblSettings.Text     = "Settings: none received";
            this.lblSettings.Font     = new System.Drawing.Font("Microsoft Sans Serif", 8.25F);
            this.lblSettings.Location = new System.Drawing.Point(10, 599);
            this.lblSettings.AutoSize = true;

            // Status
            this.lblStatus.Text      = "Initializing...";
            this.lblStatus.Location  = new System.Drawing.Point(10, 625);
            this.lblStatus.AutoSize  = true;
            this.lblStatus.ForeColor = System.Drawing.Color.DarkGreen;

            // Form
            this.ClientSize      = new System.Drawing.Size(400, 654);
            this.Controls.AddRange(new System.Windows.Forms.Control[] {
                lblTitle,
                lblLoadSlider, trkLoad,
                lblBeltSlider, trkBelt,
                chkSections,
                chkSineWave, lblVariationSlider, trkVariation,
                lblFaults, chkModuleOffline, chkScaleFault, chkOverload, chkBeltStopped, chkBeltSensorDead, chkNotZeroed,
                chkIgnoreSettings, chkApplyCal,
                lblFlow, lblTotal, lblPulses, lblSettings,
                lblStatus });
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.FixedSingle;
            this.MaximizeBox     = false;
            this.Name            = "frmSimulator";
            this.Text            = "BeltFlo Simulator";
            this.Icon            = System.Drawing.Icon.ExtractAssociatedIcon(System.Windows.Forms.Application.ExecutablePath);
            this.Load           += new System.EventHandler(this.frmSimulator_Load);

            ((System.ComponentModel.ISupportInitialize)(this.trkLoad)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkBelt)).EndInit();
            ((System.ComponentModel.ISupportInitialize)(this.trkVariation)).EndInit();
            this.ResumeLayout(false);
            this.PerformLayout();
        }

        private System.Windows.Forms.Label    lblTitle;
        private System.Windows.Forms.Label    lblLoadSlider;
        private System.Windows.Forms.TrackBar trkLoad;
        private System.Windows.Forms.Label    lblBeltSlider;
        private System.Windows.Forms.TrackBar trkBelt;
        private System.Windows.Forms.CheckBox chkSections;
        private System.Windows.Forms.CheckBox chkSineWave;
        private System.Windows.Forms.Label    lblVariationSlider;
        private System.Windows.Forms.TrackBar trkVariation;
        private System.Windows.Forms.Label    lblFaults;
        private System.Windows.Forms.CheckBox chkModuleOffline;
        private System.Windows.Forms.CheckBox chkScaleFault;
        private System.Windows.Forms.CheckBox chkOverload;
        private System.Windows.Forms.CheckBox chkBeltStopped;
        private System.Windows.Forms.CheckBox chkBeltSensorDead;
        private System.Windows.Forms.CheckBox chkNotZeroed;
        private System.Windows.Forms.CheckBox chkIgnoreSettings;
        private System.Windows.Forms.CheckBox chkApplyCal;
        private System.Windows.Forms.Label    lblFlow;
        private System.Windows.Forms.Label    lblTotal;
        private System.Windows.Forms.Label    lblPulses;
        private System.Windows.Forms.Label    lblSettings;
        private System.Windows.Forms.Label    lblStatus;
    }
}
