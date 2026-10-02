using System;
using System.Drawing;
using System.IO;
using System.Windows.Forms;

namespace BeltFlo.Classes
{
    public enum CanDriver { SLCAN, InnoMaker, PCAN }

    public static class Props
    {
        public static readonly string AppName = "BeltFlo";
        public static readonly string AppVersion = "1.0.0";
        public static readonly string AppDate = "14-Sep-2026";

        private static string cApplicationFolder;
        private static string cDataFolder;
        private static string cLogsFolder;
        private static string cExportFolder;
        private static bool cCanEnabled = false;
        private static CanDriver cCurrentCanDriver = CanDriver.SLCAN;
        private static string cCanPort = "";

        public static bool CanEnabled
        {
            get { return cCanEnabled; }
            set { cCanEnabled = value; }
        }

        public static string Version { get { return AppVersion; } }

        public static CanDriver CurrentCanDriver
        {
            get { return cCurrentCanDriver; }
            set { cCurrentCanDriver = value; }
        }

        public static string CanPort
        {
            get { return cCanPort; }
            set { cCanPort = value; }
        }

        // ── Units ─────────────────────────────────────────────────────────────
        // Internal values are always pounds, acres and lb/ac. The scale delivers
        // pounds, so nothing stored depends on a crop constant; every other unit is
        // a display conversion applied on the way out.
        //
        // Internal yield remains lb/ac. Imperial display can be lb/ac, cwt/ac
        // or tons/ac; metric remains t/ha. No crop/bushel constant is involved.
        private const double LB_PER_CWT = 100.0;
        private const double LB_PER_TON = 2000.0;
        private const double LB_PER_KG  = 2.20462;
        private const double HA_PER_AC  = 0.404686;
        private const double M_PER_FT   = 0.3048;

        public static bool IsMetric => Properties.Settings.Default.Units == "Metric";
        public static bool YieldInPounds => Properties.Settings.Default.YieldUnit == "lb/ac";
        public static bool YieldInTons   => Properties.Settings.Default.YieldUnit == "tons/ac";
        public static bool YieldInCwt    => !YieldInPounds && !YieldInTons; // cwt/ac + safe legacy fallback

        public static string AreaUnit  => IsMetric ? "ha"   : "ac";
        public static string MassUnit  => IsMetric ? "t"    : (YieldInPounds ? "lb" : YieldInTons ? "tons" : "cwt");
        public static string RateUnit  => IsMetric ? "t/ha" : (YieldInPounds ? "lb/ac" : YieldInTons ? "tons/ac" : "cwt/ac");
        public static string LoadUnit  => IsMetric ? "kg"   : "lb";        // truck loads, in ticket units
        public static string FlowUnit  => IsMetric ? "kg/min" : "lb/min";
        public static string SpeedUnit => IsMetric ? "km/h" : "mph";
        public static string BeltSpeedUnit => IsMetric ? "m/min" : "ft/min";

        private static double LbPerMassUnit => IsMetric ? 1000.0 * LB_PER_KG
            : YieldInPounds ? 1.0
            : YieldInTons ? LB_PER_TON
            : LB_PER_CWT;

        public static double DisplayArea(double acres)      => IsMetric ? acres * HA_PER_AC : acres;
        public static double DisplayMass(double lb)         => lb / LbPerMassUnit;
        public static double DisplayRate(double lbPerAc)    => IsMetric ? lbPerAc / LbPerMassUnit / HA_PER_AC : lbPerAc / LbPerMassUnit;
        public static double DisplayLoad(double lb)         => IsMetric ? lb / LB_PER_KG : lb;
        public static double DisplayFlow(double lbPerMin)   => IsMetric ? lbPerMin / LB_PER_KG : lbPerMin;
        public static double DisplaySpeed(double kmh)       => IsMetric ? kmh : kmh * 0.621371;
        public static double DisplayBeltSpeed(double ftMin) => IsMetric ? ftMin * M_PER_FT : ftMin;

        /// <summary>A weight typed in the display load unit, back to pounds.</summary>
        public static double LoadToLb(double display)       => IsMetric ? display * LB_PER_KG : display;

        public static string ApplicationFolder { get { return cApplicationFolder; } }
        public static string DataFolder { get { return cDataFolder; } }
        public static string LogsFolder { get { return cLogsFolder; } }
        public static string ExportFolder { get { return cExportFolder; } }

        public static void CheckFolders()
        {
            cApplicationFolder = AppDomain.CurrentDomain.BaseDirectory;
            cDataFolder = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),
                AppName);
            cLogsFolder = Path.Combine(cDataFolder, "Logs");
            cExportFolder = Path.Combine(cDataFolder, "Exports");

            Directory.CreateDirectory(cDataFolder);
            Directory.CreateDirectory(cLogsFolder);
            Directory.CreateDirectory(cExportFolder);
        }

        public static void WriteErrorLog(string message)
        {
            try
            {
                string path = Path.Combine(cLogsFolder ?? AppDomain.CurrentDomain.BaseDirectory, "Errors.log");
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
            catch { }
        }

        public static void WriteActivityLog(string message, bool newFile = false)
        {
            try
            {
                string path = Path.Combine(cLogsFolder ?? AppDomain.CurrentDomain.BaseDirectory, "Activity.log");
                if (newFile && File.Exists(path))
                    File.Delete(path);
                File.AppendAllText(path, $"{DateTime.Now:yyyy-MM-dd HH:mm:ss}  {message}{Environment.NewLine}");
            }
            catch { }
        }

        public static void WriteLog(string fileName, string content, bool append = true, bool newFile = false)
        {
            try
            {
                string path = Path.Combine(cLogsFolder ?? AppDomain.CurrentDomain.BaseDirectory, fileName);
                if (newFile && File.Exists(path))
                    File.Delete(path);
                if (append)
                    File.AppendAllText(path, content);
                else
                    File.WriteAllText(path, content);
            }
            catch { }
        }

        public static void SaveFormLocation(Form frm)
        {
            try
            {
                Properties.Settings.Default[$"Form_{frm.Name}_X"] = frm.Location.X;
                Properties.Settings.Default[$"Form_{frm.Name}_Y"] = frm.Location.Y;
                Properties.Settings.Default.Save();
            }
            catch { }
        }

        public static void LoadFormLocation(Form frm)
        {
            try
            {
                object x = Properties.Settings.Default[$"Form_{frm.Name}_X"];
                object y = Properties.Settings.Default[$"Form_{frm.Name}_Y"];
                if (x != null && y != null)
                {
                    int px = Convert.ToInt32(x);
                    int py = Convert.ToInt32(y);
                    Rectangle screen = Screen.GetWorkingArea(frm);
                    if (screen.Contains(px, py))
                        frm.Location = new Point(px, py);
                }
            }
            catch { }
        }

        public static void ShowMessage(string message, string title = "Info", int durationMs = 3000, bool isError = false, bool topMost = false)
        {
            try
            {
                if (Core.MainForm == null || Core.MainForm.IsDisposed) return;

                if (isError)
                    System.Media.SystemSounds.Exclamation.Play();

                Core.MainForm.BeginInvoke((Action)(() =>
                {
                    Core.MainForm.ShowStatusMessage(message, isError);
                }));
            }
            catch { }
        }

        public static void ApplyTheme(Control control)
        {
            control.BackColor = Properties.Settings.Default.MainBackColour;
            control.ForeColor = Properties.Settings.Default.MainForeColour;
            foreach (Control child in control.Controls)
                ApplyTheme(child);
        }
    }
}
