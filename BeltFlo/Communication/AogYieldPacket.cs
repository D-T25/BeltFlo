using System;
using BeltFlo.Database;

namespace BeltFlo.Communication
{
    /// <summary>
    /// Live BeltFlo -> AgOpenGPS yield packet.
    ///
    /// This deliberately uses AgOpenGPS's normal 0x80/0x81 loopback framing so a
    /// custom AOG build can receive it on the existing 127.0.0.1:15555 socket.
    /// Message id 0xC7 is currently unused by upstream AOG's loopback receiver.
    ///
    /// Layout (little-endian payload):
    ///   0   0x80
    ///   1   0x81
    ///   2   0x7F
    ///   3   0xC7  BeltFlo live yield
    ///   4   42    payload bytes
    ///   5   version = 2
    ///   6   flags: bit0 valid, bit1 pass break, bit2 pass start
    ///   7..14   latitude double
    ///   15..22  longitude double
    ///   23..26  yield lb/ac float
    ///   27..30  digging width metres float
    ///   31..34  heading degrees float
    ///   35..38  UTC unix seconds uint32
    ///   39..42  low color-scale yield lb/ac float
    ///   43..46  high color-scale yield lb/ac float
    ///   47      AOG checksum (sum bytes 2..46)
    ///
    /// BeltFlo sends positions that have ALREADY been corrected for the configured
    /// digger-to-scale processing delay. AOG must therefore draw the supplied
    /// position as-is and must not apply another flow lag.
    /// </summary>
    public static class AogYieldPacket
    {
        public const byte MessageId = 0xC7;
        public const byte Version = 2;
        public const int PayloadLength = 42;
        public const int PacketLength = 48;

        public const byte FlagValid = 1 << 0;
        public const byte FlagPassBreak = 1 << 1;
        public const byte FlagPassStart = 1 << 2;

        public static byte[] Build(
            YieldDataPoint point,
            double diggingWidthM,
            double colorScaleMinLbAc,
            double colorScaleMaxLbAc,
            bool passStart,
            bool passBreak)
        {
            if (point == null) throw new ArgumentNullException(nameof(point));

            var data = new byte[PacketLength];
            data[0] = 0x80;
            data[1] = 0x81;
            data[2] = 0x7F;
            data[3] = MessageId;
            data[4] = PayloadLength;
            data[5] = Version;

            byte flags = 0;
            if (!passBreak && point.YieldRate > 0) flags |= FlagValid;
            if (passBreak) flags |= FlagPassBreak;
            if (passStart) flags |= FlagPassStart;
            data[6] = flags;

            Copy(BitConverter.GetBytes(point.Latitude), data, 7);
            Copy(BitConverter.GetBytes(point.Longitude), data, 15);
            Copy(BitConverter.GetBytes((float)Math.Max(0, point.YieldRate)), data, 23);
            Copy(BitConverter.GetBytes((float)Math.Max(0, diggingWidthM)), data, 27);
            Copy(BitConverter.GetBytes((float)point.Heading), data, 31);

            DateTime utc = point.Timestamp.Kind == DateTimeKind.Utc
                ? point.Timestamp
                : point.Timestamp.ToUniversalTime();
            long unix = (long)(utc - new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc)).TotalSeconds;
            uint unix32 = unix <= 0 ? 0u : unix >= uint.MaxValue ? uint.MaxValue : (uint)unix;
            Copy(BitConverter.GetBytes(unix32), data, 35);

            double low = Math.Max(0, colorScaleMinLbAc);
            double high = colorScaleMaxLbAc > low ? colorScaleMaxLbAc : low + 1;
            Copy(BitConverter.GetBytes((float)low), data, 39);
            Copy(BitConverter.GetBytes((float)high), data, 43);

            byte checksum = 0;
            for (int i = 2; i < PacketLength - 1; i++)
                unchecked { checksum += data[i]; }
            data[PacketLength - 1] = checksum;

            return data;
        }

        public static bool HasGoodChecksum(byte[] data)
        {
            if (data == null || data.Length != PacketLength) return false;
            if (data[0] != 0x80 || data[1] != 0x81 || data[3] != MessageId || data[4] != PayloadLength)
                return false;

            byte checksum = 0;
            for (int i = 2; i < data.Length - 1; i++)
                unchecked { checksum += data[i]; }
            return checksum == data[data.Length - 1];
        }

        private static void Copy(byte[] source, byte[] destination, int offset)
        {
            Buffer.BlockCopy(source, 0, destination, offset, source.Length);
        }
    }
}
