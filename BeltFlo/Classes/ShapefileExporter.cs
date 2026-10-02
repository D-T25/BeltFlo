using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using BeltFlo.Database;

namespace BeltFlo.Classes
{
    /// <summary>
    /// Writes the BeltFlo yield map as a polygon ESRI Shapefile package.
    ///
    /// Each record is the same swath ribbon segment the in-app map draws between
    /// consecutive valid yield points. The package is WGS84 (EPSG:4326) and is
    /// zipped with SHP, SHX, DBF, PRJ and CPG files so it can be uploaded as one
    /// file to GIS/field-data systems such as FieldView's Imported Map workflow.
    ///
    /// This is a map-layer export, not a native combine/harvest-data format.
    /// </summary>
    public static class ShapefileExporter
    {
        private const double MaxBridgeMeters = 5.0;
        private const double MaxBridgeSeconds = 3.0;
        private const double KgHaPerLbAc = 0.453592 / 0.404686;

        private sealed class Swath
        {
            public double[] X;
            public double[] Y;
            public YieldDataPoint Data;
            public double WidthM;

            public double XMin => X.Min();
            public double XMax => X.Max();
            public double YMin => Y.Min();
            public double YMax => Y.Max();
        }

        private sealed class DbfField
        {
            public string Name;
            public char Type;
            public byte Length;
            public byte Decimals;
            public Func<Swath, string> Value;
        }

        /// <summary>
        /// Export a stored job to a zipped polygon shapefile.
        /// Returns the ZIP path, or null when there is no exportable data or an error occurs.
        /// </summary>
        public static string ExportJob(int jobId, string jobName, string zipPath)
        {
            try
            {
                if (Core.Database == null) return null;

                var points = Core.Database.YieldData.GetByJob(jobId);
                if (points == null || points.Count < 2) return null;

                double widthM = 3.6576;
                string fieldName = "";
                string cropName = "";

                foreach (var j in Core.Database.Jobs.GetAll())
                {
                    if (j.id != jobId) continue;
                    widthM = Core.JobWidthM(j.profileId, j.rowsHarvested);

                    if (j.fieldId > 0)
                    {
                        var field = Core.Database.Fields.GetAll().Find(f => f.id == j.fieldId);
                        fieldName = field.id > 0 ? field.name : "";
                    }

                    if (j.cropId > 0)
                    {
                        var crop = Core.Database.Crops.GetAll().Find(x => x.id == j.cropId);
                        cropName = crop.id > 0 ? crop.name : "";
                    }
                    break;
                }

                return ExportPoints(zipPath, jobName, points, widthM, fieldName, cropName);
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("ShapefileExporter/ExportJob: " + ex.Message);
                return null;
            }
        }

