using System;
using System.Reflection;
using BeltFlo.Classes;
using BeltFlo.Communication;
using BeltFlo.Database;

namespace BeltFlo.LogicTests
{
    internal static class Program
    {
        private static int _passed;
        private static int _failed;

        private static void Main()
        {
            Run("CRC16 CCITT-FALSE known vector", TestCrc16KnownVector);
            Run("Uncalibrated profile sends zero span", TestUncalibratedBlock);
            Run("Calibrated settings block encodes exact fields", TestCalibratedBlock);
            Run("UDP PGN 40011 packet CRCs and layout", TestUdpPacket);
            Run("CAN settings frames reconstruct same block", TestCanFrames);
            Run("Conveyor calibration validity", TestCalibrationValidity);
            Run("Scale-data safety gate rejects bad module states", TestScaleDataUsableGate);
            Run("Counter differencing gives pounds, flow and belt speed", TestCounterDifferencing);
            Run("Flow threshold rejects tiny empty-belt increments", TestFlowThreshold);
            Run("Counter wrap preserves delivered mass", TestCounterWrap);
            Run("Yield formula matches lb/ac geometry", TestYieldFormula);
            Run("Metres-to-acres conversion", TestMetresToAcres);

            Console.WriteLine();
            Console.WriteLine($"BeltFlo logic tests: {_passed} passed, {_failed} failed.");
            Environment.ExitCode = _failed == 0 ? 0 : 1;
        }

        private static void Run(string name, Action test)
        {
            try
            {
                test();
                _passed++;
                Console.WriteLine("[PASS] " + name);
            }
            catch (Exception ex)
            {
                _failed++;
                Console.WriteLine("[FAIL] " + name);
                Console.WriteLine("       " + ex.Message);
            }
        }

        private static ConveyorConfig CalibratedConfig()
        {
            return new ConveyorConfig
            {
                ZeroCounts = 123456,
                SpanLbPerCount = 0.0005,
                ZeroSetAt = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc),
                SectionLenIn = 42.5,
                InchesPerPulse = 2.125,
                BeltStopTimeoutS = 2.7
            };
        }

        private static void TestCrc16KnownVector()
        {
            byte[] data = System.Text.Encoding.ASCII.GetBytes("123456789");
            Equal((ushort)0x29B1, ModuleSettings.Crc16(data), "CRC16");
        }

        private static void TestUncalibratedBlock()
        {
            var cfg = CalibratedConfig();
            cfg.ZeroSetAt = null; // span value exists, but zero was never established

            byte[] block = ModuleSettings.Block(cfg);
            Equal(13, block.Length, "settings block length");
            Nearly(0.0f, BitConverter.ToSingle(block, 4), 1e-12, "uncalibrated span on wire");
        }

        private static void TestCalibratedBlock()
        {
            var cfg = CalibratedConfig();
            byte[] b = ModuleSettings.Block(cfg);

            Equal(123456, BitConverter.ToInt32(b, 0), "zero counts");
            Nearly(0.0005f, BitConverter.ToSingle(b, 4), 1e-9, "span");
            Equal((ushort)425, BitConverter.ToUInt16(b, 8), "section length x10");
            Equal((ushort)2125, BitConverter.ToUInt16(b, 10), "inches/pulse x1000");
            Equal((byte)27, b[12], "belt stop x10");
        }

        private static void TestUdpPacket()
        {
            var cfg = CalibratedConfig();
            byte[] block = ModuleSettings.Block(cfg);
            byte[] p = ModuleSettings.UdpPacket(cfg);

            Equal(18, p.Length, "UDP packet length");
            Equal((byte)0x4B, p[0], "PGN low byte");
            Equal((byte)0x9C, p[1], "PGN high byte");

            for (int i = 0; i < block.Length; i++)
                Equal(block[i], p[i + 2], "settings block byte " + i);

            Equal(ModuleSettings.Crc16(block), BitConverter.ToUInt16(p, 15), "CRC16 field");

            byte sum = 0;
            for (int i = 0; i < 17; i++) unchecked { sum += p[i]; }
            Equal(sum, p[17], "CRC8 byte sum");
        }

        private static void TestCanFrames()
        {
            var cfg = CalibratedConfig();
            byte[] block = ModuleSettings.Block(cfg);
            var frames = ModuleSettings.CanFrames(cfg);

            Equal(8, frames.frameA.Length, "CAN A length");
            Equal(8, frames.frameB.Length, "CAN B length");

            byte[] rebuilt = new byte[13];
            Array.Copy(frames.frameA, 0, rebuilt, 0, 8);
            Array.Copy(frames.frameB, 0, rebuilt, 8, 5);

            for (int i = 0; i < block.Length; i++)
                Equal(block[i], rebuilt[i], "CAN reconstructed block byte " + i);

            Equal(ModuleSettings.Crc16(block), BitConverter.ToUInt16(frames.frameB, 5), "CAN CRC16");
            Equal((byte)0, frames.frameB[7], "CAN reserved byte");
        }

        private static void TestCalibrationValidity()
        {
            var cfg = new ConveyorConfig
            {
                ZeroCounts = 0,
                SpanLbPerCount = 0,
                ZeroSetAt = null
            };
            False(cfg.IsCalibrated, "new profile should be uncalibrated");

            cfg.ZeroSetAt = DateTime.UtcNow;
            False(cfg.IsCalibrated, "zero alone should not be calibrated");

            cfg.SpanLbPerCount = 0.0005;
            True(cfg.IsCalibrated, "zero + nonzero span should be calibrated");

            // Raw zero counts are allowed to be exactly zero.
            cfg.ZeroCounts = 0;
            True(cfg.IsCalibrated, "raw zero count value must not invalidate calibration");
        }


