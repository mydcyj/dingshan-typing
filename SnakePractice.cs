using System;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal sealed class SnakePractice
    {
        private static readonly SnakeDirection[] Directions =
            (SnakeDirection[])Enum.GetValues(typeof(SnakeDirection));
        private readonly SnakeGame game = new SnakeGame();
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(15) };
        private readonly Stopwatch clock = new Stopwatch();
        private readonly TextBox command;
        private readonly ComboBox rolePicker;
        private readonly TextBlock roleGuide;
        private readonly FrameworkElement chargePanel;
        private readonly TextBlock chargeWord;
        private readonly TextBox chargeInput;
        private readonly TextBlock status;
        private readonly TextBlock score;
        private readonly TextBlock boardSize;
        private readonly TextBlock fruitLegend;
        private readonly TextBlock scoreGuide;
        private readonly TextBlock[] directionWords;
        private readonly SnakeBoard board;
        private readonly Action<bool> gameRunningChanged;
        private readonly LanLobby lobby;
        private SnakeMatchState online;
        private int ownSlot;
        private bool musicRunning;
        private bool updatingRole;

        internal SnakePractice(Window window, Action<bool> gameRunningChanged)
        {
            this.gameRunningChanged = gameRunningChanged;
            command = (TextBox)window.FindName("SnakeCommand");
            rolePicker = (ComboBox)window.FindName("SnakeRole");
            roleGuide = (TextBlock)window.FindName("SnakeRoleGuide");
            chargePanel = (FrameworkElement)window.FindName("SnakeChargePanel");
            chargeWord = (TextBlock)window.FindName("SnakeChargeWord");
            chargeInput = (TextBox)window.FindName("SnakeChargeInput");
            status = (TextBlock)window.FindName("SnakeStatus");
            score = (TextBlock)window.FindName("SnakeScore");
            directionWords = new[] { "SnakeNorth", "SnakeEast", "SnakeSouth", "SnakeWest" }
                .Select(name => (TextBlock)window.FindName(name)).ToArray();
            board = new SnakeBoard();
            ((Border)window.FindName("SnakeBoardHost")).Child = board;

            boardSize = (TextBlock)window.FindName("SnakeBoardSize");
            fruitLegend = (TextBlock)window.FindName("SnakeFruitLegend");
            scoreGuide = (TextBlock)window.FindName("SnakeScoreGuide");
            lobby = new LanLobby(window, EnterRoom, ApplyRoomState, LeaveRoom, () => SelectedRole);
            rolePicker.SelectionChanged += (sender, e) =>
            {
                if (updatingRole)
                    return;
                if (online == null)
                    Render();
                else
                    lobby.SetRole(SelectedRole);
            };
            ((FrameworkElement)window.FindName("SnakePage")).PreviewKeyDown += (sender, e) =>
            {
                if (e.Key == Key.Tab && chargePanel.Visibility == Visibility.Visible &&
                    (command.IsKeyboardFocused || chargeInput.IsKeyboardFocused))
                {
                    if (chargeInput.IsKeyboardFocused)
                        command.Focus();
                    else
                        chargeInput.Focus();
                    e.Handled = true;
                }
            };
            timer.Tick += Tick;
            command.TextChanged += OnCommandChanged;
            chargeInput.TextChanged += OnChargeChanged;
            DataObject.AddPastingHandler(command, (sender, e) => e.CancelCommand());
            DataObject.AddPastingHandler(chargeInput, (sender, e) => e.CancelCommand());
            Render();
        }

        private SnakeRole SelectedRole { get { return (SnakeRole)rolePicker.SelectedIndex; } }

        internal void Focus()
        {
            command.Focus();
        }

        internal void Leave()
        {
            lobby.Close();
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
                    lobby.HidePanel();
                    Start();
                }
                return;
            }

            SubmitWord(game.Words, game.Role, game.Direction, false, word =>
            {
                if (game.TryWord(word, game.WordGroup, clock.ElapsedMilliseconds))
                {
                    status.Text = "";
                    Render();
                }
            });
        }

        private void Start()
        {
            command.Clear();
            chargeInput.Clear();
            game.Start(SelectedRole);
            clock.Restart();
            status.Text = "";
            Render();
            timer.Start();
            UpdateMusic(true);
        }

        private void Tick(object sender, EventArgs e)
        {
            long now = clock.ElapsedMilliseconds;
            if (!game.MoveDue(now))
                return;

            int lives = game.Lives;
            game.Step(now);
            if (game.MoveDue(now))
                game.ScheduleNext(now);
            if (game.Lives != lives && game.IsRunning)
            {
                command.Clear();
                status.Text = "已复活，剩余 1 条命。";
            }
            if (!game.IsRunning)
            {
                timer.Stop();
                clock.Stop();
                command.Clear();
                chargeInput.Clear();
                status.Text = (game.Result == SnakeResult.Won ? "棋盘已填满，通关！"
                    : game.Result == SnakeResult.Body ? "撞到蛇身。" : "撞到墙了。")
                    + "得分 " + game.Score + "。输入 start 再来一局。";
                Render();
                UpdateMusic(false);
                return;
            }

            Render();
        }

        private void Render()
        {
            boardSize.Text = game.Size + " × " + game.Size + " 格";
            var role = game.IsRunning ? game.Role : SelectedRole;
            ShowWords(game.IsRunning, game.Direction, game.Words, role);
            ShowRole(role, game.IsRunning, game.Lives, game.IsCharging(clock.ElapsedMilliseconds),
                0, game.ChargeWord);
            fruitLegend.Visibility = Visibility.Collapsed;
            scoreGuide.Text = "有效输入按字母计分 · 吃果 +10";
            board.SetBoard(game.Size, game.Fruit < 0 ? new int[0] : new[] { game.Fruit }, -1, 0, new[]
            {
                new SnakePlayerState { Slot = 0, Result = game.Result, Body = game.Body.ToArray(),
                    Charging = game.IsRunning && game.IsCharging(clock.ElapsedMilliseconds) }
            });
            score.Text = game.Score.ToString(CultureInfo.CurrentCulture);
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
                label.Foreground = opposite || !running ? Brushes.SlateGray : SnakeBoard.Head;
                label.ToolTip = current ? "当前方向" : opposite ? "禁止掉头" : null;
                ((Border)label.Parent).Background = current ? SnakeBoard.Current : Brushes.Transparent;
            }
        }

        private void ShowRole(SnakeRole role, bool running, int lives, bool charging, int stun, string special)
        {
            rolePicker.IsEnabled = !running && (online == null || online.Phase != MatchPhase.Playing);
            string guide;
            switch (role)
            {
                case SnakeRole.Timewalker: guide = "6 / 8 / 10 字母 · 额外走 0 / 2 / 4 格"; break;
                case SnakeRole.Timid: guide = "半速 · 3 字母 · " + (running ? lives.ToString() : "2") + " 条命"; break;
                case SnakeRole.Fearless: guide = "120% 移速 · Tab 切换冲锋输入框"; break;
                default: guide = ""; break;
            }
            if (stun > 0)
                guide += " · 僵直 " + (stun / 1000.0).ToString("0.0") + " 秒";
            else if (charging)
                guide += " · 冲锋中";
            roleGuide.Text = guide;
            chargePanel.Visibility = running && role == SnakeRole.Fearless && special != null
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
                lobby.Turn(word, online.ChargeGroup, online.Round, charge: true);
            }
            else if (game.TryCharge(word, game.ChargeGroup, clock.ElapsedMilliseconds))
            {
                chargeInput.Clear();
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
            timer.Stop();
            clock.Stop();
            game.Leave();
            command.Clear();
            chargeInput.Clear();
            UpdateMusic(false);
        }

        private void LeaveRoom()
        {
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
                    lobby.Start();
                }
                return;
            }

            var own = online.Players.First(player => player.Slot == ownSlot);
            if (own.Result != SnakeResult.Running)
                return;
            SubmitWord(online.Words, own.Role, own.Direction, own.StunRemaining > 0,
                word => lobby.Turn(word, online.WordGroup, online.Round));
        }

        private void ApplyRoomState(SnakeMatchState state, int slot)
        {
            var previous = online;
            ownSlot = slot;
            online = state;
            var own = state.Players.First(player => player.Slot == slot);
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
                if (running && own.Lives < previousOwn.Lives)
                {
                    command.Clear();
                    status.Text = "已复活，剩余 1 条命。";
                }
            }
            command.IsReadOnly = state.Phase == MatchPhase.Playing ? !running || own.StunRemaining > 0 : slot != 0;
            if (restoreCommandFocus && !command.IsKeyboardFocused)
                command.Focus();
            ShowWords(running, own.Direction, state.Words, own.Role);
            ShowRole(state.Phase == MatchPhase.Playing ? own.Role : SelectedRole, running, own.Lives,
                own.Charging, own.StunRemaining, state.ChargeWord);
            score.Text = own.Score.ToString(CultureInfo.CurrentCulture);
            int size = state.Size;
            boardSize.Text = size + " × " + size + " 格";
            fruitLegend.Visibility = Visibility.Visible;
            scoreGuide.Text = "有效输入按字母计分";
            board.SetBoard(size, state.SmallFruits, state.LargeFruit, slot, state.Players);
            UpdateMusic(running);
        }

        private void UpdateMusic(bool running)
        {
            if (musicRunning == running)
                return;
            musicRunning = running;
            gameRunningChanged(running);
        }
    }

    internal sealed class SnakeBoard : FrameworkElement
    {
        internal static readonly Brush Head = MakeBrush(26, 95, 68);
        internal static readonly Brush Current = MakeBrush(216, 237, 226);
        private static readonly Brush Board = MakeBrush(232, 243, 236);
        private static readonly Brush Body = MakeBrush(75, 151, 107);
        private static readonly Brush Fruit = MakeBrush(181, 69, 43);
        private static readonly Brush LargeFruit = MakeBrush(224, 148, 44);
        private static readonly Brush FruitHighlight = MakeBrush(255, 240, 183);
        private static readonly Brush ChargeGlow = MakeBrush(26, 154, 165);
        private static readonly Brush StunFill = MakeBrush(105, 117, 132);
        private static readonly Pen Grid = MakeGridPen();
        private static readonly Brush[] Rivals =
        {
            MakeBrush(49, 99, 184), MakeBrush(126, 75, 161), MakeBrush(151, 105, 26)
        };
        private int size = SnakeGame.BaseSize;
        private int[] smallFruits = new int[0];
        private int largeFruit = -1;
        private int ownSlot;
        private SnakePlayerState[] players = new SnakePlayerState[0];

        internal void SetBoard(int size, int[] smallFruits, int largeFruit, int ownSlot, SnakePlayerState[] players)
        {
            this.size = size;
            this.smallFruits = smallFruits;
            this.largeFruit = largeFruit;
            this.ownSlot = ownSlot;
            this.players = players;
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
            for (int i = 1; i < size; i++)
            {
                drawing.DrawLine(Grid, new Point(area.X + i * cell, area.Top), new Point(area.X + i * cell, area.Bottom));
                drawing.DrawLine(Grid, new Point(area.Left, area.Y + i * cell), new Point(area.Right, area.Y + i * cell));
            }

            foreach (var player in players)
            {
                Brush fill = player.Charging ? ChargeGlow :
                    player.StunRemaining > 0 ? StunFill : PlayerColor(player.Slot, ownSlot);
                for (int i = 0; i < player.Body.Length; i++)
                {
                    var bounds = CellBounds(player.Body[i], area, cell);
                    drawing.DrawRectangle(i == 0 && player.Slot == ownSlot && !player.Charging &&
                        player.StunRemaining == 0 ? Head : fill, null, bounds);
                    if (i == 0)
                    {
                        var center = new Point(bounds.X + cell / 2, bounds.Y + cell / 2);
                        drawing.DrawEllipse(Brushes.White, null, center, cell * 0.15, cell * 0.15);
                    }
                }
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
        }

        private Rect CellBounds(int position, Rect area, double cell)
        {
            return new Rect(area.X + position % size * cell, area.Y + position / size * cell, cell, cell);
        }

        internal static Brush PlayerColor(int slot, int ownSlot)
        {
            return slot == ownSlot ? Body : Rivals[slot < ownSlot ? slot : slot - 1];
        }

        private static Brush MakeBrush(byte red, byte green, byte blue)
        {
            var brush = new SolidColorBrush(Color.FromRgb(red, green, blue));
            brush.Freeze();
            return brush;
        }

        private static Pen MakeGridPen()
        {
            var pen = new Pen(MakeBrush(208, 226, 214), 0.65);
            pen.Freeze();
            return pen;
        }
    }
}
