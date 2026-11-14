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
[assembly: System.Reflection.AssemblyVersion("2.3.1.0")]

namespace EnglishTypingPractice
{
    internal sealed class Program
    {
        [STAThread]
        private static void Main()
        {
            Forms.Application.EnableVisualStyles();
            var app = new Application();
            var settings = PracticeSettings.Load();
            var window = ThemePalette.LoadWindow(settings.Theme);
            using (var stream = typeof(Program).Assembly.GetManifestResourceStream("TypingIcon.ico"))
                window.Icon = BitmapFrame.Create(stream, BitmapCreateOptions.None, BitmapCacheOption.OnLoad);
            new Practice(window, true, settings);
            app.Run(window);
        }
    }

    internal sealed class Practice
    {
        private const int AutoPlayWpm = 100;
        private const int AutoPlayCharacterMilliseconds = 60000 / (AutoPlayWpm * 5);

        private readonly Brush correctBrush;
        private readonly Brush incorrectBrush;
        private readonly Brush incorrectBackground;
        private readonly Brush nextBackground;

        private readonly Window window;
        private readonly PracticeSettings settings;
        private readonly Random random = new Random();
        private readonly string[][] passages;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly Stopwatch autoPlayClock = new Stopwatch();
        private readonly DispatcherTimer autoPlayTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(50) };
        private readonly MusicPlayback music;
        private readonly DispatcherTimer musicTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly SnakePractice snake;
        private readonly TypingEffects effects;
        private readonly KeyboardMappingPractice keyboardMapping;
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
        private readonly ComboBox practiceFont;
        private readonly ComboBox codeExerciseKind;
        private readonly ComboBox reviewLevel;
        private readonly Button newPassage;
        private readonly Button retry;
        private readonly Button autoPlay;
        private readonly TextBlock reviewMeaning;
        private readonly TextBlock practiceResult;
        private readonly ComboBox rankingOrder;
        private readonly TextBlock rankingTitle;
        private readonly TextBlock rankingRows;
        private readonly FrameworkElement recordOverlay;
        private readonly TextBlock recordDetails;
        private readonly Button dismissRecord;
        private readonly HashSet<string>[] reviewBanks = new HashSet<string>[2];

        private int lastTopic = -1;
        private string original = "";
        private string target = "";
        private readonly List<KeyValuePair<int, Inline>> passageSpans = new List<KeyValuePair<int, Inline>>();
        private enum LetterState { Pending, Next, Correct, Incorrect }
        private int correctCount;
        private bool resetting;
        private bool updatingPlaylist;
        private bool composingChinese;
        private bool completedRound;
        private bool nextPracticeSpaceDown;
        private int roundGeneration;
        private string committedChinese = "";
        private TypingRound round;
        private string lastRoundResult;
        private string priorRoundResult;

        internal Practice(Window window) : this(window, false)
        {
        }

        internal Practice(Window window, bool showStartup)
            : this(window, showStartup, PracticeSettings.Load())
        {
        }

