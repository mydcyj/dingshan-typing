using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnglishTypingPractice
{
    internal enum SnakeRole { Standard, Timewalker, Timid, Fearless, Hunter, Bard, Navigator, Collector, Stabilizer }

    internal static class SnakeWords
    {
        internal const int NavigatorLongTurns = 3;
        internal const int NavigatorShortLength = 4;
        internal const int NavigatorMinLength = 7;
        internal const int NavigatorMaxLength = 8;
        internal const int StabilizerMinLength = 9;
        internal const int StabilizerMaxLength = 11;

        private static readonly Dictionary<string, string> Meanings = LoadMeanings();
        private static readonly Dictionary<int, string[]> ByLength = Meanings.Keys.GroupBy(word => word.Length)
            .ToDictionary(group => group.Key, group => group.ToArray());
        private static readonly string[] ChargeWords = Meanings.Keys.Where(word => word.Length >= 11).ToArray();
        private static readonly string[] StabilizerWords = Meanings.Keys.Where(word =>
            word.Length >= StabilizerMinLength && word.Length <= StabilizerMaxLength).ToArray();
        private static readonly Dictionary<string, string> SkillPhrases = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            { "environmental impact", "环境影响" },
            { "cultural heritage", "文化遗产" },
            { "scientific research", "科学研究" },
            { "economic development", "经济发展" },
            { "renewable energy", "可再生能源" },
            { "sustainable future", "可持续发展的未来" },
            { "technological change", "技术变革" },
            { "effective learning", "有效学习" }
        };
        private static readonly Dictionary<string, string> SpokenPhrases = SkillPhrases.Keys
            .ToDictionary(phrase => phrase.Replace(" ", ""), phrase => phrase, StringComparer.OrdinalIgnoreCase);

        private static Dictionary<string, string> LoadMeanings()
        {
            var meanings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            foreach (string resource in new[] { "Cet4.tsv", "Cet6.tsv" })
            using (var reader = new StreamReader(typeof(SnakeWords).Assembly.GetManifestResourceStream(resource)))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int tab = line.IndexOf('\t');
                    string word = line.Substring(0, tab);
                    if (!meanings.ContainsKey(word))
                        meanings.Add(word, line.Substring(tab + 1));
                }
            }
            return meanings;
        }

        internal static string Meaning(string word)
        {
            string phrase;
            return SpokenPhrases.TryGetValue(word, out phrase) ? SkillPhrases[phrase] : Meanings[word];
        }

        internal static string SpokenText(string word)
        {
            string phrase;
            return SpokenPhrases.TryGetValue(word, out phrase) ? phrase : word;
        }

        internal static int Variants(SnakeRole role)
        {
            return role == SnakeRole.Timewalker ? 3 : 1;
        }

        internal static int NavigatorMinLengthFor(SnakeMode mode) { return mode == SnakeMode.Territory ? 6 : NavigatorMinLength; }
        internal static int NavigatorMaxLengthFor(SnakeMode mode) { return mode == SnakeMode.Territory ? 7 : NavigatorMaxLength; }
        internal static int NavigatorShortLengthFor(SnakeMode mode) { return mode == SnakeMode.Territory ? 5 : NavigatorShortLength; }

        internal static string[] Draw(Random random, SnakeRole role, string[] previous, int completedWords, SnakeMode mode)
        {
            int length;
            if (role == SnakeRole.Navigator)
                length = completedWords % (NavigatorLongTurns + 1) == NavigatorLongTurns ? NavigatorShortLengthFor(mode) :
                    random.Next(NavigatorMinLengthFor(mode), NavigatorMaxLengthFor(mode) + 1);
            else
                length = role == SnakeRole.Timid ? random.Next(5, 7) : role == SnakeRole.Timewalker ? 6 : 6 + random.Next(2);
            int variants = Variants(role);
            var words = new string[4 * variants];
            for (int variant = 0; variant < variants; variant++)
            {
                var available = ByLength[length + 2 * variant].Except(previous)
                    .Where(word => !words.Any(shorter => shorter != null && word.StartsWith(shorter, StringComparison.Ordinal)))
                    .ToList();
                for (int direction = 0; direction < 4; direction++)
                {
                    int index = random.Next(available.Count);
                    words[direction * variants + variant] = available[index];
                    available.RemoveAt(index);
                }
            }
            return words;
        }

        internal static bool HasSkill(SnakeRole role)
        {
            return role == SnakeRole.Fearless || role == SnakeRole.Hunter || role == SnakeRole.Bard || role == SnakeRole.Stabilizer;
        }

        internal static string DrawCharge(Random random, string previous, SnakeRole role)
        {
            var pool = role == SnakeRole.Fearless ? ChargeWords : role == SnakeRole.Stabilizer ? StabilizerWords :
                SpokenPhrases.Keys.Concat(ChargeWords.Where(word => word.Length >= 15 && word.Length <= 20));
            var choices = pool.Where(word => word != previous).ToArray();
            return choices[random.Next(choices.Length)];
        }

        internal static bool IsSkillWord(string word, SnakeRole role)
        {
            if (word == null || !HasSkill(role))
                return false;
            if (role == SnakeRole.Stabilizer)
                return word.Length >= StabilizerMinLength && word.Length <= StabilizerMaxLength && Contains(word);
            return role == SnakeRole.Fearless ? word.Length >= 11 && Contains(word) :
                word.Length >= 15 && word.Length <= 20 && (Contains(word) || SpokenPhrases.ContainsKey(word));
        }

        internal static bool Contains(string word)
        {
            return word != null && Meanings.ContainsKey(word);
        }

        internal static string RoleName(SnakeRole role)
        {
            switch (role)
            {
                case SnakeRole.Timewalker: return "时空者";
                case SnakeRole.Timid: return "胆怯者";
                case SnakeRole.Fearless: return "无畏者";
                case SnakeRole.Hunter: return "狩猎者";
                case SnakeRole.Bard: return "吟唱者";
                case SnakeRole.Navigator: return "领航者";
                case SnakeRole.Collector: return "采集者";
                case SnakeRole.Stabilizer: return "稳行者";
                default: return "平凡者";
            }
        }
    }
}
