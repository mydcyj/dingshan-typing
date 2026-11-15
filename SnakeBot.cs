using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal enum SnakeBotDifficulty { Normal, Easy, Hard, Hell }

    internal sealed class SnakeBotSession
    {
        private readonly SortedDictionary<int, SnakeBot> bots = new SortedDictionary<int, SnakeBot>();
        private SnakeMatch currentMatch;
        private long nextStep;
        private int round = -1;

        internal SnakeBotDifficulty Difficulty { get; set; }
        internal IEnumerable<int> Slots { get { return bots.Keys; } }

        internal int SetCount(SnakeMatch match, int count)
        {
            if (match.Phase == MatchPhase.Playing)
                return bots.Count;
            if (currentMatch != match)
            {
                bots.Clear();
                round = -1;
                currentMatch = match;
            }
            count = Math.Max(0, Math.Min(3, count));
            while (bots.Count > count)
            {
                int slot = bots.Keys.Last();
                match.RemovePlayer(slot);
                bots.Remove(slot);
            }
            while (bots.Count < count)
            {
                int slot = match.AddPlayer("人机 " + (bots.Count + 1));
                if (slot < 0)
                    break;
                bots.Add(slot, new SnakeBot(slot));
            }
            return bots.Count;
        }

        internal void Step(SnakeMatch match, long elapsed)
        {
            if (bots.Count == 0 || match.Phase != MatchPhase.Playing)
                return;
            if (round != match.Round)
            {
                round = match.Round;
                nextStep = 0;
            }
            if (elapsed < nextStep)
                return;
            nextStep = elapsed + (Difficulty == SnakeBotDifficulty.Hell ? 30 : 60);
            foreach (var bot in bots.Values)
            {
                if (match.Phase != MatchPhase.Playing)
                    break;
                bot.Step(match, Difficulty, elapsed);
            }
        }
    }

    internal sealed class SnakeBot
    {
        private struct ProjectedHead
        {
            internal int Cell;
            internal double Arrival;
            internal bool FirstStep;
        }

        private const int Far = 10000;
        private readonly int slot;
        private readonly Random random;
        private readonly HashSet<int> dangerous = new HashSet<int>();
        private readonly HashSet<int> pulseCells = new HashSet<int>();
        private readonly List<ProjectedHead> projectedHeads = new List<ProjectedHead>(6);
        private int[] distance, queue;
        private bool[] blocked;
        private int round = -1, respawns = -1, size, start, end;
        private int lastHead = -1, lastDirection = -1, legSteps, turnSign = 1;
        private int territoryLength;
        private int cachedTarget = -2, cachedRadius = -1;
        private int nextGroup, nextHeading, nextExtra, typedLength;
        private long ready, nextPlan;
        private string word;
        private bool skill, handoff, mistype, sprint;

        internal SnakeBot(int slot)
        {
            this.slot = slot;
            random = new Random(unchecked(Environment.TickCount * 31 + slot * 9973));
        }

        internal void Step(SnakeMatch match, SnakeBotDifficulty difficulty, long now)
        {
            var snake = match.GetSnake(slot);
            if (snake == null)
                return;
            var state = match.Snapshot("人机", slot);
            if (round != state.Round)
            {
                round = state.Round;
                size = state.Size;
                distance = new int[size * size];
                queue = new int[distance.Length];
                blocked = new bool[distance.Length];
                foreach (int cell in state.Obstacles)
                    blocked[cell] = true;
                cachedTarget = -2;
                respawns = -1;
                lastHead = lastDirection = -1;
                legSteps = 0;
                turnSign = slot % 2 == 0 ? 1 : 3;
                nextPlan = now;
                word = null;
                sprint = false;
            }
            if (!string.IsNullOrEmpty(state.RelayHandoffWord))
            {
                if (now < nextPlan)
                    return;
                if (word == null || !handoff || nextGroup != state.WordGroup)
                    Begin(state.RelayHandoffWord, state.WordGroup, false, true, difficulty, now);
                if (now >= ready)
                {
                    Submit(match, state, difficulty, now);
                    Preview(match, snake, now, "");
                }
                else
                    ShowTyping(match, snake, difficulty, now);
                return;
            }
            if (!snake.IsRunning || snake.IsStunned(now))
            {
                word = null;
                return;
            }
            if (snake.Respawns != respawns || handoff)
            {
                respawns = snake.Respawns;
                word = null;
                handoff = false;
                lastHead = lastDirection = -1;
                nextPlan = now;
                legSteps = 0;
            }
            int head = snake.Body.First.Value, heading = (int)snake.Direction;
            bool hell = difficulty == SnakeBotDifficulty.Hell;
            if (hell && lastDirection != heading)
                legSteps = 0;
            else if (lastHead != head)
                legSteps = lastDirection == heading && lastHead >= 0 ? legSteps + Manhattan(lastHead, head) : 0;
            lastDirection = heading;
            lastHead = head;
            SetDanger(state, snake);
            if (hell)
                PredictHeads(match, state, snake, now);
            var own = state.Players.First(player => player.Slot == slot);
            if (state.Mode == SnakeMode.Teams)
            {
                if (own.Carrying > 0 && SnakeTeams.Region(head, state.TeamCount) == own.Team)
                    match.DropBall(slot, round, now);
                bool fast = (difficulty == SnakeBotDifficulty.Hard || hell) && own.Carrying == 0 && FreeRun(head, heading, 16) >= 14;
                if (fast != sprint)
                {
                    sprint = fast;
                    match.SetSprint(slot, round, sprint, now);
                }
            }
            if (word != null)
            {
                if (now < ready)
                {
                    ShowTyping(match, snake, difficulty, now);
                    return;
                }
                if (state.Mode == SnakeMode.Territory && !skill && legSteps < territoryLength &&
                    FreeRun(head, heading, 2) > 1)
                    return;
                if (skill || (FreeRun(head, nextHeading, nextExtra + 1) > nextExtra &&
                    (!hell || HeadRisk(head, nextHeading, nextExtra + 1, NextMove(snake, now), 0) == 0)))
                    Submit(match, state, difficulty, now);
                word = null;
                Preview(match, snake, now, "");
            }
            if (snake.TurnPending || !snake.IsRunning || now < nextPlan)
                return;
            nextPlan = now + (hell ? 30 : 120);

            int radius;
            int target = Target(state, own, head, out radius);
            if (target != cachedTarget || radius != cachedRadius)
                BuildDistances(target, radius);
            int variants = SnakeWords.Variants(snake.Role);
            int boost = SnakeGame.UsesTimewalkerBalance(state.Mode) ? 1 : 2;
            int reaction = Reaction(difficulty);
            double interval = snake.Interval(now);
            int untilMove = NextMove(snake, now);
            int lead = MovesBefore(reaction + snake.Words[((heading + 1) % 4) * variants].Length * LetterTime(difficulty), interval, untilMove);
            int room = FreeRun(head, heading, size);
            bool territorial = state.Mode == SnakeMode.Territory;
            int leg = difficulty == SnakeBotDifficulty.Easy ? 12 : difficulty == SnakeBotDifficulty.Normal ? 16 : hell ? 22 : 20;
            bool checkpoint = state.Mode == SnakeMode.Race || state.Mode == SnakeMode.Relay;
            int sight = hell && !territorial ? Math.Max(lead + 1, 4) : lead + 1;
            if (hell && checkpoint && LineValue(head, heading, Math.Min(room, sight), state.Mode) == 50)
                return;
            double straightRisk = hell ? HeadRisk(head, heading, Math.Min(room, lead + 2), untilMove, interval) : 0;
            if (territorial && legSteps + lead < leg && room > lead + 2 && straightRisk == 0)
                return;
            double best = LineValue(head, heading, Math.Min(room, sight), state.Mode) + .7;
            if (state.Mode == SnakeMode.Pulse && pulseCells.Contains(head))
                best += pulseCells.Contains(Advance(head, heading, Math.Min(room, sight))) ? -60 : 30;
            if (hell && !territorial)
                best += ContinuationValue(head, heading, room, 0, lead, state.Mode) - straightRisk;
            else
                best -= straightRisk;
            if (room <= lead + 1)
                best -= 90;
            if (territorial && legSteps + lead >= leg)
                best -= 15;
            int chosen = -1;
            for (int i = 0; i < snake.Words.Length; i++)
            {
                int direction = i / variants, variant = i % variants;
                if (direction == (heading + 2) % 4 || direction == heading && (variant != 2 || territorial))
                    continue;
                int delay = reaction + snake.Words[i].Length * LetterTime(difficulty);
                int advance = MovesBefore(delay, interval, untilMove);
                if (advance > room)
                    continue;
                int pivot = Advance(head, heading, advance);
                int extra = variant * boost;
                int open = FreeRun(pivot, direction, size);
                if (open <= extra)
                    continue;
                double value = LineValue(pivot, direction, Math.Min(open, (hell && !territorial ? sight : Math.Max(lead, 3)) + extra), state.Mode);
                if (state.Mode == SnakeMode.Pulse && pulseCells.Contains(head))
                    value += pulseCells.Contains(Advance(pivot, direction, Math.Min(open, sight + extra))) ? -60 : 30;
                if (hell)
                {
                    int turnAt = untilMove + (int)(advance * interval);
                    value -= HeadRisk(pivot, direction, extra + 1, turnAt, 0);
                    if (!territorial)
                    {
                        value += ContinuationValue(pivot, direction, open, extra, lead, state.Mode);
                        value -= HeadRisk(head, heading, advance, untilMove, interval);
                    }
                }
                if (state.Mode == SnakeMode.Power && direction != heading && distance[pivot] == 0)
                    value += 80;
                value -= advance * .20 + delay * .00030 + variant * .45;
                if (open <= lead + extra + 1)
                    value -= 9;
                if (territorial)
                {
                    value += Math.Min(open, leg) * .6;
                    if (direction == (heading + turnSign) % 4)
                        value += open >= leg ? 35 : 5;
                    int projected = pivot;
                    for (int step = 1; step <= Math.Min(open, leg); step++)
                    {
                        projected = Adjacent(projected, direction);
                        if (step >= 4 && (state.Trails[projected] & (1 << slot)) != 0)
                        {
                            value += hell ? 30 : 8;
                            break;
                        }
                    }
                }
                if (value > best)
                {
                    best = value;
                    chosen = i;
                }
            }
            if (straightRisk == 0 && CanUseSkill(snake, state, own, difficulty, now, head, heading, room, lead, untilMove, interval))
                Begin(snake.ChargeWord, snake.ChargeGroup, true, false, difficulty, now);
            else if (chosen >= 0)
            {
                nextHeading = chosen / variants;
                nextExtra = chosen % variants * boost;
                if (territorial)
                {
                    turnSign = (nextHeading - heading + 4) % 4;
                    territoryLength = leg;
                }
                Begin(snake.Words[chosen], snake.WordGroup, false, false, difficulty, now);
            }
        }

        private void Begin(string value, int group, bool isSkill, bool isHandoff, SnakeBotDifficulty difficulty, long now)
        {
            word = value;
            nextGroup = group;
            skill = isSkill;
            handoff = isHandoff;
            typedLength = 0;
            ready = now + Reaction(difficulty) + word.Length * LetterTime(difficulty);
            mistype = random.Next(100) < (difficulty == SnakeBotDifficulty.Easy ? 8 : difficulty == SnakeBotDifficulty.Normal ? 2 : 0);
        }

        private void Submit(SnakeMatch match, SnakeMatchState state, SnakeBotDifficulty difficulty, long now)
        {
            string attempt = mistype ? word.Substring(0, word.Length - 1) + "!" : word;
            if (skill)
                match.TryCharge(slot, attempt, nextGroup, state.Round, now);
            else
                match.TryWord(slot, attempt, nextGroup, state.Round, now);
            word = null;
            nextPlan = now + (mistype ? 2 * LetterTime(difficulty) : difficulty == SnakeBotDifficulty.Hell ? 30 : 100);
        }

        private void Preview(SnakeMatch match, SnakeGame snake, long now, string text)
        {
            match.SetPreview(slot, new SnakeTypingPreview { Round = round, Respawns = snake.Respawns, Skill = skill, Text = text }, now);
        }

        private void ShowTyping(SnakeMatch match, SnakeGame snake, SnakeBotDifficulty difficulty, long now)
        {
            int letters = Math.Max(0, word.Length - (int)Math.Ceiling((ready - now) / (double)LetterTime(difficulty)));
            if (letters == typedLength)
                return;
            typedLength = letters;
            Preview(match, snake, now, word.Substring(0, letters));
        }

        private int Target(SnakeMatchState state, SnakePlayerState own, int head, out int radius)
        {
            radius = 0;
            switch (state.Mode)
            {
                case SnakeMode.Race: radius = SnakeRace.CheckpointRadius; return state.RaceTarget;
                case SnakeMode.Relay: radius = SnakeRelay.CheckpointRadius; return state.RelayTarget;
                case SnakeMode.Hotspot: radius = SnakeHotspot.Radius; return state.HotspotTarget;
                case SnakeMode.Power:
                    radius = SnakePower.StationSide / 2;
                    var station = state.PowerStations.Where(item => !item.Offline && item.Charge < item.Required)
                        .OrderBy(item => Manhattan(head, item.Center) + (item.Upcoming ? 5 : 0)).FirstOrDefault();
                    return station == null ? -1 : station.Center;
                case SnakeMode.Teams:
                    if (own.Carrying > 0)
                    {
                        radius = 3;
                        return SnakeTeams.Spawn(own.Team, state.TeamCount, slot % 2 != 0);
                    }
                    var ball = state.TeamBalls.Where(item => item.Carrier < 0 && item.Team != own.Team)
                        .OrderBy(item => Manhattan(head, item.Cell)).FirstOrDefault();
                    return ball == null ? -1 : ball.Cell;
                case SnakeMode.Territory: return -1;
                case SnakeMode.Pulse: return PulseEscapeTarget(head);
                default:
                    int nearest = -1, gap = int.MaxValue;
                    foreach (int fruit in state.SmallFruits.Concat(state.LargeFruit < 0 ? new int[0] :
                        SnakeGame.LargeFruitCells(state.LargeFruit, size)))
                    {
                        int steps = Manhattan(head, fruit);
                        if (steps < gap)
                        {
                            gap = steps;
                            nearest = fruit;
                        }
                    }
                    return nearest;
            }
        }

        private int PulseEscapeTarget(int head)
        {
            if (!pulseCells.Contains(head))
                return -1;
            int target = -1, nearest = int.MaxValue;
            for (int y = 3; y < size - 3; y++)
                for (int x = 3; x < size - 3; x++)
                {
                    int cell = y * size + x;
                    if (pulseCells.Contains(cell) || pulseCells.Contains(cell - 1) || pulseCells.Contains(cell + 1) ||
                        pulseCells.Contains(cell - size) || pulseCells.Contains(cell + size) || dangerous.Contains(cell))
                        continue;
                    int gap = Manhattan(head, cell);
                    if (gap < nearest)
                    {
                        nearest = gap;
                        target = cell;
                    }
                }
            return target;
        }

        private bool CanUseSkill(SnakeGame snake, SnakeMatchState state, SnakePlayerState own, SnakeBotDifficulty difficulty,
            long now, int head, int heading, int room, int lead, int untilMove, double interval)
        {
            if (difficulty == SnakeBotDifficulty.Easy || snake.ChargeWord == null || snake.IsCharging(now) ||
                snake.IsStabilizing(now) || snake.StabilizerCooldownRemaining(now) > 0)
                return false;
            int travel = MovesBefore(Reaction(difficulty) + snake.ChargeWord.Length * LetterTime(difficulty), interval, untilMove);
            if (room <= travel + lead + 1)
                return false;
            int pivot = Advance(head, heading, travel);
            if (snake.Role == SnakeRole.Fearless)
                return room > travel + lead + 11 && (state.Mode == SnakeMode.Race || state.Mode == SnakeMode.Relay) &&
                    LineValue(pivot, heading, 8, state.Mode) > -3;
            if (snake.Role == SnakeRole.Stabilizer)
                return (state.Mode == SnakeMode.Power || state.Mode == SnakeMode.Hotspot) && distance[pivot] == 0;
            foreach (var rival in state.Players)
            {
                if (rival.Slot == slot || rival.Body.Length == 0 || (own.Team >= 0 && rival.Team == own.Team))
                    continue;
                int target = rival.Body[0];
                if (snake.Role == SnakeRole.Bard && Math.Abs(target % size - pivot % size) <= 2 &&
                    Math.Abs(target / size - pivot / size) <= 2)
                    return true;
                if (snake.Role == SnakeRole.Hunter)
                    for (int i = 1, cell = pivot; i <= 8; i++)
                    {
                        cell = Adjacent(cell, heading);
                        if (cell < 0 || blocked[cell])
                            break;
                        if (rival.Body.Contains(cell))
                            return true;
                    }
            }
            return false;
        }

        private void SetDanger(SnakeMatchState state, SnakeGame snake)
        {
            if (start != state.SafeStart || end != state.SafeEnd)
                cachedTarget = -2;
            start = state.SafeStart;
            end = state.SafeEnd;
            dangerous.Clear();
            pulseCells.Clear();
            if (state.Mode == SnakeMode.Pulse)
            {
                pulseCells.UnionWith(state.PulseCells);
                // A head already on a warning line may travel along it while typing an escape turn.
                // Once clear, plan around both warning and active lines before they become lethal.
                if (state.PulsePhase == 2 || !pulseCells.Contains(snake.Body.First.Value))
                    dangerous.UnionWith(pulseCells);
            }
            if (state.Mode == SnakeMode.Territory)
                return;
            foreach (int cell in snake.Body)
                if (cell != snake.Body.First.Value && cell != snake.Body.Last.Value)
                    dangerous.Add(cell);
            if (state.Mode != SnakeMode.Power && state.Mode != SnakeMode.Race && state.Mode != SnakeMode.Relay &&
                state.Mode != SnakeMode.Pulse)
                foreach (var player in state.Players)
                    if (player.Slot != slot && player.Result == SnakeResult.Running)
                        foreach (int cell in player.Body)
                            dangerous.Add(cell);
            foreach (int shot in state.Shots)
                dangerous.Add(shot);
        }

        private void PredictHeads(SnakeMatch match, SnakeMatchState state, SnakeGame own, long now)
        {
            projectedHeads.Clear();
            if (state.Mode == SnakeMode.Power || state.Mode == SnakeMode.Race || state.Mode == SnakeMode.Relay ||
                state.Mode == SnakeMode.Pulse ||
                own.IsCharging(now))
                return;
            foreach (var player in state.Players)
            {
                var snake = match.GetSnake(player.Slot);
                if (snake == null || snake == own || !snake.IsRunning || snake.IsStunned(now))
                    continue;
                int cell = snake.Body.First.Value;
                int heading = (int)snake.Direction;
                double arrival = NextMove(snake, now);
                for (int step = 0; step < 2; step++)
                {
                    cell = Adjacent(cell, heading);
                    if (!Inside(cell) || blocked[cell])
                        break;
                    projectedHeads.Add(new ProjectedHead
                    {
                        Cell = cell,
                        Arrival = arrival,
                        FirstStep = step == 0
                    });
                    if (step >= snake.ExtraMoves)
                        arrival += snake.Interval(now);
                }
            }
        }

        private double HeadRisk(int cell, int heading, int length, double arrival, double interval)
        {
            for (int i = 0; i < length; i++)
            {
                cell = Adjacent(cell, heading);
                foreach (var head in projectedHeads)
                    if (head.Cell == cell && Math.Abs(head.Arrival - arrival) <= (head.FirstStep ? 55 : 75))
                        return head.FirstStep ? 40 : 20;
                arrival += interval;
            }
            return 0;
        }

        private double ContinuationValue(int cell, int heading, int open, int extra, int lead, SnakeMode mode)
        {
            double best = -12;
            for (int step = 1; step <= Math.Min(open, extra + lead + 4); step++)
            {
                cell = Adjacent(cell, heading);
                if (step <= extra)
                    continue;
                for (int side = 1; side < 4; side += 2)
                {
                    int direction = (heading + side) % 4;
                    int run = FreeRun(cell, direction, 4);
                    if (run == 0)
                        continue;
                    double value = LineValue(cell, direction, run, mode) - step * .2;
                    if (cachedTarget >= 0)
                        value -= Math.Min(50, LineValue(cell, heading, 0, mode));
                    best = Math.Max(best, value);
                }
            }
            return best * .5;
        }

        private void BuildDistances(int target, int radius)
        {
            cachedTarget = target;
            cachedRadius = radius;
            for (int i = 0; i < distance.Length; i++)
                distance[i] = Far;
            if (target < 0)
                return;
            int cx = target % size, cy = target / size;
            int count = 0;
            for (int y = Math.Max(start, cy - radius); y < end && y <= cy + radius; y++)
                for (int x = Math.Max(start, cx - radius); x < end && x <= cx + radius; x++)
                {
                    int cell = y * size + x;
                    if (!blocked[cell])
                    {
                        distance[cell] = 0;
                        queue[count++] = cell;
                    }
                }
            for (int i = 0; i < count; i++)
                for (int direction = 0; direction < 4; direction++)
                {
                    int next = Adjacent(queue[i], direction);
                    if (Inside(next) && !blocked[next] && distance[next] == Far)
                    {
                        distance[next] = distance[queue[i]] + 1;
                        queue[count++] = next;
                    }
                }
        }

        private double LineValue(int cell, int direction, int length, SnakeMode mode)
        {
            if (cachedTarget < 0)
                return Math.Min(9, length) * .65;
            int best = distance[cell];
            for (int i = 0; i < length; i++)
            {
                cell = Adjacent(cell, direction);
                best = Math.Min(best, distance[cell]);
            }
            if (best == 0 && (mode == SnakeMode.Race || mode == SnakeMode.Relay))
                return 50;
            return -(.7 * best + .3 * distance[cell]);
        }

        private bool Inside(int cell)
        {
            return cell >= 0 && cell % size >= start && cell % size < end && cell / size >= start && cell / size < end;
        }

        private int Manhattan(int first, int second)
        {
            return Math.Abs(first % size - second % size) + Math.Abs(first / size - second / size);
        }

        private int Adjacent(int cell, int direction) { return SnakeGame.AdjacentCell(cell, (SnakeDirection)direction, size); }

        private int Advance(int cell, int direction, int count)
        {
            while (count-- > 0)
                cell = Adjacent(cell, direction);
            return cell;
        }

        private int FreeRun(int cell, int direction, int limit)
        {
            int steps = 0;
            for (; steps < limit; steps++)
            {
                cell = Adjacent(cell, direction);
                if (!Inside(cell) || blocked[cell] || dangerous.Contains(cell))
                    break;
            }
            return steps;
        }

        private static int MovesBefore(int delay, double interval, int nextMove)
        {
            return delay < nextMove ? 0 : 1 + (int)((delay - nextMove) / interval);
        }

        private static int NextMove(SnakeGame snake, long now)
        {
            int low = 1, high = 4000;
            while (low < high)
            {
                int middle = (low + high) / 2;
                if (snake.MoveDue(now + middle)) high = middle;
                else low = middle + 1;
            }
            return low;
        }

        private int Reaction(SnakeBotDifficulty difficulty)
        {
            if (difficulty == SnakeBotDifficulty.Hell)
                return 15;
            return (difficulty == SnakeBotDifficulty.Easy ? 280 : difficulty == SnakeBotDifficulty.Hard ? 80 : 150) + random.Next(55);
        }

        private static int LetterTime(SnakeBotDifficulty difficulty)
        {
            if (difficulty == SnakeBotDifficulty.Hell)
                return 30;
            return difficulty == SnakeBotDifficulty.Easy ? 300 : difficulty == SnakeBotDifficulty.Hard ? 120 : 180;
        }
    }
}
