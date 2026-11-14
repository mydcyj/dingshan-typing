using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    public enum UiTheme { Cyberpunk, Minimal, Steampunk }

    internal static class ThemePalette
    {
        // Recolor the shared XAML before loading so templates and frozen drawing brushes use the same palette.
        private static readonly Regex ColorLiteral = new Regex(@"#(?<alpha>[0-9A-F]{2})?(?<rgb>[0-9A-F]{6})\b");

        private static readonly Dictionary<string, string[]> Colors = new Dictionary<string, string[]>
        {
            { "101C24", new[] { "15111F", "F1F2EF", "241A14" } },
            { "EDF4F2", new[] { "F7F0FC", "202D29", "FFF0D7" } },
            { "ECF664", new[] { "56F4E5", "266858", "F0BF73" } },
            { "182015", new[] { "101A20", "FFFFFF", "24160D" } },
            { "B0C7CC", new[] { "C7B9D8", "4D6059", "DCC3A3" } },
            { "52717B", new[] { "69547F", "8C9B95", "98714E" } },
            { "192A34", new[] { "241B32", "FFFFFF", "36271E" } },
            { "101D25", new[] { "191321", "F9FBF9", "271B14" } },
            { "2B414B", new[] { "362644", "E9EFEB", "493224" } },
            { "43626C", new[] { "55416B", "B6CDC3", "66462E" } },
            { "FFAA98", new[] { "FFAAA8", "A13034", "FFAC9B" } },
            { "A0E0C8", new[] { "8FEBD2", "17604E", "B7E0AD" } },
            { "FFBEB2", new[] { "FFC1C7", "921C30", "FFD2BF" } },
            { "54282F", new[] { "5F2841", "FDE0E1", "603225" } },
            { "535B23", new[] { "375158", "D4EBE2", "654824" } },
            { "7ED1EF", new[] { "52F2E8", "3B7C69", "ECB973" } },
            { "E79AD5", new[] { "F995E8", "667F75", "D98C67" } },
            { "87C264", new[] { "54C98B", "297454", "85BE71" } },
            { "101E27", new[] { "191221", "F1F6F2", "251910" } },
            { "354B55", new[] { "473453", "C5D4CD", "523924" } },
            { "1E313A", new[] { "30223E", "FDFFFC", "3F2C1F" } },
            { "091017", new[] { "100A18", "DCE6DF", "1A120C" } },
            { "2F3A42", new[] { "3C2A45", "D8DFDB", "4D3E33" } },
            { "5D767F", new[] { "725886", "71857B", "7D5D3F" } },
            { "99ADB2", new[] { "A17DB4", "4F6258", "BB8D56" } },
            { "24402F", new[] { "233D31", "C9E2D0", "34402B" } },
            { "223E53", new[] { "273951", "CDDFED", "293F46" } },
            { "3F2E4E", new[] { "462C57", "E6D6E9", "4C3447" } },
            { "4B3C26", new[] { "4E3A37", "EFE0BB", "514128" } },
            { "67BDEF", new[] { "67BDEF", "24608C", "75B3CD" } },
            { "D192F1", new[] { "D88EF4", "794486", "CF94CD" } },
            { "F7C15D", new[] { "F8BF75", "8D5B0A", "E7B15F" } },
            { "0B1218", new[] { "100B1C", "EEF0EC", "1C140F" } },
            { "14252E", new[] { "201530", "F3F5F2", "302118" } },
            { "3E626B", new[] { "51365F", "E6EEE9", "543C29" } },
            { "5E9097", new[] { "B372DC", "8FA39A", "9D764E" } },
            { "7DABB1", new[] { "D691EF", "9CAB9F", "C79460" } },
            { "83B5BC", new[] { "D878DF", "A8B5AA", "D4AB7B" } },
            { "D9FFFF", new[] { "E9D5FC", "446759", "FFE1AA" } },
            { "F7FFC7", new[] { "C1FFF3", "267259", "FFE3A4" } },
            { "070D10", new[] { "100A19", "476357", "120B07" } },
            { "AEB748", new[] { "35BFAF", "215646", "BF8B4C" } },
            { "7BC5C7", new[] { "BF5FCB", "FFFFFF", "B8783B" } },
            { "548E9E", new[] { "714CA4", "FFFFFF", "815638" } },
            { "192832", new[] { "241832", "F9FBF9", "352418" } },
            { "4D737B", new[] { "5B3E6D", "E6EFEA", "614531" } },
            { "6E989F", new[] { "AB73CE", "8DAEA0", "BC986C" } },
            { "5D8E9F", new[] { "B368C8", "658979", "A57B54" } },
            { "A8C7C9", new[] { "D49AEA", "7EA598", "DCB277" } },
            { "A8BDBF", new[] { "B978CB", "B7CDC3", "C19368" } },
            { "BCD1CB", new[] { "C9A3E6", "C4DCD1", "C9A278" } },
            { "F1F6D9", new[] { "D3FFF4", "EDF7F1", "FDE3B8" } },
            { "8BAFB4", new[] { "8C69B9", "B1C8BF", "AB7751" } },
            { "C5D8CE", new[] { "BCEEDC", "C4D5CF", "DBB385" } },
            { "FFF5B3", new[] { "D5FFF1", "3D8A70", "FFE1A2" } },
            { "21343D", new[] { "332547", "FAFCFA", "432D21" } },
            { "5DB5D4", new[] { "5FD6F7", "3C7066", "DAAC6E" } },
            { "D285AF", new[] { "F884DA", "866A84", "CE8569" } },
            { "9DE4EB", new[] { "9FE7FA", "33645C", "F1CEA2" } },
            { "FFD48C", new[] { "FFC477", "925712", "FFD47F" } },
            { "091218", new[] { "110B1B", "52645C", "190E07" } },
            { "0B141B", new[] { "181020", "F1F4F0", "22160F" } },
            { "4D727D", new[] { "8F64B4", "8BA898", "A3754D" } },
            { "5E8C97", new[] { "A770C7", "9BAEA4", "9E7853" } },
            { "5A7B86", new[] { "8961A5", "94A89C", "A87A55" } },
            { "59A4B4", new[] { "AF7DCB", "678D7E", "B78450" } },
            { "5F8890", new[] { "9C6CB5", "92A399", "AE8865" } },
            { "15232B", new[] { "23172F", "FFFFFF", "34261B" } },
            { "89B0B5", new[] { "AF82C9", "8B9E92", "BB8D5E" } },
            { "7DAAB2", new[] { "C48CE1", "84968D", "C59460" } },
            { "A8D5D9", new[] { "D4A5ED", "51766A", "E7BC85" } },
            { "193242", new[] { "39294B", "E5ECE7", "4A3426" } }
        };

        internal static Window LoadWindow(UiTheme theme)
        {
            int palette = (int)theme;
            if (palette < 0 || palette > (int)UiTheme.Steampunk)
                palette = (int)UiTheme.Cyberpunk;
            string xaml;
            using (var reader = new StreamReader(typeof(ThemePalette).Assembly.GetManifestResourceStream("Window.xaml")))
                xaml = reader.ReadToEnd();
            xaml = ColorLiteral.Replace(xaml, match =>
            {
                string[] variants;
                return Colors.TryGetValue(match.Groups["rgb"].Value, out variants)
                    ? "#" + match.Groups["alpha"].Value + variants[palette] : match.Value;
            });
            if (theme == UiTheme.Steampunk)
                xaml = xaml.Replace(">Microsoft YaHei UI, Segoe UI</FontFamily>", ">SimSun, Cambria</FontFamily>");

            var window = (Window)XamlReader.Parse(xaml);
            if (theme == UiTheme.Minimal)
            {
                Find(window, "CyberpunkArtwork").Visibility = Visibility.Collapsed;
                Find(window, "MinimalArtwork").Visibility = Visibility.Visible;
                Find(window, "BackgroundLight").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundPatternTop").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundPatternBottom").Visibility = Visibility.Collapsed;
                Find(window, "StartupPattern").Visibility = Visibility.Collapsed;
            }
            else if (theme == UiTheme.Steampunk)
            {
                Find(window, "CyberpunkArtwork").Visibility = Visibility.Collapsed;
                Find(window, "SteampunkArtwork").Visibility = Visibility.Visible;
                var rivets = (Brush)window.FindResource("Rivets");
                foreach (string name in new[] { "BackgroundPatternTop", "BackgroundPatternBottom", "StartupPattern" })
                    ((Border)Find(window, name)).Background = rivets;
            }
            return window;
        }

        private static FrameworkElement Find(Window window, string name)
        {
            return (FrameworkElement)window.FindName(name);
        }
    }
}
