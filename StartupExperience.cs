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
        private readonly Button confirm, skip, replay;
        private readonly DispatcherTimer timer;
        private Action next;
        private Stage stage;
        private bool playOnRequest;

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
            replay = (Button)window.FindName("StartupReplay");
            ((Image)window.FindName("StartupIcon")).Source = window.Icon;

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
            replay.Click += (sender, e) => StartBrand(true);
            nickname.TextChanged += (sender, e) => error.Visibility = Visibility.Hidden;
            overlay.PreviewKeyDown += (sender, e) =>
            {
                if (stage == Stage.Brand && e.Key == Key.Escape)
                {
                    e.Handled = true;
                    ShowProfile(false);
                }
            };
            window.ContentRendered += OnContentRendered;
            window.Closed += (sender, e) => timer.Stop();
            practicePage.IsEnabled = false;
            overlay.Visibility = Visibility.Visible;
        }

        private bool Motion { get { return playOnRequest || SystemParameters.ClientAreaAnimation; } }

        private void OnContentRendered(object sender, EventArgs e)
        {
            window.ContentRendered -= OnContentRendered;
            if (stage == Stage.Brand)
                StartBrand(false);
        }

        private void StartBrand(bool requested)
        {
            if (stage != Stage.Brand && stage != Stage.Profile)
                return;
            playOnRequest = requested;
            stage = Stage.Brand;
            form.Visibility = Visibility.Collapsed;
            form.BeginAnimation(UIElement.OpacityProperty, null);
            panels.BeginAnimation(UIElement.OpacityProperty, null);
            panels.Opacity = 0;
            scanBeam.BeginAnimation(UIElement.OpacityProperty, null);
            brand.Visibility = Visibility.Visible;
            skip.Visibility = Visibility.Visible;
            overlay.Focus();
            if (!Motion)
            {
                ShowProfile(false);
                return;
            }
            Animate(brand, UIElement.OpacityProperty, 0, 1, 180);
            After(650, () => ShowProfile(true));
        }

        private void ShowProfile(bool animate)
        {
            if (stage != Stage.Brand)
                return;
            stage = Stage.Profile;
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
                playOnRequest = false;
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
                Animate(orbit, UIElement.OpacityProperty, 0, 1, 160);
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
            overlay.Visibility = Visibility.Collapsed;
            practicePage.IsEnabled = true;
            focusPractice();
        }

        private void Sweep(int milliseconds)
        {
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
