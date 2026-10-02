using System;
using System.Windows.Forms;
using BeltFlo.Communication;
using BeltFlo.Communication.Can;
using BeltFlo.Database;
using BeltFlo.Forms;

namespace BeltFlo.Classes
{
    public static class Core
    {
        // Subsystems
        public static UDPComm UDPaog;                           // GPS from AOG       recv:17777 send:15555
        public static UDPComm UDPmodule;                        // BeltFlo module     recv:30300 (WiFi mode)
        public static CanModuleComm CanModule = new CanModuleComm(); // BeltFlo module  CAN mode
        public static clsGPS GPS = new clsGPS();
        public static clsYieldCalculator Yield;
        public static clsDataCollector Collector;
        public static DB Database;

        // UI
        public static frmMain MainForm;

        // Shared tools
        public static clsTools Tls = new clsTools();

        // Live conveyor state (written by the packet parsers through the two Apply
        // methods below, read by UI + DataCollector)
        public static uint   LastCumPoundsX10 { get; set; }   // module's cumulative delivered weight, tenths of a pound; wraps
        public static uint   LastCumPulses    { get; set; }   // module's cumulative belt pulses; wraps
        public static double LastScaleLb      { get; set; }   // live weigh-section load after zero, lb
        public static int    LastScaleRaw     { get; set; }   // raw converter counts, for the calibration screen
        public static byte   LastFlags        { get; set; }
        // The module's own verdict on the converter and the cells. When it is false
        // the counters may still tick, but nothing they say can be trusted, so the
        // collector pauses recording the same way it does for a GPS dropout.
        public static bool   LastScaleOk      { get; set; } = true;
        public static bool   LastBeltRunning  { get; set; }
        public static bool   LastTared        { get; set; }   // the module holds a zero
        public static bool   LastReceivingFromPc { get; set; } // flags bit 3: a settings message reached the module within 4 s
        public static bool   LastOverload     { get; set; }   // cells at their rated limit

        /// <summary>Two-way link, as in RateController: packets arriving and the module hearing the PC.</summary>
        public static bool ModuleReceiving => ModuleConnected && LastReceivingFromPc;
        private static bool _lastModuleReceiving;

        // The conveyor settings the module should be weighing with. Sent every
        // SettingsResendSec as a heartbeat, and at once when they change.
        private static ConveyorConfig _activeConveyor;
        private static DateTime _lastSettingsSent = DateTime.MinValue;
        private const double SettingsResendSec = 2.0;

        /// <summary>Per-session diagnostic CSV, one row per module packet. Always running.</summary>
        public static clsDiagLogger DiagLog { get; private set; }

        public static bool   ModuleConnected  { get; set; }
        public static DateTime LastModuleReceive { get; set; }
        public static bool   LastDataWriteOk  { get; set; } = true;

        // Active session configuration
        public static int ActiveProfileId { get; set; } = -1;
        public static int ActiveCropId    { get; set; } = -1;
        public static int ActiveRowsHarvested { get; set; } = 0; // the job's rows harvested; 0 = the harvester's own row count
        public static int ActiveCalRev    { get; set; } = -1;   // conveyor_config row the app expects the module to run
        public static int ActiveRowsInUse { get; set; } = 0;    // rows the digging width is worked out from; recorded on every point
        // A scale before a holding tank measures field yield continuously, but
        // cannot identify which stored crop later goes into an individual truck.
        public static bool ScaleBeforeHoldingTank { get; private set; }
        public static bool ScaleTracksTruckLoads => !ScaleBeforeHoldingTank;
        /// <summary>True when the active profile has a real zero and span, not the seeded placeholder.</summary>
        public static bool ActiveScaleCalibrated => _activeConveyor?.IsCalibrated == true;

        /// <summary>
        /// One immediate verdict used by BOTH counter accounting and the GPS/map
        /// pipeline. Keeping it here prevents a bad status packet being treated
        /// differently for the fraction of a second before the next GPS tick.
        /// </summary>
        public static bool ScaleDataUsable =>
            ModuleConnected
            && ModuleReceiving
            && LastScaleOk
            && !LastOverload
            && LastTared
            && ActiveScaleCalibrated;

        // Flags
        public static bool IsShuttingDown { get; private set; }
        public static bool IsRestarting { get; private set; }
        public static bool IsUserExitRequested { get; private set; }
        public static bool IsRestartedInstance { get; set; }

