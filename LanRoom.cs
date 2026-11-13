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
        public SnakeTypingPreview Preview { get; set; }
        public int SprintRound { get; set; }
        public bool SprintHeld { get; set; }
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
        internal const string Version = "TypingSnake/8";
        internal const int StateLimit = 65536;
        internal const int MusicLimit = 64 * 1024 * 1024;
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
        private readonly Stopwatch musicClock = new Stopwatch();
        private readonly HashSet<string> musicKeys = new HashSet<string>();
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
            timer = new Timer(Step, null, Timeout.Infinite, Timeout.Infinite);
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

        internal bool Start()
        {
            lock (sync)
            {
                if (closed || !match.Start())
                    return false;
                clock.Restart();
                timer.Change(0, 15);
                return true;
            }
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
                if (!closed)
                    match.SetMode(mode);
        }

        internal void SetTeamCount(int count)
        {
            lock (sync)
                if (!closed)
                    match.SetTeamCount(count);
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
                match.Step(clock.ElapsedMilliseconds);
                if (match.Phase != MatchPhase.Playing && !match.HasBurst)
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
            string musicKey = null;
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
                    if (slot >= 0)
                    {
                        musicKey = Guid.NewGuid().ToString("N");
                        musicKeys.Add(musicKey);
                        reply.MusicKey = musicKey;
                    }
                }
                LanWire.Write(stream, reply);
                if (slot < 0)
                    return;

                while (true)
                {
                    var request = LanWire.Read<LanRequest>(stream, 512);
                    if (request == null || (request.Kind != "poll" && request.Kind != "turn" &&
                        request.Kind != "charge" && request.Kind != "role" && request.Kind != "drop"))
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
                    {
                        match.RemovePlayer(slot);
                        musicKeys.Remove(musicKey);
                    }
                    clients.Remove(client);
                }
                client.Close();
            }
        }

        private void ApplyInput(int slot, LanRequest request)
        {
            if (request.Preview != null)
                match.SetPreview(slot, request.Preview, clock.ElapsedMilliseconds);
            if (request.SprintRound != 0)
                match.SetSprint(slot, request.SprintRound, request.SprintHeld, clock.ElapsedMilliseconds);
            if (request.Kind == "role" && Enum.IsDefined(typeof(SnakeRole), request.Role))
                match.SetRole(slot, request.Role);
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
                if (closed || !musicKeys.Contains(request.Text) || request.Group != musicTrack ||
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
        private LanRequest drop;
        private SnakeTypingPreview preview;
        private LanRequest sprint;
        private IPEndPoint endpoint;
        private string musicKey;

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
                endpoint = new IPEndPoint(address, port);
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
            Guid key;
            if (reply == null || reply.Slot < 1 || reply.Slot > 3 || !ValidState(reply.State, reply.Slot) ||
                !Guid.TryParseExact(reply.MusicKey, "N", out key))
                throw new InvalidDataException("Invalid LAN room reply.");
            musicKey = reply.MusicKey;
            Slot = reply.Slot;
            state = reply.State;
            Task.Run(() => Poll(stream));
            return null;
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

        private void Poll(NetworkStream stream)
        {
            try
            {
                while (!closed)
                {
                    var request = Interlocked.Exchange(ref turn, null) ?? Interlocked.Exchange(ref drop, null) ??
                        new LanRequest { Kind = "poll" };
                    request.Preview = Interlocked.Exchange(ref preview, null);
                    var speed = Interlocked.Exchange(ref sprint, null);
                    if (speed != null)
                    {
                        request.SprintRound = speed.SprintRound;
                        request.SprintHeld = speed.SprintHeld;
                    }
                    LanWire.Write(stream, request);
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
            if (state == null || !LanWire.IsName(state.Room, 24) ||
                (state.Mode == SnakeMode.Teams ? state.Size != SnakeTeams.Size : state.Size < 32 ||
                    state.Size > 80 || (state.Size - 32) % 16 != 0) || !Enum.IsDefined(typeof(MatchPhase), state.Phase) ||
                !Enum.IsDefined(typeof(SnakeMode), state.Mode) || !Enum.IsDefined(typeof(SnakeMode), state.NextMode) ||
                state.Players == null || state.Players.Length == 0 || state.Players.Length > 4 ||
                state.Rankings == null || state.Rankings.Length > 4 ||
                state.Music == null || !state.Music.IsValid ||
                state.Words == null || (state.Words.Length != 0 && state.Words.Length != 4 && state.Words.Length != 12))
                return false;
            foreach (string word in state.Words)
                if (!SnakeWords.Contains(word))
                    return false;
            foreach (var ranked in state.Rankings)
                if (ranked == null || ranked.Slot < 0 || ranked.Slot > 3 || !LanWire.IsName(ranked.Name, 20))
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
            if (state.Obstacles == null || state.Obstacles.Length > cells ||
                state.Remaining < 0 || state.Remaining > (teams ? SnakeTeams.Duration : SnakeTerritory.Duration) ||
                (territory ? state.Land == null || state.Trails == null ||
                    state.Land.Length != cells || state.Trails.Length != cells
                    : state.Land != null || state.Trails != null || (!teams && state.Remaining != 0)))
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
                    !LanWire.IsName(player.Name, 20) || player.Body == null || player.Body.Length > cells ||
                    !Enum.IsDefined(typeof(SnakeResult), player.Result) ||
                    !Enum.IsDefined(typeof(SnakeDirection), player.Direction) ||
                    !Enum.IsDefined(typeof(SnakeRole), player.Role))
                    return false;
                if (player.Team < -1 || player.Team >= state.TeamCount || player.Carrying < 0 ||
                    player.Carrying > state.TeamBalls.Length || player.RespawnRemaining < 0 ||
                    player.RespawnRemaining > SnakeTeams.RespawnDelay + 1000 || (!teams && player.RespawnRemaining != 0))
                    return false;
                if (!SnakeTypingPreview.IsValidText(player.Typing, player.TypingSkill) ||
                    (player.TypingSkill && !SnakeWords.HasSkill(player.Role)))
                    return false;
                if (player.WordCount < 0 || (player.WordCount == 0 ? player.LastWord != null :
                    !SnakeWords.Contains(player.LastWord) && !SnakeWords.IsSkillWord(player.LastWord, SnakeRole.Hunter)))
                    return false;
                foreach (int cell in player.Body)
                    if (cell < 0 || cell >= cells)
                        return false;
                if (player.Slot == slot && player.Result == SnakeResult.Running &&
                    state.Words.Length != 4 * SnakeWords.Variants(player.Role))
                    return false;
                if (player.Slot == slot && state.Phase == MatchPhase.Playing &&
                    (SnakeWords.HasSkill(player.Role) ? !SnakeWords.IsSkillWord(state.ChargeWord, player.Role) :
                        state.ChargeWord != null))
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
