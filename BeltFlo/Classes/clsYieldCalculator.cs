using System;

namespace BeltFlo.Classes
{
    /// <summary>
    /// Turns the module's cumulative conveyor counters into a mass flow and a yield
    /// rate. The module weighs the belt section, integrates load against belt travel
    /// and reports cumulative pounds and cumulative belt pulses; this class differences
    /// them packet to packet, so a dropped packet costs nothing and a module restart
    /// is detected rather than counted.
    ///
    /// The digging-to-scale transport delay is handled by the position pipeline in
    /// clsDataCollector, which pairs the current flow with the position dug
    /// ProcessingDelaySec earlier.
    /// </summary>
    public class clsYieldCalculator
    {
        // From the active profile's conveyor configuration
        public int ProcessingDelaySec { get; set; } = 10;             // digging-to-scale delay
        public double InchesPerPulse { get; set; } = 1.0;             // belt travel per proximity pulse
        public double FlowThresholdLbPerSec { get; set; } = 0.05;     // below this the belt is running empty

        // From the harvester profile: rows harvested × row spacing, and the digger's
        // distance from AgOpenGPS's pivot (positive ahead, negative behind).
        public double DiggingWidthM { get; set; } = 3.6576;           // 4 rows at 36 in
        public double AheadOfPivotM { get; set; } = 0;

        // Latest values (read by the collector and the UI)
        public double CurrentLbPerSec { get; private set; }           // mass flow over the scale, lightly smoothed
        public double BeltFtPerMin { get; private set; }              // from differenced pulses
        public bool IsFlowing { get; private set; }

        // Packet-level decision for MASS accounting. IsFlowing above is deliberately
        // smoothed for the display and tail-empty logic; using that EMA to decide
        // whether real pounds count drops the first crop at startup and can count
        // a few empty-belt increments after flow stops.
        public double LastPacketLbPerSec { get; private set; }
        public bool CountCurrentDelta { get; private set; }

        public double InstantYield { get; private set; }              // lb/ac
        public double SmoothedYield { get; private set; }             // exponentially smoothed, display only
        public double InstantWorkRate { get; private set; }           // lb/hr
        public double SmoothedWorkRate { get; private set; }
        public bool HasReading { get; private set; }

        private const double M2_PER_ACRE = 4046.856;

        // Packet-to-packet state
        private bool _seeded;
        private uint _prevLbX10, _prevPulses;
        private DateTime _prevUtc;

        // Packets arrive at 5 Hz. 0.3 gives a time constant of about three packets,
        // enough to take the quantisation out of a tenth-of-a-pound counter without
        // hiding a real change in flow.
        private const double RateAlpha = 0.3;

        // A counter that appears to go backwards by more than this has not wrapped,
        // it has been reset — the module rebooted or its counters were cleared.
        private const uint ResetThreshold = 0x80000000u;

        /// <summary>
        /// Called for every conveyor packet. Returns the pounds delivered since the
        /// previous packet, which the collector credits to the job and the load. Zero
        /// on the first packet and after a counter reset.
        ///
        /// Never reseeded on a link dropout: the module kept counting while the packets
        /// were lost, so the first packet after the gap carries every pound in between.
        /// </summary>
        public double PushConveyorReading(uint cumPoundsX10, uint cumPulses, DateTime utc)
        {
            HasReading = true;

            if (!_seeded)
            {
                Reseed(cumPoundsX10, cumPulses, utc);
                return 0;
            }

            uint dLbX10  = unchecked(cumPoundsX10 - _prevLbX10);
            uint dPulses = unchecked(cumPulses - _prevPulses);
            double dt    = (utc - _prevUtc).TotalSeconds;

            if (dLbX10 >= ResetThreshold || dPulses >= ResetThreshold)
            {
                Props.WriteActivityLog("Conveyor counters reset by module");
                Reseed(cumPoundsX10, cumPulses, utc);
                return 0;
            }

            _prevLbX10  = cumPoundsX10;
            _prevPulses = cumPulses;
            _prevUtc    = utc;

            double dLb = dLbX10 / 10.0;

            // Two packets inside 10 ms is a transport hiccup, not a meaningful
            // rate sample. The pounds are still real; use the previous smoothed
            // flow state for this rare packet rather than manufacturing a huge
            // rate from a tiny dt.
            if (dt < 0.01)
            {
                CountCurrentDelta = dLb > 0 && IsFlowing;
                return dLb;
            }

            double rate = dLb / dt;
            double belt = dPulses * InchesPerPulse / 12.0 / dt * 60.0;

            LastPacketLbPerSec = rate;
            CountCurrentDelta  = dLb > 0 && rate > FlowThresholdLbPerSec;

            CurrentLbPerSec = CurrentLbPerSec * (1 - RateAlpha) + rate * RateAlpha;
            BeltFtPerMin    = BeltFtPerMin    * (1 - RateAlpha) + belt * RateAlpha;
            IsFlowing       = CurrentLbPerSec > FlowThresholdLbPerSec;

            return dLb;
        }

