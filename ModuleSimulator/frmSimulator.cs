using System;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;

namespace ModuleSimulator
{
    /// <summary>
    /// Simulates a BeltFlo conveyor module — sends the conveyor packet (PGN 40010,
    /// 5 Hz) over UDP to the BeltFlo PC app on port 30300, and takes the conveyor
    /// settings (PGN 40011) the app sends every 2 s on port 30400.
    ///
    /// The sliders set what a real machine would present to the scale: the load
    /// sitting on the weighed section and the belt speed. The module's job is to
    /// integrate load against belt travel, and that is done here the same way:
    /// pounds per inch of belt × inches of belt that passed this tick, using the
    /// section length and inches per pulse the app sent.
    ///
    /// While settings keep arriving (within 4 s) the packet sets flags bit 3,
    /// "receiving from PC", so the app knows the link works both ways.
    ///
    /// The cells read a fixed 100000 counts empty plus 2000 per pound, with a little
    /// noise. By default the module stays calibrated, so the load slider reads true
    /// whatever zero and span the app sent; tick "Apply app zero and span" to weigh
    /// with them instead and test the Scale Calibration screen end to end.
    /// </summary>
    public partial class frmSimulator : Form
    {
        private const int PC_RECV_PORT     = 30300;
        private const int SIM_SEND_PORT    = 30301;
        private const int MODULE_RECV_PORT = 30400;
        private const ushort SETTINGS_PGN  = 40011;
        private const double ReceivingWindowSec = 4.0;

        // Settings as held by the module. Until the app sends some, geometry matches
        // the app's default conveyor configuration.
        private double _inchesPerPulse = 1.0;
        private double _sectionLenIn   = 36.0;
        private double _zeroCounts     = 0;
        private double _spanLbPerCount = 0;
        private double _beltStopS      = 2.0;
        private DateTime _lastSettingsUtc = DateTime.MinValue;

        private System.Windows.Forms.Timer _sendTimer;
        private UdpClient  _udp;
        private UdpClient  _rx;
        private IPEndPoint _target;
        private double _simAngle = 0;
        private int    _ticks    = 0;

        // Cumulative counters, kept as doubles so fractional pounds and pulses
        // carry over between ticks, and truncated to uint32 on the wire so they
        // wrap the way the module's will.
        private double _cumLb     = 0;
        private double _cumPulses = 0;
        private double _lbPerSec  = 0;

        // The simulated load cells
        private const double RawEmpty    = 100000;
        private const double CountsPerLb = 2000;
        private const double RawNoise    = 40;     // peak-to-peak counts
        private readonly Random _rng = new Random();

        public frmSimulator()
        {
            InitializeComponent();
        }

        private static readonly string _posFile =
            Path.Combine(Application.LocalUserAppDataPath, "simpos.txt");

        private void frmSimulator_Load(object sender, EventArgs e)
        {
            RestorePosition();

            try
            {
                _udp    = new UdpClient(SIM_SEND_PORT);
                _target = new IPEndPoint(IPAddress.Loopback, PC_RECV_PORT);

                _rx = new UdpClient();
                _rx.Client.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                _rx.Client.Bind(new IPEndPoint(IPAddress.Any, MODULE_RECV_PORT));
            }
            catch (Exception ex)
            {
                lblStatus.Text = "UDP Error: " + ex.Message;
                return;
            }

            _sendTimer = new System.Windows.Forms.Timer { Interval = 100 };  // 10 Hz integration, 5 Hz send
            _sendTimer.Tick += SendTimer_Tick;
            _sendTimer.Start();
            lblStatus.Text = "Sending to 127.0.0.1:" + PC_RECV_PORT;
            ShowSettings();
        }

        private void RestorePosition()
        {
            try
            {
                if (!File.Exists(_posFile)) return;
                var parts = File.ReadAllText(_posFile).Split(',');
                if (parts.Length == 2 && int.TryParse(parts[0], out int x) && int.TryParse(parts[1], out int y))
                {
                    var pt = new System.Drawing.Point(x, y);
                    foreach (Screen s in Screen.AllScreens)
                        if (s.WorkingArea.Contains(pt)) { Location = pt; return; }
                }
            }
            catch { }
        }

