using System;
using System.Net;
using System.Net.Sockets;
using BeltFlo.Classes;
using BeltFlo.Forms;

namespace BeltFlo.Communication
{
    // BeltFlo UDP port assignments (different from RC to allow both apps to run simultaneously)
    //   AOG GPS:       recv 17777  send 15555   (same as RC — both apps share via SO_REUSEADDR)
    //   Module data:   recv 30300  send 30400   (BeltFlo only — YieldFlo keeps 30100/30200, so both apps and both modules can share one network)

    public class UDPComm
    {
        private readonly frmMain mf;
        private byte[] buffer = new byte[1024];
        private string cConnectionName;
        private IPAddress cNetworkEP;
        private int cReceivePort;
        private int cSendFromPort;
        private int cSendToPort;
        private HandleDataDelegateObj HandleDataDelegate = null;
        private Socket recvSocket;
        private volatile bool Running = false;
        private Socket sendSocket;
        private string cModuleIP;

        public UDPComm(frmMain CallingForm, int ReceivePort, int SendToPort, int SendFromPort,
                       string ConnectionName, string DestinationEndPoint = "")
        {
            mf = CallingForm;
            cReceivePort = ReceivePort;
            cSendToPort = SendToPort;
            cSendFromPort = SendFromPort;
            cConnectionName = ConnectionName;
            SetEP(DestinationEndPoint);
        }

        private delegate void HandleDataDelegateObj(int port, byte[] msg);

        public bool IsRunning { get { return Running; } }

        public string NetworkEP
        {
            get { return cNetworkEP?.ToString() ?? ""; }
            set
            {
                if (IPAddress.TryParse(value, out _))
                {
                    string[] parts = value.Split('.');
                    cNetworkEP = IPAddress.Parse(parts[0] + "." + parts[1] + "." + parts[2] + ".255");
                }
            }
        }

        public void Send(byte[] byteData)
        {
            if (!Running || sendSocket == null || byteData == null || byteData.Length == 0) return;
            try
            {
                IPEndPoint endPt = new IPEndPoint(cNetworkEP, cSendToPort);
                sendSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                    endPt, new AsyncCallback(HandleSend), null);
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("UDPComm/Send " + ex.Message);
            }
        }

