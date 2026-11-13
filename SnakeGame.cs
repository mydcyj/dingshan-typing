using System;
using System.Collections.Generic;

namespace EnglishTypingPractice
{
    internal enum SnakeDirection { North, East, South, West }
    internal enum SnakeResult { Ready, Running, Wall, Body, Won, Left, Rival, Survived, Obstacle, TimeUp }

    internal sealed class SnakeGame
    {
        internal const int BaseSize = 32;
        internal const int MoveInterval = 350;
        internal const int SmallFruitPoints = 10;
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
        private long stunUntil;
        private int chargeGroup;

        internal int Size { get; private set; }
        internal int Score { get; private set; }
        internal int Fruit { get; private set; }
        internal int WordGroup { get; private set; }
        internal SnakeRole Role { get; private set; }
        internal int Lives { get; private set; }
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

        internal SnakeGame(Random random = null)
        {
            this.random = random ?? new Random();
            Size = BaseSize;
            Fruit = -1;
            Obstacles = new HashSet<int>();
        }

        internal static int BoardSize(int players)
        {
            return BaseSize + 16 * (players - 1);
        }

        internal void Start(SnakeRole role, SnakeMode mode = SnakeMode.Classic)
        {
            Start(BaseSize, BaseSize / 2, BaseSize / 2, SnakeDirection.North, role);
            Obstacles = mode == SnakeMode.Obstacles ? SnakeObstacles.Create(Size, Body, random) : new HashSet<int>();
            PlaceFruit();
        }

        internal void Start(int size, int x, int y, SnakeDirection direction, SnakeRole role)
        {
            Size = size;
            Score = 0;
            Fruit = -1;
            Role = role;
            Lives = role == SnakeRole.Timid ? 2 : 1;
            spawnX = x;
            spawnY = y;
            spawnDirection = direction;
            nextMoveAt = Interval(0);
            chargeUntil = stunUntil = 0;
            ChargeWord = role == SnakeRole.Fearless ? SnakeWords.DrawCharge(random, ChargeWord) : null;
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

            Score += words[index].Length;
            Direction = direction;
            ExtraMoves = variant * 2;
            DrawWords();
            return true;
        }

        internal bool TryCharge(string word, int group, long now)
        {
            if (!IsRunning || Role != SnakeRole.Fearless || IsStunned(now) || IsCharging(now) ||
                group != chargeGroup || !string.Equals(word, ChargeWord, StringComparison.OrdinalIgnoreCase))
                return false;
            chargeUntil = now + ChargeDuration;
            stunnedRivals.Clear();
            nextMoveAt = now + Interval(now);
            Score += ChargeWord.Length;
            ChargeWord = SnakeWords.DrawCharge(random, ChargeWord);
            chargeGroup++;
            return true;
        }

        internal bool IsCharging(long now) { return IsRunning && chargeUntil > now; }
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
            return IsRunning && !IsStunned(now) && now >= nextMoveAt;
        }

        internal void ScheduleNext(long now)
        {
            nextMoveAt += Interval(now);
        }

        private double Interval(long now)
        {
            if (Role == SnakeRole.Timid)
                return MoveInterval / 0.75;
            if (Role == SnakeRole.Fearless)
                return IsCharging(now) ? 110 : MoveInterval / 1.2;
            return MoveInterval;
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
                {
                    var blocked = new HashSet<int>(Obstacles) { Fruit };
                    TryRevive(blocked, now);
                    return;
                }
                MoveTo(next, grows ? SmallFruitPoints : 0);
                if (grows)
                    PlaceFruit();
            }
        }

        internal int NextCell()
        {
            int x = body.First.Value % Size;
            int y = body.First.Value / Size;
            int next = body.First.Value + Offset(Direction);
            if ((Direction == SnakeDirection.North && y == 0) ||
                (Direction == SnakeDirection.East && x == Size - 1) ||
                (Direction == SnakeDirection.South && y == Size - 1) ||
                (Direction == SnakeDirection.West && x == 0))
                return -1;
            return next;
        }

        internal bool CanStep(int next, bool grows, HashSet<int> obstacles = null)
        {
            if (next < 0)
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

        internal bool TryRevive(HashSet<int> blocked, long now)
        {
            if (Lives < 2 || (Result != SnakeResult.Wall && Result != SnakeResult.Body &&
                Result != SnakeResult.Rival && Result != SnakeResult.Obstacle))
                return false;
            Lives--;
            int offset = Offset(spawnDirection);
            int head = -1;
            int best = int.MaxValue;
            for (int y = 1; y < Size - 1; y++)
            {
                for (int x = 1; x < Size - 1; x++)
                {
                    int cell = y * Size + x;
                    int distance = Math.Abs(x - spawnX) + Math.Abs(y - spawnY);
                    if (distance >= best || blocked.Contains(cell) || blocked.Contains(cell - offset) ||
                        blocked.Contains(cell - 2 * offset) || blocked.Contains(cell + offset) ||
                        (spawnDirection == SnakeDirection.North && y > Size - 3) ||
                        (spawnDirection == SnakeDirection.South && y < 2) ||
                        (spawnDirection == SnakeDirection.East && x < 2) ||
                        (spawnDirection == SnakeDirection.West && x > Size - 3))
                        continue;
                    head = cell;
                    best = distance;
                }
            }
            if (head < 0)
                return false;
            stunUntil = 0;
            nextMoveAt = now + Interval(now);
            ResetBody(head, spawnDirection);
            return true;
        }

        internal void Respawn(long now)
        {
            Respawns++;
            chargeUntil = stunUntil = 0;
            nextMoveAt = now + Interval(now);
            ResetBody(spawnY * Size + spawnX, spawnDirection);
        }

        internal void Leave()
        {
            End(SnakeResult.Left);
        }

        private void DrawWords()
        {
            WordGroup++;
            words = SnakeWords.Draw(random, Role, words);
        }

        private void PlaceFruit()
        {
            var blocked = new HashSet<int>(occupied);
            blocked.UnionWith(Obstacles);
            Fruit = FindFruit(Size, blocked, random);
            if (Fruit < 0)
                Result = SnakeResult.Won;
        }

        internal static int FindFruit(int size, HashSet<int> occupied, Random random, bool large = false)
        {
            const int margin = 4;
            int span = large ? 2 : 1;
            var inner = new List<int>();
            var outer = new List<int>();
            for (int y = 0; y <= size - span; y++)
            {
                for (int x = 0; x <= size - span; x++)
                {
                    int cell = y * size + x;
                    if (occupied.Contains(cell) || (large && (occupied.Contains(cell + 1) ||
                        occupied.Contains(cell + size) || occupied.Contains(cell + size + 1))))
                        continue;
                    if (x >= margin && y >= margin && x + span <= size - margin && y + span <= size - margin)
                        inner.Add(cell);
                    else
                        outer.Add(cell);
                }
            }
            var free = inner.Count > 0 ? inner : outer;
            return free.Count == 0 ? -1 : free[random.Next(free.Count)];
        }

        internal static int[] LargeFruitCells(int anchor, int size)
        {
            return new[] { anchor, anchor + 1, anchor + size, anchor + size + 1 };
        }
    }
}