        internal Practice(Window window, bool showStartup, PracticeSettings settings)
        {
            this.window = window;
            this.settings = settings;
            correctBrush = (Brush)window.FindResource("PracticeCorrect");
            incorrectBrush = (Brush)window.FindResource("PracticeIncorrect");
            incorrectBackground = (Brush)window.FindResource("PracticeIncorrectBackground");
            nextBackground = (Brush)window.FindResource("PracticeNextBackground");
            using (var reader = new StreamReader(typeof(Practice).Assembly.GetManifestResourceStream("Passages.json")))
                passages = new JavaScriptSerializer().Deserialize<string[][]>(reader.ReadToEnd());

            practiceKind = (ComboBox)window.FindName("PracticeKind");
            practiceKind.Items.Add(new ComboBoxItem { Content = "键位映射", Tag = "mapping" });
            practiceFont = (ComboBox)window.FindName("PracticeFont");
            codeExerciseKind = (ComboBox)window.FindName("CodeExerciseKind");
            reviewLevel = (ComboBox)window.FindName("ReviewLevel");
            newPassage = (Button)window.FindName("NewPassage");
            retry = (Button)window.FindName("Retry");
            autoPlay = (Button)window.FindName("AutoPlay");
            reviewMeaning = (TextBlock)window.FindName("ReviewMeaning");
            practiceResult = (TextBlock)window.FindName("PracticeResult");
            rankingOrder = (ComboBox)window.FindName("PracticeRankingOrder");
            rankingTitle = (TextBlock)window.FindName("PracticeRankingTitle");
            rankingRows = (TextBlock)window.FindName("PracticeRankingRows");
            recordOverlay = (FrameworkElement)window.FindName("PracticeRecordOverlay");
            recordDetails = (TextBlock)window.FindName("PracticeRecordDetails");
            dismissRecord = (Button)window.FindName("DismissPracticeRecord");
            matchCase = (CheckBox)window.FindName("MatchCase");
            includePunctuation = (CheckBox)window.FindName("IncludePunctuation");
            playMusic = (CheckBox)window.FindName("PlayMusic");
            passage = (TextBlock)window.FindName("Passage");
            typed = (TextBox)window.FindName("Typed");
            keyboardMapping = new KeyboardMappingPractice(window, typed);
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
            effects = new TypingEffects(window, passage, (Canvas)window.FindName("WordEcho"), settings.Theme);
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
            ConfigureSettingsPage(practicePage);
            Action closeRecord = () =>
            {
                recordOverlay.Visibility = Visibility.Collapsed;
                practicePage.IsEnabled = true;
                typed.Focus();
            };
            dismissRecord.Click += (sender, e) => closeRecord();
            recordOverlay.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    closeRecord();
                }
            };
            rankingOrder.SelectionChanged += (sender, e) => RefreshRanking();
            window.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key != Key.Space)
                    return;
                if (nextPracticeSpaceDown)
                {
                    e.Handled = true;
                    return;
                }
                if (!completedRound || !practicePage.IsVisible || Keyboard.Modifiers != ModifierKeys.None ||
                    !(typed.IsKeyboardFocusWithin || recordOverlay.IsKeyboardFocusWithin))
                    return;
                e.Handled = true;
                if (e.IsRepeat)
                    return;
                nextPracticeSpaceDown = true;
                if (recordOverlay.IsVisible)
                    closeRecord();
                NextPassage();
            };
            window.PreviewKeyUp += (sender, e) =>
            {
                if (e.Key == Key.Space && nextPracticeSpaceDown)
                {
                    nextPracticeSpaceDown = false;
                    e.Handled = true;
                }
            };
            window.Deactivated += (sender, e) => nextPracticeSpaceDown = false;
            var snakePage = (FrameworkElement)window.FindName("SnakePage");
            var rulesPage = (FrameworkElement)window.FindName("SnakeRules");
            InteractionMotion.Attach(window, () => settings.SnakeLowEffects && (snakePage.IsVisible || rulesPage.IsVisible));
            var closeRules = (Button)window.FindName("CloseSnakeRules");
            Action returnToSnake = () =>
            {
                rulesPage.Visibility = Visibility.Collapsed;
                InteractionMotion.Reveal(snakePage, -18, settings.SnakeLowEffects);
                snake.Focus();
            };
            ((Button)window.FindName("OpenSnakeRules")).Click += (sender, e) =>
            {
                snakePage.Visibility = Visibility.Collapsed;
                InteractionMotion.Reveal(rulesPage, 14, settings.SnakeLowEffects);
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
                if (autoPlayClock.IsRunning)
                    ResetRound();
                resumePractice = clock.IsRunning;
                effects.Reset();
                clock.Stop();
                timer.Stop();
                music.SetPlaying(false);
                practicePage.Visibility = Visibility.Collapsed;
                InteractionMotion.Reveal(snakePage, 18, settings.SnakeLowEffects);
                snake.Focus();
            };
            ((Button)window.FindName("CloseSnake")).Click += (sender, e) =>
            {
                snake.Leave();
                snakePage.Visibility = Visibility.Collapsed;
                InteractionMotion.Reveal(practicePage, -18, settings.SnakeLowEffects);
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
            newPassage.Click += (sender, e) => NextPassage();
            autoPlay.Click += (sender, e) =>
            {
                bool stopping = autoPlayClock.IsRunning;
                ResetRound();
                if (!stopping && target.Length != 0)
                {
                    typed.IsReadOnly = true;
                    autoPlay.Content = "停止演示";
                    roundStatus.Text = "自动演示 · " + AutoPlayWpm + " WPM";
                    speed.Text = elapsed.Text = "—";
                    autoPlayClock.Start();
                    autoPlayTimer.Start();
                }
                typed.Focus();
            };
            autoPlayTimer.Tick += (sender, e) => AdvanceAutoPlay();
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
            practiceFont.SelectedValue = settings.PracticeFont ?? "";
            if (practiceFont.SelectedIndex < 0)
            {
                practiceFont.SelectedIndex = 0;
                settings.PracticeFont = null;
            }
            practiceFont.SelectionChanged += (sender, e) =>
            {
                var choice = practiceFont.SelectedItem as ComboBoxItem;
                settings.PracticeFont = choice == null ? null : (string)choice.Tag;
                ApplyPracticeFont();
                SaveSettings();
                typed.Focus();
            };
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
                autoPlayTimer.Stop();
                musicTimer.Stop();
                snake.Leave();
                if (musicTrack.SelectedValue is string)
                    RememberTrack();
                music.Dispose();
                effects.Dispose();
            };
            ThemeLayout.Attach(window, settings.Theme);
            ThemeWorldMotion.Attach(window, settings.Theme, () => settings.SnakeLowEffects);
            ChoosePassage();
            if (showStartup)
                new StartupExperience(window, settings, () => typed.Focus());
        }

        private void ConfigureSettingsPage(FrameworkElement practicePage)
        {
            var page = (FrameworkElement)window.FindName("SettingsPage");
            var close = (Button)window.FindName("CloseSettings");
            var restart = (Button)window.FindName("RestartWithTheme");
            var status = (TextBlock)window.FindName("ThemeStatus");
            var options = new Dictionary<UiTheme, RadioButton>
            {
                { UiTheme.Cyberpunk, (RadioButton)window.FindName("ThemeCyberpunk") },
                { UiTheme.Minimal, (RadioButton)window.FindName("ThemeMinimal") },
                { UiTheme.Steampunk, (RadioButton)window.FindName("ThemeSteampunk") },
                { UiTheme.Modern, (RadioButton)window.FindName("ThemeModern") },
                { UiTheme.Vaporwave, (RadioButton)window.FindName("ThemeVaporwave") },
                { UiTheme.Constructivist, (RadioButton)window.FindName("ThemeConstructivist") },
                { UiTheme.Persona5, (RadioButton)window.FindName("ThemePersona5") },
                { UiTheme.ChineseStyle, (RadioButton)window.FindName("ThemeChineseStyle") }
            };
            bool resume = false;
            Func<UiTheme> selected = () => options.First(item => item.Value.IsChecked == true).Key;
            foreach (var option in options.Values)
                option.Checked += (sender, e) =>
                {
                    restart.IsEnabled = selected() != settings.Theme;
                    status.Text = restart.IsEnabled ? "保存并重启后应用所选风格。" : "正在使用此风格。";
                };
            ((Button)window.FindName("OpenSettings")).Click += (sender, e) =>
            {
                if (autoPlayClock.IsRunning)
                    ResetRound();
                resume = clock.IsRunning;
                effects.Reset();
                clock.Stop();
                timer.Stop();
                music.SetPlaying(false);
                options[settings.Theme].IsChecked = true;
                restart.IsEnabled = false;
                status.Text = "正在使用此风格。";
                practicePage.Visibility = Visibility.Collapsed;
                InteractionMotion.Reveal(page, 14, false);
                close.Focus();
            };
            Action back = () =>
            {
                page.Visibility = Visibility.Collapsed;
                InteractionMotion.Reveal(practicePage, -14, false);
                if (resume)
                {
                    clock.Start();
                    timer.Start();
                }
                UpdatePracticeMusic();
                typed.Focus();
            };
            close.Click += (sender, e) => back();
            page.PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Escape)
                {
                    e.Handled = true;
                    back();
                }
            };
            restart.Click += (sender, e) =>
            {
                UiTheme oldTheme = settings.Theme;
                UiTheme newTheme = selected();
                if (newTheme == oldTheme)
                    return;
                settings.Theme = newTheme;
                if (!settings.Save())
                {
                    settings.Theme = oldTheme;
                    status.Text = "无法保存设置，请重试。";
                    return;
                }
                try
                {
                    Process.Start(new ProcessStartInfo(typeof(Program).Assembly.Location) { UseShellExecute = false }).Dispose();
                }
                catch (System.ComponentModel.Win32Exception)
                {
                    settings.Theme = oldTheme;
                    status.Text = settings.Save() ? "无法启动新窗口，请重试。" : "无法启动新窗口，请手动重开。";
                    return;
                }
                window.Close();
            };
        }

        private void ChoosePassage()
        {
            priorRoundResult = null;
            string[] choices;
            if (IsMapping)
            {
                var words = ReviewBank(0).Where(word => word.Length >= 3 && word.Length <= 8 &&
                    word.All(character => character >= 'a' && character <= 'z')).ToList();
                var selected = new List<string>();
                for (int i = 0; i < 20 && words.Count > 0; i++)
                {
                    int wordIndex = random.Next(words.Count);
                    selected.Add(words[wordIndex]);
                    words.RemoveAt(wordIndex);
                }
                original = string.Join(" ", selected);
                ResetRound();
                return;
            }
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

        private void NextPassage()
        {
            ChoosePassage();
            typed.Focus();
        }

        private void AdvanceAutoPlay()
        {
            int length = (int)Math.Min(target.Length, autoPlayClock.ElapsedMilliseconds / AutoPlayCharacterMilliseconds);
            int previousLength = correctCount;
            if (length <= previousLength)
                return;
            string sample = target.Substring(0, length);
            typed.Text = sample;
            typed.CaretIndex = typed.Text.Length;
            if (IsChinese)
                committedChinese = sample;
            RefreshProgress();
            bool complete = length == target.Length;
            HighlightProgress(complete);
            if (settings.TypingFeedback)
            {
                if (IsChinese)
                    effects.Play(null, null, complete, PassagePosition(length - 1));
                else
                {
                    int wordStart = -1, wordEnd = -1;
                    bool letterTyped = FindFeedbackWord(sample, previousLength, length, ref wordStart, ref wordEnd);
                    PlayFeedback(wordStart, wordEnd, letterTyped, complete, length - 1);
                }
            }
            if (complete)
            {
                autoPlayTimer.Stop();
                autoPlayClock.Stop();
                autoPlay.Content = "AutoPlay";
                completedRound = true;
                roundStatus.Text = "演示结束 · 空格换题";
            }
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
        private bool IsMapping { get { return practiceKind.SelectedItem is ComboBoxItem && (string)((ComboBoxItem)practiceKind.SelectedItem).Tag == "mapping"; } }
        private bool IsCode { get { return practiceKind.SelectedIndex >= 2 && practiceKind.SelectedIndex < passages.Length; } }
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

        private void ApplyPracticeFont()
        {
            string family = settings.PracticeFont;
            var font = string.IsNullOrEmpty(family)
                ? new FontFamily(IsChinese ? "Microsoft YaHei UI" : "Consolas")
                : new FontFamily(new Uri("pack://application:,,,/"),
                    "/" + typeof(Practice).Assembly.GetName().Name + ";component/fonts/#" + family + ", Microsoft YaHei UI");
            passage.FontFamily = typed.FontFamily = font;
        }

        private void ResetRound()
        {
            effects.Reset();
            progressGlint.BeginAnimation(UIElement.OpacityProperty, null);
            timer.Stop();
            clock.Reset();
            autoPlayTimer.Stop();
            autoPlayClock.Reset();
            autoPlay.Content = "AutoPlay";
            UpdatePracticeMusic();
            resetting = true;
            completedRound = false;
            roundGeneration++;
            composingChinese = false;
            committedChinese = "";
            typed.Clear();
            typed.IsReadOnly = original.Length == 0;
            retry.IsEnabled = original.Length != 0;
            autoPlay.IsEnabled = original.Length != 0;
            reviewMeaning.Visibility = Visibility.Collapsed;
            practiceResult.Visibility = Visibility.Collapsed;
            lastRoundResult = null;
            codeExerciseKind.Visibility = IsCode ? Visibility.Visible : Visibility.Collapsed;
            reviewLevel.Visibility = IsReview ? Visibility.Visible : Visibility.Collapsed;
            newPassage.Content = IsMapping ? "换组词" : IsReview ? "换个词" : IsCode ? "换一题" : "换一篇";
            retry.Content = IsMapping ? "重练本组" : IsReview ? "重练本词" : IsCode ? "重练本题" : "重练本篇";

            matchCase.IsEnabled = !IsCode && !IsChinese;
            includePunctuation.IsEnabled = !IsCode && !IsReview && !IsMapping;
            typed.AcceptsReturn = IsCode;
            InputMethod.SetIsInputMethodEnabled(typed, IsChinese);
            ApplyPracticeFont();
            passage.FontSize = typed.FontSize = IsCode ? 18 : 20;
            typed.Height = IsCode ? 155 : IsMapping ? 66 : 96;
            keyboardMapping.SetVisible(IsMapping);
            ((TextBlock)window.FindName("PracticeTitle")).Text = IsMapping ? "键位映射 · 四级词组" : IsReview ? "复习单词" : IsCode ? "练习代码" : "练习短文";
            ((TextBlock)window.FindName("PracticeHint")).Text = IsMapping
                ? "跟随高亮键位输入词组，空格分词。打错后用 Backspace 订正；F / J 为食指定位键。"
                : IsCode
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

            roundStatus.Text = original.Length != 0 ? "未开始" : IsReview ? "这个词库暂无待复习词" : "这个题型暂无练习";
            resetting = false;
            RefreshProgress();
            RefreshClock();
            RefreshRanking();
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
            bool complete = input.Length >= target.Length && (!(IsReview || IsMapping) || correctCount == target.Length);
            if (correctCount > previousCorrect)
                HighlightProgress(complete);
            if (settings.TypingFeedback)
            {
                if (IsChinese)
                {
                    if (complete || inputChanged && (change == null || change.Changes.Any(item => item.AddedLength > 0)))
                        effects.Play(null, null, complete, PassagePosition(Math.Min(input.Length, target.Length) - 1));
                }
                else
                    PlayFeedback(change, complete);
            }
            if (complete)
            {
                completedRound = true;
                clock.Stop();
                timer.Stop();
                typed.IsReadOnly = true;
                roundStatus.Text = "本轮完成 · 空格换题";
                UpdatePracticeMusic();
            }
            double completedSpeed = RefreshClock();
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
                bool best = settings.PracticeRecords.Add(RankingKey, completedSpeed, round.AccuracyRatio);
                SaveSettings();
                RefreshRanking();
                if (best)
                {
                    double score = settings.PracticeRecords.Top(RankingKey, PracticeRankingOrder.Composite)[0].Composite;
                    recordDetails.Text = RankingLabel + "  ·  综合分 " + score.ToString("0.00") +
                        "\n" + FormatRankSpeed(completedSpeed) + "　准确率 " + round.Accuracy;
                    ((FrameworkElement)window.FindName("PracticePage")).IsEnabled = false;
                    InteractionMotion.Reveal(recordOverlay, 0, false);
                    dismissRecord.Focus();
                }
            }
        }

        private string RankingKey
        {
            get
            {
                string key = (string)((ComboBoxItem)practiceKind.SelectedItem).Tag;
                return IsCode ? key + "/" + codeExerciseKind.SelectedIndex :
                    IsReview ? key + "/" + reviewLevel.SelectedIndex : key;
            }
        }

        private string RankingLabel
        {
            get
            {
                string label = (string)((ComboBoxItem)practiceKind.SelectedItem).Content;
                var submode = IsCode ? codeExerciseKind : IsReview ? reviewLevel : null;
                return submode == null ? label : label + " · " + ((ComboBoxItem)submode.SelectedItem).Content;
            }
        }

        private void RefreshRanking()
        {
            rankingTitle.Text = RankingLabel;
            var order = (PracticeRankingOrder)Math.Max(0, rankingOrder.SelectedIndex);
            var top = settings.PracticeRecords.Top(RankingKey, order);
            rankingRows.Text = top.Length == 0 ? "完成一轮后，这里会显示该类型的前三名。" :
                string.Join("\n", top.Select((entry, index) => (index + 1) + ".　" +
                    FormatRankSpeed(entry.Speed) + "　" + entry.Accuracy.ToString("P1") + "　" +
                    entry.Composite.ToString("0.00") + " 分"));
        }

        private string FormatRankSpeed(double value)
        {
            return value.ToString("0.0") + (IsChinese ? " 字/分" : " WPM");
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
                letterTyped |= FindFeedbackWord(input, startOffset, Math.Min(input.Length, endOffset), ref wordStart, ref wordEnd);
            }
            PlayFeedback(wordStart, wordEnd, letterTyped, complete, input.Length - 1);
        }

        private bool FindFeedbackWord(string input, int startOffset, int endOffset, ref int wordStart, ref int wordEnd)
        {
            bool letterTyped = false;
            var comparison = MatchExactCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
            for (int i = startOffset; i < endOffset; i++)
            {
                letterTyped |= IsCode || char.IsLetter(input[i]);
                if (i >= target.Length || !IsWordCharacter(i) || (i + 1 < target.Length && IsWordCharacter(i + 1)))
                    continue;
                int start = i;
                while (start > 0 && IsWordCharacter(start - 1))
                    start--;
                if (string.Compare(input, start, target, start, i - start + 1, comparison) == 0)
                {
                    wordStart = start;
                    wordEnd = i;
                }
            }
            return letterTyped;
        }

        private void PlayFeedback(int wordStart, int wordEnd, bool letterTyped, bool complete, int hit)
        {
            if (letterTyped || complete)
                effects.Play(PassagePosition(wordStart),
                    wordStart < 0 ? null : target.Substring(wordStart, wordEnd - wordStart + 1), complete,
                    PassagePosition(hit));
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
            bool exact = MatchExactCase;
            correctCount = 0;
            passageSpans.Clear();
            passage.Inlines.Clear();
            // One run per contiguous highlight state keeps WPF from shaping each character separately.
            int start = 0;
            LetterState previous = LetterState.Pending;
            for (int i = 0; i < target.Length; i++)
            {
                bool entered = i < input.Length;
                bool correct = entered && (exact
                    ? input[i] == target[i]
                    : char.ToUpperInvariant(input[i]) == char.ToUpperInvariant(target[i]));
                if (correct)
                    correctCount++;

                LetterState state = correct ? LetterState.Correct : entered ? LetterState.Incorrect
                    : i == input.Length ? LetterState.Next : LetterState.Pending;
                if (target[i] == '\n')
                {
                    AddPassageRun(start, i - start, previous);
                    AddPassageInline(i, new LineBreak());
                    start = i + 1;
                }
                else if (state != previous)
                {
                    AddPassageRun(start, i - start, previous);
                    start = i;
                }
                previous = state;
            }
            AddPassageRun(start, target.Length - start, previous);

            count.Text = input.Length + " / " + target.Length;
            progress.Value = input.Length;
            accuracy.Text = round.Accuracy;
            if (IsMapping)
                keyboardMapping.Update(target, input, MatchExactCase);
        }

        private void AddPassageRun(int start, int length, LetterState state)
        {
            if (length == 0)
                return;
            var run = new Run(target.Substring(start, length));
            if (state == LetterState.Correct)
                run.Foreground = correctBrush;
            else if (state == LetterState.Incorrect)
            {
                run.Foreground = incorrectBrush;
                run.Background = incorrectBackground;
                run.TextDecorations = TextDecorations.Underline;
            }
            else if (state == LetterState.Next)
                run.Background = nextBackground;
            AddPassageInline(start, run);
        }

        private void AddPassageInline(int start, Inline inline)
        {
            passage.Inlines.Add(inline);
            passageSpans.Add(new KeyValuePair<int, Inline>(start, inline));
        }

        private TextPointer PassagePosition(int index)
        {
            if (index < 0 || index >= target.Length)
                return null;
            for (int i = passageSpans.Count - 1; i >= 0; i--)
            {
                var span = passageSpans[i];
                if (index >= span.Key)
                    return span.Value.ContentStart.GetPositionAtOffset(index - span.Key, LogicalDirection.Forward);
            }
            return null;
        }

        private string ReadInput()
        {
            return (IsChinese ? committedChinese : typed.Text).Replace("\r\n", "\n");
        }

        private double RefreshClock()
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
            double measuredSpeed = duration.TotalSeconds >= 1 || (typed.IsReadOnly && target.Length != 0)
                ? units * (IsChinese ? 60.0 : 12.0) / Math.Max(1, duration.TotalSeconds) : 0;
            speed.Text = Math.Round(measuredSpeed).ToString(CultureInfo.CurrentCulture);
            return measuredSpeed;
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