        // Events
        public static event EventHandler UpdateDisplay;
        public static event EventHandler GpsUpdated;
        public static event EventHandler JobStateChanged;
        public static event EventHandler FieldListChanged;
        public static event EventHandler CropListChanged;
        public static event EventHandler ProfileListChanged;
        public static event EventHandler ColorChanged;
        public static event EventHandler AppExit;

        private static DateTime cStartTime;
        private static System.Timers.Timer MainTimer;

        public static void Initialize(frmMain frm)
        {
            try
            {
                MainForm = frm;

                if (!IsRestartedInstance && Tls.PrevInstance()) Application.Exit();

                Props.CheckFolders();

                // Database
                string dbPath = System.IO.Path.Combine(Props.DataFolder, "BeltFlo.db");
                Database = new DB(dbPath);
                Database.Initialize();

                // Yield engine
                Yield = new clsYieldCalculator();
                Yield.ProcessingDelaySec = Properties.Settings.Default.ProcessingDelaySec;
                Collector = new clsDataCollector();

                SeedDefaultData();
                TryResumeLastJob();

                // Load persisted comm settings
                Props.CanEnabled = Properties.Settings.Default.ModuleCommType == "CAN";
                if (Enum.TryParse(Properties.Settings.Default.CanDriver, out CanDriver cd))
                    Props.CurrentCanDriver = cd;
                Props.CanPort = Properties.Settings.Default.CanPort;

                // UDP
                UDPaog = new UDPComm(MainForm, 17777, 15555, 1461, "UDPaog", "127.255.255.255"); // send-from 1461 (RC uses 1460)
                UDPmodule = new UDPComm(MainForm, 30300, 30400, 1500, "UDPmodule");

                UDPmodule.Start();
                if (!UDPmodule.IsRunning)
                    Props.ShowMessage("Module UDP failed to start.", "", 3000, true);

                UDPaog.Start();
                if (!UDPaog.IsRunning)
                    Props.ShowMessage("AOG UDP failed to start.", "", 3000, true);

                // CAN (only if configured)
                if (Props.CanEnabled)
                    UseCanComm(true);

                // 1-second status timer
                MainTimer = new System.Timers.Timer(1000);
                MainTimer.Elapsed += MainTimer_Elapsed;
                MainTimer.AutoReset = true;
                MainTimer.Enabled = true;

                // One diagnostic CSV per session, started before any packet can
                // arrive so a fault in the first seconds is still captured.
                DiagLog = new clsDiagLogger();
                DiagLog.Start();

                Props.WriteActivityLog("Started", true);
                cStartTime = DateTime.Now;
            }
            catch (Exception ex)
            {
                MessageBox.Show("Could not start: " + ex.Message, "Fatal Error",
                    MessageBoxButtons.OK, MessageBoxIcon.Error);
                Application.Exit();
            }
        }

        public static bool AppShutDown(FormClosingEventArgs e)
        {
            bool allow = IsUserExitRequested || IsRestarting
                || e.CloseReason == CloseReason.WindowsShutDown
                || e.CloseReason == CloseReason.TaskManagerClosing;

            if (allow)
            {
                IsShuttingDown = true;
                SafeTry(() => MainTimer.Enabled = false);
                SafeTry(() => UDPaog?.Stop());
                SafeTry(() => UDPmodule?.Stop());
                SafeTry(() => CanModule?.Stop());
                if (Properties.Settings.Default.ResumeJobOnStart && Collector?.ActiveJobId > 0)
                    SafeTry(() => Collector?.SuspendJob());
                else
                    SafeTry(() => Collector?.StopJob());
                SafeTry(() => Database?.Close());
                SafeTry(() => DiagLog?.Stop());
                SafeTry(() => SafeEvent.Raise(AppExit));
                SafeTry(() => LogRunTime());
            }
            return allow;
        }

        // ── Module packets ────────────────────────────────────────────────────
        //
        // Both transports land here, so UDP and CAN cannot drift apart in what they
        // do with a packet. Counters and status arrive together on UDP and as two
        // frames on CAN; each half is applied on its own.

