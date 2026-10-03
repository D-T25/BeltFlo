using System;
using System.Net;
using System.Net.Sockets;
using AgOpenWeb.Models.State;

namespace AgOpenWeb.ViewModels;

/// <summary>
/// Sends the minimum classic-AOG GPS/section stream BeltFlo already knows how
/// to consume. This lets BeltFlo run unchanged whether the guidance host is
/// classic AgOpenGPS or AgOpenWeb.
/// </summary>
public partial class MainViewModel
{
    private static readonly IPEndPoint BeltFloEndpoint =
        new(IPAddress.Loopback, 17777);
    private static readonly Socket BeltFloBridgeSocket =
        new(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

    private static void SendBeltFloGuidanceState(GpsCycleResult result)
    {
        if (!result.GpsValid) return;

        try
        {
            // Corrected-position packet understood by BeltFlo clsGPS:
            // lon double, lat double, heading double.
            var pos = new byte[29];
            pos[0] = 0x80; pos[1] = 0x81; pos[2] = 0x7F;
            pos[3] = 100; pos[4] = 24;
            Buffer.BlockCopy(BitConverter.GetBytes(result.Longitude), 0, pos, 5, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(result.Latitude), 0, pos, 13, 8);
            Buffer.BlockCopy(BitConverter.GetBytes(result.Heading), 0, pos, 21, 8);
            BeltFloBridgeSocket.SendTo(pos, BeltFloEndpoint);

            // Speed packet: uint16 km/h x10.
            var speed = new byte[7];
            speed[0] = 0x80; speed[1] = 0x81; speed[2] = 0x7F;
            speed[3] = 254; speed[4] = 2;
            double kmh = Math.Max(0, result.Speed);
            ushort speedX10 = (ushort)Math.Min(ushort.MaxValue, Math.Round(kmh * 10.0));
            Buffer.BlockCopy(BitConverter.GetBytes(speedX10), 0, speed, 5, 2);
            BeltFloBridgeSocket.SendTo(speed, BeltFloEndpoint);

            // 64-section state packet. BeltFlo only needs "any section on".
            var sections = new byte[13];
            sections[0] = 0x80; sections[1] = 0x81; sections[2] = 0x7F;
            sections[3] = 229; sections[4] = 8;
            if (result.SectionStates != null)
            {
                int count = Math.Min(64, result.SectionStates.Length);
                for (int i = 0; i < count; i++)
                    if (result.SectionStates[i])
                        sections[5 + (i >> 3)] |= (byte)(1 << (i & 7));
            }
            BeltFloBridgeSocket.SendTo(sections, BeltFloEndpoint);
        }
        catch (SocketException)
        {
            // BeltFlo is optional. Guidance must never depend on this bridge.
        }
        catch (ObjectDisposedException)
        {
        }
    }
}