        private static void TestScaleDataUsableGate()
        {
            var activeField = typeof(Core).GetField("_activeConveyor",
                BindingFlags.NonPublic | BindingFlags.Static);
            if (activeField == null) throw new Exception("Could not access Core._activeConveyor for test");

            var calibrated = CalibratedConfig();
            activeField.SetValue(null, calibrated);

            Core.ModuleConnected = true;
            Core.LastReceivingFromPc = true;
            Core.LastScaleOk = true;
            Core.LastOverload = false;
            Core.LastTared = true;

            True(Core.ScaleDataUsable, "healthy calibrated two-way link should be usable");

            Core.LastReceivingFromPc = false;
            False(Core.ScaleDataUsable, "lost PC settings heartbeat must block data");
            Core.LastReceivingFromPc = true;

            Core.LastOverload = true;
            False(Core.ScaleDataUsable, "overload must block data");
            Core.LastOverload = false;

            Core.LastScaleOk = false;
            False(Core.ScaleDataUsable, "scale hardware fault must block data");
            Core.LastScaleOk = true;

            Core.LastTared = false;
            False(Core.ScaleDataUsable, "module calibration/tare flag must block data");
            Core.LastTared = true;

            var uncalibrated = CalibratedConfig();
            uncalibrated.SpanLbPerCount = 0;
            activeField.SetValue(null, uncalibrated);
            False(Core.ScaleDataUsable, "uncalibrated active profile must block data");

            Core.ModuleConnected = false;
            Core.LastReceivingFromPc = false;
            Core.LastScaleOk = true;
            Core.LastOverload = false;
            Core.LastTared = false;
            activeField.SetValue(null, null);
        }

        private static void TestCounterDifferencing()
        {
            var y = new clsYieldCalculator { InchesPerPulse = 1.0, FlowThresholdLbPerSec = 0.05 };
            DateTime t = new DateTime(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

            Nearly(0, y.PushConveyorReading(1000, 100, t), 1e-12, "seed packet pounds");
            double lb = y.PushConveyorReading(1020, 112, t.AddSeconds(0.2));

            Nearly(2.0, lb, 1e-12, "delivered pounds");
            Nearly(10.0, y.LastPacketLbPerSec, 1e-9, "packet flow");
            Nearly(300.0 * 0.3, y.BeltFtPerMin, 1e-9, "smoothed belt ft/min");
            True(y.CountCurrentDelta, "real crop delta should count");
        }

        private static void TestFlowThreshold()
        {
            var y = new clsYieldCalculator { InchesPerPulse = 1.0, FlowThresholdLbPerSec = 0.05 };
            DateTime t = DateTime.UtcNow;

            y.PushConveyorReading(0, 0, t);
            double lb = y.PushConveyorReading(1, 1, t.AddSeconds(5));

            Nearly(0.1, lb, 1e-12, "small delta pounds still reported");
            Nearly(0.02, y.LastPacketLbPerSec, 1e-12, "small delta rate");
            False(y.CountCurrentDelta, "below-threshold noise should not count");
        }

        private static void TestCounterWrap()
        {
            var y = new clsYieldCalculator { InchesPerPulse = 1.0 };
            DateTime t = DateTime.UtcNow;

            y.PushConveyorReading(uint.MaxValue - 4, uint.MaxValue - 9, t);
            double lb = y.PushConveyorReading(3, 5, t.AddSeconds(0.2));

            Nearly(0.8, lb, 1e-12, "pounds across uint32 wrap");
            // Pulse delta: from MaxValue-9 to 5 = 15 counts.
            // 15 in / .2 s = 6.25 ft/s = 375 ft/min; EMA alpha is .3.
            Nearly(375.0 * 0.3, y.BeltFtPerMin, 1e-9, "belt speed across uint32 wrap");
        }

        private static void TestYieldFormula()
        {
            var y = new clsYieldCalculator
            {
                InchesPerPulse = 1.0,
                FlowThresholdLbPerSec = 0.05
            };
            DateTime t = DateTime.UtcNow;
            y.PushConveyorReading(0, 0, t);
            y.PushConveyorReading(20, 12, t.AddSeconds(0.2)); // 2 lb in .2 s => 10 lb/s; EMA => 3 lb/s

            const double speedKmh = 5.0;
            const double widthM = 3.6576;
            double expected = Math.Round((y.CurrentLbPerSec / ((speedKmh / 3.6) * widthM)) * 4046.856, 1);
            Nearly(expected, y.Calculate(speedKmh, widthM), 0.05, "instant yield lb/ac");
        }

        private static void TestMetresToAcres()
        {
            Nearly(1.0, clsYieldCalculator.MetresToAcres(4046.856, 1.0), 1e-12, "one acre");
        }

        private static void True(bool value, string name)
        {
            if (!value) throw new Exception(name + ": expected true");
        }

        private static void False(bool value, string name)
        {
            if (value) throw new Exception(name + ": expected false");
        }

        private static void Equal<T>(T expected, T actual, string name)
        {
            if (!Equals(expected, actual))
                throw new Exception($"{name}: expected {expected}, got {actual}");
        }

        private static void Nearly(double expected, double actual, double tolerance, string name)
        {
            if (Math.Abs(expected - actual) > tolerance)
                throw new Exception($"{name}: expected {expected}, got {actual} (tol {tolerance})");
        }
    }
}
