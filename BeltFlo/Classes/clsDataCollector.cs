using System;
using System.Collections.Generic;
using BeltFlo.Database;
using BeltFlo.Language;

namespace BeltFlo.Classes
{
    /// <summary>
    /// Accumulates yield data points during an active job.
    ///
    /// Positions are buffered for ProcessingDelaySec (the crop's transport time from
    /// the share to the weigh section) and paired with the flow measured when they
    /// drain, so crop is mapped where it was actually dug and the tail still on the
    /// belts after sections go off is not lost.
    ///
    /// Two totals, two sources. Mass — the job total and the open load — comes
    /// straight from the module's differenced pounds counter, credited the moment a
    /// packet arrives, so it is exact regardless of GPS. The map records yield rate
    /// per position. Neither is derived from the other.
    ///
    /// Writes one record per second to the database, carrying the mean yield of
    /// that second. Pass boundaries are exact: the first point after sections
    /// come on and the last point before they go off are always written, and a
    /// zero-yield marker row at the off position breaks the map ribbon so
    /// ground crossed with sections off is never painted.
    /// </summary>
    public class clsDataCollector
    {
        // One entry per GPS tick while sections are on, drained after ProcessingDelaySec
        private struct PendingPoint
        {
            public DateTime Time;
            public double Lat, Lon;
            public float Altitude, Speed, Heading;
            public double AcresInc;
            public double NewFraction;  // 0..1 of this step's swath that was not already dug — 1 when nothing overlapped
            public bool PassStart;  // first point after sections came on — force-written so the pass begins exactly here
            public bool PassEnd;    // break marker queued when sections went off — becomes a zero-yield row at the off position
        }

        private readonly Queue<PendingPoint> _pipeline = new Queue<PendingPoint>();

        // Yield samples accumulated since the last DB write. Each written row
        // carries the MEAN of the ~10 per-tick yields of its second, not one
        // instantaneous sample, so a transient dip at the write instant cannot
        // punch a false gap in the map ribbon. A row is zero only if the whole
        // interval had no flow.
        private double _yieldSum;
        private int    _yieldSamples;

        // Last drained point not yet written (the 1 Hz write gate skips most
        // points). Flushed when its pass ends so the ribbon reaches the exact
        // section-off position instead of stopping at the last whole second.
        private PendingPoint _lastDrained;
        private bool _lastDrainedUnwritten;

        public bool IsRecording { get; private set; }
        public bool IsAutoPaused { get; private set; }  // true only when paused by AOG condition (not manually)
        public int ActiveJobId { get; private set; } = -1;
        public string ActiveJobName { get; private set; } = "";

        public double TotalAcres { get; private set; }
        public double TotalPounds { get; private set; }
        public double AverageYield => TotalAcres > 0.01 ? TotalPounds / TotalAcres : 0;   // lb/ac

        // The truck being filled. -1 between loads; pounds then go to the job only.
        public int ActiveLoadId { get; private set; } = -1;
        public double CurrentLoadLb { get; private set; }

        // The open load's number within this job — 1 for the first truck — which is
        // what the operator and the ticket talk about. The database id is global.
        public int ActiveLoadNumber { get; private set; }

        // Paused from the run screen: nothing is counted — no weight to the job or
        // the load, no map points — for belt cleaning, clearing a jam, or dumping
        // on purpose. Distinct from the auto-pause that follows AOG sections.
        public bool IsPaused => ActiveJobId > 0 && !IsRecording && !IsAutoPaused;

        // Sections came on while paused and auto-resume is off: the run screen
        // sounds an alarm until the operator resumes or sections go off again.
        public bool PausedSectionsAlarm { get; private set; }
        private bool _sectionsOnWhilePaused;

        // Crop crossing the scale with no truck load open, on a harvester whose scale
        // weighs straight into the truck: that crop is going into a truck no load
        // will claim. Not raised when the scale weighs into a tank — there the tank
        // fills between trucks as a matter of course. Weight still counts to the job
        // and the map while it sounds.
        public bool NoLoadAlarm { get; private set; }
        private DateTime _noLoadFlowSince = DateTime.MaxValue;
        private const double NoLoadAlarmSec = 5.0;   // so a clod or a clean-out tail doesn't set it off

        // Positions still waiting for their crop to reach the scale. Exposed for
        // the diagnostic log: it hits 0 exactly when the app stops attributing
        // flow to a finished pass.
        public int PipelineCount => _pipeline.Count;

        private DateTime _lastWriteTime = DateTime.MinValue;
        private double _lastLat = 0, _lastLon = 0;
        private DateTime _lastFixTime = DateTime.MinValue;

        // Max plausible ground speed, used to bound a single GPS-tick's distance
        // step — guards TotalAcres against a corrupt fix (e.g. a momentary (0,0)
        // glitch) producing a bogus multi-km jump.
        private const double MaxPlausibleSpeedMps = 15.0; // ~34 mph, generous ceiling
        private const double MinFixIntervalSec = 0.05;

