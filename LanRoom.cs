using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
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
        public SnakeTypingPreview Preview { get; set; }
        public int SprintRound { get; set; }
        public bool SprintHeld { get; set; }
        public bool Ready { get; set; }
        public string Session { get; set; }
        public string AppVersion { get; set; }
    }

    internal sealed class LanReply
    {
        public string Error { get; set; }
        public int Slot { get; set; }
        public SnakeMatchState State { get; set; }
        public string MusicKey { get; set; }
    }

    internal static class LanWire
    {
        internal const int Port = 48631;
        internal const string Version = "TypingSnake/10";
        internal const string PreviousVersion = "TypingSnake/9";
        internal const int ReconnectMilliseconds = 20000;
        internal const int StateLimit = 65536;
        internal const int MusicLimit = 64 * 1024 * 1024;
        private static readonly Encoding Utf8 = new UTF8Encoding(false, true);
        internal static readonly string AppVersion = "v" + typeof(LanWire).Assembly.GetName().Version.ToString(
            typeof(LanWire).Assembly.GetName().Version.Build == 0 ? 2 : 3);

        internal static string VersionMismatch(string hostVersion, string guestVersion)
        {
            return "版本不一致：房主 " + hostVersion + "，你 " + guestVersion + "。请使用相同版本。";
        }

        internal static string DisplayVersion(string appVersion, string protocol)
        {
            System.Version parsed;
            if (appVersion != null && appVersion.Length <= 18 && appVersion.StartsWith("v", StringComparison.Ordinal) &&
                System.Version.TryParse(appVersion.Substring(1), out parsed))
                return appVersion;
            if (protocol == PreviousVersion)
                return "v1.16.2";
            if (protocol == "TypingSnake/8")
                return "v1.16.0–v1.16.1";
            return "未知版本";
        }

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
        private sealed class Seat
        {
            internal string Key;
            internal TcpClient Connection;
            internal long ReconnectUntil;
            internal bool Ready;
        }

        private readonly object sync = new object();
        private readonly HashSet<TcpClient> clients = new HashSet<TcpClient>();
        private readonly SnakeMatch match;
        private readonly SnakeBotSession bots = new SnakeBotSession();
        private readonly string room;
        private readonly TcpListener listener;
        private readonly UdpClient discovery;
        private readonly Timer timer;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly Stopwatch roomClock = Stopwatch.StartNew();
        private readonly Stopwatch musicClock = new Stopwatch();
        private readonly Seat[] seats = new Seat[4];
        private FileInfo musicFile;
        private int musicTrack;
        private long musicLength;
        private int musicVolume = 65;
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
            timer = new Timer(Step, null, 1000, 1000);
            Task.Run((Action)AcceptClients);
            Task.Run((Action)ReplyToScans);
        }

        internal int Port { get; private set; }

        internal SnakeMatchState Snapshot()
        {
            lock (sync)
                return Snapshot(0);
        }

        private SnakeMatchState Snapshot(int slot)
        {
            var state = match.Snapshot(room, slot);
            state.ReadySlots = new[] { 0 }.Concat(Enumerable.Range(1, 3).Where(i => seats[i] != null && seats[i].Ready))
                .Concat(bots.Slots).ToArray();
            state.ReconnectingSlots = Enumerable.Range(1, 3).Where(i => seats[i] != null && seats[i].Connection == null).ToArray();
            state.BotSlots = bots.Slots.ToArray();
            state.BotDifficulty = bots.Difficulty;
            state.Music = new RoomMusicState { Volume = musicVolume };
            if (musicFile != null && state.Phase == MatchPhase.Playing)
            {
                state.Music.Track = musicTrack;
                state.Music.Title = musicFile.Name.Length <= 120 ? musicFile.Name : musicFile.Name.Substring(0, 120);
                state.Music.Extension = musicFile.Extension;
                state.Music.Length = musicLength;
                state.Music.Position = musicClock.ElapsedMilliseconds;
            }
            return state;
        }

        internal void SetMusic(FileInfo file)
        {
            lock (sync)
            {
                musicLength = file == null ? 0 : file.Length;
                musicFile = file;
                musicTrack++;
                musicClock.Restart();
            }
        }

        internal void SetMusicVolume(int percent)
        {
            lock (sync)
                musicVolume = percent;
        }

        internal void SetBots(int count, SnakeBotDifficulty difficulty)
        {
            lock (sync)
            {
                if (closed || match.Phase != MatchPhase.Lobby || !Enum.IsDefined(typeof(SnakeBotDifficulty), difficulty))
                    return;
                if (bots.Slots.Count() == count && bots.Difficulty == difficulty)
                    return;
                bots.Difficulty = difficulty;
                bots.SetCount(match, Math.Max(0, Math.Min(count, 3 - seats.Count(seat => seat != null))));
                ClearReadiness();
            }
        }

        internal bool Start()
        {
            string failure;
            return Start(out failure);
        }

        internal bool Start(out string failure)
        {
            lock (sync)
            {
                failure = null;
                if (closed || match.Phase == MatchPhase.Playing)
                    return false;
                ExpireSeats();
                if (match.Phase == MatchPhase.Finished)
                {
                    ReturnToLobbyLocked();
                    return false;
                }
                if (seats.Any(seat => seat != null && (seat.Connection == null || !seat.Ready)))
                {
                    failure = "请等成员准备好再开局。";
                    return false;
                }
                if (!match.Start())
                {
                    failure = match.NextMode == SnakeMode.Power ? "四区供电至少需要两名玩家。" :
                        match.NextMode == SnakeMode.Hotspot ? "热点争夺至少需要两名玩家。" :
                        match.NextMode == SnakeMode.Relay ? "词牌接力需要四个席位，可以添加人机。" : "团战人数至少要达到队伍数。";
                    return false;
                }
                clock.Restart();
                timer.Change(0, 15);
                return true;
            }
        }

        internal void ReturnToLobby()
        {
            lock (sync)
                if (!closed)
                    ReturnToLobbyLocked();
        }

        private void ReturnToLobbyLocked()
        {
            if (!match.ResetLobby())
                return;
            clock.Reset();
            foreach (var seat in seats)
                if (seat != null)
                    seat.Ready = false;
            timer.Change(1000, 1000);
        }

        internal void Turn(string word, int group, int round, bool charge = false)
        {
            lock (sync)
                if (!closed)
                    ApplyInput(0, new LanRequest { Kind = charge ? "charge" : "turn", Text = word, Group = group, Round = round });
        }

        internal void SetPreview(SnakeTypingPreview preview)
        {
            lock (sync)
                if (!closed)
                    match.SetPreview(0, preview, clock.ElapsedMilliseconds);
        }

        internal void SetSprint(int round, bool held)
        {
            lock (sync)
                if (!closed)
                    match.SetSprint(0, round, held, clock.ElapsedMilliseconds);
        }

        internal void SetRole(SnakeRole role)
        {
            lock (sync)
                if (!closed)
                    match.SetRole(0, role);
        }

        internal void SetMode(SnakeMode mode)
        {
            lock (sync)
                if (!closed && match.Phase != MatchPhase.Playing && match.NextMode != mode)
                {
                    match.SetMode(mode);
                    ClearReadiness();
                }
        }

        internal void SetTeamCount(int count)
        {
            lock (sync)
                if (!closed && match.Phase != MatchPhase.Playing && match.NextTeamCount != count)
                {
                    match.SetTeamCount(count);
                    ClearReadiness();
                }
        }

        internal void SetUniformRaceRole(bool uniform)
        {
            lock (sync)
                if (!closed && match.Phase != MatchPhase.Playing && match.UniformRaceRole != uniform)
                {
                    match.SetUniformRaceRole(uniform);
                    ClearReadiness();
                }
        }

        private void ClearReadiness()
        {
            foreach (var seat in seats)
                if (seat != null)
                    seat.Ready = false;
        }

        internal void DropBall(int round)
        {
            lock (sync)
                if (!closed)
                    match.DropBall(0, round, clock.ElapsedMilliseconds);
        }

        private void Step(object state)
        {
            lock (sync)
            {
                if (closed)
                    return;
                ExpireSeats();
                long elapsed = clock.ElapsedMilliseconds;
                match.Step(elapsed);
                bots.Step(match, elapsed);
                if (match.Phase != MatchPhase.Playing && !match.HasBurst)
                    timer.Change(1000, 1000);
            }
        }

        private void ExpireSeats()
        {
            long now = roomClock.ElapsedMilliseconds;
            for (int slot = 1; slot < seats.Length; slot++)
                if (seats[slot] != null && seats[slot].Connection == null && seats[slot].ReconnectUntil <= now)
                    RemoveSeat(slot);
        }

        private void RemoveSeat(int slot)
        {
            var seat = seats[slot];
            seats[slot] = null;
            match.RemovePlayer(slot);
            if (seat.Connection != null)
                seat.Connection.Close();
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
                if (hello != null && hello.Kind == "music")
                {
                    SendMusic(stream, hello);
                    return;
                }
                if (hello == null)
                    return;
                if (hello.Kind != LanWire.Version || hello.AppVersion != LanWire.AppVersion)
                {
                    if (hello.Kind != null && hello.Kind.StartsWith("TypingSnake/", StringComparison.Ordinal))
                        LanWire.Write(stream, new LanReply { Error = LanWire.VersionMismatch(LanWire.AppVersion,
                            LanWire.DisplayVersion(hello.AppVersion, hello.Kind)) });
                    return;
                }
                if (!LanWire.IsName(hello.Text, 20) || !Enum.IsDefined(typeof(SnakeRole), hello.Role))
                    return;

                LanReply reply;
                lock (sync)
                {
                    if (closed)
                        return;
                    ExpireSeats();
                    string failure;
                    if (hello.Session == null)
                    {
                        slot = match.AddPlayer(hello.Text, hello.Role);
                        if (slot >= 0)
                            seats[slot] = new Seat { Key = Guid.NewGuid().ToString("N"), Connection = client };
                        failure = "房间已满或本局正在进行。";
                    }
                    else
                    {
                        slot = Array.FindIndex(seats, seat => seat != null && seat.Key == hello.Session);
                        if (slot > 0)
                        {
                            var seat = seats[slot];
                            var old = seat.Connection;
                            seat.Connection = client;
                            seat.ReconnectUntil = 0;
                            match.ClearTransientInput(slot, clock.ElapsedMilliseconds);
                            if (old != null)
                                old.Close();
                        }
                        failure = "重连席位已过期，或房主已重新开房。";
                    }
                    reply = slot < 0 ? new LanReply { Error = failure } : Reply(slot);
                    if (slot >= 0)
                        reply.MusicKey = seats[slot].Key;
                }
                LanWire.Write(stream, reply);
                if (slot < 0)
                    return;

                while (true)
                {
                    var request = LanWire.Read<LanRequest>(stream, 512);
                    if (request == null || (request.Kind != "poll" && request.Kind != "turn" &&
                        request.Kind != "charge" && request.Kind != "role" && request.Kind != "drop" &&
                        request.Kind != "ready" && request.Kind != "leave"))
                        return;
                    lock (sync)
                    {
                        if (closed || seats[slot] == null || seats[slot].Connection != client)
                            return;
                        if (request.Kind == "leave")
                        {
                            RemoveSeat(slot);
                            return;
                        }
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
                    if (!closed && slot >= 0 && seats[slot] != null && seats[slot].Connection == client)
                    {
                        var seat = seats[slot];
                        seat.Connection = null;
                        seat.ReconnectUntil = roomClock.ElapsedMilliseconds + LanWire.ReconnectMilliseconds;
                        if (match.Phase != MatchPhase.Playing)
                            seat.Ready = false;
                        match.ClearTransientInput(slot, clock.ElapsedMilliseconds);
                    }
                    clients.Remove(client);
                }
                client.Close();
            }
        }

        private void ApplyInput(int slot, LanRequest request)
        {
            if (request.Kind == "ready" && slot != 0 && match.Phase == MatchPhase.Lobby && request.Round == match.Round)
                seats[slot].Ready = request.Ready;
            if (request.Preview != null)
                match.SetPreview(slot, request.Preview, clock.ElapsedMilliseconds);
            if (request.SprintRound != 0)
                match.SetSprint(slot, request.SprintRound, request.SprintHeld, clock.ElapsedMilliseconds);
            if (request.Kind == "role" && Enum.IsDefined(typeof(SnakeRole), request.Role))
            {
                match.SetRole(slot, request.Role);
                if (slot != 0 && match.Phase != MatchPhase.Playing)
                    seats[slot].Ready = false;
            }
            if (request.Kind == "drop")
                match.DropBall(slot, request.Round, clock.ElapsedMilliseconds);
            if (request.Text == null || request.Text.Length > 20)
                return;
            if (request.Kind == "turn")
                match.TryWord(slot, request.Text, request.Group, request.Round, clock.ElapsedMilliseconds);
            if (request.Kind == "charge")
                match.TryCharge(slot, request.Text, request.Group, request.Round, clock.ElapsedMilliseconds);
        }

        private LanReply Reply(int slot)
        {
            return new LanReply { Slot = slot, State = Snapshot(slot) };
        }

        private void SendMusic(NetworkStream stream, LanRequest request)
        {
            FileInfo file;
            long length;
            lock (sync)
            {
                if (closed || !seats.Any(seat => seat != null && seat.Connection != null && seat.Key == request.Text) || request.Group != musicTrack ||
                    match.Phase != MatchPhase.Playing || musicFile == null)
                    return;
                file = musicFile;
                length = musicLength;
            }
            using (var source = file.OpenRead())
            {
                if (source.Length != length || length <= 0 || length > LanWire.MusicLimit)
                    return;
                source.CopyTo(stream);
            }
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
                            Version = LanWire.Version, AppVersion = LanWire.AppVersion, Name = room,
                            Players = match.Count, Joinable = match.CanJoin, Mode = match.NextMode, Phase = match.Phase
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
        private readonly object connectionLock = new object();
        private TcpClient client;
        private volatile SnakeMatchState state;
        private volatile bool closed;
        private volatile bool reconnecting;
        private volatile bool leaving;
        private LanRequest turn;
        private LanRequest drop;
        private SnakeTypingPreview preview;
        private LanRequest sprint;
        private LanRequest readiness;
        private IPEndPoint endpoint;
        private string musicKey;
        private string playerName;
        private SnakeRole initialRole;

        internal int Slot { get; private set; }
        internal SnakeMatchState State { get { return state; } }
        internal bool Disconnected { get { return closed; } }
        internal bool Reconnecting { get { return reconnecting; } }

        internal async Task<string> Join(IPAddress address, string name, int port = LanWire.Port, SnakeRole role = SnakeRole.Standard)
        {
            endpoint = new IPEndPoint(address, port);
            playerName = name;
            initialRole = role;
            bool cancelled;
            try
            {
                var stream = await Connect().ConfigureAwait(false);
                var reply = await Task.Run(() => Hello(stream, null)).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(reply.Error))
                {
                    Dispose();
                    return reply.Error;
                }
                Slot = reply.Slot;
                musicKey = reply.MusicKey;
                state = reply.State;
                BeginPolling(stream);
                return null;
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error)) throw;
                cancelled = closed;
                Dispose();
            }
            if (!cancelled)
            {
                var old = await Task.Run(() => LanDiscovery.FindOlderVersion(endpoint)).ConfigureAwait(false);
                if (old != null)
                    return LanWire.VersionMismatch(old, LanWire.AppVersion);
            }
            return "无法连接房间，请确认在同一局域网。";
        }

        private void BeginPolling(NetworkStream stream)
        {
            Task.Run(() => Poll(stream));
        }

        private async Task<NetworkStream> Connect()
        {
            TcpClient socket;
            lock (connectionLock)
            {
                if (closed || leaving)
                    throw new IOException("Connection closed.");
                if (client != null)
                    client.Close();
                socket = client = new TcpClient(AddressFamily.InterNetwork) { NoDelay = true };
            }
            var connect = socket.ConnectAsync(endpoint.Address, endpoint.Port);
            if (await Task.WhenAny(connect, Task.Delay(2000)).ConfigureAwait(false) != connect)
                throw new IOException("Connection timed out.");
            await connect.ConfigureAwait(false);
            var stream = socket.GetStream();
            stream.ReadTimeout = stream.WriteTimeout = 2500;
            return stream;
        }

        private LanReply Hello(NetworkStream stream, string session)
        {
            LanWire.Write(stream, new LanRequest { Kind = LanWire.Version, Text = playerName, Role = initialRole,
                Session = session, AppVersion = LanWire.AppVersion });
            var reply = LanWire.Read<LanReply>(stream, LanWire.StateLimit);
            if (reply != null && !string.IsNullOrEmpty(reply.Error))
                return reply;
            Guid key;
            if (reply == null || reply.Slot < 1 || reply.Slot > 3 || !ValidState(reply.State, reply.Slot) ||
                !Guid.TryParseExact(reply.MusicKey, "N", out key))
                throw new InvalidDataException("Invalid LAN room reply.");
            return reply;
        }

        internal void Turn(string word, int group, int round, bool charge = false)
        {
            Interlocked.Exchange(ref turn, new LanRequest { Kind = charge ? "charge" : "turn", Text = word, Group = group, Round = round });
        }

        internal void SetPreview(SnakeTypingPreview value)
        {
            Interlocked.Exchange(ref preview, value);
        }

        internal void SetSprint(int round, bool held)
        {
            Interlocked.Exchange(ref sprint, new LanRequest { SprintRound = round, SprintHeld = held });
        }

        internal void SetRole(SnakeRole role)
        {
            Interlocked.Exchange(ref turn, new LanRequest { Kind = "role", Role = role });
        }

        internal void DropBall(int round)
        {
            Interlocked.Exchange(ref drop, new LanRequest { Kind = "drop", Round = round });
        }

        internal void SetReady(bool ready)
        {
            if (state != null)
                Interlocked.Exchange(ref readiness, new LanRequest { Kind = "ready", Ready = ready, Round = state.Round });
        }

        internal void Leave()
        {
            leaving = true;
        }

        internal async Task DownloadMusic(RoomMusicState music, string path, CancellationToken cancellation)
        {
            using (var transfer = new TcpClient(AddressFamily.InterNetwork))
            using (cancellation.Register(transfer.Close))
            {
                var connect = transfer.ConnectAsync(endpoint.Address, endpoint.Port);
                if (await Task.WhenAny(connect, Task.Delay(4000, cancellation)).ConfigureAwait(false) != connect)
                    throw new IOException("Music connection timed out.");
                await connect.ConfigureAwait(false);
                using (var stream = transfer.GetStream())
                using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                {
                    stream.WriteTimeout = 4000;
                    LanWire.Write(stream, new LanRequest { Kind = "music", Group = music.Track, Text = musicKey });
                    byte[] buffer = new byte[32768];
                    long remaining = music.Length;
                    while (remaining > 0)
                    {
                        var read = stream.ReadAsync(buffer, 0, (int)Math.Min(remaining, buffer.Length), cancellation);
                        using (var deadline = CancellationTokenSource.CreateLinkedTokenSource(cancellation))
                        {
                            if (await Task.WhenAny(read, Task.Delay(5000, deadline.Token)).ConfigureAwait(false) != read)
                                throw new IOException("Music transfer timed out.");
                            deadline.Cancel();
                        }
                        int count = await read.ConfigureAwait(false);
                        if (count == 0)
                            throw new EndOfStreamException();
                        file.Write(buffer, 0, count);
                        remaining -= count;
                    }
                }
            }
        }

        private async Task Poll(NetworkStream stream)
        {
            try
            {
                while (!closed)
                {
                    try
                    {
                        if (leaving)
                        {
                            LanWire.Write(stream, new LanRequest { Kind = "leave" });
                            break;
                        }
                        var request = Interlocked.Exchange(ref turn, null) ?? Interlocked.Exchange(ref drop, null) ??
                            Interlocked.Exchange(ref readiness, null) ?? new LanRequest { Kind = "poll" };
                        request.Preview = Interlocked.Exchange(ref preview, null);
                        var speed = Interlocked.Exchange(ref sprint, null);
                        if (speed != null)
                        {
                            request.SprintRound = speed.SprintRound;
                            request.SprintHeld = speed.SprintHeld;
                        }
                        LanWire.Write(stream, request);
                        var reply = LanWire.Read<LanReply>(stream, LanWire.StateLimit);
                        if (reply == null || reply.Slot != Slot || !ValidState(reply.State, Slot))
                            break;
                        state = reply.State;
                        await Task.Delay(75).ConfigureAwait(false);
                        continue;
                    }
                    catch (Exception error)
                    {
                        if (!LanWire.IsConnectionError(error)) throw;
                    }
                    stream = await Reconnect().ConfigureAwait(false);
                    if (stream == null)
                        break;
                }
            }
            finally
            {
                Dispose();
            }
        }

        private async Task<NetworkStream> Reconnect()
        {
            reconnecting = true;
            var deadline = Stopwatch.StartNew();
            while (!closed && !leaving && deadline.ElapsedMilliseconds < LanWire.ReconnectMilliseconds)
            {
                try
                {
                    var stream = await Connect().ConfigureAwait(false);
                    var reply = Hello(stream, musicKey);
                    if (!string.IsNullOrEmpty(reply.Error) || reply.Slot != Slot || reply.MusicKey != musicKey)
                        break;
                    Interlocked.Exchange(ref turn, null);
                    Interlocked.Exchange(ref drop, null);
                    Interlocked.Exchange(ref readiness, null);
                    Interlocked.Exchange(ref preview, null);
                    Interlocked.Exchange(ref sprint, null);
                    state = reply.State;
                    reconnecting = false;
                    return stream;
                }
                catch (Exception error)
                {
                    if (!LanWire.IsConnectionError(error)) throw;
                }
                await Task.Delay(400).ConfigureAwait(false);
            }
            return null;
        }

        private static bool ValidState(SnakeMatchState state, int slot)
        {
            if (state == null || !LanWire.IsName(state.Room, 24) ||
                (state.Mode == SnakeMode.Race ? state.Size != SnakeRace.Size :
                    state.Mode == SnakeMode.Power ? state.Size != SnakePower.Size :
                    state.Mode == SnakeMode.Hotspot ? state.Size != SnakeHotspot.Size :
                    state.Mode == SnakeMode.Relay ? state.Size != SnakeRelay.Size :
                    state.Mode == SnakeMode.Teams ? state.Size != SnakeTeams.Size : state.Size < 32 ||
                    state.Size > 80 || (state.Size - 32) % 16 != 0) || !Enum.IsDefined(typeof(MatchPhase), state.Phase) ||
                !Enum.IsDefined(typeof(SnakeMode), state.Mode) || !Enum.IsDefined(typeof(SnakeMode), state.NextMode) ||
                state.Players == null || state.Players.Length == 0 || state.Players.Length > 4 ||
                state.Rankings == null || state.Rankings.Length > 4 ||
                state.ReadySlots == null || state.ReadySlots.Length > 4 ||
                state.ReconnectingSlots == null || state.ReconnectingSlots.Length > 3 ||
                state.BotSlots == null || state.BotSlots.Length > 3 || !Enum.IsDefined(typeof(SnakeBotDifficulty), state.BotDifficulty) ||
                state.Music == null || !state.Music.IsValid ||
                state.Words == null || (state.Words.Length != 0 && state.Words.Length != 4 && state.Words.Length != 12))
                return false;
            foreach (string word in state.Words)
                if (!SnakeWords.Contains(word))
                    return false;
            foreach (var ranked in state.Rankings)
                if (ranked == null || ranked.Slot < 0 || ranked.Slot > 3 ||
                    !LanWire.IsName(ranked.Name, SnakePlayerState.MaxDisplayNameLength))
                    return false;
            int cells = state.Size * state.Size;
            bool teams = state.Mode == SnakeMode.Teams && state.Phase != MatchPhase.Lobby;
            if (state.NextTeamCount < 2 || state.NextTeamCount > 4 || state.TeamBalls == null || state.TeamScores == null ||
                (teams ? state.TeamCount < 2 || state.TeamCount > 4 || state.TeamScores.Length != state.TeamCount ||
                    state.TeamBalls.Length != state.TeamCount * 5 :
                    state.TeamCount != 0 || state.TeamScores.Length != 0 || state.TeamBalls.Length != 0))
                return false;
            foreach (var ball in state.TeamBalls)
                if (ball == null || ball.Team < 0 || ball.Team >= state.TeamCount || ball.Carrier < -1 || ball.Carrier > 3 ||
                    (ball.Carrier == -1 ? ball.Cell < 0 || ball.Cell >= cells : ball.Cell != -1))
                    return false;
            if (state.SafeStart < 0 || state.SafeEnd > state.Size || state.SafeEnd - state.SafeStart < 7 ||
                state.NextMinute < 1 || state.NextMinute > 60000 || state.Shots == null || state.Bursts == null ||
                state.Shots.Length > cells || state.Bursts.Length > 4)
                return false;
            foreach (int cell in System.Linq.Enumerable.Concat(state.Shots, state.Bursts))
                if (cell < 0 || cell >= cells)
                    return false;
            bool territory = state.Mode == SnakeMode.Territory && state.Phase != MatchPhase.Lobby;
            bool race = state.Mode == SnakeMode.Race && state.Phase != MatchPhase.Lobby;
            bool power = state.Mode == SnakeMode.Power && state.Phase != MatchPhase.Lobby;
            bool hotspot = state.Mode == SnakeMode.Hotspot && state.Phase != MatchPhase.Lobby;
            bool relay = state.Mode == SnakeMode.Relay && state.Phase != MatchPhase.Lobby;
            if (state.HotspotTarget < -1 || state.HotspotTarget >= cells || state.HotspotUpcoming < -1 || state.HotspotUpcoming >= cells ||
                (!hotspot && (state.HotspotTarget != -1 || state.HotspotUpcoming != -1)) ||
                state.RelayTarget < -1 || state.RelayTarget >= cells || (!relay && state.RelayTarget != -1) ||
                state.RelayWinner < -1 || state.RelayWinner > (relay ? 1 : -1) ||
                state.RelayTeams == null || state.RelayTeams.Length != (relay ? 2 : 0) ||
                (state.RelayHandoffWord != null && (!relay || state.Phase != MatchPhase.Playing ||
                    (state.RelayHandoffWord.Length != 6 && state.RelayHandoffWord.Length != 7) || !SnakeWords.Contains(state.RelayHandoffWord))))
                return false;
            foreach (var relayTeam in state.RelayTeams)
                if (relayTeam == null || relayTeam.Legs < 0 || relayTeam.Legs > SnakeRelay.LegCount ||
                    relayTeam.TimeMs < 0 || relayTeam.PenaltyMs < 0 || relayTeam.FinishMs < -1 ||
                    relayTeam.RunnerSlot < -1 || relayTeam.RunnerSlot > 3 || relayTeam.WaitingSlot < -1 || relayTeam.WaitingSlot > 3)
                    return false;
            if (state.RaceTarget < -1 || state.RaceTarget >= cells || (!race && (state.RaceSeed != 0 || state.RaceTarget != -1)) ||
                (race && state.RaceSeed <= 0) || state.PowerStations == null || state.PowerStations.Length != (power ? 4 : 0) ||
                (state.PowerWon && !power))
                return false;
            foreach (var station in state.PowerStations)
                if (station == null || station.Center < 0 || station.Center >= cells || station.Required < 4 || station.Required > 6 ||
                    station.Charge < 0 || station.Charge > station.Required)
                    return false;
            if (state.Obstacles == null || state.Obstacles.Length > cells ||
                state.Remaining < 0 || state.Remaining > (teams ? SnakeTeams.Duration : race ? SnakeRace.Duration :
                    power ? SnakePower.Duration : territory ? SnakeTerritory.Duration : hotspot ? SnakeHotspot.Duration : relay ? SnakeRelay.Duration : 0) ||
                (territory ? state.Land == null || state.Trails == null ||
                    state.Land.Length != cells || state.Trails.Length != cells
                    : state.Land != null || state.Trails != null))
                return false;
            if (territory)
                for (int i = 0; i < cells; i++)
                    if (state.Land[i] < 0 || state.Land[i] > 4 || state.Trails[i] < 0 || state.Trails[i] > 15)
                        return false;
            foreach (int cell in state.Obstacles)
                if (cell < 0 || cell >= cells)
                    return false;
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
                    !LanWire.IsName(player.Name, SnakePlayerState.MaxDisplayNameLength) ||
                    player.Body == null || player.Body.Length > cells ||
                    !Enum.IsDefined(typeof(SnakeResult), player.Result) ||
                    !Enum.IsDefined(typeof(SnakeDirection), player.Direction) ||
                    !Enum.IsDefined(typeof(SnakeRole), player.Role))
                    return false;
                if (player.Team < -1 || player.Team >= (relay ? 2 : state.TeamCount) || player.Carrying < 0 ||
                    player.Carrying > state.TeamBalls.Length || player.RespawnRemaining < 0 ||
                    player.RespawnRemaining > (teams ? SnakeTeams.RespawnDelay : power ? SnakePower.RespawnDelay :
                        hotspot ? SnakeHotspot.RespawnDelay : relay ? SnakeRelay.RespawnDelay : 0))
                    return false;
                if (!SnakeTypingPreview.IsValidText(player.Typing, player.TypingSkill) ||
                    (player.TypingSkill && !SnakeWords.HasSkill(player.Role, state.Mode)))
                    return false;
                if (player.Stabilizing && (player.Role != SnakeRole.Stabilizer || player.Result != SnakeResult.Running))
                    return false;
                if (player.StabilizerCooldownRemaining < 0 || player.StabilizerCooldownRemaining > SnakeGame.StabilizerTerritoryCooldown ||
                    (player.StabilizerCooldownRemaining > 0 && (!territory || player.Role != SnakeRole.Stabilizer || player.Result != SnakeResult.Running)))
                    return false;
                if (player.WordCount < 0 || (player.WordCount == 0 ? player.LastWord != null :
                    !SnakeWords.Contains(player.LastWord) && !SnakeWords.IsSkillWord(player.LastWord, SnakeRole.Hunter)))
                    return false;
                if (player.Turns < 0 || player.Turns > player.WordCount || player.SkillHits < 0 ||
                    player.Deliveries < 0 || player.LandGained < 0 || player.LandLost < 0 || player.SurvivalMs < 0)
                    return false;
                if (player.RaceProgress < 0 || player.RaceProgress > (race ? SnakeRace.CheckpointCount : 0) ||
                    player.RacePenaltyMs < 0 || player.RaceFinishMs < -1 || player.RaceCheckpointMs < 0 ||
                    (!race && (player.RacePenaltyMs != 0 || player.RaceFinishMs != -1 || player.RaceCheckpointMs != 0)) ||
                    (race && player.Slot != slot && player.Body.Length != 0) ||
                    player.PowerContribution < 0 || player.PowerContribution > (power ? 24 : 0))
                    return false;
                foreach (int cell in player.Body)
                    if (cell < 0 || cell >= cells)
                        return false;
                if (player.Slot == slot && player.Result == SnakeResult.Running &&
                    state.Words.Length != 4 * SnakeWords.Variants(player.Role))
                    return false;
                if (player.Slot == slot && state.Phase == MatchPhase.Playing &&
                    (SnakeWords.HasSkill(player.Role, state.Mode) && (!relay || player.Result == SnakeResult.Running) ?
                        !SnakeWords.IsSkillWord(state.ChargeWord, player.Role) :
                        state.ChargeWord != null))
                    return false;
            }
            return slots.Contains(slot) && state.ReadySlots.Distinct().Count() == state.ReadySlots.Length &&
                state.ReconnectingSlots.Distinct().Count() == state.ReconnectingSlots.Length &&
                state.ReadySlots.Contains(0) && state.ReadySlots.All(slots.Contains) &&
                state.BotSlots.Distinct().Count() == state.BotSlots.Length &&
                state.BotSlots.All(item => item != 0 && slots.Contains(item) && state.ReadySlots.Contains(item) && !state.ReconnectingSlots.Contains(item)) &&
                state.ReconnectingSlots.All(item => item != 0 && slots.Contains(item));
        }

        public void Dispose()
        {
            lock (connectionLock)
            {
                closed = true;
                if (client != null)
                    client.Close();
            }
        }
    }
}
