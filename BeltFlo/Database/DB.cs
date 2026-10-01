using System.Data.SQLite;
using System;
using BeltFlo.Classes;

namespace BeltFlo.Database
{
    /// <summary>
    /// SQLite database wrapper. Creates all tables on first run.
    ///
    /// There is no installed base to preserve, so a schema change is an edit to
    /// the create statements below, not an add-column migration. Delete the
    /// database file to pick it up.
    /// </summary>
    public class DB
    {
        private readonly string _connectionString;

        public JobRepo Jobs { get; private set; }
        public YieldDataRepo YieldData { get; private set; }
        public ProfileRepo Profiles { get; private set; }
        public CropRepo Crops { get; private set; }
        public FieldRepo Fields { get; private set; }
        public ConveyorConfigRepo ConveyorConfigs { get; private set; }
        public LoadRepo Loads { get; private set; }

        public DB(string dbPath)
        {
            _connectionString = $"Data Source={dbPath};Foreign Keys=True;";
        }

        public void Initialize()
        {
            try
            {
                using var conn = OpenConnection();
                CreateTables(conn);

                Jobs = new JobRepo(_connectionString);
                YieldData = new YieldDataRepo(_connectionString);
                Profiles = new ProfileRepo(_connectionString);
                Crops = new CropRepo(_connectionString);
                Fields = new FieldRepo(_connectionString);
                ConveyorConfigs = new ConveyorConfigRepo(_connectionString);
                Loads = new LoadRepo(_connectionString);
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("DB/Initialize: " + ex.Message);
                throw;
            }
        }

        public SQLiteConnection OpenConnection()
        {
            var conn = new SQLiteConnection(_connectionString);
            conn.Open();
            return conn;
        }

        public void Close()
        {
            SQLiteConnection.ClearAllPools();
        }

