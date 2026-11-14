using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal sealed class StartupExperience
    {
        private enum Stage { Brand, Profile, Confirmed, Closing, Finished }

        private readonly Window window;
        private readonly PracticeSettings settings;
        private readonly Action focusPractice;
        private readonly FrameworkElement overlay, panels, brand, form, orbit, scanBeam, practicePage;
        private readonly TranslateTransform scan;
        private readonly RotateTransform orbitRotation;
        private readonly TextBlock title, hint, nicknameLabel, savedName, error, orbitText;
        private readonly TextBox nickname;
        private readonly Button confirm, skip;
        private readonly DispatcherTimer timer;
        private readonly StartupThemeEffects themeEffects;
        private Action next;
        private Stage stage;

        internal StartupExperience(Window window, PracticeSettings settings, Action focusPractice)
        {
            this.window = window;
            this.settings = settings;
            this.focusPractice = focusPractice;
            overlay = (FrameworkElement)window.FindName("StartupOverlay");
            panels = (FrameworkElement)window.FindName("StartupPanels");
            brand = (FrameworkElement)window.FindName("StartupBrand");
            form = (FrameworkElement)window.FindName("StartupForm");
            orbit = (FrameworkElement)window.FindName("StartupOrbit");
            scanBeam = (FrameworkElement)window.FindName("StartupScanBeam");
            practicePage = (FrameworkElement)window.FindName("PracticePage");
            scan = (TranslateTransform)window.FindName("StartupScan");
            orbitRotation = (RotateTransform)window.FindName("StartupOrbitRotation");
            title = (TextBlock)window.FindName("StartupTitle");
            hint = (TextBlock)window.FindName("StartupHint");
            nicknameLabel = (TextBlock)window.FindName("StartupNicknameLabel");
            savedName = (TextBlock)window.FindName("StartupSavedName");
            error = (TextBlock)window.FindName("StartupError");
            orbitText = (TextBlock)window.FindName("StartupOrbitText");
            nickname = (TextBox)window.FindName("StartupNickname");
            confirm = (Button)window.FindName("StartupConfirm");
            skip = (Button)window.FindName("StartupSkip");
            var icon = (Image)window.FindName("StartupIcon");
            icon.Source = window.Icon;
            if (settings.Theme == UiTheme.ArtDeco)
            {
                ((Panel)overlay).Background = StartupThemeEffects.DecoLacquer;
                var backdrops = ((Panel)panels).Children;
                foreach (UIElement ornament in backdrops)
                    ornament.Visibility = Visibility.Collapsed;
                backdrops.Add(new Border { Background = StartupThemeEffects.DecoFrame, Margin = new Thickness(15) });
                icon.Visibility = Visibility.Collapsed;
                var caption = (TextBlock)((Panel)brand).Children[1];
                caption.Foreground = StartupThemeEffects.DecoIvory;
                caption.FontSize = 40;
                orbitText.Foreground = StartupThemeEffects.DecoIvory;
                foreach (UIElement ornament in ((Panel)orbit).Children)
                {
                    if (ornament != orbitText)
                        ornament.Visibility = Visibility.Collapsed;
                }
                var loginCard = (Border)form;
                loginCard.Background = (Brush)window.FindResource("Panel");
                loginCard.BorderBrush = (Brush)window.FindResource("Accent");
                loginCard.BorderThickness = new Thickness(1, 2, 1, 2);
                loginCard.Padding = new Thickness(28, 21, 28, 23);
                title.HorizontalAlignment = HorizontalAlignment.Center;
                title.TextAlignment = hint.TextAlignment = savedName.TextAlignment = TextAlignment.Center;
                confirm.HorizontalAlignment = HorizontalAlignment.Center;
            }
            themeEffects = new StartupThemeEffects(window, settings.Theme);
            var startupLayers = (Grid)overlay;
            startupLayers.Children.Insert(startupLayers.Children.IndexOf(brand), themeEffects);

            timer = new DispatcherTimer(DispatcherPriority.Normal, window.Dispatcher);
            timer.Tick += (sender, e) =>
            {
                timer.Stop();
                var action = next;
                next = null;
                if (action != null)
                    action();
            };
            confirm.Click += (sender, e) => Confirm();
            skip.Click += (sender, e) => ShowProfile(false);
            nickname.TextChanged += (sender, e) => error.Visibility = Visibility.Hidden;
            overlay.PreviewKeyDown += (sender, e) =>
            {
                if (stage == Stage.Brand && (e.Key == Key.Escape || e.Key == Key.Enter || e.Key == Key.Space))
                {
                    e.Handled = true;
                    ShowProfile(false);
                }
            };
            window.Loaded += OnLoaded;
            window.Closed += (sender, e) =>
            {
                timer.Stop();
                themeEffects.Dispose();
            };
            practicePage.IsEnabled = false;
            overlay.Visibility = Visibility.Visible;
        }

        private static bool Motion { get { return SystemParameters.ClientAreaAnimation; } }

        private void OnLoaded(object sender, RoutedEventArgs e)
        {
            window.Loaded -= OnLoaded;
            overlay.Focus();
            if (!Motion)
            {
                ShowProfile(false);
                return;
            }
            Animate(brand, UIElement.OpacityProperty, 0, 1, 180);
            themeEffects.Play(false);
            After(550, () => ShowProfile(true));
        }

        private void ShowProfile(bool animate)
        {
            if (stage != Stage.Brand)
                return;
            stage = Stage.Profile;
            themeEffects.Dispose();
            timer.Stop();
            next = null;
            skip.Visibility = Visibility.Collapsed;
            string localName = PracticeSettings.NormalizeLocalNickname(settings.LocalNickname);
            bool registered = localName != null;
            title.Text = registered ? "登录本地账号" : "注册本地账号";
            hint.Text = registered ? "联机时会使用这个昵称。" : "设置一个昵称，联机时会自动填入。";
            savedName.Text = localName ?? "";
            savedName.Visibility = registered ? Visibility.Visible : Visibility.Collapsed;
            nickname.Visibility = nicknameLabel.Visibility = registered ? Visibility.Collapsed : Visibility.Visible;
            confirm.Content = registered ? "登录" : "注册";
            form.IsEnabled = false;
            form.Visibility = Visibility.Visible;
            if (animate && Motion)
            {
                Animate(brand, UIElement.OpacityProperty, brand.Opacity, 0, 170);
                Animate(panels, UIElement.OpacityProperty, 0, 1, 310);
                After(210, () =>
                {
                    brand.Visibility = Visibility.Collapsed;
                    Sweep(420);
                    RevealForm(true, registered);
                });
            }
            else
            {
                brand.Visibility = Visibility.Collapsed;
                panels.Opacity = 1;
                RevealForm(false, registered);
            }
        }

        private void RevealForm(bool animate, bool registered)
        {
            form.IsEnabled = true;
            if (animate)
                Animate(form, UIElement.OpacityProperty, 0, 1, 190);
            else
                form.Opacity = 1;
            if (registered)
                confirm.Focus();
            else
                nickname.Focus();
        }

        private void Confirm()
        {
            if (stage != Stage.Profile || !form.IsEnabled)
                return;
            string localName = PracticeSettings.NormalizeLocalNickname(settings.LocalNickname);
            if (localName == null)
            {
                localName = PracticeSettings.NormalizeLocalNickname(nickname.Text);
                if (localName == null)
                {
                    ShowError("请输入 1–20 个字符的昵称。");
                    return;
                }
                string previous = settings.LocalNickname;
                settings.LocalNickname = localName;
                if (!settings.Save())
                {
                    settings.LocalNickname = previous;
                    ShowError("无法保存昵称，请重试。");
                    return;
                }
            }

            stage = Stage.Confirmed;
            form.IsEnabled = false;
            if (!Motion)
            {
                Finish();
                return;
            }
            overlay.Focus();
            Animate(form, UIElement.OpacityProperty, form.Opacity, 0, 130);
            After(130, () =>
            {
                form.Visibility = Visibility.Collapsed;
                orbit.Visibility = Visibility.Visible;
                orbitText.Text = "准备就绪";
                themeEffects.Play(true);
                Animate(orbit, UIElement.OpacityProperty, 0, 1, 160);
                if (settings.Theme != UiTheme.Minimal && settings.Theme != UiTheme.ArtDeco)
                    Animate(orbitRotation, RotateTransform.AngleProperty, -32, 106, 450);
                Sweep(410);
                After(480, () =>
                {
                    stage = Stage.Closing;
                    Animate(overlay, UIElement.OpacityProperty, 1, 0, 200);
                    After(210, Finish);
                });
            });
        }

        private void ShowError(string message)
        {
            error.Text = message;
            error.Visibility = Visibility.Visible;
            nickname.Focus();
        }

        private void Finish()
        {
            stage = Stage.Finished;
            themeEffects.Dispose();
            overlay.Visibility = Visibility.Collapsed;
            practicePage.IsEnabled = true;
            focusPractice();
        }

        private void Sweep(int milliseconds)
        {
            if (settings.Theme == UiTheme.ArtDeco)
                return;
            double offset = overlay.ActualHeight / 2 + scanBeam.ActualHeight;
            Animate(scan, TranslateTransform.YProperty, -offset, offset, milliseconds);
            var fade = new DoubleAnimationUsingKeyFrames();
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.Zero));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(.66, TimeSpan.FromMilliseconds(milliseconds * .26)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, TimeSpan.FromMilliseconds(milliseconds)));
            scanBeam.BeginAnimation(UIElement.OpacityProperty, fade);
        }

        private void After(int milliseconds, Action action)
        {
            timer.Stop();
            next = action;
            timer.Interval = TimeSpan.FromMilliseconds(milliseconds);
            timer.Start();
        }

        private static void Animate(UIElement target, DependencyProperty property, double from, double to, int milliseconds)
        {
            target.BeginAnimation(property, Transition(from, to, milliseconds));
        }

        private static void Animate(Animatable target, DependencyProperty property, double from, double to, int milliseconds)
        {
            target.BeginAnimation(property, Transition(from, to, milliseconds));
        }

        private static DoubleAnimation Transition(double from, double to, int milliseconds)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
                { EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut } };
        }
    }
}
