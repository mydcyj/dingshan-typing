using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal sealed class PowerStation
    {
        public int Center { get; set; }
        public int Charge { get; set; }
        public int Required { get; set; }
        public bool Offline { get; set; }
        public bool Upcoming { get; set; }
    }

    internal sealed class SnakePower
    {
        internal const int Size = 48;
        internal const int StationSide = 5;
        internal const int Duration = 5 * 60 * 1000;
        internal const int RespawnDelay = 5000;
        internal const int OutageInterval = 45000;
        internal const int OutageDuration = 10000;
        internal const int OutageWarning = 5000;

        private static readonly int[] Spawns =
        {
            12 * Size + 12, 35 * Size + 35, 12 * Size + 35, 35 * Size + 12
        };

        private static readonly int[] Centers =
        {
            9 * Size + 9, 9 * Size + 38, 38 * Size + 38, 38 * Size + 9
        };

        private readonly int required;
        private readonly int[] charge = new int[Centers.Length];
        private readonly int[] contributions = new int[Spawns.Length];
        private readonly int[] outageOrder;

        internal HashSet<int> Obstacles { get; private set; }
        internal bool IsComplete { get { return charge.All(value => value == required); } }

        internal SnakePower(int playerCount, Random random)
        {
            required = playerCount + 2;
            outageOrder = Enumerable.Range(0, Centers.Length).OrderBy(index => random.Next()).ToArray();
            Obstacles = SnakeObstacles.Create(Size, Centers, Spawns, random);
        }

        internal int Spawn(int seat) { return Spawns[seat]; }

        internal int Contribution(int slot) { return contributions[slot]; }

        internal bool Charge(int slot, int head, long elapsed)
        {
            if (elapsed >= Duration)
                return false;
            int station = Array.FindIndex(Centers, center => Contains(center, head));
            if (station < 0 || charge[station] >= required || station == OfflineStation(elapsed))
                return false;
            charge[station]++;
            contributions[slot]++;
            return true;
        }

        internal PowerStation[] Snapshot(long elapsed)
        {
            int offline = OfflineStation(elapsed);
            int upcoming = UpcomingStation(elapsed);
            return Centers.Select((center, index) => new PowerStation
            {
                Center = center,
                Charge = charge[index],
                Required = required,
                Offline = index == offline,
                Upcoming = index == upcoming
            }).ToArray();
        }

        internal void Move(SnakeGame snake)
        {
            int next = snake.NextCell();
            if (snake.CanStep(next, false, Obstacles))
                snake.MoveTo(next, 0);
        }

        private static bool Contains(int center, int head)
        {
            int radius = StationSide / 2;
            return Math.Abs(center % Size - head % Size) <= radius &&
                Math.Abs(center / Size - head / Size) <= radius;
        }

        private int OfflineStation(long elapsed)
        {
            if (elapsed < OutageInterval || elapsed >= Duration || elapsed % OutageInterval >= OutageDuration)
                return -1;
            return outageOrder[(int)(elapsed / OutageInterval - 1) % outageOrder.Length];
        }

        private int UpcomingStation(long elapsed)
        {
            int until = OutageInterval - (int)(elapsed % OutageInterval);
            if (until > OutageWarning || elapsed + until >= Duration)
                return -1;
            return outageOrder[(int)(elapsed / OutageInterval) % outageOrder.Length];
        }
    }
}