        // --- Overlap ---------------------------------------------------------
        // Ground already dug is not new acres, and a digger only partly in crop is
        // not lifting its full width. Both come from the same grid: see
        // clsCoverageGrid for why it returns a ratio rather than an area.
        private readonly clsCoverageGrid _coverage = new clsCoverageGrid();

        /// <summary>Fraction of the last swath that was new ground. Diagnostic log only.</summary>
        public double LastNewFraction { get; private set; } = 1.0;

        // Below this the machine is essentially re-running ground it already dug.
        // Dividing a part-width flow by a vanishing width sends yield to infinity,
        // so under this the tick produces no area, no lb/ac and no map row. The
        // mass is unaffected — it came off the scale and is already counted.
        private const double MinNewFraction = 0.15;

        // Replay guards when rebuilding coverage for a resumed job. Same numbers as
        // the map's ribbon breaks (frmYieldMap MaxBridgeMeters/Seconds), so the
        // ground the grid believes was dug is the ground the map painted.
        private const double MaxRebuildStepM = 5.0;
        private const double MaxRebuildGapSec = 3.0;

        // --- Crash safety ----------------------------------------------------
        // The totals live in memory and reach the job row on a lifecycle event —
        // StartJob, SuspendJob, StopJob, LoadJob. A power cut or a killed process
        // would otherwise roll the job back to the last clean exit, which could be
        // hours. Saving once a minute bounds that to a minute's digging; skipped
        // when nothing has moved, so a parked machine never touches the disk. The
        // open load's running weight is saved on the same beat.
        private const double TotalsSaveIntervalSec = 60.0;
        private DateTime _lastTotalsSave = DateTime.MinValue;
        private double _savedAcres = -1, _savedPounds = -1, _savedLoadLb = -1;

        // Pounds over the scale since the last row was written, and the belt pulse
        // count at that row, so each row carries what arrived during its interval.
        private double _poundsSinceWrite;
        private uint _pulsesAtLastWrite;
        private bool _hasPulseMark;

        // --- Tail drain -----------------------------------------------------
        // A pass stops being integrated when its last position drains, one
        // ProcessingDelaySec after sections go off. The belts are not empty at
        // that moment: crop already on them keeps arriving at the scale. Mass is
        // counted regardless — it comes off the counter — but the tail keeps the
        // job in the recording state until the belt actually runs empty, so the
        // status bar and the auto-pause tell the truth about what the machine is
        // doing.
        private bool _tailActive;
        private DateTime _tailStart;
        private DateTime _tailEndsAt;      // set when the next pass's crop is due to arrive
        private DateTime _tailEmptySince;  // first below-threshold reading of the current run

        public bool IsDrainingTail => _tailActive;
        public string LastTailEndReason { get; private set; } = "";

        // Backstop only. A belt that never reads empty — a zero that has drifted,
        // or a stuck reading — would otherwise hold the job "recording" forever.
        // Clean-out on a digger is tens of seconds; 120 clears that with room.
        private const double TailTimeoutSec = 120.0;

        // Flow has to stay down this long to end a drain, so a single dropped or
        // glitched reading mid-tail cannot truncate it early.
        private const double TailEmptyConfirmSec = 0.5;

        // --- Scale validity -------------------------------------------------
        // A reading the scale did not actually make is worse than no reading: it
        // maps, it accumulates, and nothing about it looks wrong afterwards.
        // Recording stops while the module says the scale cannot be trusted, the
        // same way it stops for a GPS dropout, and the pass is closed off rather
        // than bridged across the gap.
        private bool _scaleFault;

        /// <summary>True while recording is held off because the scale cannot be trusted.</summary>
        public bool ScaleFault => _scaleFault;

        /// <summary>
        /// Adopts a rescaled total for the job currently recording. Without this,
        /// RescaleJob's new total_pounds survives only until the next lifecycle
        /// write, which would put the stale in-memory figure straight back.
        /// No-op for any other job.
        /// </summary>
        public void SyncTotalPounds(int jobId, double totalPounds)
        {
            if (jobId > 0 && jobId == ActiveJobId) TotalPounds = totalPounds;
        }

        private void SaveTotalsPeriodically()
        {
            if (ActiveJobId <= 0 || Core.Database == null) return;
            if ((DateTime.UtcNow - _lastTotalsSave).TotalSeconds < TotalsSaveIntervalSec) return;
            if (TotalAcres == _savedAcres && TotalPounds == _savedPounds && CurrentLoadLb == _savedLoadLb) return;

            MarkTotalsSaved();
            Core.Database.Jobs.UpdateTotals(ActiveJobId, TotalAcres, TotalPounds);
            if (ActiveLoadId > 0)
                Core.Database.Loads.UpdateMonitorLb(ActiveLoadId, CurrentLoadLb);
        }

