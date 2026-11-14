using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    // The key positions are the physical ANSI QWERTY layout; input still comes from the real keyboard.
    internal sealed class KeyboardMappingPractice
    {
        private sealed class KeyCap
        {
            internal Key Key;
            internal Border Plate;
            internal TextBlock Label;
        }

        private readonly Border host;
        private readonly TextBlock hint;
        private readonly TextBlock meaning;
        private readonly List<KeyCap> caps = new List<KeyCap>();
        private readonly Dictionary<Key, bool> pressed = new Dictionary<Key, bool>();
        private readonly Brush field, text, line, accent, ink, correct, incorrect;
        private Key expected = Key.None;
        private bool needsShift;
        private bool correcting;

        internal KeyboardMappingPractice(Window window, TextBox input)
        {
            field = (Brush)window.FindResource("Field");
            text = (Brush)window.FindResource("Text");
            line = (Brush)window.FindResource("Line");
            accent = (Brush)window.FindResource("Accent");
            ink = (Brush)window.FindResource("Ink");
            correct = (Brush)window.FindResource("PracticeCorrect");
            incorrect = (Brush)window.FindResource("PracticeIncorrect");
            hint = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = accent, Margin = new Thickness(0, 0, 0, 5) };
            meaning = new TextBlock { TextWrapping = TextWrapping.Wrap, Foreground = text, Margin = new Thickness(0, 0, 0, 8) };
            var content = new StackPanel();
            content.Children.Add(hint);
            content.Children.Add(meaning);
            var board = new StackPanel { Width = 750 };
            AddRow(board, new[] { "~\n`", "!\n1", "@\n2", "#\n3", "$\n4", "%\n5", "^\n6", "&\n7", "*\n8", "(\n9", ")\n0", "_\n-", "+\n=", "Backspace" },
                new[] { Key.Oem3, Key.D1, Key.D2, Key.D3, Key.D4, Key.D5, Key.D6, Key.D7, Key.D8, Key.D9, Key.D0, Key.OemMinus, Key.OemPlus, Key.Back },
                new[] { 1.0, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2 });
            AddRow(board, new[] { "Tab", "Q", "W", "E", "R", "T", "Y", "U", "I", "O", "P", "{\n[", "}\n]", "|\n\\" },
                new[] { Key.Tab, Key.Q, Key.W, Key.E, Key.R, Key.T, Key.Y, Key.U, Key.I, Key.O, Key.P, Key.Oem4, Key.Oem6, Key.Oem5 },
                new[] { 1.5, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1.5 });
            AddRow(board, new[] { "Caps Lock", "A", "S", "D", "F ▰", "G", "H", "J ▰", "K", "L", ":\n;", "\"\n'", "Enter" },
                new[] { Key.CapsLock, Key.A, Key.S, Key.D, Key.F, Key.G, Key.H, Key.J, Key.K, Key.L, Key.Oem1, Key.OemQuotes, Key.Enter },
                new[] { 1.75, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.25 });
            AddRow(board, new[] { "Shift", "Z", "X", "C", "V", "B", "N", "M", "<\n,", ">\n.", "?\n/", "Shift" },
                new[] { Key.LeftShift, Key.Z, Key.X, Key.C, Key.V, Key.B, Key.N, Key.M, Key.OemComma, Key.OemPeriod, Key.Oem2, Key.RightShift },
                new[] { 2.25, 1, 1, 1, 1, 1, 1, 1, 1, 1, 1, 2.75 });
            AddRow(board, new[] { "Ctrl", "Win", "Alt", "Space · 空格", "Alt", "Win", "Menu", "Ctrl" },
                new[] { Key.LeftCtrl, Key.LWin, Key.LeftAlt, Key.Space, Key.RightAlt, Key.RWin, Key.Apps, Key.RightCtrl },
                new[] { 1.25, 1.25, 1.25, 6.25, 1.25, 1.25, 1.25, 1.25 });
            content.Children.Add(new Viewbox { Child = board, Stretch = Stretch.Uniform, StretchDirection = StretchDirection.DownOnly });
            content.Children.Add(new TextBlock
            {
                Text = "亮色：下一键   边框：正在按下   红色：按键不符   F / J：食指归位",
                Foreground = (Brush)window.FindResource("Muted"), FontSize = 12,
                TextWrapping = TextWrapping.Wrap, Margin = new Thickness(0, 7, 0, 0)
            });
            host = new Border
            {
                Child = content, Background = field, BorderBrush = line, BorderThickness = new Thickness(1),
                Padding = new Thickness(10), Margin = new Thickness(0, 10, 0, 0), Visibility = Visibility.Collapsed,
                IsHitTestVisible = false
            };
            var inputPanel = (StackPanel)input.Parent;
            inputPanel.Children.Insert(inputPanel.Children.IndexOf(input) + 1, host);
            window.RegisterName("MappingKeyboard", host);
            window.RegisterName("MappingNextKey", hint);
            window.RegisterName("MappingWordMeaning", meaning);
            input.PreviewKeyDown += (sender, e) =>
            {
                if (!host.IsVisible || input.IsReadOnly)
                    return;
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                bool shift = key == Key.LeftShift || key == Key.RightShift;
                bool utility = key == Key.Back || key == Key.Delete || key == Key.Left || key == Key.Right ||
                    key == Key.Home || key == Key.End;
                pressed[key] = key == expected || shift && needsShift || utility;
                Paint();
            };
            input.PreviewKeyUp += (sender, e) =>
            {
                pressed.Remove(e.Key == Key.System ? e.SystemKey : e.Key);
                Paint();
            };
            input.LostKeyboardFocus += (sender, e) => { pressed.Clear(); Paint(); };
            window.Deactivated += (sender, e) => { pressed.Clear(); Paint(); };
        }

        internal void SetVisible(bool visible)
        {
            host.Visibility = visible ? Visibility.Visible : Visibility.Collapsed;
            pressed.Clear();
            expected = Key.None;
            needsShift = correcting = false;
            Paint();
        }

        internal void Update(string target, string input, bool matchCase)
        {
            if (host.Visibility != Visibility.Visible)
                return;
            int position = 0;
            while (position < input.Length && position < target.Length && (matchCase
                ? input[position] == target[position]
                : char.ToUpperInvariant(input[position]) == char.ToUpperInvariant(target[position])))
                position++;
            correcting = position < Math.Min(input.Length, target.Length);
            char next = position < target.Length ? target[position] : '\0';
            expected = KeyForCharacter(next, out needsShift);
            hint.Text = next == '\0' ? "本组完成 · 按空格换组词" :
                (correcting ? "第 " + (position + 1) + " 个字符有误，请移到错处订正或用 Backspace 删除。应输入 " : "下一键：") +
                KeyLabel(next) + "　" + FingerFor(expected) + (needsShift ? "，配合另一只手的 Shift" : "");
            int wordPosition = Math.Min(position, target.Length - 1);
            if (wordPosition >= 0 && target[wordPosition] == ' ' && wordPosition > 0)
                wordPosition--;
            int start = Math.Max(0, wordPosition), end = start;
            while (start > 0 && char.IsLetter(target[start - 1])) start--;
            while (end < target.Length && char.IsLetter(target[end])) end++;
            string word = end > start ? target.Substring(start, end - start) : "";
            meaning.Text = SnakeWords.Contains(word) ? word + "  ·  " + SnakeWords.Meaning(word) : "";
            Paint();
        }

        private void AddRow(Panel board, string[] labels, Key[] keys, double[] widths)
        {
            var row = new StackPanel { Orientation = Orientation.Horizontal };
            for (int i = 0; i < labels.Length; i++)
            {
                var label = new TextBlock
                {
                    Text = labels[i], FontFamily = new FontFamily("Consolas, Microsoft YaHei UI"),
                    FontSize = labels[i].Length > 7 ? 11 : 13, TextAlignment = TextAlignment.Center,
                    VerticalAlignment = VerticalAlignment.Center, HorizontalAlignment = HorizontalAlignment.Center
                };
                var plate = new Border
                {
                    Child = label, Width = widths[i] * 50 - 4, Height = 40, Margin = new Thickness(2),
                    CornerRadius = new CornerRadius(3), BorderThickness = new Thickness(1)
                };
                caps.Add(new KeyCap { Key = keys[i], Plate = plate, Label = label });
                row.Children.Add(plate);
            }
            board.Children.Add(row);
        }

        private void Paint()
        {
            foreach (var cap in caps)
            {
                bool down;
                bool isPressed = pressed.TryGetValue(cap.Key, out down);
                bool next = cap.Key == expected || needsShift && (cap.Key == Key.LeftShift || cap.Key == Key.RightShift);
                cap.Plate.Background = next ? accent : field;
                cap.Label.Foreground = next ? ink : text;
                cap.Plate.BorderBrush = isPressed ? down ? correct : incorrect : correcting && cap.Key == Key.Back ? incorrect : line;
                cap.Plate.BorderThickness = new Thickness(isPressed ? 3 : correcting && cap.Key == Key.Back ? 2 : 1);
            }
        }

        internal static Key KeyForCharacter(char character, out bool shift)
        {
            shift = char.IsUpper(character);
            char lower = char.ToLowerInvariant(character);
            if (lower >= 'a' && lower <= 'z') return Key.A + (lower - 'a');
            if (character >= '0' && character <= '9') return Key.D0 + (character - '0');
            const string plain = "`-=[]\\;',./";
            const string upper = "~_+{}|:\"<>?";
            var keys = new[] { Key.Oem3, Key.OemMinus, Key.OemPlus, Key.Oem4, Key.Oem6, Key.Oem5, Key.Oem1, Key.OemQuotes, Key.OemComma, Key.OemPeriod, Key.Oem2 };
            int index = plain.IndexOf(character);
            if (index >= 0) return keys[index];
            index = upper.IndexOf(character);
            if (index >= 0) { shift = true; return keys[index]; }
            index = ")!@#$%^&*(".IndexOf(character);
            if (index >= 0) { shift = true; return Key.D0 + index; }
            return character == ' ' ? Key.Space : character == '\t' ? Key.Tab : character == '\n' || character == '\r' ? Key.Enter : Key.None;
        }

        private static string KeyLabel(char character)
        {
            return character == ' ' ? "Space 空格" : character == '\t' ? "Tab" : character == '\n' ? "Enter" : character.ToString();
        }

        internal static string FingerFor(Key key)
        {
            if (key == Key.Space) return "任一拇指";
            if (new[] { Key.Oem3, Key.D1, Key.Q, Key.A, Key.Z, Key.Tab }.Contains(key)) return "左手小指";
            if (new[] { Key.D2, Key.W, Key.S, Key.X }.Contains(key)) return "左手无名指";
            if (new[] { Key.D3, Key.E, Key.D, Key.C }.Contains(key)) return "左手中指";
            if (new[] { Key.D4, Key.D5, Key.R, Key.T, Key.F, Key.G, Key.V, Key.B }.Contains(key)) return "左手食指";
            if (new[] { Key.D6, Key.D7, Key.Y, Key.U, Key.H, Key.J, Key.N, Key.M }.Contains(key)) return "右手食指";
            if (new[] { Key.D8, Key.I, Key.K, Key.OemComma }.Contains(key)) return "右手中指";
            if (new[] { Key.D9, Key.O, Key.L, Key.OemPeriod }.Contains(key)) return "右手无名指";
            return key == Key.None ? "" : "右手小指";
        }
    }
}
