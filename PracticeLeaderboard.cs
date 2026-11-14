using System;
using System.Collections.Generic;
using System.Linq;

namespace EnglishTypingPractice
{
    internal enum PracticeRankingOrder { Speed, Accuracy, Composite }

    internal sealed class PracticeRecord
    {
        public double Speed { get; set; }
        public double Accuracy { get; set; }
        public DateTime PlayedAt { get; set; }

        internal double Composite { get { return Speed * (Accuracy * Accuracy); } }
    }

    internal sealed class PracticeLeaderboard
    {
        private const int RankLimit = 3;
        private static readonly PracticeRankingOrder[] Orders =
            { PracticeRankingOrder.Speed, PracticeRankingOrder.Accuracy, PracticeRankingOrder.Composite };

        public Dictionary<string, List<PracticeRecord>> ByMode { get; set; }

        public PracticeLeaderboard()
        {
            ByMode = new Dictionary<string, List<PracticeRecord>>(StringComparer.Ordinal);
        }

        internal bool Add(string mode, double speed, double accuracy)
        {
            var records = RecordsFor(mode).ToList();
            var record = new PracticeRecord { Speed = speed, Accuracy = accuracy, PlayedAt = DateTime.UtcNow };
            bool newBest = records.Count == 0 || record.Composite > records.Max(previous => previous.Composite);
            records.Add(record);

            var kept = new HashSet<PracticeRecord>(Orders.SelectMany(order => Rank(records, order)));
            if (ByMode == null)
                ByMode = new Dictionary<string, List<PracticeRecord>>(StringComparer.Ordinal);
            ByMode[mode] = records.Where(kept.Contains).ToList();
            return newBest;
        }

        internal PracticeRecord[] Top(string mode, PracticeRankingOrder order)
        {
            return Rank(RecordsFor(mode), order).ToArray();
        }

        private IEnumerable<PracticeRecord> RecordsFor(string mode)
        {
            List<PracticeRecord> records;
            return ByMode != null && ByMode.TryGetValue(mode, out records) && records != null
                ? records.Where(record => record != null) : Enumerable.Empty<PracticeRecord>();
        }

        private static IEnumerable<PracticeRecord> Rank(IEnumerable<PracticeRecord> records, PracticeRankingOrder order)
        {
            if (order == PracticeRankingOrder.Speed)
                return records.OrderByDescending(record => record.Speed).ThenByDescending(record => record.Accuracy).Take(RankLimit);
            if (order == PracticeRankingOrder.Accuracy)
                return records.OrderByDescending(record => record.Accuracy).ThenByDescending(record => record.Speed).Take(RankLimit);
            return records.OrderByDescending(record => record.Composite).ThenByDescending(record => record.Accuracy)
                .ThenByDescending(record => record.Speed).Take(RankLimit);
        }
    }
}