        /// <summary>
        /// Notes that the database and the in-memory totals agree as of now, and
        /// restarts the interval. Called wherever a lifecycle event has just
        /// written them, so a freshly opened job does not save on its first tick.
        /// </summary>
        private void MarkTotalsSaved()
        {
            _lastTotalsSave = DateTime.UtcNow;
            _savedAcres = TotalAcres;
            _savedPounds = TotalPounds;
            _savedLoadLb = CurrentLoadLb;
        }

        // ── Job lifecycle ─────────────────────────────────────────────────────

        public void StartJob(int jobId, string jobName = "")
        {
            // Close any currently active job before starting a new one
            if (ActiveJobId > 0 && Core.Database != null)
            {
                FinishLoad();
                Core.Database.Jobs.UpdateTotals(ActiveJobId, TotalAcres, TotalPounds);
                Core.Database.Jobs.Close(ActiveJobId);
            }

            ActiveJobId = jobId;
            ActiveJobName = jobName;
            TotalAcres = 0;
            TotalPounds = 0;
            ActiveLoadId = -1;
            CurrentLoadLb = 0;
            _poundsSinceWrite = 0;
            _hasPulseMark = false;
            _lastLat = 0;
            _lastLon = 0;
            _lastFixTime = DateTime.MinValue;
            _lastWriteTime = DateTime.MinValue;
            _scaleFault = false;
            _coverage.Reset();
            ResetPipeline();
            MarkTotalsSaved();
            IsRecording  = false;
            IsAutoPaused = true;   // starts recording when sections come on
        }

        public void RenameActiveJob(string name) => ActiveJobName = name;

        public void StopJob()
        {
            IsRecording = false;
            ResetPipeline();   // crop still in transit is abandoned on an explicit stop
            if (ActiveJobId > 0 && Core.Database != null)
            {
                FinishLoad();
                Core.Database.Jobs.UpdateTotals(ActiveJobId, TotalAcres, TotalPounds);
                Core.Database.Jobs.Close(ActiveJobId);
            }
            ActiveJobId = -1;
            ActiveJobName = "";
        }

        // Manual pause discards in-transit positions; their flow can't be
        // matched after an arbitrary pause. Pounds stop being credited too — a
        // manual pause means "this is not part of the job". The pass is closed so
        // resuming starts a fresh ribbon rather than bridging the gap. Sections
        // already on when pause is pressed do not count as coming on, or pausing
        // with the digger down would undo itself at once.
        public void PauseJob()
        {
            if (ActiveJobId <= 0 || IsPaused) return;
            IsRecording  = false;
            IsAutoPaused = false;
            ResetPipeline();
            if (_lastLat != 0 || _lastLon != 0) _coverage.Flush();
            _lastLat = 0;
            _lastLon = 0;
            _lastFixTime = DateTime.MinValue;
            _sectionsOnWhilePaused = Core.GPS.IsConnected && Core.GPS.SectionsActive;
            PausedSectionsAlarm = false;
            Props.WriteActivityLog("Paused - nothing counted");
            Core.RaiseJobStateChanged();
        }

        // Abandon everything in transit: buffered positions, the yield average
        // in progress, and any unwritten drained point that belonged to them.
        private void ResetPipeline()
        {
            _pipeline.Clear();
            _yieldSum = 0;
            _yieldSamples = 0;
            _lastDrainedUnwritten = false;
            _tailActive = false;
        }

        private void AutoPause() { IsRecording = false; IsAutoPaused = true; }

        private void AutoResume() { IsRecording = true; IsAutoPaused = false; }

        /// <summary>
        /// Saves accumulated totals to the DB but leaves the job status as Active
        /// so it can be auto-resumed on the next app start. The open load stays
        /// open for the same reason — the truck is still there tomorrow.
        /// </summary>
        public void SuspendJob()
        {
            IsRecording = false;
            ResetPipeline();
            if (ActiveJobId > 0 && Core.Database != null)
            {
                Core.Database.Jobs.UpdateTotals(ActiveJobId, TotalAcres, TotalPounds);
                if (ActiveLoadId > 0)
                    Core.Database.Loads.UpdateMonitorLb(ActiveLoadId, CurrentLoadLb);
            }
            ActiveJobId   = -1;
            ActiveJobName = "";
            ActiveLoadId  = -1;
            CurrentLoadLb = 0;
        }

        // Re-arms the auto-pause: recording starts as soon as sections are on and
        // the scale is good.
        public void ResumeJob()
        {
            if (!IsPaused) return;
            IsRecording  = false;
            IsAutoPaused = true;
            PausedSectionsAlarm = false;
            Props.WriteActivityLog("Resumed");
            Core.RaiseJobStateChanged();
        }

