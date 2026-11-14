using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal sealed class SnakeHotspot
    {
        internal const int Size = 48;
        internal const int Radius = 3;
        internal const int Duration = 240000;
        internal const int RespawnDelay = 5000;
        private const int Interval = 30000;
        private const int Warning = 5000;

        private readonly int[] targets;
        private readonly long[] held = new long[4];
        private long previous;

        internal SnakeHotspot(Random random)
        {
            int[] points = { 10, 23, 37 };
            targets = points.SelectMany(y => points.Select(x => y * Size + x))
                .OrderBy(cell => random.Next()).Take(Duration / Interval).ToArray();
        }

        internal int Target(long elapsed)
        {
            return elapsed >= Duration ? -1 : targets[(int)(elapsed / Interval)];
        }

        internal int Upcoming(long elapsed)
        {
            int next = (int)(elapsed / Interval) + 1;
            return next < targets.Length && elapsed % Interval >= Interval - Warning ? targets[next] : -1;
        }

        internal int Score(int slot) { return (int)(held[slot] / 1000); }

        internal void Advance(long now, int[] heads)
        {
            now = Math.Min(now, Duration);
            while (previous < now)
            {
                int target = Target(previous);
                long until = Math.Min(now, (previous / Interval + 1) * Interval);
                int owner = -1;
                for (int slot = 0; slot < heads.Length; slot++)
                {
                    int head = heads[slot];
                    if (head < 0 || Math.Abs(head % Size - target % Size) > Radius ||
                        Math.Abs(head / Size - target / Size) > Radius)
                        continue;
                    if (owner >= 0)
                    {
                        owner = -1;
                        break;
                    }
                    owner = slot;
                }
                if (owner >= 0)
                    held[owner] += until - previous;
                previous = until;
            }
        }

        internal static int SafeSpawn(SnakeGame snake, HashSet<int> occupied)
        {
            int origin = snake.SpawnHead;
            int best = -1, distance = int.MaxValue;
            for (int cell = 0; cell < Size * Size; cell++)
            {
                int delta = Math.Abs(cell % Size - origin % Size) + Math.Abs(cell / Size - origin / Size);
                if (delta >= distance || occupied.Contains(cell))
                    continue;
                int forward = SnakeGame.AdjacentCell(cell, snake.SpawnDirection, Size);
                var reverse = (SnakeDirection)(((int)snake.SpawnDirection + 2) % 4);
                int tail = SnakeGame.AdjacentCell(cell, reverse, Size);
                int end = tail < 0 ? -1 : SnakeGame.AdjacentCell(tail, reverse, Size);
                if (forward < 0 || end < 0 || occupied.Contains(forward) || occupied.Contains(tail) || occupied.Contains(end))
                    continue;
                best = cell;
                distance = delta;
                if (delta == 0)
                    break;
            }
            return best;
        }
    }
}
