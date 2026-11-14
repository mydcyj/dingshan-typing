using System;
using System.IO;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    internal sealed class SnakeMusicPlayback : IDisposable
    {
        private MediaPlayer player;
        private string path;
        private bool playing;
        private bool opened;
        private bool unavailable;

        internal void SetPlaying(bool active, double volume)
        {
            bool changed = playing != active;
            playing = active;
            if (player == null && active && !unavailable)
                Open();
            if (player == null)
                return;
            if (player.Volume != volume)
                player.Volume = volume;
            if (!changed || !opened)
                return;
            if (active)
                player.Play();
            else
                player.Stop();
        }

        private void Open()
        {
            try
            {
                // WPF's audio player opens WAV files by URI, so extract the embedded loop once.
                path = Path.Combine(Path.GetTempPath(), "DingshanTyping-Snake-" + Guid.NewGuid().ToString("N") + ".wav");
                using (var source = typeof(SnakeMusicPlayback).Assembly.GetManifestResourceStream("SnakeMusic.wav"))
                using (var output = new FileStream(path, FileMode.CreateNew))
                    source.CopyTo(output);
                player = new MediaPlayer();
                player.MediaOpened += (sender, e) =>
                {
                    opened = true;
                    if (playing)
                        player.Play();
                };
                player.MediaEnded += (sender, e) =>
                {
                    if (!playing)
                        return;
                    player.Position = TimeSpan.Zero;
                    player.Play();
                };
                player.MediaFailed += (sender, e) => MarkUnavailable();
                player.Open(new Uri(path, UriKind.Absolute));
            }
            catch (IOException) { MarkUnavailable(); }
            catch (UnauthorizedAccessException) { MarkUnavailable(); }
        }

        private void MarkUnavailable()
        {
            unavailable = true;
            Dispose();
        }

        public void Dispose()
        {
            if (player != null)
            {
                player.Close();
                player = null;
            }
            opened = false;
            if (path == null)
                return;
            try { File.Delete(path); }
            catch (IOException) { }
            catch (UnauthorizedAccessException) { }
            path = null;
        }
    }
}
