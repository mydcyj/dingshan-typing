using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace EnglishTypingPractice
{
    internal sealed class LanRequest
    {
        public string Kind { get; set; }
        public string Text { get; set; }
        public int Group { get; set; }
        public int Round { get; set; }
        public SnakeRole Role { get; set; }
    }

    internal sealed class LanReply
    {
        public string Error { get; set; }
        public int Slot { get; set; }
        public SnakeMatchState State { get; set; }
    }

    internal static class LanWire
    {
        internal const int Port = 48631;
        internal const string Version = "TypingSnake/4";
        internal const int StateLimit = 65536;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);

        internal static byte[] Encode<T>(T value)
        {
            return Utf8.GetBytes(new JavaScriptSerializer().Serialize(value));
        }

        internal static T Decode<T>(byte[] data)
        {
            return new JavaScriptSerializer { RecursionLimit = 12 }.Deserialize<T>(Utf8.GetString(data));
        }

        internal static void Write<T>(NetworkStream stream, T value)
        {
            byte[] data = Encode(value);
            var frame = new byte[4 + data.Length];
            Buffer.BlockCopy(BitConverter.GetBytes(IPAddress.HostToNetworkOrder(data.Length)), 0, frame, 0, 4);
            Buffer.BlockCopy(data, 0, frame, 4, data.Length);
            stream.Write(frame, 0, frame.Length);
        }

        internal static T Read<T>(NetworkStream stream, int limit)
        {
            int size = IPAddress.NetworkToHostOrder(BitConverter.ToInt32(ReadBytes(stream, 4), 0));
            if (size <= 0 || size > limit)
                throw new InvalidDataException("Invalid LAN message size.");
            return Decode<T>(ReadBytes(stream, size));
        }

        private static byte[] ReadBytes(NetworkStream stream, int size)
        {
            var data = new byte[size];
            int offset = 0;
            while (offset < size)
            {
                int read = stream.Read(data, offset, size - offset);
                if (read == 0)
                    throw new EndOfStreamException();
                offset += read;
            }
            return data;
        }

        internal static bool IsConnectionError(Exception error)
        {
            return error is SocketException || error is IOException || error is InvalidDataException ||
                error is ArgumentException || error is InvalidOperationException ||
                error is FormatException || error is OverflowException;
        }

        internal static bool IsName(string value, int max)
        {
            if (string.IsNullOrWhiteSpace(value) || value.Length > max)
                return false;
            foreach (char character in value)
                if (char.IsControl(character))
                    return false;
            return true;
        }
    }

    internal sealed class LanHost : IDisposable
    {
        private readonly object sync = new object();
        private readonly HashSet<TcpClient> clients = new HashSet<TcpClient>();
        private readonly SnakeMatch match;
        private readonly string room;
        private readonly TcpListener listener;
        private readonly UdpClient discovery;
        private readonly Timer timer;
        private readonly Stopwatch clock = new Stopwatch();
        private bool closed;

        internal LanHost(string room, string name, int port = LanWire.Port, IPAddress address = null, SnakeRole role = SnakeRole.Standard)
        {
            this.room = room;
            match = new SnakeMatch(name, role);
            var binding = address ?? IPAddress.Any;
            listener = new TcpListener(binding, port);
            try
            {
                listener.Start(8);
                Port = ((IPEndPoint)listener.LocalEndpoint).Port;
                discovery = new UdpClient(new IPEndPoint(binding, Port));
            }
            catch
            {
                listener.Stop();
                throw;
            }
            timer = new Timer(Step, null, Timeout.Infinite, Timeout.Infinite);
            Task.Run((Action)AcceptClients);
            Task.Run((Action)ReplyToScans);
        }

        internal int Port { get; private set; }

        internal SnakeMatchState Snapshot()
        {
            lock (sync)
                return match.Snapshot(room, 0);
        }

        internal void Start()
        {
            lock (sync)
                if (!closed && match.Start())
                {
                    clock.Restart();
                    timer.Change(0, 15);
                }
        }

        internal void Turn(string word, int group, int round, bool charge = false)
        {
            lock (sync)
                if (!closed)
                    ApplyInput(0, new LanRequest { Kind = charge ? "charge" : "turn", Text = word, Group = group, Round = round });
        }

        internal void SetRole(SnakeRole role)
        {
            lock (sync)
                if (!closed)
                    match.SetRole(0, role);
        }

        private void Step(object state)
        {
            lock (sync)
            {
                if (closed)
                    return;
                match.Step(clock.ElapsedMilliseconds);
                if (match.Phase != MatchPhase.Playing)
                    timer.Change(Timeout.Infinite, Timeout.Infinite);
            }
        }

        private void AcceptClients()
        {
            try
            {
                while (true)
                {
                    var client = listener.AcceptTcpClient();
                    lock (sync)
                    {
                        if (closed || clients.Count >= 8)
                        {
                            client.Close();
                            continue;
                        }
                        clients.Add(client);
                    }
                    Task.Run(() => ServeClient(client));
                }
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error)) throw;
            }
        }

        private void ServeClient(TcpClient client)
        {
            int slot = -1;
            try
            {
                client.NoDelay = true;
                var stream = client.GetStream();
                stream.ReadTimeout = stream.WriteTimeout = 5000;
                var hello = LanWire.Read<LanRequest>(stream, 512);
                if (hello == null || hello.Kind != LanWire.Version || !LanWire.IsName(hello.Text, 20) ||
                    !Enum.IsDefined(typeof(SnakeRole), hello.Role))
                    return;

                LanReply reply;
                lock (sync)
                {
                    if (closed)
                        return;
                    slot = match.AddPlayer(hello.Text, hello.Role);
                    reply = slot < 0 ? new LanReply { Error = "房间已满或本局正在进行。" } : Reply(slot);
                }
                LanWire.Write(stream, reply);
                if (slot < 0)
                    return;

                while (true)
                {
                    var request = LanWire.Read<LanRequest>(stream, 512);
                    if (request == null || (request.Kind != "poll" && request.Kind != "turn" &&
                        request.Kind != "charge" && request.Kind != "role"))
                        return;
                    lock (sync)
                    {
                        if (closed)
                            return;
                        ApplyInput(slot, request);
                        reply = Reply(slot);
                    }
                    LanWire.Write(stream, reply);
                }
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error)) throw;
            }
            finally
            {
                lock (sync)
                {
                    if (slot >= 0)
                        match.RemovePlayer(slot);
                    clients.Remove(client);
                }
                client.Close();
            }
        }

        private void ApplyInput(int slot, LanRequest request)
        {
            if (request.Kind == "role" && Enum.IsDefined(typeof(SnakeRole), request.Role))
                match.SetRole(slot, request.Role);
            if (request.Text == null || request.Text.Length > 20)
                return;
            if (request.Kind == "turn")
                match.TryWord(slot, request.Text, request.Group, request.Round, clock.ElapsedMilliseconds);
            if (request.Kind == "charge")
                match.TryCharge(slot, request.Text, request.Group, request.Round, clock.ElapsedMilliseconds);
        }

        private LanReply Reply(int slot)
        {
            return new LanReply { Slot = slot, State = match.Snapshot(room, slot) };
        }

        private void ReplyToScans()
        {
            try
            {
                var sender = new IPEndPoint(IPAddress.Any, 0);
                while (true)
                {
                    var data = discovery.Receive(ref sender);
                    if (!LanDiscovery.IsProbe(data))
                        continue;
                    LanRoomInfo info;
                    lock (sync)
                    {
                        if (closed)
                            return;
                        info = new LanRoomInfo
                        {
                            Version = LanWire.Version, Name = room, Players = match.Count, Joinable = match.CanJoin
                        };
                    }
                    data = LanWire.Encode(info);
                    try { discovery.Send(data, data.Length, sender); }
                    catch (SocketException) { }
                }
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error)) throw;
            }
        }

        public void Dispose()
        {
            lock (sync)
            {
                if (closed)
                    return;
                closed = true;
                timer.Dispose();
                listener.Stop();
                discovery.Close();
                foreach (var client in clients)
                    client.Close();
            }
        }
    }

    internal sealed class LanClient : IDisposable
    {
        private readonly TcpClient client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
        private volatile SnakeMatchState state;
        private volatile bool closed;
        private LanRequest turn;

        internal int Slot { get; private set; }
        internal SnakeMatchState State { get { return state; } }
        internal bool Disconnected { get { return closed; } }

        internal async Task<string> Join(IPAddress address, string name, int port = LanWire.Port, SnakeRole role = SnakeRole.Standard)
        {
            try
            {
                var connect = client.ConnectAsync(address, port);
                if (await Task.WhenAny(connect, Task.Delay(3000)).ConfigureAwait(false) != connect)
                {
                    Dispose();
                    return "连接超时，请确认同学仍在房间里。";
                }
                await connect.ConfigureAwait(false);
                return await Task.Run(() => JoinRoom(name, role)).ConfigureAwait(false);
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error)) throw;
                Dispose();
                return "无法连接房间，请确认在同一局域网。";
            }
        }

        private string JoinRoom(string name, SnakeRole role)
        {
            var stream = client.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 5000;
            LanWire.Write(stream, new LanRequest { Kind = LanWire.Version, Text = name, Role = role });
            var reply = LanWire.Read<LanReply>(stream, LanWire.StateLimit);
            if (reply != null && !string.IsNullOrEmpty(reply.Error))
            {
                Dispose();
                return reply.Error;
            }
            if (reply == null || reply.Slot < 1 || reply.Slot > 3 || !ValidState(reply.State, reply.Slot))
                throw new InvalidDataException("Invalid LAN room reply.");
            Slot = reply.Slot;
            state = reply.State;
            Task.Run(() => Poll(stream));
            return null;
        }

        internal void Turn(string word, int group, int round, bool charge = false)
        {
            Interlocked.Exchange(ref turn, new LanRequest { Kind = charge ? "charge" : "turn", Text = word, Group = group, Round = round });
        }

        internal void SetRole(SnakeRole role)
        {
            Interlocked.Exchange(ref turn, new LanRequest { Kind = "role", Role = role });
        }

        private void Poll(NetworkStream stream)
        {
            try
            {
                var poll = new LanRequest { Kind = "poll" };
                while (!closed)
                {
                    LanWire.Write(stream, Interlocked.Exchange(ref turn, null) ?? poll);
                    var reply = LanWire.Read<LanReply>(stream, LanWire.StateLimit);
                    if (reply == null || !ValidState(reply.State, Slot))
                        break;
                    state = reply.State;
                    Thread.Sleep(75);
                }
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error)) throw;
            }
            finally
            {
                Dispose();
            }
        }

        private static bool ValidState(SnakeMatchState state, int slot)
        {
            if (state == null || !LanWire.IsName(state.Room, 24) || state.Size < 32 || state.Size > 80 ||
                (state.Size - 32) % 16 != 0 || !Enum.IsDefined(typeof(MatchPhase), state.Phase) ||
                state.Players == null || state.Players.Length == 0 || state.Players.Length > 4 ||
                state.Rankings == null || state.Rankings.Length > 4 ||
                state.Words == null || (state.Words.Length != 0 && state.Words.Length != 4 && state.Words.Length != 12))
                return false;
            foreach (string word in state.Words)
                if (!SnakeWords.Contains(word))
                    return false;
            if (state.ChargeWord != null && (!SnakeWords.Contains(state.ChargeWord) || state.ChargeWord.Length < 11))
                return false;
            foreach (var ranked in state.Rankings)
                if (ranked == null || ranked.Slot < 0 || ranked.Slot > 3 || !LanWire.IsName(ranked.Name, 20))
                    return false;
            int cells = state.Size * state.Size;
            if (state.SmallFruits == null || state.SmallFruits.Length > 4 ||
                state.LargeFruit < -1 || state.LargeFruit >= cells)
                return false;
            var fruitCells = new HashSet<int>();
            if (state.LargeFruit >= 0)
            {
                if (state.LargeFruit % state.Size == state.Size - 1 || state.LargeFruit / state.Size == state.Size - 1)
                    return false;
                fruitCells.UnionWith(SnakeGame.LargeFruitCells(state.LargeFruit, state.Size));
            }
            foreach (int cell in state.SmallFruits)
                if (cell < 0 || cell >= cells || !fruitCells.Add(cell))
                    return false;
            var slots = new HashSet<int>();
            foreach (var player in state.Players)
            {
                if (player == null || player.Slot < 0 || player.Slot > 3 || !slots.Add(player.Slot) ||
                    !LanWire.IsName(player.Name, 20) || player.Body == null || player.Body.Length > cells ||
                    !Enum.IsDefined(typeof(SnakeResult), player.Result) ||
                    !Enum.IsDefined(typeof(SnakeDirection), player.Direction) ||
                    !Enum.IsDefined(typeof(SnakeRole), player.Role))
                    return false;
                foreach (int cell in player.Body)
                    if (cell < 0 || cell >= cells)
                        return false;
                if (player.Slot == slot && player.Result == SnakeResult.Running &&
                    state.Words.Length != 4 * SnakeWords.Variants(player.Role))
                    return false;
            }
            return slots.Contains(slot);
        }

        public void Dispose()
        {
            closed = true;
            client.Close();
        }
    }
}
