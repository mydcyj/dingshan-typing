using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal sealed class SnakePractice
    {
        private static readonly SnakeDirection[] Directions =
            (SnakeDirection[])Enum.GetValues(typeof(SnakeDirection));
        private SnakeGame game = new SnakeGame();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        private readonly Stopwatch clock = new Stopwatch();
        private readonly TextBox command;
        private readonly ComboBox rolePicker;
        private readonly ComboBox modePicker;
        private readonly Slider soloMapSize;
        private readonly FrameworkElement soloMapControls;
        private readonly FrameworkElement overview;
        private readonly TextBlock overviewSize;
        private readonly TextBlock cameraLookHint;
        private readonly TextBlock cameraMode;
        private readonly System.Collections.Generic.HashSet<Key> lookKeys = new System.Collections.Generic.HashSet<Key>();
        private readonly FrameworkElement setup;
        private readonly FrameworkElement playArea;
        private readonly TextBlock roleGuide;
        private readonly FrameworkElement chargePanel;
        private readonly TextBlock chargeWord;
        private readonly TextBox chargeInput;
        private readonly TextBlock status;
        private readonly TextBlock score;
        private readonly TextBlock scoreLabel;
        private readonly FrameworkElement wordFlash;
        private readonly TextBlock flashWord;
        private readonly TextBlock flashBlue;
        private readonly TextBlock flashRose;
        private readonly TextBlock flashMeaning;
        private readonly TextBlock boardSize;
        private readonly TextBlock fruitLegend;
        private readonly TextBlock scoreGuide;
        private readonly TextBlock[] directionWords;
        private readonly SnakeBoard board;
        private readonly Action<bool> gameRunningChanged;
        private readonly Action<double, double> shakeWindow;
        private readonly SnakeWordVoice voice;
        private readonly CheckBox voiceEnabled;
        private readonly PracticeSettings settings;
        private readonly LanLobby lobby;
        private SnakeMatchState online;
        private SnakeMatch soloMatch;
        private int ownSlot;
        private bool musicRunning;
        private bool updatingRole;
        private bool updatingMode;
        private int sprintRound;

        internal SnakePractice(Window window, Action<bool> gameRunningChanged, PracticeSettings settings, Action saveSettings,
            Action<double, double> shakeWindow)
        {
            this.gameRunningChanged = gameRunningChanged;
            this.shakeWindow = shakeWindow;
            this.settings = settings;
            command = (TextBox)window.FindName("SnakeCommand");
            rolePicker = (ComboBox)window.FindName("SnakeRole");
            modePicker = (ComboBox)window.FindName("SnakeMode");
            soloMapSize = (Slider)window.FindName("SoloMapSize");
            soloMapControls = (FrameworkElement)window.FindName("SoloMapControls");
            soloMapSize.Minimum = SnakeGame.MinSoloSize;
            soloMapSize.Maximum = SnakeGame.MaxSoloSize;
            roleGuide = (TextBlock)window.FindName("SnakeRoleGuide");
            chargePanel = (FrameworkElement)window.FindName("SnakeChargePanel");
            chargeWord = (TextBlock)window.FindName("SnakeChargeWord");
            chargeInput = (TextBox)window.FindName("SnakeChargeInput");
            status = (TextBlock)window.FindName("SnakeStatus");
            voiceEnabled = (CheckBox)window.FindName("SnakeVoiceEnabled");
            voiceEnabled.IsChecked = settings.SnakeVoiceEnabled;
            voice = new SnakeWordVoice(() =>
            {
                voiceEnabled.IsChecked = false;
                voiceEnabled.IsEnabled = false;
                voiceEnabled.Content = "英文女声不可用";
                voiceEnabled.ToolTip = "请检查 Windows 的默认音频输出和英文女声，再重启游戏。";
                ToolTipService.SetShowOnDisabled(voiceEnabled, true);
                settings.SnakeVoiceEnabled = false;
                saveSettings();
            });
            voiceEnabled.Click += (sender, e) =>
            {
                settings.SnakeVoiceEnabled = voiceEnabled.IsChecked == true && voice.Enable();
                if (!settings.SnakeVoiceEnabled)
                    voice.Stop();
                saveSettings();
            };
            window.Closed += (sender, e) => voice.Dispose();
            score = (TextBlock)window.FindName("SnakeScore");
            scoreLabel = (TextBlock)window.FindName("SnakeScoreLabel");
            wordFlash = (FrameworkElement)window.FindName("SnakeWordFlash");
            flashWord = (TextBlock)window.FindName("SnakeFlashWord");
            flashBlue = (TextBlock)window.FindName("SnakeFlashBlue");
            flashRose = (TextBlock)window.FindName("SnakeFlashRose");
            flashMeaning = (TextBlock)window.FindName("SnakeFlashMeaning");
            directionWords = new[] { "SnakeNorth", "SnakeEast", "SnakeSouth", "SnakeWest" }
                .Select(name => (TextBlock)window.FindName(name)).ToArray();
            board = new SnakeBoard();
            ((Border)window.FindName("SnakeBoardHost")).Child = board;
            overview = (FrameworkElement)window.FindName("SnakeOverview");
            overviewSize = (TextBlock)window.FindName("SnakeOverviewSize");
            cameraLookHint = (TextBlock)window.FindName("SnakeCameraLookHint");
            cameraMode = (TextBlock)window.FindName("SnakeCameraMode");
            playArea = (FrameworkElement)window.FindName("SnakePlayArea");
            setup = (FrameworkElement)window.FindName("SnakeSetup");
            var boardSlot = (FrameworkElement)window.FindName("SnakeBoardSlot");
            var square = (FrameworkElement)window.FindName("SnakeSquare");
            var minimapHost = (Border)window.FindName("SnakeMinimapHost");
            minimapHost.Child = board.Minimap;
            playArea.SizeChanged += (sender, e) =>
            {
                double side = Math.Floor(Math.Min(132, Math.Min(e.NewSize.Width * .36, e.NewSize.Height * .29)));
                minimapHost.Width = minimapHost.Height = side;
                UpdateSetup();
            };
            boardSlot.SizeChanged += (sender, e) =>
            {
                double side = Math.Floor(Math.Min(e.NewSize.Width, e.NewSize.Height));
                square.Width = square.Height = side;
            };

            boardSize = (TextBlock)window.FindName("SnakeBoardSize");
            fruitLegend = (TextBlock)window.FindName("SnakeFruitLegend");
            scoreGuide = (TextBlock)window.FindName("SnakeScoreGuide");
            lobby = new LanLobby(window, EnterRoom, ApplyRoomState, LeaveRoom, () => SelectedRole, () => SelectedMode,
                settings, saveSettings);
            rolePicker.SelectionChanged += (sender, e) =>
            {
                if (updatingRole)
                    return;
                if (online == null || soloMatch != null)
                {
                    if (soloMatch != null)
                        soloMatch.SetRole(0, SelectedRole);
                    if (online == null)
                        Render();
                    else
                        RefreshSoloMatch();
                }
                else
                    lobby.SetRole(SelectedRole);
            };
            modePicker.SelectionChanged += (sender, e) =>
            {
                if (updatingMode)
                    return;
                if (online == null || soloMatch != null)
                {
                    soloMatch = null;
                    online = null;
                    game = new SnakeGame();
                    status.Text = "输入 start 开始单人游戏。";
                    Render();
                }
                else
                    lobby.SetMode(SelectedMode);
            };
            var page = (FrameworkElement)window.FindName("SnakePage");
            var mapValue = (TextBlock)window.FindName("SoloMapValue");
            soloMapSize.ValueChanged += (sender, e) =>
            {
                mapValue.Text = SelectedMapSize.ToString(CultureInfo.CurrentCulture);
                if (soloMatch != null)
                {
                    timer.Stop();
                    clock.Stop();
                    soloMatch = null;
                    online = null;
                    command.IsReadOnly = false;
                }
                game = new SnakeGame();
                status.Text = "输入 start 开始单人游戏。";
                Render();
            };
            page.PreviewKeyDown += (sender, e) =>
            {
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (key == Key.LeftAlt || key == Key.RightAlt)
                {
                    if (!e.IsRepeat)
                    {
                        ClearLook();
                        board.ToggleCamera();
                        UpdateOverview(board.MapSize);
                    }
                    e.Handled = true;
                    return;
                }
                if (board.CameraLocked && (command.IsKeyboardFocusWithin || chargeInput.IsKeyboardFocusWithin ||
                    (online == null ? game.IsRunning : online.Phase == MatchPhase.Playing)) &&
                    (key == Key.Left || key == Key.Right || key == Key.Up || key == Key.Down))
                {
                    if (!e.IsRepeat && lookKeys.Add(key))
                        UpdateLook();
                    e.Handled = true;
                    return;
                }
                if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) && !e.IsRepeat && online != null &&
                    online.Mode == SnakeMode.Teams && online.Phase == MatchPhase.Playing)
                {
                    lobby.DropBall(online.Round);
                    sprintRound = online.Round;
                    lobby.SetSprint(sprintRound, true);
                    e.Handled = true;
                }
                else if (e.Key == Key.Space && (command.IsKeyboardFocusWithin || chargeInput.IsKeyboardFocusWithin))
                {
                    (chargeInput.IsKeyboardFocusWithin ? chargeInput : command).Clear();
                    e.Handled = true;
                }
                else if (e.Key == Key.Tab && chargePanel.Visibility == Visibility.Visible &&
                    (command.IsKeyboardFocused || chargeInput.IsKeyboardFocused))
                {
                    if (chargeInput.IsKeyboardFocused)
                        command.Focus();
                    else
                        chargeInput.Focus();
                    e.Handled = true;
                }
            };
            window.PreviewKeyUp += (sender, e) =>
            {
                Key key = e.Key == Key.System ? e.SystemKey : e.Key;
                if (lookKeys.Remove(key))
                {
                    UpdateLook();
                    e.Handled = true;
                }
                if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) &&
                    !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                    StopSprint();
            };
            window.Deactivated += (sender, e) =>
            {
                StopSprint();
                ClearLook();
            };
            page.IsVisibleChanged += (sender, e) =>
            {
                if (!page.IsVisible)
                {
                    StopSprint();
                    ClearLook();
                    voice.Stop();
                }
            };
            timer.Tick += Tick;
            command.TextChanged += OnCommandChanged;
            chargeInput.TextChanged += OnChargeChanged;
            command.TextChanged += (sender, e) => PublishTyping();
            chargeInput.TextChanged += (sender, e) => PublishTyping();
            command.GotKeyboardFocus += (sender, e) => PublishTyping();
            chargeInput.GotKeyboardFocus += (sender, e) => PublishTyping();
            DataObject.AddPastingHandler(command, (sender, e) => e.CancelCommand());
            DataObject.AddPastingHandler(chargeInput, (sender, e) => e.CancelCommand());
            Render();
        }

        private SnakeRole SelectedRole { get { return (SnakeRole)rolePicker.SelectedIndex; } }
        private SnakeMode SelectedMode { get { return (SnakeMode)modePicker.SelectedIndex; } }
        private int SelectedMapSize { get { return (int)soloMapSize.Value; } }

        internal void Focus()
        {
            command.Focus();
        }

        private void PublishTyping(bool publish = true)
        {
            var own = online == null ? null : online.Players.First(player => player.Slot == ownSlot);
            bool active = own == null ? game.IsRunning :
                online.Phase == MatchPhase.Playing && own.Result == SnakeResult.Running;
            bool canType = active && (own == null || own.StunRemaining == 0);
            bool skill = canType && chargeInput.IsKeyboardFocusWithin && chargePanel.Visibility == Visibility.Visible;
            string text = canType ? (skill ? chargeInput.Text : command.Text) : "";
            if (!SnakeTypingPreview.IsValidText(text, skill))
                text = new string(text.Where(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z')
                    .Take(skill ? chargeInput.MaxLength : command.MaxLength).ToArray());
            board.SetLocalTyping(active ? text : null, skill);
            if (publish && canType && own != null && soloMatch == null)
            {
                lobby.SetPreview(new SnakeTypingPreview
                {
                    Round = online.Round,
                    Respawns = own.Respawns,
                    Skill = skill,
                    Text = text
                });
            }
        }

        internal void Leave()
        {
            StopSprint();
            wordFlash.BeginAnimation(UIElement.OpacityProperty, null);
            soloMatch = null;
            lobby.Close();
            online = null;
            command.IsReadOnly = false;
            timer.Stop();
            clock.Stop();
            game.Leave();
            command.Clear();
            chargeInput.Clear();
            if (game.Result == SnakeResult.Left)
                status.Text = "已结束本局。输入 start 再来一局。";
            Render();
            UpdateMusic(false);
        }

        private void OnCommandChanged(object sender, TextChangedEventArgs e)
        {
            if (command.Text.Length == 0)
                return;

            if (online != null)
            {
                HandleRoomCommand();
                return;
            }

            if (!game.IsRunning)
            {
                if (command.Text.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    if (SelectedMode == SnakeMode.Teams)
                    {
                        status.Text = "团战需要先创建或加入局域网房间。";
                        command.Clear();
                        return;
                    }
                    lobby.HidePanel();
                    Start();
                }
                return;
            }

            SubmitWord(game.Words, game.Role, game.Direction, false, word =>
            {
                var before = game.Direction;
                if (game.TryWord(word, game.WordGroup, clock.ElapsedMilliseconds))
                {
                    FlashWord(game.LastWord);
                    PulseDirection(game.Direction);
                    if (before != game.Direction)
                        TurnFeedback(before);
                    status.Text = "";
                    Render();
                }
            });
        }

        private void Start()
        {
            ClearLook();
            board.ResetCamera();
            wordFlash.BeginAnimation(UIElement.OpacityProperty, null);
            online = null;
            soloMatch = null;
            command.Clear();
            chargeInput.Clear();
            if (SelectedMode == SnakeMode.Territory || SelectedRole == SnakeRole.Hunter || SelectedRole == SnakeRole.Bard)
            {
                soloMatch = new SnakeMatch("你", SelectedRole, solo: true, soloClassicSize: SelectedMapSize);
                soloMatch.SetMode(SelectedMode);
                soloMatch.Start();
                clock.Restart();
                RefreshSoloMatch();
                timer.Start();
                return;
            }
            game.Start(SelectedRole, SelectedMode, SelectedMode == SnakeMode.Classic ? SelectedMapSize : SnakeGame.BaseSize);
            clock.Restart();
            status.Text = "";
            Render();
            timer.Start();
            UpdateMusic(true);
        }

        private void Tick(object sender, EventArgs e)
        {
            long now = clock.ElapsedMilliseconds;
            if (soloMatch != null)
            {
                soloMatch.Step(now);
                RefreshSoloMatch();
                if (soloMatch.Phase == MatchPhase.Finished && !soloMatch.HasBurst)
                {
                    timer.Stop();
                    clock.Stop();
                }
                return;
            }
            bool changed = game.UpdateSoloClock(now);
            if (!changed && !game.MoveDue(now))
                return;

            if (game.MoveDue(now))
            {
                game.Step(now);
                if (game.MoveDue(now))
                    game.ScheduleNext(now);
            }
            if (!game.IsRunning)
            {
                timer.Stop();
                clock.Stop();
                command.Clear();
                chargeInput.Clear();
                status.Text = (game.Result == SnakeResult.Won ? "棋盘已填满，通关！"
                    : game.Result == SnakeResult.Body ? "撞到蛇身。"
                    : game.Result == SnakeResult.Obstacle ? "撞到障碍物。" : "撞到墙了。")
                    + "得分 " + game.Score + "。输入 start 再来一局。";
                Render();
                UpdateMusic(false);
                return;
            }

            Render();
        }

        private void Render()
        {
            bool territory = SelectedMode == SnakeMode.Territory;
            int size = SelectedMode == SnakeMode.Teams ? SnakeTeams.Size :
                SelectedMode == SnakeMode.Classic && game.Result == SnakeResult.Ready ? SelectedMapSize : game.Size;
            int side = size == game.Size ? game.Bounds.End - game.Bounds.Start : size;
            boardSize.Text = SelectedMode == SnakeMode.Teams ? "60 × 60 格" : side + " × " + side + " 格";
            var role = game.IsRunning ? game.Role : SelectedRole;
            ShowWords(game.IsRunning, game.Direction, game.Words, role);
            ShowRole(role, SelectedMode, game.IsRunning, game.IsCharging(clock.ElapsedMilliseconds),
                0, game.ChargeWord);
            fruitLegend.Visibility = Visibility.Collapsed;
            scoreLabel.Text = territory ? "占地格数" : "分数";
            scoreGuide.Text = SelectedMode == SnakeMode.Teams ? "八分钟 · 回己方区域按 Shift 放球"
                : territory ? "五分钟 · 闭合轨迹占地"
                : SelectedMode == SnakeMode.Obstacles ? "避开障碍 · 吃果 +10 · " + WordScoreGuide(role, SelectedMode)
                : (game.IsRunning ? ShrinkGuide(side, (int)(60000 - clock.ElapsedMilliseconds % 60000)) : "每分钟缩小 5 格") +
                    " · 吃果 +10 · " + WordScoreGuide(role, SelectedMode);
            board.SetBoard(size, territory || game.Fruit < 0 ? new int[0] : new[] { game.Fruit }, -1, 0, new[]
            {
                new SnakePlayerState { Slot = 0, Result = game.Result, Body = territory ? new int[0] : game.Body.ToArray(),
                    Charging = game.IsRunning && game.IsCharging(clock.ElapsedMilliseconds) }
            }, SelectedMode == SnakeMode.Obstacles ? game.Obstacles.ToArray() : null,
                safeStart: game.Bounds.Start, safeEnd: size == game.Size ? game.Bounds.End : size);
            UpdateOverview(size);
            PublishTyping(false);
            score.Text = (territory ? 0 : game.Score).ToString(CultureInfo.CurrentCulture);
        }

        private void ShowWords(bool running, SnakeDirection movement, string[] words, SnakeRole role)
        {
            foreach (var direction in Directions)
            {
                var label = directionWords[(int)direction];
                bool current = running && direction == movement;
                bool opposite = running && (int)direction == ((int)movement + 2) % 4;
                int variants = SnakeWords.Variants(role);
                label.Text = !running || (current && variants == 1) ? "—" :
                    string.Join("\n", Enumerable.Range(0, variants).Select(variant =>
                    {
                        if (current && variant != 2)
                            return variant == 0 ? "—" : null;
                        string word = words[(int)direction * variants + variant];
                        return word + " " + SnakeWords.Meaning(word) + (opposite ? " ×" : "");
                    }).Where(line => line != null));
                label.Foreground = opposite || !running ? SnakeBoard.Quiet : SnakeBoard.Label;
                label.ToolTip = current ? "当前方向" : opposite ? "禁止掉头" : null;
                var card = (Border)((StackPanel)label.Parent).Parent;
                if (current)
                    card.Background = SnakeBoard.Current;
                else
                    card.ClearValue(Border.BackgroundProperty);
            }
        }

        private static string WordScoreGuide(SnakeRole role, SnakeMode mode)
        {
            return role == SnakeRole.Timewalker && SnakeGame.UsesTimewalkerBalance(mode)
                ? "转向 +" + SnakeGame.TimewalkerTurnPoints + " · 直行不计分"
                : "有效输入按字母计分";
        }

        private void ShowRole(SnakeRole role, SnakeMode mode, bool running, bool charging, int stun, string special)
        {
            rolePicker.IsEnabled = !running && (online == null || online.Phase != MatchPhase.Playing);
            modePicker.IsEnabled = rolePicker.IsEnabled && (online == null || ownSlot == 0);
            soloMapControls.Visibility = SelectedMode == SnakeMode.Classic && (online == null || soloMatch != null)
                ? Visibility.Visible : Visibility.Collapsed;
            soloMapSize.IsEnabled = rolePicker.IsEnabled;
            UpdateSetup();
            string guide;
            switch (role)
            {
                case SnakeRole.Timewalker:
                    guide = "6 / 8 / 10 字母 · 额外走 " + (SnakeGame.UsesTimewalkerBalance(mode) ? "0 / 1 / 2" : "0 / 2 / 4") + " 格";
                    break;
                case SnakeRole.Timid: guide = "85% 移速 · 5–6 字母"; break;
                case SnakeRole.Fearless:
                    guide = (SnakeGame.FearlessBaseSpeed(mode) * 100).ToString("0") + "% 基础移速 · Tab 切换冲锋输入框";
                    break;
                case SnakeRole.Hunter: guide = "Tab 切换射击输入框 · 15–20 字母 · 子弹为自身 3 倍移速"; break;
                case SnakeRole.Bard: guide = "Tab 切换吟唱输入框 · 15–20 字母 · 瞬间判定周围 5 × 5 格"; break;
                default: guide = ""; break;
            }
            if (stun > 0)
                guide += " · 僵直 " + (stun / 1000.0).ToString("0.0") + " 秒";
            else if (charging)
                guide += " · 冲锋中";
            roleGuide.Text = guide;
            chargePanel.Visibility = running && SnakeWords.HasSkill(role) && special != null
                ? Visibility.Visible : Visibility.Collapsed;
            chargeWord.Text = special == null ? "" : special + " " + SnakeWords.Meaning(special);
            chargeInput.IsReadOnly = charging || stun > 0;
        }

        private void OnChargeChanged(object sender, TextChangedEventArgs e)
        {
            if (chargeInput.Text.Length == 0)
                return;
            string word = chargeInput.Text;
            if (online != null)
            {
                var own = online.Players.First(player => player.Slot == ownSlot);
                if (online.Phase != MatchPhase.Playing || own.Result != SnakeResult.Running ||
                    own.Charging || own.StunRemaining > 0 ||
                    !word.Equals(online.ChargeWord, StringComparison.OrdinalIgnoreCase))
                    return;
                chargeInput.Clear();
                if (soloMatch == null)
                    lobby.Turn(word, online.ChargeGroup, online.Round, charge: true);
                else if (soloMatch.TryCharge(0, word, online.ChargeGroup, online.Round, clock.ElapsedMilliseconds))
                    RefreshSoloMatch();
            }
            else if (game.TryCharge(word, game.ChargeGroup, clock.ElapsedMilliseconds))
            {
                chargeInput.Clear();
                FlashWord(game.LastWord);
                Render();
            }
        }

        private void SubmitWord(string[] words, SnakeRole role, SnakeDirection direction, bool stunned, Action<string> submit)
        {
            string word = command.Text;
            int index = Array.FindIndex(words, candidate => word.Equals(candidate, StringComparison.OrdinalIgnoreCase));
            if (index < 0)
                return;
            command.Clear();
            int variants = SnakeWords.Variants(role);
            int target = index / variants;
            if (stunned)
                status.Text = "僵直中，暂时无法操作。";
            else if (target == ((int)direction + 2) % 4)
                status.Text = "禁止掉头。";
            else if (target != (int)direction || index % variants == 2)
                submit(word);
        }

        private void EnterRoom()
        {
            soloMatch = null;
            online = null;
            timer.Stop();
            clock.Stop();
            game.Leave();
            command.Clear();
            chargeInput.Clear();
            UpdateMusic(false);
        }

        private void LeaveRoom()
        {
            StopSprint();
            wordFlash.BeginAnimation(UIElement.OpacityProperty, null);
            if (online == null)
                return;
            online = null;
            command.IsReadOnly = false;
            command.Clear();
            chargeInput.Clear();
            status.Text = "输入 start 开始单人游戏。";
            UpdateMusic(false);
            Render();
        }

        private void HandleRoomCommand()
        {
            if (online.Phase != MatchPhase.Playing)
            {
                if (ownSlot == 0 && command.Text.Equals("start", StringComparison.OrdinalIgnoreCase))
                {
                    command.Clear();
                    if (soloMatch == null)
                        lobby.Start();
                    else
                        Start();
                }
                return;
            }

            var own = online.Players.First(player => player.Slot == ownSlot);
            if (own.Result != SnakeResult.Running)
                return;
            SubmitWord(online.Words, own.Role, own.Direction, own.StunRemaining > 0, word =>
            {
                if (soloMatch == null)
                    lobby.Turn(word, online.WordGroup, online.Round);
                else if (soloMatch.TryWord(0, word, online.WordGroup, online.Round, clock.ElapsedMilliseconds))
                    RefreshSoloMatch();
            });
        }

        private void RefreshSoloMatch()
        {
            ApplyRoomState(soloMatch.Snapshot("单人游戏", 0), 0);
        }

        private static string ShrinkGuide(int side, int next)
        {
            return side <= 7 ? "缩圈已完成" : "下次缩圈 " + ((next + 999) / 1000) + " 秒";
        }

        internal static string TerritoryResult(SnakeMatchState state)
        {
            var ranked = state.Rankings;
            var winners = ranked.TakeWhile(player => player.Score == ranked[0].Score).ToArray();
            return winners.Length == 1 ? winners[0].Name + "获胜" :
                "并列第一 · " + string.Join("、", winners.Select(player => player.Name));
        }

        private void ApplyRoomState(SnakeMatchState state, int slot)
        {
            var previous = online;
            var own = state.Players.First(player => player.Slot == slot);
            var previousOwn = previous == null ? null : previous.Players.FirstOrDefault(player => player.Slot == slot);
            if (previousOwn == null || previous.Round != state.Round || own.Respawns != previousOwn.Respawns)
            {
                ClearLook();
                board.ResetCamera();
            }
            ownSlot = slot;
            online = state;
            if (modePicker.SelectedIndex != (int)state.NextMode)
            {
                updatingMode = true;
                modePicker.SelectedIndex = (int)state.NextMode;
                updatingMode = false;
            }
            if (previousOwn != null && previous.Round == state.Round && own.WordCount > previousOwn.WordCount)
            {
                FlashWord(own.LastWord);
                if (previous.WordGroup != state.WordGroup &&
                    previousOwn.Respawns == own.Respawns)
                    PulseDirection(own.Direction);
            }
            if (previousOwn != null && previous.Round == state.Round && own.Respawns == previousOwn.Respawns &&
                own.Result == SnakeResult.Running && previousOwn.Result == SnakeResult.Running &&
                previous.WordGroup != state.WordGroup && own.Direction != previousOwn.Direction)
                TurnFeedback(previousOwn.Direction);
            if (state.Phase == MatchPhase.Playing && rolePicker.SelectedIndex != (int)own.Role)
            {
                updatingRole = true;
                rolePicker.SelectedIndex = (int)own.Role;
                updatingRole = false;
            }
            bool running = state.Phase == MatchPhase.Playing && own.Result == SnakeResult.Running;
            bool restoreCommandFocus = running && (previous == null || previous.Round != state.Round);
            if (previousOwn == null || previous.Round != state.Round || previous.Phase != state.Phase ||
                previousOwn.Result != own.Result)
            {
                command.Clear();
                chargeInput.Clear();
                status.Text = state.Phase == MatchPhase.Lobby
                    ? (slot == 0 ? "等待同学加入，输入 start 开局。" : "等待房主输入 start。")
                    : state.Phase == MatchPhase.Finished
                        ? ((own.Result == SnakeResult.Survived ? "你是最后存活的玩家。" : "本局结束。") +
                            (soloMatch != null ? (state.Mode == SnakeMode.Territory
                                ? "共占领 " + own.Score + " 格。" : "得分 " + own.Score + "。") : "") +
                            (slot == 0 ? "输入 start 再开一局。" : "等待房主开局。"))
                        : running ? "" : "你已出局，可以继续观战。";
            }
            else if (previous.WordGroup != state.WordGroup)
            {
                status.Text = "";
            }
            if (previous != null && previous.ChargeGroup != state.ChargeGroup)
                chargeInput.Clear();
            if (previousOwn != null && previous.Round == state.Round)
            {
                if (own.StunRemaining > 0 && previousOwn.StunRemaining == 0)
                {
                    command.Clear();
                    chargeInput.Clear();
                }
                if (running && own.Respawns != previousOwn.Respawns)
                {
                    command.Clear();
                    chargeInput.Clear();
                    status.Text = "已回到起点。";
                }
            }
            if (state.Mode == SnakeMode.Teams && state.Phase == MatchPhase.Playing && !running)
                status.Text = own.RespawnRemaining > 0
                    ? "复活倒计时 " + ((own.RespawnRemaining + 999) / 1000) + " 秒"
                    : "正在寻找己方复活位置。";
            command.IsReadOnly = state.Phase == MatchPhase.Playing ? !running || own.StunRemaining > 0 : slot != 0;
            if (restoreCommandFocus && !command.IsKeyboardFocused)
                command.Focus();
            ShowWords(running, own.Direction, state.Words, own.Role);
            ShowRole(state.Phase == MatchPhase.Playing ? own.Role : SelectedRole,
                state.Phase == MatchPhase.Playing ? state.Mode : state.NextMode, running,
                own.Charging, own.StunRemaining, state.ChargeWord);
            bool teams = state.Mode == SnakeMode.Teams;
            score.Text = (own.Team >= 0 ? state.TeamScores[own.Team] : own.Score).ToString(CultureInfo.CurrentCulture);
            int size = state.Size;
            int side = state.SafeEnd - state.SafeStart;
            boardSize.Text = side + " × " + side + " 格";
            bool territory = state.Mode == SnakeMode.Territory;
            scoreLabel.Text = teams ? "本队小球" : territory ? "占地格数" : "分数";
            fruitLegend.Visibility = territory || teams || soloMatch != null ? Visibility.Collapsed : Visibility.Visible;
            int seconds = (state.Remaining + 999) / 1000;
            scoreGuide.Text = teams ? (state.Phase == MatchPhase.Playing
                ? string.Format("剩余 {0}:{1:00} · 携带 {2} 球 · Shift {3}", seconds / 60, seconds % 60,
                    own.Carrying, own.Carrying == 0 ? "提速" : "放球")
                : "八分钟 · Shift 放球或提速")
                : territory
                ? (state.Phase == MatchPhase.Playing ? string.Format("剩余 {0}:{1:00} · ", seconds / 60, seconds % 60) : "五分钟 · ") +
                    "每分钟提速 20% · 闭合轨迹占地"
                : state.Mode == SnakeMode.Obstacles ? "避开障碍 · " + WordScoreGuide(own.Role, state.Mode)
                : (state.Phase == MatchPhase.Playing ? ShrinkGuide(side, state.NextMinute) : "每分钟缩小 5 格") +
                    " · " + WordScoreGuide(own.Role, state.Mode);
            board.SetBoard(size, state.SmallFruits, state.LargeFruit, slot, state.Players, state.Obstacles, state.Land,
                state.Trails, state.SafeStart, state.SafeEnd, state.Shots, state.Bursts,
                state.TeamCount, state.TeamBalls, soloMatch == null);
            UpdateOverview(size);
            PublishTyping(false);
            UpdateMusic(running && soloMatch != null);
        }

        private void UpdateOverview(int size)
        {
            overview.Visibility = size > SnakeGame.BaseSize || board.CameraLocked ? Visibility.Visible : Visibility.Collapsed;
            overviewSize.Text = size + " × " + size + " 格";
            cameraMode.Text = board.CameraLocked ? "锁定视角 · Alt 切换" : "自由视角 · Alt 切换";
            cameraLookHint.Visibility = board.CameraLocked ? Visibility.Visible : Visibility.Collapsed;
        }

        private void UpdateLook()
        {
            board.SetLook(new Vector((lookKeys.Contains(Key.Right) ? 1 : 0) - (lookKeys.Contains(Key.Left) ? 1 : 0),
                (lookKeys.Contains(Key.Down) ? 1 : 0) - (lookKeys.Contains(Key.Up) ? 1 : 0)));
        }

        private void ClearLook()
        {
            if (lookKeys.Count == 0)
                return;
            lookKeys.Clear();
            UpdateLook();
        }

        private void UpdateSetup()
        {
            setup.Visibility = !rolePicker.IsEnabled && playArea.ActualHeight < 420
                ? Visibility.Collapsed : Visibility.Visible;
        }

        private void UpdateMusic(bool running)
        {
            if (musicRunning == running)
                return;
            musicRunning = running;
            gameRunningChanged(running);
        }

        private void StopSprint()
        {
            if (sprintRound == 0)
                return;
            lobby.SetSprint(sprintRound, false);
            sprintRound = 0;
        }

        private void FlashWord(string word)
        {
            if (string.IsNullOrEmpty(word))
                return;
            flashWord.Text = flashBlue.Text = flashRose.Text = word;
            flashMeaning.Text = SnakeWords.Meaning(word);
            if (board.IsVisible && voiceEnabled.IsChecked == true)
            {
                int volume = online != null && soloMatch == null ? settings.RoomEffectsVolume : 100;
                if (volume > 0 && voice.Enable())
                    voice.Speak(word, volume);
            }
            var fade = new DoubleAnimationUsingKeyFrames { FillBehavior = FillBehavior.Stop };
            fade.KeyFrames.Add(new DiscreteDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.Zero)));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(1, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(620))));
            fade.KeyFrames.Add(new LinearDoubleKeyFrame(0, KeyTime.FromTimeSpan(TimeSpan.FromMilliseconds(1040))));
            wordFlash.BeginAnimation(UIElement.OpacityProperty, fade);
            for (int i = 0; i < 2; i++)
            {
                var transform = (TranslateTransform)(i == 0 ? flashBlue : flashRose).RenderTransform;
                transform.BeginAnimation(TranslateTransform.XProperty,
                    new DoubleAnimation(i == 0 ? -8 : 8, i == 0 ? -2 : 2, TimeSpan.FromMilliseconds(300)));
            }
        }

        private void TurnFeedback(SnakeDirection previousDirection)
        {
            board.ShowTurnEffect(previousDirection);
            if (board.IsVisible && SystemParameters.ClientAreaAnimation)
                shakeWindow(2.5, 140);
        }

        private void PulseDirection(SnakeDirection direction)
        {
            if (!SystemParameters.ClientAreaAnimation)
                return;
            var card = (Border)((StackPanel)directionWords[(int)direction].Parent).Parent;
            var current = (SolidColorBrush)card.FindResource("Line");
            var accent = (SolidColorBrush)card.FindResource("Accent");
            var edge = new SolidColorBrush(current.Color);
            card.BorderBrush = edge;
            var fade = new ColorAnimation(accent.Color, current.Color, TimeSpan.FromMilliseconds(340))
                { FillBehavior = FillBehavior.Stop };
            fade.Completed += (sender, e) =>
            {
                if (ReferenceEquals(card.BorderBrush, edge))
                    card.ClearValue(Border.BorderBrushProperty);
            };
            edge.BeginAnimation(SolidColorBrush.ColorProperty, fade);
        }
    }

    internal sealed class SnakeBoard : FrameworkElement
    {
        private const int CameraMargin = 6;
        private const int CameraStep = 8;
        private const int TurnEffectLength = 300;
        private const int FireEffectLength = 280;
        private const int BurstEffectLength = 520;
        private static readonly DependencyProperty CameraOffsetProperty = DependencyProperty.Register(
            "CameraOffset", typeof(Point), typeof(SnakeBoard),
            new FrameworkPropertyMetadata(new Point(), FrameworkPropertyMetadataOptions.AffectsRender,
                (sender, e) => ((SnakeBoard)sender).Minimap.InvalidateVisual()));

        private sealed class MapOverview : FrameworkElement
        {
            private readonly SnakeBoard board;

            internal MapOverview(SnakeBoard board)
            {
                this.board = board;
            }

            protected override void OnRender(DrawingContext drawing)
            {
                double side = Math.Min(ActualWidth, ActualHeight);
                if (side > 0)
                    board.DrawOverview(drawing, side);
            }
        }

        private sealed class ShotVisual
        {
            internal int Cell;
            internal Point From;
            internal Vector Direction;
            internal double MovedAt;
        }

        private struct Flash
        {
            internal Point Origin;
            internal Vector Direction;
            internal double StartedAt;
        }

        private static readonly Brush Head = MakeBrush(236, 246, 100);
        internal static readonly Brush Current = MakeBrush(49, 63, 42);
        internal static readonly Brush Quiet = MakeBrush(158, 181, 188);
        internal static readonly Brush Label = MakeBrush(228, 244, 237);
        private static readonly Brush Board = MakeBrush(16, 30, 39);
        private static readonly Brush Bubble = MakeBrush(30, 49, 58);
        private static readonly Brush Body = MakeBrush(135, 194, 100);
        private static readonly Brush Fruit = MakeBrush(255, 139, 113);
        private static readonly Brush LargeFruit = MakeBrush(255, 192, 99);
        private static readonly Brush FruitHighlight = MakeBrush(255, 240, 183);
        private static readonly Brush ChargeGlow = MakeBrush(129, 230, 238);
        private static readonly Brush StunFill = MakeBrush(149, 154, 167);
        private static readonly Brush ClosedZone = MakeBrush(47, 58, 66);
        private static readonly Brush OutsideMap = MakeBrush(9, 16, 23);
        private static readonly Brush ShotCore = MakeBrush(255, 248, 220);
        private static readonly Brush ShotGlow = MakeBrush(255, 105, 44, 90);
        private static readonly Pen ShotTrail = MakePen(MakeBrush(255, 126, 57, 155), 2.5);
        private static readonly Brush BurstFill = MakeBrush(207, 129, 255, 55);
        private static readonly Pen BurstOutline = MakeBurstPen();
        private static readonly Brush[] TurnColors =
        {
            MakeBrush(93, 225, 252), MakeBrush(242, 144, 248), MakeBrush(237, 251, 172)
        };
        private static readonly Pen FruitArrowOutline = MakePen(MakeBrush(69, 38, 20), 1.6);
        private static readonly Brush Obstacle = MakeBrush(93, 118, 127);
        private static readonly Pen ObstacleOutline = MakeObstaclePen();
        private static readonly Pen Grid = MakeGridPen();
        private static readonly Pen ViewOutline = MakePen(Head, 1.25);
        private static readonly Brush[] Stars =
        {
            MakeBrush(255, 255, 255, 82),
            MakeBrush(255, 255, 255, 135),
            MakeBrush(255, 255, 255, 197)
        };
        private static readonly Brush[] Rivals =
        {
            MakeBrush(103, 189, 239), MakeBrush(209, 146, 241), MakeBrush(247, 193, 93)
        };
        private static readonly Brush[] TerritoryColors = { Body, Rivals[0], Rivals[1], Rivals[2] };
        private static readonly string[] TeamNames = { "绿队", "蓝队", "紫队", "金队" };
        private static readonly Brush[] LandColors =
        {
            MakeBrush(36, 64, 47), MakeBrush(34, 62, 83), MakeBrush(63, 46, 78), MakeBrush(75, 60, 38)
        };
        private int size = SnakeGame.BaseSize;
        private int[] smallFruits = new int[0];
        private int largeFruit = -1;
        private int ownSlot;
        private SnakePlayerState[] players = new SnakePlayerState[0];
        private int[] obstacles;
        private int[] land;
        private int[] trails;
        private int safeStart;
        private int safeEnd;
        private int[] shots;
        private int[] bursts;
        private ShotVisual[] shotVisuals = new ShotVisual[0];
        private readonly System.Collections.Generic.List<Flash> muzzleFlashes = new System.Collections.Generic.List<Flash>();
        private readonly System.Collections.Generic.List<Flash> burstFlashes = new System.Collections.Generic.List<Flash>();
        private readonly System.Collections.Generic.List<Flash> turnFlashes = new System.Collections.Generic.List<Flash>();
        private readonly Stopwatch effectClock = Stopwatch.StartNew();
        private readonly DispatcherTimer effectTimer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(24) };
        private int teams;
        private TeamBall[] teamBalls;
        private bool showNames;
        private string localTyping;
        private bool localTypingSkill;
        private Int32Rect viewport = new Int32Rect(0, 0, SnakeGame.BaseSize, SnakeGame.BaseSize);
        private bool resetCamera;
        private int respawns;
        private bool cameraLocked;
        private Point cameraHead = new Point(16.5, 16.5);
        private Vector lookOffset;

        internal SnakeBoard()
        {
            Minimap = new MapOverview(this);
            effectTimer.Tick += (sender, e) =>
            {
                double now = effectClock.Elapsed.TotalMilliseconds;
                turnFlashes.RemoveAll(flash => now - flash.StartedAt >= TurnEffectLength);
                muzzleFlashes.RemoveAll(flash => now - flash.StartedAt >= FireEffectLength);
                burstFlashes.RemoveAll(flash => now - flash.StartedAt >= BurstEffectLength);
                if (turnFlashes.Count == 0 && muzzleFlashes.Count == 0 && burstFlashes.Count == 0 &&
                    !shotVisuals.Any(shot => now - shot.MovedAt < 95))
                    effectTimer.Stop();
                InvalidateVisual();
            };
            IsVisibleChanged += (sender, e) =>
            {
                if (!IsVisible)
                    effectTimer.Stop();
            };
        }

        internal FrameworkElement Minimap { get; private set; }
        internal bool CameraLocked { get { return cameraLocked; } }
        internal int MapSize { get { return size; } }

        internal void ToggleCamera()
        {
            cameraLocked = !cameraLocked;
            lookOffset = new Vector();
            PositionCamera(size, true);
        }

        internal void SetLook(Vector direction)
        {
            if (!cameraLocked)
                return;
            lookOffset = direction * CameraStep;
            PositionCamera(size, false);
        }

        internal void SetLocalTyping(string text, bool skill)
        {
            if (localTyping == text && localTypingSkill == skill)
                return;
            localTyping = text;
            localTypingSkill = skill;
            InvalidateVisual();
        }

        internal void ResetCamera()
        {
            resetCamera = true;
        }

        internal void ShowTurnEffect(SnakeDirection previousDirection)
        {
            var own = players.FirstOrDefault(player => player.Slot == ownSlot && player.Body.Length > 0);
            if (own == null || !IsVisible)
                return;
            AddTurnEffect(own.Body[0], previousDirection, effectClock.Elapsed.TotalMilliseconds);
            effectTimer.Start();
            InvalidateVisual();
        }

        private void AddTurnEffect(int head, SnakeDirection previousDirection, double now)
        {
            turnFlashes.Add(new Flash
            {
                Origin = CellCenter(head),
                Direction = DirectionVector(previousDirection),
                StartedAt = now
            });
        }

        internal void SetBoard(int size, int[] smallFruits, int largeFruit, int ownSlot, SnakePlayerState[] players,
            int[] obstacles = null, int[] land = null, int[] trails = null, int safeStart = 0, int safeEnd = 0,
            int[] shots = null, int[] bursts = null, int teams = 0, TeamBall[] teamBalls = null, bool showNames = false)
        {
            var own = players.FirstOrDefault(player => player.Slot == ownSlot && player.Body.Length > 0);
            bool reset = resetCamera || size != this.size || ownSlot != this.ownSlot ||
                (own != null && own.Respawns != respawns);
            if (reset || own != null)
            {
                int head = own == null ? (size / 2) * (size + 1) : own.Body[0];
                cameraHead = new Point(head % size + .5, head / size + .5);
                PositionCamera(size, reset);
            }
            if (own != null)
                respawns = own.Respawns;
            resetCamera = false;
            if (!reset && IsVisible)
            {
                double now = effectClock.Elapsed.TotalMilliseconds;
                foreach (var player in players)
                {
                    var prior = this.players.FirstOrDefault(item => item.Slot == player.Slot);
                    if (prior == null || prior.Body.Length == 0 || player.Respawns != prior.Respawns)
                        continue;
                    if (player.Slot != ownSlot && player.Body.Length > 0 && player.Direction != prior.Direction)
                        AddTurnEffect(prior.Body[0], prior.Direction, now);
                    if (player.Role == SnakeRole.Hunter && player.SkillUses > prior.SkillUses)
                        muzzleFlashes.Add(new Flash
                        {
                            Origin = CellCenter(player.Body.Length == 0 ? prior.Body[0] : player.Body[0]),
                            Direction = DirectionVector(player.Direction),
                            StartedAt = now
                        });
                }
            }
            this.size = size;
            this.smallFruits = smallFruits;
            this.largeFruit = largeFruit;
            this.ownSlot = ownSlot;
            this.players = players;
            this.obstacles = obstacles;
            this.land = land;
            this.trails = trails;
            this.safeStart = safeStart;
            this.safeEnd = safeEnd == 0 ? size : safeEnd;
            this.teams = teams;
            this.teamBalls = teamBalls;
            this.showNames = showNames;
            if (reset)
            {
                this.shots = null;
                this.bursts = null;
                shotVisuals = new ShotVisual[0];
                muzzleFlashes.Clear();
                burstFlashes.Clear();
                turnFlashes.Clear();
            }
            UpdateSkillEffects(shots, bursts);
            InvalidateVisual();
            Minimap.InvalidateVisual();
        }

        private void PositionCamera(int size, bool reset)
        {
            if (cameraLocked)
            {
                var next = cameraHead - new Vector(viewport.Width / 2.0, viewport.Height / 2.0) + lookOffset;
                if (reset)
                    BeginAnimation(CameraOffsetProperty, null);
                SetValue(CameraOffsetProperty, next);
                return;
            }
            MoveCamera(new Point(CameraOrigin((int)cameraHead.X, viewport.X, size, reset),
                CameraOrigin((int)cameraHead.Y, viewport.Y, size, reset)), reset);
        }

        private static int CameraOrigin(int position, int origin, int size, bool reset)
        {
            int side = SnakeGame.BaseSize;
            if (reset || position < origin || position >= origin + side)
                origin = position - side / 2;
            else if (position < origin + CameraMargin)
                origin -= CameraStep;
            else if (position >= origin + side - CameraMargin)
                origin += CameraStep;
            return Math.Max(0, Math.Min(size - side, origin));
        }

        private void MoveCamera(Point next, bool reset)
        {
            if (!reset && next.X == viewport.X && next.Y == viewport.Y)
                return;
            var current = (Point)GetValue(CameraOffsetProperty);
            viewport.X = (int)next.X;
            viewport.Y = (int)next.Y;
            SetValue(CameraOffsetProperty, next);
            if (!reset && current != next && IsVisible && SystemParameters.ClientAreaAnimation)
            {
                BeginAnimation(CameraOffsetProperty, new PointAnimation(current, next, TimeSpan.FromMilliseconds(190))
                {
                    EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                    FillBehavior = FillBehavior.Stop
                });
            }
            else
                BeginAnimation(CameraOffsetProperty, null);
        }

        protected override void OnRender(DrawingContext drawing)
        {
            double side = Math.Min(ActualWidth, ActualHeight);
            if (side <= 0)
                return;

            var area = new Rect((ActualWidth - side) / 2, (ActualHeight - side) / 2, side, side);
            double cell = side / SnakeGame.BaseSize;
            var offset = (Point)GetValue(CameraOffsetProperty);
            var world = new Rect(area.X - offset.X * cell, area.Y - offset.Y * cell, size * cell, size * cell);
            int left = Math.Max(0, (int)Math.Floor(offset.X)), top = Math.Max(0, (int)Math.Floor(offset.Y));
            var visible = new Int32Rect(left, top,
                Math.Min(size, (int)Math.Ceiling(offset.X + viewport.Width)) - left,
                Math.Min(size, (int)Math.Ceiling(offset.Y + viewport.Height)) - top);
            drawing.PushClip(new RectangleGeometry(area));
            if (cameraLocked)
                drawing.DrawRectangle(OutsideMap, null, area);
            DrawTerrain(drawing, world, cell, visible, true);
            var gridArea = Rect.Intersect(area, world);
            for (int x = visible.X + 1; x < visible.X + visible.Width; x++)
            {
                double line = world.X + x * cell;
                drawing.DrawLine(Grid, new Point(line, gridArea.Top), new Point(line, gridArea.Bottom));
            }
            for (int y = visible.Y + 1; y < visible.Y + visible.Height; y++)
            {
                double line = world.Y + y * cell;
                drawing.DrawLine(Grid, new Point(gridArea.Left, line), new Point(gridArea.Right, line));
            }
            DrawPieces(drawing, world, cell, visible);
            DrawBubbles(drawing, area, world, cell);
            DrawFruitArrow(drawing, area, world, cell);
            drawing.Pop();
        }

        private void DrawTerrain(DrawingContext drawing, Rect area, double cell, Int32Rect region, bool drawStars = false)
        {
            drawing.DrawRectangle(Board, null, area);
            if (teams > 0)
            {
                double width = teams == 4 ? area.Width / 2 : area.Width / teams;
                double height = teams == 4 ? area.Height / 2 : area.Height;
                for (int team = 0; team < teams; team++)
                    drawing.DrawRectangle(LandColors[team], null, new Rect(
                        area.X + (teams == 4 ? team % 2 : team) * width,
                        area.Y + (teams == 4 ? team / 2 : 0) * height, width, height));
            }
            if (safeEnd > 0 && (safeStart > 0 || safeEnd < size))
            {
                drawing.DrawRectangle(ClosedZone, null, area);
                double safeSide = (safeEnd - safeStart) * cell;
                drawing.DrawRectangle(Board, null, new Rect(area.X + safeStart * cell, area.Y + safeStart * cell,
                    safeSide, safeSide));
            }
            if (land != null)
                for (int y = region.Y; y < region.Y + region.Height; y++)
                    for (int x = region.X; x < region.X + region.Width; x++)
                    {
                        int i = y * size + x;
                        if (land[i] != 0)
                            drawing.DrawRectangle(LandColors[land[i] - 1], null, CellBounds(i, area, cell));
                    }

            if (drawStars)
                DrawStars(drawing, area, cell, region);
            if (obstacles != null)
                foreach (int obstacle in obstacles)
                    drawing.DrawRectangle(Obstacle, ObstacleOutline, CellBounds(obstacle, area, cell));
        }

        private static void DrawStars(DrawingContext drawing, Rect world, double cell, Int32Rect visible)
        {
            for (int y = visible.Y; y < visible.Y + visible.Height; y++)
            {
                for (int x = visible.X; x < visible.X + visible.Width; x++)
                {
                    // Derive position and brightness from world coordinates so stars move with the map.
                    uint star = unchecked((uint)x * 0x9E3779B9u ^ (uint)y * 0x85EBCA6Bu);
                    star = unchecked((star ^ (star >> 16)) * 0x7FEB352Du);
                    star ^= star >> 15;
                    if (star % 19 != 0)
                        continue;

                    int rank = (int)((star >> 24) & 7);
                    int style = rank == 0 ? 2 : rank <= 2 ? 1 : 0;
                    double dx = .20 + ((star >> 5) & 15) * .04;
                    double dy = .20 + ((star >> 13) & 15) * .04;
                    var point = new Point(world.X + (x + dx) * cell, world.Y + (y + dy) * cell);
                    double radius = Math.Max(.55, Math.Min(2.7, cell * (.052 + style * .033)));
                    drawing.DrawEllipse(Stars[style], null, point, radius, radius);
                }
            }
        }

        private void DrawPieces(DrawingContext drawing, Rect area, double cell, Int32Rect visible)
        {
            double now = effectClock.Elapsed.TotalMilliseconds;
            if (trails != null)
                for (int y = visible.Y; y < visible.Y + visible.Height; y++)
                {
                    for (int x = visible.X; x < visible.X + visible.Width; x++)
                    {
                        int i = y * size + x;
                        if (trails[i] == 0)
                            continue;
                        var bounds = CellBounds(i, area, cell);
                        int stripe = 0;
                        for (int slot = 0; slot < TerritoryColors.Length; slot++)
                            if ((trails[i] & (1 << slot)) != 0)
                            {
                                drawing.DrawRectangle(TerritoryColors[slot], null, new Rect(
                                    bounds.X + cell * 0.15, bounds.Y + cell * (0.12 + 0.2 * stripe),
                                    cell * 0.7, cell * 0.15));
                                stripe++;
                            }
                    }
                }

            DrawTurnEffect(drawing, area, cell, now);
            foreach (var player in players)
            {
                Brush fill = player.Charging ? ChargeGlow : player.StunRemaining > 0 ? StunFill :
                    player.Team >= 0 ? TerritoryColors[player.Team] : PlayerColor(player.Slot, ownSlot, land != null);
                for (int i = 0; i < player.Body.Length; i++)
                {
                    if (!InView(player.Body[i], visible))
                        continue;
                    var bounds = CellBounds(player.Body[i], area, cell);
                    drawing.DrawRectangle(teams == 0 && land == null && i == 0 && player.Slot == ownSlot && !player.Charging &&
                        player.StunRemaining == 0 ? Head : fill, null, bounds);
                    if (i == 0)
                    {
                        var center = new Point(bounds.X + cell / 2, bounds.Y + cell / 2);
                        drawing.DrawEllipse(Brushes.White, null, center, cell * 0.15, cell * 0.15);
                    }
                }
            }
            if (teamBalls != null)
                foreach (var ball in teamBalls)
                    if (ball.Cell >= 0)
                    {
                        var bounds = CellBounds(ball.Cell, area, cell);
                        var center = new Point(bounds.X + cell / 2, bounds.Y + cell / 2);
                        drawing.DrawEllipse(TerritoryColors[ball.Team], null, center, cell * 0.47, cell * 0.47);
                        drawing.DrawEllipse(Brushes.White, null, new Point(center.X - cell * 0.12, center.Y - cell * 0.12),
                            cell * 0.10, cell * 0.10);
                    }

            foreach (int fruit in smallFruits)
                drawing.DrawRectangle(Fruit, null, CellBounds(fruit, area, cell));
            if (largeFruit >= 0)
            {
                var anchor = CellBounds(largeFruit, area, cell);
                drawing.DrawRectangle(LargeFruit, null, new Rect(anchor.X, anchor.Y, cell * 2, cell * 2));
                drawing.DrawRectangle(FruitHighlight, null,
                    new Rect(anchor.X + cell * 0.65, anchor.Y + cell * 0.65, cell * 0.7, cell * 0.7));
            }
            DrawSkillEffects(drawing, area, cell, now);
        }

        private void UpdateSkillEffects(int[] nextShots, int[] nextBursts)
        {
            double now = effectClock.Elapsed.TotalMilliseconds;
            if (nextShots == null)
                shotVisuals = new ShotVisual[0];
            else if (shots == null || !shots.SequenceEqual(nextShots))
            {
                var previous = shotVisuals;
                var matched = new bool[previous.Length];
                shotVisuals = new ShotVisual[nextShots.Length];
                for (int i = 0; i < nextShots.Length; i++)
                {
                    int cell = nextShots[i];
                    int nearest = -1;
                    double distance = 5;
                    for (int j = 0; j < previous.Length; j++)
                    {
                        var motion = CellCenter(cell) - CellCenter(previous[j].Cell);
                        double steps = Math.Abs(motion.X) + Math.Abs(motion.Y);
                        if (matched[j] || steps >= distance || (motion.X != 0 && motion.Y != 0) ||
                            (steps > 0 && previous[j].Direction.LengthSquared > 0 &&
                                Vector.Multiply(motion, previous[j].Direction) <= 0))
                            continue;
                        nearest = j;
                        distance = steps;
                    }
                    if (nearest >= 0)
                    {
                        matched[nearest] = true;
                        var shot = previous[nearest];
                        if (shot.Cell != cell)
                        {
                            Point destination = CellCenter(cell);
                            shot.From = ShotPosition(shot, now);
                            var direction = destination - CellCenter(shot.Cell);
                            direction.Normalize();
                            shot.Cell = cell;
                            shot.Direction = direction;
                            shot.MovedAt = now;
                        }
                        shotVisuals[i] = shot;
                    }
                    else
                        shotVisuals[i] = StartShot(cell, now);
                }
            }

            if (nextBursts != null && IsVisible)
                foreach (int cell in nextBursts.Distinct())
                    if (bursts == null || nextBursts.Count(other => other == cell) > bursts.Count(other => other == cell))
                        burstFlashes.Add(new Flash { Origin = CellCenter(cell), StartedAt = now });
            shots = nextShots;
            bursts = nextBursts;
            if (IsVisible && (turnFlashes.Count > 0 || muzzleFlashes.Count > 0 || burstFlashes.Count > 0 ||
                shotVisuals.Any(shot => now - shot.MovedAt < 95)))
                effectTimer.Start();
        }

        private ShotVisual StartShot(int cell, double now)
        {
            Point center = CellCenter(cell);
            var origin = center;
            var direction = new Vector();
            double closest = 4;
            foreach (var player in players)
            {
                if (player.Role != SnakeRole.Hunter || player.Body.Length == 0)
                    continue;
                var head = CellCenter(player.Body[0]);
                var facing = DirectionVector(player.Direction);
                var path = center - head;
                double distance = Vector.Multiply(path, facing);
                if (distance < 0 || distance >= closest || (path - facing * distance).LengthSquared > 0)
                    continue;
                closest = distance;
                origin = head;
                direction = facing;
            }
            if (IsVisible && !muzzleFlashes.Any(flash => now - flash.StartedAt < 95 && (flash.Origin - origin).LengthSquared <= 1))
                muzzleFlashes.Add(new Flash { Origin = origin, Direction = direction, StartedAt = now });
            return new ShotVisual { Cell = cell, From = origin, Direction = direction, MovedAt = now };
        }

        private Point ShotPosition(ShotVisual shot, double now)
        {
            var destination = CellCenter(shot.Cell);
            double progress = SystemParameters.ClientAreaAnimation ? Math.Min(1, (now - shot.MovedAt) / 95) : 1;
            return shot.From + (destination - shot.From) * progress;
        }

        private void DrawTurnEffect(DrawingContext drawing, Rect area, double cell, double now)
        {
            foreach (var flash in turnFlashes)
            {
                double progress = (now - flash.StartedAt) / TurnEffectLength;
                if (progress >= 1)
                    continue;
                Point head = ToScreen(flash.Origin, area, cell);
                var side = new Vector(-flash.Direction.Y, flash.Direction.X);
                drawing.PushOpacity((1 - progress) * .72);
                for (int i = 0; i < TurnColors.Length; i++)
                {
                    Point afterimage = head + flash.Direction * (cell * (.34 + i * .24)) + side * (cell * (i - 1) * .14);
                    double radius = cell * (.35 - i * .05);
                    drawing.DrawRectangle(TurnColors[i], null,
                        new Rect(afterimage.X - radius, afterimage.Y - radius, radius * 2, radius * 2));
                }
                drawing.Pop();
            }
        }

        private void DrawSkillEffects(DrawingContext drawing, Rect area, double cell, double now)
        {
            foreach (var shot in shotVisuals)
            {
                Point center = ToScreen(ShotPosition(shot, now), area, cell);
                if (shot.Direction.LengthSquared > 0)
                {
                    Point tail = center - shot.Direction * (cell * 1.75);
                    drawing.DrawLine(ShotTrail, tail, center);
                    for (int i = 1; i <= 3; i++)
                    {
                        Point spark = center - shot.Direction * (cell * (.38 + i * .36));
                        double radius = cell * (.22 - .035 * i);
                        drawing.DrawEllipse(ShotGlow, null, spark, radius, radius);
                    }
                }
                drawing.DrawEllipse(ShotGlow, null, center, cell * .54, cell * .54);
                drawing.DrawEllipse(ShotCore, null, center, cell * .20, cell * .20);
            }
            foreach (var flash in muzzleFlashes)
            {
                double progress = (now - flash.StartedAt) / FireEffectLength;
                if (progress >= 1)
                    continue;
                Point center = ToScreen(flash.Origin, area, cell) + flash.Direction * (cell * .52);
                drawing.PushOpacity(1 - progress);
                double radius = cell * (.42 + progress * .45);
                drawing.DrawEllipse(ShotGlow, null, center, radius, radius);
                drawing.DrawEllipse(null, ShotTrail, center, radius, radius);
                drawing.DrawEllipse(ShotCore, null, center, cell * .22, cell * .22);
                if (flash.Direction.LengthSquared > 0)
                    drawing.DrawLine(ShotTrail, center, center + flash.Direction * (cell * (1.1 + progress)));
                drawing.Pop();
            }
            foreach (var flash in burstFlashes)
            {
                double progress = (now - flash.StartedAt) / BurstEffectLength;
                if (progress >= 1)
                    continue;
                var burst = new Rect(flash.Origin.X - 2.5, flash.Origin.Y - 2.5, 5, 5);
                burst.Intersect(new Rect(0, 0, size, size));
                var pulseArea = new Rect(ToScreen(burst.TopLeft, area, cell), ToScreen(burst.BottomRight, area, cell));
                Point center = ToScreen(flash.Origin, area, cell);
                drawing.PushOpacity(1 - progress);
                drawing.DrawRectangle(BurstFill, BurstOutline, pulseArea);
                double radius = cell * (.25 + progress * 2.25);
                drawing.DrawEllipse(null, BurstOutline, center, radius, radius);
                drawing.DrawEllipse(ShotCore, null, center, cell * .18, cell * .18);
                drawing.Pop();
            }
        }

        private void DrawFruitArrow(DrawingContext drawing, Rect viewportArea, Rect world, double cell)
        {
            if (largeFruit < 0)
                return;
            var own = players.FirstOrDefault(player => player.Slot == ownSlot && player.Body.Length > 0);
            if (own == null)
                return;
            Point head = ToScreen(CellCenter(own.Body[0]), world, cell);
            if (!viewportArea.Contains(head))
                return;
            Point fruit = ToScreen(CellCenter(largeFruit) + new Vector(.5, .5), world, cell);
            var direction = fruit - head;
            if (direction.LengthSquared == 0)
                return;
            direction.Normalize();
            var normal = new Vector(-direction.Y, direction.X);
            double length = Math.Max(3.5, Math.Min(9, cell * .48));
            var bounds = viewportArea;
            bounds.Inflate(-length, -length);
            if (bounds.IsEmpty)
                return;

            var anchor = head + direction * (cell * 1.35 + length);
            double separation = cell * 1.1 + length;
            var nearFruit = new Rect(fruit.X - cell, fruit.Y - cell, 2 * cell, 2 * cell);
            nearFruit.Inflate(length * .5, length * .5);
            foreach (var candidate in new[]
            {
                anchor, head + normal * separation, head - normal * separation,
                head - direction * separation, head + (normal - direction) * separation,
                head - (normal + direction) * separation
            })
            {
                if (!bounds.Contains(candidate) || nearFruit.Contains(candidate) || own.Body.Any(position =>
                {
                    Rect body = CellBounds(position, world, cell);
                    body.Inflate(length * .70, length * .70);
                    return body.Contains(candidate);
                }))
                    continue;
                var arrow = new StreamGeometry();
                using (var shape = arrow.Open())
                {
                    shape.BeginFigure(candidate + direction * length, true, true);
                    shape.LineTo(candidate - direction * length * .75 + normal * length * .70, true, false);
                    shape.LineTo(candidate - direction * length * .33, true, false);
                    shape.LineTo(candidate - direction * length * .75 - normal * length * .70, true, false);
                }
                drawing.DrawGeometry(LargeFruit, FruitArrowOutline, arrow);
                return;
            }
        }

        private static Point ToScreen(Point point, Rect world, double cell)
        {
            return new Point(world.X + point.X * cell, world.Y + point.Y * cell);
        }

        private static Vector DirectionVector(SnakeDirection direction)
        {
            switch (direction)
            {
                case SnakeDirection.North: return new Vector(0, -1);
                case SnakeDirection.East: return new Vector(1, 0);
                case SnakeDirection.South: return new Vector(0, 1);
                default: return new Vector(-1, 0);
            }
        }

        private void DrawOverview(DrawingContext drawing, double side)
        {
            var world = new Rect(0, 0, side, side);
            double cell = side / size;
            DrawTerrain(drawing, world, cell, new Int32Rect(0, 0, size, size));
            foreach (int fruit in smallFruits)
                DrawMapItem(drawing, Fruit, fruit, world, cell);
            if (largeFruit >= 0)
                DrawMapItem(drawing, LargeFruit, largeFruit, world, cell);
            if (teamBalls != null)
                foreach (var ball in teamBalls)
                    if (ball.Cell >= 0)
                        DrawMapItem(drawing, TerritoryColors[ball.Team], ball.Cell, world, cell);
            var offset = (Point)GetValue(CameraOffsetProperty);
            var view = Rect.Intersect(world, new Rect(offset.X * cell, offset.Y * cell, viewport.Width * cell, viewport.Height * cell));
            if (!view.IsEmpty)
            {
                view.Inflate(-Math.Min(.65, view.Width / 4), -Math.Min(.65, view.Height / 4));
                drawing.DrawRectangle(null, ViewOutline, view);
            }
            foreach (var player in players.OrderBy(item => item.Slot == ownSlot))
            {
                if (player.Body.Length == 0)
                    continue;
                bool own = player.Slot == ownSlot;
                var head = CellBounds(player.Body[0], world, cell);
                var color = own ? Head : teams > 0 && player.Team >= 0 ? TerritoryColors[player.Team] :
                    PlayerColor(player.Slot, ownSlot, land != null);
                double radius = own ? 3.3 : 2.65;
                double inset = radius + .5;
                var point = new Point(Math.Max(inset, Math.Min(side - inset, head.X + cell / 2)),
                    Math.Max(inset, Math.Min(side - inset, head.Y + cell / 2)));
                drawing.DrawEllipse(color, null, point, radius, radius);
            }
        }

        private void DrawMapItem(DrawingContext drawing, Brush brush, int position, Rect world, double cell)
        {
            var bounds = CellBounds(position, world, cell);
            drawing.DrawEllipse(brush, null, new Point(bounds.X + cell / 2, bounds.Y + cell / 2), 1.3, 1.3);
        }

        private bool InView(int position, Int32Rect visible)
        {
            int x = position % size, y = position / size;
            return x >= visible.X && x < visible.X + visible.Width &&
                y >= visible.Y && y < visible.Y + visible.Height;
        }

        private void DrawBubbles(DrawingContext drawing, Rect area, Rect world, double cell)
        {
            var placed = new System.Collections.Generic.List<Rect>();
            double textWidth = Math.Max(1, Math.Min(190, area.Width - 14));
            foreach (var player in players)
            {
                if (player.Body.Length == 0)
                    continue;
                bool own = player.Slot == ownSlot;
                string name = showNames ? player.Name : null;
                string input = own ? localTyping : player.Result == SnakeResult.Running ? player.Typing ?? "" : null;
                bool skill = own ? localTypingSkill : player.TypingSkill;
                if (string.IsNullOrEmpty(name) && input == null)
                    continue;
                var head = CellBounds(player.Body[0], world, cell);
                var center = new Point(head.X + cell / 2, head.Y + cell / 2);
                if (!area.Contains(center))
                    continue;
                FormattedText label = string.IsNullOrEmpty(name) ? null : BubbleText(name,
                    "Segoe UI, Microsoft YaHei UI", Label, Math.Min(138, textWidth));
                FormattedText typing = null;
                if (input != null)
                {
                    string text = (skill ? "技能 " : "") + (input.Length == 0 ? "…" : input);
                    typing = BubbleText(text, "Consolas, Microsoft YaHei UI",
                        skill ? ChargeGlow : input.Length == 0 ? Quiet : Head, textWidth);
                }
                double width = Math.Max(label == null ? 0 : label.Width, typing == null ? 0 : typing.Width) + 12;
                double labelHeight = label == null ? 0 : label.Height + 1;
                double height = labelHeight + 6 + (typing == null ? 0 : typing.Height);
                double x = Math.Max(area.Left + 1, Math.Min(area.Right - width - 1, center.X - width / 2));
                Rect bubble = Rect.Empty;
                for (int i = 0; i <= placed.Count; i++)
                {
                    double y = head.Top - (i + 1) * (height + 5);
                    if (y < area.Top)
                        y = head.Bottom + 5 + i * (height + 4);
                    bubble = new Rect(x, Math.Min(area.Bottom - height - 1, y), width, height);
                    if (!placed.Any(other => other.IntersectsWith(bubble)))
                        break;
                }
                placed.Add(bubble);
                Brush color = player.Team >= 0 ? TerritoryColors[player.Team] :
                    own ? Head : PlayerColor(player.Slot, ownSlot, land != null);
                var pen = new Pen(color, 1);
                drawing.DrawLine(pen, center, new Point(bubble.X + bubble.Width / 2,
                    bubble.Y < head.Top ? bubble.Bottom : bubble.Top));
                drawing.DrawRectangle(Bubble, pen, bubble);
                if (label != null)
                    drawing.DrawText(label, new Point(bubble.X + 6, bubble.Y + 3));
                if (typing != null)
                    drawing.DrawText(typing, new Point(bubble.X + 6, bubble.Y + labelHeight + 3));
            }
        }

        private FormattedText BubbleText(string text, string font, Brush brush, double width)
        {
            return new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(font), 11, brush, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                { MaxTextWidth = width, MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
        }

        internal static string TeamName(int team)
        {
            return TeamNames[team];
        }

        private Rect CellBounds(int position, Rect area, double cell)
        {
            return new Rect(area.X + position % size * cell, area.Y + position / size * cell, cell, cell);
        }

        private Point CellCenter(int position)
        {
            return new Point(position % size + .5, position / size + .5);
        }

        internal static Brush PlayerColor(int slot, int ownSlot, bool territory = false)
        {
            if (territory)
                return TerritoryColors[slot];
            return slot == ownSlot ? Body : Rivals[slot < ownSlot ? slot : slot - 1];
        }

        private static Pen MakeObstaclePen()
        {
            var pen = new Pen(MakeBrush(153, 173, 178), 0.8);
            pen.Freeze();
            return pen;
        }

        private static Pen MakeBurstPen()
        {
            var pen = new Pen(MakeBrush(241, 167, 255), 2.4);
            pen.Freeze();
            return pen;
        }

        private static Brush MakeBrush(byte red, byte green, byte blue, byte alpha = 255)
        {
            var brush = new SolidColorBrush(Color.FromArgb(alpha, red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Pen MakeGridPen()
        {
            return MakePen(MakeBrush(53, 75, 85), 0.65);
        }

        private static Pen MakePen(Brush brush, double width)
        {
            var pen = new Pen(brush, width);
            pen.Freeze();
            return pen;
        }
    }
}
