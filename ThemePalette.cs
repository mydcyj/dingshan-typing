using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Markup;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    public enum UiTheme { Cyberpunk, Minimal, Steampunk, Modern, Vaporwave, Constructivist, Persona5, ChineseStyle,
        Windows98, CassetteFuturism, ArtDeco, Atompunk, Ukiyoe, Pixel, Solarpunk }

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

        // Ordering matches the four themes appended to UiTheme. Existing saved numeric values stay stable.
        private static readonly Dictionary<string, string[]> NewColors = new Dictionary<string, string[]>
        {
            { "101C24", new[] { "008080", "182423", "12100F", "E7DDBE", "E8DBC0", "151329", "EDF4DA" } },
            { "EDF4F2", new[] { "171717", "F4E7C7", "F6E8CD", "193B37", "1D3546", "F1EDFF", "193D32" } },
            { "ECF664", new[] { "000080", "F0BD70", "D4AF63", "146B65", "A73C2E", "FFD36B", "286C4D" } },
            { "182015", new[] { "FFFFFF", "1C241F", "211509", "FFFBEC", "FFF6DB", "221A36", "F8FFE9" } },
            { "B0C7CC", new[] { "353839", "CCC198", "CAB89A", "435F58", "59656A", "B9AED3", "526E59" } },
            { "52717B", new[] { "808080", "736B48", "8D6D3E", "709889", "8D8973", "7765A4", "92AE80" } },
            { "192A34", new[] { "C0C0C0", "293531", "221C17", "FFF1CF", "F8EFD9", "242039", "F8FBEA" } },
            { "101D25", new[] { "FFFFFF", "10211E", "181410", "FFF8E5", "FFF5DF", "19162D", "FFFFF2" } },
            { "2B414B", new[] { "C0C0C0", "3F4840", "292119", "E7DDBE", "E1D2B5", "39304E", "E0EDC9" } },
            { "43626C", new[] { "A7BABD", "62533B", "49351D", "B7D7C5", "C5D1CB", "5C456F", "C7DFAD" } },
            { "FFAA98", new[] { "98171D", "FFC397", "F4A489", "A1342A", "A2392E", "FFA8BC", "AC492F" } },
            { "A0E0C8", new[] { "006045", "ACD39F", "B6D5AD", "167157", "295F55", "90EACB", "237044" } },
            { "FFBEB2", new[] { "8E131D", "FFD2A9", "FFB1A0", "922721", "9C302B", "FF9CA9", "9E332C" } },
            { "54282F", new[] { "FFDCDD", "653522", "58312B", "F3D4BF", "ECD0B7", "642940", "F9DAC9" } },
            { "535B23", new[] { "D2DDF7", "554826", "4C3920", "CBE5D7", "E2D1A9", "544523", "D9ECC5" } },
            { "7ED1EF", new[] { "000080", "EAC06F", "F0D49A", "217C72", "2C5770", "87D9FB", "367B6A" } },
            { "E79AD5", new[] { "008080", "80A191", "E5A66A", "BB4636", "AD5541", "EEA5D9", "D9A347" } },
            { "87C264", new[] { "008262", "A9C889", "C8D798", "25776C", "3C8074", "A1E07A", "3C8152" } },
            { "101E27", new[] { "E7ECEA", "142723", "171311", "E3EEDB", "EDE3CC", "16152B", "E6F0D8" } },
            { "313F2A", new[] { "D0DBF1", "4C4929", "41311B", "CAE3D5", "DDD3B5", "3E384D", "CADFB6" } },
            { "9EB5BC", new[] { "3F4546", "D3CAA5", "CAB89A", "3E5B53", "687873", "A59BB9", "536E57" } },
            { "E4F4ED", new[] { "171717", "F4E7C7", "F6E8CD", "193B37", "243B48", "F1EDFF", "193D32" } },
            { "354B55", new[] { "BAC7CB", "3D503D", "473724", "A6C5AC", "C4BDA7", "302940", "B8D1A8" } },
            { "1E313A", new[] { "FFFFFF", "2D3F35", "272019", "FFF2D7", "FFF0D3", "302642", "F5FBE6" } },
            { "091017", new[] { "889996", "14201D", "110E0B", "B6CABA", "D3C4A7", "0D0B19", "CBD9BA" } },
            { "2F3A42", new[] { "9CABA9", "35473C", "3D3125", "B7C9B8", "C4B59C", "382641", "B6C8A3" } },
            { "5D767F", new[] { "647674", "6A7D5D", "76593A", "6C8475", "8C8570", "605177", "739369" } },
            { "99ADB2", new[] { "3E4E50", "B8B392", "B89560", "3F5C4E", "525F60", "AB8CC3", "466F4A" } },
            { "24402F", new[] { "AED8C1", "334C38", "314535", "AAD5B8", "AAD5B8", "24402F", "AAD5B8" } },
            { "223E53", new[] { "B6CBE6", "2D4345", "253B50", "A8D1D7", "A8D1D7", "223E53", "A8D1D7" } },
            { "3F2E4E", new[] { "D8C1DB", "4D4355", "462F49", "DEC9DD", "DEC9DD", "3F2E4E", "DEC9DD" } },
            { "4B3C26", new[] { "E7D1A9", "634D2E", "534024", "F1D18A", "F1D18A", "4B3C26", "F1D18A" } },
            { "67BDEF", new[] { "2058A0", "8BC6DA", "83C6EF", "2A6288", "2A6288", "67BDEF", "2A6288" } },
            { "D192F1", new[] { "774782", "D5A8D6", "DEA7EB", "875273", "875273", "D192F1", "875273" } },
            { "F7C15D", new[] { "936013", "F3BD63", "F3C26F", "8D5318", "8D5318", "F7C15D", "8D5318" } },
            { "0B1218", new[] { "008080", "121D1B", "100E0D", "E8CF9F", "E0D1B2", "100E23", "E1ECCA" } },
            { "14252E", new[] { "008080", "23332F", "181410", "F6E8C7", "F5E9CE", "201A38", "F4F8DF" } },
            { "3E626B", new[] { "008080", "354038", "211B16", "BFD8C0", "D1D8C5", "363055", "CFE8BA" } },
            { "5E9097", new[] { "357C7F", "53664C", "8B6E43", "87A796", "87A796", "5E9097", "87A796" } },
            { "7DABB1", new[] { "C0C0C0", "A99053", "806136", "7FAF9C", "7FAF9C", "7DABB1", "7FAF9C" } },
            { "83B5BC", new[] { "C0C0C0", "56654D", "715630", "85AD97", "85AD97", "83B5BC", "85AD97" } },
            { "D9FFFF", new[] { "FFFFFF", "FFF4D1", "FFF2D6", "FFFFFF", "FFFFFF", "D9FFFF", "FFFFFF" } },
            { "F7FFC7", new[] { "FFFFFF", "F7D181", "F6D588", "B93327", "B93327", "F7FFC7", "B93327" } },
            { "070D10", new[] { "000000", "101814", "0D0B0A", "23443B", "23443B", "070D10", "23443B" } },
            { "AEB748", new[] { "141485", "DCA653", "C49754", "B54032", "B54032", "AEB748", "B54032" } },
            { "7BC5C7", new[] { "008080", "556554", "AA8652", "FFE6B1", "FFE6B1", "7BC5C7", "FFE6B1" } },
            { "548E9E", new[] { "008080", "42554C", "705435", "E2F2D9", "E2F2D9", "548E9E", "E2F2D9" } },
            { "192832", new[] { "C0C0C0", "24322E", "191510", "FFF0C9", "EEE0C1", "1B1830", "EDF4DA" } },
            { "4D737B", new[] { "D4D0C8", "3B4840", "2D2117", "DCE9CC", "E0E2CF", "463660", "D5E8BC" } },
            { "FFF5B3", new[] { "FFFFFF", "EDBA69", "E7C789", "94CEB5", "94CEB5", "FFF5B3", "94CEB5" } },
            { "21343D", new[] { "D4D0C8", "29342B", "211A14", "F4E9C7", "E7D8BB", "2A203D", "EBF3D5" } },
            { "5DB5D4", new[] { "184B94", "B1D5CE", "E4BE7D", "185C59", "185C59", "5DB5D4", "185C59" } },
            { "D285AF", new[] { "973F6E", "D9A16F", "CF9A61", "B23E32", "B23E32", "D285AF", "B23E32" } },
            { "9DE4EB", new[] { "15496F", "C0D8CF", "E5C590", "17645C", "17645C", "9DE4EB", "17645C" } },
            { "FFD48C", new[] { "815617", "F7CA7E", "F8D796", "90450D", "90450D", "FFD48C", "90450D" } },
            { "091218", new[] { "23333C", "15221F", "100D0B", "395F54", "395F54", "091218", "395F54" } },
            { "0B141B", new[] { "008080", "17221F", "14110E", "E8DAB6", "E8DAB6", "0B141B", "E8DAB6" } },
            { "4D727D", new[] { "C0C0C0", "4C624D", "846236", "B1CDAD", "B1CDAD", "4D727D", "B1CDAD" } },
            { "5E8C97", new[] { "008080", "626347", "9E8050", "9BAB85", "9BAB85", "5E8C97", "9BAB85" } },
            { "5A7B86", new[] { "C0C0C0", "5A6855", "806440", "BACDA9", "BACDA9", "5A7B86", "BACDA9" } },
            { "59A4B4", new[] { "0D5579", "96A17D", "AF8D56", "4B8D7C", "4B8D7C", "59A4B4", "4B8D7C" } },
            { "5F8890", new[] { "000080", "50604E", "735C3F", "A6BD92", "A6BD92", "5F8890", "A6BD92" } },
            { "15232B", new[] { "FFFFFF", "27352E", "211B15", "FFF0D0", "FFF0D0", "15232B", "FFF0D0" } },
            { "89B0B5", new[] { "008080", "9E8156", "AB8A53", "9CAD82", "9CAD82", "89B0B5", "9CAD82" } },
            { "7DAAB2", new[] { "538C8D", "5C7159", "A28252", "A6C7A1", "A6C7A1", "7DAAB2", "A6C7A1" } },
            { "A8D5D9", new[] { "224E5E", "A2B294", "D7B37B", "408875", "408875", "A8D5D9", "408875" } },
            { "193242", new[] { "C0C0C0", "283D32", "2B2118", "D0E3BF", "D0E3BF", "193242", "D0E3BF" } }
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
                    if (theme >= UiTheme.Windows98)
                        return NewColors.TryGetValue(rgb, out variants)
                            ? "#" + match.Groups["alpha"].Value + variants[theme - UiTheme.Windows98] : match.Value;
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
            else if (theme >= UiTheme.Windows98)
            {
                foreach (string name in new[] { "BackgroundLight", "BackgroundPatternTop", "BackgroundPatternBottom",
                    "BackgroundGrid", "StartupPattern" })
                    Find(window, name).Visibility = Visibility.Collapsed;
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
            ThemeControlDesign.Apply(window, theme);
            return window;
        }

        private static FrameworkElement Find(Window window, string name)
        {
            return (FrameworkElement)window.FindName(name);
        }
    }
}
