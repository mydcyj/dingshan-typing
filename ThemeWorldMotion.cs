using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    // Animated scenery stays behind controls and has no input or gameplay state.
    internal sealed class ThemeWorldMotion
    {
        private readonly Window window;
        private readonly Func<bool> lowEffects;
        private readonly WorldScene background, header;
        private readonly Stopwatch elapsed = new Stopwatch();
        private readonly DispatcherTimer timer;
        private bool closed;

        internal static void Attach(Window window, UiTheme theme, Func<bool> lowEffects)
        {
            new ThemeWorldMotion(window, theme, lowEffects);
        }

        private ThemeWorldMotion(Window window, UiTheme theme, Func<bool> lowEffects)
        {
            this.window = window;
            this.lowEffects = lowEffects;
            background = new WorldScene(theme, false) { Name = "ThemeWorldBackground", Opacity = .74 };
            header = new WorldScene(theme, true) { Name = "ThemeWorldHeader", HorizontalAlignment = HorizontalAlignment.Right };
            ((Grid)window.FindName("BackgroundLayers")).Children.Add(background);
            var hero = (Grid)window.FindName("PracticeHero");
            Grid.SetColumnSpan(header, Math.Max(1, hero.ColumnDefinitions.Count));
            Grid.SetRowSpan(header, Math.Max(1, hero.RowDefinitions.Count));
            Panel.SetZIndex(header, 0);
            hero.Children.Add(header);
            hero.SizeChanged += ResizeHeader;
            timer = new DispatcherTimer(DispatcherPriority.Background) { Interval = TimeSpan.FromMilliseconds(1000.0 / 24) };
            timer.Tick += Tick;
            window.Loaded += Update;
            window.StateChanged += Update;
            window.IsVisibleChanged += VisibilityChanged;
            window.Closed += Close;
            ((FrameworkElement)window.FindName("PracticePage")).IsVisibleChanged += VisibilityChanged;
            var checkbox = window.FindName("SnakeLowEffects") as System.Windows.Controls.Primitives.ButtonBase;
            if (checkbox != null) checkbox.Click += Update;
            SystemParameters.StaticPropertyChanged += SystemChanged;
            if (window.IsLoaded) Update(null, EventArgs.Empty);
        }

        private void ResizeHeader(object sender, SizeChangedEventArgs e)
        {
            header.Width = Math.Min(440, e.NewSize.Width);
            header.Opacity = e.NewSize.Width < 500 ? .23 : .92;
        }

        private void VisibilityChanged(object sender, DependencyPropertyChangedEventArgs e) { Update(sender, EventArgs.Empty); }
        private void SystemChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ClientAreaAnimation") Update(sender, EventArgs.Empty);
        }

        private bool CanRun()
        {
            return !closed && window.IsLoaded && window.IsVisible && window.WindowState != WindowState.Minimized
                && SystemParameters.ClientAreaAnimation && (lowEffects == null || !lowEffects());
        }

        private void Update(object sender, EventArgs e)
        {
            if (CanRun()) { elapsed.Start(); timer.Start(); }
            else { elapsed.Stop(); timer.Stop(); }
        }

        private void Tick(object sender, EventArgs e)
        {
            if (!CanRun()) { Update(sender, e); return; }
            double seconds = elapsed.Elapsed.TotalSeconds;
            if (background.IsVisible) background.Advance(seconds);
            if (header.IsVisible) header.Advance(seconds);
        }

        private void Close(object sender, EventArgs e)
        {
            closed = true;
            timer.Stop();
            elapsed.Stop();
            timer.Tick -= Tick;
            window.Loaded -= Update;
            window.StateChanged -= Update;
            window.IsVisibleChanged -= VisibilityChanged;
            window.Closed -= Close;
            ((FrameworkElement)window.FindName("PracticePage")).IsVisibleChanged -= VisibilityChanged;
            ((Grid)window.FindName("PracticeHero")).SizeChanged -= ResizeHeader;
            var checkbox = window.FindName("SnakeLowEffects") as System.Windows.Controls.Primitives.ButtonBase;
            if (checkbox != null) checkbox.Click -= Update;
            SystemParameters.StaticPropertyChanged -= SystemChanged;
        }

        internal sealed class WorldScene : FrameworkElement
        {
            private readonly UiTheme theme;
            private readonly bool hero;
            private readonly Dictionary<string, DrawingImage> art = new Dictionary<string, DrawingImage>();
            private readonly Brush white = Paint("FFF3DC"), dark = Paint("191422"), cyan = Paint("68F5ED"), pink = Paint("F05D9E");
            private readonly Brush brass = Paint("D7AA65"), iron = Paint("49382C"), steam = Paint("E9D4B4");
            private readonly Brush ink = Paint("37655A"), paper = Paint("F4ECD9"), red = Paint("BA3329"), violet = Paint("8C61C7");
            private readonly Pen trace, copper, jade, redPen;
            private readonly Pen ironTwelve, ironThirteen, ironTwo, violetOne, violetTwo, violetFour;
            private readonly Pen redSix, redTwelve, redEight, redEighteen, darkTwo;
            private readonly Geometry star = Shape("M0,-18 L5,-5 19,-4 9,6 11,21 0,12 -13,20 -10,5 -22,-4 -6,-6 Z");
            private readonly Geometry bolt = Shape("M0,0 L35,-18 23,2 48,-6 3,29 15,7 -8,14 Z");
            private readonly Geometry bird = Shape("M-11,3 Q-6,-3 0,1 Q5,-5 12,-2");
            private readonly Geometry palm = Shape("M0,0 Q10,-29 7,-58 Q-12,-77 -30,-57 Q-22,-83 6,-67 Q-2,-89 -15,-91 Q8,-96 12,-71 Q29,-88 45,-72 Q26,-80 16,-64 Q35,-72 47,-51 Q27,-67 16,-58 Q32,-59 34,-44 Q23,-55 16,-53 Q16,-27 6,0 Z");
            private double seconds;

            internal WorldScene(UiTheme theme, bool hero)
            {
                this.theme = theme;
                this.hero = hero;
                IsHitTestVisible = false;
                Focusable = false;
                ClipToBounds = true;
                trace = Stroke(cyan, 1.2); copper = Stroke(brass, 1.8);
                jade = Stroke(ink, 1.15); redPen = Stroke(red, 1.4);
                ironTwelve = Stroke(iron, 12); ironThirteen = Stroke(iron, 13); ironTwo = Stroke(iron, 2);
                violetOne = Stroke(violet, 1); violetTwo = Stroke(violet, 2); violetFour = Stroke(violet, 4);
                redSix = Stroke(red, 6); redTwelve = Stroke(red, 12); redEight = Stroke(red, 8);
                redEighteen = Stroke(red, 18); darkTwo = Stroke(dark, 2);
                MakeArt();
            }

            internal void Advance(double value) { seconds = value; InvalidateVisual(); }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                if (ActualWidth < 1 || ActualHeight < 1) return;
                double width = hero ? 440 : 1100, height = hero ? 150 : 700;
                dc.PushTransform(new ScaleTransform(ActualWidth / width, ActualHeight / height));
                switch (theme)
                {
                    case UiTheme.Cyberpunk: Cyberpunk(dc, width, height); break;
                    case UiTheme.Steampunk: Steampunk(dc, width, height); break;
                    case UiTheme.Vaporwave: Vaporwave(dc, width, height); break;
                    case UiTheme.ChineseStyle: Chinese(dc, width, height); break;
                    case UiTheme.Persona5: Persona(dc, width, height); break;
                    case UiTheme.Constructivist: Constructivist(dc, width, height); break;
                    case UiTheme.Minimal: Minimal(dc, width, height); break;
                    default: Modern(dc, width, height); break;
                }
                dc.Pop();
            }

            private void Cyberpunk(DrawingContext dc, double w, double h)
            {
                int count = hero ? 5 : 12;
                for (int i = 0; i < count; i++)
                {
                    double depth = .35 + (i % 3) * .21;
                    double x = Wrap(seconds * (15 + i * 3) * (i % 2 == 0 ? 1 : -1) + i * 131, w + 100) - 70;
                    double y = hero ? 43 + i % 3 * 28 : 68 + i * 43;
                    double size = 49 * depth;
                    dc.PushOpacity(.4 + depth * .4);
                    dc.DrawLine(trace, new Point(x - 27, y + 6), new Point(x - 5, y + 6));
                    Sprite(dc, "car", x, y, size, size * .4);
                    dc.Pop();
                }
                double carrierX = Wrap(seconds * 6 + (hero ? 270 : 180), w + 200) - 130;
                Sprite(dc, "carrier", carrierX, hero ? 10 : 62, hero ? 168 : 240, hero ? 49 : 70);
                double rescueX = Wrap(seconds * -15 + w * .85, w + 160) - 90;
                double rescueY = hero ? 72 + Math.Sin(seconds * .5) * 4 : h * .67 + Math.Sin(seconds * .5) * 8;
                Sprite(dc, "rescue", rescueX, rescueY, hero ? 113 : 166, hero ? 46 : 67);
                dc.PushOpacity(.58 + .32 * Math.Sin(seconds * 2.1));
                dc.DrawEllipse(cyan, null, new Point(rescueX + 17, rescueY + 27), 3, 1.4);
                dc.DrawEllipse(pink, null, new Point(rescueX + 89, rescueY + 27), 3, 1.4);
                dc.Pop();
            }

            private void Steampunk(DrawingContext dc, double w, double h)
            {
                double shipX = Wrap(seconds * 5.5 + w * .58, w + 200) - 100;
                double shipY = hero ? 10 : 95;
                Sprite(dc, "airship", shipX, shipY + Math.Sin(seconds * .35) * 4, hero ? 141 : 225, hero ? 62 : 99);
                Sprite(dc, "airship", Wrap(seconds * -3 + 70, w + 130) - 90, hero ? 30 : 38, 75, 33);
                double mechX = Wrap(seconds * 5.8 + (hero ? 145 : 880), w + 160) - 80;
                double floor = hero ? 146 : h - 20;
                double scale = hero ? .79 : 1.8;
                dc.PushTransform(new TranslateTransform(mechX, floor));
                dc.PushTransform(new ScaleTransform(scale, scale));
                double stride = Math.Sin(seconds * 2.5);
                Leg(dc, -14, stride, true); Leg(dc, 13, -stride, false);
                double bob = Math.Abs(stride) * 2.2;
                Sprite(dc, "boiler", -34, -125 + bob, 70, 85);
                dc.PushTransform(new RotateTransform(stride * 13, -30, -83 + bob));
                Sprite(dc, "arm", -55, -90 + bob, 25, 62); dc.Pop();
                dc.PushTransform(new RotateTransform(-stride * 13, 33, -83 + bob));
                Sprite(dc, "arm", 30, -90 + bob, 25, 62); dc.Pop();
                Gear(dc, -2, -70 + bob, 14, seconds * 42);
                for (int i = 0; i < 7; i++)
                {
                    double age = Wrap(seconds * .48 + i / 7.0, 1);
                    dc.PushOpacity((1 - age) * .34);
                    dc.DrawEllipse(steam, null, new Point(21 + age * 37 + Math.Sin(age * 4) * 7, -124 - age * 67),
                        5 + age * 14, 4 + age * 9);
                    dc.Pop();
                }
                dc.Pop(); dc.Pop();
                Gear(dc, hero ? 37 : 30, hero ? 122 : h - 52, hero ? 18 : 33, -seconds * 13);
            }

            private void Leg(DrawingContext dc, double hip, double phase, bool rear)
            {
                dc.PushOpacity(rear ? .72 : 1);
                var knee = new Point(hip + phase * 11, -27 - Math.Max(0, phase) * 7);
                var ankle = new Point(hip + phase * 20, -6 - Math.Max(0, phase) * 4);
                dc.DrawLine(ironTwelve, new Point(hip, -53), knee);
                dc.DrawLine(copper, new Point(hip - 3, -53), new Point(knee.X - 3, knee.Y));
                dc.DrawLine(ironThirteen, knee, ankle);
                dc.DrawLine(copper, new Point(knee.X + 4, knee.Y), new Point(ankle.X + 4, ankle.Y));
                dc.DrawEllipse(brass, ironTwo, knee, 7, 7);
                dc.DrawRoundedRectangle(iron, copper, new Rect(ankle.X - 12, ankle.Y - 1, 28, 7), 2, 2);
                dc.Pop();
            }

            private void Vaporwave(DrawingContext dc, double w, double h)
            {
                double horizon = hero ? 66 : h * .65, center = w * .62;
                dc.PushOpacity(.55);
                dc.DrawEllipse(pink, null, new Point(center, horizon - 17), hero ? 32 : 78, hero ? 32 : 78);
                for (int i = 0; i < 5; i++)
                {
                    double y = horizon - 22 + i * (hero ? 8 : 15);
                    dc.DrawLine(hero ? violetTwo : violetFour, new Point(center - (hero ? 32 : 78), y), new Point(center + (hero ? 32 : 78), y));
                }
                for (int i = -5; i <= 5; i++)
                    dc.DrawLine(violetOne, new Point(center + i * 13, horizon), new Point(center + i * w * .19, h));
                for (int i = 0; i < 10; i++)
                {
                    double phase = Wrap(i / 10.0 + seconds * .045, 1);
                    double y = horizon + phase * phase * (h - horizon);
                    dc.DrawLine(trace, new Point(0, y), new Point(w, y));
                }
                dc.Pop();
                for (int i = 0; i < 2; i++)
                {
                    dc.PushTransform(new TranslateTransform(i == 0 ? w * .13 : w * .86, h - 3));
                    double scale = hero ? .85 : 2;
                    dc.PushTransform(new ScaleTransform(scale, scale));
                    dc.PushTransform(new RotateTransform(Math.Sin(seconds * .35 + i) * 2));
                    dc.DrawGeometry(violet, null, palm); dc.Pop(); dc.Pop(); dc.Pop();
                }
                Sprite(dc, "retro", center - 35 + Math.Sin(seconds * .21) * 8, h - (hero ? 32 : 64), hero ? 70 : 130, hero ? 26 : 49);
                if (!hero)
                    for (int i = 0; i < 3; i++) Sprite(dc, "window", 22 + i * 390,
                        50 + i % 2 * 155 + Math.Sin(seconds * .3 + i) * 12, 142, 91);
            }

            private void Chinese(DrawingContext dc, double w, double h)
            {
                for (int i = 0; i < (hero ? 2 : 5); i++)
                {
                    double x = Wrap(seconds * (2 + i * .3) + i * w / 4, w + 180) - 80;
                    double y = hero ? 50 + i * 25 : 95 + i * 113;
                    dc.PushOpacity(.10 + i % 2 * .04);
                    dc.DrawEllipse(paper, null, new Point(x, y), hero ? 87 : 150, hero ? 9 : 17);
                    dc.DrawEllipse(ink, null, new Point(x + 24, y + 6), hero ? 67 : 104, hero ? 4 : 7);
                    dc.Pop();
                }
                for (int i = 0; i < 3; i++)
                {
                    dc.PushTransform(new TranslateTransform(Wrap(seconds * 6 + w * .65 + i * 21, w + 70) - 20,
                        (hero ? 30 : h * .22) + i * 11 + Math.Sin(seconds * 1.2 + i) * 3));
                    dc.PushTransform(new ScaleTransform(.6, .5 + Math.Sin(seconds * 2 + i) * .23));
                    dc.DrawGeometry(null, jade, bird); dc.Pop(); dc.Pop();
                }
                double boat = Wrap(seconds * 3 + w * .38, w + 110) - 50, water = h - (hero ? 10 : 29);
                Sprite(dc, "boat", boat, water - (hero ? 28 : 49) + Math.Sin(seconds * .8) * 1.5, hero ? 82 : 143, hero ? 31 : 54);
                dc.PushOpacity(.3);
                for (int i = 0; i < 3; i++) dc.DrawLine(jade, new Point(boat - 25 - i * 4, water + i * 4), new Point(boat + 15 - i * 5, water + i * 4));
                dc.Pop();
            }

            private void Persona(DrawingContext dc, double w, double h)
            {
                for (int i = 0; i < (hero ? 4 : 9); i++)
                {
                    double x = Wrap(seconds * (2 + i % 3) + i * 149, w + 100) - 50;
                    double y = hero ? 30 + i % 3 * 40 : 28 + i * 79;
                    dc.PushTransform(new TranslateTransform(x, y + Math.Sin(seconds * .6 + i) * 6));
                    dc.PushTransform(new RotateTransform(-17 + Math.Sin(seconds * .17 + i) * 12));
                    dc.PushOpacity(hero ? .8 : .38);
                    dc.DrawGeometry(i % 2 == 0 ? red : white, darkTwo, i % 3 == 0 ? bolt : star);
                    dc.Pop(); dc.Pop(); dc.Pop();
                }
                double sweep = Wrap(seconds * 17 + w * .4, w + 200) - 100;
                dc.PushOpacity(.14);
                dc.DrawLine(hero ? redEight : redEighteen, new Point(sweep - 100, h), new Point(sweep + 80, 0));
                dc.Pop();
            }

            private void Constructivist(DrawingContext dc, double w, double h)
            {
                double x = w * .76, y = h * .48;
                dc.PushOpacity(hero ? .7 : .28);
                dc.DrawEllipse(null, hero ? redSix : redTwelve, new Point(x, y), hero ? 42 : 115, hero ? 42 : 115);
                dc.PushTransform(new RotateTransform(22 + Math.Sin(seconds * .12) * 8, x, y));
                dc.DrawRectangle(dark, null, new Rect(x - (hero ? 70 : 180), y - 4, hero ? 140 : 360, hero ? 10 : 22));
                dc.DrawRectangle(red, null, new Rect(x + 20, y - (hero ? 51 : 140), hero ? 12 : 24, hero ? 103 : 280));
                dc.Pop();
                double rail = Math.Sin(seconds * .2) * (hero ? 10 : 25);
                dc.DrawLine(redPen, new Point(4, h * .8 + rail), new Point(w * .48, 8 + rail));
                dc.DrawRectangle(paper, darkTwo, new Rect(w * .25 + rail, h * .25, hero ? 22 : 49, hero ? 22 : 49));
                dc.Pop();
            }

            private void Minimal(DrawingContext dc, double w, double h)
            {
                dc.PushOpacity(.16);
                double x = w * .7 + Math.Sin(seconds * .15) * w * .06;
                double y = h * .45 + Math.Cos(seconds * .13) * h * .08;
                dc.DrawEllipse(null, jade, new Point(x, y), hero ? 32 : 80, hero ? 32 : 80);
                dc.DrawLine(jade, new Point(x - 70, y + 32), new Point(x + 70, y + 32));
                dc.DrawEllipse(ink, null, new Point(x + Math.Sin(seconds * .2) * 32, y + Math.Cos(seconds * .2) * 32), 3, 3);
                dc.Pop();
            }

            private void Modern(DrawingContext dc, double w, double h)
            {
                dc.PushOpacity(hero ? .48 : .23);
                for (int i = 0; i < (hero ? 5 : 10); i++)
                {
                    double x = w * (.1 + i % 5 * .19) + Math.Sin(seconds * .16 + i) * 13;
                    double y = hero ? 55 + i % 2 * 50 : 50 + i * 67;
                    dc.PushTransform(new TranslateTransform(x, y));
                    dc.PushTransform(new RotateTransform(-30 + seconds * .7 + i * 32));
                    dc.DrawGeometry(i % 2 == 0 ? white : brass, null, bolt);
                    dc.Pop(); dc.Pop();
                }
                double line = Wrap(seconds * 3, h);
                dc.DrawLine(trace, new Point(0, line), new Point(w, line));
                dc.Pop();
            }

            private void MakeArt()
            {
                if (theme == UiTheme.Cyberpunk)
                {
                    Add("car", dc => {
                        dc.DrawGeometry(dark, trace, Shape("M0,11 L16,2 54,2 71,13 62,21 H8 Z"));
                        dc.DrawGeometry(cyan, null, Shape("M19,4 H49 L58,11 H10 Z"));
                        dc.DrawRectangle(pink, null, new Rect(5, 16, 12, 2)); dc.DrawRectangle(cyan, null, new Rect(58, 15, 9, 2));
                    });
                    Add("carrier", dc => {
                        dc.DrawGeometry(dark, Stroke(pink, 1.4), Shape("M0,24 L25,5 133,1 174,17 183,29 147,41 H29 Z"));
                        dc.DrawLine(trace, new Point(25, 29), new Point(157, 29));
                        dc.DrawGeometry(cyan, null, Shape("M140,9 L165,18 171,25 H137 Z"));
                        dc.DrawRectangle(pink, null, new Rect(29, 37, 26, 7)); dc.DrawRectangle(pink, null, new Rect(126, 37, 26, 7));
                        Label(dc, "鼎山航运", 36, 10, 12, white); Label(dc, "AERO", 95, 13, 8, cyan);
                    });
                    Add("rescue", dc => {
                        dc.DrawGeometry(white, Stroke(cyan, 1), Shape("M0,28 L15,17 23,6 H83 L105,24 99,37 H12 Z"));
                        dc.DrawGeometry(dark, null, Shape("M66,9 H81 L93,23 H67 Z"));
                        dc.DrawRectangle(red, null, new Rect(27, 15, 14, 5)); dc.DrawRectangle(red, null, new Rect(32, 10, 4, 15));
                        Label(dc, "白金急救", 44, 27, 8, dark);
                        dc.DrawRectangle(cyan, null, new Rect(4, 30, 12, 6)); dc.DrawRectangle(pink, null, new Rect(88, 30, 12, 6));
                    });
                }
                if (theme == UiTheme.Steampunk)
                {
                    Add("airship", dc => {
                        dc.DrawEllipse(iron, copper, new Point(93, 29), 79, 27);
                        dc.DrawGeometry(brass, null, Shape("M15,29 L1,15 3,43 Z M158,19 L183,8 175,30 Z"));
                        for (int i = 0; i < 6; i++) dc.DrawLine(copper, new Point(42 + i * 19, 7), new Point(42 + i * 19, 52));
                        dc.DrawLine(copper, new Point(58, 53), new Point(67, 70)); dc.DrawLine(copper, new Point(126, 53), new Point(118, 70));
                        dc.DrawRoundedRectangle(iron, copper, new Rect(63, 64, 60, 14), 4, 4);
                        for (int i = 0; i < 4; i++) dc.DrawEllipse(brass, null, new Point(73 + i * 12, 71), 3, 3);
                        Label(dc, "鼎山机械", 68, 22, 10, brass);
                    });
                    Add("boiler", dc => {
                        dc.DrawRoundedRectangle(iron, copper, new Rect(10, 25, 48, 52), 10, 10);
                        dc.DrawRectangle(iron, copper, new Rect(40, 0, 12, 35)); dc.DrawRectangle(brass, null, new Rect(37, 0, 18, 5));
                        dc.DrawRoundedRectangle(brass, Stroke(iron, 2), new Rect(17, 13, 29, 27), 6, 6);
                        dc.DrawRectangle(iron, null, new Rect(19, 22, 25, 7)); dc.DrawRectangle(white, null, new Rect(23, 24, 16, 3));
                        dc.DrawEllipse(iron, copper, new Point(33, 55), 17, 17);
                        for (int i = 0; i < 4; i++) dc.DrawLine(copper, new Point(17, 49 + i * 5), new Point(49, 49 + i * 5));
                        dc.DrawEllipse(brass, null, new Point(6, 36), 6, 6); dc.DrawEllipse(brass, null, new Point(62, 36), 6, 6);
                    });
                    Add("arm", dc => {
                        dc.DrawRoundedRectangle(iron, copper, new Rect(4, 0, 16, 40), 5, 5);
                        dc.DrawEllipse(brass, Stroke(iron, 2), new Point(12, 9), 7, 7);
                        dc.DrawLine(copper, new Point(9, 20), new Point(9, 34));
                        dc.DrawRoundedRectangle(iron, copper, new Rect(0, 36, 24, 19), 3, 3);
                    });
                }
                if (theme == UiTheme.Vaporwave)
                {
                    Add("retro", dc => {
                        dc.DrawGeometry(violet, trace, Shape("M0,22 L15,7 H65 L80,22 76,35 H4 Z"));
                        dc.DrawGeometry(dark, Stroke(pink, 1), Shape("M20,9 H60 L67,21 H13 Z"));
                        dc.DrawLine(trace, new Point(8, 27), new Point(72, 27));
                        dc.DrawRectangle(pink, null, new Rect(7, 29, 19, 4)); dc.DrawRectangle(pink, null, new Rect(54, 29, 19, 4));
                    });
                    Add("window", dc => {
                        dc.DrawRoundedRectangle(paper, Stroke(violet, 2), new Rect(0, 0, 140, 88), 3, 3);
                        dc.DrawRectangle(violet, null, new Rect(1, 1, 138, 17));
                        Label(dc, "dream.exe", 8, 3, 10, white);
                        dc.DrawEllipse(pink, null, new Point(73, 49), 19, 19);
                        for (int i = 0; i < 3; i++) dc.DrawLine(trace, new Point(8, 64 + i * 6), new Point(132, 64 + i * 6));
                    });
                }
                if (theme == UiTheme.ChineseStyle)
                    Add("boat", dc => {
                        dc.DrawGeometry(ink, null, Shape("M0,29 Q39,36 91,26 L78,39 H18 Z"));
                        dc.DrawLine(jade, new Point(46, 0), new Point(46, 32));
                        dc.DrawGeometry(paper, jade, Shape("M43,2 Q22,13 22,26 H43 Z"));
                        dc.DrawGeometry(ink, null, Shape("M46,14 Q65,11 66,24 H46 Z"));
                        dc.DrawEllipse(red, null, new Point(72, 23), 2, 2);
                    });
            }

            private void Gear(DrawingContext dc, double x, double y, double radius, double angle)
            {
                dc.PushTransform(new RotateTransform(angle, x, y));
                dc.DrawEllipse(iron, copper, new Point(x, y), radius * .8, radius * .8);
                for (int i = 0; i < 10; i++)
                {
                    dc.PushTransform(new RotateTransform(i * 36, x, y));
                    dc.DrawRectangle(brass, null, new Rect(x - radius * .13, y - radius, radius * .26, radius * .35));
                    dc.Pop();
                }
                dc.DrawEllipse(iron, copper, new Point(x, y), radius * .31, radius * .31);
                dc.Pop();
            }

            private void Add(string key, Action<DrawingContext> draw)
            {
                var group = new DrawingGroup();
                using (var dc = group.Open()) draw(dc);
                group.Freeze();
                var image = new DrawingImage(group); image.Freeze(); art.Add(key, image);
            }
            private void Sprite(DrawingContext dc, string key, double x, double y, double w, double h)
            {
                dc.DrawImage(art[key], new Rect(x, y, w, h));
            }
            private static void Label(DrawingContext dc, string text, double x, double y, double size, Brush brush)
            {
                dc.DrawText(new FormattedText(text, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight,
                    new Typeface("Microsoft YaHei UI"), size, brush, 1), new Point(x, y));
            }
            private static double Wrap(double value, double size) { return (value % size + size) % size; }
            private static Brush Paint(string hex)
            {
                var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString("#" + hex)); brush.Freeze(); return brush;
            }
            private static Pen Stroke(Brush brush, double width) { var pen = new Pen(brush, width); pen.Freeze(); return pen; }
            private static Geometry Shape(string path) { var shape = Geometry.Parse(path); shape.Freeze(); return shape; }
        }
    }
}
