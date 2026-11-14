using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    public enum UiTheme { Cyberpunk, Minimal, Steampunk, Modern, Vaporwave, Constructivist, Persona5, ChineseStyle }

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
            { "313F2A", new[] { "375158", "D4EBE2", "654824" } },
            { "9EB5BC", new[] { "C7B9D8", "4D6059", "DCC3A3" } },
            { "E4F4ED", new[] { "F7F0FC", "202D29", "FFF0D7" } },
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

        private static readonly Dictionary<string, string> VaporwaveColors = new Dictionary<string, string>
        {
            { "101C24", "F4C3EB" }, { "EDF4F2", "301A4D" }, { "ECF664", "61399B" },
            { "182015", "FFFFFF" }, { "B0C7CC", "59436F" }, { "52717B", "8C68AF" },
            { "192A34", "FFF3FA" }, { "101D25", "FFF9FE" }, { "2B414B", "E8C7FA" },
            { "43626C", "C9F1EA" }, { "FFAA98", "A12F5F" }, { "A0E0C8", "175D60" },
            { "FFBEB2", "9C235C" }, { "54282F", "FFD8E7" }, { "535B23", "CEFAF0" },
            { "7ED1EF", "257E82" }, { "E79AD5", "AD4D96" }, { "87C264", "20776C" },
            { "101E27", "FAE9FC" }, { "313F2A", "BFF2E9" }, { "9EB5BC", "5A436E" },
            { "E4F4ED", "301A4D" }, { "354B55", "E5C1EE" }, { "1E313A", "FFF3F9" },
            { "091017", "EBCBEF" }, { "2F3A42", "CFACE8" }, { "5D767F", "9069B0" },
            { "99ADB2", "8D6DAB" }, { "24402F", "C1EEE2" }, { "223E53", "C3EBF8" },
            { "3F2E4E", "E7CCF4" }, { "4B3C26", "F8DFB5" }, { "67BDEF", "22568E" },
            { "D192F1", "863F9B" }, { "F7C15D", "915514" }, { "0B1218", "FEBADB" },
            { "14252E", "F5C1F1" }, { "3E626B", "AAE9EC" }, { "5E9097", "B986CF" },
            { "7DABB1", "B186CA" }, { "83B5BC", "AB77C5" }, { "D9FFFF", "FFFFFF" },
            { "F7FFC7", "21A299" }, { "070D10", "61399B" }, { "AEB748", "7950B6" },
            { "7BC5C7", "FFF4FC" }, { "548E9E", "FFF9F4" }, { "192832", "FCE0F3" },
            { "4D737B", "D6B3F6" }, { "FFF5B3", "A6F8ED" }, { "21343D", "F9E4F8" },
            { "5DB5D4", "216A85" }, { "D285AF", "893880" }, { "9DE4EB", "285E77" },
            { "FFD48C", "885617" }, { "091218", "53346A" }, { "0B141B", "F1D0F1" },
            { "4D727D", "C6A2DF" }, { "5E8C97", "B890D4" }, { "5A7B86", "A779CB" },
            { "59A4B4", "55C4C1" }, { "5F8890", "A785C6" }, { "15232B", "FFF6FC" },
            { "89B0B5", "8C68AF" }, { "7DAAB2", "8D67B4" }, { "A8D5D9", "8755AE" },
            { "193242", "E5C5FA" }
        };

        private static readonly Dictionary<string, string[]> AdditionalColors = new Dictionary<string, string[]>
        {
            { "101C24", new[] { "DBC8AC", "171417", "DEDAC3" } },
            { "EDF4F2", new[] { "291B17", "FFFAF6", "263C38" } },
            { "ECF664", new[] { "A72722", "DE102B", "962F25" } },
            { "182015", new[] { "FFF8E9", "FFFFFF", "FFF6E8" } },
            { "B0C7CC", new[] { "5D4234", "E8D8D9", "49564A" } },
            { "52717B", new[] { "805C4A", "F0EBE3", "8B8B73" } },
            { "192A34", new[] { "F2E3CC", "211D21", "EEEAD9" } },
            { "101D25", new[] { "FFF4DD", "130F15", "FBF7E9" } },
            { "2B414B", new[] { "E3CBAA", "30282E", "E2E4D0" } },
            { "43626C", new[] { "D8A879", "5E202A", "D1DFCB" } },
            { "FFAA98", new[] { "A7191C", "FF9B9D", "A43728" } },
            { "A0E0C8", new[] { "345A33", "8FE3D2", "236658" } },
            { "FFBEB2", new[] { "A42022", "FFD3CF", "9B2B22" } },
            { "54282F", new[] { "F5C7B6", "711629", "F1DBCB" } },
            { "535B23", new[] { "E6C6A3", "601B29", "CFE2CE" } },
            { "7ED1EF", new[] { "B14522", "FF4260", "2D7869" } },
            { "E79AD5", new[] { "6D4E35", "FFFFFF", "A64230" } },
            { "87C264", new[] { "3B643F", "74DCC8", "357561" } },
            { "101E27", new[] { "F1E0C6", "151318", "E9EBD9" } },
            { "313F2A", new[] { "E2C69D", "622334", "D1E5D2" } },
            { "9EB5BC", new[] { "6B4A39", "E8D8D9", "566A5C" } },
            { "E4F4ED", new[] { "2C1D17", "FFF9F4", "263C38" } },
            { "354B55", new[] { "DBC4A7", "4A333C", "C5CFBA" } },
            { "1E313A", new[] { "FCEDD4", "251D25", "F8F3E3" } },
            { "091017", new[] { "D6B99A", "0C0B10", "D3D9C4" } },
            { "2F3A42", new[] { "C6A487", "622431", "B7C6AF" } },
            { "5D767F", new[] { "826251", "C09C9F", "748777" } },
            { "99ADB2", new[] { "8A6652", "9D777F", "5B7162" } },
            { "24402F", new[] { "C6D4AF", "29463D", "C7E1CC" } },
            { "223E53", new[] { "C6D6D5", "22374F", "C8DDE7" } },
            { "3F2E4E", new[] { "E3BDCA", "4D2B49", "E3D0DA" } },
            { "4B3C26", new[] { "EAC290", "543A25", "F0D7A0" } },
            { "67BDEF", new[] { "2C6385", "64BFFF", "305F82" } },
            { "D192F1", new[] { "79465E", "ECA8F7", "81526F" } },
            { "F7C15D", new[] { "81500C", "FFD16A", "875719" } },
            { "0B1218", new[] { "DBC4A4", "110E14", "E4E0CD" } },
            { "14252E", new[] { "EBD7B6", "2C0D16", "EFE9D6" } },
            { "3E626B", new[] { "E2B984", "8B0E25", "CEDCCA" } },
            { "5E9097", new[] { "B97652", "963144", "739180" } },
            { "7DABB1", new[] { "BB775A", "E34F65", "829485" } },
            { "83B5BC", new[] { "B5624B", "973144", "80A08C" } },
            { "D9FFFF", new[] { "FFF3D7", "FFF8EE", "F9F6E8" } },
            { "F7FFC7", new[] { "C33D2B", "FF4562", "3B806D" } },
            { "070D10", new[] { "552719", "100B0F", "324B40" } },
            { "AEB748", new[] { "92211D", "E91C41", "8E3F31" } },
            { "7BC5C7", new[] { "E6AD78", "DD1232", "ECEEE1" } },
            { "548E9E", new[] { "D98A55", "6C152C", "D9E4D3" } },
            { "192832", new[] { "E7CEAA", "231820", "EAE8D4" } },
            { "4D737B", new[] { "F1D5AA", "671022", "E1ECDC" } },
            { "FFF5B3", new[] { "E47D48", "FF9D99", "C8E6CA" } },
            { "21343D", new[] { "F3DFB8", "382129", "E7E9D4" } },
            { "5DB5D4", new[] { "B04329", "F9F3ED", "256253" } },
            { "D285AF", new[] { "794735", "EF2848", "A04532" } },
            { "9DE4EB", new[] { "5E3C31", "E0EDF1", "2B6053" } },
            { "FFD48C", new[] { "8B410E", "FFD56C", "8C5925" } },
            { "091218", new[] { "4C2D1E", "0C0B10", "455F55" } },
            { "0B141B", new[] { "E5CDB0", "110E13", "E9E9D5" } },
            { "4D727D", new[] { "BA8763", "6D2039", "9BA791" } },
            { "5E8C97", new[] { "B88660", "8D3344", "819B85" } },
            { "5A7B86", new[] { "A3674A", "632333", "849684" } },
            { "59A4B4", new[] { "B46445", "D53147", "538D76" } },
            { "5F8890", new[] { "8E6247", "6A434B", "748F7E" } },
            { "15232B", new[] { "FAEACD", "20161E", "F8F1DC" } },
            { "89B0B5", new[] { "947059", "AB666F", "809788" } },
            { "7DAAB2", new[] { "9D674A", "982D48", "749684" } },
            { "A8D5D9", new[] { "A25137", "FFD6D5", "477F69" } },
            { "193242", new[] { "E1B57D", "34232D", "D9E8D4" } },
        };

        internal static Window LoadWindow(UiTheme theme)
        {
            if (!System.Enum.IsDefined(typeof(UiTheme), theme))
                theme = UiTheme.Modern;
            string xaml;
            using (var reader = new StreamReader(typeof(ThemePalette).Assembly.GetManifestResourceStream("Window.xaml")))
                xaml = reader.ReadToEnd();
            if (theme != UiTheme.Modern)
                xaml = ColorLiteral.Replace(xaml, match =>
                {
                    string rgb = match.Groups["rgb"].Value;
                    string vapor;
                    if (theme == UiTheme.Vaporwave)
                        return VaporwaveColors.TryGetValue(rgb, out vapor)
                            ? "#" + match.Groups["alpha"].Value + vapor : match.Value;
                    string[] variants;
                    if (theme >= UiTheme.Constructivist)
                        return AdditionalColors.TryGetValue(rgb, out variants)
                            ? "#" + match.Groups["alpha"].Value + variants[theme - UiTheme.Constructivist] : match.Value;
                    return Colors.TryGetValue(rgb, out variants)
                        ? "#" + match.Groups["alpha"].Value + variants[(int)theme] : match.Value;
                });
            if (theme == UiTheme.Steampunk)
                xaml = xaml.Replace(">Microsoft YaHei UI, Segoe UI</FontFamily>", ">SimSun, Cambria</FontFamily>");
            else if (theme == UiTheme.ChineseStyle)
                xaml = xaml.Replace(">Microsoft YaHei UI, Segoe UI</FontFamily>", ">KaiTi, SimSun</FontFamily>");

            var window = (Window)XamlReader.Parse(xaml);
            if (theme != UiTheme.Modern)
            {
                window.Resources["BoardStar"] = window.FindResource("Muted");
                Find(window, "ModernArtwork").Visibility = Visibility.Collapsed;
            }
            if (theme == UiTheme.Cyberpunk)
            {
                Find(window, "CyberpunkArtwork").Visibility = Visibility.Visible;
                Find(window, "CyberpunkCity").Visibility = Visibility.Visible;
            }
            if (theme == UiTheme.Minimal)
            {
                Find(window, "MinimalArtwork").Visibility = Visibility.Visible;
                Find(window, "BackgroundLight").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundPatternTop").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundPatternBottom").Visibility = Visibility.Collapsed;
                Find(window, "StartupPattern").Visibility = Visibility.Collapsed;
            }
            else if (theme == UiTheme.Steampunk)
            {
                Find(window, "SteampunkArtwork").Visibility = Visibility.Visible;
                Find(window, "SteampunkCity").Visibility = Visibility.Visible;
                var rivets = (Brush)window.FindResource("Rivets");
                foreach (string name in new[] { "BackgroundPatternTop", "BackgroundPatternBottom", "StartupPattern" })
                    ((Border)Find(window, name)).Background = rivets;
            }
            else if (theme == UiTheme.Vaporwave)
            {
                foreach (string name in new[] { "VaporwaveBackground", "VaporwaveArtwork", "VaporwaveTitleBar" })
                    Find(window, name).Visibility = Visibility.Visible;
                Find(window, "BackgroundPatternTop").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundPatternBottom").Visibility = Visibility.Collapsed;
            }
            else if (theme >= UiTheme.Constructivist)
            {
                string prefix = theme.ToString();
                Find(window, prefix + "Artwork").Visibility = Visibility.Visible;
                Find(window, prefix + "Background").Visibility = Visibility.Visible;
                Find(window, "BackgroundPatternTop").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundPatternBottom").Visibility = Visibility.Collapsed;
                Find(window, "BackgroundLight").Visibility = Visibility.Collapsed;
            }
            DenseCityArtwork.Apply(window, theme);
            return window;
        }

        private static FrameworkElement Find(Window window, string name)
        {
            return (FrameworkElement)window.FindName(name);
        }
    }
}
