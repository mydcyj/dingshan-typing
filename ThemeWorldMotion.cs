using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Globalization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    // Animated scenery stays behind controls and has no input or gameplay state.
    internal sealed class ThemeWorldMotion
    {
        private readonly Window window;
        private readonly UiTheme theme;
        private readonly Func<bool> lowEffects;
        private readonly FrameworkElement startup;
        private readonly WorldScene background, header;
        private readonly Stopwatch elapsed = new Stopwatch();
        private static readonly TimeSpan FrameInterval = TimeSpan.FromTicks(TimeSpan.TicksPerSecond / 60);
        private static readonly TimeSpan FrameTolerance = TimeSpan.FromMilliseconds(1);
        private TimeSpan? lastRenderingTime;
        private TimeSpan nextFrame;
        private bool rendering;
        private bool closed;

        internal static void Attach(Window window, UiTheme theme, Func<bool> lowEffects)
        {
            new ThemeWorldMotion(window, theme, lowEffects);
        }

        private ThemeWorldMotion(Window window, UiTheme theme, Func<bool> lowEffects)
        {
            this.window = window;
            this.theme = theme;
            this.lowEffects = lowEffects;
            startup = (FrameworkElement)window.FindName("StartupOverlay");
            background = new WorldScene(theme, false) { Name = "ThemeWorldBackground", Opacity = .74 };
            header = new WorldScene(theme, true) { Name = "ThemeWorldHeader",
                HorizontalAlignment = theme == UiTheme.ArtDeco ? HorizontalAlignment.Stretch : HorizontalAlignment.Right };
            ((Grid)window.FindName("BackgroundLayers")).Children.Add(background);
            var hero = (Grid)window.FindName("PracticeHero");
            Grid.SetColumnSpan(header, Math.Max(1, hero.ColumnDefinitions.Count));
            Grid.SetRowSpan(header, Math.Max(1, hero.RowDefinitions.Count));
            Panel.SetZIndex(header, 0);
            hero.Children.Add(header);
            hero.SizeChanged += ResizeHeader;
            window.Loaded += Update;
            window.StateChanged += Update;
            window.IsVisibleChanged += VisibilityChanged;
            window.Closed += Close;
            startup.IsVisibleChanged += VisibilityChanged;
            var checkbox = window.FindName("SnakeLowEffects") as System.Windows.Controls.Primitives.ButtonBase;
            if (checkbox != null) checkbox.Click += Update;
            SystemParameters.StaticPropertyChanged += SystemChanged;
            if (window.IsLoaded) Update(null, EventArgs.Empty);
        }

        private void ResizeHeader(object sender, SizeChangedEventArgs e)
        {
            if (theme == UiTheme.ArtDeco)
                return;
            if (theme >= UiTheme.Ukiyoe)
            {
                header.Width = Math.Min(640, e.NewSize.Width * .56);
                header.Opacity = 1;
                return;
            }
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
            return !closed && window.IsLoaded && window.IsVisible && !startup.IsVisible && window.WindowState != WindowState.Minimized
                && SystemParameters.ClientAreaAnimation && (lowEffects == null || !lowEffects());
        }

        private void Update(object sender, EventArgs e)
        {
            if (!CanRun())
                StopRendering();
            else if (!rendering)
            {
                rendering = true;
                lastRenderingTime = null;
                elapsed.Start();
                CompositionTarget.Rendering += Render;
            }
        }

        private void Render(object sender, EventArgs e)
        {
            if (!CanRun()) { StopRendering(); return; }
            var time = ((RenderingEventArgs)e).RenderingTime;
            if (lastRenderingTime == time)
                return;
            if (!lastRenderingTime.HasValue || time < lastRenderingTime.Value)
                nextFrame = time;
            lastRenderingTime = time;
            if (time + FrameTolerance < nextFrame)
                return;
            nextFrame += FrameInterval;
            if (nextFrame <= time)
                nextFrame = time + FrameInterval;
            double seconds = elapsed.Elapsed.TotalSeconds;
            if (background.IsVisible) background.Advance(seconds);
            if (header.IsVisible) header.Advance(seconds);
        }

        private void StopRendering()
        {
            if (!rendering)
                return;
            CompositionTarget.Rendering -= Render;
            elapsed.Stop();
            rendering = false;
        }

        private void Close(object sender, EventArgs e)
        {
            closed = true;
            StopRendering();
            window.Loaded -= Update;
            window.StateChanged -= Update;
            window.IsVisibleChanged -= VisibilityChanged;
            window.Closed -= Close;
            startup.IsVisibleChanged -= VisibilityChanged;
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
            private readonly DrawingGroup scenery = new DrawingGroup();
            private readonly DrawingGroup decoCrest = new DrawingGroup();
            private readonly Action<double> moveVignette;
            private readonly DrawingGroup[] traffic;
            private readonly TranslateTransform carrier = new TranslateTransform(), rescue = new TranslateTransform();
            private readonly DrawingGroup rescueLights = new DrawingGroup();
            private DrawingGroup carrierDrawing, rescueDrawing;
            private static readonly string[] VehicleKeys = { "car", "police", "luxury", "tuner" };
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
                if (theme == UiTheme.Cyberpunk)
                {
                    traffic = new DrawingGroup[hero ? 6 : 12];
                    BuildCyberpunk();
                }
                else if (theme == UiTheme.Windows98)
                    moveVignette = BuildDesktop();
                else if (theme == UiTheme.CassetteFuturism)
                    moveVignette = BuildCassette();
                else if (theme == UiTheme.ArtDeco)
                    moveVignette = hero ? BuildDecoHeader() : BuildDecoBackground();
                else if (theme == UiTheme.Atompunk)
                    moveVignette = BuildAtomicStation();
                else if (theme == UiTheme.Ukiyoe)
                    moveVignette = BuildWoodblockCoast();
                else if (theme == UiTheme.Pixel)
                {
                    RenderOptions.SetEdgeMode(this, EdgeMode.Aliased);
                    moveVignette = BuildPixelIslands();
                }
                else if (theme == UiTheme.Solarpunk)
                    moveVignette = BuildSolarGarden();
                Advance(0);
            }

            private double SceneWidth { get { return hero && theme != UiTheme.ArtDeco ? 440 : 1100; } }
            private double SceneHeight { get { return hero ? (theme == UiTheme.ArtDeco ? 200 : theme >= UiTheme.Ukiyoe ? 180 : 150) : 700; } }
            private double CarrierWidth { get { return hero ? 168 : 240; } }
            private double RescueWidth { get { return hero ? 138 : 180; } }

            internal void Advance(double value)
            {
                seconds = value;
                if (theme == UiTheme.Cyberpunk)
                {
                    MoveCyberpunk();
                    return;
                }
                if (moveVignette != null)
                {
                    moveVignette(value);
                    return;
                }
                // Keep the drawing attached to the visual. A frame changes its content without invalidating layout.
                double w = SceneWidth, h = SceneHeight;
                using (var dc = scenery.Open())
                    switch (theme)
                    {
                        case UiTheme.Steampunk: Steampunk(dc, w, h); break;
                        case UiTheme.Vaporwave: Vaporwave(dc, w, h); break;
                        case UiTheme.ChineseStyle: Chinese(dc, w, h); break;
                        case UiTheme.Persona5: Persona(dc, w, h); break;
                        case UiTheme.Constructivist: Constructivist(dc, w, h); break;
                        case UiTheme.Minimal: Minimal(dc, w, h); break;
                        default: Modern(dc, w, h); break;
                    }
            }

            protected override void OnRender(DrawingContext dc)
            {
                base.OnRender(dc);
                if (ActualWidth < 1 || ActualHeight < 1) return;
                dc.PushTransform(new ScaleTransform(ActualWidth / SceneWidth, ActualHeight / SceneHeight));
                dc.DrawDrawing(scenery);
                dc.Pop();
                if (hero && theme == UiTheme.ArtDeco)
                {
                    double scale = Math.Min(ActualWidth / 800, ActualHeight / SceneHeight);
                    dc.PushTransform(new MatrixTransform(scale, 0, 0, scale, ActualWidth / 2, ActualHeight * .329));
                    dc.DrawDrawing(decoCrest);
                    dc.Pop();
                }
            }

            private DrawingGroup LandscapeSurface(string color)
            {
                AddSceneDrawing(scenery, null, dc => dc.DrawRectangle(Paint(color), null, new Rect(0, 0, SceneWidth, SceneHeight)));
                if (hero) return scenery;
                var landscape = SceneVignette(0, 340, 2.5);
                landscape.Opacity = .36;
                return landscape;
            }

            private void FadeLandscapeEdge(DrawingGroup landscape, string color)
            {
                if (hero)
                    AddSceneDrawing(landscape, null, dc => dc.DrawRectangle(Tone(color, "00" + color, 0), null, new Rect(0, 0, 46, 180)));
            }

            private Action<double> BuildWoodblockCoast()
            {
                var landscape = LandscapeSurface("EFE3C6");
                var blue = Paint("183C55");
                var sea = Paint("4E8191");
                var pale = Paint("F7EED8");
                var contour = Stroke(blue, 1.2);
                AddSceneDrawing(landscape, null, dc =>
                {
                    dc.DrawEllipse(Paint("AE4533"), null, new Point(335, 50), 29, 29);
                    dc.DrawGeometry(Paint("879C95"), null, Shape("M174,123 L254,66 320,122 Z"));
                    dc.DrawGeometry(blue, null, Shape("M214,127 L307,47 379,127 Z"));
                    dc.DrawGeometry(pale, null, Shape("M281,70 L307,47 329,73 313,69 308,78 300,69 292,77 Z"));
                    dc.DrawGeometry(sea, contour, Shape("M0,128 Q47,106 94,117 T184,122 Q220,107 256,118 Q329,111 440,123 V180 H0 Z"));
                    dc.DrawGeometry(null, Stroke(pale, 1), Shape("M205,129 Q267,111 349,130 T470,130 M232,135 Q293,124 357,137 T465,135"));
                    for (int i = 0; i < 80; i++)
                    {
                        double x = (i * 37) % 438, y = (i * 23) % 175;
                        dc.DrawLine(Stroke(Paint("1B6D6654"), .45), new Point(x, y), new Point(x + 2 + i % 3, y + .4));
                    }
                });
                var clouds = new TranslateTransform();
                AddSceneDrawing(landscape, clouds, dc =>
                {
                    dc.DrawGeometry(pale, Stroke(Paint("B6B6A0"), .6), Shape("M40,43 H89 C101,43 100,35 110,35 H141 C157,35 158,43 150,47 H117 C107,47 107,53 97,53 H45 C32,53 31,45 40,43 Z M235,21 H277 C290,21 293,28 283,31 H260 C254,31 256,38 246,38 H217 C204,38 208,31 219,31 H229 C241,31 225,23 235,21 Z"));
                    dc.DrawGeometry(null, contour, Shape("M187,39 Q193,34 199,39 Q205,34 211,39 M215,48 Q220,44 225,48 Q230,44 235,48"));
                });
                var wave = new TranslateTransform();
                AddSceneDrawing(landscape, wave, dc =>
                {
                    dc.DrawGeometry(blue, contour, Shape("M-9,176 C35,160 40,117 50,83 C59,44 99,24 133,40 C153,49 162,68 158,88 C151,74 142,66 127,67 C148,90 157,112 174,127 C204,156 245,147 273,128 C242,168 202,181 162,169 C123,158 100,123 97,91 C81,111 88,138 63,164 L45,184 Z"));
                    dc.DrawGeometry(sea, null, Shape("M-5,180 C38,160 51,141 59,110 C69,75 86,60 109,53 C90,69 88,89 96,112 C108,142 137,166 164,177 L170,186 Z"));
                    dc.DrawGeometry(pale, null, Shape("M49,84 C55,48 91,28 122,36 C143,39 162,57 161,77 L153,65 152,77 144,60 144,74 135,57 132,68 123,55 117,63 107,53 100,63 91,59 83,73 73,70 67,84 59,83 52,99 Z"));
                    dc.DrawGeometry(pale, Stroke(blue, .7), Shape("M77,59 Q73,41 87,37 Q94,35 95,40 Q85,38 84,47 L88,49 Q92,40 105,35 Q118,30 118,37 Q107,37 105,44 Q122,36 137,43 Q151,50 147,57 Q144,47 133,48 Q149,55 156,68 Q160,77 154,81 Q152,70 143,67 Q147,80 141,83 Q139,71 129,65 Q132,76 126,78 Q123,65 116,62 Q118,70 111,72 L105,60 Q95,58 88,68 Z"));
                    dc.DrawGeometry(null, Stroke(pale, 1.3), Shape("M18,166 C45,151 50,119 60,96 M35,168 C64,142 62,100 89,81 M51,172 C72,145 74,113 86,104 M103,115 C120,161 161,181 200,165 M111,111 C132,151 163,161 186,158 M125,108 Q143,133 167,141"));
                    dc.DrawGeometry(blue, contour, Shape("M220,184 C256,168 270,155 290,138 C325,107 345,107 371,123 C389,133 398,158 442,155 V184 Z"));
                    dc.DrawGeometry(pale, null, Shape("M270,157 Q316,101 352,115 Q371,118 379,131 L367,127 364,135 354,126 344,134 336,123 325,132 320,127 Q291,142 270,157 Z"));
                    dc.DrawGeometry(null, Stroke(sea, 2), Shape("M278,178 Q326,136 349,150 Q375,166 441,173 M304,178 Q326,151 347,159 Q374,177 420,178"));
                    for (int i = 0; i < 20; i++)
                    {
                        double x = 61 + (i * 13) % 74, y = 84 + (i * 19) % 57;
                        dc.DrawGeometry(null, Stroke(pale, .55), Shape("M" + x + "," + y + " q-3,-5 1,-7 q4,-1 3,3"));
                    }
                });
                var foam = new TranslateTransform();
                AddSceneDrawing(landscape, foam, dc =>
                {
                    foreach (Point p in new[] { new Point(76,31), new Point(113,21), new Point(148,36), new Point(176,78), new Point(198,102), new Point(61,49), new Point(173,52) })
                        dc.DrawEllipse(pale, Stroke(blue, .6), p, 1.4, 2);
                });
                FadeLandscapeEdge(landscape, "EFE3C6");
                return t => { wave.X = Math.Sin(t * .5) * 2; wave.Y = Math.Sin(t * .7) * 1.4; clouds.X = Math.Sin(t * .12) * 10; foam.Y = Math.Sin(t * .8) * 2.3; };
            }

            private Action<double> BuildPixelIslands()
            {
                var landscape = LandscapeSurface("171B35");
                var dusk = Paint("343B67");
                var plum = Paint("635A86");
                var gold = Paint("F0BB91");
                var mint = Paint("8CCAB3");
                var grass = Paint("456D6F");
                var stone = Paint("3D3755");
                AddSceneDrawing(landscape, null, dc =>
                {
                    dc.DrawRectangle(dusk, null, new Rect(0, 56, 440, 124));
                    dc.DrawRectangle(Paint("70668D"), null, new Rect(0, 83, 440, 97));
                    for (int i = 0; i < 55; i++)
                    {
                        double x = i * 8;
                        dc.DrawRectangle(dusk, null, new Rect(x, 54 + (i % 2) * 4, 4, 4));
                        dc.DrawRectangle(plum, null, new Rect(x, 80 + (i % 2) * 4, 4, 4));
                    }
                    dc.DrawGeometry(gold, null, Shape("M340,25 H365 V29 H373 V37 H377 V57 H373 V65 H365 V69 H340 V65 H332 V57 H328 V37 H332 V29 H340 Z"));
                    dc.DrawGeometry(dusk, null, Shape("M335,31 H341 V37 H335 Z M355,51 H367 V57 H355 Z M346,59 H354 V65 H346 Z"));
                    dc.DrawGeometry(Paint("4C4A75"), null, Shape("M0,121 H32 V109 H48 V101 H72 V89 H88 V101 H104 V113 H144 V105 H164 V93 H180 V105 H192 V121 H232 V113 H252 V101 H280 V113 H300 V97 H316 V89 H332 V109 H356 V117 H388 V97 H404 V113 H440 V180 H0 Z"));
                    dc.DrawGeometry(dusk, null, Shape("M0,141 H40 V129 H72 V137 H112 V125 H144 V141 H176 V133 H216 V145 H256 V129 H288 V137 H320 V121 H352 V129 H392 V141 H440 V180 H0 Z"));
                    dc.DrawGeometry(stone, null, Shape("M148,137 H392 V145 H384 V153 H360 V161 H332 V169 H312 V177 H228 V169 H204 V161 H184 V153 H156 V145 H148 Z M45,103 H129 V111 H117 V119 H105 V127 H77 V119 H61 V111 H45 Z"));
                    dc.DrawGeometry(grass, null, Shape("M148,129 H176 V121 H216 V125 H352 V121 H384 V129 H392 V139 H148 Z M45,99 H77 V95 H109 V99 H129 V105 H45 Z"));
                    dc.DrawGeometry(mint, null, Shape("M152,129 H192 V125 H216 V129 H248 V133 H152 Z M324,125 H356 V129 H380 V133 H324 Z M49,99 H73 V103 H49 Z"));
                    dc.DrawGeometry(Paint("66506C"), null, Shape("M172,145 H196 V149 H188 V153 H172 Z M212,157 H232 V165 H224 V169 H212 Z M336,145 H360 V149 H348 V157 H336 Z"));
                    dc.DrawGeometry(Paint("253849"), null, Shape("M198,121 V83 H186 V71 H194 V63 H190 V55 H206 V47 H222 V55 H230 V63 H234 V75 H222 V87 H210 V121 Z M341,124 V97 H329 V89 H333 V77 H341 V69 H357 V77 H365 V85 H361 V97 H353 V124 Z"));
                    dc.DrawGeometry(grass, null, Shape("M190,63 H202 V55 H218 V59 H226 V71 H218 V79 H202 V75 H190 Z M333,81 H345 V73 H353 V81 H361 V89 H341 V93 H333 Z"));
                    dc.DrawGeometry(mint, null, Shape("M202,55 H214 V59 H202 Z M194,63 H206 V67 H194 Z M345,77 H353 V81 H345 Z"));
                    dc.DrawRectangle(Paint("BB8F87"), null, new Rect(268, 89, 48, 40));
                    dc.DrawGeometry(stone, null, Shape("M256,89 H264 V81 H272 V73 H308 V81 H316 V89 H324 V93 H256 Z"));
                    dc.DrawGeometry(gold, null, Shape("M276,99 H284 V109 H276 Z M300,99 H308 V109 H300 Z M288,111 H296 V129 H288 Z"));
                    dc.DrawRectangle(Paint("171B35"), null, new Rect(276, 103, 8, 2));
                    dc.DrawRectangle(Paint("171B35"), null, new Rect(300, 103, 8, 2));
                    dc.DrawGeometry(Paint("9D7790"), null, Shape("M268,95 H278 V99 H268 Z M308,115 H316 V119 H308 Z M278,121 H286 V125 H278 Z"));
                    dc.DrawGeometry(gold, null, Shape("M164,123 H168 V113 H170 V123 H180 V119 H184 V125 H164 Z M316,127 H328 V123 H332 V129 H316 Z"));
                    for (int i = 0; i < 16; i++) dc.DrawRectangle(gold, null, new Rect(21 + i * 26 % 388, 15 + i * 19 % 42, 2, 2));
                });
                var cloud = new TranslateTransform();
                AddSceneDrawing(landscape, cloud, dc =>
                {
                    dc.DrawGeometry(Paint("A397CF"), null, Shape("M68,43 H80 V35 H104 V39 H124 V47 H140 V51 H68 Z M219,29 H235 V21 H255 V25 H271 V33 H283 V37 H219 Z M357,89 H373 V81 H401 V85 H417 V93 H357 Z"));
                });
                var falls = new TranslateTransform();
                var water = new DrawingGroup { ClipGeometry = new RectangleGeometry(new Rect(252, 135, 12, 45)) };
                landscape.Children.Add(water);
                AddSceneDrawing(water, null, dc => dc.DrawRectangle(grass, null, new Rect(252, 135, 12, 45)));
                AddSceneDrawing(water, falls, dc =>
                {
                    for (int i = -1; i < 6; i++) dc.DrawRectangle(mint, null, new Rect(i % 2 == 0 ? 252 : 260, 131 + i * 12, 4, 8));
                });
                // A block-stepped fade protects the crisp pixel edge.
                if (hero) AddSceneDrawing(landscape, null, dc =>
                {
                    for (int i = 0; i < 8; i++)
                    {
                        dc.PushOpacity(1 - i / 8.0);
                        dc.DrawRectangle(Paint("171B35"), null, new Rect(i * 4, 0, 4, 180));
                        dc.Pop();
                    }
                });
                return t => { cloud.X = Math.Round(Math.Sin(t * .12) * 12 / 2) * 2; falls.Y = Math.Floor(t * 4) % 6 * 2; };
            }

            private Action<double> BuildSolarGarden()
            {
                var landscape = LandscapeSurface("EDF1D4");
                var forest = Paint("245549");
                var leaf = Paint("6E9F6C");
                var cream = Paint("FFF4D2");
                var sky = Paint("B9DBC6");
                var glass = Paint("A2D2BE");
                var edge = Stroke(forest, .8);
                AddSceneDrawing(landscape, null, dc =>
                {
                    dc.DrawEllipse(Paint("E8BF62"), null, new Point(350, 43), 31, 31);
                    dc.DrawGeometry(sky, null, Shape("M0,123 Q64,77 132,101 T277,89 Q358,61 440,92 V180 H0 Z"));
                    dc.DrawGeometry(Paint("B7CDA0"), null, Shape("M0,151 Q82,106 172,135 T336,117 Q386,112 440,134 V180 H0 Z"));
                    dc.DrawGeometry(Paint("A8C09B"), null, Shape("M123,123 V82 H153 V58 H186 V78 H205 V126 Z M370,130 V62 H394 V82 H417 V127 Z"));
                    dc.DrawGeometry(cream, edge, Shape("M180,145 V62 Q204,51 226,62 V145 Z M224,146 V44 Q246,34 268,44 V143 Z M269,141 V75 Q292,65 313,75 V140 Z"));
                    dc.DrawGeometry(Paint("D9D8B8"), null, Shape("M211,57 L225,62 V146 H212 Z M255,39 L268,44 V144 H255 Z M303,70 L313,75 V141 H303 Z"));
                    for (int floor = 0; floor < 4; floor++)
                    {
                        double y = 63 + floor * 19;
                        dc.DrawRectangle(glass, null, new Rect(232, y - 11, 28, 9));
                        dc.DrawGeometry(leaf, edge, Shape("M218," + (y - 1) + " Q247," + (y + 7) + " 273," + (y - 1) + " V" + (y + 4) + " Q246," + (y + 12) + " 218," + (y + 4) + " Z"));
                        dc.DrawRectangle(glass, null, new Rect(188, y + 8, 30, 9));
                        dc.DrawLine(Stroke(forest, 2), new Point(176, y + 22), new Point(229, y + 22));
                    }
                    for (int i = 0; i < 3; i++)
                    {
                        double y = 88 + i * 18;
                        dc.DrawRectangle(glass, null, new Rect(278, y, 27, 10));
                        dc.DrawLine(Stroke(leaf, 4), new Point(264, y + 15), new Point(319, y + 15));
                    }
                    dc.DrawGeometry(leaf, null, Shape("M74,158 Q149,138 208,153 Q275,124 339,142 Q390,125 440,153 V180 H0 Z"));
                    dc.DrawGeometry(cream, null, Shape("M108,180 Q185,149 235,164 Q326,130 440,151 V158 Q332,142 238,173 Q190,159 130,180 Z"));
                    dc.DrawGeometry(glass, edge, Shape("M102,151 V128 C103,110 113,101 133,101 C153,101 164,112 165,128 V151 Z"));
                    dc.DrawGeometry(null, Stroke(cream, 1.5), Shape("M104,127 H164 M133,102 V151 M120,104 Q110,127 119,151 M146,104 Q156,128 147,151 M102,141 H164"));
                    dc.DrawLine(Stroke(forest, 2), new Point(0, 103), new Point(440, 103));
                    dc.DrawGeometry(forest, null, Shape("M85,104 V149 H89 V104 Z M327,104 V146 H331 V104 Z"));
                    dc.DrawGeometry(glass, edge, Shape("M345,152 L366,130 H397 L376,153 Z"));
                    dc.DrawGeometry(null, Stroke(cream, .8), Shape("M355,142 H387 M362,135 H393 M360,151 L381,130 M369,151 L390,130"));
                    dc.DrawGeometry(null, Stroke(forest, 2), Shape("M72,110 L75,44 M372,105 L375,40"));
                });
                var turbines = new[] { new RotateTransform(0, 75, 44), new RotateTransform(0, 375, 40) };
                for (int i = 0; i < 2; i++)
                {
                    double x = i == 0 ? 75 : 375, y = i == 0 ? 44 : 40;
                    AddSceneDrawing(landscape, turbines[i], dc =>
                    {
                        for (int blade = 0; blade < 3; blade++)
                        {
                            dc.PushTransform(new RotateTransform(blade * 120, x, y));
                            dc.DrawGeometry(cream, edge, Shape("M" + x + "," + y + " l-2,-22 4,3 1,16 Z"));
                            dc.Pop();
                        }
                        dc.DrawEllipse(forest, null, new Point(x, y), 2.3, 2.3);
                    });
                }
                var tram = new TranslateTransform();
                AddSceneDrawing(landscape, tram, dc =>
                {
                    dc.DrawRoundedRectangle(cream, edge, new Rect(0, 88, 34, 13), 5, 5);
                    dc.DrawRoundedRectangle(forest, null, new Rect(5, 90, 24, 7), 2, 2);
                    dc.DrawLine(Stroke(cream, 1), new Point(17, 90), new Point(17, 97));
                    dc.DrawLine(edge, new Point(5, 102), new Point(29, 102));
                });
                var leaves = new TranslateTransform();
                AddSceneDrawing(landscape, leaves, dc =>
                {
                    for (int i = 0; i < 24; i++)
                    {
                        double x = 184 + i % 6 * 25, y = 62 + i / 6 * 20;
                        dc.DrawLine(Stroke(forest, .7), new Point(x, y), new Point(x - 1, y + 9));
                        dc.DrawEllipse(forest, null, new Point(x, y), 4 + i % 3, 4);
                        dc.DrawEllipse(leaf, null, new Point(x - 3, y - 3), 4, 3);
                        dc.DrawEllipse(Paint("91B676"), null, new Point(x + 2, y - 2), 3, 3);
                        if (i % 3 == 0)
                            dc.DrawGeometry(null, Stroke(leaf, 2), Shape("M" + (x + 4) + "," + (y + 4) + " q2,8 -1,12"));
                    }
                    dc.DrawEllipse(forest, null, new Point(244, 33), 14, 7);
                    dc.DrawEllipse(leaf, null, new Point(237, 30), 8, 5);
                    dc.DrawEllipse(leaf, null, new Point(249, 29), 7, 5);
                    dc.DrawGeometry(forest, null, Shape("M410,180 Q401,162 416,145 Q414,159 419,166 Q418,144 435,136 Q428,152 424,167 Q435,150 448,156 Q437,164 428,175 V180 Z M16,180 Q17,156 8,147 Q5,164 12,176 Q5,162 -5,162 V180 Z"));
                });
                FadeLandscapeEdge(landscape, "EDF1D4");
                return t => { turbines[0].Angle = t * 16; turbines[1].Angle = t * 13; tram.X = LoopX(t * 9 + 117, 440, 34); leaves.X = Math.Sin(t * .8) * .8; };
            }

            private Action<double> BuildDesktop()
            {
                var teal = Paint("008080");
                var pale = Paint("E9E9D4");
                var gray = Paint("C0C0C0");
                var shade = Paint("777A7F");
                var navy = Paint("000080");
                var monitor = Paint("073F46");
                var glass = Paint("17767A");
                var text = Paint("92D5CA");
                var shadow = Paint("184D53");
                var panelEdge = Stroke(shade, 1.5);
                var pixel = Stroke(pale, 1.5);
                var scan = new TranslateTransform();
                var cursor = new TranslateTransform();
                if (!hero)
                {
                    AddSceneDrawing(scenery, null, dc =>
                    {
                        dc.DrawRectangle(teal, null, new Rect(0, 0, SceneWidth, SceneHeight));
                        dc.DrawRectangle(gray, panelEdge, new Rect(0, 675, SceneWidth, 25));
                        dc.DrawLine(pixel, new Point(0, 676), new Point(SceneWidth, 676));
                        dc.DrawRectangle(gray, panelEdge, new Rect(8, 680, 89, 15));
                        dc.DrawRectangle(gray, panelEdge, new Rect(SceneWidth - 102, 680, 91, 15));
                        for (int i = 0; i < 4; i++)
                        {
                            double y = 36 + i * 74;
                            dc.DrawRectangle(shadow, null, new Rect(23, y + 3, 27, 20));
                            dc.DrawRectangle(gray, pixel, new Rect(20, y, 27, 20));
                            dc.DrawRectangle(navy, null, new Rect(23, y + 4, 20, 11));
                            dc.DrawRectangle(pale, null, new Rect(19, y + 28, 34, 2));
                        }
                    });
                }
                var deck = SceneVignette(hero ? 200 : 744, hero ? 12 : 477, hero ? .92 : 1.43);
                AddSceneDrawing(deck, null, dc =>
                {
                    dc.DrawGeometry(shadow, null, Shape("M12,31 H218 V119 H27 L12,104 Z"));
                    dc.DrawRectangle(gray, panelEdge, new Rect(85, 7, 135, 83));
                    dc.DrawRectangle(navy, null, new Rect(88, 10, 129, 13));
                    dc.DrawRectangle(pale, null, new Rect(94, 15, 42, 2));
                    dc.DrawRectangle(pale, panelEdge, new Rect(90, 26, 127, 52));
                    for (int i = 0; i < 4; i++)
                    {
                        dc.DrawRectangle(teal, null, new Rect(161, 34 + i * 9, 36 - (i * 7 % 21), 2));
                        dc.DrawRectangle(gray, panelEdge, new Rect(180 + i * 10, 13, 6, 6));
                    }
                    dc.DrawGeometry(shade, null, Shape("M92,23 H112 L122,32 V85 L104,100 H91 Z"));
                    dc.DrawGeometry(pale, panelEdge, Shape("M5,20 H100 V83 L95,90 H10 L5,85 Z"));
                    dc.DrawRectangle(shade, null, new Rect(11, 27, 83, 53));
                    dc.DrawRectangle(monitor, null, new Rect(15, 30, 75, 46));
                    dc.DrawGeometry(glass, null, Shape("M15,30 H90 V42 L17,67 H15 Z"));
                    dc.DrawRectangle(text, null, new Rect(19, 38, 22, 2));
                    dc.DrawRectangle(text, null, new Rect(19, 46, 33, 2));
                    dc.DrawRectangle(text, null, new Rect(19, 54, 27, 2));
                    dc.DrawRectangle(teal, null, new Rect(84, 83, 3, 2));
                    dc.DrawGeometry(gray, panelEdge, Shape("M41,90 H64 L68,102 82,108 V112 H21 V108 L38,102 Z"));
                    dc.DrawGeometry(navy, panelEdge, Shape("M125,65 H159 L165,71 V107 H125 Z"));
                    dc.DrawRectangle(gray, null, new Rect(132, 65, 23, 10));
                    dc.DrawRectangle(pale, null, new Rect(131, 83, 27, 17));
                    dc.DrawRectangle(teal, null, new Rect(133, 87, 21, 2));
                });
                AddSceneDrawing(deck, scan, dc => dc.DrawRectangle(text, null, new Rect(17, 34, 71, 1))).Opacity = .33;
                AddSceneDrawing(deck, cursor, dc => dc.DrawGeometry(Brushes.White, Stroke(navy, 1), Shape("M0,0 V14 L4,10 8,17 11,15 7,8 H13 Z")));
                return t =>
                {
                    scan.Y = Math.Floor(Wrap(t * 8, 39));
                    cursor.X = Math.Round(159 + Math.Sin(t * .69) * 33);
                    cursor.Y = Math.Round(67 + Math.Cos(t * .93) * 10);
                };
            }

            private Action<double> BuildCassette()
            {
                var green = Paint("25423A");
                var graphite = Paint("112421");
                var panel = Paint("323C35");
                var muted = Paint("829A83");
                var amber = Paint("F4BC6C");
                var rust = Paint("CD7248");
                var stripe = Stroke(muted, 1);
                var reels = new[] { new RotateTransform(0, 53, 63), new RotateTransform(0, 142, 63) };
                var meters = new[] { new ScaleTransform(.6, 1, 201, 85), new ScaleTransform(.5, 1, 201, 98) };
                if (!hero)
                {
                    AddSceneDrawing(scenery, null, dc =>
                    {
                        dc.DrawRectangle(graphite, null, new Rect(0, 0, SceneWidth, SceneHeight));
                        dc.DrawGeometry(green, null, Shape("M0,471 H119 L234,569 H920 L1039,463 H1100 V700 H0 Z"));
                        dc.DrawGeometry(null, stripe, Shape("M0,498 H105 L223,593 H928 L1048,491 H1100 M0,511 H94 L213,608 H941 L1060,506 H1100"));
                        dc.DrawRectangle(panel, stripe, new Rect(24, 45, 49, 307));
                        for (int i = 0; i < 18; i++)
                            dc.DrawLine(stripe, new Point(37, 56 + i * 16), new Point(i % 4 == 0 ? 60 : 52, 56 + i * 16));
                        dc.DrawLine(Stroke(amber, 2), new Point(35, 180), new Point(62, 180));
                    });
                }
                var deck = SceneVignette(hero ? 154 : 690, hero ? 10 : 504, hero ? 1 : 1.6);
                AddSceneDrawing(deck, null, dc =>
                {
                    dc.DrawGeometry(graphite, Stroke(muted, 1.5), Shape("M0,12 L12,0 H259 L274,15 V103 L261,124 H12 L0,109 Z"));
                    dc.DrawGeometry(panel, null, Shape("M7,13 L15,6 H256 L267,16 V27 H7 Z"));
                    dc.DrawRectangle(green, stripe, new Rect(11, 29, 179, 76));
                    dc.DrawRectangle(graphite, null, new Rect(17, 34, 167, 57));
                    dc.DrawRectangle(muted, null, new Rect(34, 53, 125, 21));
                    dc.DrawRectangle(graphite, null, new Rect(53, 59, 89, 8));
                    dc.DrawGeometry(null, stripe, Shape("M24,35 H144 L182,63 V87 H160 M21,93 H47 L59,101 H137 L149,93 H168"));
                    dc.DrawRectangle(panel, stripe, new Rect(196, 29, 67, 81));
                    dc.DrawRectangle(graphite, null, new Rect(200, 33, 59, 39));
                    dc.DrawGeometry(null, Stroke(amber, 1.1), Shape("M205,57 L213,51 219,57 224,44 229,60 236,53 240,46 245,50 253,45"));
                    dc.DrawRectangle(graphite, null, new Rect(201, 82, 56, 6));
                    dc.DrawRectangle(graphite, null, new Rect(201, 95, 56, 6));
                    dc.DrawRectangle(rust, null, new Rect(245, 12, 11, 4));
                    dc.DrawRectangle(amber, null, new Rect(228, 12, 11, 4));
                    dc.DrawRectangle(muted, null, new Rect(211, 12, 11, 4));
                    for (int i = 0; i < 3; i++)
                        dc.DrawRectangle(muted, stripe, new Rect(21 + 29 * i, 110, 23, 6));
                });
                for (int reel = 0; reel < reels.Length; reel++)
                {
                    double x = reel == 0 ? 53 : 142;
                    AddSceneDrawing(deck, reels[reel], dc =>
                    {
                        dc.DrawEllipse(graphite, stripe, new Point(x, 63), 23, 23);
                        dc.DrawEllipse(muted, stripe, new Point(x, 63), 15, 15);
                        for (int i = 0; i < 3; i++)
                        {
                            double phase = i * Math.PI * 2 / 3;
                            dc.DrawEllipse(graphite, null, new Point(x + 10 * Math.Cos(phase), 63 + 10 * Math.Sin(phase)), 3.4, 3.4);
                        }
                        dc.DrawEllipse(amber, null, new Point(x, 63), 3.2, 3.2);
                    });
                }
                for (int meter = 0; meter < meters.Length; meter++)
                {
                    double y = 82 + meter * 13;
                    AddSceneDrawing(deck, meters[meter], dc =>
                    {
                        for (int i = 0; i < 11; i++)
                            dc.DrawRectangle(i > 8 ? rust : amber, null, new Rect(201 + i * 5, y, 3.2, 6));
                    });
                }
                return t =>
                {
                    reels[0].Angle = Wrap(t * 69, 360);
                    reels[1].Angle = Wrap(t * 53, 360);
                    meters[0].ScaleX = .63 + .23 * Math.Sin(t * 3.1) + .12 * Math.Sin(t * 7);
                    meters[1].ScaleX = .66 + .22 * Math.Sin(t * 4.2) + .09 * Math.Sin(t * 11);
                };
            }

            private Action<double> BuildDecoHeader()
            {
                var fineGold = Stroke(Paint("A78349"), 1);
                var brightGold = Stroke(Paint("D4AF63"), 1.2);
                var bronze = Stroke(Paint("5C482D"), 1);
                AddSceneDrawing(scenery, null, dc =>
                {
                    dc.DrawRectangle(Tone("201915", "100E0D", 90), null, new Rect(0, 0, 1100, 200));
                    dc.DrawGeometry(Paint("181310"), fineGold, Shape(
                        "M10,54 H24 V31 H49 V12 H475 L488,22 H612 L625,12 H1051 V31 H1076 V54 H1090 V146 H1076 V169 H1051 V188 H625 L612,178 H488 L475,188 H49 V169 H24 V146 H10 Z"));
                    dc.DrawGeometry(null, bronze, Shape(
                        "M17,59 H31 V37 H56 V19 H472 L486,29 H614 L628,19 H1044 V37 H1069 V59 H1083 V141 H1069 V163 H1044 V181 H628 L614,171 H486 L472,181 H56 V163 H31 V141 H17 Z"));

                    // One half of the inlay is reflected on the central axis.
                    for (int side = 0; side < 2; side++)
                    {
                        if (side == 1) dc.PushTransform(new ScaleTransform(-1, 1, 550, 0));
                        dc.DrawGeometry(Tone("38291B", "14100F", 0), null, Shape(
                            "M31,40 H82 V56 H120 V71 H159 V86 H205 V101 H250 V117 H205 V132 H159 V147 H120 V162 H82 V178 H61 V160 H34 V140 H20 V61 H31 Z"));
                        dc.DrawGeometry(null, fineGold, Shape(
                            "M58,28 H89 V50 H128 V66 H168 V81 H211 V96 H259 M58,172 H89 V150 H128 V134 H168 V119 H211 V104 H259"));
                        dc.DrawGeometry(null, bronze, Shape(
                            "M42,48 H77 V63 H111 V77 H151 V91 H195 M42,152 H77 V137 H111 V123 H151 V109 H195"));
                        dc.DrawGeometry(null, bronze, Shape(
                            "M42,71 L195,100 42,129 M49,59 L183,100 49,141 M76,62 L170,100 76,138 M37,100 H232"));
                        dc.DrawGeometry(Paint("D4AF63"), null, Shape("M256,100 L263,96 270,100 263,104 Z"));
                        dc.DrawGeometry(Paint("916A39"), null, Shape("M278,100 L282,97 286,100 282,103 Z"));
                        if (side == 1) dc.Pop();
                    }

                    dc.DrawGeometry(null, fineGold, Shape("M430,160 H493 L505,166 H595 L607,160 H670"));
                    dc.DrawGeometry(null, bronze, Shape("M466,166 H489 L502,173 H598 L611,166 H634"));
                    dc.DrawGeometry(Paint("D4AF63"), null, Shape("M542,165 L550,161 558,165 550,169 Z"));
                });
                var sun = AddSceneDrawing(decoCrest, new TranslateTransform(), dc =>
                {
                    // All rails point to the same origin so the fan stays legible at narrow widths.
                    for (int ray = -7; ray <= 7; ray++)
                    {
                        double angle = ray * Math.PI / 17;
                        double outer = 44 - Math.Abs(ray) * .7;
                        var start = new Point(Math.Sin(angle) * 12, -Math.Cos(angle) * 12);
                        var end = new Point(Math.Sin(angle) * outer, -Math.Cos(angle) * outer);
                        dc.DrawLine(ray % 2 == 0 ? brightGold : fineGold, start, end);
                    }
                    dc.DrawGeometry(Paint("D4AF63"), null, Shape("M-10,-1 A10,10 0 0 1 10,-1 Z"));
                    dc.DrawGeometry(null, bronze, Shape("M-46,1 H46 M-39,5 H39 M-27,9 H27"));
                    dc.DrawGeometry(Paint("D4AF63"), null, Shape("M-54,1 L-50,-2 -46,1 -50,4 Z M46,1 L50,-2 54,1 50,4 Z"));
                });
                return t => sun.Opacity = .87 + .13 * Math.Cos(t * .62);
            }

            private Action<double> BuildDecoBackground()
            {
                var border = Stroke(Paint("726045"), 1);
                var filigree = Stroke(Paint("3B2D20"), 1);
                AddSceneDrawing(scenery, null, dc =>
                {
                    dc.DrawRectangle(Tone("1C1714", "100E0D", 90), null, new Rect(0, 0, SceneWidth, SceneHeight));
                    for (int side = 0; side < 2; side++)
                    {
                        if (side == 1) dc.PushTransform(new ScaleTransform(-1, 1, 550, 0));
                        dc.DrawGeometry(Tone("493724", "181410", 0), null, Shape(
                            "M0,0 H136 V44 H108 V92 H83 V137 H61 V563 H83 V608 H108 V656 H136 V700 H0 Z"));
                        dc.DrawGeometry(null, border, Shape("M125,16 V34 H98 V83 H72 V130 H49 V570 H72 V617 H98 V666 H125 V684"));
                        dc.DrawGeometry(null, filigree, Shape("M94,16 V26 H84 V74 H60 V123 H36 V577 H60 V626 H84 V674 H94 V684"));
                        dc.DrawGeometry(null, filigree, Shape("M22,121 V578 M12,136 V563 M112,19 H478 M112,681 H478"));
                        if (side == 1) dc.Pop();
                    }
                });
                var glints = AddSceneDrawing(scenery, new TranslateTransform(), dc =>
                {
                    dc.DrawGeometry(Paint("9D7B45"), null, Shape("M22,331 L28,350 22,369 16,350 Z M1078,331 L1084,350 1078,369 1072,350 Z"));
                    dc.DrawGeometry(Paint("ECCF89"), null, Shape("M22,341 L25,350 22,359 19,350 Z M1078,341 L1081,350 1078,359 1075,350 Z"));
                });
                return t => glints.Opacity = .44 + .18 * Math.Cos(t * .47);
            }

            private static LinearGradientBrush Tone(string start, string end, double angle)
            {
                var brush = new LinearGradientBrush(((SolidColorBrush)Paint(start)).Color, ((SolidColorBrush)Paint(end)).Color, angle);
                brush.Freeze();
                return brush;
            }

            private Action<double> BuildAtomicStation()
            {
                var cream = Paint("FFF0CF");
                var coral = Paint("E6674C");
                var petrol = Paint("0C6063");
                var tide = Paint("3F9D98");
                var shadow = Paint("8DBDB1");
                var glass = Paint("90C7BA");
                var gold = Paint("E7AF59");
                var rim = Stroke(petrol, 1.3);
                if (!hero)
                {
                    AddSceneDrawing(scenery, null, dc =>
                    {
                        dc.DrawGeometry(shadow, null, Shape("M0,700 V545 Q125,430 332,545 Q690,725 1100,442 V700 Z"));
                        dc.DrawGeometry(tide, null, Shape("M0,700 V602 Q226,526 399,570 Q716,679 1100,565 V700 Z"));
                        dc.DrawGeometry(petrol, null, Shape("M0,700 V645 Q199,574 398,632 Q773,723 1100,623 V700 Z"));
                        dc.DrawGeometry(null, Stroke(cream, 1.4), Shape("M0,676 Q202,601 398,652 Q765,744 1100,646"));
                    });
                }
                var station = SceneVignette(hero ? 158 : 650, hero ? 4 : 445, hero ? .95 : 1.65);
                AddSceneDrawing(station, null, dc =>
                {
                    dc.DrawEllipse(gold, null, new Point(216, 56), 42, 42);
                    dc.DrawEllipse(null, Stroke(petrol, .7), new Point(143, 56), 131, 35);
                    dc.DrawGeometry(coral, null, Shape("M245,18 L249,7 252,18 265,22 252,25 249,39 245,25 232,22 Z"));
                    dc.DrawGeometry(cream, null, Shape("M35,22 L38,13 40,22 49,24 40,27 38,35 35,27 27,24 Z"));
                    dc.DrawGeometry(shadow, null, Shape("M26,125 Q118,108 222,125 L245,132 H11 Z"));
                    dc.DrawGeometry(petrol, rim, Shape("M94,78 H108 L92,126 H83 Z M150,76 H165 L171,126 H157 Z"));
                    dc.DrawGeometry(coral, null, Shape("M98,78 H102 L87,124 H83 Z M159,75 H165 L171,126 H167 Z"));
                    dc.DrawGeometry(glass, rim, Shape("M48,71 L197,68 187,99 Q126,116 63,102 Z"));
                    for (int i = 0; i < 6; i++)
                        dc.DrawLine(rim, new Point(62 + i * 24, 78), new Point(69 + i * 22, 102 + (i % 3) * 2));
                    dc.DrawGeometry(petrol, null, Shape("M27,88 L98,70 205,74 223,68 210,85 135,92 H48 Z"));
                    dc.DrawGeometry(cream, rim, Shape("M13,76 L81,67 Q98,36 140,37 Q181,34 196,63 L232,53 212,75 131,83 Z"));
                    dc.DrawGeometry(tide, rim, Shape("M88,65 Q110,39 140,39 Q168,37 190,62 Z"));
                    dc.DrawGeometry(cream, null, Shape("M105,55 Q137,37 166,46 Q134,42 105,57 Z"));
                    dc.DrawGeometry(coral, null, Shape("M19,77 L131,84 213,76 207,79 131,88 36,81 Z"));
                    dc.DrawGeometry(petrol, null, Shape("M157,43 L173,15 176,16 161,43 Z"));
                    dc.DrawEllipse(coral, rim, new Point(175, 15), 3.6, 3.6);
                });
                var orbit = new TranslateTransform();
                AddSceneDrawing(station, orbit, dc =>
                {
                    dc.DrawGeometry(coral, rim, Shape("M-8,0 L-2,-2 8,-1 10,1 5,3 H-3 Z"));
                    dc.DrawGeometry(cream, rim, Shape("M-3,-2 Q1,-6 5,-2 Z"));
                    dc.DrawRectangle(gold, null, new Rect(-11, 0, 3, 1.5));
                });
                return t =>
                {
                    double phase = t * .58 + .5;
                    orbit.X = 143 + 128 * Math.Cos(phase);
                    orbit.Y = 56 + 35 * Math.Sin(phase);
                };
            }

            private DrawingGroup SceneVignette(double x, double y, double scale)
            {
                var position = new MatrixTransform(scale, 0, 0, scale, x, y);
                position.Freeze();
                var group = new DrawingGroup { Transform = position };
                scenery.Children.Add(group);
                return group;
            }

            private static DrawingGroup AddSceneDrawing(DrawingGroup parent, Transform motion, Action<DrawingContext> draw)
            {
                var group = new DrawingGroup();
                using (var dc = group.Open())
                    draw(dc);
                if (motion != null)
                    group.Transform = motion;
                else
                    group.Freeze();
                parent.Children.Add(group);
                return group;
            }

            private void BuildCyberpunk()
            {
                for (int i = 0; i < traffic.Length; i++)
                {
                    double y = hero ? 43 + i % 3 * 28 : 68 + i * 43;
                    string key = VehicleKeys[i % VehicleKeys.Length];
                    double size = VehicleWidth(i);
                    bool eastbound = i % 2 == 0;
                    double height = size * art[key].Height / art[key].Width;
                    traffic[i] = AddMovingDrawing(new TranslateTransform(0, y), dc =>
                    {
                        if (!eastbound) dc.PushTransform(new ScaleTransform(-1, 1, size / 2, 0));
                        dc.PushOpacity(.35);
                        dc.DrawLine(trace, new Point(-19, height * .67), new Point(-3, height * .67));
                        dc.Pop();
                        Sprite(dc, key, 0, 0, size, height);
                        if (!eastbound) dc.Pop();
                    });
                }
                carrier.Y = hero ? 10 : 62;
                carrierDrawing = AddMovingDrawing(carrier, dc => Sprite(dc, "carrier", 0, 0, CarrierWidth, hero ? 49 : 70));
                double rescueHeight = hero ? 52 : 68;
                rescueDrawing = AddMovingDrawing(rescue, dc => Sprite(dc, "rescue", 0, 0, RescueWidth, rescueHeight));
                using (var dc = rescueLights.Open())
                {
                    dc.DrawEllipse(cyan, null, new Point(RescueWidth * .09, rescueHeight * .6), 2.5, 1.1);
                    dc.DrawEllipse(pink, null, new Point(RescueWidth * .88, rescueHeight * .74), 2.5, 1.1);
                }
                rescueDrawing.Children.Add(rescueLights);
            }

            private DrawingGroup AddMovingDrawing(TranslateTransform position, Action<DrawingContext> draw)
            {
                var group = new DrawingGroup { Transform = position };
                using (var dc = group.Open()) draw(dc);
                scenery.Children.Add(group);
                return group;
            }

            private void MoveCyberpunk()
            {
                double w = SceneWidth;
                for (int i = 0; i < traffic.Length; i++)
                {
                    var position = (TranslateTransform)traffic[i].Transform;
                    double width = VehicleWidth(i);
                    double velocity = (15 + i * 3) * (i % 2 == 0 ? 1 : -1);
                    position.X = LoopX(seconds * velocity + 45 + i * 131, w, width);
                    traffic[i].Opacity = (.56 + i % 3 * .14) * FadeAtEdges(position.X, w, width);
                }
                carrier.X = LoopX(seconds * 6 + (hero ? 140 : 50), w, CarrierWidth);
                carrierDrawing.Opacity = FadeAtEdges(carrier.X, w, CarrierWidth);
                rescue.X = LoopX(seconds * 15 + w * .58, w, RescueWidth);
                rescueDrawing.Opacity = FadeAtEdges(rescue.X, w, RescueWidth);
                rescue.Y = (hero ? 72 : SceneHeight * .67) + Math.Sin(seconds * .5) * (hero ? 4 : 8);
                rescueLights.Opacity = .58 + .32 * Math.Sin(seconds * 2.1);
            }

            private static double VehicleWidth(int index) { return 32 + index % 3 * 10; }

            private void Steampunk(DrawingContext dc, double w, double h)
            {
                double shipWidth = hero ? 141 : 225;
                double shipX = LoopX(seconds * 5.5 + w * .58 - 100, w, shipWidth);
                double shipY = hero ? 10 : 95;
                EdgeSprite(dc, "airship", shipX, shipY + Math.Sin(seconds * .35) * 4, shipWidth, hero ? 62 : 99);
                EdgeSprite(dc, "airship", LoopX(seconds * -3 - 20, w, 75), hero ? 30 : 38, 75, 33);
                double mechWidth = hero ? 130 : 300;
                double mechLeft = LoopX(seconds * 5.8 + (hero ? 65 : 800) - mechWidth / 2, w, mechWidth);
                double mechX = mechLeft + mechWidth / 2;
                double floor = hero ? 146 : h - 20;
                double scale = hero ? .79 : 1.8;
                dc.PushOpacity(FadeAtEdges(mechLeft, w, mechWidth));
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
                dc.Pop(); dc.Pop(); dc.Pop();
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
                    double radius = hero ? 91 : 150;
                    double x = LoopX(seconds * (2 + i * .3) + i * w / 4 - 80 - radius, w, radius * 2) + radius;
                    double y = hero ? 50 + i * 25 : 95 + i * 113;
                    dc.PushOpacity(.10 + i % 2 * .04);
                    dc.DrawEllipse(paper, null, new Point(x, y), hero ? 87 : 150, hero ? 9 : 17);
                    dc.DrawEllipse(ink, null, new Point(x + 24, y + 6), hero ? 67 : 104, hero ? 4 : 7);
                    dc.Pop();
                }
                for (int i = 0; i < 3; i++)
                {
                    dc.PushTransform(new TranslateTransform(LoopX(seconds * 6 + w * .65 + i * 21 - 32, w, 24) + 12,
                        (hero ? 30 : h * .22) + i * 11 + Math.Sin(seconds * 1.2 + i) * 3));
                    dc.PushTransform(new ScaleTransform(.6, .5 + Math.Sin(seconds * 2 + i) * .23));
                    dc.DrawGeometry(null, jade, bird); dc.Pop(); dc.Pop();
                }
                double boatWidth = hero ? 82 : 143;
                double boat = LoopX(seconds * 3 + w * .38 - 50, w, boatWidth), water = h - (hero ? 10 : 29);
                EdgeSprite(dc, "boat", boat, water - (hero ? 31 : 54) + Math.Sin(seconds * .8) * 1.5, boatWidth, hero ? 31 : 54);
                dc.PushOpacity(.3 * FadeAtEdges(boat, w, boatWidth));
                for (int i = 0; i < 3; i++) dc.DrawLine(jade, new Point(boat - 25 - i * 4, water + i * 4), new Point(boat + 15 - i * 5, water + i * 4));
                dc.Pop();
            }

            private void Persona(DrawingContext dc, double w, double h)
            {
                for (int i = 0; i < (hero ? 4 : 9); i++)
                {
                    double x = LoopX(seconds * (2 + i % 3) + i * 149 - 78, w, 66) + 28;
                    double y = hero ? 30 + i % 3 * 40 : 28 + i * 79;
                    dc.PushTransform(new TranslateTransform(x, y + Math.Sin(seconds * .6 + i) * 6));
                    dc.PushTransform(new RotateTransform(-17 + Math.Sin(seconds * .17 + i) * 12));
                    dc.PushOpacity(hero ? .8 : .38);
                    dc.DrawGeometry(i % 2 == 0 ? red : white, darkTwo, i % 3 == 0 ? bolt : star);
                    dc.Pop(); dc.Pop(); dc.Pop();
                }
                double sweep = LoopX(seconds * 17 + w * .4 - 200, w, 200) + 100;
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
                        dc.DrawGeometry(Paint("323747"), Stroke(Paint("8A8FA6"), .8), Shape("M0,12 L8,8 H17 L27,2 H46 L58,10 68,12 71,16 66,21 H8 Z"));
                        dc.DrawGeometry(Paint("55727E"), null, Shape("M27,4 H35 V10 H19 Z M38,4 H46 L54,10 H38 Z"));
                        dc.DrawGeometry(Paint("55566B"), null, Shape("M9,12 H59 L64,14 H12 Z"));
                        dc.DrawGeometry(Paint("272837"), null, Shape("M11,17 H23 L22,21 H13 Z M48,17 H62 L61,21 H50 Z"));
                        dc.DrawRectangle(Paint("C5778B"), null, new Rect(3, 13, 7, 2));
                        dc.DrawRectangle(Paint("9EB9C8"), null, new Rect(60, 13, 9, 2));
                    });
                    Add("police", DrawCyberPatrol);
                    Add("luxury", DrawCyberLuxury);
                    Add("tuner", DrawCyberTuner);
                    Add("carrier", DrawCyberCarrier);
                    Add("rescue", DrawCyberRescue);
                }
                if (theme == UiTheme.Steampunk)
                {
                    Add("gear", dc =>
                    {
                        dc.DrawEllipse(iron, copper, new Point(), 16, 16);
                        for (int i = 0; i < 10; i++)
                        {
                            dc.PushTransform(new RotateTransform(i * 36));
                            dc.DrawRectangle(brass, null, new Rect(-2.6, -20, 5.2, 7));
                            dc.Pop();
                        }
                        dc.DrawEllipse(iron, copper, new Point(), 6.2, 6.2);
                    });
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

            private void DrawCyberPatrol(DrawingContext dc)
            {
                var hull = Paint("272A43");
                var plating = Paint("696B87");
                var cold = Paint("87B7DF");
                var warning = Paint("DE6E91");
                dc.DrawGeometry(hull, Stroke(plating, .8), Shape("M1,10 H16 L23,5 H58 L65,11 74,13 76,18 69,24 H8 L0,20 Z"));
                dc.DrawGeometry(plating, null, Shape("M17,12 L24,6 H44 L39,12 Z M48,7 H58 L63,12 H45 Z M2,17 H73 L71,20 H3 Z"));
                dc.DrawGeometry(Paint("343751"), null, Shape("M13,19 H31 L28,27 H11 Z M52,19 H72 L66,27 H50 Z M29,1 H48 L51,5 H28 Z"));
                dc.DrawRectangle(cold, null, new Rect(29, 1, 8, 2.8));
                dc.DrawRectangle(warning, null, new Rect(41, 1, 8, 2.8));
                dc.DrawGeometry(cold, null, Shape("M62,13 H70 L74,16 H62 Z M12,24 H24 V25.5 H12 Z M52,24 H66 V25.5 H52 Z"));
                dc.DrawGeometry(Paint("A9AAC6"), null, Shape("M35,15 H41 L39,19 36,21 33,18 Z"));
                dc.DrawRectangle(warning, null, new Rect(2, 12, 7, 2));
            }

            private void DrawCyberLuxury(DrawingContext dc)
            {
                var shell = Paint("8A819B");
                var shade = Paint("504B67");
                var trim = Paint("DEC5A6");
                dc.DrawGeometry(shell, Stroke(Paint("AFA6B8"), .8), Shape("M0,13 L8,9 18,8 32,2 Q45,0 58,3 L71,9 83,11 88,15 83,19 H8 Z"));
                dc.DrawGeometry(Paint("39374E"), null, Shape("M23,8 L33,4 Q46,2 56,4 L65,8 Z"));
                dc.DrawGeometry(shade, null, Shape("M0,14 H86 L82,19 H8 Z M14,16 H27 V21 H17 Z M61,16 H76 L74,21 H60 Z"));
                dc.DrawGeometry(trim, null, Shape("M12,11 H75 L81,12 H11 Z M78,14 H87 L85,16 H77 Z"));
                dc.DrawLine(Stroke(Paint("71617A"), .9), new Point(44, 4), new Point(45, 8));
                dc.DrawRectangle(Paint("C38194"), null, new Rect(2, 12, 8, 2));
            }

            private void DrawCyberTuner(DrawingContext dc)
            {
                var metal = Paint("5C395E");
                var paint = Paint("8A5082");
                var mark = Paint("E19BBC");
                var engine = Paint("BBB0C6");
                dc.DrawGeometry(metal, Stroke(Paint("A184AF"), .8), Shape("M1,3 H20 L19,6 H12 L13,12 H21 L29,4 H43 L58,13 71,15 74,20 65,24 H7 L1,21 Z"));
                dc.DrawGeometry(Paint("33364C"), null, Shape("M24,12 L31,6 H42 L52,12 Z"));
                dc.DrawGeometry(paint, null, Shape("M3,14 H30 L38,17 H57 L62,15 71,16 72,19 H38 L28,17 H4 Z"));
                dc.DrawGeometry(mark, null, Shape("M22,14 H33 L45,21 H33 Z M37,14 H42 L54,21 H49 Z M2,4 H20 L19,6 H2 Z"));
                dc.DrawGeometry(Paint("352B47"), null, Shape("M4,19 H24 L20,25 H4 Z M55,19 H70 L67,25 H54 Z"));
                dc.DrawGeometry(engine, null, Shape("M2,19 H10 V21 H1 Z M2,22 H9 V24 H1 Z M63,16 H72 V18 H63 Z"));
            }

            private void DrawCyberCarrier(DrawingContext dc)
            {
                var graphite = Paint("29273D");
                var steel = Paint("47435D");
                var yellow = Paint("CDB05E");
                var amber = Paint("976C46");
                var coolLight = Paint("8EABB9");
                var edge = Stroke(Paint("847894"), .75);

                // Freight containers ride under the hull between the two lift pods.
                dc.DrawGeometry(graphite, edge, Shape("M24,29 H150 L139,47 H45 L34,41 H24 Z"));
                dc.DrawGeometry(steel, null, Shape("M42,36 H139 L131,47 H48 Z"));
                dc.DrawGeometry(amber, null, Shape("M49,37 H75 V45 H51 Z M78,37 H104 V45 H78 Z M107,37 H132 L127,45 H107 Z"));
                dc.DrawGeometry(graphite, edge, Shape("M0,20 L17,10 32,10 43,2 H123 L134,5 H159 L183,19 V29 L164,38 H32 L21,32 H7 Z"));
                dc.DrawGeometry(steel, null, Shape("M33,11 L44,3 H123 L137,8 H158 L172,16 H44 Z"));
                dc.DrawGeometry(yellow, null, Shape("M27,12 H135 L124,33 H36 L27,27 Z"));
                dc.DrawGeometry(amber, null, Shape("M27,27 L36,33 H124 L127,28 H36 Z M7,22 H22 V27 H7 Z"));
                dc.DrawGeometry(dark, null, Shape("M140,11 H159 L173,20 H135 Z M16,13 H23 V17 H10 Z"));
                dc.DrawGeometry(coolLight, null, Shape("M142,13 H157 L165,18 H140 Z M171,25 H181 V27 H169 Z"));
                dc.DrawGeometry(graphite, edge, Shape("M16,29 H37 L42,34 V40 H19 L13,36 Z M139,28 H157 L169,34 L164,41 H140 L135,37 Z"));
                dc.DrawGeometry(amber, null, Shape("M18,32 H34 L38,35 H17 Z M142,31 H157 L164,35 H139 Z"));
                dc.DrawGeometry(coolLight, null, Shape("M20,37 H36 V38.5 H20 Z M141,37 H161 V39 H141 Z"));
                dc.DrawGeometry(dark, null, Shape("M34,16 H44 L40,20 H32 Z M32,23 H38 L34,27 H30 Z M115,17 H129 L128,19 H114 Z M112,23 H125 L124,25 H111 Z"));
                HullMark(dc, "鼎山航运", new Rect(44, 15, 65, 14), dark, false);
                HullMark(dc, "DS–06", new Rect(85, 5, 25, 5), Paint("C6B9CB"), false);
            }

            private void DrawCyberRescue(DrawingContext dc)
            {
                var armor = Paint("A9B1AF");
                var olive = Paint("475C63");
                var shadow = Paint("29353F");
                var signal = Paint("CF5976");
                var reflection = Paint("C1BACA");
                var coolLight = Paint("88B6C1");
                var edge = Stroke(shadow, .9);

                // The rear fan and the side thruster overlap the armored cabin.
                dc.DrawGeometry(shadow, edge, Shape("M2,20 L12,12 H30 L36,25 28,39 H9 L0,31 Z M29,31 H107 L116,41 L102,48 H33 L21,42 Z"));
                dc.DrawGeometry(olive, null, Shape("M4,21 L13,14 H27 L30,21 24,31 H8 Z M36,40 H104 L100,45 H38 Z"));
                dc.DrawGeometry(armor, edge, Shape("M11,27 L24,12 38,4 H87 L102,11 126,23 129,31 116,39 H24 L12,36 Z"));
                dc.DrawGeometry(reflection, null, Shape("M22,20 L39,5 H87 L101,12 H39 L31,20 Z"));
                dc.DrawGeometry(olive, null, Shape("M14,30 L28,23 H40 L35,35 H117 L111,38 H25 L15,35 Z M79,11 H97 L120,24 H106 L91,16 H77 Z"));
                dc.DrawGeometry(shadow, null, Shape("M91,12 H97 L114,21 H104 Z M102,12 L124,23 H117 Z M72,10 H83 L77,15 H70 Z"));
                dc.DrawGeometry(coolLight, null, Shape("M94,13 H97 L107,19 H103 Z M115,27 H127 V29 H113 Z M9,29 H18 V31 H8 Z"));
                dc.DrawGeometry(signal, null, Shape("M30,17 H34 V20 H38 V24 H34 V29 H30 V24 H25 V20 H30 Z M41,6 H56 L53,9 H38 Z M60,6 H68 L65,9 H57 Z"));
                dc.DrawGeometry(olive, edge, Shape("M12,31 H30 L41,37 V44 H17 L9,40 Z M99,30 H118 L124,34 V39 L114,44 H97 L92,38 Z"));
                dc.DrawGeometry(armor, null, Shape("M14,32 H30 L35,36 H12 Z M99,31 H116 L120,34 H96 Z"));
                dc.DrawGeometry(signal, null, Shape("M11,37 H24 L22,40 H13 Z M109,36 H122 L118,39 H107 Z"));
                dc.DrawGeometry(shadow, null, Shape("M52,27 H94 L89,37 H48 Z"));
                HullMark(dc, "创伤小组", new Rect(41, 15, 53, 10), shadow, true);
                HullMark(dc, "白金会员", new Rect(54, 28, 34, 8), Paint("D9D4DE"), true);
            }

            private static void HullMark(DrawingContext dc, string value, Rect plate, Brush brush, bool slanted)
            {
                var face = new Typeface(new FontFamily("Microsoft YaHei UI"), FontStyles.Normal, FontWeights.Bold, FontStretches.Normal);
                var text = new FormattedText(value, CultureInfo.GetCultureInfo("zh-CN"), FlowDirection.LeftToRight, face, 32, brush, 1);
                var ink = text.BuildGeometry(new Point());
                if (ink.Bounds.IsEmpty) return;
                ink = Geometry.Combine(ink, ink.GetWidenedPathGeometry(Stroke(brush, 1.3)), GeometryCombineMode.Union, null);
                Rect bounds = ink.Bounds;
                double sx = (plate.Width - (slanted ? 2 : 0)) / bounds.Width;
                double sy = plate.Height / bounds.Height;
                double shear = slanted ? -.17 : 0;
                ink.Transform = new MatrixTransform(sx, 0, shear * sy, sy,
                    plate.Left - bounds.Left * sx - bounds.Top * shear * sy + (slanted ? 2 : 0), plate.Top - bounds.Top * sy);

                // Sparse stencil breaks leave complex characters readable on the small sprites.
                var cuts = new GeometryGroup();
                cuts.Children.Add(new RectangleGeometry(new Rect(plate.Left + plate.Width * .17, plate.Top + plate.Height * .42, plate.Width * .09, .75)));
                cuts.Children.Add(new RectangleGeometry(new Rect(plate.Left + plate.Width * .70, plate.Top + plate.Height * .70, plate.Width * .13, .75)));
                dc.DrawGeometry(brush, null, Geometry.Combine(ink, cuts, GeometryCombineMode.Exclude, null));
            }

            private void Gear(DrawingContext dc, double x, double y, double radius, double angle)
            {
                dc.PushTransform(new RotateTransform(angle, x, y));
                Sprite(dc, "gear", x - radius, y - radius, radius * 2, radius * 2);
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

            private void EdgeSprite(DrawingContext dc, string key, double x, double y, double w, double h)
            {
                dc.PushOpacity(FadeAtEdges(x, SceneWidth, w));
                Sprite(dc, key, x, y, w, h);
                dc.Pop();
            }

            internal static double LoopX(double x, double sceneWidth, double spriteWidth)
            {
                const double overscan = 24;
                double left = -spriteWidth - overscan;
                return left + Wrap(x - left, sceneWidth + overscan - left);
            }

            internal static double FadeAtEdges(double x, double sceneWidth, double spriteWidth)
            {
                const double fadeWidth = 16;
                double amount = Math.Max(0, Math.Min(1, Math.Min(x, sceneWidth - x - spriteWidth) / fadeWidth));
                return amount * amount * (3 - 2 * amount);
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
