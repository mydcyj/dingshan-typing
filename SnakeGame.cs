using System;
using System.Collections.Generic;

namespace EnglishTypingPractice
{
    internal enum SnakeDirection { North, East, South, West }
    internal enum SnakeResult { Ready, Running, Wall, Body, Won, Left, Rival, Survived, Obstacle, TimeUp, Skill }

    internal sealed class SnakeGame
    {
        internal const int BaseSize = 32;
        internal const int MinSoloSize = 32;
        internal const int MaxSoloSize = 128;
        internal const int MoveInterval = 350;
        internal const int SmallFruitPoints = 10;
        internal const int TimewalkerTurnPoints = 6;
        internal const int NavigatorTurnPoints = 6;
        internal const int CollectorTurnPoints = 5;
        internal const int CollectorSmallFruitPoints = 14;
        internal const int CollectorLargeFruitPoints = 38;
        internal const double CollectorCarryPenalty = 0.08;
        internal const int StabilizerDuration = 2500;
        internal const double StabilizerSpeed = 0.7;
        internal const int StabilizerTerritoryCooldown = 40000;
        internal const int ChargeDuration = 1000;
        internal const int StunDuration = 3000;


        private readonly Random random;
        private readonly LinkedList<int> body = new LinkedList<int>();
        private readonly HashSet<int> occupied = new HashSet<int>();
        private readonly HashSet<SnakeGame> stunnedRivals = new HashSet<SnakeGame>();
        private string[] words = new string[0];
        private SnakeDirection movement;
        private int spawnX;
        private int spawnY;
        private SnakeDirection spawnDirection;
        private double nextMoveAt;
        private long chargeUntil;
        private long stabilizationUntil;
        private long stabilizeReadyAt;
        private long stunUntil;
        private int chargeGroup;
        private SnakeMode mode;
        private double speed = 1;
        private double carrySpeed = 1;
        private bool sprintHeld;
        private int carried;

        internal int Size { get; private set; }
        internal int Score { get; private set; }
        internal int Fruit { get; private set; }
        internal int WordGroup { get; private set; }
        internal SnakeRole Role { get; private set; }
        internal int SkillUses { get; private set; }
        internal string LastWord { get; private set; }
        internal int WordCount { get; private set; }
        internal int Turns { get; private set; }
        internal int SkillHits { get; set; }
        internal int Deliveries { get; set; }
        internal long SurvivalMs { get; private set; }
        internal int Respawns { get; private set; }
        internal int ExtraMoves { get; private set; }
        internal string ChargeWord { get; private set; }
        internal int ChargeGroup { get { return chargeGroup; } }
        internal string[] Words { get { return words; } }
        internal SnakeResult Result { get; private set; }
        internal SnakeDirection Direction { get; private set; }
        internal SnakeDirection Opposite { get { return (SnakeDirection)(((int)Direction + 2) % 4); } }
        internal bool TurnPending { get { return Direction != movement || ExtraMoves > 0; } }
        internal bool IsRunning { get { return Result == SnakeResult.Running; } }
        internal LinkedList<int> Body { get { return body; } }
        internal HashSet<int> Obstacles { get; private set; }
        internal SnakeBounds Bounds { get; private set; }

        internal SnakeGame(Random random = null)
        {
            this.random = random ?? new Random();
            Size = BaseSize;
            Bounds = new SnakeBounds(Size, 0);
            Fruit = -1;
            Obstacles = new HashSet<int>();
        }

        internal static int BoardSize(int players)
        {
            return BaseSize + 16 * (players - 1);
        }

        internal void Start(SnakeRole role, SnakeMode mode = SnakeMode.Classic, int size = BaseSize)
        {
            Start(size, size / 2, size / 2, SnakeDirection.North, role, mode);
            Obstacles = mode == SnakeMode.Obstacles
                ? SnakeObstacles.Create(Size, Body, new[] { SpawnHead }, random) : new HashSet<int>();
            PlaceFruit();
        }

