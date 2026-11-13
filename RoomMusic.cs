using System;
using System.IO;
using System.Linq;
using System.Media;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    internal sealed class RoomMusicState
    {
        public int Volume { get; set; }
        public int Track { get; set; }
        public string Title { get; set; }
        public string Extension { get; set; }
        public long Length { get; set; }
        public long Position { get; set; }

        internal bool IsValid
        {
            get
            {
                return Volume >= 0 && Volume <= 100 && (Track == 0 ? Length == 0 :
                    Track > 0 && !string.IsNullOrEmpty(Title) && Title.Length <= 120 &&
                    MusicPlayback.IsAudioExtension(Extension) && Length > 0 && Length <= LanWire.MusicLimit &&
                    Position >= 0 && Position <= TimeSpan.MaxValue.TotalMilliseconds);
            }
        }
    }

    internal sealed class RoomMusic
    {
        internal const string Folder = @"E:\dingshan";
        private readonly TextBlock status;
        private readonly PracticeSettings settings;
        private readonly SoundPlayer[] effects = new SoundPlayer[3];
        private MusicPlayback hostPlayer;
        private MediaPlayer guestPlayer;
        private CancellationTokenSource pending;
        private string localFile;
        private RoomMusicState music;
        private bool playing;
        private int track;
        private int failedTrack;

        internal RoomMusic(TextBlock status, PracticeSettings settings)
        {
            this.status = status;
            this.settings = settings;
        }

        internal Task Host(LanHost host)
        {
            host.SetMusicVolume(settings.RoomMasterVolume);
            var player = new MusicPlayback(text =>
            {
                status.Text = text;
                if (text.StartsWith("正在读取", StringComparison.Ordinal) || text.StartsWith("音乐无法", StringComparison.Ordinal))
                    host.SetMusic(null);
            }, (tracks, index) => { });
            hostPlayer = player;
            player.TrackPlaying += file =>
            {
                try
                {
                    if (file.Length <= LanWire.MusicLimit && file.Length > 0)
                        host.SetMusic(file);
                    else
                        status.Text = file.Name + " · 无法作为联机音乐发送";
                }
                catch (IOException) { status.Text = file.Name + " · 音乐文件不可用"; }
                catch (UnauthorizedAccessException) { status.Text = file.Name + " · 音乐文件不可用"; }
            };
            UpdateVolume();
            return player.LoadFolder(Folder, null);
        }

        internal void Refresh(SnakeMatchState state, SnakeMatchState previous, LanClient client, int slot)
        {
            music = state.Music;
            bool active = state.Phase == MatchPhase.Playing;
            if (playing != active)
            {
                playing = active;
                if (hostPlayer != null)
                {
                    if (active)
                        hostPlayer.SetPlaying(true);
                    else
                        hostPlayer.StopAndRewind();
                }
            }
            UpdateVolume();
            if (client != null)
                UpdateGuest(client);
            if (previous == null || state.Round != previous.Round)
                return;
            var own = state.Players.First(player => player.Slot == slot);
            var last = previous.Players.First(player => player.Slot == slot);
            if (active && own.SkillUses > last.SkillUses)
                PlayEffect(1);
            else if (active && (own.Respawns > last.Respawns || own.Carrying != last.Carrying ||
                (own.Result == SnakeResult.Running && own.Body.Length > last.Body.Length)))
                PlayEffect(0);
            else if (state.Phase == MatchPhase.Finished && previous.Phase == MatchPhase.Playing)
                PlayEffect(2);
        }

        internal void UpdateVolume()
        {
            int master = music == null ? settings.RoomMasterVolume : music.Volume;
            double volume = master * settings.RoomMusicVolume / 10000.0;
            if (hostPlayer != null)
                hostPlayer.SetVolume((int)Math.Round(volume * 100));
            if (guestPlayer != null)
                guestPlayer.Volume = volume;
        }

        internal void ResetEffects()
        {
            for (int i = 0; i < effects.Length; i++)
                if (effects[i] != null)
                {
                    effects[i].Stop();
                    effects[i].Dispose();
                    effects[i] = null;
                }
        }

        private void PlayEffect(int level)
        {
            if (settings.RoomEffectsVolume == 0)
                return;
            if (effects[level] == null)
                effects[level] = new SoundPlayer(TypingEffects.CreateSound(level, settings.RoomEffectsVolume / 100.0));
            effects[level].Play();
        }

        private void UpdateGuest(LanClient client)
        {
            if (!playing || music.Track == 0)
            {
                if (track != 0)
                    StopGuest();
                status.Text = playing ? "等待房主音乐…" : "开局后播放房主音乐";
                return;
            }
            if (track != music.Track && failedTrack != music.Track)
                LoadGuest(client, music);
        }

        private async void LoadGuest(LanClient client, RoomMusicState current)
        {
            StopGuest();
            track = current.Track;
            var cancellation = new CancellationTokenSource();
            pending = cancellation;
            string directory = Path.Combine(Path.GetTempPath(), "DingshanTyping-Room");
            string path = Path.Combine(directory, Guid.NewGuid().ToString("N") + current.Extension);
            status.Text = "正在接收 " + current.Title;
            try
            {
                Directory.CreateDirectory(directory);
                await client.DownloadMusic(current, path, cancellation.Token);
                if (cancellation.IsCancellationRequested || pending != cancellation)
                    return;
                var player = new MediaPlayer();
                guestPlayer = player;
                localFile = path;
                UpdateVolume();
                player.MediaOpened += (sender, e) =>
                {
                    if (guestPlayer != player)
                        return;
                    if (player.NaturalDuration.HasTimeSpan)
                        player.Position = TimeSpan.FromMilliseconds(Math.Min(music.Position,
                            player.NaturalDuration.TimeSpan.TotalMilliseconds));
                    player.Play();
                    status.Text = "正在播放 " + current.Title;
                };
                player.MediaFailed += (sender, e) =>
                {
                    if (guestPlayer == player)
                        TrackFailed(current.Track, "无法播放房主的这首音乐");
                };
                player.Open(new Uri(path, UriKind.Absolute));
            }
            catch (Exception error)
            {
                if (!LanWire.IsConnectionError(error) && !(error is OperationCanceledException || error is UnauthorizedAccessException))
                    throw;
                if (!cancellation.IsCancellationRequested && pending == cancellation)
                    TrackFailed(current.Track, "未能接收房主音乐");
            }
            finally
            {
                if (localFile != path)
                    DeleteCache(path);
                if (pending == cancellation)
                    pending = null;
                cancellation.Dispose();
            }
        }

        private void TrackFailed(int id, string message)
        {
            StopGuest();
            failedTrack = id;
            status.Text = message;
        }

        private void StopGuest()
        {
            if (pending != null)
                pending.Cancel();
            pending = null;
            if (guestPlayer != null)
                guestPlayer.Close();
            guestPlayer = null;
            DeleteCache(localFile);
            localFile = null;
            track = 0;
        }

        private static void DeleteCache(string path)
        {
            if (path == null)
                return;
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
        }

        internal void Close()
        {
            if (hostPlayer != null)
                hostPlayer.Dispose();
            hostPlayer = null;
            StopGuest();
            ResetEffects();
            music = null;
            playing = false;
            failedTrack = 0;
            status.Text = "";
        }
    }
}