        private void Reseed(uint cumPoundsX10, uint cumPulses, DateTime utc)
        {
            _seeded     = true;
            _prevLbX10  = cumPoundsX10;
            _prevPulses = cumPulses;
            _prevUtc    = utc;
            CurrentLbPerSec    = 0;
            BeltFtPerMin       = 0;
            LastPacketLbPerSec = 0;
            CountCurrentDelta  = false;
            IsFlowing          = false;
        }

        /// <summary>
        /// Pairs the current flow with a buffered position point.
        /// speedKmh is the ground speed recorded at that position.
        /// Returns the yield value for that position, lb/ac.
        /// </summary>
        public double Calculate(double speedKmh)
        {
            return Calculate(speedKmh, DiggingWidthM);
        }

        /// <summary>
        /// As Calculate(speed), but divides by the width actually digging new crop
        /// rather than the full width.
        ///
        /// On a half-overlapped pass only half the digger meets standing crop, so the
        /// flow arriving is half — dividing that by the full width returns half the
        /// true yield and paints a cold streak on ground that yielded normally.
        /// Dividing by the width that did the digging returns the field's actual
        /// yield. Mass is unaffected either way: pounds come straight off the scale.
        /// </summary>
        public double Calculate(double speedKmh, double effectiveWidthM)
        {
            if (speedKmh < 0.5 || effectiveWidthM <= 0 || !IsFlowing)
            {
                InstantYield = 0;
                InstantWorkRate = 0;
                Smooth(0, 0);
                return 0;
            }

            // Area rate: m²/s
            double speedMs = speedKmh / 3.6;
            double areaRateM2s = speedMs * effectiveWidthM;

            // lb/s ÷ m²/s = lb/m², then to the acre
            double yieldLbPerM2 = CurrentLbPerSec / areaRateM2s;
            InstantYield = Math.Round(yieldLbPerM2 * M2_PER_ACRE, 1);

            // Throughput is independent of ground speed and consistent with the
            // accumulated total, which comes from the same counter.
            InstantWorkRate = Math.Round(CurrentLbPerSec * 3600.0, 0);

            Smooth(InstantYield, InstantWorkRate);

            return InstantYield;
        }

        // Display damping. Updating every sample makes the readout drift rather than
        // step. Display only: totals, the map and yield_data all record InstantYield.
        private const double SmoothAlpha = 0.1;
        private bool _smoothSeeded = false;
        // Full-precision EMA state — the exposed properties are rounded for display,
        // but the state must not be, or a small value latches instead of decaying.
        private double _emaYield = 0;
        private double _emaWork = 0;

        private void Smooth(double instantYield, double instantWork)
        {
            // Seed on the first sample after a reset. Starting from zero would make
            // the readout crawl up to the true value every time a job starts, which
            // looks like a fault.
            if (!_smoothSeeded)
            {
                _emaYield = instantYield;
                _emaWork = instantWork;
                _smoothSeeded = true;
            }
            else
            {
                _emaYield = _emaYield * (1 - SmoothAlpha) + instantYield * SmoothAlpha;
                _emaWork = _emaWork * (1 - SmoothAlpha) + instantWork * SmoothAlpha;
            }

            SmoothedYield = Math.Round(_emaYield, 1);
            SmoothedWorkRate = Math.Round(_emaWork, 0);
        }

        /// <summary>
        /// Calculate incremental acres from a distance travelled.
        /// distanceM: metres travelled since last call.
        /// </summary>
        public static double MetresToAcres(double distanceM, double widthM)
        {
            return (distanceM * widthM) / M2_PER_ACRE;
        }

        public void ResetSmoothing()
        {
            _emaYield = 0;
            _emaWork = 0;
            _smoothSeeded = false;
            SmoothedYield = 0;
            SmoothedWorkRate = 0;
        }
    }
}