        /// <summary>
        /// The module's cumulative counters. Differences them, credits the pounds to
        /// the job and the open load, and writes one diagnostic row.
        /// </summary>
        public static void ApplyConveyorCounters(uint cumPoundsX10, uint cumPulses)
        {
            DateTime now = DateTime.UtcNow;
            if (cumPulses != LastCumPulses) _lastPulseChangeUtc = now;

            LastCumPoundsX10  = cumPoundsX10;
            LastCumPulses     = cumPulses;
            ModuleConnected   = true;
            LastModuleReceive = DateTime.UtcNow;

            double dLb = Yield?.PushConveyorReading(cumPoundsX10, cumPulses, DateTime.UtcNow) ?? 0;

            // Below the empty-belt threshold the belt is carrying dirt, not crop —
            // cleaning the belt or running it empty — so nothing is credited to the
            // job, the load or the map.
            // The threshold for credited mass is packet-level, not the smoothed
            // display flow. This counts the first real crop immediately and drops
            // empty-belt noise immediately when it falls below the configured rate.
            if (dLb > 0 && Yield.CountCurrentDelta && ScaleDataUsable)
                Collector?.OnPoundsDelta(dLb);

            // Alarm/tail behaviour is intentionally smoother than accounting.
            Collector?.CheckNoLoad(Yield?.IsFlowing ?? false);

            // After the pulse time above is current, so weight and pulses are judged
            // from the same packet.
            EvaluateBeltSensor(now);

            // One diagnostic row per counter packet — 5 Hz, independent of whether a
            // job is recording, so a fault between jobs still leaves evidence.
            DiagLog?.Log();
        }

        /// <summary>The module's status flags and live section weight.</summary>
        public static void ApplyConveyorStatus(byte flags, double scaleLb, int scaleRaw)
        {
            bool reconnected = !ModuleConnected;

            LastFlags           = flags;
            LastScaleOk         = (flags & 0x01) != 0;
            LastBeltRunning     = (flags & 0x02) != 0;
            LastTared           = (flags & 0x04) != 0;
            LastReceivingFromPc = (flags & 0x08) != 0;
            LastOverload        = (flags & 0x10) != 0;
            LastScaleLb         = scaleLb;
            LastScaleRaw        = scaleRaw;
            ModuleConnected   = true;
            LastModuleReceive = DateTime.UtcNow;

            if (reconnected)
            {
                // A module that has just appeared, or come back after a restart, has
                // not heard the settings yet. Send them now, and don't call its clear
                // bit a fault before it has had the chance.
                _lastModuleReceiving = false;
                SendModuleSettings(true);
            }

            // Log each change, as RateController does. Losing it is said once across
            // the status bar; the Module label stays orange while it lasts.
            if (ModuleReceiving != _lastModuleReceiving)
            {
                _lastModuleReceiving = ModuleReceiving;
                if (ModuleReceiving)
                {
                    Props.WriteActivityLog("Module receiving: True");
                }
                else
                {
                    Props.WriteErrorLog("Module receiving: False - settings are not reaching the module");
                    Props.ShowMessage(Language.Lang.lgModuleNotReceiving, "", 5000, true);
                }
            }

            _scaleWindow.Enqueue((LastModuleReceive, scaleLb));
            while (_scaleWindow.Count > 0
                   && (LastModuleReceive - _scaleWindow.Peek().utc).TotalSeconds > BeltCheckSec)
                _scaleWindow.Dequeue();

            // Not evaluated here. UDP applies status before counters, so judging now
            // would pair this packet's weight with the previous packet's pulses — a
            // belt that has just restarted would flash a false Belt warning.
        }

        // ── Belt sensor check ─────────────────────────────────────────────────
        //
        // A disconnected proximity sensor sends no pulses, which is exactly what a
        // stopped belt sends, so the packet cannot tell them apart. What does tell
        // them apart is the weight: crop sitting on a stopped belt reads steady,
        // crop moving over the section on a running belt rises and falls. Weight
        // present AND moving with no pulses for a few seconds means the belt is
        // running and its sensor is not being heard.
        //
        // That matters more than it looks. The module multiplies weight by belt
        // travel, so with no pulses it records zero pounds while crop is still
        // going into the truck, and nothing else on screen would show it.
        //
        // The thresholds are first guesses, to be set from bench and field
        // readings: 2 lb is well above a clean zero, and a 1 lb swing in 3 s is
        // beyond converter noise on a stopped belt but well within the lumpiness
        // of crop on a moving one.

        private const double BeltCheckSec        = 3.0;
        private const double BeltCheckMinLb      = 2.0;
        private const double BeltCheckMinSwingLb = 1.0;

