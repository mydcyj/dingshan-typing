using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Media;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal enum PlaybackMode
    {
        Sequential,
        Shuffle,
        RepeatTrack
    }

    internal sealed class MusicPlayback : IDisposable
    {
        private static readonly HashSet<string> AudioExtensions = new HashSet<string>(
            new[] { ".mp3", ".wav", ".wma", ".m4a", ".aac", ".flac" }, StringComparer.OrdinalIgnoreCase);

        private readonly Action<string> showStatus;
        private readonly Action<FileInfo[], int> showPlaylist;
        private readonly Dispatcher dispatcher = Dispatcher.CurrentDispatcher;
        private readonly Random random = new Random();
        private string summary = "尚未选择音乐文件夹";
        private FileInfo[] tracks = new FileInfo[0];
        private int folderVersion;
        private int trackIndex;
        private int failedTracks;
        private bool playing;
        private bool opened;
        private double volume = 0.35;
        private MediaPlayer player;

        internal MusicPlayback(Action<string> showStatus, Action<FileInfo[], int> showPlaylist)
        {
            this.showStatus = showStatus;
            this.showPlaylist = showPlaylist;
        }

        internal PlaybackMode Mode { get; set; }
        internal event Action<FileInfo> TrackPlaying;
        internal bool IsPlaying { get { return playing; } }
        internal TimeSpan Position { get { return opened ? player.Position : TimeSpan.Zero; } }
        internal TimeSpan Duration
        {
            get { return opened && player.NaturalDuration.HasTimeSpan ? player.NaturalDuration.TimeSpan : TimeSpan.Zero; }
        }

        internal void Seek(double seconds)
        {
            if (Duration > TimeSpan.Zero)
                player.Position = TimeSpan.FromSeconds(Math.Max(0, Math.Min(Duration.TotalSeconds, seconds)));
        }

        internal void SetVolume(int percent)
        {
            volume = percent / 100.0;
            if (player != null)
                player.Volume = volume;
        }

        internal async Task LoadFolder(string path, string rememberedTrack)
        {
            int version = ++folderVersion;
            ClosePlayer();
            tracks = new FileInfo[0];
            trackIndex = 0;
            failedTracks = 0;
            showPlaylist(tracks, -1);
            showStatus("正在扫描音乐文件…");

            DirectoryInfo root;
            FileInfo[] found;
            try
            {
                root = new DirectoryInfo(path);
                found = await Task.Run(() => FindAudioFiles(root));
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException || ex is ArgumentException))
                    throw;
                if (version == folderVersion)
                {
                    summary = "无法访问音乐文件夹，请重新选择。";
                    showStatus(summary);
                }
                return;
            }

            if (version != folderVersion)
                return;
            tracks = found;
            int rememberedIndex = Array.FindIndex(tracks,
                file => string.Equals(file.FullName, rememberedTrack, StringComparison.OrdinalIgnoreCase));
            trackIndex = Math.Max(0, rememberedIndex);
            showPlaylist(tracks, tracks.Length == 0 ? -1 : trackIndex);
            summary = tracks.Length == 0
                ? root.Name + " · 没有识别到音乐"
                : string.Format("{0} · 找到 {1} 首", root.Name, tracks.Length);
            showStatus(summary);
            if (playing && tracks.Length > 0)
                OpenTrack();
        }

        internal static bool IsAudioExtension(string extension)
        {
            return extension != null && AudioExtensions.Contains(extension);
        }

        internal static FileInfo[] FindAudioFiles(DirectoryInfo root)
        {
            var files = new List<FileInfo>();
            var pending = new Stack<DirectoryInfo>();
            pending.Push(root);
            while (pending.Count > 0)
            {
                var directory = pending.Pop();
                try
                {
                    files.AddRange(directory.EnumerateFiles().Where(file => AudioExtensions.Contains(file.Extension)));
                    foreach (var child in directory.EnumerateDirectories())
                    {
                        if ((child.Attributes & FileAttributes.ReparsePoint) == 0)
                            pending.Push(child);
                    }
                }
                catch (IOException)
                {
                    if (directory == root) throw;
                }
                catch (UnauthorizedAccessException)
                {
                    if (directory == root) throw;
                }
            }
            return files.OrderBy(file => file.Name, StringComparer.OrdinalIgnoreCase)
                .ThenBy(file => file.FullName, StringComparer.OrdinalIgnoreCase).ToArray();
        }

        internal void SetPlaying(bool active)
        {
            if (playing == active)
                return;
            playing = active;
            if (tracks.Length == 0)
                return;

            if (playing)
            {
                if (player == null)
                {
                    failedTracks = 0;
                    OpenTrack();
                }
                else if (opened)
                {
                    PlayTrack();
                }
            }
            else
            {
                if (opened)
                    player.Pause();
                showStatus(summary);
            }
        }

        internal void StopAndRewind()
        {
            SetPlaying(false);
            ClosePlayer();
        }

        private void OpenTrack()
        {
            player = new MediaPlayer { Volume = volume };
            player.MediaOpened += OnMediaOpened;
            player.MediaEnded += OnMediaEnded;
            player.MediaFailed += OnMediaFailed;
            showStatus("正在读取 " + tracks[trackIndex].Name);
            try
            {
                player.Open(new Uri(tracks[trackIndex].FullName, UriKind.Absolute));
            }
            catch (Exception ex)
            {
                if (!(ex is IOException || ex is UnauthorizedAccessException
                    || ex is UriFormatException || ex is InvalidOperationException))
                    throw;
                TrackFailed();
            }
        }

        private void PlayTrack()
        {
            player.Play();
            showStatus("正在播放 " + tracks[trackIndex].Name);
            if (TrackPlaying != null)
                TrackPlaying(tracks[trackIndex]);
        }

        private void OnMediaOpened(object sender, EventArgs e)
        {
            if (sender != player)
                return;
            opened = true;
            if (playing)
                PlayTrack();
        }

        private void OnMediaEnded(object sender, EventArgs e)
        {
            if (sender != player)
                return;
            SelectTrack(Mode == PlaybackMode.RepeatTrack ? trackIndex : NextTrackIndex());
        }

        internal void SelectTrack(int index)
        {
            if (index < 0 || index >= tracks.Length)
                return;

            failedTracks = 0;
            ChangeTrack(index);
            if (playing)
                OpenTrack();
        }

        internal void NextTrack()
        {
            if (tracks.Length > 1)
                SelectTrack(NextTrackIndex());
        }

        private int NextTrackIndex()
        {
            if (Mode == PlaybackMode.Shuffle && tracks.Length > 1)
            {
                int index = random.Next(tracks.Length - 1);
                return index >= trackIndex ? index + 1 : index;
            }
            return (trackIndex + 1) % tracks.Length;
        }

        private void ChangeTrack(int index)
        {
            ClosePlayer();
            trackIndex = index;
            showPlaylist(tracks, trackIndex);
        }

        private void OnMediaFailed(object sender, ExceptionEventArgs e)
        {
            if (sender == player)
                TrackFailed();
        }

        private void TrackFailed()
        {
            ClosePlayer();
            if (!playing)
                return;
            if (++failedTracks >= tracks.Length)
            {
                showStatus("音乐无法播放，请检查音频格式或重新选择文件夹。");
                return;
            }

            ChangeTrack((trackIndex + 1) % tracks.Length);
            int version = folderVersion;
            dispatcher.BeginInvoke(new Action(() =>
            {
                if (version == folderVersion && playing && player == null && failedTracks < tracks.Length)
                    OpenTrack();
            }));
        }

        private void ClosePlayer()
        {
            if (player == null)
                return;
            player.MediaOpened -= OnMediaOpened;
            player.MediaEnded -= OnMediaEnded;
            player.MediaFailed -= OnMediaFailed;
            player.Close();
            player = null;
            opened = false;
        }

        public void Dispose()
        {
            folderVersion++;
            playing = false;
            ClosePlayer();
        }
    }
}