        /// <summary>
        /// Loads a previously created job, restoring its accumulated totals and
        /// any load that was still being filled.
        /// </summary>
        public void LoadJob(int jobId, string jobName, double existingAcres, double existingPounds)
        {
            // Close any different active job before loading the new one
            if (ActiveJobId > 0 && ActiveJobId != jobId && Core.Database != null)
            {
                FinishLoad();
                Core.Database.Jobs.UpdateTotals(ActiveJobId, TotalAcres, TotalPounds);
                Core.Database.Jobs.Close(ActiveJobId);
            }

            ActiveJobId = jobId;
            ActiveJobName = jobName;
            TotalAcres = existingAcres;
            TotalPounds = existingPounds;
            _poundsSinceWrite = 0;
            _hasPulseMark = false;
            _lastLat = 0;
            _lastLon = 0;
            _lastFixTime = DateTime.MinValue;
            _lastWriteTime = DateTime.MinValue;
            _scaleFault = false;
            ResetPipeline();
            RebuildCoverage(jobId);

            ActiveLoadId  = -1;
            CurrentLoadLb = 0;
            var open = Core.Database?.Loads.GetOpen(jobId);
            ActiveLoadNumber = 0;
            if (open != null)
            {
                ActiveLoadId  = open.Id;
                CurrentLoadLb = open.MonitorLb;
                ActiveLoadNumber = Core.Database.Loads.GetAll(jobId).FindAll(l => l.Id <= open.Id).Count;
            }

            MarkTotalsSaved();
            IsRecording  = false;
            IsAutoPaused = true;   // auto-resumes once AOG connects and digging starts
        }

        /// <summary>
        /// Replays a job's stored positions through the coverage grid so a resumed
        /// job knows what it already dug.
        ///
        /// Without this the grid starts empty on resume and the first lap back over
        /// yesterday's ground is charged as new acres. Every point needed is already
        /// in yield_data, so this is a replay, not an estimate.
        ///
        /// The break guards match the map's swath drawer: a pair of points is only a
        /// swath if both ends were flowing and they are close enough in time and
        /// distance to be consecutive.
        /// </summary>
        private void RebuildCoverage(int jobId)
        {
            _coverage.Reset();
            if (jobId <= 0 || Core.Database == null) return;

            try
            {
                var points = Core.Database.YieldData.GetByJob(jobId);
                double widthM = Core.Yield != null ? Core.Yield.DiggingWidthM : 0;
                if (widthM <= 0 || points.Count < 2) return;

                int swaths = 0;
                for (int i = 1; i < points.Count; i++)
                {
                    var a = points[i - 1];
                    var b = points[i];

                    if (a.YieldRate <= 0 || b.YieldRate <= 0) continue;
                    if ((b.Timestamp - a.Timestamp).TotalSeconds > MaxRebuildGapSec) continue;

                    double distM = HaversineMetres(a.Latitude, a.Longitude, b.Latitude, b.Longitude);
                    if (distM > MaxRebuildStepM) continue;

                    _coverage.MarkSwath(a.Latitude, a.Longitude, b.Latitude, b.Longitude, widthM);
                    swaths++;
                }

                _coverage.Flush();
                Props.WriteActivityLog("Coverage rebuilt for job " + jobId + ": "
                    + swaths + " swaths, " + _coverage.TileCount + " tiles");
            }
            catch (Exception ex)
            {
                // A job that cannot be replayed still records — it just cannot
                // credit itself for ground dug before the restart.
                Props.WriteErrorLog("DataCollector/RebuildCoverage: " + ex.Message);
            }
        }

        // ── Loads ─────────────────────────────────────────────────────────────

        /// <summary>
        /// Opens a new truck load on the active job, closing the one being filled.
        /// Returns the new load id, or -1 with no job.
        /// </summary>
        public int StartLoad(string truck = "")
        {
            if (ActiveJobId <= 0 || Core.Database == null) return -1;
            FinishLoad();
            ActiveLoadId  = Core.Database.Loads.Create(ActiveJobId, truck, Core.ActiveCalRev);
            CurrentLoadLb = 0;
            ActiveLoadNumber = Core.Database.Loads.GetAll(ActiveJobId).Count;
            Props.WriteActivityLog("Load " + ActiveLoadNumber + " started (id " + ActiveLoadId + ")");
            MarkTotalsSaved();
            Core.RaiseJobStateChanged();
            return ActiveLoadId;
        }

        /// <summary>
        /// The truck has left: freezes the monitor weight on the load and waits for
        /// its ticket. Pounds arriving afterwards go to the job alone until the
        /// next load is opened.
        /// </summary>
        public void FinishLoad()
        {
            if (ActiveLoadId <= 0) return;
            Core.Database?.Loads.Close(ActiveLoadId, CurrentLoadLb);
            Props.WriteActivityLog("Load " + ActiveLoadNumber + " finished at " + CurrentLoadLb.ToString("0") + " lb (id " + ActiveLoadId + ")");
            ActiveLoadId  = -1;
            CurrentLoadLb = 0;
            ActiveLoadNumber = 0;
            MarkTotalsSaved();
            Core.RaiseJobStateChanged();
        }

