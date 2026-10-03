using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
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
            Run("Scale arrangement separates truck weights from pre-tank yield", TestScaleArrangement);
            Run("Imperial display supports lb cwt and tons per acre", TestImperialYieldUnits);
            Run("Scale-data safety gate rejects bad module states", TestScaleDataUsableGate);
            Run("Counter differencing gives pounds, flow and belt speed", TestCounterDifferencing);
            Run("Flow threshold rejects tiny empty-belt increments", TestFlowThreshold);
            Run("Counter wrap preserves delivered mass", TestCounterWrap);
            Run("Yield formula matches lb/ac geometry", TestYieldFormula);
            Run("Metres-to-acres conversion", TestMetresToAcres);
            Run("FieldView shapefile export writes polygon package", TestShapefileExport);
            Run("AgOpenGPS live-yield packet layout and checksum", TestAogYieldPacket);

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


        private static void TestScaleArrangement()
        {
            var p = new HarvesterProfile { ScaleLocation = HarvesterProfile.DirectToTruck };
            Equal("Truck", HarvesterProfile.DirectToTruck, "direct value stays database-compatible");
            Equal("Tank", HarvesterProfile.BeforeHoldingTank, "before-tank value stays database-compatible");
            True(p.TracksTruckWeight, "direct-to-truck should track truck monitor pounds");
            False(p.IsBeforeHoldingTank, "direct-to-truck is not pre-tank");

            p.ScaleLocation = HarvesterProfile.BeforeHoldingTank;
            False(p.TracksTruckWeight, "pre-tank scale must not assign scale pounds to a truck");
            True(p.IsBeforeHoldingTank, "pre-tank arrangement should be recognized");
        }

        private static void TestImperialYieldUnits()
        {
            // Settings is an internal generated type in the application assembly.
            // Use reflection here so the test verifies the real Props path without
            // changing the production settings class just for the test project.
            Type settingsType = typeof(Props).Assembly.GetType("BeltFlo.Properties.Settings", true);
            object settings = settingsType.GetProperty("Default",
                BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static).GetValue(null);
            PropertyInfo unitsProp = settingsType.GetProperty("Units");
            PropertyInfo yieldProp = settingsType.GetProperty("YieldUnit");

            string oldUnits = (string)unitsProp.GetValue(settings);
            string oldYield = (string)yieldProp.GetValue(settings);
            try
            {
                unitsProp.SetValue(settings, "Imperial");

                yieldProp.SetValue(settings, "lb/ac");
                Equal("lb/ac", Props.RateUnit, "lb/ac rate label");
                Equal("lb", Props.MassUnit, "lb mass label");
                Nearly(20000.0, Props.DisplayRate(20000.0), 1e-9, "lb/ac conversion");

                yieldProp.SetValue(settings, "cwt/ac");
                Equal("cwt/ac", Props.RateUnit, "cwt/ac rate label");
                Equal("cwt", Props.MassUnit, "cwt mass label");
                Nearly(200.0, Props.DisplayRate(20000.0), 1e-9, "cwt/ac conversion");

                yieldProp.SetValue(settings, "tons/ac");
                Equal("tons/ac", Props.RateUnit, "tons/ac rate label");
                Equal("tons", Props.MassUnit, "tons mass label");
                Nearly(10.0, Props.DisplayRate(20000.0), 1e-9, "tons/ac conversion");
            }
            finally
            {
                unitsProp.SetValue(settings, oldUnits);
                yieldProp.SetValue(settings, oldYield);
            }
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

        private static void TestAogYieldPacket()
        {
            DateTime stamp = new DateTime(2026, 10, 2, 14, 30, 0, DateTimeKind.Utc);
            var point = new YieldDataPoint
            {
                Timestamp = stamp,
                Latitude = 48.1234567,
                Longitude = -97.7654321,
                Heading = 271.5f,
                YieldRate = 48250.0
            };

            byte[] p = AogYieldPacket.Build(point, 6.096, 18000, 82000, true, false);
            Equal(AogYieldPacket.PacketLength, p.Length, "AOG packet length");
            Equal(0x80, p[0], "AOG header 0");
            Equal(0x81, p[1], "AOG header 1");
            Equal(0x7F, p[2], "AOG source");
            Equal(AogYieldPacket.MessageId, p[3], "AOG message id");
            Equal(AogYieldPacket.PayloadLength, p[4], "AOG payload length");
            Equal(AogYieldPacket.Version, p[5], "AOG packet version");
            True((p[6] & AogYieldPacket.FlagValid) != 0, "AOG valid flag");
            True((p[6] & AogYieldPacket.FlagPassStart) != 0, "AOG pass-start flag");
            True((p[6] & AogYieldPacket.FlagPassBreak) == 0, "AOG no break flag");
            Nearly(point.Latitude, BitConverter.ToDouble(p, 7), 1e-9, "AOG latitude");
            Nearly(point.Longitude, BitConverter.ToDouble(p, 15), 1e-9, "AOG longitude");
            Nearly(point.YieldRate, BitConverter.ToSingle(p, 23), 0.01, "AOG yield");
            Nearly(6.096, BitConverter.ToSingle(p, 27), 1e-5, "AOG width");
            Nearly(point.Heading, BitConverter.ToSingle(p, 31), 1e-5, "AOG heading");
            Nearly(18000.0, BitConverter.ToSingle(p, 39), 0.01, "AOG color low");
            Nearly(82000.0, BitConverter.ToSingle(p, 43), 0.01, "AOG color high");
            True(AogYieldPacket.HasGoodChecksum(p), "AOG checksum");

            byte[] brk = AogYieldPacket.Build(point, 6.096, 18000, 82000, false, true);
            True((brk[6] & AogYieldPacket.FlagValid) == 0, "break not valid");
            True((brk[6] & AogYieldPacket.FlagPassBreak) != 0, "break flag set");
            True(AogYieldPacket.HasGoodChecksum(brk), "break checksum");
        }

        private static void TestShapefileExport()
        {
            string zipPath = Path.Combine(Path.GetTempPath(),
                "BeltFlo_shape_test_" + Guid.NewGuid().ToString("N") + ".zip");
            try
            {
                DateTime t = new DateTime(2026, 10, 2, 12, 0, 0, DateTimeKind.Utc);
                var points = new List<YieldDataPoint>
                {
                    new YieldDataPoint
                    {
                        Timestamp = t,
                        Latitude = 48.0000000,
                        Longitude = -97.0000000,
                        Speed = 5,
                        Heading = 0,
                        YieldRate = 22000,
                        AcresAccumulated = 1.00,
                        PoundsInc = 50,
                        LoadId = -1,
                        BeltFtMin = 80,
                        CalRev = 4,
                        RowsInUse = 12
                    },
                    new YieldDataPoint
                    {
                        Timestamp = t.AddSeconds(1),
                        Latitude = 48.0000100,
                        Longitude = -97.0000000,
                        Speed = 5,
                        Heading = 0,
                        YieldRate = 23000,
                        AcresAccumulated = 1.01,
                        PoundsInc = 52,
                        LoadId = -1,
                        BeltFtMin = 82,
                        CalRev = 4,
                        RowsInUse = 12
                    }
                };

                string result = ShapefileExporter.ExportPoints(
                    zipPath, "Shape Test", points, 6.0, "Test Field", "Sugar Beet");
                Equal(zipPath, result, "shape export return path");
                True(File.Exists(zipPath), "shape ZIP exists");

                using (var fs = File.OpenRead(zipPath))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Read))
                {
                    foreach (string ext in new[] { ".shp", ".shx", ".dbf", ".prj", ".cpg" })
                        True(zip.GetEntry("Shape Test" + ext) != null, "ZIP contains " + ext);

                    var shpEntry = zip.GetEntry("Shape Test.shp");
                    byte[] shp = ReadEntry(shpEntry);
                    True(shp.Length > 108, "SHP has header and record");
                    Equal(9994, ReadBigEndianInt32(shp, 0), "SHP file code");
                    Equal(1000, BitConverter.ToInt32(shp, 28), "SHP version");
                    Equal(5, BitConverter.ToInt32(shp, 32), "SHP polygon type");
                    Equal(5, BitConverter.ToInt32(shp, 108), "SHP first record polygon type");

                    var dbfEntry = zip.GetEntry("Shape Test.dbf");
                    byte[] dbf = ReadEntry(dbfEntry);
                    Equal(1, BitConverter.ToInt32(dbf, 4), "DBF record count");
                }
            }
            finally
            {
                try { if (File.Exists(zipPath)) File.Delete(zipPath); } catch { }
            }
        }

        private static byte[] ReadEntry(ZipArchiveEntry entry)
        {
            if (entry == null) return Array.Empty<byte>();
            using (var input = entry.Open())
            using (var ms = new MemoryStream())
            {
                input.CopyTo(ms);
                return ms.ToArray();
            }
        }

        private static int ReadBigEndianInt32(byte[] b, int offset) =>
            (b[offset] << 24) | (b[offset + 1] << 16) | (b[offset + 2] << 8) | b[offset + 3];

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
