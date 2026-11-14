using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal sealed class TypingEffects : IDisposable
    {
        private readonly Window window;
        private readonly Canvas echo;
        private readonly TextBlock passage;
        private readonly UiTheme theme;
        private readonly Brush[] echoBrushes;
        private readonly TranslateTransform shake = new TranslateTransform();
        private readonly SoundPlayer[] sounds = new SoundPlayer[3];
        private readonly DispatcherTimer windowShake = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        private readonly Stopwatch shakeClock = new Stopwatch();
        private Point origin;
        private double windowStrength;
        private double windowDuration;

        internal TypingEffects(Window window, TextBlock passage, Canvas echo, UiTheme theme)
        {
            this.window = window;
            this.theme = theme;
            ((FrameworkElement)window.Content).RenderTransform = shake;
            this.echo = echo;
            this.passage = passage;
            echoBrushes = new[] { (Brush)window.FindResource("WordEchoPrimary"), (Brush)window.FindResource("WordEchoSecondary") };
            windowShake.Tick += (sender, e) => MoveWindow();
            window.PreviewMouseDown += (sender, e) => StopWindowShake();
            window.StateChanged += (sender, e) => StopWindowShake();
        }

        internal void Play(TextPointer firstLetter, string word, bool complete, TextPointer hitLetter = null)
        {
            int level = complete ? 2 : word != null ? 1 : 0;
            if (sounds[level] == null)
                sounds[level] = new SoundPlayer(ThemedTypingAudio.CreateSound(theme, level));
            sounds[level].Play();
            if (!SystemParameters.ClientAreaAnimation)
                return;
            if (theme == UiTheme.Magritte)
            {
                DrawImpact(hitLetter ?? firstLetter, level);
                return;
            }
            if (word != null && firstLetter != null)
                Echo(firstLetter, word);
            DrawImpact(hitLetter ?? firstLetter, level);
            if (level > 0)
                ShakeWindow(complete ? 11 : 3.5, complete ? 390 : 160);
        }

        private void DrawImpact(TextPointer hitLetter, int level)
        {
            if (echo.ActualWidth <= 0 || echo.ActualHeight <= 0)
                return;
            var center = new Point(echo.ActualWidth / 2, echo.ActualHeight / 2);
            if (hitLetter != null)
            {
                Rect bounds = hitLetter.GetCharacterRect(LogicalDirection.Forward);
                if (!bounds.IsEmpty)
                    center = passage.TranslatePoint(new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2), echo);
            }
            double width = Math.Min(echo.ActualWidth, level == 2 ? 186 : level == 1 ? 107 : 46);
            double height = Math.Min(echo.ActualHeight, level == 2 ? 104 : level == 1 ? 73 : 41);
            var shape = new TypingEffectVisuals(theme, level, echoBrushes[0], echoBrushes[1])
            {
                Width = width,
                Height = height,
                IsHitTestVisible = false
            };
            Canvas.SetLeft(shape, Math.Max(0, Math.Min(echo.ActualWidth - width, center.X - width / 2)));
            Canvas.SetTop(shape, Math.Max(0, Math.Min(echo.ActualHeight - height, center.Y - height / 2)));
            echo.Children.Add(shape);
            var life = new DoubleAnimation(0, 1, TimeSpan.FromMilliseconds(level == 2 ? 435 : level == 1 ? 265 : 112));
            life.Completed += (sender, e) => echo.Children.Remove(shape);
            shape.BeginAnimation(TypingEffectVisuals.ProgressProperty, life);
        }

        internal void ShakeWindow(double strength, double duration)
        {
            if (window.IsVisible && window.WindowState == WindowState.Normal &&
                !double.IsNaN(window.Left) && !double.IsNaN(window.Top))
            {
                if (!windowShake.IsEnabled)
                    origin = new Point(window.Left, window.Top);
                windowStrength = strength;
                windowDuration = duration;
                shakeClock.Restart();
                windowShake.Start();
                MoveWindow();
            }
            else
            {
                shake.BeginAnimation(TranslateTransform.XProperty, Shake(strength, duration));
                shake.BeginAnimation(TranslateTransform.YProperty, Shake(strength * -0.6, duration));
            }
        }

        private void MoveWindow()
        {
            double progress = shakeClock.Elapsed.TotalMilliseconds / windowDuration;
            if (progress >= 1)
            {
                StopWindowShake();
                return;
            }
            double offset = windowStrength * (1 - progress) * Math.Cos(progress * 8 * Math.PI);
            window.Left = origin.X + offset;
            window.Top = origin.Y - offset * 0.6;
        }

        private void StopWindowShake()
        {
            if (!windowShake.IsEnabled)
                return;
            windowShake.Stop();
            shakeClock.Stop();
            if (window.IsVisible && window.WindowState == WindowState.Normal)
            {
                window.Left = origin.X;
                window.Top = origin.Y;
            }
        }

        private void Echo(TextPointer firstLetter, string word)
        {
            Rect bounds = firstLetter.GetCharacterRect(LogicalDirection.Forward);
            if (bounds.IsEmpty)
                return;
            Point origin = passage.TranslatePoint(bounds.Location, echo);
            for (int i = 0; i < echoBrushes.Length; i++)
            {
                double direction = i == 0 ? -1 : 1;
                var shadow = new TextBlock
                {
                    Text = word, FontFamily = passage.FontFamily, FontSize = passage.FontSize,
                    Foreground = echoBrushes[i]
                };
                Canvas.SetLeft(shadow, origin.X + direction * 2);
                Canvas.SetTop(shadow, origin.Y);
                echo.Children.Add(shadow);
                var drift = new TranslateTransform();
                shadow.RenderTransform = drift;
                var duration = TimeSpan.FromMilliseconds(220);
                drift.BeginAnimation(TranslateTransform.XProperty, new DoubleAnimation(0, direction * 10, duration));
                drift.BeginAnimation(TranslateTransform.YProperty, new DoubleAnimation(0, -7, duration));
                var fade = new DoubleAnimation(0.58, 0, duration);
                fade.Completed += (sender, e) => echo.Children.Remove(shadow);
                shadow.BeginAnimation(UIElement.OpacityProperty, fade);
            }
        }

        private static DoubleAnimationUsingKeyFrames Shake(double strength, double duration)
        {
            var motion = new DoubleAnimationUsingKeyFrames
            {
                Duration = TimeSpan.FromMilliseconds(duration), IsAdditive = true, FillBehavior = FillBehavior.Stop
            };
            for (int i = 0; i <= 8; i++)
                motion.KeyFrames.Add(new LinearDoubleKeyFrame(strength * (8 - i) / 8 * (i % 2 == 0 ? 1 : -1),
                    KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(i * duration / 8))));
            return motion;
        }

        internal static MemoryStream CreateSound(int level, double volume = 1)
        {
            const int sampleRate = 22050;
            int count = sampleRate * (level == 0 ? 36 : level == 1 ? 170 : 410) / 1000;
            var stream = new MemoryStream();
            var writer = new BinaryWriter(stream);
            writer.Write(0x46464952); // RIFF
            writer.Write(36 + count * 2);
            writer.Write(0x45564157); // WAVE
            writer.Write(0x20746D66); // fmt
            writer.Write(16);
            writer.Write((short)1);
            writer.Write((short)1);
            writer.Write(sampleRate);
            writer.Write(sampleRate * 2);
            writer.Write((short)2);
            writer.Write((short)16);
            writer.Write(0x61746164); // data
            writer.Write(count * 2);
            for (int i = 0; i < count; i++)
            {
                double time = (double)i / sampleRate;
                double sample = Tone(time, 220, 0.009) * 0.35 + Tone(time, 980, 0.005) * 0.17;
                if (level > 0)
                    sample += Tone(time, 370, 0.035) * 0.31 + Tone(time - 0.023, 587, 0.035) * 0.23;
                if (level > 1)
                    sample += Tone(time - 0.055, 740, 0.071) * 0.29 + Tone(time - 0.108, 988, 0.09) * 0.23;
                writer.Write((short)(sample * volume * short.MaxValue));
            }
            writer.Flush();
            stream.Position = 0;
            return stream;
        }

        private static double Tone(double time, double frequency, double decay)
        {
            return time < 0 ? 0 : Math.Sin(time * frequency * 2 * Math.PI) *
                Math.Min(1, time * 2000) * Math.Exp(-time / decay);
        }

        internal void Reset()
        {
            foreach (var sound in sounds)
                if (sound != null)
                    sound.Stop();
            echo.Children.Clear();
            StopWindowShake();
            shake.BeginAnimation(TranslateTransform.XProperty, null);
            shake.BeginAnimation(TranslateTransform.YProperty, null);
        }

        public void Dispose()
        {
            Reset();
            foreach (var sound in sounds)
                if (sound != null)
                    sound.Dispose();
        }
    }
}
