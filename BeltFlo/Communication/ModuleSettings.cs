using System;
using BeltFlo.Classes;
using BeltFlo.Database;

namespace BeltFlo.Communication
{
    /// <summary>
    /// The weighing settings the module needs, sent PC → module every 2 s and at once
    /// when they change. The module works out pounds itself (weight on the section ×
    /// belt travel), so it has to hold the same zero, span and geometry as the app's
    /// active conveyor configuration.
    ///
    /// The repeat doubles as a heartbeat, as in RateController: the module sets flags
    /// bit 3 ("receiving from PC") in its conveyor packet while a valid settings
    /// message has arrived within the last 4 s. The app shows the Module label green
    /// only when packets arrive and that bit is set.
    ///
    /// Settings block, 13 bytes, little-endian:
    ///   [0-3]   zero_counts         int32    raw counts with the belt running empty
    ///   [4-7]   span_lb_per_count   float32  pounds per raw count above zero
    ///   [8-9]   section_len_in_x10  uint16   weighed section length, tenths of an inch
    ///   [10-11] in_per_pulse_x1000  uint16   belt travel per pulse, thousandths of an inch
    ///   [12]    belt_stop_s_x10     uint8    no pulses for this long = belt stopped, tenths of a second
    ///
    /// UDP, PGN 40011, 18 bytes, to module port 30400:
    ///   [0-1]   PGN 40011 LE (0x4B 0x9C)
    ///   [2-14]  settings block
    ///   [15-16] CRC-16 LE of the block — lets the module reject a damaged message
    ///   [17]    CRC8 — byte sum of everything before it, as the conveyor packet
    ///
    /// CAN, two extended frames, DLC 8, source address 0xF9 (the PC):
    ///   0x18FF04F9  [0-7] block bytes 0-7
    ///   0x18FF05F9  [0-4] block bytes 8-12, [5-6] CRC-16 LE, [7] 0
    ///   The module applies the pair only when the CRC-16 matches both halves.
    ///
    /// CRC-16/CCITT-FALSE: polynomial 0x1021, initial 0xFFFF, no reflection, no final XOR.
    /// </summary>
    public static class ModuleSettings
    {
        public const ushort Pgn         = 40011;
        public const uint   CanFrameAId = 0x18FF04F9u;
        public const uint   CanFrameBId = 0x18FF05F9u;

        public static byte[] Block(ConveyorConfig c)
        {
            var b = new byte[13];
            Array.Copy(BitConverter.GetBytes((int)Math.Round(c.ZeroCounts)), 0, b, 0, 4);
            // Keep the packet layout unchanged, but send an unusable span until
            // calibration is real. The module then reports zero/tare separately
            // while refusing to accumulate pounds from a placeholder calibration.
            float span = c.IsCalibrated ? (float)c.SpanLbPerCount : 0.0f;
            Array.Copy(BitConverter.GetBytes(span), 0, b, 4, 4);
            Array.Copy(BitConverter.GetBytes(ToUInt16(c.SectionLenIn * 10.0)), 0, b, 8, 2);
            Array.Copy(BitConverter.GetBytes(ToUInt16(c.InchesPerPulse * 1000.0)), 0, b, 10, 2);
            b[12] = (byte)Math.Max(0, Math.Min(255, Math.Round(c.BeltStopTimeoutS * 10.0)));
            return b;
        }

        public static byte[] UdpPacket(ConveyorConfig c)
        {
            byte[] block = Block(c);
            var p = new byte[18];
            p[0] = (byte)(Pgn & 0xFF);
            p[1] = (byte)(Pgn >> 8);
            Array.Copy(block, 0, p, 2, 13);
            Array.Copy(BitConverter.GetBytes(Crc16(block)), 0, p, 15, 2);
            p[17] = Core.Tls.CRC(p, 17);
            return p;
        }

        public static (byte[] frameA, byte[] frameB) CanFrames(ConveyorConfig c)
        {
            byte[] block = Block(c);
            var a = new byte[8];
            var b = new byte[8];
            Array.Copy(block, 0, a, 0, 8);
            Array.Copy(block, 8, b, 0, 5);
            Array.Copy(BitConverter.GetBytes(Crc16(block)), 0, b, 5, 2);
            return (a, b);
        }

        public static ushort Crc16(byte[] data)
        {
            ushort crc = 0xFFFF;
            foreach (byte x in data)
            {
                crc ^= (ushort)(x << 8);
                for (int i = 0; i < 8; i++)
                    crc = (crc & 0x8000) != 0 ? (ushort)((crc << 1) ^ 0x1021) : (ushort)(crc << 1);
            }
            return crc;
        }

        private static ushort ToUInt16(double v) => (ushort)Math.Max(0, Math.Min(65535, Math.Round(v)));
    }
}
