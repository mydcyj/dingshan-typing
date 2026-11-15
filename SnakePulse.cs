using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal sealed class SnakePulse
    {
        internal const int Size = 48;
        internal const int Duration = 180000;
        internal const int WaveCount = 12;
        internal const int WaveLength = 15000;
        internal const int RespawnDelay = 3000;
        private readonly Random random;
        private readonly int[] survived = new int[4];
        private readonly int[] deaths = new int[4];
        private readonly HashSet<int> cells = new HashSet<int>();
        private int[] cellArray = new int[0];
        internal int Wave { get; private set; }
        internal int Phase { get; private set; }
        internal int Lives { get; private set; }
        internal long NextBoundary { get; private set; }
        internal bool Won { get; set; }
        internal int[] Cells { get { return cellArray; } }
        internal int Survived(int slot) { return survived[slot]; }
        internal int Deaths(int slot) { return deaths[slot]; }
        internal bool IsDangerous(int cell) { return Phase == 2 && cells.Contains(cell); }
        internal int PhaseRemaining(long now) { return (int)Math.Max(0, NextBoundary - now); }

        internal SnakePulse(int participants, Random random)
        {
            this.random = random;
            Lives = participants * 2;
            StartWave(1, new int[0]);
        }

        private void StartWave(int wave, int[] heads)
        {
            Wave = wave;
            Phase = 1;
            cells.Clear();
            int count = 1 + (wave - 1) / 4;
            foreach (int row in SelectLanes(count, heads.Select(head => head / Size).ToArray()))
                for (int x = 0; x < Size; x++) cells.Add(row * Size + x);
            foreach (int column in SelectLanes(count, heads.Select(head => head % Size).ToArray()))
                for (int y = 0; y < Size; y++) cells.Add(y * Size + column);
            cellArray = cells.OrderBy(cell => cell).ToArray();
            NextBoundary = (wave - 1) * WaveLength + WarningLength;
        }

        private int[] SelectLanes(int count, int[] headCoordinates)
        {
            var selected = new List<int> { headCoordinates.Length == 0 ? random.Next(Size) : headCoordinates[random.Next(headCoordinates.Length)] };
            while (selected.Count < count)
            {
                var available = Enumerable.Range(0, Size).Where(value => selected.All(lane => Math.Abs(value - lane) >= 6)).ToArray();
                selected.Add(available[random.Next(available.Length)]);
            }
            return selected.ToArray();
        }

        private int WarningLength { get { return Wave <= 4 ? 3000 : Wave <= 8 ? 2500 : 2000; } }
        private int ActiveLength { get { return Wave <= 4 ? 3000 : Wave <= 8 ? 4000 : 5000; } }

        // Called at each boundary, before movement at the same timestamp.
        internal void Advance(long now, IEnumerable<int> activeSlots, int[] activeHeads)
        {
            if (now < NextBoundary || NextBoundary >= Duration)
                return;
            if (Phase == 1)
            {
                Phase = 2;
                NextBoundary += ActiveLength;
            }
            else if (Phase == 2)
            {
                foreach (int slot in activeSlots) survived[slot]++;
                Phase = 0;
                cells.Clear();
                cellArray = new int[0];
                NextBoundary = Wave * WaveLength;
            }
            else
                StartWave(Wave + 1, activeHeads);
        }

        internal bool TakeLife(int slot)
        {
            deaths[slot]++;
            if (Lives == 0) return false;
            Lives--;
            return true;
        }

        internal int SafeSpawn(SnakeGame snake)
        {
            int origin = snake.SpawnHead;
            var reverse = (SnakeDirection)(((int)snake.SpawnDirection + 2) % 4);
            foreach (int head in Enumerable.Range(0, Size * Size).OrderBy(cell =>
                Math.Abs(cell % Size - origin % Size) + Math.Abs(cell / Size - origin / Size)))
            {
                int back = head;
                bool safe = true;
                for (int i = 0; i < 3; i++)
                {
                    if (back < 0 || cells.Contains(back)) { safe = false; break; }
                    back = SnakeGame.AdjacentCell(back, reverse, Size);
                }
                int forward = head;
                for (int i = 0; safe && i < SnakeObstacles.SpawnClearance; i++)
                {
                    forward = SnakeGame.AdjacentCell(forward, snake.SpawnDirection, Size);
                    if (forward < 0 || cells.Contains(forward)) safe = false;
                }
                if (safe) return head;
            }
            return -1;
        }
    }
}
