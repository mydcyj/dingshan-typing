using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal sealed class SnakeRelayTeamState
    {
        public int Legs { get; set; }
        public int TimeMs { get; set; }
        public int PenaltyMs { get; set; }
        public int FinishMs { get; set; }
        public int RunnerSlot { get; set; }
        public int WaitingSlot { get; set; }
    }

    internal sealed class SnakeRelay
    {
        internal const int Size = 48;
        internal const int Duration = 240000;
        internal const int RespawnDelay = 5000;
        internal const int CheckpointRadius = 1;
        internal const int LegCount = 4;

        private sealed class TeamEntry
        {
            internal int Legs, LastGate, Penalty;
            internal int Finish = -1;
            internal string Handoff;
        }

        private readonly int[] slots;
        private readonly int[] teamBySlot = new int[4];
        private readonly int[] gates;
        private readonly SnakeDirection[] headings;
        private readonly TeamEntry[] teams = { new TeamEntry(), new TeamEntry() };
        private readonly string[] handoffWords;

        internal HashSet<int> Obstacles { get; private set; }

        internal SnakeRelay(int[] slots, int courseSeed, Random teamsRandom)
        {
            this.slots = slots;
            for (int i = slots.Length - 1; i > 0; i--)
            {
                int other = teamsRandom.Next(i + 1);
                int old = slots[i];
                slots[i] = slots[other];
                slots[other] = old;
            }
            for (int i = 0; i < slots.Length; i++)
                teamBySlot[slots[i]] = i / 2;

            var course = new Random(courseSeed);
            var words = new Random(courseSeed);
            handoffWords = Enumerable.Range(0, LegCount - 1).Select(i =>
                SnakeWords.Draw(words, SnakeRole.Standard, new string[0], 0, SnakeMode.Relay)[0]).ToArray();
            bool mirror = course.Next(2) != 0;
            int turns = course.Next(4);
            int top = course.Next(8, 11), left = course.Next(8, 11), right = course.Next(36, 40), bottom = course.Next(36, 40);
            gates = new[] { At(left, top + 7), At(right - 7, top), At(right, bottom - 7),
                At(left + 7, bottom), At(left, top + 7) }.Select(cell => Transform(cell, turns, mirror)).ToArray();
            var bends = new[] { At(left, top), At(right, top), At(right, bottom), At(left, bottom) }
                .Select(cell => Transform(cell, turns, mirror)).ToArray();
            headings = Enumerable.Range(0, LegCount).Select(i => Heading(gates[i], bends[i])).ToArray();
            var protectedCells = new List<int>();
            for (int leg = 0; leg < LegCount; leg++)
            {
                AddSegment(protectedCells, gates[leg], bends[leg]);
                AddSegment(protectedCells, bends[leg], gates[leg + 1]);
            }
            Obstacles = SnakeObstacles.Create(Size, protectedCells, gates.Take(LegCount), course);
        }

        internal int Team(int slot) { return teamBySlot[slot]; }
        internal int Legs(int team) { return teams[team].Legs; }
        internal bool Finished { get { return teams.Any(team => team.Finish >= 0); } }

        internal void Start(int slot, SnakeGame snake, SnakeRole role)
        {
            snake.Start(Size, gates[0] % Size, gates[0] / Size, headings[0], role, SnakeMode.Relay);
            if (slots[Team(slot) * 2] != slot)
                snake.End(SnakeResult.Ready);
        }

        internal int Target(int slot)
        {
            int legs = teams[Team(slot)].Legs;
            return legs >= LegCount ? -1 : gates[legs + 1];
        }

        internal string HandoffWord(int slot)
        {
            int team = Team(slot);
            return WaitingSlot(team) == slot ? teams[team].Handoff : null;
        }

        internal int HandoffGroup(int slot) { return int.MaxValue - teams[Team(slot)].Legs; }

        internal bool TryHandoff(int slot, SnakeGame snake, string word, int group, long elapsed)
        {
            string expected = HandoffWord(slot);
            if (expected == null || group != HandoffGroup(slot) || !string.Equals(word, expected, StringComparison.OrdinalIgnoreCase))
                return false;
            var team = teams[Team(slot)];
            team.Handoff = null;
            snake.BeginRelayLeg(elapsed, gates[team.Legs], headings[team.Legs], expected);
            return true;
        }

        internal bool Move(int slot, SnakeGame snake, long elapsed)
        {
            var team = teams[Team(slot)];
            int next = snake.NextCell();
            if (!snake.CanStep(next, false, Obstacles))
            {
                team.Penalty += RespawnDelay;
                return false;
            }
            snake.MoveTo(next, 0);
            int target = gates[team.Legs + 1];
            if (Math.Abs(next % Size - target % Size) > CheckpointRadius ||
                Math.Abs(next / Size - target / Size) > CheckpointRadius)
                return true;
            team.LastGate = (int)elapsed;
            if (++team.Legs == LegCount)
            {
                team.Finish = (int)elapsed;
                snake.End(SnakeResult.Won);
            }
            else
            {
                team.Handoff = handoffWords[team.Legs - 1];
                snake.End(SnakeResult.Ready);
            }
            return true;
        }

        internal void Respawn(int slot, SnakeGame snake, long now)
        {
            int leg = teams[Team(slot)].Legs;
            snake.RespawnAt(now, gates[leg], headings[leg]);
        }

        internal SnakeRelayTeamState[] Snapshot(long now)
        {
            return teams.Select((team, i) => new SnakeRelayTeamState
            {
                Legs = team.Legs, TimeMs = team.Finish >= 0 ? team.Finish : (int)now,
                PenaltyMs = team.Penalty, FinishMs = team.Finish, RunnerSlot = RunnerSlot(i), WaitingSlot = WaitingSlot(i)
            }).ToArray();
        }

        internal int Winner()
        {
            if (teams[0].Legs != teams[1].Legs)
                return teams[0].Legs > teams[1].Legs ? 0 : 1;
            return teams[0].LastGate == teams[1].LastGate ? -1 : teams[0].LastGate < teams[1].LastGate ? 0 : 1;
        }

        private int RunnerSlot(int team)
        {
            var entry = teams[team];
            return entry.Handoff != null || entry.Legs >= LegCount ? -1 : slots[team * 2 + entry.Legs % 2];
        }

        private int WaitingSlot(int team)
        {
            var entry = teams[team];
            return entry.Handoff == null ? -1 : slots[team * 2 + entry.Legs % 2];
        }

        private static int At(int x, int y) { return y * Size + x; }

        private static int Transform(int cell, int rotation, bool mirror)
        {
            int x = cell % Size, y = cell / Size;
            if (mirror)
                x = Size - 1 - x;
            for (int i = 0; i < rotation; i++)
            {
                int prior = x;
                x = Size - 1 - y;
                y = prior;
            }
            return At(x, y);
        }

        private static SnakeDirection Heading(int start, int end)
        {
            return start / Size == end / Size ? (end > start ? SnakeDirection.East : SnakeDirection.West) :
                (end > start ? SnakeDirection.South : SnakeDirection.North);
        }

        private static void AddSegment(List<int> protectedCells, int start, int end)
        {
            var direction = Heading(start, end);
            for (int cell = start; cell != end; cell = SnakeGame.AdjacentCell(cell, direction, Size))
                protectedCells.Add(cell);
            protectedCells.Add(end);
        }
    }
}
