using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal enum MatchPhase { Lobby, Playing, Finished }

    internal sealed class SnakePlayerState
    {
        public int Slot { get; set; }
        public string Name { get; set; }
        public int Score { get; set; }
        public SnakeDirection Direction { get; set; }
        public SnakeResult Result { get; set; }
        public int[] Body { get; set; }
        public SnakeRole Role { get; set; }
        public int Lives { get; set; }
        public int Respawns { get; set; }
        public bool Charging { get; set; }
        public int StunRemaining { get; set; }
    }

    internal sealed class SnakeMatchState
    {
        public string Room { get; set; }
        public MatchPhase Phase { get; set; }
        public int Size { get; set; }
        public SnakeMode Mode { get; set; }
        public SnakeMode NextMode { get; set; }
        public int[] Obstacles { get; set; }
        public int[] Land { get; set; }
        public int[] Trails { get; set; }
        public int Remaining { get; set; }
        public int SafeStart { get; set; }
        public int SafeEnd { get; set; }
        public int[] Shots { get; set; }
        public int[] Bursts { get; set; }
        public int NextMinute { get; set; }
        public int[] SmallFruits { get; set; }
        public int LargeFruit { get; set; }
        public SnakePlayerState[] Players { get; set; }
        public string[] Words { get; set; }
        public int WordGroup { get; set; }
        public string ChargeWord { get; set; }
        public int ChargeGroup { get; set; }
        public int Round { get; set; }
        public SnakePlayerState[] Rankings { get; set; }
    }

    internal sealed class SnakeMatch
    {
        internal const int LargeFruitPoints = 30;

        private sealed class Player
        {
            internal string Name;
            internal bool Connected = true;
            internal SnakeGame Snake;
            internal SnakeRole Role;
            internal int BurstCell = -1;
            internal long BurstUntil;
        }

        private sealed class Shot
        {
            internal SnakeGame Owner;
            internal SnakeDirection Direction;
            internal int Cell;
            internal double NextStep;
        }

        private readonly Player[] players = new Player[4];
        private readonly Random random = new Random();
        private readonly List<int> smallFruits = new List<int>();
        private readonly List<Shot> shots = new List<Shot>();
        private readonly bool solo;
        private HashSet<int> obstacles = new HashSet<int>();
        private int[] obstacleCells = new int[0];
        private SnakeTerritory territory;
        private SnakeMode mode;
        private SnakeMode nextMode;
        private int round;
        private int size = SnakeGame.BaseSize;
        private SnakeBounds bounds = new SnakeBounds(SnakeGame.BaseSize, 0);
        private int participants;
        private long now;
        private int largeFruit = -1;
        private SnakePlayerState[] rankings = new SnakePlayerState[0];

        internal MatchPhase Phase { get; private set; }
        internal int Count { get { return players.Count(player => player != null && player.Connected); } }
        internal bool CanJoin { get { return Phase != MatchPhase.Playing && Count < players.Length; } }
        internal bool HasBurst { get { return players.Any(player => player != null && player.BurstUntil > now); } }

        internal SnakeMatch(string hostName, SnakeRole role = SnakeRole.Standard, bool solo = false)
        {
            this.solo = solo;
            players[0] = new Player { Name = hostName, Role = role };
        }

        internal int AddPlayer(string name, SnakeRole role = SnakeRole.Standard)
        {
            if (!CanJoin)
                return -1;
            int slot = Array.FindIndex(players, player => player == null || !player.Connected);
            players[slot] = new Player { Name = name, Role = role };
            return slot;
        }

        internal void SetRole(int slot, SnakeRole role)
        {
            if (Phase != MatchPhase.Playing && players[slot] != null)
                players[slot].Role = role;
        }

        internal void SetMode(SnakeMode value)
        {
            if (Phase != MatchPhase.Playing)
                nextMode = value;
        }

        internal void RemovePlayer(int slot)
        {
            var player = players[slot];
            if (player == null)
                return;
            player.Connected = false;
            if (player.Snake != null)
                player.Snake.Leave();
            if (Phase != MatchPhase.Playing)
                return;
            if (territory != null)
                territory.ClearTrail(slot);
            else
                FinishIfDecided();
        }

        internal bool Start()
        {
            if (Phase == MatchPhase.Playing)
                return false;
            round++;
            now = 0;
            participants = Count;
            size = SnakeGame.BoardSize(participants);
            mode = nextMode;
            bounds = new SnakeBounds(size, 0);
            Phase = MatchPhase.Playing;
            rankings = new SnakePlayerState[0];
            smallFruits.Clear();
            shots.Clear();
            largeFruit = -1;

            int low = size / 4;
            int high = size - low - 1;
            int[] xs = { low, high, high, low };
            int[] ys = { low, high, low, high };
            SnakeDirection[] directions = { SnakeDirection.East, SnakeDirection.West, SnakeDirection.South, SnakeDirection.North };
            int seat = 0;
            for (int slot = 0; slot < players.Length; slot++)
            {
                var player = players[slot];
                if (player == null || !player.Connected)
                {
                    players[slot] = null;
                    continue;
                }
                player.Snake = new SnakeGame(random);
                player.BurstCell = -1;
                player.BurstUntil = 0;
                if (participants == 1)
                    player.Snake.Start(size, size / 2, size / 2, SnakeDirection.North, player.Role, mode);
                else
                    player.Snake.Start(size, xs[seat], ys[seat], directions[seat], player.Role, mode);
                seat++;
            }
            obstacles = mode == SnakeMode.Obstacles
                ? SnakeObstacles.Create(size, ActivePlayers().SelectMany(player => player.Snake.Body), random)
                : new HashSet<int>();
            obstacleCells = obstacles.ToArray();
            territory = mode == SnakeMode.Territory ? new SnakeTerritory(size) : null;
            if (territory == null)
                FillFruits();
            else
                for (int slot = 0; slot < players.Length; slot++)
                    if (players[slot] != null)
                        territory.Visit(slot, players[slot].Snake.Body.First.Value);
            return true;
        }

        internal bool TryWord(int slot, string word, int group, int roundNumber, long elapsed)
        {
            if (Phase != MatchPhase.Playing || roundNumber != round || (territory != null && elapsed >= SnakeTerritory.Duration))
                return false;
            var player = players[slot];
            return player != null && player.Connected && player.Snake.TryWord(word, group, elapsed);
        }

        internal bool TryCharge(int slot, string word, int group, int roundNumber, long elapsed)
        {
            if (Phase != MatchPhase.Playing || roundNumber != round || (territory != null && elapsed >= SnakeTerritory.Duration))
                return false;
            if (elapsed / 60000 != now / 60000)
                Step(elapsed);
            if (Phase != MatchPhase.Playing)
                return false;
            var player = players[slot];
            if (player == null || !player.Connected || !player.Snake.TryCharge(word, group, elapsed))
                return false;
            var snake = player.Snake;
            int head = snake.Body.First.Value;
            if (snake.Role == SnakeRole.Hunter)
                shots.Add(new Shot { Owner = snake, Direction = snake.Direction, Cell = head, NextStep = elapsed + snake.Interval(elapsed) / 3 });
            else if (snake.Role == SnakeRole.Bard)
            {
                player.BurstCell = head;
                player.BurstUntil = elapsed + 420;
                foreach (var rival in ActivePlayers().Where(other => other != player).ToArray())
                {
                    int rivalHead = rival.Snake.Body.First.Value;
                    if (Math.Abs(rivalHead % size - head % size) <= 2 && Math.Abs(rivalHead / size - head / size) <= 2)
                        Defeat(rival);
                }
                SettleCombat();
            }
            return true;
        }

        internal void Step(long elapsed)
        {
            now = elapsed;
            if (Phase != MatchPhase.Playing)
                return;
            if (territory != null && now >= SnakeTerritory.Duration)
            {
                foreach (var player in ActivePlayers())
                    player.Snake.End(SnakeResult.TimeUp);
                Finish();
                return;
            }
            var active = ActivePlayers().ToArray();
            if (mode == SnakeMode.Classic)
            {
                var newBounds = new SnakeBounds(size, now);
                if (newBounds.Start != bounds.Start || newBounds.End != bounds.End)
                {
                    bounds = newBounds;
                    foreach (var player in active)
                        player.Snake.UpdateClock(now);
                    smallFruits.RemoveAll(cell => !bounds.Contains(cell, size));
                    if (largeFruit >= 0 && SnakeGame.LargeFruitCells(largeFruit, size).Any(cell => !bounds.Contains(cell, size)))
                        largeFruit = -1;
                    SettleCombat();
                    if (Phase != MatchPhase.Playing)
                        return;
                }
            }
            else if (territory != null)
                foreach (var player in active)
                    player.Snake.UpdateClock(now);

            AdvanceShots();
            if (Phase != MatchPhase.Playing)
                return;
            var due = ActivePlayers().Select(player => player.Snake).Where(snake => snake.MoveDue(now)).ToArray();
            if (due.Length == 0)
                return;
            int substeps = due.Max(snake => snake.ExtraMoves) + 1;
            for (int step = 0; step < substeps && Phase == MatchPhase.Playing; step++)
            {
                Move(due.Where(snake => snake.IsRunning && !snake.IsStunned(now) && snake.ExtraMoves >= step).ToArray());
                if (Phase == MatchPhase.Playing)
                    CheckShotContacts();
            }
            foreach (var snake in due)
            {
                if (snake.MoveDue(now))
                    snake.ScheduleNext(now);
                snake.FinishBurst();
            }
        }

        private void Move(SnakeGame[] due)
        {
            if (due.Length == 0)
                return;
            if (territory != null)
            {
                MoveTerritory(due);
                return;
            }
            var snakes = ActivePlayers().Select(player => player.Snake).ToArray();
            var largeCells = largeFruit < 0 ? new int[0] : SnakeGame.LargeFruitCells(largeFruit, size);
            var moves = new Dictionary<SnakeGame, int>();
            var largeConsumers = new List<SnakeGame>();
            foreach (var snake in due)
            {
                int next = snake.NextCell();
                bool takesLarge = largeCells.Contains(next);
                if (!snake.CanStep(next, takesLarge || smallFruits.Contains(next), obstacles))
                    continue;
                moves.Add(snake, next);
                if (takesLarge)
                    largeConsumers.Add(snake);
            }
            var collisions = new HashSet<SnakeGame>();
            foreach (var move in moves)
            {
                if (!move.Key.IsCharging(now) && moves.Any(other => other.Key != move.Key &&
                    !other.Key.IsCharging(now) && other.Value == move.Value))
                    collisions.Add(move.Key);
            }
            var charging = moves.Where(item => item.Key.IsCharging(now)).ToArray();
            var stunned = new HashSet<SnakeGame>();
            foreach (var move in charging)
                foreach (var rival in snakes.Where(snake => snake != move.Key &&
                    (snake.Body.Contains(move.Value) || (moves.ContainsKey(snake) && moves[snake] == move.Value))))
                    if (move.Key.HitRival(rival))
                        stunned.Add(rival);
            // A tail only moves out of the way if its snake survives this step.
            int previous;
            do
            {
                previous = collisions.Count;
                foreach (var move in moves)
                {
                    if (move.Key.IsCharging(now))
                        continue;
                    foreach (var rival in snakes.Where(snake => snake != move.Key))
                    {
                        if ((!rival.IsCharging(now) || stunned.Contains(rival)) && rival.Body.Contains(move.Value) &&
                            (move.Value != rival.Body.Last.Value || !moves.ContainsKey(rival) ||
                             stunned.Contains(rival) || smallFruits.Contains(moves[rival]) ||
                             largeConsumers.Contains(rival) || collisions.Contains(rival)))
                            collisions.Add(move.Key);
                    }
                }
            } while (previous != collisions.Count);

            foreach (var rival in stunned)
                rival.Stun(now);

            bool ateLarge = false;
            // Simultaneous arrivals at different cells of the large fruit all collect it.
            foreach (var move in moves)
            {
                if (move.Key.IsStunned(now))
                    continue;
                if (collisions.Contains(move.Key))
                    move.Key.End(SnakeResult.Rival);
                else
                {
                    bool takesLarge = largeConsumers.Contains(move.Key);
                    ateLarge |= takesLarge;
                    int points = takesLarge ? LargeFruitPoints :
                        smallFruits.Remove(move.Value) ? SnakeGame.SmallFruitPoints : 0;
                    move.Key.MoveTo(move.Value, points);
                }
            }
            if (ateLarge)
                largeFruit = -1;
            RevivePlayers();
            FinishIfDecided();
            if (Phase == MatchPhase.Playing)
                FillFruits();
        }

        private void MoveTerritory(SnakeGame[] due)
        {
            var captures = new List<KeyValuePair<Player, bool[]>>();
            foreach (var snake in due)
            {
                if (snake.IsStunned(now))
                    continue;
                int slot = Array.FindIndex(players, player => player != null && player.Snake == snake);
                int next = snake.NextCell();
                if (next < 0)
                {
                    RespawnTerritory(slot);
                    continue;
                }
                else
                {
                    if (snake.IsCharging(now))
                        foreach (var rival in ActivePlayers().Select(player => player.Snake))
                            if (rival != snake && rival.Body.Contains(next) && snake.HitRival(rival))
                                rival.Stun(now);
                    snake.MoveTo(next, 0);
                }
                var captured = territory.Visit(slot, next);
                if (captured != null)
                    captures.Add(new KeyValuePair<Player, bool[]>(players[slot], captured));
            }
            if (captures.Count > 0)
                foreach (var player in ActivePlayers().ToArray())
                    if (captures.Any(capture => capture.Key != player && capture.Value[player.Snake.Body.First.Value]))
                        Defeat(player);
        }

        private void RespawnTerritory(int slot)
        {
            var snake = players[slot].Snake;
            territory.ClearTrail(slot);
            snake.Respawn(now);
            territory.Visit(slot, snake.Body.First.Value);
        }

        private void Defeat(Player player)
        {
            if (territory == null)
                player.Snake.End(SnakeResult.Skill);
            else
                RespawnTerritory(Array.IndexOf(players, player));
        }

        private void AdvanceShots()
        {
            bool hit = false;
            for (int i = shots.Count - 1; i >= 0; i--)
            {
                var shot = shots[i];
                while (shot.NextStep <= now)
                {
                    shot.Cell = SnakeGame.AdjacentCell(shot.Cell, shot.Direction, size);
                    if (!bounds.Contains(shot.Cell, size) || obstacles.Contains(shot.Cell))
                    {
                        shots.RemoveAt(i);
                        break;
                    }
                    if (HitWithShot(shot))
                    {
                        hit = true;
                        shots.RemoveAt(i);
                        break;
                    }
                    shot.NextStep += shot.Owner.Interval(now) / 3;
                }
            }
            if (hit)
                SettleCombat();
        }

        private void CheckShotContacts()
        {
            bool hit = false;
            for (int i = shots.Count - 1; i >= 0; i--)
                if (HitWithShot(shots[i]))
                {
                    shots.RemoveAt(i);
                    hit = true;
                }
            if (hit)
                SettleCombat();
        }

        private bool HitWithShot(Shot shot)
        {
            var target = ActivePlayers().FirstOrDefault(player => player.Snake != shot.Owner && player.Snake.Body.Contains(shot.Cell));
            if (target == null)
                return false;
            Defeat(target);
            return true;
        }

        private void SettleCombat()
        {
            if (territory != null)
                return;
            RevivePlayers();
            FinishIfDecided();
            if (Phase == MatchPhase.Playing)
                FillFruits();
        }

        internal SnakeMatchState Snapshot(string room, int slot)
        {
            var own = players[slot].Snake;
            return new SnakeMatchState
            {
                Room = room,
                Phase = Phase,
                Size = Phase == MatchPhase.Lobby ? SnakeGame.BoardSize(Count) : size,
                Mode = Phase == MatchPhase.Lobby ? nextMode : mode,
                NextMode = nextMode,
                Obstacles = obstacleCells,
                Land = territory == null ? null : territory.Land,
                Trails = territory == null ? null : territory.Trails,
                Remaining = territory == null ? 0 : (int)Math.Max(0, SnakeTerritory.Duration - now),
                SafeStart = Phase == MatchPhase.Lobby ? 0 : bounds.Start,
                SafeEnd = Phase == MatchPhase.Lobby ? SnakeGame.BoardSize(Count) : bounds.End,
                Shots = shots.Select(shot => shot.Cell).ToArray(),
                Bursts = players.Where(player => player != null && player.BurstUntil > now).Select(player => player.BurstCell).ToArray(),
                NextMinute = (int)(60000 - now % 60000),
                SmallFruits = smallFruits.ToArray(),
                LargeFruit = largeFruit,
                WordGroup = own == null ? 0 : own.WordGroup,
                ChargeGroup = own == null ? 0 : own.ChargeGroup,
                ChargeWord = own == null ? null : own.ChargeWord,
                Round = round,
                Words = own == null ? new string[0] : own.Words,
                Players = PlayerStates(),
                Rankings = rankings
            };
        }

        private SnakePlayerState[] PlayerStates()
        {
            var result = new List<SnakePlayerState>(players.Length);
            for (int slot = 0; slot < players.Length; slot++)
            {
                var player = players[slot];
                if (player == null || (!player.Connected && player.Snake == null))
                    continue;
                var snake = player.Snake;
                result.Add(new SnakePlayerState
                {
                    Slot = slot,
                    Name = player.Name,
                    Score = territory != null ? territory.Score(slot) : snake == null ? 0 : snake.Score,
                    Direction = snake == null ? SnakeDirection.North : snake.Direction,
                    Result = snake == null ? SnakeResult.Ready : snake.Result,
                    Role = Phase == MatchPhase.Playing && snake != null ? snake.Role : player.Role,
                    Lives = snake == null ? (player.Role == SnakeRole.Timid ? 2 : 1) : snake.Lives,
                    Respawns = snake == null ? 0 : snake.Respawns,
                    Charging = snake != null && snake.IsCharging(now),
                    StunRemaining = snake == null ? 0 : snake.StunRemaining(now),
                    Body = snake != null && (snake.IsRunning || snake.Result == SnakeResult.Survived ||
                        snake.Result == SnakeResult.Won || snake.Result == SnakeResult.TimeUp) ? snake.Body.ToArray() : new int[0]
                });
            }
            return result.ToArray();
        }

        private void Finish()
        {
            Phase = MatchPhase.Finished;
            shots.Clear();
            rankings = PlayerStates().Where(player => territory == null || player.Result != SnakeResult.Left)
                .OrderByDescending(player => player.Score).ThenBy(player => player.Slot).ToArray();
        }

        private void FinishIfDecided()
        {
            var survivors = ActivePlayers().ToArray();
            if (survivors.Length == 1 && participants > 1)
                survivors[0].Snake.End(SnakeResult.Survived);
            else if (survivors.Length != 0)
                return;
            Finish();
        }

        private IEnumerable<Player> ActivePlayers()
        {
            return players.Where(player => player != null && player.Snake != null && player.Snake.IsRunning);
        }

        private void RevivePlayers()
        {
            foreach (var player in players.Where(item => item != null && item.Connected &&
                item.Snake != null && !item.Snake.IsRunning && item.Snake.Lives > 1))
            {
                var blocked = new HashSet<int>(ActivePlayers().SelectMany(item => item.Snake.Body));
                blocked.UnionWith(obstacles);
                blocked.UnionWith(smallFruits);
                if (largeFruit >= 0)
                    blocked.UnionWith(SnakeGame.LargeFruitCells(largeFruit, size));
                player.Snake.TryRevive(blocked, now);
            }
        }

        private void FillFruits()
        {
            if ((solo || largeFruit >= 0) && smallFruits.Count == participants)
                return;
            var occupied = new HashSet<int>(ActivePlayers().SelectMany(player => player.Snake.Body));
            occupied.UnionWith(obstacles);
            occupied.UnionWith(smallFruits);
            if (largeFruit < 0 && !solo)
                largeFruit = SnakeGame.FindFruit(size, occupied, random, large: true, bounds: bounds,
                    heads: ActivePlayers().Select(player => player.Snake.Body.First.Value).ToArray());
            if (largeFruit >= 0)
                occupied.UnionWith(SnakeGame.LargeFruitCells(largeFruit, size));
            while (smallFruits.Count < participants)
            {
                int fruit = SnakeGame.FindFruit(size, occupied, random, bounds: bounds);
                if (fruit < 0)
                    break;
                smallFruits.Add(fruit);
                occupied.Add(fruit);
            }
            if (largeFruit >= 0 || smallFruits.Count > 0)
                return;
            foreach (var player in ActivePlayers())
                player.Snake.End(SnakeResult.Won);
            Finish();
        }
    }
}
