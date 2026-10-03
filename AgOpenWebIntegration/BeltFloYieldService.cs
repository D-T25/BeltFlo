using System;
using AgOpenWeb.Models;
using AgOpenWeb.Models.Base;
using AgOpenWeb.Models.Coverage;
using AgOpenWeb.Models.State;
using AgOpenWeb.Services.Interfaces;

namespace AgOpenWeb.Services.Coverage;

/// <summary>
/// Receives BeltFlo's delay-corrected live-yield packet (0xC7) through
/// AgOpenWeb's existing UDP service and recolors ONLY the coverage display
/// layer. Detection bits are never changed here, so yield color cannot affect
/// automatic section control.
/// </summary>
public sealed class BeltFloYieldService : IDisposable
{
    private const byte MessageId = 0xC7;
    private const byte ProtocolV1 = 1;
    private const byte ProtocolV2 = 2;
    private const byte FlagValid = 1 << 0;
    private const byte FlagBreak = 1 << 1;
    private const byte FlagStart = 1 << 2;
    private const double MaxBridgeMeters = 12.0;
    private const uint MaxGapSeconds = 4;

    private readonly IUdpCommunicationService _udp;
    private readonly ICoverageMapService _coverage;
    private readonly ApplicationState _state;

    private Sample _previous;
    private bool _hasPrevious;

    private struct Sample
    {
        public uint UnixSeconds;
        public double YieldLbAc;
        public double WidthM;
        public double LowLbAc;
        public double HighLbAc;
        public Vec2 Position;
        public byte Flags;
    }

    public BeltFloYieldService(
        IUdpCommunicationService udp,
        ICoverageMapService coverage,
        ApplicationState state)
    {
        _udp = udp;
        _coverage = coverage;
        _state = state;
        _udp.DataReceived += OnUdpDataReceived;
    }

    private void OnUdpDataReceived(object? sender, UdpDataReceivedEventArgs e)
    {
        if (e.PGN != MessageId) return;
        HandlePacket(e.Data);
    }

    internal void HandlePacket(byte[] data)
    {
        if (!TryParse(data, out Sample sample))
            return;

        if ((sample.Flags & FlagStart) != 0)
            _hasPrevious = false;

        bool valid = (sample.Flags & FlagValid) != 0;
        bool passBreak = (sample.Flags & FlagBreak) != 0;
        if (!valid || passBreak || sample.YieldLbAc <= 0 || sample.WidthM <= 0)
        {
            _hasPrevious = false;
            return;
        }

        var plane = _state.Field.LocalPlane;
        if (plane == null)
        {
            _hasPrevious = false;
            return;
        }

        // Position is carried as WGS84 in the packet. Convert with the exact
        // LocalPlane AgOpenWeb uses for its current field.
        double lat = BitConverter.ToDouble(data, 7);
        double lon = BitConverter.ToDouble(data, 15);
        if (!ValidLatLon(lat, lon))
        {
            _hasPrevious = false;
            return;
        }
        sample.Position = new Vec2(plane.ConvertWgs84ToGeoCoord(new Wgs84(lat, lon)));

        if (_hasPrevious)
        {
            double dE = sample.Position.Easting - _previous.Position.Easting;
            double dN = sample.Position.Northing - _previous.Position.Northing;
            double distance = Math.Sqrt(dE * dE + dN * dN);
            uint dt = sample.UnixSeconds >= _previous.UnixSeconds
                ? sample.UnixSeconds - _previous.UnixSeconds
                : uint.MaxValue;

            if (distance >= 0.05 && distance <= MaxBridgeMeters && dt <= MaxGapSeconds)
            {
                double halfWidth = Math.Max(0.05, sample.WidthM * 0.5);
                double pE = -dN / distance * halfWidth;
                double pN =  dE / distance * halfWidth;

                var leftA  = new Vec2(_previous.Position.Easting + pE, _previous.Position.Northing + pN);
                var rightA = new Vec2(_previous.Position.Easting - pE, _previous.Position.Northing - pN);
                var rightB = new Vec2(sample.Position.Easting - pE, sample.Position.Northing - pN);
                var leftB  = new Vec2(sample.Position.Easting + pE, sample.Position.Northing + pN);

                double yield = (_previous.YieldLbAc + sample.YieldLbAc) * 0.5;
                double low = sample.LowLbAc;
                double high = sample.HighLbAc;
                if (high <= low)
                {
                    low = 20000;
                    high = 80000;
                }

                _coverage.PaintDisplayOnlyQuad(
                    leftA, rightA, rightB, leftB,
                    YieldColor(yield, low, high));
                _coverage.FlushCoverageUpdate();
            }
        }

        _previous = sample;
        _hasPrevious = true;
    }

    private static bool TryParse(byte[] data, out Sample sample)
    {
        sample = default;
        if (data == null || data.Length < 40
            || data[0] != 0x80 || data[1] != 0x81
            || data[3] != MessageId)
            return false;

        bool v1 = data.Length == 40 && data[4] == 34 && data[5] == ProtocolV1;
        bool v2 = data.Length == 48 && data[4] == 42 && data[5] == ProtocolV2;
        if (!v1 && !v2) return false;

        byte checksum = 0;
        for (int i = 2; i < data.Length - 1; i++)
            unchecked { checksum += data[i]; }
        if (checksum != data[data.Length - 1]) return false;

        double lat = BitConverter.ToDouble(data, 7);
        double lon = BitConverter.ToDouble(data, 15);
        if (!ValidLatLon(lat, lon)) return false;

        sample.Flags = data[6];
        sample.YieldLbAc = BitConverter.ToSingle(data, 23);
        sample.WidthM = BitConverter.ToSingle(data, 27);
        sample.UnixSeconds = BitConverter.ToUInt32(data, 35);
        sample.LowLbAc = v2 ? BitConverter.ToSingle(data, 39) : 20000;
        sample.HighLbAc = v2 ? BitConverter.ToSingle(data, 43) : 80000;
        return sample.WidthM > 0 && sample.WidthM <= 100;
    }

    private static CoverageColor YieldColor(double yieldLbAc, double low, double high)
    {
        double t = (yieldLbAc - low) / (high - low);
        t = Math.Clamp(t, 0, 1);
        int band = Math.Min(7, (int)(t * 8.0));

        return band switch
        {
            0 => new CoverageColor(179, 13, 13),
            1 => new CoverageColor(242, 38, 13),
            2 => new CoverageColor(255, 115, 0),
            3 => new CoverageColor(255, 217, 0),
            4 => new CoverageColor(166, 230, 13),
            5 => new CoverageColor(26, 184, 31),
            6 => new CoverageColor(0, 166, 199),
            _ => new CoverageColor(13, 64, 242)
        };
    }

    private static bool ValidLatLon(double lat, double lon) =>
        !double.IsNaN(lat) && !double.IsInfinity(lat)
        && !double.IsNaN(lon) && !double.IsInfinity(lon)
        && lat >= -90 && lat <= 90 && lon >= -180 && lon <= 180;

    public void Dispose()
    {
        _udp.DataReceived -= OnUdpDataReceived;
    }
}
