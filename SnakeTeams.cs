using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal sealed class TeamBall
    {
        public int Team { get; set; }
        public int Cell { get; set; }
        public int Carrier { get; set; }
    }

    internal sealed class SnakeTeams
    {
        internal const int Size = 60;
        internal const int Duration = 8 * 60 * 1000;
        internal const int RespawnDelay = 15000;
        private readonly Random random;
        private readonly int[] assignments = { -1, -1, -1, -1 };
        private readonly List<TeamBall> balls = new List<TeamBall>();

        internal SnakeTeams(int count, int[] slots, Random random)
        {
            Count = count;
            this.random = random;
            for (int i = slots.Length - 1; i > 0; i--)
            {
                int j = random.Next(i + 1);
                int slot = slots[i];
                slots[i] = slots[j];
                slots[j] = slot;
            }
            for (int i = 0; i < slots.Length; i++)
                assignments[slots[i]] = i % count;
        }

        internal int Count { get; private set; }
        internal int Team(int slot) { return assignments[slot]; }
        internal int Carrying(int slot) { return balls.Count(ball => ball.Carrier == slot); }
        internal int[] Scores() { return Enumerable.Range(0, Count).Select(team => balls.Count(ball => ball.Team == team && ball.Carrier < 0)).ToArray(); }
        internal TeamBall[] Snapshot() { return balls.Select(ball => new TeamBall { Team = ball.Team, Cell = ball.Cell, Carrier = ball.Carrier }).ToArray(); }

        internal static int Region(int cell, int count)
        {
            int x = cell % Size;
            int y = cell / Size;
            return count == 4 ? (y / 30) * 2 + x / 30 : x / (Size / count);
        }

        internal static int Spawn(int team, int count, bool second)
        {
            int width = count == 4 ? 30 : Size / count;
            int x = (count == 4 ? team % 2 : team) * width + width / 2;
            int y = count == 4 ? team / 2 * 30 + 15 : 30;
            return (y + (second ? 5 : -5)) * Size + x;
        }

        internal int FindSpawn(int slot, int preferred, IEnumerable<int> occupied)
        {
            var blocked = new HashSet<int>(occupied);
            blocked.UnionWith(balls.Where(ball => ball.Carrier < 0).Select(ball => ball.Cell));
            int team = Team(slot);
            int closest = -1;
            int best = int.MaxValue;
            for (int cell = Size; cell < Size * (Size - 2); cell++)
            {
                if (Region(cell, Count) != team || Region(cell + Size * 2, Count) != team ||
                    blocked.Contains(cell) || blocked.Contains(cell - Size) ||
                    blocked.Contains(cell + Size) || blocked.Contains(cell + Size * 2))
                    continue;
                int distance = Math.Abs(cell % Size - preferred % Size) + Math.Abs(cell / Size - preferred / Size);
                if (distance < best)
                {
                    closest = cell;
                    best = distance;
                }
            }
            return closest;
        }

        internal void PlaceInitial(IEnumerable<int> occupied)
        {
            var reserved = new HashSet<int>(occupied);
            for (int team = 0; team < Count; team++)
                for (int i = 0; i < 5; i++)
                    balls.Add(new TeamBall { Team = team, Cell = FindCell(team, reserved), Carrier = -1 });
        }

        internal bool IsOwnBall(int slot, int cell)
        {
            return balls.Any(ball => ball.Cell == cell && ball.Carrier < 0 && ball.Team == Team(slot));
        }

        internal void PickUp(int slot, int cell)
        {
            foreach (var ball in balls)
                if (ball.Carrier < 0 && ball.Cell == cell && ball.Team != Team(slot))
                {
                    ball.Carrier = slot;
                    ball.Cell = -1;
                    break;
                }
        }

        internal bool Drop(int slot, int cell)
        {
            if (Region(cell, Count) != Team(slot) || balls.Any(ball => ball.Cell == cell))
                return false;
            var carried = balls.FirstOrDefault(ball => ball.Carrier == slot);
            if (carried == null)
                return false;
            carried.Carrier = -1;
            carried.Team = Team(slot);
            carried.Cell = cell;
            return true;
        }

        internal void Return(int slot, IEnumerable<int> occupied)
        {
            var reserved = new HashSet<int>(occupied);
            foreach (var ball in balls.Where(item => item.Carrier == slot))
            {
                ball.Cell = FindCell(ball.Team, reserved);
                ball.Carrier = -1;
            }
        }

        private int FindCell(int team, HashSet<int> reserved)
        {
            var free = Enumerable.Range(0, Size * Size).Where(cell => Region(cell, Count) == team &&
                !reserved.Contains(cell) && !balls.Any(ball => ball.Cell == cell)).ToArray();
            return free[random.Next(free.Length)];
        }
    }
}
