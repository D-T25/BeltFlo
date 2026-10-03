using AgOpenGPS.Core.Models;
using OpenTK.Graphics.OpenGL;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace AgOpenGPS
{
    public partial class FormGPS
    {
        private const byte BeltFloYieldProtocolV1 = 1;
        private const byte BeltFloYieldProtocolV2 = 2;
        private const byte BeltFloYieldFlagValid = 1 << 0;
        private const byte BeltFloYieldFlagBreak = 1 << 1;
        private const byte BeltFloYieldFlagStart = 1 << 2;
        private const double BeltFloMaxBridgeMeters = 12.0;
        private const uint BeltFloMaxGapSeconds = 4;
        private const double BeltFloDefaultYieldMinLbAc = 20000.0;
        private const double BeltFloDefaultYieldMaxLbAc = 80000.0;

        private sealed class BeltFloYieldSegment
        {
            public GeoCoord LeftA;
            public GeoCoord RightA;
            public GeoCoord LeftB;
            public GeoCoord RightB;
            public double YieldLbAc;
        }

        private struct BeltFloYieldSample
        {
            public uint UnixSeconds;
            public double Latitude;
            public double Longitude;
            public double YieldLbAc;
            public double WidthM;
            public double HeadingDeg;
            public byte Flags;
            public GeoCoord Coord;
        }

        private readonly List<BeltFloYieldSegment> beltFloYieldSegments =
            new List<BeltFloYieldSegment>(32768);

        private BeltFloYieldSample beltFloPreviousSample;
        private bool beltFloHasPreviousSample;
        private string beltFloLoadedField = null;
        private double beltFloYieldMinLbAc = BeltFloDefaultYieldMinLbAc;
        private double beltFloYieldMaxLbAc = BeltFloDefaultYieldMaxLbAc;

        private void ReceiveBeltFloYield(byte[] data)
        {
            if (data == null || data.Length < 40 || data[3] != 0xC7) return;

            bool isV1 = data.Length == 40 && data[4] == 34 && data[5] == BeltFloYieldProtocolV1;
            bool isV2 = data.Length == 48 && data[4] == 42 && data[5] == BeltFloYieldProtocolV2;
            if (!isV1 && !isV2) return;
            if (!isJobStarted || string.IsNullOrWhiteSpace(currentFieldDirectory)) return;

            EnsureBeltFloYieldField();

            if (isV2)
            {
                double low = BitConverter.ToSingle(data, 39);
                double high = BitConverter.ToSingle(data, 43);
                if (ValidYieldRange(low, high))
                {
                    beltFloYieldMinLbAc = low;
                    beltFloYieldMaxLbAc = high;
                }
            }

            var sample = new BeltFloYieldSample
            {
                Flags = data[6],
                Latitude = BitConverter.ToDouble(data, 7),
                Longitude = BitConverter.ToDouble(data, 15),
                YieldLbAc = BitConverter.ToSingle(data, 23),
                WidthM = BitConverter.ToSingle(data, 27),
                HeadingDeg = BitConverter.ToSingle(data, 31),
                UnixSeconds = BitConverter.ToUInt32(data, 35)
            };

            if (!ValidLatLon(sample.Latitude, sample.Longitude)) return;
            if (sample.WidthM <= 0 || sample.WidthM > 100) return;

            sample.Coord = AppModel.LocalPlane.ConvertWgs84ToGeoCoord(
                new Wgs84(sample.Latitude, sample.Longitude));

            AcceptBeltFloYieldSample(sample);
            AppendBeltFloYieldSample(sample);
        }

        private void EnsureBeltFloYieldField()
        {
            string field = isJobStarted ? currentFieldDirectory : null;
            if (string.Equals(field, beltFloLoadedField, StringComparison.Ordinal)) return;

            beltFloLoadedField = field;
            beltFloYieldSegments.Clear();
            beltFloHasPreviousSample = false;
            beltFloYieldMinLbAc = BeltFloDefaultYieldMinLbAc;
            beltFloYieldMaxLbAc = BeltFloDefaultYieldMaxLbAc;

            if (string.IsNullOrWhiteSpace(field)) return;

            string path = BeltFloYieldFilePath();
            if (!File.Exists(path)) return;

            try
            {
                foreach (string line in File.ReadLines(path))
                {
                    if (string.IsNullOrWhiteSpace(line) || line.StartsWith("#")) continue;

                    string[] p = line.Split(',');
                    if (p.Length < 7) continue;

                    uint unix;
                    double lat, lon, yield, width, heading;
                    byte flags;
                    if (!uint.TryParse(p[0], NumberStyles.Integer, CultureInfo.InvariantCulture, out unix)
                        || !double.TryParse(p[1], NumberStyles.Float, CultureInfo.InvariantCulture, out lat)
                        || !double.TryParse(p[2], NumberStyles.Float, CultureInfo.InvariantCulture, out lon)
                        || !double.TryParse(p[3], NumberStyles.Float, CultureInfo.InvariantCulture, out yield)
                        || !double.TryParse(p[4], NumberStyles.Float, CultureInfo.InvariantCulture, out width)
                        || !double.TryParse(p[5], NumberStyles.Float, CultureInfo.InvariantCulture, out heading)
                        || !byte.TryParse(p[6], NumberStyles.Integer, CultureInfo.InvariantCulture, out flags))
                        continue;

                    if (!ValidLatLon(lat, lon) || width <= 0 || width > 100) continue;

                    if (p.Length >= 9
                        && double.TryParse(p[7], NumberStyles.Float, CultureInfo.InvariantCulture, out double scaleLow)
                        && double.TryParse(p[8], NumberStyles.Float, CultureInfo.InvariantCulture, out double scaleHigh)
                        && ValidYieldRange(scaleLow, scaleHigh))
                    {
                        beltFloYieldMinLbAc = scaleLow;
                        beltFloYieldMaxLbAc = scaleHigh;
                    }

                    var sample = new BeltFloYieldSample
                    {
                        UnixSeconds = unix,
                        Latitude = lat,
                        Longitude = lon,
                        YieldLbAc = yield,
                        WidthM = width,
                        HeadingDeg = heading,
                        Flags = flags,
                        Coord = AppModel.LocalPlane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon))
                    };
                    AcceptBeltFloYieldSample(sample);
                }
            }
            catch (Exception ex)
            {
                AgLibrary.Logging.Log.EventWriter("BeltFlo yield load failed: " + ex.Message);
                beltFloYieldSegments.Clear();
                beltFloHasPreviousSample = false;
            }
        }

        private void AcceptBeltFloYieldSample(BeltFloYieldSample sample)
        {
            bool passStart = (sample.Flags & BeltFloYieldFlagStart) != 0;
            bool passBreak = (sample.Flags & BeltFloYieldFlagBreak) != 0;
            bool valid = (sample.Flags & BeltFloYieldFlagValid) != 0;

            if (passStart)
                beltFloHasPreviousSample = false;

            if (passBreak || !valid || sample.YieldLbAc <= 0)
            {
                beltFloHasPreviousSample = false;
                return;
            }

            if (beltFloHasPreviousSample)
            {
                double dE = sample.Coord.Easting - beltFloPreviousSample.Coord.Easting;
                double dN = sample.Coord.Northing - beltFloPreviousSample.Coord.Northing;
                double distance = Math.Sqrt(dE * dE + dN * dN);

                uint dt = sample.UnixSeconds >= beltFloPreviousSample.UnixSeconds
                    ? sample.UnixSeconds - beltFloPreviousSample.UnixSeconds
                    : uint.MaxValue;

                if (distance >= 0.05 && distance <= BeltFloMaxBridgeMeters && dt <= BeltFloMaxGapSeconds)
                {
                    double halfWidth = Math.Max(0.05, sample.WidthM * 0.5);
                    double pE = -dN / distance * halfWidth;
                    double pN = dE / distance * halfWidth;

                    beltFloYieldSegments.Add(new BeltFloYieldSegment
                    {
                        LeftA = new GeoCoord(
                            beltFloPreviousSample.Coord.Northing + pN,
                            beltFloPreviousSample.Coord.Easting + pE),
                        RightA = new GeoCoord(
                            beltFloPreviousSample.Coord.Northing - pN,
                            beltFloPreviousSample.Coord.Easting - pE),
                        LeftB = new GeoCoord(
                            sample.Coord.Northing + pN,
                            sample.Coord.Easting + pE),
                        RightB = new GeoCoord(
                            sample.Coord.Northing - pN,
                            sample.Coord.Easting - pE),
                        YieldLbAc = (beltFloPreviousSample.YieldLbAc + sample.YieldLbAc) * 0.5
                    });
                }
            }

            beltFloPreviousSample = sample;
            beltFloHasPreviousSample = true;
        }

        private void DrawBeltFloYield()
        {
            EnsureBeltFloYieldField();
            if (beltFloYieldSegments.Count == 0) return;

            bool cullWasEnabled = GL.IsEnabled(EnableCap.CullFace);
            if (cullWasEnabled) GL.Disable(EnableCap.CullFace);

            GL.Enable(EnableCap.Blend);

            foreach (BeltFloYieldSegment s in beltFloYieldSegments)
            {
                SetBeltFloYieldColor(s.YieldLbAc);
                GL.Begin(PrimitiveType.TriangleStrip);
                GL.Vertex3(s.LeftA.Easting, s.LeftA.Northing, 0.025);
                GL.Vertex3(s.RightA.Easting, s.RightA.Northing, 0.025);
                GL.Vertex3(s.LeftB.Easting, s.LeftB.Northing, 0.025);
                GL.Vertex3(s.RightB.Easting, s.RightB.Northing, 0.025);
                GL.End();
            }

            if (cullWasEnabled) GL.Enable(EnableCap.CullFace);
        }

        private void SetBeltFloYieldColor(double yieldLbAc)
        {
            double t = (yieldLbAc - beltFloYieldMinLbAc)
                     / (beltFloYieldMaxLbAc - beltFloYieldMinLbAc);
            if (t < 0) t = 0;
            if (t > 1) t = 1;

            int band = Math.Min(7, (int)(t * 8.0));
            switch (band)
            {
                case 0: GL.Color4(0.70f, 0.05f, 0.05f, 0.88f); break;
                case 1: GL.Color4(0.95f, 0.15f, 0.05f, 0.88f); break;
                case 2: GL.Color4(1.00f, 0.45f, 0.00f, 0.88f); break;
                case 3: GL.Color4(1.00f, 0.85f, 0.00f, 0.88f); break;
                case 4: GL.Color4(0.65f, 0.90f, 0.05f, 0.88f); break;
                case 5: GL.Color4(0.10f, 0.72f, 0.12f, 0.88f); break;
                case 6: GL.Color4(0.00f, 0.65f, 0.78f, 0.88f); break;
                default: GL.Color4(0.05f, 0.25f, 0.95f, 0.88f); break;
            }
        }

        private void AppendBeltFloYieldSample(BeltFloYieldSample sample)
        {
            string path = BeltFloYieldFilePath();
            if (string.IsNullOrEmpty(path)) return;

            try
            {
                string dir = Path.GetDirectoryName(path);
                if (!Directory.Exists(dir)) Directory.CreateDirectory(dir);

                if (!File.Exists(path))
                    File.WriteAllText(path,
                        "# BeltFlo live yield: unix,lat,lon,yield_lb_ac,width_m,heading_deg,flags,scale_low_lb_ac,scale_high_lb_ac"
                        + Environment.NewLine);

                string line = string.Format(CultureInfo.InvariantCulture,
                    "{0},{1:F8},{2:F8},{3:F1},{4:F3},{5:F1},{6},{7:F1},{8:F1}",
                    sample.UnixSeconds,
                    sample.Latitude,
                    sample.Longitude,
                    sample.YieldLbAc,
                    sample.WidthM,
                    sample.HeadingDeg,
                    sample.Flags,
                    beltFloYieldMinLbAc,
                    beltFloYieldMaxLbAc);

                File.AppendAllText(path, line + Environment.NewLine);
            }
            catch (Exception ex)
            {
                AgLibrary.Logging.Log.EventWriter("BeltFlo yield save failed: " + ex.Message);
            }
        }

        private string BeltFloYieldFilePath()
        {
            if (!isJobStarted || string.IsNullOrWhiteSpace(currentFieldDirectory)) return null;
            return Path.Combine(
                RegistrySettings.fieldsDirectory,
                currentFieldDirectory,
                "BeltFloYield.txt");
        }

        private static bool ValidYieldRange(double low, double high)
        {
            return !double.IsNaN(low) && !double.IsInfinity(low)
                && !double.IsNaN(high) && !double.IsInfinity(high)
                && low >= 0 && high > low;
        }

        private static bool ValidLatLon(double lat, double lon)
        {
            return !double.IsNaN(lat) && !double.IsInfinity(lat)
                && !double.IsNaN(lon) && !double.IsInfinity(lon)
                && lat >= -90 && lat <= 90
                && lon >= -180 && lon <= 180;
        }
    }
}
