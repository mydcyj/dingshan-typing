using System;
using System.Diagnostics;
using System.IO;
using System.Media;
using System.Windows;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    internal sealed class StartupThemeEffects : FrameworkElement, IDisposable
    {
        private static readonly Brush Steam = new RadialGradientBrush(Color.FromArgb(206, 255, 236, 210),
            Color.FromArgb(0, 255, 236, 210));
        private static readonly Pen Skyline = new Pen(new SolidColorBrush(Color.FromArgb(118, 249, 92, 191)), 1.5);
        private static readonly Geometry City = Geometry.Parse(
            "M-212,104 H-192 V91 H-179 V99 H-166 V78 H-156 V87 H-144 V67 H-134 V96 H-121 V83 H-104 V105 H-74 " +
            "M74,105 H96 V83 H107 V96 H119 V70 H134 V82 H149 V60 H159 V86 H172 V76 H187 V96 H197 V90 H212");
        private static readonly Brush Sunset = new LinearGradientBrush(Color.FromRgb(249, 154, 189),
            Color.FromRgb(248, 199, 161), 90);
        private static readonly Brush Palm = new SolidColorBrush(Color.FromRgb(113, 63, 130));
        private static readonly Pen MintTrace = new Pen(new SolidColorBrush(Color.FromRgb(83, 159, 146)), 1.3);
        private static readonly Geometry PalmSilhouette = Geometry.Parse(
            "M-6,1 Q7,-28 0,-58 Q-10,-72 -29,-59 Q-22,-76 -3,-66 Q-6,-80 -24,-80 " +
            "Q-5,-87 4,-70 Q17,-88 33,-73 Q14,-77 8,-66 Q29,-73 39,-53 Q22,-65 8,-62 " +
            "Q26,-62 28,-46 Q17,-56 8,-58 Q16,-27 3,1 Z");
        private static readonly Brush Paper = new SolidColorBrush(Color.FromRgb(251, 236, 211));
        private static readonly Brush Cinnabar = new SolidColorBrush(Color.FromRgb(177, 43, 37));
        private static readonly Brush JadeInk = new SolidColorBrush(Color.FromArgb(168, 41, 93, 83));
        private static readonly Brush FadedInk = new SolidColorBrush(Color.FromArgb(104, 81, 114, 100));
        private static readonly Geometry PrintWedge = Geometry.Parse("M-215,83 L-92,-63 -107,-7 -211,115 Z");
        private static readonly Geometry PrintCut = Geometry.Parse("M94,-74 L208,3 208,23 101,-48 Z");
        private static readonly Geometry ComicLeft = Geometry.Parse("M-219,70 L-142,-47 -168,22 -121,-8 -151,43 -102,25 -185,100 Z");
        private static readonly Geometry ComicRight = Geometry.Parse("M212,-83 L130,0 177,-21 143,30 192,12 157,53 216,24 Z");
        private static readonly Geometry ComicStar = Geometry.Parse("M0,-16 L5,-6 16,-5 8,3 10,15 0,10 -11,15 -9,3 -17,-5 -5,-6 Z");
        private static readonly Geometry MountainFar = Geometry.Parse(
            "M-213,81 Q-184,54 -171,49 Q-157,49 -134,26 Q-117,38 -104,48 Q-73,60 -61,87 " +
            "Q-35,90 -17,107 L-213,107 Z M211,107 H47 Q64,77 89,66 Q105,42 127,30 Q149,48 164,49 Q182,62 211,72 Z");
        private static readonly Geometry MountainNear = Geometry.Parse(
            "M-213,92 Q-193,93 -160,72 Q-142,53 -132,60 Q-121,72 -99,78 Q-76,100 -64,107 L-213,107 Z " +
            "M210,107 H84 Q102,97 121,79 Q141,72 168,55 Q184,64 210,78 Z");
        private readonly UiTheme theme;
        private readonly Brush accent;
        private readonly Pen fine, bold;
        private readonly Stopwatch clock = new Stopwatch();
        private SoundPlayer sound;
        private MemoryStream audio;
        private bool confirmed;

        internal StartupThemeEffects(Window window, UiTheme theme)
        {
            this.theme = theme;
            accent = (Brush)window.FindResource("Accent");
            fine = new Pen((Brush)window.FindResource("Line"), 1);
            bold = new Pen(accent, 2);
            IsHitTestVisible = false;
            Visibility = Visibility.Collapsed;
        }

        internal void Play(bool confirmation)
        {
            Dispose();
            confirmed = confirmation;
            Visibility = Visibility.Visible;
            clock.Restart();
            CompositionTarget.Rendering += OnFrame;
            audio = CreateCue(theme, confirmation);
            sound = new SoundPlayer(audio);
            sound.Play();
        }

        private void OnFrame(object sender, EventArgs e)
        {
            InvalidateVisual();
        }

        public void Dispose()
        {
            CompositionTarget.Rendering -= OnFrame;
            clock.Reset();
            Visibility = Visibility.Collapsed;
            if (sound != null)
            {
                sound.Stop();
                sound.Dispose();
                sound = null;
            }
            if (audio != null)
            {
                audio.Dispose();
                audio = null;
            }
        }

        protected override void OnRender(DrawingContext drawing)
        {
            double t = clock.Elapsed.TotalSeconds;
            double scale = Math.Min(1, Math.Min(ActualWidth / 440, ActualHeight / 350));
            double arrive = Math.Min(1, t * 5);
            if (scale <= 0 || arrive <= 0)
                return;
            drawing.PushTransform(new TranslateTransform(ActualWidth / 2, ActualHeight / 2));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.PushOpacity(arrive);
            if (theme == UiTheme.Steampunk)
                DrawSteam(drawing, t);
            else if (theme == UiTheme.Cyberpunk)
                DrawCyber(drawing, t);
            else if (theme == UiTheme.Minimal)
                DrawMinimal(drawing, arrive);
            else if (theme == UiTheme.Vaporwave)
                DrawVaporwave(drawing, t);
            else if (theme == UiTheme.Constructivist)
                DrawConstructivist(drawing, t);
            else if (theme == UiTheme.Persona5)
                DrawComic(drawing, t);
            else if (theme == UiTheme.ChineseStyle)
                DrawInk(drawing, t);
            else
                DrawModern(drawing, t);
            drawing.Pop();
            drawing.Pop();
            drawing.Pop();
        }

        private void DrawSteam(DrawingContext drawing, double t)
        {
            var airship = new Point(-168 + t * 28, -76);
            drawing.DrawEllipse(null, fine, airship, 31, 9);
            drawing.DrawEllipse(null, fine, airship, 19, 9);
            drawing.DrawLine(fine, new Point(airship.X - 30, airship.Y), new Point(airship.X + 31, airship.Y));
            drawing.DrawLine(fine, new Point(airship.X - 8, airship.Y + 7), new Point(airship.X - 5, airship.Y + 13));
            drawing.DrawLine(fine, new Point(airship.X + 8, airship.Y + 7), new Point(airship.X + 5, airship.Y + 13));
            drawing.DrawRectangle(null, fine, new Rect(airship.X - 7, airship.Y + 12, 14, 4));
            drawing.DrawLine(fine, new Point(-209, 70), new Point(208, 70));
            drawing.DrawLine(fine, new Point(-209, 77), new Point(208, 77));
            DrawGear(drawing, -151, 12, 52, 14, t * 128);
            DrawGear(drawing, -184, 74, 27, 9, -t * 200);
            DrawGear(drawing, 147, -21, 46, 13, -t * 140);
            DrawGear(drawing, 179, 35, 28, 9, t * 200);
            DrawGear(drawing, 137, 74, 23, 8, -t * 225);
            for (int i = 0; i < 10; i++)
            {
                double age = t - .10 - i * .031;
                if (age <= 0 || age >= .43)
                    continue;
                double progress = age / .43;
                drawing.PushOpacity((1 - progress) * Math.Min(1, progress * 7));
                int side = i % 2 == 0 ? -1 : 1;
                var center = new Point(side * (170 + 53 * progress + (i % 3) * 5), 38 - progress * (91 + i % 4 * 16));
                drawing.DrawEllipse(Steam, null, center, 10 + progress * 27, 8 + progress * 18);
                drawing.Pop();
            }
        }

        private void DrawGear(DrawingContext drawing, double x, double y, double radius, int teeth, double angle)
        {
            drawing.PushTransform(new TranslateTransform(x, y));
            drawing.PushTransform(new RotateTransform(angle));
            var profile = new StreamGeometry();
            using (var path = profile.Open())
            {
                for (int point = 0; point < teeth * 4; point++)
                {
                    double a = point * 2 * Math.PI / (teeth * 4);
                    double r = point % 4 == 1 || point % 4 == 2 ? radius : radius * .84;
                    var position = new Point(Math.Cos(a) * r, Math.Sin(a) * r);
                    if (point == 0)
                        path.BeginFigure(position, false, true);
                    else
                        path.LineTo(position, true, false);
                }
            }
            drawing.DrawGeometry(null, bold, profile);
            drawing.DrawEllipse(null, fine, new Point(), radius * .67, radius * .67);
            drawing.DrawEllipse(null, bold, new Point(), radius * .25, radius * .25);
            for (int i = 0; i < 4; i++)
            {
                drawing.DrawLine(fine, new Point(0, radius * .28), new Point(0, radius * .63));
                drawing.PushTransform(new RotateTransform(90));
            }
            for (int i = 0; i < 6; i++)
                drawing.Pop();
        }

        private void DrawCyber(DrawingContext drawing, double t)
        {
            double sweep = (t * 2.6 % 1) * 160 - 80;
            drawing.DrawGeometry(null, Skyline, City);
            foreach (int side in new[] { -1, 1 })
            {
                var corner = new Point(side * 135, -65);
                drawing.DrawLine(bold, corner, new Point(side * 161, -65));
                drawing.DrawLine(bold, new Point(side * 161, -65), new Point(side * 192, -37));
                drawing.DrawLine(fine, new Point(side * 192, -37), new Point(side * 192, 75));
                drawing.DrawLine(fine, new Point(side * 192, 75), new Point(side * 116, 75));
                for (int i = 0; i < 5; i++)
                {
                    double y = -31 + 19 * i;
                    drawing.DrawLine(fine, new Point(side * 187, y), new Point(side * 178, y));
                }
                drawing.DrawRectangle(accent, null, new Rect(side < 0 ? -196 : 188, sweep, 8, 3));
                drawing.DrawLine(bold, new Point(side * 188, sweep), new Point(side * 140, sweep));
            }
        }

        private void DrawMinimal(DrawingContext drawing, double arrive)
        {
            double extent = 150 * (1 - Math.Pow(1 - arrive, 3));
            drawing.DrawLine(fine, new Point(-extent, 77), new Point(extent, 77));
            drawing.DrawLine(bold, new Point(-16, 77), new Point(16, 77));
            drawing.DrawEllipse(null, fine, new Point(-extent, 77), 3, 3);
            drawing.DrawEllipse(null, fine, new Point(extent, 77), 3, 3);
        }

        private void DrawVaporwave(DrawingContext drawing, double t)
        {
            var sun = new Point(-158, -18 - 16 * Math.Min(1, t * 3));
            var bands = new GeometryGroup();
            var sunBounds = new Rect(sun.X - 45, sun.Y - 45, 90, 90);
            bands.Children.Add(new RectangleGeometry(new Rect(sunBounds.X, sunBounds.Y, sunBounds.Width, 58)));
            for (int i = 0; i < 5; i++)
                bands.Children.Add(new RectangleGeometry(new Rect(sunBounds.X, sun.Y + 16 + i * 6, sunBounds.Width, 4 - i * .55)));
            drawing.PushClip(new EllipseGeometry(sun, 45, 45));
            drawing.PushClip(bands);
            drawing.DrawRectangle(Sunset, null, sunBounds);
            drawing.Pop();
            drawing.Pop();

            drawing.PushTransform(new TranslateTransform(158, 49));
            drawing.PushTransform(new RotateTransform(-4 + 6 * Math.Sin(t * 5)));
            drawing.DrawGeometry(Palm, null, PalmSilhouette);
            drawing.Pop();
            drawing.Pop();

            drawing.DrawLine(MintTrace, new Point(-212, 60), new Point(211, 60));
            for (int i = 1; i <= 4; i++)
            {
                double y = 60 + i * i * 2;
                drawing.DrawLine(fine, new Point(-212, y), new Point(211, y));
            }
            for (int i = -3; i <= 3; i++)
                drawing.DrawLine(fine, new Point(i * 24, 60), new Point(i * 68, 92));

            double scan = 69 + t * 180 % 29;
            drawing.DrawLine(MintTrace, new Point(-204, scan), new Point(-109, scan));
            drawing.DrawLine(MintTrace, new Point(109, scan), new Point(204, scan));
        }

        private void DrawModern(DrawingContext drawing, double t)
        {
            if (confirmed)
                return;
            foreach (int side in new[] { -1, 1 })
            {
                drawing.DrawLine(fine, new Point(side * 210, 46), new Point(side * 169, 85));
                drawing.DrawLine(fine, new Point(side * 169, 85), new Point(side * 123, 85));
                double x = side * (210 - Math.Min(1, t * 2) * 42);
                drawing.DrawLine(bold, new Point(x, 85), new Point(x - side * 14, 85));
            }
        }

        private void DrawConstructivist(DrawingContext drawing, double t)
        {
            double settle = 1 - Math.Min(1, t * 4.5);
            drawing.PushTransform(new TranslateTransform(-62 * settle, 25 * settle));
            drawing.DrawGeometry(accent, null, PrintWedge);
            drawing.DrawLine(fine, new Point(-211, 53), new Point(-128, -41));
            drawing.DrawLine(fine, new Point(-210, 35), new Point(-160, -21));
            drawing.Pop();

            drawing.PushTransform(new TranslateTransform(62 * settle, -25 * settle));
            drawing.DrawGeometry(accent, null, PrintCut);
            drawing.DrawLine(fine, new Point(119, -71), new Point(211, -9));
            drawing.DrawEllipse(null, bold, new Point(157, 48), 24, 24);
            drawing.DrawLine(fine, new Point(137, 67), new Point(182, 26));
            drawing.Pop();
            drawing.DrawRectangle(accent, null, new Rect(-75, 92, 150 * Math.Min(1, t * 3.5), 4));
        }

        private void DrawComic(DrawingContext drawing, double t)
        {
            double bounce = Math.Sin(t * 16) * Math.Max(0, 1 - t * 2) * 5;
            drawing.PushTransform(new TranslateTransform(-bounce, bounce));
            drawing.DrawGeometry(Paper, bold, ComicLeft);
            drawing.Pop();
            drawing.PushTransform(new TranslateTransform(bounce, -bounce));
            drawing.DrawGeometry(accent, null, ComicRight);
            drawing.Pop();
            DrawComicStar(drawing, -143, 62, .92, 16 + bounce, Paper);
            DrawComicStar(drawing, 143, -54, .65, -16 - bounce, Paper);
            DrawComicStar(drawing, 184, 40, .45, bounce, accent);
            double sweep = Math.Min(1, t * 3.4);
            drawing.DrawLine(bold, new Point(-186, 96), new Point(-186 + 113 * sweep, 88));
            drawing.DrawLine(bold, new Point(79, 85), new Point(79 + 131 * sweep, 71));
        }

        private void DrawComicStar(DrawingContext drawing, double x, double y, double scale, double angle, Brush fill)
        {
            drawing.PushTransform(new TranslateTransform(x, y));
            drawing.PushTransform(new ScaleTransform(scale, scale));
            drawing.PushTransform(new RotateTransform(angle));
            drawing.DrawGeometry(fill, bold, ComicStar);
            drawing.Pop();
            drawing.Pop();
            drawing.Pop();
        }

        private void DrawInk(DrawingContext drawing, double t)
        {
            double spread = Math.Min(1, t * 3.5);
            drawing.PushClip(new RectangleGeometry(new Rect(-213, 21, 426, 89)));
            drawing.PushTransform(new TranslateTransform(0, (1 - spread) * 24));
            drawing.DrawGeometry(FadedInk, null, MountainFar);
            drawing.DrawGeometry(JadeInk, null, MountainNear);
            drawing.Pop();
            drawing.Pop();
            drawing.DrawLine(fine, new Point(-206, 114), new Point(-65, 114));
            drawing.DrawLine(fine, new Point(65, 114), new Point(205, 114));
            double sealY = -70 - 27 * Math.Max(0, 1 - t * 4);
            var seal = new Rect(151, sealY, 31, 31);
            drawing.DrawRectangle(Cinnabar, null, seal);
            var carving = new Pen(Paper, 1.35);
            drawing.DrawRectangle(null, carving, new Rect(154, sealY + 3, 25, 25));
            drawing.DrawLine(carving, new Point(166, sealY + 7), new Point(166, sealY + 24));
            drawing.DrawLine(carving, new Point(158, sealY + 16), new Point(175, sealY + 16));
            drawing.DrawLine(carving, new Point(158, sealY + 7), new Point(175, sealY + 7));
            drawing.DrawLine(carving, new Point(158, sealY + 24), new Point(175, sealY + 24));
        }

        internal static MemoryStream CreateCue(UiTheme theme, bool confirmation)
        {
            const int sampleRate = 22050;
            const int count = sampleRate / 2;
            var stream = new MemoryStream();
            var random = new Random(319);
            using (var writer = new BinaryWriter(stream, System.Text.Encoding.ASCII, true))
            {
                writer.Write(0x46464952);
                writer.Write(36 + count * 2);
                writer.Write(0x45564157);
                writer.Write(0x20746D66);
                writer.Write(16);
                writer.Write((short)1);
                writer.Write((short)1);
                writer.Write(sampleRate);
                writer.Write(sampleRate * 2);
                writer.Write((short)2);
                writer.Write((short)16);
                writer.Write(0x61746164);
                writer.Write(count * 2);
                double previousNoise = 0;
                for (int i = 0; i < count; i++)
                {
                    double t = (double)i / sampleRate;
                    double sample;
                    double root = confirmation ? 1.25 : 1;
                    if (theme == UiTheme.Steampunk)
                    {
                        double tooth = t % .049;
                        double noise = random.NextDouble() * 2 - 1;
                        double steam = Math.Max(0, Math.Sin(Math.PI * (t - .12) / .34));
                        sample = Tone(tooth, 860 * root, .006) * .16 + Tone(tooth, 127, .012) * .20 +
                            (noise - previousNoise * .5) * steam * .12 + Tone(t - .31, 318 * root, .08) * .10;
                        previousNoise = noise;
                    }
                    else if (theme == UiTheme.Cyberpunk)
                        sample = Tone(t, 108 * root + t * 1280, .15) * .16 +
                            Tone(t - .095, 936 * root, .045) * .12 + Tone(t - .22, 1417 * root, .072) * .11;
                    else if (theme == UiTheme.Minimal)
                        sample = Tone(t, 587 * root, .080) * .19 + Tone(t - .14, 880 * root, .10) * .13;
                    else if (theme == UiTheme.Vaporwave)
                    {
                        double sway = 1 + .005 * Math.Sin(2 * Math.PI * t * 6);
                        double first = confirmation ? 329.63 : 261.63;
                        double middle = confirmation ? 440 : 329.63;
                        double last = confirmation ? 523.25 : 392;
                        sample = SoftSynth(t, first * sway) * .15 + SoftSynth(t - .082, middle * sway) * .13 +
                            SoftSynth(t - .165, last * sway) * .13 + Tone(t - .247, last / 2, .14) * .08;
                    }
                    else if (theme == UiTheme.Constructivist)
                    {
                        double strike = confirmation ? .10 : .13;
                        sample = PrintStamp(t, 112 * root) * .28 + PrintStamp(t - strike, 148 * root) * .24 +
                            Tone(t - strike * 2, 278 * root, .077) * .16 + Tone(t - strike * 2, 607 * root, .044) * .10;
                    }
                    else if (theme == UiTheme.Persona5)
                    {
                        double riff = confirmation ? 1.122 : 1;
                        double snare = random.NextDouble() * 2 - 1;
                        double hit = t - .15;
                        double snap = hit < 0 ? 0 : Math.Min(1, hit * 1100) * Math.Exp(-hit / .018);
                        sample = PrintStamp(t, 83 * riff) * .24 + SoftSynth(t - .071, 220 * riff) * .136 +
                            snare * snap * .208 + Tone(t - .19, 293.66 * riff, .095) * .18 +
                            Tone(t - .249, 415.30 * riff, .092) * .14;
                    }
                    else if (theme == UiTheme.ChineseStyle)
                    {
                        double first = confirmation ? 392 : 329.63;
                        sample = Zither(t, first) * .21 + Zither(t - .11, first * 1.4983) * .14 +
                            Zither(t - .209, first * 2) * .14 + Tone(t - .215, 109 * root, .102) * .10;
                    }
                    else
                        sample = Tone(t, 230 * root, .10) * .19 + Tone(t - .075, 413 * root, .11) * .14 +
                            Tone(t - .19, 620 * root, .11) * .12;
                    double fade = Math.Min(1, (count - i) / (sampleRate * .065));
                    writer.Write((short)(sample * fade * short.MaxValue));
                }
            }
            stream.Position = 0;
            return stream;
        }

        private static double Tone(double time, double hz, double decay)
        {
            return time <= 0 ? 0 : Math.Sin(2 * Math.PI * hz * time) * Math.Min(1, time * 550) * Math.Exp(-time / decay);
        }

        private static double SoftSynth(double time, double frequency)
        {
            if (time <= 0)
                return 0;
            double envelope = Math.Min(1, time * 72) * Math.Exp(-time / .18);
            return (Math.Sin(2 * Math.PI * frequency * time) + .24 * Math.Sin(4 * Math.PI * frequency * time)) * envelope;
        }

        private static double PrintStamp(double time, double pitch)
        {
            if (time <= 0)
                return 0;
            return Tone(time, pitch + 38 * Math.Exp(-time * 85), .043) + Tone(time, pitch * 4.7, .011) * .32;
        }

        private static double Zither(double time, double pitch)
        {
            return Tone(time, pitch, .13) + Tone(time, pitch * 2.01, .059) * .33 +
                Tone(time, pitch * 3.02, .024) * .17;
        }
    }
}