        /// <summary>
        /// Pure-data entry point used by ExportJob and the automated tests.
        /// </summary>
        public static string ExportPoints(
            string zipPath,
            string jobName,
            List<YieldDataPoint> sourcePoints,
            double widthM,
            string fieldName = "",
            string cropName = "")
        {
            if (string.IsNullOrWhiteSpace(zipPath) || sourcePoints == null || sourcePoints.Count < 2 || widthM <= 0)
                return null;

            string tempDir = null;
            try
            {
                // PassTransients is display/export treatment and must not mutate the
                // caller's objects. Work on clones just as the CSV/map display does.
                var points = sourcePoints.Select(ClonePoint).ToList();
                PassTransients.Apply(points);

                var swaths = BuildSwaths(points, widthM);
                if (swaths.Count == 0) return null;

                string baseName = SafeBaseName(jobName);
                tempDir = Path.Combine(Path.GetTempPath(), "BeltFloShape_" + Guid.NewGuid().ToString("N"));
                Directory.CreateDirectory(tempDir);

                string shp = Path.Combine(tempDir, baseName + ".shp");
                string shx = Path.Combine(tempDir, baseName + ".shx");
                string dbf = Path.Combine(tempDir, baseName + ".dbf");
                string prj = Path.Combine(tempDir, baseName + ".prj");
                string cpg = Path.Combine(tempDir, baseName + ".cpg");
                string txt = Path.Combine(tempDir, baseName + "_README.txt");

                WriteShapeAndIndex(shp, shx, swaths);
                WriteDbf(dbf, swaths, jobName ?? "", fieldName ?? "", cropName ?? "");
                File.WriteAllText(prj,
                    "GEOGCS[\"WGS 84\",DATUM[\"WGS_1984\",SPHEROID[\"WGS 84\",6378137,298.257223563]]," +
                    "PRIMEM[\"Greenwich\",0],UNIT[\"degree\",0.0174532925199433],AUTHORITY[\"EPSG\",\"4326\"]]",
                    Encoding.ASCII);
                File.WriteAllText(cpg, "1252", Encoding.ASCII);
                File.WriteAllText(txt,
                    "BeltFlo Yield Map Shapefile\r\n" +
                    "Geometry: polygon swath segments\r\n" +
                    "Projection: WGS 84 / EPSG:4326\r\n" +
                    "YLD_LBAC: yield in pounds per acre\r\n" +
                    "YLD_KGHA: yield in kilograms per hectare\r\n" +
                    "WIDTH_M: digging width in metres\r\n" +
                    "POUNDS: BeltFlo pounds increment stored with the segment endpoint\r\n" +
                    "LOAD_ID: direct-to-truck load id; -1 for job-level/pre-tank points\r\n" +
                    "CAL_REV: BeltFlo scale calibration revision\r\n" +
                    "\r\n" +
                    "For FieldView, upload this ZIP through Data > Upload & Import and use it as an Imported Map.\r\n" +
                    "This file is a spatial yield-map layer; it is not a native combine harvest-data file.\r\n",
                    Encoding.UTF8);

                string outDir = Path.GetDirectoryName(zipPath);
                if (!string.IsNullOrEmpty(outDir)) Directory.CreateDirectory(outDir);
                if (File.Exists(zipPath)) File.Delete(zipPath);

                using (var fs = new FileStream(zipPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                using (var zip = new ZipArchive(fs, ZipArchiveMode.Create))
                {
                    AddZipEntry(zip, shp);
                    AddZipEntry(zip, shx);
                    AddZipEntry(zip, dbf);
                    AddZipEntry(zip, prj);
                    AddZipEntry(zip, cpg);
                    AddZipEntry(zip, txt);
                }

                return zipPath;
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("ShapefileExporter/ExportPoints: " + ex.Message);
                return null;
            }
            finally
            {
                if (!string.IsNullOrEmpty(tempDir))
                {
                    try { if (Directory.Exists(tempDir)) Directory.Delete(tempDir, true); }
                    catch { }
                }
            }
        }

        private static List<Swath> BuildSwaths(List<YieldDataPoint> points, double widthM)
        {
            var result = new List<Swath>();
            for (int i = 1; i < points.Count; i++)
            {
                var a = points[i - 1];
                var b = points[i];

                if (a.YieldRate <= 0 || b.YieldRate <= 0 || b.Speed < 0.5) continue;
                double dt = (b.Timestamp - a.Timestamp).TotalSeconds;
                if (dt <= 0 || dt > MaxBridgeSeconds) continue;

                var swath = MakeSwath(a, b, widthM);
                if (swath != null) result.Add(swath);
            }
            return result;
        }

        // Same ribbon geometry used by frmYieldMap.ComputeSegmentCorners.
        private static Swath MakeSwath(YieldDataPoint a, YieldDataPoint b, double widthM)
        {
            if (!ValidCoordinate(a.Latitude, a.Longitude) || !ValidCoordinate(b.Latitude, b.Longitude))
                return null;

            double midLat = (a.Latitude + b.Latitude) / 2.0;
            double mPerLat = 111320.0;
            double mPerLon = Math.Max(1.0, 111320.0 * Math.Cos(midLat * Math.PI / 180.0));

            double dEast = (b.Longitude - a.Longitude) * mPerLon;
            double dNorth = (b.Latitude - a.Latitude) * mPerLat;
            double len = Math.Sqrt(dEast * dEast + dNorth * dNorth);
            if (len < 0.05 || len > MaxBridgeMeters) return null;

            double h1 = a.Heading * Math.PI / 180.0;
            double h2 = b.Heading * Math.PI / 180.0;
            double sinAvg = (Math.Sin(h1) + Math.Sin(h2)) / 2.0;
            double cosAvg = (Math.Cos(h1) + Math.Cos(h2)) / 2.0;
            double mag = Math.Sqrt(sinAvg * sinAvg + cosAvg * cosAvg);

            double uE, uN;
            if (mag > 1e-6)
            {
                uE = sinAvg / mag;
                uN = cosAvg / mag;
            }
            else
            {
                uE = dEast / len;
                uN = dNorth / len;
            }

            double halfWidth = widthM / 2.0;
            double pLat = uE * halfWidth / mPerLat;
            double pLon = -uN * halfWidth / mPerLon;

            // Exterior rings are clockwise as required by the shapefile convention.
            double[] x =
            {
                a.Longitude + pLon,
                b.Longitude + pLon,
                b.Longitude - pLon,
                a.Longitude - pLon,
                a.Longitude + pLon
            };
            double[] y =
            {
                a.Latitude + pLat,
                b.Latitude + pLat,
                b.Latitude - pLat,
                a.Latitude - pLat,
                a.Latitude + pLat
            };

            return new Swath { X = x, Y = y, Data = b, WidthM = widthM };
        }

        private static void WriteShapeAndIndex(string shpPath, string shxPath, List<Swath> swaths)
        {
            const int shapeType = 5;       // Polygon
            const int pointsPerRecord = 5; // four corners + repeated first point
            const int contentBytes = 4 + 32 + 4 + 4 + 4 + pointsPerRecord * 16;
            const int contentWords = contentBytes / 2;
            const int recordBytes = 8 + contentBytes;

            double xMin = swaths.Min(s => s.XMin);
            double yMin = swaths.Min(s => s.YMin);
            double xMax = swaths.Max(s => s.XMax);
            double yMax = swaths.Max(s => s.YMax);

            int shpLengthWords = (100 + swaths.Count * recordBytes) / 2;
            int shxLengthWords = (100 + swaths.Count * 8) / 2;

            using (var bw = new BinaryWriter(File.Create(shpPath)))
            {
                WriteMainHeader(bw, shpLengthWords, shapeType, xMin, yMin, xMax, yMax);

                for (int i = 0; i < swaths.Count; i++)
                {
                    var s = swaths[i];
                    WriteBigEndianInt32(bw, i + 1);
                    WriteBigEndianInt32(bw, contentWords);

                    bw.Write(shapeType);
                    bw.Write(s.XMin);
                    bw.Write(s.YMin);
                    bw.Write(s.XMax);
                    bw.Write(s.YMax);
                    bw.Write(1); // parts
                    bw.Write(pointsPerRecord);
                    bw.Write(0); // first part starts at point 0

                    for (int p = 0; p < pointsPerRecord; p++)
                    {
                        bw.Write(s.X[p]);
                        bw.Write(s.Y[p]);
                    }
                }
            }

            using (var bw = new BinaryWriter(File.Create(shxPath)))
            {
                WriteMainHeader(bw, shxLengthWords, shapeType, xMin, yMin, xMax, yMax);

                int offsetWords = 50; // 100-byte main header
                for (int i = 0; i < swaths.Count; i++)
                {
                    WriteBigEndianInt32(bw, offsetWords);
                    WriteBigEndianInt32(bw, contentWords);
                    offsetWords += 4 + contentWords; // 8-byte record header + content
                }
            }
        }

        private static void WriteMainHeader(
            BinaryWriter bw, int fileLengthWords, int shapeType,
            double xMin, double yMin, double xMax, double yMax)
        {
            WriteBigEndianInt32(bw, 9994);
            for (int i = 0; i < 5; i++) WriteBigEndianInt32(bw, 0);
            WriteBigEndianInt32(bw, fileLengthWords);

            bw.Write(1000);      // version, little-endian
            bw.Write(shapeType); // Polygon
            bw.Write(xMin);
            bw.Write(yMin);
            bw.Write(xMax);
            bw.Write(yMax);
            bw.Write(0.0); // Z min
            bw.Write(0.0); // Z max
            bw.Write(0.0); // M min
            bw.Write(0.0); // M max
        }

        private static void WriteDbf(
            string path, List<Swath> swaths, string jobName, string fieldName, string cropName)
        {
            var enc = Encoding.GetEncoding(1252);
            string FixedText(string value, int width) => FitText(value ?? "", width, enc);
            string Num(double value, int width, int decimals) =>
                FitNumber(value.ToString("F" + decimals, CultureInfo.InvariantCulture), width);

            var fields = new List<DbfField>
            {
                new DbfField { Name="TIME_UTC",  Type='C', Length=19, Decimals=0, Value=s => FixedText(s.Data.Timestamp.ToString("yyyy-MM-dd HH:mm:ss"), 19) },
                new DbfField { Name="YLD_LBAC",  Type='N', Length=12, Decimals=1, Value=s => Num(s.Data.YieldRate, 12, 1) },
                new DbfField { Name="YLD_KGHA",  Type='N', Length=12, Decimals=1, Value=s => Num(s.Data.YieldRate * KgHaPerLbAc, 12, 1) },
                new DbfField { Name="WIDTH_M",   Type='N', Length=9,  Decimals=3, Value=s => Num(s.WidthM, 9, 3) },
                new DbfField { Name="POUNDS",    Type='N', Length=12, Decimals=2, Value=s => Num(s.Data.PoundsInc, 12, 2) },
                new DbfField { Name="ACRES",     Type='N', Length=12, Decimals=4, Value=s => Num(s.Data.AcresAccumulated, 12, 4) },
                new DbfField { Name="SPEED_KMH", Type='N', Length=9,  Decimals=2, Value=s => Num(s.Data.Speed, 9, 2) },
                new DbfField { Name="HEADING",   Type='N', Length=7,  Decimals=1, Value=s => Num(s.Data.Heading, 7, 1) },
                new DbfField { Name="ELEV_M",    Type='N', Length=9,  Decimals=1, Value=s => Num(s.Data.Elevation, 9, 1) },
                new DbfField { Name="LOAD_ID",   Type='N', Length=10, Decimals=0, Value=s => FitNumber(s.Data.LoadId.ToString(CultureInfo.InvariantCulture), 10) },
                new DbfField { Name="CAL_REV",   Type='N', Length=10, Decimals=0, Value=s => FitNumber(s.Data.CalRev.ToString(CultureInfo.InvariantCulture), 10) },
                new DbfField { Name="ROWS",      Type='N', Length=4,  Decimals=0, Value=s => FitNumber(s.Data.RowsInUse.ToString(CultureInfo.InvariantCulture), 4) },
                new DbfField { Name="BELT_FPM",  Type='N', Length=9,  Decimals=1, Value=s => Num(s.Data.BeltFtMin, 9, 1) },
                new DbfField { Name="JOB",       Type='C', Length=40, Decimals=0, Value=s => FixedText(jobName, 40) },
                new DbfField { Name="FIELD",     Type='C', Length=40, Decimals=0, Value=s => FixedText(fieldName, 40) },
                new DbfField { Name="CROP",      Type='C', Length=30, Decimals=0, Value=s => FixedText(cropName, 30) }
            };

            int headerLength = 32 + fields.Count * 32 + 1;
            int recordLength = 1 + fields.Sum(f => (int)f.Length);
            DateTime now = DateTime.UtcNow;

            using (var bw = new BinaryWriter(File.Create(path)))
            {
                bw.Write((byte)0x03); // dBASE III
                bw.Write((byte)Math.Max(0, Math.Min(255, now.Year - 1900)));
                bw.Write((byte)now.Month);
                bw.Write((byte)now.Day);
                bw.Write(swaths.Count);
                bw.Write((ushort)headerLength);
                bw.Write((ushort)recordLength);
                bw.Write(new byte[20]);

                foreach (var f in fields)
                {
                    byte[] name = Encoding.ASCII.GetBytes(f.Name.Length > 10 ? f.Name.Substring(0, 10) : f.Name);
                    var desc = new byte[32];
                    Array.Copy(name, desc, Math.Min(name.Length, 10));
                    desc[11] = (byte)f.Type;
                    desc[16] = f.Length;
                    desc[17] = f.Decimals;
                    bw.Write(desc);
                }
                bw.Write((byte)0x0D);

                foreach (var s in swaths)
                {
                    bw.Write((byte)0x20); // not deleted
                    foreach (var f in fields)
                    {
                        string value = f.Value(s) ?? "";
                        byte[] bytes = enc.GetBytes(value);
                        if (bytes.Length > f.Length)
                            bw.Write(bytes, 0, f.Length);
                        else
                        {
                            bw.Write(bytes);
                            if (bytes.Length < f.Length)
                                bw.Write(Enumerable.Repeat((byte)0x20, f.Length - bytes.Length).ToArray());
                        }
                    }
                }
                bw.Write((byte)0x1A);
            }
        }

        private static string FitText(string value, int width, Encoding enc)
        {
            if (string.IsNullOrEmpty(value)) return new string(' ', width);

            // Character data is Windows-1252 for broad DBF compatibility. Replace
            // unsupported characters rather than producing a malformed fixed record.
            byte[] bytes = enc.GetBytes(value);
            if (bytes.Length > width)
                value = enc.GetString(bytes, 0, width);

            return value.PadRight(width);
        }

        private static string FitNumber(string value, int width)
        {
            if (string.IsNullOrEmpty(value)) return new string(' ', width);
            if (value.Length > width) return new string('*', width);
            return value.PadLeft(width);
        }

        private static void WriteBigEndianInt32(BinaryWriter bw, int value)
        {
            bw.Write(new[]
            {
                (byte)((value >> 24) & 0xFF),
                (byte)((value >> 16) & 0xFF),
                (byte)((value >> 8) & 0xFF),
                (byte)(value & 0xFF)
            });
        }

        private static void AddZipEntry(ZipArchive zip, string path)
        {
            var entry = zip.CreateEntry(Path.GetFileName(path), CompressionLevel.Optimal);
            using (var input = File.OpenRead(path))
            using (var output = entry.Open())
                input.CopyTo(output);
        }

        private static bool ValidCoordinate(double lat, double lon) =>
            !double.IsNaN(lat) && !double.IsInfinity(lat)
            && !double.IsNaN(lon) && !double.IsInfinity(lon)
            && lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;

        private static string SafeBaseName(string name)
        {
            string s = string.IsNullOrWhiteSpace(name) ? "BeltFlo_Yield" : name.Trim();
            foreach (char ch in Path.GetInvalidFileNameChars()) s = s.Replace(ch, '_');
            if (s.Length > 80) s = s.Substring(0, 80);
            return s.Length == 0 ? "BeltFlo_Yield" : s;
        }

        private static YieldDataPoint ClonePoint(YieldDataPoint p) => new YieldDataPoint
        {
            Id = p.Id,
            JobId = p.JobId,
            LoadId = p.LoadId,
            Timestamp = p.Timestamp,
            Latitude = p.Latitude,
            Longitude = p.Longitude,
            Elevation = p.Elevation,
            Speed = p.Speed,
            Heading = p.Heading,
            YieldRate = p.YieldRate,
            AcresAccumulated = p.AcresAccumulated,
            PoundsInc = p.PoundsInc,
            BeltPulses = p.BeltPulses,
            BeltFtMin = p.BeltFtMin,
            ScaleLb = p.ScaleLb,
            ScaleRaw = p.ScaleRaw,
            CalRev = p.CalRev,
            RowsInUse = p.RowsInUse
        };
    }
}
