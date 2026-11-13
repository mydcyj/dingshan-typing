using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal enum SnakeMode { Classic, Obstacles, Territory }

    internal static class SnakeObstacles
    {
        private static readonly int[][] Shapes =
        {
            new[] { 0, 1, 2, 3 }, new[] { 0, 1, 4, 5 }, new[] { 0, 1, 2, 5 },
            new[] { 0, 4, 8, 9 }, new[] { 1, 5, 8, 9 }, new[] { 1, 2, 4, 5 },
            new[] { 0, 1, 5, 6 }
        };

        internal static HashSet<int> Create(int size, IEnumerable<int> bodies, Random random)
        {
            var cells = new HashSet<int>();
            var reserved = new HashSet<int>();
            foreach (int cell in bodies)
                Reserve(reserved, size, cell, 5);

            for (int attempts = 0; cells.Count < 4 * (7 + (size - SnakeGame.BaseSize) / 4) && attempts < 300; attempts++)
            {
                int x = random.Next(2, size - 5);
                int y = random.Next(2, size - 5);
                int rotation = random.Next(4);
                var shape = Shapes[random.Next(Shapes.Length)].Select(cell =>
                {
                    int dx = cell % 4;
                    int dy = cell / 4;
                    for (int turn = 0; turn < rotation; turn++)
                    {
                        int oldX = dx;
                        dx = 3 - dy;
                        dy = oldX;
                    }
                    return (y + dy) * size + x + dx;
                }).ToArray();
                if (shape.Any(reserved.Contains))
                    continue;
                cells.UnionWith(shape);
                foreach (int cell in shape)
                    Reserve(reserved, size, cell, 1);
            }
            return cells;
        }

        private static void Reserve(HashSet<int> cells, int size, int center, int radius)
        {
            int x = center % size;
            int y = center / size;
            for (int row = Math.Max(0, y - radius); row <= Math.Min(size - 1, y + radius); row++)
                for (int col = Math.Max(0, x - radius); col <= Math.Min(size - 1, x + radius); col++)
                    cells.Add(row * size + col);
        }
    }

    internal sealed class SnakeTerritory
    {
        internal const int Duration = 5 * 60 * 1000;

        private readonly int size;
        private readonly int[] land;
        private readonly int[] trails;
        private readonly int[] scores = new int[4];
        private readonly List<int>[] paths = { new List<int>(), new List<int>(), new List<int>(), new List<int>() };
        private int[] landSnapshot;
        private int[] trailSnapshot;

        internal SnakeTerritory(int size)
        {
            this.size = size;
            land = new int[size * size];
            trails = new int[land.Length];
        }

        internal int[] Land { get { return landSnapshot ?? (landSnapshot = (int[])land.Clone()); } }
        internal int[] Trails { get { return trailSnapshot ?? (trailSnapshot = (int[])trails.Clone()); } }
        internal int Score(int slot) { return scores[slot]; }

        internal void ClearTrail(int slot)
        {
            int bit = 1 << slot;
            foreach (int cell in paths[slot])
                trails[cell] &= ~bit;
            paths[slot].Clear();
            trailSnapshot = null;
        }

        internal void Visit(int slot, int cell)
        {
            var path = paths[slot];
            int start = path.IndexOf(cell);
            if (start >= 0)
            {
                Capture(slot, path, start);
                ClearTrail(slot);
            }
            path.Add(cell);
            trails[cell] |= 1 << slot;
            trailSnapshot = null;
        }

        private void Capture(int slot, List<int> path, int start)
        {
            var visited = new bool[land.Length];
            for (int i = start; i < path.Count; i++)
            {
                visited[path[i]] = true;
                Claim(slot, path[i]);
            }

            // The loop blocks a flood fill starting outside it on the board's edges.
            var outside = new Queue<int>();
            for (int i = 0; i < size; i++)
            {
                Enqueue(i, visited, outside);
                Enqueue(land.Length - 1 - i, visited, outside);
                Enqueue(i * size, visited, outside);
                Enqueue(i * size + size - 1, visited, outside);
            }
            while (outside.Count > 0)
            {
                int cell = outside.Dequeue();
                if (cell % size > 0) Enqueue(cell - 1, visited, outside);
                if (cell % size < size - 1) Enqueue(cell + 1, visited, outside);
                if (cell >= size) Enqueue(cell - size, visited, outside);
                if (cell < land.Length - size) Enqueue(cell + size, visited, outside);
            }
            for (int cell = 0; cell < land.Length; cell++)
                if (!visited[cell])
                    Claim(slot, cell);
            landSnapshot = null;
        }

        private void Claim(int slot, int cell)
        {
            int previous = land[cell];
            if (previous == slot + 1)
                return;
            if (previous != 0)
                scores[previous - 1]--;
            land[cell] = slot + 1;
            scores[slot]++;
        }

        private static void Enqueue(int cell, bool[] visited, Queue<int> outside)
        {
            if (visited[cell])
                return;
            visited[cell] = true;
            outside.Enqueue(cell);
        }
    }
}
