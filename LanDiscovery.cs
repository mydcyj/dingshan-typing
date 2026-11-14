using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Text;

namespace EnglishTypingPractice
{
    internal sealed class LanRoomInfo
    {
        public string Version { get; set; }
        public string AppVersion { get; set; }
        public string Name { get; set; }
        public int Players { get; set; }
        public bool Joinable { get; set; }
        public SnakeMode Mode { get; set; }
        public MatchPhase Phase { get; set; }
    }

    internal sealed class DiscoveredRoom
    {
        internal LanRoomInfo Info;
        internal IPEndPoint Endpoint;

        public override string ToString()
        {
            string status = Info.Phase == MatchPhase.Playing ? "对局中" : Info.Players == 4 ? "已满" :
                Info.Phase == MatchPhase.Finished ? "已结算" : "等待开局";
            return Info.Name + " · " + (Info.Version == LanWire.Version ? LanDiscovery.ModeName(Info.Mode) + " · " : "") +
                Info.Players + "/4 · " + LanWire.DisplayVersion(Info.AppVersion, Info.Version) + " · " + status;
        }
    }

    internal static class LanDiscovery
    {
        private static readonly byte[] Probe = Encoding.ASCII.GetBytes(LanWire.Version + " find");
        private static readonly byte[][] Probes = { Probe, Encoding.ASCII.GetBytes(LanWire.PreviousVersion + " find"),
            Encoding.ASCII.GetBytes("TypingSnake/8 find") };

        internal static bool IsProbe(byte[] data)
        {
            return data.Length == Probe.Length && data.SequenceEqual(Probe);
        }

        internal static string ModeName(SnakeMode mode)
        {
            switch (mode)
            {
                case SnakeMode.Obstacles: return "障碍赛";
                case SnakeMode.Territory: return "圈地战";
                case SnakeMode.Teams: return "团战";
                default: return "经典";
            }
        }

        internal static string FindOlderVersion(IPEndPoint endpoint)
        {
            try
            {
                return Scan(endpoint.Port, new[] { endpoint.Address }).Where(room => room.Info.Version != LanWire.Version)
                    .Select(room => LanWire.DisplayVersion(room.Info.AppVersion, room.Info.Version)).FirstOrDefault();
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error) && !(error is NetworkInformationException)) throw;
                return null;
            }
        }

        internal static DiscoveredRoom[] Scan(int port = LanWire.Port, IPAddress[] destinations = null)
        {
            using (var client = new UdpClient(AddressFamily.InterNetwork))
            {
                client.Client.Bind(new IPEndPoint(IPAddress.Any, 0));
                client.EnableBroadcast = true;
                var results = new Dictionary<IPEndPoint, DiscoveredRoom>();
                var addresses = destinations ?? ScanAddresses();
                var watch = Stopwatch.StartNew();
                while (watch.ElapsedMilliseconds < 1450)
                {
                    foreach (var address in addresses)
                    {
                        var endpoint = new IPEndPoint(address, port);
                        foreach (var probe in Probes)
                            try { client.Send(probe, probe.Length, endpoint); }
                            catch (SocketException) { }
                    }

                    long next = watch.ElapsedMilliseconds + 450;
                    while (watch.ElapsedMilliseconds < next)
                    {
                        int timeout = (int)(next - watch.ElapsedMilliseconds);
                        if (timeout <= 0 || !client.Client.Poll(timeout * 1000, SelectMode.SelectRead))
                            break;
                        try
                        {
                            IPEndPoint sender = null;
                            byte[] data = client.Receive(ref sender);
                            if (data.Length > 1024 || sender.Port != port)
                                continue;
                            var info = LanWire.Decode<LanRoomInfo>(data);
                            if (info != null && LanWire.IsName(info.Name, 24) && info.Players >= 1 && info.Players <= 4 &&
                                (info.Version == LanWire.Version && Enum.IsDefined(typeof(SnakeMode), info.Mode) &&
                                    Enum.IsDefined(typeof(MatchPhase), info.Phase) ||
                                    info.Version == LanWire.PreviousVersion || info.Version == "TypingSnake/8"))
                            {
                                if (info.Version != LanWire.Version && !info.Joinable && info.Players < 4)
                                    info.Phase = MatchPhase.Playing;
                                results[sender] = new DiscoveredRoom { Info = info, Endpoint = sender };
                            }
                        }
                        catch (Exception error)
                        {
                            if (!LanWire.IsConnectionError(error)) throw;
                        }
                    }
                }
                var local = new HashSet<IPAddress>(LocalAddresses());
                local.Add(IPAddress.Loopback);
                var rooms = results.Values.ToList();
                if (rooms.Any(room => IPAddress.IsLoopback(room.Endpoint.Address)))
                    rooms.RemoveAll(room => !IPAddress.IsLoopback(room.Endpoint.Address) && local.Contains(room.Endpoint.Address));
                return rooms.OrderBy(room => room.Info.Name, StringComparer.CurrentCultureIgnoreCase).ToArray();
            }
        }

        internal static IPAddress[] LocalAddresses()
        {
            return LocalInterfaces().Select(item => item.Address).Distinct().ToArray();
        }

        private static IPAddress[] ScanAddresses()
        {
            var addresses = new HashSet<IPAddress> { IPAddress.Loopback, IPAddress.Broadcast };
            foreach (var item in LocalInterfaces())
            {
                var address = item.Address.GetAddressBytes();
                var mask = item.IPv4Mask.GetAddressBytes();
                for (int i = 0; i < address.Length; i++)
                    address[i] = (byte)(address[i] | ~mask[i]);
                addresses.Add(new IPAddress(address));
            }
            return addresses.ToArray();
        }

        private static IEnumerable<UnicastIPAddressInformation> LocalInterfaces()
        {
            return NetworkInterface.GetAllNetworkInterfaces()
                .Where(network => network.OperationalStatus == OperationalStatus.Up &&
                    network.NetworkInterfaceType != NetworkInterfaceType.Loopback &&
                    network.NetworkInterfaceType != NetworkInterfaceType.Tunnel)
                .SelectMany(network => network.GetIPProperties().UnicastAddresses)
                .Where(item => item.Address.AddressFamily == AddressFamily.InterNetwork && item.IPv4Mask != null);
        }
    }
}
