using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Runtime.Versioning;
using System.Web.Script.Serialization;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: TargetFramework(".NETFramework,Version=v4.8")]
[assembly: System.Reflection.AssemblyTitle("鼎山打字通")]
[assembly: System.Reflection.AssemblyProduct("鼎山打字通")]
[assembly: System.Reflection.AssemblyDescription("中英文与代码打字练习、局域网贪吃蛇")]
[assembly: System.Reflection.AssemblyVersion("2.1.0.0")]

namespace EnglishTypingPractice
{
    internal sealed class Program
    {
        [STAThread]
        private static void Main()
        {
            Forms.Application.EnableVisualStyles();
            var app = new Application();
            Window window;
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("Window.xaml"))
                window = (Window)XamlReader.Load(stream);
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("TypingIcon.ico"))
                window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            new Practice(window, showStartup: true);
            app.Run(window);
        }
    }

    internal sealed class Practice
    {
        private static readonly Brush Correct = new SolidColorBrush(Color.FromRgb(160, 224, 200));
        private static readonly Brush Incorrect = new SolidColorBrush(Color.FromRgb(255, 190, 178));
        private static readonly Brush IncorrectBackground = new SolidColorBrush(Color.FromRgb(84, 40, 47));
        private static readonly Brush NextBackground = new SolidColorBrush(Color.FromRgb(83, 91, 35));

        private readonly Window window;
        private readonly PracticeSettings settings = PracticeSettings.Load();
        private readonly Random random = new Random();
        private readonly string[][] passages;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly MusicPlayback music;
        private readonly DispatcherTimer musicTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly SnakePractice snake;
        private readonly TypingEffects effects;
        private bool resumePractice;
        private bool? manualMusic;
        private bool syncingMusicPosition;
        private bool draggingMusicPosition;

        private readonly CheckBox matchCase;
        private readonly CheckBox includePunctuation;
        private readonly CheckBox playMusic;
        private readonly TextBlock passage;
        private readonly TextBox typed;
        private readonly ProgressBar progress;
        private readonly Border progressGlint;
        private readonly TextBlock count;
        private readonly TextBlock roundStatus;
        private readonly TextBlock speed;
        private readonly TextBlock accuracy;
        private readonly TextBlock elapsed;
        private readonly TextBlock musicStatus;
        private readonly ComboBox musicTrack;
        private readonly Button nextTrack;
        private readonly TextBlock settingsNotice;
        private readonly ComboBox practiceKind;
        private readonly ComboBox codeExerciseKind;
        private readonly ComboBox reviewLevel;
        private readonly Button newPassage;
        private readonly Button retry;
        private readonly TextBlock reviewMeaning;
        private readonly TextBlock practiceResult;
        private readonly HashSet<string>[] reviewBanks = new HashSet<string>[2];

        private int lastTopic = -1;
        private string original = "";
        private string target = "";
        private Run[] letters = new Run[0];
        private int correctCount;
        private bool resetting;
        private bool updatingPlaylist;
        private bool composingChinese;
        private int roundGeneration;
        private string committedChinese = "";
        private TypingRound round;
        private string lastRoundResult;
        private string priorRoundResult;

        internal Practice(Window window) : this(window, false)
        {
        }

        internal Practice(Window window, bool showStartup)
        {
            this.window = window;
            using (var reader = new StreamReader(typeof(Practice).Assembly.GetManifestResourceStream("Passages.json")))
                passages = new JavaScriptSerializer().Deserialize<string[][]>(reader.ReadToEnd());

            practiceKind = (ComboBox)window.FindName("PracticeKind");
            codeExerciseKind = (ComboBox)window.FindName("CodeExerciseKind");
            reviewLevel = (ComboBox)window.FindName("ReviewLevel");
            newPassage = (Button)window.FindName("NewPassage");
            retry = (Button)window.FindName("Retry");
            reviewMeaning = (TextBlock)window.FindName("ReviewMeaning");
            practiceResult = (TextBlock)window.FindName("PracticeResult");
            var workbench = (Grid)window.FindName("PracticeWorkbench");
            var sidebar = (FrameworkElement)window.FindName("PracticeSidebar");
            var sidebarColumn = (ColumnDefinition)window.FindName("PracticeSidebarColumn");
            workbench.SizeChanged += (sender, e) =>
            {
                bool wide = e.NewSize.Width >= 900;
                sidebarColumn.Width = new GridLength(wide ? 349 : 0);
                Grid.SetRow(sidebar, wide ? 0 : 1);
                Grid.SetColumn(sidebar, wide ? 1 : 0);
                sidebar.Margin = wide ? new Thickness(13, 0, 0, 0) : new Thickness(0, 10, 0, 0);
            };
            matchCase = (CheckBox)window.FindName("MatchCase");
            includePunctuation = (CheckBox)window.FindName("IncludePunctuation");
            playMusic = (CheckBox)window.FindName("PlayMusic");
            passage = (TextBlock)window.FindName("Passage");
            typed = (TextBox)window.FindName("Typed");
            progress = (ProgressBar)window.FindName("Progress");
            progressGlint = (Border)window.FindName("ProgressGlint");
            count = (TextBlock)window.FindName("Count");
            roundStatus = (TextBlock)window.FindName("RoundStatus");
            speed = (TextBlock)window.FindName("Speed");
            accuracy = (TextBlock)window.FindName("Accuracy");
            elapsed = (TextBlock)window.FindName("Elapsed");
            musicStatus = (TextBlock)window.FindName("MusicStatus");
            musicTrack = (ComboBox)window.FindName("MusicTrack");
            nextTrack = (Button)window.FindName("NextTrack");
            settingsNotice = (TextBlock)window.FindName("SettingsNotice");
            var feedback = (CheckBox)window.FindName("TypingFeedback");
            feedback.IsChecked = settings.TypingFeedback;
            effects = new TypingEffects(window, passage, (Canvas)window.FindName("WordEcho"));
            feedback.Click += (sender, e) =>
            {
                settings.TypingFeedback = feedback.IsChecked == true;
                if (!settings.TypingFeedback)
                    effects.Reset();
                SaveSettings();
                typed.Focus();
            };

            music = new MusicPlayback(text => musicStatus.Text = text, UpdatePlaylist);
            snake = new SnakePractice(window, running => music.SetPlaying(settings.PlayMusic && running), settings,
                SaveSettings, effects.ShakeWindow);
            var practicePage = (FrameworkElement)window.FindName("PracticePage");
            var snakePage = (FrameworkElement)window.FindName("SnakePage");
            var rulesPage = (FrameworkElement)window.FindName("SnakeRules");
            var closeRules = (Button)window.FindName("CloseSnakeRules");
            Action returnToSnake = () =>
            {
                rulesPage.Visibility = Visibility.Collapsed;
                RevealPage(snakePage, -10);
                snake.Focus();
            };
            ((Button)window.FindName("OpenSnakeRules")).Click += (sender, e) =>
            {
                snakePage.Visibility = Visibility.Collapsed;
                RevealPage(rulesPage, 10);
                ((ScrollViewer)window.FindName("SnakeRulesScroll")).ScrollToTop();
                closeRules.Focus();
            };
            closeRules.Click += (sender, e) => returnToSnake();
            rulesPage.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    returnToSnake();
                    e.Handled = true;
                }
            };
            ((Button)window.FindName("OpenSnake")).Click += (sender, e) =>
            {
                resumePractice = clock.IsRunning;
                effects.Reset();
                clock.Stop();
                timer.Stop();
                music.SetPlaying(false);
                practicePage.Visibility = Visibility.Collapsed;
                RevealPage(snakePage, 10);
                snake.Focus();
            };
            ((Button)window.FindName("CloseSnake")).Click += (sender, e) =>
            {
                snake.Leave();
                snakePage.Visibility = Visibility.Collapsed;
                RevealPage(practicePage, -10);
                if (resumePractice)
                {
                    clock.Start();
                    timer.Start();
                }
                else if (IsReview && original.Length == 0)
                    ChoosePassage();
                UpdatePracticeMusic();
                typed.Focus();
            };
            matchCase.IsChecked = settings.MatchCase;
            includePunctuation.IsChecked = settings.IncludePunctuation;
            playMusic.IsChecked = settings.PlayMusic;
            musicStatus.Text = "尚未选择音乐文件夹";
            ConfigureMusicControls();

            matchCase.Click += ChangePracticeOptions;
            includePunctuation.Click += ChangePracticeOptions;
            playMusic.Click += (sender, e) =>
            {
                settings.PlayMusic = playMusic.IsChecked == true;
                manualMusic = null;
                SaveSettings();
                UpdatePracticeMusic();
            };
            newPassage.Click += (sender, e) =>
            {
                ChoosePassage();
                typed.Focus();
            };
            retry.Click += (sender, e) =>
            {
                if (lastRoundResult != null)
                    priorRoundResult = lastRoundResult;
                ResetRound();
                typed.Focus();
            };
            SelectionChangedEventHandler changeExercise = (sender, e) =>
            {
                lastTopic = -1;
                ChoosePassage();
                typed.Focus();
            };
            practiceKind.SelectionChanged += changeExercise;
            codeExerciseKind.SelectionChanged += changeExercise;
            reviewLevel.SelectionChanged += changeExercise;
            typed.PreviewKeyDown += (sender, e) =>
            {
                if (IsCode && !typed.IsReadOnly && e.Key == Key.Tab && Keyboard.Modifiers == ModifierKeys.None)
                {
                    if (typed.Text.Replace("\r\n", "\n").Length - typed.SelectedText.Replace("\r\n", "\n").Length <= target.Length - 4)
                    {
                        int caret = typed.SelectionStart;
                        typed.SelectedText = "    ";
                        typed.CaretIndex = caret + 4;
                    }
                    e.Handled = true;
                }
            };
            ((Button)window.FindName("ChooseFolder")).Click += ChooseMusicFolder;
            typed.TextChanged += OnTextChanged;
            TextCompositionManager.AddPreviewTextInputStartHandler(typed, OnCompositionStart);
            TextCompositionManager.AddPreviewTextInputUpdateHandler(typed, OnCompositionStart);
            TextCompositionManager.AddPreviewTextInputHandler(typed, OnCompositionCommit);
            DataObject.AddPastingHandler(typed, (sender, e) => e.CancelCommand());
            timer.Tick += (sender, e) => RefreshClock();
            window.Loaded += async (sender, e) =>
            {
                if (!showStartup)
                    typed.Focus();
                musicTimer.Start();
                if (!string.IsNullOrEmpty(settings.Folder))
                    await LoadMusicFolder();
            };
            window.Closed += (sender, e) =>
            {
                timer.Stop();
                musicTimer.Stop();
                snake.Leave();
                if (musicTrack.SelectedValue is string)
                    RememberTrack();
                music.Dispose();
                effects.Dispose();
            };
            ConfigureBackgroundMotion((TranslateTransform)window.FindName("BackgroundDrift"), practicePage);
            ChoosePassage();
            if (showStartup)
                new StartupExperience(window, settings, () => typed.Focus());
        }

        private static void RevealPage(FrameworkElement page, double offset)
        {
            page.Visibility = Visibility.Visible;
            if (!SystemParameters.ClientAreaAnimation)
                return;
            var slide = new TranslateTransform();
            page.RenderTransform = slide;
            var duration = TimeSpan.FromMilliseconds(190);
            var ease = new QuadraticEase { EasingMode = EasingMode.EaseOut };
            slide.BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(offset, 0, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
            page.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(.86, 1, duration) { EasingFunction = ease, FillBehavior = FillBehavior.Stop });
        }

        private void ConfigureBackgroundMotion(TranslateTransform drift, FrameworkElement practicePage)
        {
            ClockGroup motion = null;
            Action update = () =>
            {
                if (!SystemParameters.ClientAreaAnimation || (!practicePage.IsVisible && settings.SnakeLowEffects))
                {
                    drift.ApplyAnimationClock(TranslateTransform.XProperty, null);
                    drift.ApplyAnimationClock(TranslateTransform.YProperty, null);
                    motion = null;
                    return;
                }
                if (motion == null)
                {
                    if (window.WindowState == WindowState.Minimized)
                        return;
                    var ease = new SineEase { EasingMode = EasingMode.EaseInOut };
                    var duration = TimeSpan.FromSeconds(13);
                    var timeline = new ParallelTimeline { AutoReverse = true, RepeatBehavior = RepeatBehavior.Forever };
                    timeline.Children.Add(new DoubleAnimation(-.16, .13, duration) { EasingFunction = ease });
                    timeline.Children.Add(new DoubleAnimation(.06, -.08, duration) { EasingFunction = ease });
                    motion = (ClockGroup)timeline.CreateClock(true);
                    drift.ApplyAnimationClock(TranslateTransform.XProperty, (AnimationClock)motion.Children[0]);
                    drift.ApplyAnimationClock(TranslateTransform.YProperty, (AnimationClock)motion.Children[1]);
                }
                if (window.WindowState == WindowState.Minimized)
                    motion.Controller.Pause();
                else
                    motion.Controller.Resume();
            };
            System.ComponentModel.PropertyChangedEventHandler changed = (sender, e) =>
            {
                if (e.PropertyName == "ClientAreaAnimation")
                    update();
            };
            SystemParameters.StaticPropertyChanged += changed;
            window.StateChanged += (sender, e) => update();
            practicePage.IsVisibleChanged += (sender, e) => update();
            var lowEffects = (CheckBox)window.FindName("SnakeLowEffects");
            lowEffects.Click += (sender, e) => update();
            window.Loaded += (sender, e) => update();
            window.Closed += (sender, e) =>
            {
                SystemParameters.StaticPropertyChanged -= changed;
                if (motion != null)
                    motion.Controller.Stop();
            };
        }

        private void ChoosePassage()
        {
            priorRoundResult = null;
            string[] choices;
            if (IsReview)
            {
                int level = reviewLevel.SelectedIndex;
                var bank = level > 0 ? ReviewBank(level - 1) : null;
                choices = settings.SnakeReviewWords.Where(item => item.Value > 0 && SnakeWords.Contains(item.Key) &&
                        (bank == null || bank.Contains(item.Key)))
                    .OrderByDescending(item => item.Value).ThenBy(item => item.Key).Take(50).Select(item => item.Key).ToArray();
            }
            else
            {
                choices = passages[practiceKind.SelectedIndex];
                if (IsCode)
                    choices = TypingRound.CodeExercises(choices, codeExerciseKind.SelectedIndex);
            }
            if (choices.Length == 0)
            {
                original = "";
                lastTopic = -1;
                ResetRound();
                roundStatus.Text = IsReview ? "这个词库暂无待复习词" : "这个题型暂无练习";
                return;
            }
            if (choices.Length == 1 || lastTopic >= choices.Length)
                lastTopic = -1;
            int index = random.Next(lastTopic < 0 ? choices.Length : choices.Length - 1);
            if (lastTopic >= 0 && index >= lastTopic)
                index++;
            lastTopic = index;
            original = choices[index];
            ResetRound();
        }

        private HashSet<string> ReviewBank(int index)
        {
            if (reviewBanks[index] != null)
                return reviewBanks[index];
            var words = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            using (var reader = new StreamReader(typeof(Practice).Assembly.GetManifestResourceStream(index == 0 ? "Cet4.tsv" : "Cet6.tsv")))
            {
                string line;
                while ((line = reader.ReadLine()) != null)
                {
                    int separator = line.IndexOf('\t');
                    if (separator > 0)
                        words.Add(line.Substring(0, separator));
                }
            }
            return reviewBanks[index] = words;
        }

        private bool IsReview { get { return practiceKind.SelectedIndex == passages.Length; } }
        private bool IsCode { get { return practiceKind.SelectedIndex >= 2 && !IsReview; } }
        private bool IsChinese { get { return practiceKind.SelectedIndex == 1; } }
        private bool MatchExactCase { get { return IsCode || settings.MatchCase; } }

        private void UpdatePracticeMusic()
        {
            music.SetPlaying(manualMusic ?? (settings.PlayMusic && clock.IsRunning));
        }

        private void ChangePracticeOptions(object sender, RoutedEventArgs e)
        {
            settings.MatchCase = matchCase.IsChecked == true;
            settings.IncludePunctuation = includePunctuation.IsChecked == true;
            SaveSettings();
            priorRoundResult = null;
            ResetRound();
        }

        private void ResetRound()
        {
            effects.Reset();
            progressGlint.BeginAnimation(UIElement.OpacityProperty, null);
            timer.Stop();
            clock.Reset();
            UpdatePracticeMusic();
            resetting = true;
            roundGeneration++;
            composingChinese = false;
            committedChinese = "";
            typed.Clear();
            typed.IsReadOnly = original.Length == 0;
            retry.IsEnabled = original.Length != 0;
            reviewMeaning.Visibility = Visibility.Collapsed;
            practiceResult.Visibility = Visibility.Collapsed;
            lastRoundResult = null;
            codeExerciseKind.Visibility = IsCode ? Visibility.Visible : Visibility.Collapsed;
            reviewLevel.Visibility = IsReview ? Visibility.Visible : Visibility.Collapsed;
            newPassage.Content = IsReview ? "换个词" : IsCode ? "换一题" : "换一篇";
            retry.Content = IsReview ? "重练本词" : IsCode ? "重练本题" : "重练本篇";

            matchCase.IsEnabled = !IsCode && !IsChinese;
            includePunctuation.IsEnabled = !IsCode && !IsReview;
            typed.AcceptsReturn = IsCode;
            InputMethod.SetIsInputMethodEnabled(typed, IsChinese);
            var font = new FontFamily(IsChinese ? "Microsoft YaHei UI" : "Consolas");
            passage.FontFamily = typed.FontFamily = font;
            passage.FontSize = typed.FontSize = IsCode ? 18 : 20;
            typed.Height = IsCode ? 155 : 96;
            ((TextBlock)window.FindName("PracticeTitle")).Text = IsReview ? "复习单词" : IsCode ? "练习代码" : "练习短文";
            ((TextBlock)window.FindName("PracticeHint")).Text = IsCode
                ? "照着示例输入，保留大小写、符号和换行。Tab 输入 4 个空格。"
                : IsChinese ? "用中文输入法照着短文输入，汉字上屏后开始计时。"
                : IsReview ? "复习本机贪吃蛇里打错或清空的词。拼写正确后揭晓中文释义。"
                : "照着四级英语短文输入，敲下第一个字符开始计时。";
            ((TextBlock)window.FindName("SpeedLabel")).Text = IsChinese ? "速度（字/分）" : "速度 (WPM)";
            speed.ToolTip = IsChinese ? "每分钟输入的正确汉字数。" : "每 5 个正确字符折算为 1 个词。";
            target = IsCode || IsReview || settings.IncludePunctuation ? original : new string(original.Where(c => !char.IsPunctuation(c)).ToArray());
            round = new TypingRound(target, MatchExactCase, IsCode, IsChinese);
            typed.MaxLength = target.Length + target.Count(c => c == '\n');
            progress.Maximum = target.Length;
            letters = target.Select(c => new Run(c == '\n' ? "" : c.ToString())).ToArray();
            passage.Inlines.Clear();
            for (int i = 0; i < letters.Length; i++)
            {
                passage.Inlines.Add(letters[i]);
                if (target[i] == '\n')
                    passage.Inlines.Add(new LineBreak());
            }

            roundStatus.Text = original.Length != 0 ? "未开始" : IsReview ? "这个词库暂无待复习词" : "这个题型暂无练习";
            resetting = false;
            RefreshProgress();
            RefreshClock();
        }

        private void OnCompositionStart(object sender, TextCompositionEventArgs e)
        {
            if (IsChinese && !typed.IsReadOnly)
                composingChinese = true;
        }

        private void OnCompositionCommit(object sender, TextCompositionEventArgs e)
        {
            if (!IsChinese || !composingChinese)
                return;
            int generation = roundGeneration;
            typed.Dispatcher.BeginInvoke(DispatcherPriority.Input, new Action(() =>
            {
                if (generation != roundGeneration)
                    return;
                composingChinese = false;
                AcceptInput(null);
            }));
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (!composingChinese)
                AcceptInput(e);
        }

        private void AcceptInput(TextChangedEventArgs change)
        {
            if (resetting || typed.IsReadOnly)
                return;

            if (IsChinese)
                committedChinese = typed.Text;
            string input = ReadInput();
            if (input.Length == 0 && !clock.IsRunning)
                return;

            if (!clock.IsRunning)
            {
                clock.Start();
                timer.Start();
                roundStatus.Text = "练习中";
                UpdatePracticeMusic();
            }

            int previousCorrect = correctCount;
            bool inputChanged = round.Observe(input, clock.Elapsed.TotalSeconds);
            RefreshProgress();
            bool complete = input.Length >= target.Length && (!IsReview || correctCount == target.Length);
            if (correctCount > previousCorrect)
                HighlightProgress(complete);
            if (settings.TypingFeedback)
            {
                if (IsChinese)
                {
                    if (complete || inputChanged && (change == null || change.Changes.Any(item => item.AddedLength > 0)))
                        effects.Play(null, null, complete);
                }
                else
                    PlayFeedback(change, complete);
            }
            if (complete)
            {
                clock.Stop();
                timer.Stop();
                typed.IsReadOnly = true;
                roundStatus.Text = "本轮完成";
                UpdatePracticeMusic();
            }
            RefreshClock();
            if (complete)
            {
                lastRoundResult = speed.Text + (IsChinese ? " 字/分" : " WPM") + "　准确率 " + round.Accuracy;
                practiceResult.Text = "本轮成绩　" + lastRoundResult +
                    (priorRoundResult == null ? "" : "\n上次成绩　" + priorRoundResult) + "\n" + round.Summary();
                practiceResult.Visibility = Visibility.Visible;
                if (IsReview)
                {
                    reviewMeaning.Text = SnakeWords.Meaning(target);
                    reviewMeaning.Visibility = Visibility.Visible;
                }
            }
        }

        private void HighlightProgress(bool complete)
        {
            if (!SystemParameters.ClientAreaAnimation || !progress.IsVisible || progress.ActualWidth == 0)
                return;
            double edge = progress.ActualWidth * progress.Value / progress.Maximum;
            var duration = TimeSpan.FromMilliseconds(complete ? 420 : 185);
            ((TranslateTransform)progressGlint.RenderTransform).BeginAnimation(TranslateTransform.XProperty,
                new DoubleAnimation(edge - progressGlint.Width, edge - 7, duration) { FillBehavior = FillBehavior.Stop });
            progressGlint.BeginAnimation(UIElement.OpacityProperty,
                new DoubleAnimation(complete ? 1 : .78, 0, duration) { FillBehavior = FillBehavior.Stop });
        }

        private void PlayFeedback(TextChangedEventArgs e, bool complete)
        {
            string input = ReadInput();
            int wordStart = -1;
            int wordEnd = -1;
            bool letterTyped = false;
            string rawInput = typed.Text;
            foreach (var change in e.Changes)
            {
                int startOffset = change.Offset;
                int endOffset = Math.Min(rawInput.Length, change.Offset + change.AddedLength);
                if (IsCode)
                {
                    startOffset = rawInput.Substring(0, startOffset).Replace("\r\n", "\n").Length;
                    endOffset = rawInput.Substring(0, endOffset).Replace("\r\n", "\n").Length;
                }
                for (int i = startOffset; i < Math.Min(input.Length, endOffset); i++)
                {
                    letterTyped |= IsCode || char.IsLetter(input[i]);
                    if (i >= target.Length || !IsWordCharacter(i) || (i + 1 < target.Length && IsWordCharacter(i + 1)))
                        continue;
                    int start = i;
                    while (start > 0 && IsWordCharacter(start - 1))
                        start--;
                    int length = i - start + 1;
                    var comparison = MatchExactCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    if (string.Compare(input, start, target, start, length, comparison) == 0)
                    {
                        wordStart = start;
                        wordEnd = i;
                    }
                }
            }
            if (letterTyped || complete)
                effects.Play(wordStart < 0 ? null : letters[wordStart],
                    wordStart < 0 ? null : target.Substring(wordStart, wordEnd - wordStart + 1), complete);
        }

        private bool IsWordCharacter(int index)
        {
            char character = target[index];
            if (IsCode)
                return char.IsLetterOrDigit(character) || character == '_';
            return char.IsLetter(character) || ((character == '\'' || character == '’' || character == '-') &&
                index > 0 && index + 1 < target.Length &&
                char.IsLetter(target[index - 1]) && char.IsLetter(target[index + 1]));
        }

        private void RefreshProgress()
        {
            string input = ReadInput();
            correctCount = 0;
            for (int i = 0; i < letters.Length; i++)
            {
                var letter = letters[i];
                bool entered = i < input.Length;
                bool correct = entered && (MatchExactCase
                    ? input[i] == target[i]
                    : char.ToUpperInvariant(input[i]) == char.ToUpperInvariant(target[i]));
                bool incorrect = entered && !correct;
                if (correct)
                    correctCount++;

                letter.Foreground = correct ? Correct : incorrect ? Incorrect : passage.Foreground;
                letter.Background = incorrect ? IncorrectBackground : i == input.Length ? NextBackground : null;
                letter.TextDecorations = incorrect ? TextDecorations.Underline : null;
            }

            count.Text = input.Length + " / " + target.Length;
            progress.Value = input.Length;
            accuracy.Text = round.Accuracy;
        }

        private string ReadInput()
        {
            return (IsChinese ? committedChinese : typed.Text).Replace("\r\n", "\n");
        }

        private void RefreshClock()
        {
            TimeSpan duration = clock.Elapsed;
            elapsed.Text = string.Format("{0}:{1:00}", (int)duration.TotalMinutes, duration.Seconds);
            int units = correctCount;
            if (IsChinese)
            {
                string input = ReadInput();
                units = Enumerable.Range(0, Math.Min(input.Length, target.Length))
                    .Count(i => input[i] == target[i] && target[i] >= '\u3400' && target[i] <= '\u9fff');
            }
            speed.Text = duration.TotalSeconds >= 1 || (typed.IsReadOnly && target.Length != 0)
                ? Math.Round(units * (IsChinese ? 60.0 : 12.0) / Math.Max(1, duration.TotalSeconds)).ToString(CultureInfo.CurrentCulture)
                : "0";
        }

        private async void ChooseMusicFolder(object sender, RoutedEventArgs e)
        {
            using (var dialog = new Forms.FolderBrowserDialog())
            {
                dialog.Description = "选择包含练习音乐的文件夹";
                dialog.ShowNewFolderButton = false;
                if (Directory.Exists(settings.Folder))
                    dialog.SelectedPath = settings.Folder;

                if (dialog.ShowDialog() != Forms.DialogResult.OK)
                    return;
                if (!string.Equals(settings.Folder, dialog.SelectedPath, StringComparison.OrdinalIgnoreCase))
                    settings.MusicTrack = null;
                settings.Folder = dialog.SelectedPath;
            }

            SaveSettings();
            typed.Focus();
            await LoadMusicFolder();
        }

        private System.Threading.Tasks.Task LoadMusicFolder()
        {
            musicStatus.ToolTip = settings.Folder;
            return music.LoadFolder(settings.Folder, settings.MusicTrack);
        }

        private void ConfigureMusicControls()
        {
            var mode = (ComboBox)window.FindName("MusicMode");
            var volume = (Slider)window.FindName("MusicVolume");
            var volumeLabel = (TextBlock)window.FindName("VolumeLabel");
            var toggle = (Button)window.FindName("ToggleMusic");
            var position = (Slider)window.FindName("MusicPosition");
            var time = (TextBlock)window.FindName("MusicTime");
            toggle.Click += (sender, e) =>
            {
                manualMusic = !music.IsPlaying;
                UpdatePracticeMusic();
                toggle.Content = music.IsPlaying ? "暂停" : "播放";
            };
            position.AddHandler(System.Windows.Controls.Primitives.Thumb.DragStartedEvent,
                new System.Windows.Controls.Primitives.DragStartedEventHandler((sender, e) => draggingMusicPosition = true));
            position.AddHandler(System.Windows.Controls.Primitives.Thumb.DragCompletedEvent,
                new System.Windows.Controls.Primitives.DragCompletedEventHandler((sender, e) =>
                {
                    draggingMusicPosition = false;
                    if (!e.Canceled)
                        music.Seek(position.Value);
                }));
            position.ValueChanged += (sender, e) =>
            {
                if (!syncingMusicPosition && !draggingMusicPosition)
                    music.Seek(position.Value);
                time.Text = FormatMusicTime(TimeSpan.FromSeconds(position.Value)) + " / " + FormatMusicTime(music.Duration);
            };
            musicTimer.Tick += (sender, e) =>
            {
                toggle.IsEnabled = musicTrack.IsEnabled;
                toggle.Content = music.IsPlaying ? "暂停" : "播放";
                var duration = music.Duration;
                position.IsEnabled = duration > TimeSpan.Zero;
                if (!draggingMusicPosition)
                {
                    syncingMusicPosition = true;
                    position.Maximum = Math.Max(1, duration.TotalSeconds);
                    position.Value = music.Position.TotalSeconds;
                    syncingMusicPosition = false;
                    time.Text = FormatMusicTime(music.Position) + " / " + FormatMusicTime(duration);
                }
            };
            mode.SelectedIndex = (int)settings.MusicMode;
            music.Mode = settings.MusicMode;
            volume.Value = settings.MusicVolume;
            music.SetVolume(settings.MusicVolume);
            volumeLabel.Text = settings.MusicVolume + "%";

            mode.SelectionChanged += (sender, e) =>
            {
                settings.MusicMode = (PlaybackMode)mode.SelectedIndex;
                music.Mode = settings.MusicMode;
                SaveSettings();
            };
            volume.ValueChanged += (sender, e) =>
            {
                settings.MusicVolume = (int)e.NewValue;
                music.SetVolume(settings.MusicVolume);
                volumeLabel.Text = settings.MusicVolume + "%";
                SaveSettings();
            };
            musicTrack.SelectionChanged += (sender, e) =>
            {
                if (updatingPlaylist || musicTrack.SelectedIndex < 0)
                    return;
                music.SelectTrack(musicTrack.SelectedIndex);
                RememberTrack();
            };
            nextTrack.Click += (sender, e) =>
            {
                music.NextTrack();
                RememberTrack();
                typed.Focus();
            };
            window.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.F8 && Keyboard.Modifiers == ModifierKeys.None && nextTrack.IsEnabled)
                {
                    music.NextTrack();
                    RememberTrack();
                    e.Handled = true;
                }
            };
        }

        private static string FormatMusicTime(TimeSpan time)
        {
            return string.Format("{0}:{1:00}", (int)time.TotalMinutes, time.Seconds);
        }

        private void UpdatePlaylist(FileInfo[] files, int selectedIndex)
        {
            updatingPlaylist = true;
            if (!ReferenceEquals(musicTrack.ItemsSource, files))
                musicTrack.ItemsSource = files;
            musicTrack.SelectedIndex = selectedIndex;
            musicTrack.IsEnabled = files.Length > 0;
            nextTrack.IsEnabled = files.Length > 1;
            updatingPlaylist = false;
        }

        private void RememberTrack()
        {
            var selected = musicTrack.SelectedValue as string;
            if (selected == null || string.Equals(selected, settings.MusicTrack, StringComparison.OrdinalIgnoreCase))
                return;
            settings.MusicTrack = selected;
            SaveSettings();
        }

        private void SaveSettings()
        {
            settingsNotice.Visibility = settings.Save() ? Visibility.Collapsed : Visibility.Visible;
        }
    }
}
