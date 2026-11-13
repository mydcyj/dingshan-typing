using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace EnglishTypingPractice
{
    internal enum SnakeRole { Standard, Timewalker, Timid, Fearless }

    internal static class SnakeWords
    {
        private static readonly Dictionary<string, string> Meanings = LoadMeanings();
        private static readonly Dictionary<int, string[]> ByLength = Meanings.Keys.GroupBy(word => word.Length)
            .ToDictionary(group => group.Key, group => group.ToArray());
        private static readonly string[] ChargeWords = Meanings.Keys.Where(word => word.Length >= 11).ToArray();

        private static Dictionary<string, string> LoadMeanings()
        {
            var meanings = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StreamReader(typeof(SnakeWords).Assembly.GetManifestResourceStream("Cet4.tsv")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int tab = line.IndexOf('\t');
                    meanings.Add(line.Substring(0, tab), line.Substring(tab + 1));
                }
            }
            return meanings;
        }

        internal static string Meaning(string word)
        {
            return Meanings[word];
        }

        internal static int Variants(SnakeRole role)
        {
            return role == SnakeRole.Timewalker ? 3 : 1;
        }

        internal static string[] Draw(Random random, SnakeRole role, string[] previous)
        {
            int length = role == SnakeRole.Timid ? 3 : role == SnakeRole.Timewalker ? 6 : 6 + random.Next(2);
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

        internal static string DrawCharge(Random random, string previous)
        {
            var choices = ChargeWords.Where(word => word != previous).ToArray();
            return choices[random.Next(choices.Length)];
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
                default: return "普通";
            }
        }
    }
}
