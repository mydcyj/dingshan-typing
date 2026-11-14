using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web.Script.Serialization;

namespace EnglishTypingPractice
{
    internal sealed class PracticeSettings
    {
        private static readonly string FolderPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "EnglishTypingPractice");
        private static readonly string SettingsPath = Path.Combine(FolderPath, "settings.json");

        public PracticeSettings()
        {
            MatchCase = true;
            IncludePunctuation = true;
            PlayMusic = true;
            MusicVolume = 35;
            RoomMasterVolume = 65;
            RoomMusicVolume = 65;
            RoomEffectsVolume = 50;
            SnakeReviewWords = new Dictionary<string, int>();
            SnakeRaceBest = new Dictionary<string, int>();
            PracticeRecords = new PracticeLeaderboard();
        }

        public bool MatchCase { get; set; }
        public bool IncludePunctuation { get; set; }
        public bool TypingFeedback { get; set; }
        public bool SnakeVoiceEnabled { get; set; }
        public bool SnakeLowEffects { get; set; }
        public string LocalNickname { get; set; }
        public string PracticeFont { get; set; }
        public UiTheme Theme { get; set; }
        public Dictionary<string, int> SnakeReviewWords { get; set; }
        public Dictionary<string, int> SnakeRaceBest { get; set; }
        public PracticeLeaderboard PracticeRecords { get; set; }
        public bool PlayMusic { get; set; }
        public string Folder { get; set; }
        public PlaybackMode MusicMode { get; set; }
        public int MusicVolume { get; set; }
        public string MusicTrack { get; set; }
        public int RoomMasterVolume { get; set; }
        public int RoomMusicVolume { get; set; }
        public int RoomEffectsVolume { get; set; }

        internal static PracticeSettings Load()
        {
            try
            {
                var settings = new JavaScriptSerializer().Deserialize<PracticeSettings>(File.ReadAllText(SettingsPath))
                    ?? new PracticeSettings();
                if (!Enum.IsDefined(typeof(PlaybackMode), settings.MusicMode))
                    settings.MusicMode = PlaybackMode.Sequential;
                if (!Enum.IsDefined(typeof(UiTheme), settings.Theme))
                    settings.Theme = UiTheme.Cyberpunk;
                settings.MusicVolume = Math.Max(0, Math.Min(100, settings.MusicVolume));
                settings.RoomMasterVolume = Math.Max(0, Math.Min(100, settings.RoomMasterVolume));
                settings.RoomMusicVolume = Math.Max(0, Math.Min(100, settings.RoomMusicVolume));
                settings.RoomEffectsVolume = Math.Max(0, Math.Min(100, settings.RoomEffectsVolume));
                if (settings.SnakeReviewWords == null)
                    settings.SnakeReviewWords = new Dictionary<string, int>();
                if (settings.SnakeRaceBest == null)
                    settings.SnakeRaceBest = new Dictionary<string, int>();
                if (settings.PracticeRecords == null)
                    settings.PracticeRecords = new PracticeLeaderboard();
                settings.LocalNickname = NormalizeLocalNickname(settings.LocalNickname);
                return settings;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException
                    || ex is ArgumentException || ex is InvalidOperationException))
                    throw;
                return new PracticeSettings();
            }
        }

        internal static string NormalizeLocalNickname(string value)
        {
            string nickname = value == null ? null : value.Trim();
            return LanWire.IsName(nickname, 20) ? nickname : null;
        }

        internal bool RecordSnakeReview(string input, IEnumerable<string> allowedWords)
        {
            if (string.IsNullOrEmpty(input) || input.Length < 2 || input.Length > 20 ||
                !input.All(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z'))
                return false;
            input = input.ToLowerInvariant();
            var matches = allowedWords.Where(SnakeWords.Contains).Distinct().ToArray();
            var prefixes = matches.Where(word => word.StartsWith(input, StringComparison.Ordinal)).ToArray();
            string selected;
            if (prefixes.Length != 0)
                selected = prefixes.Length == 1 ? prefixes[0] : null;
            else
            {
                int best = 3;
                selected = null;
                foreach (string word in matches)
                {
                    int distance = TypingRound.EditDistance(input, word);
                    if (distance < best)
                    {
                        best = distance;
                        selected = word;
                    }
                    else if (distance == best)
                        selected = null;
                }
            }
            if (selected == null)
                return false;
            int count;
            SnakeReviewWords.TryGetValue(selected, out count);
            SnakeReviewWords[selected] = Math.Max(0, Math.Min(9999, count)) + 1;
            if (SnakeReviewWords.Count > 300)
                SnakeReviewWords.Remove(SnakeReviewWords.OrderBy(item => item.Value).First().Key);
            return true;
        }

        internal bool Save()
        {
            string temporaryPath = Path.Combine(FolderPath, Guid.NewGuid().ToString("N") + ".tmp");
            try
            {
                Directory.CreateDirectory(FolderPath);
                File.WriteAllText(temporaryPath, new JavaScriptSerializer().Serialize(this));
                if (File.Exists(SettingsPath))
                    File.Replace(temporaryPath, SettingsPath, null);
                else
                    File.Move(temporaryPath, SettingsPath);
                return true;
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException))
                    throw;
                try { File.Delete(temporaryPath); }
                catch (IOException) { }
                catch (UnauthorizedAccessException) { }
                return false;
            }
        }
    }
}