        /// <summary>
        /// Continues a finished load of the running job — a truck pulled aside and
        /// brought back to be topped up. Only a load still waiting for its ticket,
        /// and only while no other load is open. Returns false when not allowed.
        /// </summary>
        public bool ReopenLoad(int loadId)
        {
            if (ActiveJobId <= 0 || ActiveLoadId > 0 || Core.Database == null) return false;
            var load = Core.Database.Loads.GetById(loadId);
            if (load == null || load.JobId != ActiveJobId || load.Status != LoadRecord.StatusWaiting) return false;

            Core.Database.Loads.Reopen(loadId);
            ActiveLoadId  = loadId;
            CurrentLoadLb = load.MonitorLb;
            ActiveLoadNumber = Core.Database.Loads.GetAll(ActiveJobId).FindAll(l => l.Id <= loadId).Count;
            Props.WriteActivityLog("Load " + ActiveLoadNumber + " reopened at " + CurrentLoadLb.ToString("0") + " lb (id " + loadId + ")");
            MarkTotalsSaved();
            Core.RaiseJobStateChanged();
            return true;
        }

        // ── Mass ──────────────────────────────────────────────────────────────

        /// <summary>
        /// Pounds the module says crossed the scale since its previous packet.
        /// Credited to the job and the open load whenever a job is open and not
        /// manually paused — crop reaching the truck while the machine is turning,
        /// or emptying out while parked, is real crop. Only a manual pause says
        /// "this is not part of the job".
        /// </summary>
        public void OnPoundsDelta(double lb)
        {
            if (lb <= 0 || ActiveJobId < 0) return;
            if (!IsRecording && !IsAutoPaused) return;   // manual pause
            if (_scaleFault) return;                     // the module said not to trust this

            TotalPounds       += lb;
            if (ActiveLoadId > 0)
                CurrentLoadLb += lb;
            _poundsSinceWrite += lb;
        }

        // ── Position ──────────────────────────────────────────────────────────

        /// <summary>Called for every counter packet with whether crop is flowing over the scale.</summary>
        public void CheckNoLoad(bool flowing)
        {
            bool noLoadFlow = ActiveJobId > 0 && !IsPaused && ActiveLoadId <= 0
                              && flowing && !Core.ScaleWeighsIntoTank;
            if (!noLoadFlow)
            {
                _noLoadFlowSince = DateTime.MaxValue;
                if (NoLoadAlarm)
                {
                    NoLoadAlarm = false;
                    Core.RaiseJobStateChanged();
                }
                return;
            }

            if (_noLoadFlowSince == DateTime.MaxValue) _noLoadFlowSince = DateTime.UtcNow;
            if (!NoLoadAlarm && (DateTime.UtcNow - _noLoadFlowSince).TotalSeconds >= NoLoadAlarmSec)
            {
                NoLoadAlarm = true;
                Props.WriteActivityLog("No load open while crop is crossing the scale");
                Core.RaiseJobStateChanged();
            }
        }

        // Sections switching on while paused: resume if the setting allows it,
        // otherwise raise the run screen's alarm. Only the off-to-on change counts,
        // and the alarm clears when sections go off again.
        private void CheckSectionsWhilePaused(clsGPS gps)
        {
            bool sectionsOn = gps.IsConnected && gps.SectionsActive;
            bool cameOn = sectionsOn && !_sectionsOnWhilePaused;
            _sectionsOnWhilePaused = sectionsOn;

            if (!sectionsOn)
            {
                if (PausedSectionsAlarm)
                {
                    PausedSectionsAlarm = false;
                    Core.RaiseJobStateChanged();
                }
                return;
            }
            if (!cameOn) return;

            if (Properties.Settings.Default.AutoResumePause)
            {
                ResumeJob();
                Props.ShowMessage(Lang.lgResumedSectionsOn, "", 3000);
            }
            else
            {
                PausedSectionsAlarm = true;
                Props.WriteActivityLog("Sections on while paused - nothing counted");
                Core.RaiseJobStateChanged();
            }
        }