        internal void Start(int size, int x, int y, SnakeDirection direction, SnakeRole role, SnakeMode mode = SnakeMode.Classic)
        {
            this.mode = mode;
            Size = size;
            Bounds = new SnakeBounds(Size, 0);
            speed = 1;
            carrySpeed = 1;
            sprintHeld = false;
            carried = 0;
            Score = 0;
            Fruit = -1;
            Role = role;
            SkillUses = 0;
            LastWord = null;
            WordCount = 0;
            Turns = SkillHits = Deliveries = Respawns = 0;
            SurvivalMs = 0;
            spawnX = x;
            spawnY = y;
            spawnDirection = direction;
            chargeUntil = stunUntil = stabilizationUntil = stabilizeReadyAt = 0;
            nextMoveAt = Interval(0);
            ChargeWord = SnakeWords.HasSkill(role, mode) ? SnakeWords.DrawCharge(random, ChargeWord, role) : null;
            chargeGroup++;
            ResetBody(y * size + x, direction);
        }

        private void ResetBody(int head, SnakeDirection direction)
        {
            Direction = movement = direction;
            Result = SnakeResult.Running;
            ExtraMoves = 0;
            body.Clear();
            occupied.Clear();
            int offset = Offset(direction);
            for (int i = 0; i < 3; i++)
            {
                int cell = head - i * offset;
                body.AddLast(cell);
                occupied.Add(cell);
            }
            DrawWords();
        }

