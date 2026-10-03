using BeltFlo.Language;

namespace BeltFlo.Forms
{
    partial class frmMenuSettings
    {
        private System.ComponentModel.IContainer components = null;

        protected override void Dispose(bool disposing)
        {
            if (disposing && (components != null))
                components.Dispose();
            base.Dispose(disposing);
        }

        private void InitializeComponent()
        {
            System.ComponentModel.ComponentResourceManager resources = new System.ComponentModel.ComponentResourceManager(typeof(frmMenuSettings));
            this.components = new System.ComponentModel.Container();
            this.pnlTitle = new System.Windows.Forms.Panel();
            this.lblTitle = new System.Windows.Forms.Label();
            this.pnlContent = new System.Windows.Forms.Panel();
            this.lblWifiInfo = new System.Windows.Forms.Label();
            this.lblUnits = new System.Windows.Forms.Label();
            this.btnImperial = new System.Windows.Forms.Button();
            this.btnMetric = new System.Windows.Forms.Button();
            this.lblYieldUnits = new System.Windows.Forms.Label();
            this.btnYieldLb = new System.Windows.Forms.Button();
            this.btnYieldCwt = new System.Windows.Forms.Button();
            this.btnYieldTons = new System.Windows.Forms.Button();
            this.lblAogYieldRange = new System.Windows.Forms.Label();
            this.btnYieldLow = new System.Windows.Forms.Button();
            this.btnYieldHigh = new System.Windows.Forms.Button();
            this.lblNetwork = new System.Windows.Forms.Label();
            this.btnEthernet = new System.Windows.Forms.Button();
            this.btnCAN = new System.Windows.Forms.Button();
            this.lblCanDriver = new System.Windows.Forms.Label();
            this.cbCanDriver = new System.Windows.Forms.ComboBox();
            this.lblCanPort = new System.Windows.Forms.Label();
            this.cbCanPort = new System.Windows.Forms.ComboBox();
            this.btnRescanPorts = new System.Windows.Forms.Button();
            this.lblCanStatus = new System.Windows.Forms.Label();
            this.tmrCanStatus = new System.Windows.Forms.Timer(this.components);
            this.lblResumeJob = new System.Windows.Forms.Label();
            this.btnResumeOn = new System.Windows.Forms.Button();
            this.btnResumeOff = new System.Windows.Forms.Button();
            this.lblAutoResume = new System.Windows.Forms.Label();
            this.btnAutoResumeOn = new System.Windows.Forms.Button();
            this.btnAutoResumeOff = new System.Windows.Forms.Button();
            this.lblTruckFull = new System.Windows.Forms.Label();
            this.lblTruckFullVal = new System.Windows.Forms.Label();
            this.lblTruckFullUnit = new System.Windows.Forms.Label();
            this.btnSaveSettings = new System.Windows.Forms.Button();
            this.btnSettingsClose = new System.Windows.Forms.Button();
            this.pnlTitle.SuspendLayout();
            this.pnlContent.SuspendLayout();
            this.SuspendLayout();
            // 
            // pnlTitle
            // 
            this.pnlTitle.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.pnlTitle.Controls.Add(this.lblTitle);
            this.pnlTitle.Dock = System.Windows.Forms.DockStyle.Top;
            this.pnlTitle.Location = new System.Drawing.Point(2, 2);
            this.pnlTitle.Name = "pnlTitle";
            this.pnlTitle.Size = new System.Drawing.Size(560, 48);
            this.pnlTitle.TabIndex = 0;
            // 
            // lblTitle
            // 
            this.lblTitle.Dock = System.Windows.Forms.DockStyle.Fill;
            this.lblTitle.Font = new System.Drawing.Font("Microsoft Sans Serif", 18F, System.Drawing.FontStyle.Bold);
            this.lblTitle.ForeColor = System.Drawing.Color.FromArgb(((int)(((byte)(180)))), ((int)(((byte)(200)))), ((int)(((byte)(220)))));
            this.lblTitle.Location = new System.Drawing.Point(0, 0);
            this.lblTitle.Name = "lblTitle";
            this.lblTitle.Size = new System.Drawing.Size(560, 48);
            this.lblTitle.TabIndex = 0;
            this.lblTitle.Text = "Settings";
            this.lblTitle.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            //
            // pnlContent
            //
            this.pnlContent.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(45)))), ((int)(((byte)(45)))), ((int)(((byte)(45)))));
            this.pnlContent.Controls.Add(this.lblWifiInfo);
            this.pnlContent.Controls.Add(this.lblUnits);
            this.pnlContent.Controls.Add(this.btnImperial);
            this.pnlContent.Controls.Add(this.btnMetric);
            this.pnlContent.Controls.Add(this.lblYieldUnits);
            this.pnlContent.Controls.Add(this.btnYieldLb);
            this.pnlContent.Controls.Add(this.btnYieldCwt);
            this.pnlContent.Controls.Add(this.btnYieldTons);
            this.pnlContent.Controls.Add(this.lblAogYieldRange);
            this.pnlContent.Controls.Add(this.btnYieldLow);
            this.pnlContent.Controls.Add(this.btnYieldHigh);
            this.pnlContent.Controls.Add(this.lblNetwork);
            this.pnlContent.Controls.Add(this.btnEthernet);
            this.pnlContent.Controls.Add(this.btnCAN);
            this.pnlContent.Controls.Add(this.lblCanDriver);
            this.pnlContent.Controls.Add(this.cbCanDriver);
            this.pnlContent.Controls.Add(this.lblCanPort);
            this.pnlContent.Controls.Add(this.cbCanPort);
            this.pnlContent.Controls.Add(this.btnRescanPorts);
            this.pnlContent.Controls.Add(this.lblCanStatus);
            this.pnlContent.Controls.Add(this.lblResumeJob);
            this.pnlContent.Controls.Add(this.btnResumeOn);
            this.pnlContent.Controls.Add(this.btnResumeOff);
            this.pnlContent.Controls.Add(this.lblAutoResume);
            this.pnlContent.Controls.Add(this.btnAutoResumeOn);
            this.pnlContent.Controls.Add(this.btnAutoResumeOff);
            this.pnlContent.Controls.Add(this.lblTruckFull);
            this.pnlContent.Controls.Add(this.lblTruckFullVal);
            this.pnlContent.Controls.Add(this.lblTruckFullUnit);
            this.pnlContent.Controls.Add(this.btnSaveSettings);
            this.pnlContent.Controls.Add(this.btnSettingsClose);
            this.pnlContent.Dock = System.Windows.Forms.DockStyle.Fill;
            this.pnlContent.Location = new System.Drawing.Point(2, 50);
            this.pnlContent.Name = "pnlContent";
            this.pnlContent.Size = new System.Drawing.Size(560, 736);
            this.pnlContent.TabIndex = 1;
            // 
            // lblWifiInfo
            // 
            this.lblWifiInfo.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F);
            this.lblWifiInfo.ForeColor = System.Drawing.Color.Silver;
            this.lblWifiInfo.Location = new System.Drawing.Point(8, 362);
            this.lblWifiInfo.Name = "lblWifiInfo";
            this.lblWifiInfo.Size = new System.Drawing.Size(544, 72);
            this.lblWifiInfo.TabIndex = 9;
            this.lblWifiInfo.Text = "Connect the PC to the module\'s WiFi access point, a WiFi network, or by wired Eth" +
    "ernet (W5500). Module setup page: http://192.168.200.1 (via the module\'s access " +
    "point).";
            this.lblWifiInfo.TextAlign = System.Drawing.ContentAlignment.MiddleLeft;
            // 
            // lblUnits
            // 
            this.lblUnits.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblUnits.ForeColor = System.Drawing.Color.Silver;
            this.lblUnits.Location = new System.Drawing.Point(8, 10);
            this.lblUnits.Name = "lblUnits";
            this.lblUnits.Size = new System.Drawing.Size(300, 26);
            this.lblUnits.TabIndex = 0;
            this.lblUnits.Text = "Units";
            // 
            // btnImperial
            // 
            this.btnImperial.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnImperial.FlatAppearance.BorderSize = 0;
            this.btnImperial.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnImperial.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnImperial.ForeColor = System.Drawing.Color.White;
            this.btnImperial.Location = new System.Drawing.Point(8, 40);
            this.btnImperial.Name = "btnImperial";
            this.btnImperial.Size = new System.Drawing.Size(267, 48);
            this.btnImperial.TabIndex = 1;
            this.btnImperial.Text = global::BeltFlo.Language.Lang.lgImperial;
            this.btnImperial.UseVisualStyleBackColor = false;
            this.btnImperial.Click += new System.EventHandler(this.btnImperial_Click);
            // 
            // btnMetric
            // 
            this.btnMetric.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnMetric.FlatAppearance.BorderSize = 0;
            this.btnMetric.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnMetric.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnMetric.ForeColor = System.Drawing.Color.White;
            this.btnMetric.Location = new System.Drawing.Point(285, 40);
            this.btnMetric.Name = "btnMetric";
            this.btnMetric.Size = new System.Drawing.Size(267, 48);
            this.btnMetric.TabIndex = 2;
            this.btnMetric.Text = global::BeltFlo.Language.Lang.lgMetric;
            this.btnMetric.UseVisualStyleBackColor = false;
            this.btnMetric.Click += new System.EventHandler(this.btnMetric_Click);
            //
            // lblYieldUnits
            //
            this.lblYieldUnits.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblYieldUnits.ForeColor = System.Drawing.Color.Silver;
            this.lblYieldUnits.Location = new System.Drawing.Point(8, 100);
            this.lblYieldUnits.Name = "lblYieldUnits";
            this.lblYieldUnits.Size = new System.Drawing.Size(400, 26);
            this.lblYieldUnits.TabIndex = 27;
            this.lblYieldUnits.Text = global::BeltFlo.Language.Lang.lgYieldUnits;
            //
            // btnYieldLb
            //
            this.btnYieldLb.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnYieldLb.FlatAppearance.BorderSize = 0;
            this.btnYieldLb.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnYieldLb.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Bold);
            this.btnYieldLb.ForeColor = System.Drawing.Color.White;
            this.btnYieldLb.Location = new System.Drawing.Point(8, 130);
            this.btnYieldLb.Name = "btnYieldLb";
            this.btnYieldLb.Size = new System.Drawing.Size(172, 48);
            this.btnYieldLb.TabIndex = 28;
            this.btnYieldLb.Text = global::BeltFlo.Language.Lang.lgLbPerAc;
            this.btnYieldLb.UseVisualStyleBackColor = false;
            this.btnYieldLb.Click += new System.EventHandler(this.btnYieldLb_Click);
            //
            // btnYieldCwt
            //
            this.btnYieldCwt.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnYieldCwt.FlatAppearance.BorderSize = 0;
            this.btnYieldCwt.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnYieldCwt.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Bold);
            this.btnYieldCwt.ForeColor = System.Drawing.Color.White;
            this.btnYieldCwt.Location = new System.Drawing.Point(190, 130);
            this.btnYieldCwt.Name = "btnYieldCwt";
            this.btnYieldCwt.Size = new System.Drawing.Size(172, 48);
            this.btnYieldCwt.TabIndex = 29;
            this.btnYieldCwt.Text = global::BeltFlo.Language.Lang.lgCwtPerAc;
            this.btnYieldCwt.UseVisualStyleBackColor = false;
            this.btnYieldCwt.Click += new System.EventHandler(this.btnYieldCwt_Click);
            //
            // btnYieldTons
            //
            this.btnYieldTons.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnYieldTons.FlatAppearance.BorderSize = 0;
            this.btnYieldTons.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnYieldTons.Font = new System.Drawing.Font("Microsoft Sans Serif", 13F, System.Drawing.FontStyle.Bold);
            this.btnYieldTons.ForeColor = System.Drawing.Color.White;
            this.btnYieldTons.Location = new System.Drawing.Point(372, 130);
            this.btnYieldTons.Name = "btnYieldTons";
            this.btnYieldTons.Size = new System.Drawing.Size(180, 48);
            this.btnYieldTons.TabIndex = 30;
            this.btnYieldTons.Text = global::BeltFlo.Language.Lang.lgTonsPerAc;
            this.btnYieldTons.UseVisualStyleBackColor = false;
            this.btnYieldTons.Click += new System.EventHandler(this.btnYieldTons_Click);
            //
            // lblAogYieldRange
            //
            this.lblAogYieldRange.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblAogYieldRange.ForeColor = System.Drawing.Color.Silver;
            this.lblAogYieldRange.Location = new System.Drawing.Point(8, 190);
            this.lblAogYieldRange.Name = "lblAogYieldRange";
            this.lblAogYieldRange.Size = new System.Drawing.Size(400, 26);
            this.lblAogYieldRange.TabIndex = 31;
            this.lblAogYieldRange.Text = global::BeltFlo.Language.Lang.lgAogYieldRange;
            //
            // btnYieldLow
            //
            this.btnYieldLow.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnYieldLow.FlatAppearance.BorderSize = 0;
            this.btnYieldLow.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnYieldLow.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.5F, System.Drawing.FontStyle.Bold);
            this.btnYieldLow.ForeColor = System.Drawing.Color.White;
            this.btnYieldLow.Location = new System.Drawing.Point(8, 220);
            this.btnYieldLow.Name = "btnYieldLow";
            this.btnYieldLow.Size = new System.Drawing.Size(267, 48);
            this.btnYieldLow.TabIndex = 32;
            this.btnYieldLow.Text = "Low";
            this.btnYieldLow.UseVisualStyleBackColor = false;
            this.btnYieldLow.Click += new System.EventHandler(this.btnYieldLow_Click);
            //
            // btnYieldHigh
            //
            this.btnYieldHigh.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnYieldHigh.FlatAppearance.BorderSize = 0;
            this.btnYieldHigh.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnYieldHigh.Font = new System.Drawing.Font("Microsoft Sans Serif", 12.5F, System.Drawing.FontStyle.Bold);
            this.btnYieldHigh.ForeColor = System.Drawing.Color.White;
            this.btnYieldHigh.Location = new System.Drawing.Point(285, 220);
            this.btnYieldHigh.Name = "btnYieldHigh";
            this.btnYieldHigh.Size = new System.Drawing.Size(267, 48);
            this.btnYieldHigh.TabIndex = 33;
            this.btnYieldHigh.Text = "High";
            this.btnYieldHigh.UseVisualStyleBackColor = false;
            this.btnYieldHigh.Click += new System.EventHandler(this.btnYieldHigh_Click);
            // 
            // lblNetwork
            // 
            this.lblNetwork.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblNetwork.ForeColor = System.Drawing.Color.Silver;
            this.lblNetwork.Location = new System.Drawing.Point(8, 272);
            this.lblNetwork.Name = "lblNetwork";
            this.lblNetwork.Size = new System.Drawing.Size(400, 26);
            this.lblNetwork.TabIndex = 6;
            this.lblNetwork.Text = "Module Communication";
            // 
            // btnEthernet
            // 
            this.btnEthernet.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnEthernet.FlatAppearance.BorderSize = 0;
            this.btnEthernet.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnEthernet.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnEthernet.ForeColor = System.Drawing.Color.White;
            this.btnEthernet.Location = new System.Drawing.Point(8, 302);
            this.btnEthernet.Name = "btnEthernet";
            this.btnEthernet.Size = new System.Drawing.Size(267, 48);
            this.btnEthernet.TabIndex = 7;
            this.btnEthernet.Text = global::BeltFlo.Language.Lang.lgWiFi;
            this.btnEthernet.UseVisualStyleBackColor = false;
            this.btnEthernet.Click += new System.EventHandler(this.btnEthernet_Click);
            // 
            // btnCAN
            // 
            this.btnCAN.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnCAN.FlatAppearance.BorderSize = 0;
            this.btnCAN.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnCAN.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnCAN.ForeColor = System.Drawing.Color.White;
            this.btnCAN.Location = new System.Drawing.Point(285, 302);
            this.btnCAN.Name = "btnCAN";
            this.btnCAN.Size = new System.Drawing.Size(267, 48);
            this.btnCAN.TabIndex = 8;
            this.btnCAN.Text = "CAN";
            this.btnCAN.UseVisualStyleBackColor = false;
            this.btnCAN.Click += new System.EventHandler(this.btnCAN_Click);
            // 
            // lblCanDriver
            // 
            this.lblCanDriver.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblCanDriver.ForeColor = System.Drawing.Color.Silver;
            this.lblCanDriver.Location = new System.Drawing.Point(8, 362);
            this.lblCanDriver.Name = "lblCanDriver";
            this.lblCanDriver.Size = new System.Drawing.Size(267, 26);
            this.lblCanDriver.TabIndex = 10;
            this.lblCanDriver.Text = "CAN Driver";
            // 
            // cbCanDriver
            // 
            this.cbCanDriver.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbCanDriver.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F);
            this.cbCanDriver.Items.AddRange(new object[] {
            "SLCAN",
            "InnoMaker",
            "PCAN"});
            this.cbCanDriver.Location = new System.Drawing.Point(8, 394);
            this.cbCanDriver.Name = "cbCanDriver";
            this.cbCanDriver.Size = new System.Drawing.Size(267, 32);
            this.cbCanDriver.TabIndex = 11;
            // 
            // lblCanPort
            // 
            this.lblCanPort.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblCanPort.ForeColor = System.Drawing.Color.Silver;
            this.lblCanPort.Location = new System.Drawing.Point(285, 362);
            this.lblCanPort.Name = "lblCanPort";
            this.lblCanPort.Size = new System.Drawing.Size(267, 26);
            this.lblCanPort.TabIndex = 12;
            this.lblCanPort.Text = "COM Port";
            // 
            // cbCanPort
            // 
            this.cbCanPort.DropDownStyle = System.Windows.Forms.ComboBoxStyle.DropDownList;
            this.cbCanPort.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F);
            this.cbCanPort.Location = new System.Drawing.Point(285, 394);
            this.cbCanPort.Name = "cbCanPort";
            this.cbCanPort.Size = new System.Drawing.Size(195, 32);
            this.cbCanPort.TabIndex = 13;
            // 
            // btnRescanPorts
            // 
            this.btnRescanPorts.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(80)))), ((int)(((byte)(80)))), ((int)(((byte)(80)))));
            this.btnRescanPorts.FlatAppearance.BorderSize = 0;
            this.btnRescanPorts.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnRescanPorts.Font = new System.Drawing.Font("Segoe UI", 11.25F, System.Drawing.FontStyle.Regular, System.Drawing.GraphicsUnit.Point, ((byte)(0)));
            this.btnRescanPorts.ForeColor = System.Drawing.Color.White;
            this.btnRescanPorts.Image = ((System.Drawing.Image)(resources.GetObject("btnRescanPorts.Image")));
            this.btnRescanPorts.Location = new System.Drawing.Point(488, 390);
            this.btnRescanPorts.Name = "btnRescanPorts";
            this.btnRescanPorts.Size = new System.Drawing.Size(64, 40);
            this.btnRescanPorts.TabIndex = 14;
            this.btnRescanPorts.TextAlign = System.Drawing.ContentAlignment.TopCenter;
            this.btnRescanPorts.UseVisualStyleBackColor = false;
            this.btnRescanPorts.Click += new System.EventHandler(this.btnRescanPorts_Click);
            //
            // lblCanStatus
            //
            this.lblCanStatus.Font = new System.Drawing.Font("Microsoft Sans Serif", 11F, System.Drawing.FontStyle.Bold);
            this.lblCanStatus.ForeColor = System.Drawing.Color.Silver;
            this.lblCanStatus.Location = new System.Drawing.Point(8, 428);
            this.lblCanStatus.Name = "lblCanStatus";
            this.lblCanStatus.Size = new System.Drawing.Size(400, 18);
            this.lblCanStatus.TabIndex = 20;
            this.lblCanStatus.Text = "Adapter: --";
            //
            // tmrCanStatus
            //
            this.tmrCanStatus.Enabled = true;
            this.tmrCanStatus.Interval = 500;
            this.tmrCanStatus.Tick += new System.EventHandler(this.tmrCanStatus_Tick);
            //
            // lblResumeJob
            //
            this.lblResumeJob.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblResumeJob.ForeColor = System.Drawing.Color.Silver;
            this.lblResumeJob.Location = new System.Drawing.Point(8, 446);
            this.lblResumeJob.Name = "lblResumeJob";
            this.lblResumeJob.Size = new System.Drawing.Size(400, 26);
            this.lblResumeJob.TabIndex = 15;
            this.lblResumeJob.Text = "Resume Job on Start";
            // 
            // btnResumeOn
            // 
            this.btnResumeOn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnResumeOn.FlatAppearance.BorderSize = 0;
            this.btnResumeOn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResumeOn.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnResumeOn.ForeColor = System.Drawing.Color.White;
            this.btnResumeOn.Location = new System.Drawing.Point(8, 476);
            this.btnResumeOn.Name = "btnResumeOn";
            this.btnResumeOn.Size = new System.Drawing.Size(267, 48);
            this.btnResumeOn.TabIndex = 16;
            this.btnResumeOn.Text = global::BeltFlo.Language.Lang.lgOn;
            this.btnResumeOn.UseVisualStyleBackColor = false;
            this.btnResumeOn.Click += new System.EventHandler(this.btnResumeOn_Click);
            // 
            // btnResumeOff
            // 
            this.btnResumeOff.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnResumeOff.FlatAppearance.BorderSize = 0;
            this.btnResumeOff.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnResumeOff.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnResumeOff.ForeColor = System.Drawing.Color.White;
            this.btnResumeOff.Location = new System.Drawing.Point(285, 476);
            this.btnResumeOff.Name = "btnResumeOff";
            this.btnResumeOff.Size = new System.Drawing.Size(267, 48);
            this.btnResumeOff.TabIndex = 17;
            this.btnResumeOff.Text = global::BeltFlo.Language.Lang.lgOff;
            this.btnResumeOff.UseVisualStyleBackColor = false;
            this.btnResumeOff.Click += new System.EventHandler(this.btnResumeOff_Click);
            //
            // lblAutoResume
            //
            this.lblAutoResume.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblAutoResume.ForeColor = System.Drawing.Color.Silver;
            this.lblAutoResume.Location = new System.Drawing.Point(8, 536);
            this.lblAutoResume.Name = "lblAutoResume";
            this.lblAutoResume.Size = new System.Drawing.Size(544, 26);
            this.lblAutoResume.TabIndex = 21;
            this.lblAutoResume.Text = global::BeltFlo.Language.Lang.lgAutoResumePause;
            //
            // btnAutoResumeOn
            //
            this.btnAutoResumeOn.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnAutoResumeOn.FlatAppearance.BorderSize = 0;
            this.btnAutoResumeOn.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAutoResumeOn.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnAutoResumeOn.ForeColor = System.Drawing.Color.White;
            this.btnAutoResumeOn.Location = new System.Drawing.Point(8, 566);
            this.btnAutoResumeOn.Name = "btnAutoResumeOn";
            this.btnAutoResumeOn.Size = new System.Drawing.Size(267, 48);
            this.btnAutoResumeOn.TabIndex = 22;
            this.btnAutoResumeOn.Text = global::BeltFlo.Language.Lang.lgOn;
            this.btnAutoResumeOn.UseVisualStyleBackColor = false;
            this.btnAutoResumeOn.Click += new System.EventHandler(this.btnAutoResumeOn_Click);
            //
            // btnAutoResumeOff
            //
            this.btnAutoResumeOff.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnAutoResumeOff.FlatAppearance.BorderSize = 0;
            this.btnAutoResumeOff.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnAutoResumeOff.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnAutoResumeOff.ForeColor = System.Drawing.Color.White;
            this.btnAutoResumeOff.Location = new System.Drawing.Point(285, 566);
            this.btnAutoResumeOff.Name = "btnAutoResumeOff";
            this.btnAutoResumeOff.Size = new System.Drawing.Size(267, 48);
            this.btnAutoResumeOff.TabIndex = 23;
            this.btnAutoResumeOff.Text = global::BeltFlo.Language.Lang.lgOff;
            this.btnAutoResumeOff.UseVisualStyleBackColor = false;
            this.btnAutoResumeOff.Click += new System.EventHandler(this.btnAutoResumeOff_Click);
            //
            // lblTruckFull
            //
            this.lblTruckFull.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblTruckFull.ForeColor = System.Drawing.Color.Silver;
            this.lblTruckFull.Location = new System.Drawing.Point(8, 626);
            this.lblTruckFull.Name = "lblTruckFull";
            this.lblTruckFull.Size = new System.Drawing.Size(300, 26);
            this.lblTruckFull.TabIndex = 24;
            this.lblTruckFull.Text = "Truck Full Warning";
            //
            // lblTruckFullVal
            //
            this.lblTruckFullVal.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.lblTruckFullVal.BorderStyle = System.Windows.Forms.BorderStyle.FixedSingle;
            this.lblTruckFullVal.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.lblTruckFullVal.ForeColor = System.Drawing.Color.White;
            this.lblTruckFullVal.Location = new System.Drawing.Point(285, 622);
            this.lblTruckFullVal.Name = "lblTruckFullVal";
            this.lblTruckFullVal.Size = new System.Drawing.Size(195, 36);
            this.lblTruckFullVal.TabIndex = 25;
            this.lblTruckFullVal.Text = "Off";
            this.lblTruckFullVal.TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
            this.lblTruckFullVal.Click += new System.EventHandler(this.lblTruckFullVal_Click);
            //
            // lblTruckFullUnit
            //
            this.lblTruckFullUnit.Font = new System.Drawing.Font("Microsoft Sans Serif", 12F);
            this.lblTruckFullUnit.ForeColor = System.Drawing.Color.Silver;
            this.lblTruckFullUnit.Location = new System.Drawing.Point(488, 628);
            this.lblTruckFullUnit.Name = "lblTruckFullUnit";
            this.lblTruckFullUnit.Size = new System.Drawing.Size(64, 24);
            this.lblTruckFullUnit.TabIndex = 26;
            this.lblTruckFullUnit.Text = "";
            //
            // btnSaveSettings
            //
            this.btnSaveSettings.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(0)))), ((int)(((byte)(110)))), ((int)(((byte)(0)))));
            this.btnSaveSettings.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSaveSettings.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnSaveSettings.ForeColor = System.Drawing.Color.White;
            this.btnSaveSettings.Location = new System.Drawing.Point(8, 680);
            this.btnSaveSettings.Name = "btnSaveSettings";
            this.btnSaveSettings.Size = new System.Drawing.Size(130, 44);
            this.btnSaveSettings.TabIndex = 18;
            this.btnSaveSettings.Text = global::BeltFlo.Language.Lang.lgSave;
            this.btnSaveSettings.UseVisualStyleBackColor = false;
            this.btnSaveSettings.Click += new System.EventHandler(this.btnSaveSettings_Click);
            // 
            // btnSettingsClose
            // 
            this.btnSettingsClose.BackColor = System.Drawing.Color.FromArgb(((int)(((byte)(60)))), ((int)(((byte)(60)))), ((int)(((byte)(60)))));
            this.btnSettingsClose.FlatStyle = System.Windows.Forms.FlatStyle.Flat;
            this.btnSettingsClose.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F, System.Drawing.FontStyle.Bold);
            this.btnSettingsClose.ForeColor = System.Drawing.Color.White;
            this.btnSettingsClose.Location = new System.Drawing.Point(422, 680);
            this.btnSettingsClose.Name = "btnSettingsClose";
            this.btnSettingsClose.Size = new System.Drawing.Size(130, 44);
            this.btnSettingsClose.TabIndex = 19;
            this.btnSettingsClose.Text = global::BeltFlo.Language.Lang.lgClose;
            this.btnSettingsClose.UseVisualStyleBackColor = false;
            this.btnSettingsClose.Click += new System.EventHandler(this.btnSettingsClose_Click);
            // 
            // frmMenuSettings
            // 
            this.BackColor = System.Drawing.Color.White;
            this.ClientSize = new System.Drawing.Size(564, 788);
            this.Controls.Add(this.pnlContent);
            this.Controls.Add(this.pnlTitle);
            this.Font = new System.Drawing.Font("Microsoft Sans Serif", 14.25F);
            this.FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            this.Name = "frmMenuSettings";
            this.Padding = new System.Windows.Forms.Padding(2);
            this.ShowInTaskbar = false;
            this.StartPosition = System.Windows.Forms.FormStartPosition.CenterScreen;
            this.Text = "Settings";
            this.TopMost = true;
            this.Load += new System.EventHandler(this.frmMenuSettings_Load);
            this.pnlTitle.ResumeLayout(false);
            this.pnlContent.ResumeLayout(false);
            this.ResumeLayout(false);

        }

        private System.Windows.Forms.Panel    pnlTitle;
        private System.Windows.Forms.Label    lblTitle;
        private System.Windows.Forms.Panel    pnlContent;
        private System.Windows.Forms.Label    lblUnits;
        private System.Windows.Forms.Button   btnImperial;
        private System.Windows.Forms.Button   btnMetric;
        private System.Windows.Forms.Label    lblYieldUnits;
        private System.Windows.Forms.Button   btnYieldLb;
        private System.Windows.Forms.Button   btnYieldCwt;
        private System.Windows.Forms.Button   btnYieldTons;
        private System.Windows.Forms.Label    lblAogYieldRange;
        private System.Windows.Forms.Button   btnYieldLow;
        private System.Windows.Forms.Button   btnYieldHigh;
        private System.Windows.Forms.Label    lblNetwork;
        private System.Windows.Forms.Button   btnEthernet;
        private System.Windows.Forms.Button   btnCAN;
        private System.Windows.Forms.Label    lblWifiInfo;
        private System.Windows.Forms.Label    lblCanDriver;
        private System.Windows.Forms.ComboBox cbCanDriver;
        private System.Windows.Forms.Label    lblCanPort;
        private System.Windows.Forms.ComboBox cbCanPort;
        private System.Windows.Forms.Button   btnRescanPorts;
        private System.Windows.Forms.Label    lblCanStatus;
        private System.Windows.Forms.Timer    tmrCanStatus;
        private System.Windows.Forms.Label    lblResumeJob;
        private System.Windows.Forms.Button   btnResumeOn;
        private System.Windows.Forms.Button   btnResumeOff;
        private System.Windows.Forms.Label    lblAutoResume;
        private System.Windows.Forms.Button   btnAutoResumeOn;
        private System.Windows.Forms.Button   btnAutoResumeOff;
        private System.Windows.Forms.Label    lblTruckFull;
        private System.Windows.Forms.Label    lblTruckFullVal;
        private System.Windows.Forms.Label    lblTruckFullUnit;
        private System.Windows.Forms.Button   btnSaveSettings;
        private System.Windows.Forms.Button   btnSettingsClose;
    }
}