        private static DateTime _lastPulseChangeUtc = DateTime.MinValue;
        private static readonly System.Collections.Generic.Queue<(DateTime utc, double lb)> _scaleWindow
            = new System.Collections.Generic.Queue<(DateTime utc, double lb)>();

        /// <summary>True while crop appears to be moving over the scale with no belt pulses arriving.</summary>
        public static bool BeltSensorSuspect { get; private set; }

        private static void EvaluateBeltSensor(DateTime now)
        {
            bool suspect = false;

            // Only judge a full window, and only once pulses have been still for all of it.
            if (_scaleWindow.Count > 1
                && (now - _scaleWindow.Peek().utc).TotalSeconds >= BeltCheckSec - 0.5
                && (now - _lastPulseChangeUtc).TotalSeconds >= BeltCheckSec)
            {
                double min = double.MaxValue, max = double.MinValue, sum = 0;
                foreach (var s in _scaleWindow)
                {
                    if (s.lb < min) min = s.lb;
                    if (s.lb > max) max = s.lb;
                    sum += s.lb;
                }
                double mean = sum / _scaleWindow.Count;
                suspect = mean >= BeltCheckMinLb && (max - min) >= BeltCheckMinSwingLb;
            }

            if (suspect == BeltSensorSuspect) return;
            BeltSensorSuspect = suspect;

            if (suspect)
            {
                Props.WriteErrorLog("Belt sensor suspect: weight moving on the section with no pulses for "
                                    + BeltCheckSec.ToString("0") + " s (scale " + LastScaleLb.ToString("0.0") + " lb)");
                Props.ShowMessage(Language.Lang.lgBeltSensorSuspect, "", 5000, true);
            }
            else
            {
                Props.WriteActivityLog("Belt sensor OK again");
            }
        }