        /// <summary>
        /// Called every GPS update (~10 Hz). Writes to DB once per second.
        /// </summary>
        public void OnGpsUpdate()
        {
            if (ActiveJobId < 0) return;

            var gps = Core.GPS;

            // Paused from the run screen: nothing is recorded, but sections coming
            // on are watched for — they mean crop is probably crossing the scale
            // uncounted.
            if (IsPaused)
            {
                CheckSectionsWhilePaused(gps);
                return;
            }

            if (!gps.IsConnected) return;

            var yield = Core.Yield;

            // Sections turn off over already-dug ground even when moving.
            bool harvestActive = gps.SectionsActive;

            // Nothing below this point can produce real data while the scale is
            // untrusted — the flow every calculation depends on would be fabricated.
            // Checked before the position is buffered so no point enters the
            // pipeline whose crop arrives during the blind window.
            if (!ScaleUsable())
            {
                // The readout has to fall to zero rather than freeze at the last
                // good value: a frozen number reads as a live one.
                yield.Calculate(0);

                if (!_scaleFault)
                {
                    _scaleFault = true;
                    bool wasRecording = IsRecording;

                    Props.WriteErrorLog("DataCollector/Scale invalid — recording paused"
                        + " (moduleConnected=" + Core.ModuleConnected
                        + ", scaleOk=" + Core.LastScaleOk
                        + ", flags=0x" + Core.LastFlags.ToString("X2") + ")");

                    // Alarm only when this actually interrupted digging. A module
                    // dropping out between passes stops nothing, and an alert the
                    // operator learns to dismiss is worse than no alert.
                    if (wasRecording)
                        Props.ShowMessage(Lang.lgScaleFault, "", 4000, true);

                    EndPassOnFault(gps);
                    if (!IsAutoPaused) AutoPause();
                    Core.RaiseJobStateChanged();
                }
                return;
            }

            if (_scaleFault)
            {
                _scaleFault = false;
                Props.WriteActivityLog("Scale valid again");
                // Only claim recording resumed when it does — auto-resume still
                // waits for sections, so with the digger up nothing restarts yet.
                if (harvestActive)
                    Props.ShowMessage(Lang.lgScaleRestored, "", 3000);
                Core.RaiseJobStateChanged();
            }

            // AOG sends its pivot position; the crop is lifted at the digger, which
            // sits AheadOfPivotM from it (negative behind, as on a towed harvester).
            // Record the digger position so pass boundaries land where it crossed
            // them — AOG paints its coverage at the tool the same way.
            double hdgRad = gps.Heading * Math.PI / 180.0;
            double lat = gps.Latitude
                       + yield.AheadOfPivotM * Math.Cos(hdgRad) / 111320.0;
            double lon = gps.Longitude
                       + yield.AheadOfPivotM * Math.Sin(hdgRad)
                         / Math.Max(1.0, 111320.0 * Math.Cos(gps.Latitude * Math.PI / 180.0));

            if (harvestActive)
            {
                // A new pass has started while the previous one is still draining.
                // Its crop cannot reach the scale for another ProcessingDelaySec,
                // so everything arriving before then still belongs to the old pass —
                // the tail runs on until exactly the moment the new crop is due.
                if (_tailActive && _tailEndsAt == DateTime.MaxValue)
                    _tailEndsAt = DateTime.UtcNow.AddSeconds(yield.ProcessingDelaySec);

                // Crop is entering the machine — buffer this position. Its crop
                // reaches the scale ProcessingDelaySec from now.
                bool passStart = _lastLat == 0 && _lastLon == 0;
                double acresInc = 0;
                double newFraction = 1.0;
                DateTime now = DateTime.UtcNow;
                if (!passStart)
                {
                    double distM = HaversineMetres(_lastLat, _lastLon, lat, lon);
                    double dtSec = Math.Max((now - _lastFixTime).TotalSeconds, MinFixIntervalSec);
                    if (distM <= MaxPlausibleSpeedMps * dtSec)
                    {
                        // Ground already dug is not new ground. Marking happens here,
                        // at the position, not at drain time — the grid is about where
                        // the share has been, which has nothing to do with when that
                        // strip's crop reaches the scale.
                        newFraction = _coverage.MarkSwath(_lastLat, _lastLon, lat, lon, yield.DiggingWidthM);
                        LastNewFraction = newFraction;
                        acresInc = clsYieldCalculator.MetresToAcres(distM, yield.DiggingWidthM) * newFraction;
                    }
                    // else: implausible GPS jump (e.g. a momentary 0,0 glitch fix) —
                    // skip this tick's acreage/yield contribution instead of adding a
                    // bogus multi-km increment to the job total. The point is still
                    // enqueued below; the map's own AddSwath/MaxBridgeMeters guard
                    // already refuses to bridge a gap this large.
                }
                _lastLat = lat;
                _lastLon = lon;
                _lastFixTime = now;

                _pipeline.Enqueue(new PendingPoint
                {
                    Time = now,
                    Lat = lat,
                    Lon = lon,
                    Altitude = gps.Altitude,
                    Speed = gps.Speed,
                    Heading = gps.Heading,
                    AcresInc = acresInc,
                    NewFraction = newFraction,
                    PassStart = passStart
                });
            }
            else
            {
                // Digger up / sections off — no new crop, but keep draining the
                // pipeline: delay-time's worth of crop is still on the belts.
                if (_lastLat != 0 || _lastLon != 0)
                {
                    // Sections just went off: send a pass-end marker down the
                    // pipeline at the last dug position. When it drains it ends the
                    // map ribbon exactly there, so a brief section-off can never be
                    // painted across.
                    _pipeline.Enqueue(new PendingPoint
                    {
                        Time = DateTime.UtcNow,
                        Lat = _lastLat,
                        Lon = _lastLon,
                        Altitude = gps.Altitude,
                        Speed = gps.Speed,
                        Heading = gps.Heading,
                        AcresInc = 0,
                        PassEnd = true
                    });

                    // The pass is over positionally, so its last few metres can come
                    // out of the coverage grid's lag and become dug ground. The next
                    // pass may cross them within seconds of the turn.
                    _coverage.Flush();
                }
                _lastLat = 0;
                _lastLon = 0;
                _lastFixTime = DateTime.MinValue;
            }

            // Drain positions older than the transport delay — their crop is at
            // the scale now, so pair them with the current flow reading.
            DateTime cutoff = DateTime.UtcNow.AddSeconds(-yield.ProcessingDelaySec);

            while (_pipeline.Count > 0 && _pipeline.Peek().Time <= cutoff)
            {
                PendingPoint pt = _pipeline.Dequeue();

                if (pt.PassEnd)
                {
                    // The pass ending at this position has fully drained. Flush
                    // the final real point so the ribbon reaches the section-off
                    // location, then write a zero-yield marker row there — the
                    // map's both-ends-flowing guard turns it into a guaranteed
                    // ribbon break however short the section-off was.
                    if (_lastDrainedUnwritten)
                        WritePoint(_lastDrained, _yieldSum / _yieldSamples);
                    WritePoint(pt, 0);
                    _yieldSum = 0;
                    _yieldSamples = 0;
                    _lastDrainedUnwritten = false;
                    _lastWriteTime = DateTime.UtcNow;

                    // Everything positional for this pass is now written, but the
                    // machine is still delivering its crop. Stay in the recording
                    // state until it runs empty.
                    BeginTailDrain();
                    continue;
                }

                // Real crop is arriving again — the previous pass's tail is over
                // whether or not the timer said so.
                if (_tailActive) EndTailDrain("next pass");

                // Only the part of the digger in standing crop produced this flow,
                // so only that width may divide it. Floored for the calculation so a
                // near-total overlap cannot divide by nearly nothing; the tick is
                // then excluded from the record below rather than trusted.
                double newFrac = pt.NewFraction;
                yield.Calculate(pt.Speed, yield.DiggingWidthM * Math.Max(newFrac, MinNewFraction));

                if (newFrac < MinNewFraction)
                {
                    // Re-running ground already dug. There is no new area, so there
                    // is no lb/ac to compute and nothing to map. The crop's mass is
                    // already in the totals from the counter.
                    continue;
                }

                TotalAcres += pt.AcresInc;

                _yieldSum += yield.InstantYield;
                _yieldSamples++;
                _lastDrained = pt;
                _lastDrainedUnwritten = true;

                // Write to DB once per second; a pass-start point is written
                // immediately so the ribbon begins exactly where sections came on.
                if (pt.PassStart || (DateTime.UtcNow - _lastWriteTime).TotalSeconds >= 1.0)
                {
                    _lastWriteTime = DateTime.UtcNow;
                    WritePoint(pt, _yieldSum / _yieldSamples);
                    _yieldSum = 0;
                    _yieldSamples = 0;
                    _lastDrainedUnwritten = false;
                }
            }

            if (_tailActive)
                CheckTail(yield);

            // Bound what a power cut can undo to one minute of digging.
            SaveTotalsPeriodically();

            // Keep the live display honest while idle
            if (!harvestActive && _pipeline.Count == 0)
                yield.Calculate(gps.Speed);

            // Recording while crop is entering the machine, crop is still in
            // transit, or the machine is still emptying out the last pass
            bool shouldRecord = harvestActive || _pipeline.Count > 0 || _tailActive;

            if (shouldRecord && IsAutoPaused)
            {
                AutoResume();
                Core.RaiseJobStateChanged();
            }
            else if (!shouldRecord && !IsAutoPaused)
            {
                AutoPause();
                Core.RaiseJobStateChanged();
            }
        }