        /// <summary>
        /// Send to an explicit host/port instead of this channel's configured
        /// broadcast target. BeltFlo uses this for its live-yield bridge so the
        /// same packet can be offered to classic AgOpenGPS (15555) and
        /// AgOpenWeb (9999) without changing the GPS receive channel.
        /// </summary>
        public void SendTo(byte[] byteData, IPAddress address, int port)
        {
            if (!Running || sendSocket == null || byteData == null || byteData.Length == 0
                || address == null || port <= 0) return;
            try
            {
                sendSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                    new IPEndPoint(address, port), new AsyncCallback(HandleSend), null);
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("UDPComm/SendTo " + ex.Message);
            }
        }

        /// <summary>
        /// Sends straight to the module the last packet came from, so it arrives even
        /// where broadcasts are filtered. Broadcasts only until a module has been heard.
        /// </summary>
        public void SendToModule(byte[] byteData)
        {
            if (!Running || sendSocket == null || byteData == null || byteData.Length == 0) return;
            try
            {
                IPAddress dest = IPAddress.TryParse(cModuleIP ?? "", out IPAddress ip) ? ip : cNetworkEP;
                sendSocket.BeginSendTo(byteData, 0, byteData.Length, SocketFlags.None,
                    new IPEndPoint(dest, cSendToPort), new AsyncCallback(HandleSend), null);
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("UDPComm/SendToModule " + ex.Message);
            }
        }

        public void Start()
        {
            try
            {
                HandleDataDelegate = HandleData;

                recvSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                recvSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                recvSocket.Bind(new IPEndPoint(IPAddress.Any, cReceivePort));

                sendSocket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);
                sendSocket.SetSocketOption(SocketOptionLevel.Socket, SocketOptionName.ReuseAddress, true);
                // Windows refuses a send to a broadcast address without this.
                sendSocket.EnableBroadcast = true;
                sendSocket.Bind(new IPEndPoint(IPAddress.Any, cSendFromPort));

                EndPoint client = new IPEndPoint(IPAddress.Any, 0);
                recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None,
                    ref client, new AsyncCallback(Receive), recvSocket);

                Running = true;
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("UDPComm/Start " + ex.Message);
            }
        }

        public void Stop()
        {
            if (!Running) return;
            Running = false;
            try { recvSocket?.Close(); } catch { }
            try { sendSocket?.Close(); } catch { }
            recvSocket = null;
            sendSocket = null;
        }

        // ── Packet dispatch ───────────────────────────────────────────────────
        private void HandleData(int port, byte[] data)
        {
            try
            {
                if (data.Length < 2 || Core.IsShuttingDown) return;

                int pgn = data[1] << 8 | data[0];

                switch (pgn)
                {
                    // ── AOG GPS packets (PGN 33152 = 0x8180) ─────────────────
                    case 33152:
                        if (data.Length > 4)
                        {
                            switch (data[3])
                            {
                                case 100:   // Corrected position (lat/lon doubles; speed not present)
                                case 208:   // Dual GPS / TwoL (lat/lon/speed/elevation doubles)
                                    Core.GPS.ParseByteData(data, data[3]);
                                    Core.RaiseGpsUpdated();

                                    // Feed GPS update to data collector
                                    Core.Collector?.OnGpsUpdate();
                                    break;

                                case 229:   // Section control — bytes 5-12 are 64 section bits
                                    if (data.Length >= 13)
                                    {
                                        bool any = false;
                                        for (int i = 5; i <= 12; i++)
                                            if (data[i] != 0) { any = true; break; }
                                        Core.GPS.SectionsActive = any;
                                    }
                                    break;

                                case 254:   // AutoSteer data — carries speed when not using TwoL
                                    // Bytes 5-6: speed * 10 (uint16, km/h)
                                    if (data.Length >= 7)
                                        Core.GPS.ParseSpeedPgn254(data);
                                    break;
                            }
                        }
                        break;

                    // ── BeltFlo conveyor packet (PGN 40010, 5 Hz) ────────────
                    case 40010:
                        ParseConveyorPacket(data);
                        break;
                }
            }
            catch (Exception ex)
            {
                Props.WriteErrorLog("UDPComm/HandleData " + ex.Message);
            }
        }

        private void ParseConveyorPacket(byte[] data)
        {
            // Module → PC conveyor packet (19 bytes). A new PGN rather than a new
            // layout under 40001, so a grain module on the same network can never be
            // read as a scale.
            // [0-1]   PGN 40010 little-endian (0x4A 0x9C)
            // [2]     flags  bit0=ScaleOK, bit1=BeltRunning, bit2=Tared,
            //                bit3=ReceivingFromPC (settings within 4 s), bit4=Overload
            // [3-6]   cum_pounds_x10  uint32 LE  delivered weight, tenths of a pound; wraps
            // [7-10]  cum_pulses      uint32 LE  belt pulses; wraps
            // [11-12] scale_lb_x10    int16 LE   live weigh-section load after zero, tenths
            // [13-16] scale_raw       int32 LE   raw converter counts
            // [17]    reserved
            // [18]    CRC8 — over everything before it
            if (data.Length < 19) return;
            if (!Core.Tls.GoodCRC(data)) return;

            byte flags      = data[2];
            uint cumLbX10   = BitConverter.ToUInt32(data, 3);
            uint cumPulses  = BitConverter.ToUInt32(data, 7);
            short scaleX10  = BitConverter.ToInt16(data, 11);
            int scaleRaw    = BitConverter.ToInt32(data, 13);

            // Status first, so the diagnostic row the counters write sees this
            // packet's flags rather than the previous one's.
            Core.ApplyConveyorStatus(flags, scaleX10 / 10.0, scaleRaw);
            Core.ApplyConveyorCounters(cumLbX10, cumPulses);
        }

        // ── Socket callbacks ──────────────────────────────────────────────────
        private void HandleSend(IAsyncResult asyncResult)
        {
            try { sendSocket?.EndSend(asyncResult); }
            catch (Exception ex) { Props.WriteErrorLog("UDPComm/HandleSend " + ex.Message); }
        }

        public string ModuleIP
        {
            get
            {
                if (Core.ModuleConnected)
                {
                    return cModuleIP;
                }
                else
                {
                    return "";
                }
            }
        }

        private void Receive(IAsyncResult asyncResult)
        {
            if (!Running) return;
            try
            {
                EndPoint epSender = new IPEndPoint(IPAddress.Any, 0);
                int msgLen = recvSocket.EndReceiveFrom(asyncResult, ref epSender);
                byte[] localMsg = null;
                int port = 0;
                if (msgLen > 0)
                {
                    localMsg = new byte[msgLen];
                    Array.Copy(buffer, localMsg, msgLen);
                    port = ((IPEndPoint)epSender).Port;
                    cModuleIP = ((IPEndPoint)epSender).Address.ToString();
                }

                // Re-arm listener
                try
                {
                    if (Running && recvSocket != null)
                    {
                        EndPoint nextSender = new IPEndPoint(IPAddress.Any, 0);
                        recvSocket.BeginReceiveFrom(buffer, 0, buffer.Length, SocketFlags.None,
                            ref nextSender, new AsyncCallback(Receive), recvSocket);
                    }
                }
                catch (ObjectDisposedException) { }

                // Marshal to UI thread
                if (Running && !Core.IsShuttingDown && msgLen > 0 && HandleDataDelegate != null
                    && mf != null && mf.IsHandleCreated && !mf.IsDisposed)
                {
                    try { mf.BeginInvoke(HandleDataDelegate, new object[] { port, localMsg }); }
                    catch (InvalidOperationException) { }
                }
            }
            catch (ObjectDisposedException) { }
            catch (Exception ex) { Props.WriteErrorLog("UDPComm/Receive " + ex.Message); }
        }

        private void SetEP(string dest)
        {
            if (IPAddress.TryParse(dest, out _))
                NetworkEP = dest;
            else
                cNetworkEP = IPAddress.Broadcast;  // 255.255.255.255 fallback
        }
    }
}