        public static void RequestUserExit()
        {
            if (ModuleConnected)
            {
                var answer = MessageBox.Show("Confirm Exit?", "Exit",
                    MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (answer != DialogResult.Yes) return;
            }
            IsUserExitRequested = true;
            Application.Exit();
        }

        public static void RequestRestart(string language = null)
        {
            IsRestarting = true;
            // Pass language + restarted flag as args so:
            //   1. Language is received directly (bypasses any settings version-path mismatch)
            //   2. PrevInstance() check is skipped (old process may still be in cleanup when new one starts)
            // Start AFTER ApplicationExit so UDP ports are released before new instance binds them.
            string args = "--restarted" + (language != null ? $" --lang={language}" : "");
            Application.ApplicationExit += (s, ev) =>
                System.Diagnostics.Process.Start(Application.ExecutablePath, args);
            Application.Exit();
        }

        public static void RaiseColorChanged() => SafeEvent.Raise(ColorChanged);
        public static void RaiseJobStateChanged()   => SafeEvent.Raise(JobStateChanged);
        public static void RaiseFieldListChanged()   => SafeEvent.Raise(FieldListChanged);
        public static void RaiseCropListChanged()    => SafeEvent.Raise(CropListChanged);
        public static void RaiseProfileListChanged() => SafeEvent.Raise(ProfileListChanged);
        public static void RaiseGpsUpdated() => SafeEvent.Raise(GpsUpdated);

        /// <summary>Digging width for a job: rows harvested (0 = the harvester's own) × row spacing.</summary>
        public static double JobWidthM(int profileId, int rowsHarvested)
        {
            var p = Database?.Profiles.GetById(profileId);
            return (p ?? new HarvesterProfile()).WidthM(rowsHarvested);
        }

        public static void LoadJobConfig(int profileId, int cropId, int rowsHarvested)
        {
            ActiveProfileId     = profileId;
            ActiveCropId        = cropId;
            ActiveRowsHarvested = rowsHarvested;

            // Remembered so the next start, with no job to resume, comes up on the
            // same harvester rather than the first profile in the list.
            if (profileId > 0 && Properties.Settings.Default.CurrentProfile != profileId.ToString())
            {
                Properties.Settings.Default.CurrentProfile = profileId.ToString();
                Properties.Settings.Default.Save();
            }

            if (Database == null || Yield == null) return;

            // Width and position come from the harvester: rows × row spacing, and the
            // digger's distance from AgOpenGPS's pivot.
            var profile = Database.Profiles.GetById(profileId);
            if (profile != null)
            {
                ActiveRowsInUse     = rowsHarvested > 0 ? rowsHarvested : profile.Rows;
                Yield.DiggingWidthM = profile.WidthM(rowsHarvested);
                Yield.AheadOfPivotM = profile.AheadOfPivotM;
                ScaleBeforeHoldingTank = profile.IsBeforeHoldingTank;
            }

            // The conveyor configuration belongs to the machine, not the crop: the
            // scale does not care what is running over it.
            if (profileId > 0)
            {
                var cfg = Database.ConveyorConfigs.GetLatest(profileId);
                if (cfg != null)
                {
                    ActiveCalRev                = cfg.Id;
                    _activeConveyor             = cfg;
                    Yield.InchesPerPulse        = cfg.InchesPerPulse > 0 ? cfg.InchesPerPulse : 1.0;
                    Yield.FlowThresholdLbPerSec = cfg.FlowThresholdLbS;
                    Yield.ProcessingDelaySec    = cfg.DelaySec > 0
                        ? cfg.DelaySec
                        : Properties.Settings.Default.ProcessingDelaySec;
                    SendModuleSettings(true);
                }
            }
        }

        /// <summary>
        /// Saves a profile's conveyor settings. A new calibration revision starts only
        /// when something that changes pounds already recorded changes — span, weigh
        /// section length or belt travel per pulse — so old points can be rescaled
        /// by the revision they carry. Zero, delay, the empty-belt threshold and the
        /// belt-stopped timeout update the current revision in place. Returns the
        /// revision id. The active profile's settings go to the module at once.
        /// </summary>
        public static int SaveConveyorConfig(ConveyorConfig edited)
        {
            var latest = Database.ConveyorConfigs.GetLatest(edited.ProfileId);
            bool newRevision = latest == null
                || !SameValue(latest.SpanLbPerCount, edited.SpanLbPerCount)
                || !SameValue(latest.SectionLenIn,   edited.SectionLenIn)
                || !SameValue(latest.InchesPerPulse, edited.InchesPerPulse);

            if (newRevision)
            {
                edited.Id = Database.ConveyorConfigs.Save(edited);
            }
            else
            {
                edited.Id = latest.Id;
                Database.ConveyorConfigs.UpdateInPlace(edited);
            }
            Props.WriteActivityLog("Conveyor settings saved, revision " + edited.Id
                                   + (newRevision ? " (new)" : " (updated)"));

            if (edited.ProfileId == ActiveProfileId)
                LoadJobConfig(ActiveProfileId, ActiveCropId, ActiveRowsHarvested);
            return edited.Id;
        }

        private static bool SameValue(double a, double b) =>
            Math.Abs(a - b) <= 1e-9 * Math.Max(1.0, Math.Abs(a));

        /// <summary>
        /// Sends the active conveyor settings to the module over whichever link is in
        /// use. The 1 s timer calls it unforced, which sends every SettingsResendSec as
        /// the heartbeat the module's "receiving from PC" bit is built on; a change
        /// or a module reconnecting forces an immediate send.
        /// </summary>
        public static void SendModuleSettings(bool force)
        {
            if (_activeConveyor == null) return;
            if (!force && (DateTime.UtcNow - _lastSettingsSent).TotalSeconds < SettingsResendSec) return;
            _lastSettingsSent = DateTime.UtcNow;

            if (Props.CanEnabled)
                CanModule?.SendSettings(_activeConveyor);
            else
                UDPmodule?.SendToModule(ModuleSettings.UdpPacket(_activeConveyor));
        }

        private static void SeedDefaultData()
        {
            try
            {
                if (Database.Profiles.GetAll().Count == 0)
                    Database.Profiles.Create(new HarvesterProfile { Name = "Default", HarvesterId = "Harvester 1" });

                if (Database.Crops.GetAll().Count == 0)
                {
                    Database.Crops.Create("Potato");
                    Database.Crops.Create("Sugar Beet");
                    Database.Crops.Create("Carrot");
                    Database.Crops.Create("Onion");
                }

                // Every profile carries a conveyor configuration from the start, so a
                // packet always has a revision to be checked against.
                foreach (var p in Database.Profiles.GetAll())
                    if (Database.ConveyorConfigs.GetLatest(p.Id) == null)
                        Database.ConveyorConfigs.Save(new ConveyorConfig { ProfileId = p.Id });

                // Set active to first available so yield calc has reasonable defaults
                var profiles = Database.Profiles.GetAll();
                var crops    = Database.Crops.GetAll();

                // The last profile used, if it still exists; otherwise the first.
                if (profiles.Count > 0)
                {
                    ActiveProfileId = profiles[0].Id;
                    if (int.TryParse(Properties.Settings.Default.CurrentProfile, out int lastId)
                        && profiles.Exists(p => p.Id == lastId))
                        ActiveProfileId = lastId;
                }
                if (crops.Count > 0) ActiveCropId = crops[0].id;

                if (ActiveProfileId > 0)
                    LoadJobConfig(ActiveProfileId, ActiveCropId, 0);
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("Core/SeedDefaultData: " + ex.Message);
            }
        }

        private static void TryResumeLastJob()
        {
            try
            {
                bool resumed = false;
                foreach (var j in Database.Jobs.GetAll())
                {
                    if (j.status != "Active") continue;

                    if (!resumed && Properties.Settings.Default.ResumeJobOnStart)
                    {
                        int profileId = j.profileId > 0 ? j.profileId : ActiveProfileId;
                        int cropId    = j.cropId    > 0 ? j.cropId    : ActiveCropId;
                        LoadJobConfig(profileId, cropId, j.rowsHarvested);
                        Collector.LoadJob(j.id, j.name, j.acres, j.volume);
                        RaiseJobStateChanged();
                        resumed = true;
                    }
                    else
                    {
                        // Close any extra stale active jobs
                        Database.Jobs.Close(j.id);
                    }
                }
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("Core/TryResumeLastJob: " + ex.Message);
            }
        }

        public static int UseCanComm(bool enable)
        {
            if (enable)
            {
                bool ok = CanModule.Start(Props.CurrentCanDriver, Props.CanPort);
                Props.CanEnabled = ok;
                if (!ok)
                    Props.ShowMessage("CAN failed to start. Check driver and COM port.", "", 5000, true);
                return ok ? 1 : 2;
            }
            else
            {
                CanModule?.Stop();
                Props.CanEnabled = false;
                return 0;
            }
        }

        private static void MainTimer_Elapsed(object sender, System.Timers.ElapsedEventArgs e)
        {
            SafeTry(() => SendModuleSettings(false));   // heartbeat, throttled to every 2 s
            SafeEvent.Raise(UpdateDisplay);
        }

        private static void LogRunTime()
        {
            Props.WriteActivityLog("Stopped");
            string mes = "Run time (hours): " +
                ((DateTime.Now - cStartTime).TotalSeconds / 3600.0).ToString("N1");
            Props.WriteActivityLog(mes);
        }

        private static void SafeTry(Action action)
        {
            try { action(); }
            catch (Exception ex)
            {
                try { Props.WriteActivityLog("Shutdown error: " + ex.Message); }
                catch { }
            }
        }
    }