        internal bool TryWord(string word, int group, long now)
        {
            if (!IsRunning || IsStunned(now) || TurnPending || group != WordGroup)
                return false;
            int index = Array.FindIndex(words, candidate => string.Equals(candidate, word, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return false;
            int variants = SnakeWords.Variants(Role);
            int variant = index % variants;
            var direction = (SnakeDirection)(index / variants);
            if (direction == Opposite || (direction == Direction && variant != 2))
                return false;

            bool balancedTimewalker = Role == SnakeRole.Timewalker && UsesTimewalkerBalance(mode);
            if (balancedTimewalker)
                Score += direction != Direction ? TimewalkerTurnPoints : 0;
            else if (mode == SnakeMode.Classic || mode == SnakeMode.Obstacles)
                Score += Role == SnakeRole.Navigator ? NavigatorTurnPoints :
                    Role == SnakeRole.Collector ? CollectorTurnPoints : words[index].Length;
            else
                Score += words[index].Length;
            LastWord = words[index];
            WordCount++;
            if (direction != Direction)
                Turns++;
            Direction = direction;
            ExtraMoves = variant * (balancedTimewalker ? 1 : 2);
            DrawWords();
            return true;
        }

        internal static bool UsesTimewalkerBalance(SnakeMode mode)
        {
            return mode == SnakeMode.Classic || mode == SnakeMode.Obstacles;
        }

        internal bool TryCharge(string word, int group, long now)
        {
            if (!IsRunning || !SnakeWords.HasSkill(Role, mode) || IsStunned(now) || IsCharging(now) || IsStabilizing(now) ||
                StabilizerCooldownRemaining(now) > 0 || group != chargeGroup ||
                !string.Equals(word, ChargeWord, StringComparison.OrdinalIgnoreCase))
                return false;
            if (Role == SnakeRole.Fearless)
            {
                chargeUntil = now + ChargeDuration;
                stunnedRivals.Clear();
                nextMoveAt = now + Interval(now);
            }
            if (Role == SnakeRole.Stabilizer)
            {
                ApplyStabilizationExpiry(now);
                if (nextMoveAt > now)
                    nextMoveAt = now + (nextMoveAt - now) / StabilizerSpeedFor(mode);
                stabilizationUntil = now + StabilizerDurationFor(mode);
                if (mode == SnakeMode.Territory)
                    stabilizeReadyAt = now + StabilizerTerritoryCooldown;
                Score += StabilizerSkillPointsFor(mode);
            }
            else
                Score += ChargeWord.Length;
            LastWord = ChargeWord;
            WordCount++;
            SkillUses++;
            ChargeWord = SnakeWords.DrawCharge(random, ChargeWord, Role);
            chargeGroup++;
            return true;
        }

        internal bool IsCharging(long now) { return IsRunning && chargeUntil > now; }
        internal void RecordActiveTime(long now)
        {
            if (IsRunning)
                SurvivalMs = now;
        }
        internal bool IsStabilizing(long now) { return IsRunning && stabilizationUntil > now; }
        internal int StabilizerCooldownRemaining(long now) { return IsRunning ? (int)Math.Max(0, stabilizeReadyAt - now) : 0; }
        internal bool IsStunned(long now) { return IsRunning && stunUntil > now; }
        internal int StunRemaining(long now) { return IsRunning ? (int)Math.Max(0, stunUntil - now) : 0; }

        internal void Stun(long now)
        {
            chargeUntil = 0;
            stunUntil = now + StunDuration;
            nextMoveAt = stunUntil;
            Direction = movement;
            ExtraMoves = 0;
        }

        internal bool HitRival(SnakeGame rival)
        {
            return stunnedRivals.Add(rival);
        }

        internal bool MoveDue(long now)
        {
            return IsRunning && !IsStunned(now) && now >= MovementDeadline(now);
        }

        internal void ScheduleNext(long now)
        {
            ApplyStabilizationExpiry(now);
            nextMoveAt += Interval(now);
        }

        private double MovementDeadline(long now)
        {
            // The stored deadline is measured at slow speed until the effect expires.
            if (stabilizationUntil == 0 || now < stabilizationUntil || nextMoveAt <= stabilizationUntil ||
                nextMoveAt == stunUntil)
                return nextMoveAt;
            return stabilizationUntil + (nextMoveAt - stabilizationUntil) * StabilizerSpeedFor(mode);
        }

        private void ApplyStabilizationExpiry(long now)
        {
            if (stabilizationUntil == 0 || now < stabilizationUntil)
                return;
            nextMoveAt = MovementDeadline(now);
            stabilizationUntil = 0;
        }

        internal double Interval(long now)
        {
            if (Role == SnakeRole.Timid)
                return MoveInterval / (0.85 * speed * carrySpeed);
            if (Role == SnakeRole.Fearless)
                return (IsCharging(now) ? 110 : MoveInterval / FearlessBaseSpeed(mode)) / (speed * carrySpeed);
            return MoveInterval / (speed * carrySpeed * (IsStabilizing(now) ? StabilizerSpeedFor(mode) : 1));
        }

        internal static int StabilizerDurationFor(SnakeMode mode) { return mode == SnakeMode.Territory ? 1500 : StabilizerDuration; }
        internal static double StabilizerSpeedFor(SnakeMode mode) { return mode == SnakeMode.Territory ? 0.85 : StabilizerSpeed; }

        internal static int StabilizerSkillPointsFor(SnakeMode mode)
        {
            return mode == SnakeMode.Obstacles ? 8 : mode == SnakeMode.Classic ? 5 : 0;
        }

        internal static double FearlessBaseSpeed(SnakeMode mode)
        {
            return mode == SnakeMode.Territory ? 1 : 1.2;
        }

        internal void SetCarrying(int count, long now)
        {
            carried = count;
            UpdateTeamSpeed(now);
        }

        internal void SetSprint(bool held, long now)
        {
            sprintHeld = held;
            UpdateTeamSpeed(now);
        }

        private void UpdateTeamSpeed(long now)
        {
            ApplyStabilizationExpiry(now);
            double penalty = Role == SnakeRole.Collector ? CollectorCarryPenalty : 0.1;
            double factor = carried == 0 && sprintHeld ? 1.25 : Math.Max(0.1, 1 - carried * penalty);
            if (factor != carrySpeed && !IsStunned(now) && nextMoveAt > now)
                nextMoveAt = now + (nextMoveAt - now) * carrySpeed / factor;
            carrySpeed = factor;
        }

        internal bool UpdateClock(long now)
        {
            if (mode == SnakeMode.Territory)
            {
                double newSpeed = Math.Pow(1.2, now / 60000);
                if (speed != newSpeed)
                {
                    ApplyStabilizationExpiry(now);
                    if (!IsStunned(now))
                        nextMoveAt = now + (nextMoveAt - now) * speed / newSpeed;
                    speed = newSpeed;
                }
                return false;
            }
            if (mode != SnakeMode.Classic)
                return false;
            var bounds = new SnakeBounds(Size, now);
            if (bounds.Start == Bounds.Start && bounds.End == Bounds.End)
                return false;
            Bounds = bounds;
            if (!Bounds.Contains(body.First.Value, Size))
                End(SnakeResult.Wall);
            return true;
        }

        internal bool UpdateSoloClock(long now)
        {
            bool stabilizationExpired = stabilizationUntil != 0 && now >= stabilizationUntil;
            if (stabilizationExpired)
                ApplyStabilizationExpiry(now);
            if (!UpdateClock(now))
                return stabilizationExpired;
            if (IsRunning && !Bounds.Contains(Fruit, Size))
                PlaceFruit();
            return true;
        }

        internal void FinishBurst()
        {
            ExtraMoves = 0;
        }

        internal void Step(long now)
        {
            if (!IsRunning)
                return;

            int moves = 1 + ExtraMoves;
            FinishBurst();
            while (moves-- > 0 && IsRunning)
            {
                int next = NextCell();
                bool grows = next >= 0 && next == Fruit;
                if (!CanStep(next, grows, Obstacles))
                    return;
                MoveTo(next, grows ? SmallFruitPoints : 0);
                if (grows)
                    PlaceFruit();
            }
        }

        internal int NextCell()
        {
            return AdjacentCell(body.First.Value, Direction, Size);
        }

        internal static int AdjacentCell(int cell, SnakeDirection direction, int size)
        {
            switch (direction)
            {
                case SnakeDirection.North: return cell >= size ? cell - size : -1;
                case SnakeDirection.East: return cell % size < size - 1 ? cell + 1 : -1;
                case SnakeDirection.South: return cell / size < size - 1 ? cell + size : -1;
                default: return cell % size > 0 ? cell - 1 : -1;
            }
        }

        internal bool CanStep(int next, bool grows, HashSet<int> obstacles = null)
        {
            if (!Bounds.Contains(next, Size))
            {
                Result = SnakeResult.Wall;
                return false;
            }
            if (occupied.Contains(next) && (grows || next != body.Last.Value))
            {
                Result = SnakeResult.Body;
                return false;
            }
            if (obstacles != null && obstacles.Contains(next))
            {
                Result = SnakeResult.Obstacle;
                return false;
            }
            return true;
        }

        internal void MoveTo(int next, int fruitPoints)
        {
            movement = Direction;

            if (fruitPoints == 0)
            {
                occupied.Remove(body.Last.Value);
                body.RemoveLast();
            }
            body.AddFirst(next);
            occupied.Add(next);
            if (Role == SnakeRole.Collector && fruitPoints > 0 && (mode == SnakeMode.Classic || mode == SnakeMode.Obstacles))
                fruitPoints = fruitPoints == SmallFruitPoints ? CollectorSmallFruitPoints : CollectorLargeFruitPoints;
            Score += fruitPoints;
        }

        private int Offset(SnakeDirection direction)
        {
            switch (direction)
            {
                case SnakeDirection.North: return -Size;
                case SnakeDirection.East: return 1;
                case SnakeDirection.South: return Size;
                default: return -1;
            }
        }

        internal void End(SnakeResult result)
        {
            if (IsRunning)
                Result = result;
        }

        internal void Respawn(long now, int? head = null)
        {
            RespawnAt(now, head ?? (spawnY * Size + spawnX), spawnDirection);
        }

        internal void RespawnAt(long now, int head, SnakeDirection direction)
        {
            Respawns++;
            RestartBody(now, head, direction);
        }

        internal void BeginRelayLeg(long now, int head, SnakeDirection direction, string handoffWord)
        {
            LastWord = handoffWord;
            WordCount++;
            RestartBody(now, head, direction);
        }

        private void RestartBody(long now, int head, SnakeDirection direction)
        {
            chargeUntil = stunUntil = stabilizationUntil = 0;
            ChargeWord = SnakeWords.HasSkill(Role, mode) ? SnakeWords.DrawCharge(random, ChargeWord, Role) : null;
            chargeGroup++;
            ResetBody(head, direction);
            nextMoveAt = now + Interval(now);
        }

        internal int SpawnHead { get { return spawnY * Size + spawnX; } }
        internal SnakeDirection SpawnDirection { get { return spawnDirection; } }

        internal void Leave()
        {
            End(SnakeResult.Left);
        }

        private void DrawWords()
        {
            WordGroup++;
            words = SnakeWords.Draw(random, Role, words, WordCount, mode);
        }

        private void PlaceFruit()
        {
            var blocked = new HashSet<int>(occupied);
            blocked.UnionWith(Obstacles);
            Fruit = FindFruit(Size, blocked, random, bounds: Bounds);
            if (Fruit < 0)
                Result = SnakeResult.Won;
        }

        internal void PlaceTutorialFruit()
        {
            int next = NextCell();
            if (Bounds.Contains(next, Size) && !occupied.Contains(next))
                Fruit = next;
        }

        internal static int FindFruit(int size, HashSet<int> occupied, Random random, bool large = false,
            SnakeBounds? bounds = null, int[] heads = null)
        {
            const int margin = 4;
            int span = large ? 2 : 1;
            var inner = new List<int>();
            var outer = new List<int>();
            var area = bounds ?? new SnakeBounds(size, 0);
            for (int y = area.Start; y <= area.End - span; y++)
            {
                for (int x = area.Start; x <= area.End - span; x++)
                {
                    int cell = y * size + x;
                    if (occupied.Contains(cell) || (large && (occupied.Contains(cell + 1) ||
                        occupied.Contains(cell + size) || occupied.Contains(cell + size + 1))))
                        continue;
                    if (x >= area.Start + margin && y >= area.Start + margin &&
                        x + span <= area.End - margin && y + span <= area.End - margin)
                        inner.Add(cell);
                    else
                        outer.Add(cell);
                }
            }
            var free = inner.Count > 0 ? inner : outer;
            if (large && heads != null && heads.Length > 1 && free.Count > 0)
            {
                var fair = new List<int>();
                int bestGap = int.MaxValue, bestTotal = int.MaxValue;
                foreach (int cell in free)
                {
                    int minimum = int.MaxValue, maximum = 0, total = 0;
                    foreach (int head in heads)
                    {
                        int distance = Math.Abs(2 * (head % size - cell % size) - 1) +
                            Math.Abs(2 * (head / size - cell / size) - 1);
                        minimum = Math.Min(minimum, distance);
                        maximum = Math.Max(maximum, distance);
                        total += distance;
                    }
                    int gap = maximum - minimum;
                    if (gap > bestGap || (gap == bestGap && total > bestTotal))
                        continue;
                    if (gap != bestGap || total != bestTotal)
                        fair.Clear();
                    bestGap = gap;
                    bestTotal = total;
                    fair.Add(cell);
                }
                free = fair;
            }
            return free.Count == 0 ? -1 : free[random.Next(free.Count)];
        }

        internal static int[] LargeFruitCells(int anchor, int size)
        {
            return new[] { anchor, anchor + 1, anchor + size, anchor + size + 1 };
        }
    }
}