        private void SavePosition()
        {
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(_posFile));
                File.WriteAllText(_posFile, $"{Location.X},{Location.Y}");
            }
            catch { }
        }

        private void chkSections_CheckedChanged(object sender, EventArgs e)
        {
            chkSections.ForeColor = chkSections.Checked
                ? System.Drawing.Color.DarkGreen
                : System.Drawing.Color.Red;
        }

        // ── Settings from the app ─────────────────────────────────────────────

        private bool ReceivingFromPc =>
            (DateTime.UtcNow - _lastSettingsUtc).TotalSeconds < ReceivingWindowSec;

        private void ReceiveSettings()
        {
            try
            {
                while (_rx != null && _rx.Available > 0)
                {
                    IPEndPoint from = null;
                    byte[] d = _rx.Receive(ref from);
                    HandleSettings(d);
                }
            }
            catch { }
        }

        private void HandleSettings(byte[] d)
        {
            // PGN 40011, 18 bytes: [0-1] PGN, [2-14] settings block, [15-16] CRC-16,
            // [17] byte-sum CRC8. Layout in the app's ModuleSettings.cs.
            if (d.Length < 18) return;
            if ((d[0] | (d[1] << 8)) != SETTINGS_PGN) return;

            int ck = 0;
            for (int i = 0; i < 17; i++) ck += d[i];
            if ((byte)ck != d[17]) return;

            byte[] block = new byte[13];
            Array.Copy(d, 2, block, 0, 13);
            if (Crc16(block) != BitConverter.ToUInt16(d, 15)) return;

            // Old firmware knows nothing of this message: drop it, so bit 3 clears
            // and the app shows the link as one-way.
            if (chkIgnoreSettings.Checked) return;

            _zeroCounts     = BitConverter.ToInt32(block, 0);
            _spanLbPerCount = BitConverter.ToSingle(block, 4);
            double section  = BitConverter.ToUInt16(block, 8) / 10.0;
            double ipp      = BitConverter.ToUInt16(block, 10) / 1000.0;
            _beltStopS      = block[12] / 10.0;
            if (section > 0) _sectionLenIn   = section;
            if (ipp > 0)     _inchesPerPulse = ipp;
            _lastSettingsUtc = DateTime.UtcNow;
        }

        private void ShowSettings()
        {
            lblSettings.Text = _lastSettingsUtc == DateTime.MinValue
                ? "Settings: none received"
                : $"Settings: {_sectionLenIn:F1} in section, {_inchesPerPulse:F3} in/pulse — "
                  + (ReceivingFromPc ? "receiving" : "not receiving");
        }

        // CRC-16/CCITT-FALSE, the same as the app's ModuleSettings.Crc16.
        private static ushort Crc16(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte x in data)
            {
                crc ^= (ushort)(x << 8);
                for (int i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }

        // ── Module loop ───────────────────────────────────────────────────────

        private void SendTimer_Tick(object sender, EventArgs e)
        {
            ReceiveSettings();
            ShowSettings();

            // A module that has lost power or its wiring sends nothing at all. The
            // PC app has no packet to read a fault out of — it times out after 5 s
            // of silence, which is the only path to a red Module label.
            if (chkModuleOffline.Checked)
            {
                lblFlow.Text        = "Flow: --";
                lblStatus.Text      = "Module offline — sending nothing";
                lblStatus.ForeColor = System.Drawing.Color.Red;
                return;
            }

            _simAngle += 0.05;
            _ticks++;

            double loadLb    = trkLoad.Value / 10.0;      // 0.0 – 50.0 lb on the section
            double beltFtMin = trkBelt.Value;             // 0 – 300 ft/min
            double variation = trkVariation.Value / 100.0;

            lblVariationSlider.Text = $"Variation: {trkVariation.Value}%";

            // Nothing on the belt unless the digger is in the ground.
            if (!chkSections.Checked) loadLb = 0;
            if (chkBeltStopped.Checked)
            {
                // Crop left sitting on a stopped belt: the section reads steady.
                beltFtMin = 0;
            }
            else
            {
                if (chkSineWave.Checked) loadLb *= 1.0 + variation * Math.Sin(_simAngle);
                // Crop moving over the section is lumpy, and that lumpiness is what
                // the PC app uses to tell a running belt from a stopped one when no
                // pulses arrive.
                loadLb *= 1.0 + 0.15 * Math.Sin(_simAngle * 9.7) + 0.08 * Math.Sin(_simAngle * 23.3);
            }

            // Integrate. lb per inch of belt × inches that passed this tick. With a
            // dead belt sensor the belt still moves but the module hears no pulses,
            // so it integrates nothing — the failure the app's Belt warning exists for.
            // The cells: a fixed physical response with a little converter noise.
            int scaleRaw = (int)Math.Round(RawEmpty + loadLb * CountsPerLb + (_rng.NextDouble() - 0.5) * RawNoise);

            // A real module weighs with the zero and span the app sent. Unless the
            // box is ticked the simulator stays calibrated, so the slider reads true.
            double sectionLb = chkApplyCal.Checked && _spanLbPerCount != 0
                ? (scaleRaw - _zeroCounts) * _spanLbPerCount
                : loadLb;

            const double dt = 0.1;
            double beltInPerSec   = beltFtMin * 12.0 / 60.0;
            double sensedInPerSec = chkBeltSensorDead.Checked ? 0 : beltInPerSec;
            _lbPerSec   = sectionLb / _sectionLenIn * sensedInPerSec;
            _cumLb     += _lbPerSec * dt;
            _cumPulses += sensedInPerSec * dt / _inchesPerPulse;

            if (_ticks % 2 == 0)
            {
                // bit0 ScaleOK, bit1 BeltRunning, bit2 Tared, bit3 ReceivingFromPC,
                // bit4 Overload. Match the real ESP32: PGN 40011 has no separate
                // zero-valid bit, so the app's non-zero calibrated span means the
                // zero in that same settings block is intentional too.
                byte flags = 0;
                if (!chkNotZeroed.Checked && Math.Abs(_spanLbPerCount) > 1e-9) flags |= 0x04;
                if (!chkScaleFault.Checked) flags |= 0x01;
                if (sensedInPerSec > 0)     flags |= 0x02;
                if (ReceivingFromPc)        flags |= 0x08;
                if (chkOverload.Checked)    flags |= 0x10;

                uint cumLbX10  = unchecked((uint)(long)(_cumLb * 10.0));
                uint cumPulses = unchecked((uint)(long)_cumPulses);
                short scaleX10 = (short)Math.Max(-32768, Math.Min(32767, sectionLb * 10.0));

                SendConveyorPacket(flags, cumLbX10, cumPulses, scaleX10, scaleRaw);
            }

            // Update UI labels
            lblFlow.Text   = $"Flow: {_lbPerSec:F2} lb/s  ({_lbPerSec * 60:F0} lb/min)";
            lblTotal.Text  = $"Total: {_cumLb:F1} lb";
            lblPulses.Text = $"Pulses: {(long)_cumPulses}  ({beltFtMin:F0} ft/min)";

            if (chkScaleFault.Checked)
            {
                lblStatus.Text      = "Sending ScaleOK = 0 — app should show NO SCALE now";
                lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else if (chkOverload.Checked)
            {
                lblStatus.Text      = "Sending Overload = 1 — app should stop trusting the scale";
                lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else if (chkBeltSensorDead.Checked)
            {
                lblStatus.Text      = "Belt sensor dead — app shows Belt after 3 s with crop on";
                lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else if (chkBeltStopped.Checked)
            {
                lblStatus.Text      = "Belt stopped — pulses and pounds frozen";
                lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else if (chkNotZeroed.Checked)
            {
                lblStatus.Text      = "Not zeroed — app shows Zero";
                lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else if (chkIgnoreSettings.Checked)
            {
                lblStatus.Text      = "Ignoring settings — app Module label turns orange";
                lblStatus.ForeColor = System.Drawing.Color.DarkOrange;
            }
            else
            {
                lblStatus.Text      = "Sending to 127.0.0.1:" + PC_RECV_PORT;
                lblStatus.ForeColor = System.Drawing.Color.DarkGreen;
            }
        }

        private void SendConveyorPacket(byte flags, uint cumLbX10, uint cumPulses, short scaleX10, int scaleRaw)
        {
            // Conveyor packet — 19 bytes:
            // [0-1]   PGN 40010 LE (0x4A 0x9C)
            // [2]     flags  bit0=ScaleOK, bit1=BeltRunning, bit2=Tared, bit3=ReceivingFromPC, bit4=Overload
            // [3-6]   cum_pounds_x10  uint32 LE
            // [7-10]  cum_pulses      uint32 LE
            // [11-12] scale_lb_x10    int16 LE
            // [13-16] scale_raw       int32 LE
            // [17]    reserved
            // [18]    CRC8 — byte sum of everything before it
            byte[] pkt = new byte[19];
            pkt[0] = 0x4A;
            pkt[1] = 0x9C;
            pkt[2] = flags;
            Array.Copy(BitConverter.GetBytes(cumLbX10),  0, pkt, 3,  4);
            Array.Copy(BitConverter.GetBytes(cumPulses), 0, pkt, 7,  4);
            Array.Copy(BitConverter.GetBytes(scaleX10),  0, pkt, 11, 2);
            Array.Copy(BitConverter.GetBytes(scaleRaw),  0, pkt, 13, 4);
            pkt[17] = 0;

            int ck = 0;
            for (int i = 0; i < 18; i++) ck += pkt[i];
            pkt[18] = (byte)ck;

            try { _udp.Send(pkt, pkt.Length, _target); } catch { }
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            SavePosition();
            _sendTimer?.Stop();
            _udp?.Close();
            _rx?.Close();
            base.OnFormClosed(e);
        }
    }
}
