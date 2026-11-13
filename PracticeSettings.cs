using System;
using System.IO;
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
        }

        public bool MatchCase { get; set; }
        public bool IncludePunctuation { get; set; }
        public bool TypingFeedback { get; set; }
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
                settings.MusicVolume = Math.Max(0, Math.Min(100, settings.MusicVolume));
                settings.RoomMasterVolume = Math.Max(0, Math.Min(100, settings.RoomMasterVolume));
                settings.RoomMusicVolume = Math.Max(0, Math.Min(100, settings.RoomMusicVolume));
                settings.RoomEffectsVolume = Math.Max(0, Math.Min(100, settings.RoomEffectsVolume));
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