        /// <summary>
        /// Whether the current flow reading can be trusted for mapping and totals.
        /// Every part matters:
        ///   - packets are arriving,
        ///   - settings are reaching the module (otherwise it may hold another
        ///     profile's zero/span/geometry),
        ///   - the converter is healthy and not overloaded,
        ///   - the module confirms a zero,
        ///   - the active app profile has a real known-weight span.
        /// </summary>
        private bool ScaleUsable()
        {
            return Core.ModuleConnected
                && Core.ModuleReceiving
                && Core.LastScaleOk
                && !Core.LastOverload
                && Core.LastTared
                && Core.ActiveScaleCalibrated;
        }

        /// <summary>
        /// Closes the pass at the last good position when the scale goes blind.
        /// Buffered positions are abandoned rather than written: their crop
        /// reaches the scale during the blind window, so pairing them with any
        /// later reading would invent data for ground that was never measured.
        /// The zero-yield marker breaks the map ribbon here for the same reason a
        /// section-off does — without it the map paints straight across the gap.
        /// </summary>
        private void EndPassOnFault(clsGPS gps)
        {
            if (_lastDrainedUnwritten && _yieldSamples > 0)
                WritePoint(_lastDrained, _yieldSum / _yieldSamples);

            if (_lastLat != 0 || _lastLon != 0)
                WritePoint(new PendingPoint
                {
                    Time     = DateTime.UtcNow,
                    Lat      = _lastLat,
                    Lon      = _lastLon,
                    Altitude = gps.Altitude,
                    Speed    = gps.Speed,
                    Heading  = gps.Heading,
                    AcresInc = 0
                }, 0);

            if (_tailActive) EndTailDrain("scale fault");
            _coverage.Flush();   // ground dug before the fault is still dug
            ResetPipeline();

            // Recovery starts a fresh pass. Without this the first good tick
            // measures its distance from the pre-fault position and charges the
            // whole blind window's travel to the job as dug acres.
            _lastLat = 0;
            _lastLon = 0;
            _lastFixTime   = DateTime.MinValue;
            _lastWriteTime = DateTime.MinValue;
        }

