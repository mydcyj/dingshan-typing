using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal sealed class SnakeRace
    {
        internal const int Size = 48;
        internal const int CheckpointCount = 6;
        internal const int CheckpointRadius = 1;
        internal const int Duration = 180000;
        internal const int Penalty = 5000;
        internal const int SoloSeed = 20261114;

        private sealed class Entry
        {
            internal int Progress;
            internal int PenaltyMs;
            internal int LastCheckpointMs;
            internal int FinishMs = -1;
        }

        private readonly Entry[] entries = { new Entry(), new Entry(), new Entry(), new Entry() };
        private readonly int[] route;
        private readonly SnakeDirection[] headings;

        internal int Seed { get; private set; }
        internal HashSet<int> Obstacles { get; private set; }
        internal int[] ObstacleCells { get; private set; }

        internal SnakeRace(int seed)
        {
            Seed = seed;
            var random = new Random(seed);
            int startX = random.Next(7, 10), right = random.Next(33, 37);
            int left = random.Next(10, 14), endX = random.Next(34, 38);
            int top = random.Next(7, 10), middle = random.Next(19, 24);
            int bottom = random.Next(32, 36), finishY = random.Next(41, 44);
            int rotation = random.Next(4);
            bool mirror = random.Next(2) == 0;
            route = new[]
            {
                Transform(startX, top, rotation, mirror), Transform(right, top, rotation, mirror),
                Transform(right, middle, rotation, mirror), Transform(left, middle, rotation, mirror),
                Transform(left, bottom, rotation, mirror), Transform(endX, bottom, rotation, mirror),
                Transform(endX, finishY, rotation, mirror)
            };
            headings = Enumerable.Range(0, CheckpointCount).Select(index => Heading(route[index], route[index + 1])).ToArray();

            // A wide corridor stays clear so every gate has a reachable entrance and a safe respawn runway.
            var corridor = new List<int>();
            for (int index = 0; index < CheckpointCount; index++)
            {
                int cell = route[index];
                while (cell != route[index + 1])
                {
                    corridor.Add(cell);
                    cell = SnakeGame.AdjacentCell(cell, headings[index], Size);
                }
                // Keep room to type a turn after respawning in the direction of the last leg.
                for (int runway = 0; index < CheckpointCount - 1 && runway < 5; runway++)
                {
                    cell = SnakeGame.AdjacentCell(cell, headings[index], Size);
                    if (cell < 0)
                        break;
                    corridor.Add(cell);
                }
            }
            corridor.Add(route[CheckpointCount]);
            Obstacles = SnakeObstacles.Create(Size, corridor, random);
            ObstacleCells = Obstacles.OrderBy(cell => cell).ToArray();
        }

        internal void Start(int slot, SnakeGame snake, SnakeRole role)
        {
            entries[slot] = new Entry();
            snake.Start(Size, route[0] % Size, route[0] / Size, headings[0], role, SnakeMode.Race);
        }

        internal int NextCheckpoint(int slot)
        {
            int progress = Progress(slot);
            return progress == CheckpointCount ? -1 : route[progress + 1];
        }

        internal int Progress(int slot) { return entries[slot].Progress; }
        internal int PenaltyMs(int slot) { return entries[slot].PenaltyMs; }
        internal int FinishMs(int slot) { return entries[slot].FinishMs; }
        internal int LastCheckpointMs(int slot) { return entries[slot].LastCheckpointMs; }

        internal void Move(int slot, SnakeGame snake, long elapsed)
        {
            if (elapsed >= Duration || !snake.IsRunning)
                return;
            var entry = entries[slot];
            int next = snake.NextCell();
            if (!snake.CanStep(next, false, Obstacles))
            {
                entry.PenaltyMs += Penalty;
                snake.RespawnAt(elapsed, route[entry.Progress], headings[Math.Max(0, entry.Progress - 1)]);
                return;
            }
            snake.MoveTo(next, 0);
            int checkpoint = route[entry.Progress + 1];
            if (Math.Abs(next % Size - checkpoint % Size) > CheckpointRadius ||
                Math.Abs(next / Size - checkpoint / Size) > CheckpointRadius)
                return;
            entry.LastCheckpointMs = (int)elapsed + entry.PenaltyMs;
            if (++entry.Progress == CheckpointCount)
            {
                entry.FinishMs = entry.LastCheckpointMs;
                snake.End(SnakeResult.Won);
            }
        }

        internal int Compare(int leftSlot, int rightSlot)
        {
            var left = entries[leftSlot];
            var right = entries[rightSlot];
            if (left.FinishMs >= 0 && right.FinishMs >= 0)
                return left.FinishMs.CompareTo(right.FinishMs);
            int progress = right.Progress.CompareTo(left.Progress);
            return progress != 0 ? progress : left.LastCheckpointMs.CompareTo(right.LastCheckpointMs);
        }

        internal IEnumerable<SnakePlayerState> Rank(IEnumerable<SnakePlayerState> players)
        {
            return players.OrderBy(player => player.Result == SnakeResult.Left).ThenBy(player => player.Slot,
                Comparer<int>.Create((left, right) =>
                {
                    int order = Compare(left, right);
                    return order != 0 ? order : left.CompareTo(right);
                }));
        }

        private static int Transform(int x, int y, int rotation, bool mirror)
        {
            if (mirror)
                x = Size - 1 - x;
            for (int index = 0; index < rotation; index++)
            {
                int oldX = x;
                x = Size - 1 - y;
                y = oldX;
            }
            return y * Size + x;
        }

        private static SnakeDirection Heading(int from, int to)
        {
            if (from / Size == to / Size)
                return to > from ? SnakeDirection.East : SnakeDirection.West;
            return to > from ? SnakeDirection.South : SnakeDirection.North;
        }
    }
}
