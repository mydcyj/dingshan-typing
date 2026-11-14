using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;

namespace EnglishTypingPractice
{
    internal sealed class TypingRound
    {
        private readonly string target;
        private readonly bool matchCase;
        private readonly bool code;
        private readonly Dictionary<char, int> missed = new Dictionary<char, int>();
        private readonly Match[] words;
        private readonly Dictionary<int, double> wordStarts = new Dictionary<int, double>();
        private readonly Dictionary<int, double> wordTimes = new Dictionary<int, double>();
        private readonly HashSet<int> indent = new HashSet<int>();
        private string previous = "";
        private int attempts, correct, symbolErrors, indentErrors;

        internal TypingRound(string target, bool matchCase, bool code, bool chinese)
        {
            this.target = target;
            this.matchCase = matchCase;
            this.code = code;
            words = chinese ? new Match[0] : Regex.Matches(target, code ? @"[A-Za-z_][A-Za-z_0-9]*" : @"[A-Za-z]+(?:['’-][A-Za-z]+)*")
                .Cast<Match>().ToArray();
            if (code)
                foreach (Match line in Regex.Matches(target, @"(?m)^[ \t]+"))
                    for (int i = line.Index; i < line.Index + line.Length; i++)
                        indent.Add(i);
        }

        internal double AccuracyRatio { get { return attempts == 0 ? 0 : (double)correct / attempts; } }
        internal string Accuracy { get { return attempts == 0 ? "—" : (100 * AccuracyRatio).ToString("0.#") + "%"; } }

        internal bool Observe(string input, double seconds)
        {
            if (input == previous)
                return false;
            int start = 0;
            int common = Math.Min(input.Length, previous.Length);
            while (start < common && input[start] == previous[start])
                start++;
            int end = input.Length;
            int oldEnd = previous.Length;
            while (end > start && oldEnd > start && input[end - 1] == previous[oldEnd - 1])
            {
                end--;
                oldEnd--;
            }
            for (int i = start; i < end && i < target.Length; i++)
            {
                char expected = target[i];
                char typed = input[i];
                attempts++;
                if (matchCase ? expected == typed : char.ToUpperInvariant(expected) == char.ToUpperInvariant(typed))
                    correct++;
                else
                {
                    int count;
                    missed.TryGetValue(expected, out count);
                    missed[expected] = count + 1;
                    if (indent.Contains(i))
                        indentErrors++;
                    else if (code && (char.IsPunctuation(expected) || char.IsSymbol(expected) ||
                        char.IsPunctuation(typed) || char.IsSymbol(typed)))
                        symbolErrors++;
                }
            }
            foreach (var word in words)
            {
                if (word.Index >= end)
                    break;
                if (word.Index + word.Length <= start || wordTimes.ContainsKey(word.Index))
                    continue;
                double begin;
                if (!wordStarts.TryGetValue(word.Index, out begin))
                    wordStarts.Add(word.Index, begin = seconds);
                var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                if (input.Length >= word.Index + word.Length &&
                    string.Compare(input, word.Index, target, word.Index, word.Length, comparison) == 0)
                    wordTimes.Add(word.Index, seconds - begin);
            }
            previous = input;
            return true;
        }

        internal string Summary()
        {
            var errors = missed.OrderByDescending(item => item.Value).ThenBy(item => item.Key).Take(5)
                .Select(item => FormatCharacter(item.Key) + " ×" + item.Value);
            var slow = words.Where(word => wordTimes.ContainsKey(word.Index) && wordTimes[word.Index] > 0)
                .GroupBy(word => word.Value, matchCase ? StringComparer.Ordinal : StringComparer.OrdinalIgnoreCase)
                .Select(group => new { Word = group.Key, Seconds = group.Max(word => wordTimes[word.Index]) })
                .OrderByDescending(item => item.Seconds).Take(3)
                .Select(item => item.Word + "（" + item.Seconds.ToString("0.0") + " 秒）");
            var lines = new List<string> { missed.Count == 0 ? "本轮没有错字" : "常错字符　" + string.Join("、", errors) };
            if (slow.Any())
                lines.Add("耗时最多的词　" + string.Join("、", slow));
            if (code)
                lines.Add("符号错误 " + symbolErrors + " 次　行首缩进错误 " + indentErrors + " 次");
            return string.Join("\n", lines);
        }

        private static string FormatCharacter(char character)
        {
            switch (character)
            {
                case ' ': return "空格";
                case '\t': return "Tab";
                case '\n': return "换行";
                default: return character.ToString();
            }
        }

        internal static string[] CodeExercises(IEnumerable<string> passages, int category)
        {
            if (category == 0)
                return passages.ToArray();
            return passages.SelectMany(passage => passage.Split('\n')).Where(line =>
                category == 1 ? line.IndexOfAny(new[] { '(', ')', '[', ']', '{', '}' }) >= 0 :
                category == 2 ? line.IndexOfAny(new[] { '\'', '"' }) >= 0 :
                    line.Length > 0 && (line[0] == ' ' || line[0] == '\t'))
                .Select(line => category == 3 ? line : line.Trim()).Distinct().ToArray();
        }

        internal static int EditDistance(string first, string second)
        {
            int[] distances = Enumerable.Range(0, second.Length + 1).ToArray();
            for (int i = 0; i < first.Length; i++)
            {
                int diagonal = distances[0];
                distances[0] = i + 1;
                for (int j = 0; j < second.Length; j++)
                {
                    int above = distances[j + 1];
                    distances[j + 1] = Math.Min(Math.Min(above + 1, distances[j] + 1),
                        diagonal + (first[i] == second[j] ? 0 : 1));
                    diagonal = above;
                }
            }
            return distances[second.Length];
        }
    }
}
