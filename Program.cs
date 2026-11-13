using System;
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
using System.Windows.Media.Imaging;
using System.Windows.Input;
using System.Windows.Threading;
using Forms = System.Windows.Forms;

[assembly: TargetFramework(".NETFramework,Version=v4.8")]
[assembly: System.Reflection.AssemblyTitle("鼎山打字通")]
[assembly: System.Reflection.AssemblyProduct("鼎山打字通")]
[assembly: System.Reflection.AssemblyDescription("英文打字练习与局域网贪吃蛇")]
[assembly: System.Reflection.AssemblyVersion("1.8.0.0")]

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
            new Practice(window);
            app.Run(window);
        }
    }

    internal sealed class Practice
    {
        private static readonly Brush Correct = new SolidColorBrush(Color.FromRgb(38, 101, 84));
        private static readonly Brush Incorrect = new SolidColorBrush(Color.FromRgb(151, 41, 66));
        private static readonly Brush IncorrectBackground = new SolidColorBrush(Color.FromRgb(251, 224, 231));
        private static readonly Brush NextBackground = new SolidColorBrush(Color.FromRgb(220, 231, 255));

        private readonly Window window;
        private readonly PracticeSettings settings = PracticeSettings.Load();
        private readonly Random random = new Random();
        private readonly string[][][] topics;
        private readonly Stopwatch clock = new Stopwatch();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(200) };
        private readonly MusicPlayback music;
        private readonly SnakePractice snake;
        private readonly TypingEffects effects;
        private bool resumePractice;

        private readonly CheckBox matchCase;
        private readonly CheckBox includePunctuation;
        private readonly CheckBox playMusic;
        private readonly TextBlock passage;
        private readonly TextBox typed;
        private readonly ProgressBar progress;
        private readonly TextBlock count;
        private readonly TextBlock roundStatus;
        private readonly TextBlock speed;
        private readonly TextBlock accuracy;
        private readonly TextBlock elapsed;
        private readonly TextBlock musicStatus;
        private readonly ComboBox musicTrack;
        private readonly Button nextTrack;
        private readonly TextBlock settingsNotice;

        private int lastTopic = -1;
        private string original = "";
        private string target = "";
        private Run[] letters = new Run[0];
        private int correctCount;
        private bool resetting;
        private bool updatingPlaylist;

        internal Practice(Window window)
        {
            this.window = window;
            using (var reader = new StreamReader(typeof(Practice).Assembly.GetManifestResourceStream("Passages.json")))
                topics = new JavaScriptSerializer().Deserialize<string[][][]>(reader.ReadToEnd());

            matchCase = (CheckBox)window.FindName("MatchCase");
            includePunctuation = (CheckBox)window.FindName("IncludePunctuation");
            playMusic = (CheckBox)window.FindName("PlayMusic");
            passage = (TextBlock)window.FindName("Passage");
            typed = (TextBox)window.FindName("Typed");
            progress = (ProgressBar)window.FindName("Progress");
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
            snake = new SnakePractice(window, running => music.SetPlaying(settings.PlayMusic && running));
            var practicePage = (FrameworkElement)window.FindName("PracticePage");
            var snakePage = (FrameworkElement)window.FindName("SnakePage");
            ((Button)window.FindName("OpenSnake")).Click += (sender, e) =>
            {
                resumePractice = clock.IsRunning;
                effects.Reset();
                clock.Stop();
                timer.Stop();
                music.SetPlaying(false);
                practicePage.Visibility = Visibility.Collapsed;
                snakePage.Visibility = Visibility.Visible;
                snake.Focus();
            };
            ((Button)window.FindName("CloseSnake")).Click += (sender, e) =>
            {
                snake.Leave();
                snakePage.Visibility = Visibility.Collapsed;
                practicePage.Visibility = Visibility.Visible;
                if (resumePractice)
                {
                    clock.Start();
                    timer.Start();
                }
                music.SetPlaying(settings.PlayMusic && resumePractice);
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
                SaveSettings();
                music.SetPlaying(settings.PlayMusic && clock.IsRunning);
            };
            ((Button)window.FindName("NewPassage")).Click += (sender, e) =>
            {
                ChoosePassage();
                typed.Focus();
            };
            ((Button)window.FindName("Retry")).Click += (sender, e) =>
            {
                ResetRound();
                typed.Focus();
            };
            ((Button)window.FindName("ChooseFolder")).Click += ChooseMusicFolder;
            typed.TextChanged += OnTextChanged;
            DataObject.AddPastingHandler(typed, (sender, e) => e.CancelCommand());
            timer.Tick += (sender, e) => RefreshClock();
            window.Loaded += async (sender, e) =>
            {
                typed.Focus();
                if (!string.IsNullOrEmpty(settings.Folder))
                    await LoadMusicFolder();
            };
            window.Closed += (sender, e) =>
            {
                timer.Stop();
                snake.Leave();
                if (musicTrack.SelectedValue is string)
                    RememberTrack();
                music.Dispose();
                effects.Dispose();
            };
            ChoosePassage();
        }

        private void ChoosePassage()
        {
            int index = random.Next(lastTopic < 0 ? topics.Length : topics.Length - 1);
            if (lastTopic >= 0 && index >= lastTopic)
                index++;
            lastTopic = index;
            original = string.Join(" ", topics[index].Select(group => group[random.Next(group.Length)]));
            ResetRound();
        }

        private void ChangePracticeOptions(object sender, RoutedEventArgs e)
        {
            settings.MatchCase = matchCase.IsChecked == true;
            settings.IncludePunctuation = includePunctuation.IsChecked == true;
            SaveSettings();
            ResetRound();
        }

        private void ResetRound()
        {
            effects.Reset();
            timer.Stop();
            clock.Reset();
            music.SetPlaying(false);
            resetting = true;
            typed.Clear();
            typed.IsReadOnly = false;

            target = settings.IncludePunctuation ? original : new string(original.Where(c => !char.IsPunctuation(c)).ToArray());
            typed.MaxLength = target.Length;
            progress.Maximum = target.Length;
            letters = target.Select(c => new Run(c.ToString())).ToArray();
            passage.Inlines.Clear();
            passage.Inlines.AddRange(letters);

            roundStatus.Text = "未开始";
            resetting = false;
            RefreshProgress();
            RefreshClock();
        }

        private void OnTextChanged(object sender, TextChangedEventArgs e)
        {
            if (resetting || typed.IsReadOnly || (typed.Text.Length == 0 && !clock.IsRunning))
                return;

            if (!clock.IsRunning)
            {
                clock.Start();
                timer.Start();
                roundStatus.Text = "练习中";
                music.SetPlaying(settings.PlayMusic);
            }

            RefreshProgress();
            bool complete = typed.Text.Length >= target.Length;
            if (settings.TypingFeedback)
                PlayFeedback(e, complete);
            if (complete)
            {
                clock.Stop();
                timer.Stop();
                typed.IsReadOnly = true;
                roundStatus.Text = "本轮完成";
                music.SetPlaying(false);
            }
            RefreshClock();
        }

        private void PlayFeedback(TextChangedEventArgs e, bool complete)
        {
            string input = typed.Text;
            int wordStart = -1;
            int wordEnd = -1;
            bool letterTyped = false;
            foreach (var change in e.Changes)
                for (int i = change.Offset; i < Math.Min(input.Length, change.Offset + change.AddedLength); i++)
                {
                    letterTyped |= char.IsLetter(input[i]);
                    if (!char.IsLetter(target[i]) || (i + 1 < target.Length && IsWordCharacter(i + 1)))
                        continue;
                    int start = i;
                    while (start > 0 && IsWordCharacter(start - 1))
                        start--;
                    int length = i - start + 1;
                    var comparison = settings.MatchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
                    if (string.Compare(input, start, target, start, length, comparison) == 0)
                    {
                        wordStart = start;
                        wordEnd = i;
                    }
                }
            if (letterTyped || complete)
                effects.Play(wordStart < 0 ? null : letters[wordStart],
                    wordStart < 0 ? null : target.Substring(wordStart, wordEnd - wordStart + 1), complete);
        }

        private bool IsWordCharacter(int index)
        {
            char character = target[index];
            return char.IsLetter(character) || ((character == '\'' || character == '’' || character == '-') &&
                index > 0 && index + 1 < target.Length &&
                char.IsLetter(target[index - 1]) && char.IsLetter(target[index + 1]));
        }

        private void RefreshProgress()
        {
            string input = typed.Text;
            correctCount = 0;
            for (int i = 0; i < letters.Length; i++)
            {
                var letter = letters[i];
                bool entered = i < input.Length;
                bool correct = entered && (settings.MatchCase
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
            accuracy.Text = input.Length == 0
                ? "—"
                : (100.0 * correctCount / input.Length).ToString("0.#", CultureInfo.CurrentCulture) + "%";
        }

        private void RefreshClock()
        {
            TimeSpan duration = clock.Elapsed;
            elapsed.Text = string.Format("{0}:{1:00}", (int)duration.TotalMinutes, duration.Seconds);
            speed.Text = duration.TotalSeconds >= 1
                ? Math.Round(correctCount * 12.0 / duration.TotalSeconds).ToString(CultureInfo.CurrentCulture)
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