        private void CreateTables(SQLiteConnection conn)
        {
            // Mass is pounds throughout, yield lb/ac, area acres. Display units are
            // applied on the way out (see Props).
            string sql = @"
PRAGMA journal_mode=WAL;
PRAGMA foreign_keys=ON;

-- The harvester. Digging width is row_count × row_spacing_m; there are no headers.
-- ahead_of_pivot_m follows AgOpenGPS: positive ahead of its pivot, negative behind.
-- scale_location is 'Truck' (weighs straight into the truck) or 'Tank'.
CREATE TABLE IF NOT EXISTS profiles (
    id               INTEGER PRIMARY KEY AUTOINCREMENT,
    name             TEXT    NOT NULL,
    harvester_id     TEXT    NOT NULL DEFAULT '',
    row_count        INTEGER NOT NULL DEFAULT 4,
    row_spacing_m    REAL    NOT NULL DEFAULT 0.9144,
    ahead_of_pivot_m REAL    NOT NULL DEFAULT 0,
    scale_location   TEXT    NOT NULL DEFAULT 'Truck',
    created_at       TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS crops (
    id    INTEGER PRIMARY KEY AUTOINCREMENT,
    name  TEXT    NOT NULL
);

CREATE TABLE IF NOT EXISTS fields (
    id       INTEGER PRIMARY KEY AUTOINCREMENT,
    name     TEXT NOT NULL,
    boundary TEXT NOT NULL DEFAULT ''
);

CREATE TABLE IF NOT EXISTS jobs (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    profile_id   INTEGER REFERENCES profiles(id),
    field_id     INTEGER REFERENCES fields(id),
    crop_id      INTEGER REFERENCES crops(id),
    rows_harvested INTEGER NOT NULL DEFAULT 0,   -- 0 = the harvester's own row count; more for windrowed crop
    name         TEXT    NOT NULL,
    started_at   TEXT    NOT NULL DEFAULT (datetime('now')),
    ended_at     TEXT,
    status       TEXT    NOT NULL DEFAULT 'Active',
    total_acres  REAL    NOT NULL DEFAULT 0,
    total_pounds REAL    NOT NULL DEFAULT 0,
    notes        TEXT    NOT NULL DEFAULT ''
);

-- The row id is the calibration revision every load and yield_data row records.
-- A new row only when span, section length or inches per pulse change (the
-- things that change pounds already recorded); zero, delay, threshold and
-- belt-stopped timeout update the current row. The module confirms it holds the
-- active row by reporting a CRC-16 of its settings, not the id.
CREATE TABLE IF NOT EXISTS conveyor_config (
    id                  INTEGER PRIMARY KEY AUTOINCREMENT,
    profile_id          INTEGER NOT NULL REFERENCES profiles(id),
    zero_counts         REAL    NOT NULL DEFAULT 0,
    span_lb_per_count   REAL    NOT NULL DEFAULT 0,
    zero_set_at         TEXT,
    pulses_per_rev      INTEGER NOT NULL DEFAULT 0,
    inches_per_pulse    REAL    NOT NULL DEFAULT 1,
    section_len_in      REAL    NOT NULL DEFAULT 36,
    flow_threshold_lb_s REAL    NOT NULL DEFAULT 0.05,
    belt_stop_timeout_s REAL    NOT NULL DEFAULT 2,
    delay_sec           INTEGER NOT NULL DEFAULT 10,
    created_at          TEXT    NOT NULL DEFAULT (datetime('now'))
);

CREATE TABLE IF NOT EXISTS loads (
    id           INTEGER PRIMARY KEY AUTOINCREMENT,
    job_id       INTEGER NOT NULL REFERENCES jobs(id),
    truck        TEXT    NOT NULL DEFAULT '',
    opened_at    TEXT    NOT NULL DEFAULT (datetime('now')),
    closed_at    TEXT,
    monitor_lb   REAL    NOT NULL DEFAULT 0,
    certified_lb REAL,
    factor       REAL    NOT NULL DEFAULT 1,
    cal_rev      INTEGER NOT NULL DEFAULT 0,
    flag         TEXT    NOT NULL DEFAULT '',
    status       TEXT    NOT NULL DEFAULT 'Active'
);

CREATE TABLE IF NOT EXISTS yield_data (
    id                 INTEGER PRIMARY KEY AUTOINCREMENT,
    job_id             INTEGER NOT NULL REFERENCES jobs(id),
    load_id            INTEGER NOT NULL DEFAULT -1,
    timestamp          TEXT    NOT NULL,
    latitude           REAL    NOT NULL DEFAULT 0,
    longitude          REAL    NOT NULL DEFAULT 0,
    elevation          REAL    NOT NULL DEFAULT 0,
    speed              REAL    NOT NULL DEFAULT 0,
    heading            REAL    NOT NULL DEFAULT 0,
    yield_rate         REAL    NOT NULL DEFAULT 0,
    acres_accumulated  REAL    NOT NULL DEFAULT 0,
    pounds_inc         REAL    NOT NULL DEFAULT 0,
    belt_pulses        INTEGER NOT NULL DEFAULT 0,
    belt_ft_min        REAL    NOT NULL DEFAULT 0,
    scale_lb           REAL    NOT NULL DEFAULT 0,
    scale_raw          INTEGER NOT NULL DEFAULT 0,
    cal_rev            INTEGER NOT NULL DEFAULT 0,
    rows_in_use        INTEGER NOT NULL DEFAULT 0
);

CREATE INDEX IF NOT EXISTS idx_yield_data_job  ON yield_data(job_id);
CREATE INDEX IF NOT EXISTS idx_yield_data_load ON yield_data(load_id);
CREATE INDEX IF NOT EXISTS idx_loads_job       ON loads(job_id);

-- Early BeltFlo test builds seeded an uncalibrated conveyor with span=1.
-- Zero is now the explicit uncalibrated value. Only rows with no recorded
-- zero timestamp are placeholders, so a real calibration is never touched.
UPDATE conveyor_config
SET span_lb_per_count = 0
WHERE zero_set_at IS NULL AND ABS(span_lb_per_count - 1.0) < 0.000001;
";
            using var cmd = new SQLiteCommand(sql, conn);
            cmd.ExecuteNonQuery();
        }
    }
}