    // ── SafeEvent ─────────────────────────────────────────────────────────────
    public static class SafeEvent
    {
        public static void Raise(EventHandler evt, EventArgs args = null, object sender = null)
        {
            if (evt == null) return;
            if (args == null) args = EventArgs.Empty;
            if (sender == null) sender = typeof(Core);

            foreach (EventHandler handler in evt.GetInvocationList())
                InvokeHandler(handler, sender, args);
        }

        private static void InvokeHandler(EventHandler handler, object sender, EventArgs args)
        {
            if (handler.Target is System.Windows.Forms.Control ctrl)
            {
                if (ctrl.IsDisposed || ctrl.Disposing || !ctrl.IsHandleCreated) return;
                if (ctrl.InvokeRequired)
                    ctrl.BeginInvoke(new Action(() => handler(sender, args)));
                else
                    handler(sender, args);
            }
            else
            {
                handler(sender, args);
            }
        }
    }

    // ── FormManager ───────────────────────────────────────────────────────────
    public static class FormManager
    {
        private static readonly System.Collections.Generic.Dictionary<string, Form> forms
            = new System.Collections.Generic.Dictionary<string, Form>();

        public static void ShowForm(Form frm)
        {
            string key = frm.GetType().FullName;
            var main = Core.MainForm;
            if (main == null || main.IsDisposed || !main.IsHandleCreated) return;

            if (main.InvokeRequired)
            {
                main.BeginInvoke((Action)(() => ShowForm(frm)));
                return;
            }

            if (forms.TryGetValue(key, out Form existing) && !existing.IsDisposed)
            {
                existing.BringToFront();
                frm.Dispose();
                return;
            }

            forms[key] = frm;
            frm.FormClosed += (s, e) => forms.Remove(key);
            frm.Show();
        }
    }
}
