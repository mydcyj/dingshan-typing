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
        private readonly LanLobby lobby;
        private SnakeMatchState online;
        private SnakeMatch soloMatch;
        private int ownSlot;
        private bool musicRunning;
        private bool updatingRole;
        private bool updatingMode;
        private int sprintRound;

        internal SnakePractice(Window window, Action<bool> gameRunningChanged, PracticeSettings settings, Action saveSettings)
        {
            this.gameRunningChanged = gameRunningChanged;
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
                if ((e.Key == Key.LeftShift || e.Key == Key.RightShift) &&
                    !Keyboard.IsKeyDown(Key.LeftShift) && !Keyboard.IsKeyDown(Key.RightShift))
                    StopSprint();
            };
            window.Deactivated += (sender, e) => StopSprint();
            page.IsVisibleChanged += (sender, e) =>
            {
                if (!page.IsVisible)
                    StopSprint();
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

        private void PublishTyping()
        {
            if (online == null || soloMatch != null || online.Phase != MatchPhase.Playing)
                return;
            var own = online.Players.First(player => player.Slot == ownSlot);
            if (own.Result != SnakeResult.Running || own.StunRemaining > 0)
                return;
            bool skill = chargeInput.IsKeyboardFocusWithin && chargePanel.Visibility == Visibility.Visible;
            string text = skill ? chargeInput.Text : command.Text;
            lobby.SetPreview(new SnakeTypingPreview
            {
                Round = online.Round,
                Respawns = own.Respawns,
                Skill = skill,
                Text = new string(text.Where(c => c >= 'a' && c <= 'z' || c >= 'A' && c <= 'Z').ToArray())
            });
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
                if (game.TryWord(word, game.WordGroup, clock.ElapsedMilliseconds))
                {
                    FlashWord(game.LastWord);
                    PulseDirection(game.Direction);
                    status.Text = "";
                    Render();
                }
            });
        }

        private void Start()
        {
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
            int size = SelectedMode == SnakeMode.Classic && game.Result == SnakeResult.Ready ? SelectedMapSize : game.Size;
            int side = size == game.Size ? game.Bounds.End - game.Bounds.Start : size;
            boardSize.Text = SelectedMode == SnakeMode.Teams ? "60 × 60 格" : side + " × " + side + " 格";
            var role = game.IsRunning ? game.Role : SelectedRole;
            ShowWords(game.IsRunning, game.Direction, game.Words, role);
            ShowRole(role, game.IsRunning, game.IsCharging(clock.ElapsedMilliseconds),
                0, game.ChargeWord);
            fruitLegend.Visibility = Visibility.Collapsed;
            scoreLabel.Text = territory ? "占地格数" : "分数";
            scoreGuide.Text = SelectedMode == SnakeMode.Teams ? "八分钟 · 回己方区域按 Shift 放球"
                : territory ? "五分钟 · 闭合轨迹占地"
                : SelectedMode == SnakeMode.Obstacles ? "避开障碍 · 吃果 +10 · 有效输入按字母计分"
                : (game.IsRunning ? ShrinkGuide(side, (int)(60000 - clock.ElapsedMilliseconds % 60000)) : "每分钟缩小 5 格") +
                    " · 吃果 +10";
            board.SetBoard(size, territory || game.Fruit < 0 ? new int[0] : new[] { game.Fruit }, -1, 0, new[]
            {
                new SnakePlayerState { Slot = 0, Result = game.Result, Body = territory ? new int[0] : game.Body.ToArray(),
                    Charging = game.IsRunning && game.IsCharging(clock.ElapsedMilliseconds) }
            }, SelectedMode == SnakeMode.Obstacles ? game.Obstacles.ToArray() : null,
                safeStart: game.Bounds.Start, safeEnd: size == game.Size ? game.Bounds.End : size);
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

        private void ShowRole(SnakeRole role, bool running, bool charging, int stun, string special)
        {
            rolePicker.IsEnabled = !running && (online == null || online.Phase != MatchPhase.Playing);
            modePicker.IsEnabled = rolePicker.IsEnabled && (online == null || ownSlot == 0);
            soloMapControls.Visibility = SelectedMode == SnakeMode.Classic && (online == null || soloMatch != null)
                ? Visibility.Visible : Visibility.Collapsed;
            soloMapSize.IsEnabled = rolePicker.IsEnabled;
            string guide;
            switch (role)
            {
                case SnakeRole.Timewalker: guide = "6 / 8 / 10 字母 · 额外走 0 / 2 / 4 格"; break;
                case SnakeRole.Timid: guide = "85% 移速 · 5–6 字母"; break;
                case SnakeRole.Fearless: guide = "120% 移速 · Tab 切换冲锋输入框"; break;
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
            ownSlot = slot;
            online = state;
            if (modePicker.SelectedIndex != (int)state.NextMode)
            {
                updatingMode = true;
                modePicker.SelectedIndex = (int)state.NextMode;
                updatingMode = false;
            }
            var own = state.Players.First(player => player.Slot == slot);
            if (previous != null && previous.Round == state.Round &&
                own.WordCount > previous.Players.First(player => player.Slot == slot).WordCount)
            {
                FlashWord(own.LastWord);
                if (previous.WordGroup != state.WordGroup &&
                    previous.Players.First(player => player.Slot == slot).Respawns == own.Respawns)
                    PulseDirection(own.Direction);
            }
            if (state.Phase == MatchPhase.Playing && rolePicker.SelectedIndex != (int)own.Role)
            {
                updatingRole = true;
                rolePicker.SelectedIndex = (int)own.Role;
                updatingRole = false;
            }
            bool running = state.Phase == MatchPhase.Playing && own.Result == SnakeResult.Running;
            bool restoreCommandFocus = running && (previous == null || previous.Round != state.Round);
            if (previous == null || previous.Round != state.Round || previous.Phase != state.Phase ||
                previous.Players.First(player => player.Slot == slot).Result != own.Result)
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
            if (previous != null && previous.Round == state.Round)
            {
                var previousOwn = previous.Players.First(player => player.Slot == slot);
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
            ShowRole(state.Phase == MatchPhase.Playing ? own.Role : SelectedRole, running,
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
                : state.Mode == SnakeMode.Obstacles ? "避开障碍 · 有效输入按字母计分"
                : (state.Phase == MatchPhase.Playing ? ShrinkGuide(side, state.NextMinute) : "每分钟缩小 5 格") +
                    " · 有效输入按字母计分";
            board.SetBoard(size, state.SmallFruits, state.LargeFruit, slot, state.Players, state.Obstacles, state.Land,
                state.Trails, state.SafeStart, state.SafeEnd, state.Shots, state.Bursts,
                state.TeamCount, state.TeamBalls, soloMatch == null);
            UpdateMusic(running && soloMatch != null);
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
        private static readonly Brush ShotFill = MakeBrush(255, 149, 99);
        private static readonly Pen BurstOutline = MakeBurstPen();
        private static readonly Brush Obstacle = MakeBrush(93, 118, 127);
        private static readonly Pen ObstacleOutline = MakeObstaclePen();
        private static readonly Pen Grid = MakeGridPen();
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
        private int teams;
        private TeamBall[] teamBalls;
        private bool showNames;

        internal void SetBoard(int size, int[] smallFruits, int largeFruit, int ownSlot, SnakePlayerState[] players,
            int[] obstacles = null, int[] land = null, int[] trails = null, int safeStart = 0, int safeEnd = 0,
            int[] shots = null, int[] bursts = null, int teams = 0, TeamBall[] teamBalls = null, bool showNames = false)
        {
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
            this.shots = shots;
            this.bursts = bursts;
            this.teams = teams;
            this.teamBalls = teamBalls;
            this.showNames = showNames;
            InvalidateVisual();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            double side = Math.Min(ActualWidth, ActualHeight);
            if (side <= 0)
                return;

            var area = new Rect((ActualWidth - side) / 2, (ActualHeight - side) / 2, side, side);
            drawing.DrawRectangle(Board, null, area);
            double cell = side / size;
            if (teams > 0)
            {
                double width = teams == 4 ? side / 2 : side / teams;
                double height = teams == 4 ? side / 2 : side;
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
                for (int i = 0; i < land.Length; i++)
                    if (land[i] != 0)
                        drawing.DrawRectangle(LandColors[land[i] - 1], null, CellBounds(i, area, cell));
            for (int i = 1; i < size; i++)
            {
                drawing.DrawLine(Grid, new Point(area.X + i * cell, area.Top), new Point(area.X + i * cell, area.Bottom));
                drawing.DrawLine(Grid, new Point(area.Left, area.Y + i * cell), new Point(area.Right, area.Y + i * cell));
            }

            if (obstacles != null)
                foreach (int obstacle in obstacles)
                    drawing.DrawRectangle(Obstacle, ObstacleOutline, CellBounds(obstacle, area, cell));
            if (trails != null)
                for (int i = 0; i < trails.Length; i++)
                {
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

            foreach (var player in players)
            {
                Brush fill = player.Charging ? ChargeGlow : player.StunRemaining > 0 ? StunFill :
                    player.Team >= 0 ? TerritoryColors[player.Team] : PlayerColor(player.Slot, ownSlot, land != null);
                for (int i = 0; i < player.Body.Length; i++)
                {
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
            if (shots != null)
                foreach (int shot in shots)
                {
                    var bounds = CellBounds(shot, area, cell);
                    drawing.DrawEllipse(ShotFill, null, new Point(bounds.X + cell / 2, bounds.Y + cell / 2), cell * 0.35, cell * 0.35);
                }
            if (bursts != null)
                foreach (int burst in bursts)
                {
                    int x = burst % size, y = burst / size;
                    int left = Math.Max(0, x - 2), top = Math.Max(0, y - 2);
                    drawing.DrawRectangle(null, BurstOutline, new Rect(area.X + left * cell, area.Y + top * cell,
                        (Math.Min(size, x + 3) - left) * cell, (Math.Min(size, y + 3) - top) * cell));
                }
            if (showNames)
                DrawNames(drawing, area, cell);
        }

        private void DrawNames(DrawingContext drawing, Rect area, double cell)
        {
            var placed = new System.Collections.Generic.List<Rect>();
            foreach (var player in players.Where(item => item.Body.Length > 0 && !string.IsNullOrEmpty(item.Name)))
            {
                var head = CellBounds(player.Body[0], area, cell);
                var center = new Point(head.X + cell / 2, head.Y + cell / 2);
                var label = new FormattedText(player.Name, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                    new Typeface("Segoe UI, Microsoft YaHei UI"), 11, Label, VisualTreeHelper.GetDpi(this).PixelsPerDip)
                    { MaxTextWidth = Math.Max(1, Math.Min(138, area.Width - 14)), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                FormattedText typing = null;
                if (!string.IsNullOrEmpty(player.Typing) || player.TypingSkill)
                {
                    string text = player.TypingSkill ? "技能 " + (player.Typing == "" ? "…" : player.Typing) : player.Typing;
                    typing = new FormattedText(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                        new Typeface("Consolas, Microsoft YaHei UI"), 11, player.TypingSkill ? ChargeGlow : Head,
                        VisualTreeHelper.GetDpi(this).PixelsPerDip)
                        { MaxTextWidth = Math.Max(1, area.Width - 14), MaxLineCount = 1, Trimming = TextTrimming.CharacterEllipsis };
                }
                double width = Math.Max(label.Width, typing == null ? 0 : typing.Width) + 12;
                double height = label.Height + 6 + (typing == null ? 0 : typing.Height + 1);
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
                Brush color = player.Team >= 0 ? TerritoryColors[player.Team] : PlayerColor(player.Slot, ownSlot, land != null);
                var pen = new Pen(color, 1);
                drawing.DrawLine(pen, center, new Point(bubble.X + bubble.Width / 2,
                    bubble.Y < head.Top ? bubble.Bottom : bubble.Top));
                drawing.DrawRectangle(Bubble, pen, bubble);
                drawing.DrawText(label, new Point(bubble.X + 6, bubble.Y + 3));
                if (typing != null)
                    drawing.DrawText(typing, new Point(bubble.X + 6, bubble.Y + label.Height + 4));
            }
        }

        internal static string TeamName(int team)
        {
            return TeamNames[team];
        }

        private Rect CellBounds(int position, Rect area, double cell)
        {
            return new Rect(area.X + position % size * cell, area.Y + position / size * cell, cell, cell);
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
            var pen = new Pen(MakeBrush(214, 37, 52), 2.4);
            pen.Freeze();
            return pen;
        }

        private static Brush MakeBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Pen MakeGridPen()
        {
            var pen = new Pen(MakeBrush(53, 75, 85), 0.65);
            pen.Freeze();
            return pen;
        }
    }
}