        private void BeginTailDrain()
        {
            _tailActive     = true;
            _tailStart      = DateTime.UtcNow;
            _tailEndsAt     = DateTime.MaxValue;
            _tailEmptySince = DateTime.MaxValue;
        }

        /// <summary>
        /// Keeps the job in the recording state while crop is still leaving the
        /// machine after a pass has ended. Runs until the belt is empty, until the
        /// next pass's crop is due, or until the timeout — whichever comes first.
        /// </summary>
        private void CheckTail(clsYieldCalculator yield)
        {
            DateTime now = DateTime.UtcNow;

            if (!yield.IsFlowing)
            {
                if (_tailEmptySince == DateTime.MaxValue) _tailEmptySince = now;
                if ((now - _tailEmptySince).TotalSeconds >= TailEmptyConfirmSec)
                {
                    EndTailDrain("empty");
                    return;
                }
            }
            else
            {
                _tailEmptySince = DateTime.MaxValue;   // flow came back — not empty yet
            }

            if (now >= _tailEndsAt)
            {
                EndTailDrain("next pass");
                return;
            }

            if ((now - _tailStart).TotalSeconds >= TailTimeoutSec)
                EndTailDrain("timeout");
        }

        private void EndTailDrain(string reason)
        {
            _tailActive       = false;
            _tailEndsAt       = DateTime.MaxValue;
            _tailEmptySince   = DateTime.MaxValue;
            LastTailEndReason = reason;

            // A timeout is never normal: it means flow never fell below the
            // threshold, which is the zero drifting rather than the machine being
            // slow. Worth a line in the log — the symptom otherwise is a job total
            // that quietly disagrees with the weigh ticket.
            if (reason == "timeout")
                Props.WriteErrorLog("DataCollector/TailDrain still flowing after "
                                    + TailTimeoutSec.ToString("0") + " s — re-zero the scale");
        }

        private void WritePoint(PendingPoint pt, double yieldRate)
        {
            int pulses = 0;
            if (_hasPulseMark)
                pulses = (int)Math.Min(int.MaxValue, unchecked(Core.LastCumPulses - _pulsesAtLastWrite));
            _pulsesAtLastWrite = Core.LastCumPulses;
            _hasPulseMark = true;

            var point = new YieldDataPoint
            {
                JobId = ActiveJobId,
                LoadId = ActiveLoadId,
                Timestamp = pt.Time,
                Latitude = pt.Lat,
                Longitude = pt.Lon,
                Elevation = pt.Altitude,
                Speed = pt.Speed,
                Heading = pt.Heading,
                YieldRate = yieldRate,
                AcresAccumulated = TotalAcres,
                PoundsInc = _poundsSinceWrite,
                BeltPulses = pulses,
                BeltFtMin = Core.Yield?.BeltFtPerMin ?? 0,
                ScaleLb = Core.LastScaleLb,
                ScaleRaw = Core.LastScaleRaw,
                // The full revision id from the database, which never wraps. The
                // module's settings checksum only confirms it is weighing with it.
                CalRev = Core.ActiveCalRev,
                RowsInUse = Core.ActiveRowsInUse
            };
            _poundsSinceWrite = 0;

            Core.LastDataWriteOk = Core.Database?.YieldData.Insert(point) ?? true;
        }

        private static double HaversineMetres(double lat1, double lon1, double lat2, double lon2)
        {
            const double R = 6_371_000; // earth radius metres
            double dLat = ToRad(lat2 - lat1);
            double dLon = ToRad(lon2 - lon1);
            double a = Math.Sin(dLat / 2) * Math.Sin(dLat / 2)
                     + Math.Cos(ToRad(lat1)) * Math.Cos(ToRad(lat2))
                     * Math.Sin(dLon / 2) * Math.Sin(dLon / 2);
            return R * 2 * Math.Atan2(Math.Sqrt(a), Math.Sqrt(1 - a));
        }

        private static double ToRad(double deg) => deg * Math.PI / 180.0;
    }
}
