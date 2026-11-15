using System;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;

namespace EnglishTypingPractice
{
    internal sealed class LanLobby
    {
        private readonly FrameworkElement panel;
        private readonly TextBox name;
        private readonly TextBox roomName;
        private readonly TextBox address;
        private readonly ListBox rooms;
        private readonly TextBlock notice;
        private readonly Button toggle;
        private readonly Button create;
        private readonly Button scan;
        private readonly Button join;
        private readonly Button leave;
        private readonly Button ready;
        private readonly TextBlock rules;
        private readonly TextBlock roster;
        private readonly TextBox command;
        private readonly TextBlock snakeStatus;
        private readonly FrameworkElement audioControls;
        private readonly FrameworkElement masterControl;
        private readonly ComboBox teamPicker;
        private readonly CheckBox uniformRaceRole;
        private readonly ComboBox botCount;
        private readonly ComboBox botDifficulty;
        private bool updatingBots;
        private static readonly SnakeBotDifficulty[] Difficulties =
            { SnakeBotDifficulty.Easy, SnakeBotDifficulty.Normal, SnakeBotDifficulty.Hard, SnakeBotDifficulty.Hell };
        private readonly TextBlock teamLabel;
        private readonly RoomMusic music;
        private readonly SnakeBoard board;
        private readonly Action entered;
        private readonly Action<SnakeMatchState, int> update;
        private readonly Action exited;
        private readonly Func<SnakeRole> selectedRole;
        private readonly Func<SnakeMode> selectedMode;
        private readonly PracticeSettings settings;
        private readonly Action saveSettings;
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        private LanHost host;
        private LanClient client;
        private SnakeMatchState latest;
        private string localAddress;
        private int action;
        private bool busy;
        private bool updatingTeams;
        private bool reconnecting;
        private string displayedNickname;

        internal LanLobby(Window window, Action entered, Action<SnakeMatchState, int> update, Action exited,
            Func<SnakeRole> selectedRole, Func<SnakeMode> selectedMode, PracticeSettings settings, Action saveSettings,
            SnakeBoard board)
        {
            this.entered = entered;
            this.update = update;
            this.exited = exited;
            this.selectedRole = selectedRole;
            this.selectedMode = selectedMode;
            this.settings = settings;
            this.saveSettings = saveSettings;
            this.board = board;
            panel = (FrameworkElement)window.FindName("LanPanel");
            name = (TextBox)window.FindName("LanName");
            RefreshNickname();
            roomName = (TextBox)window.FindName("LanRoomName");
            address = (TextBox)window.FindName("LanAddress");
            rooms = (ListBox)window.FindName("LanRooms");
            notice = (TextBlock)window.FindName("LanNotice");
            toggle = (Button)window.FindName("LanToggle");
            create = (Button)window.FindName("LanCreate");
            scan = (Button)window.FindName("LanScan");
            join = (Button)window.FindName("LanJoin");
            leave = (Button)window.FindName("LanLeave");
            ready = (Button)window.FindName("LanReady");
            rules = (TextBlock)window.FindName("LanRules");
            roster = (TextBlock)window.FindName("LanRoster");
            command = (TextBox)window.FindName("SnakeCommand");
            snakeStatus = (TextBlock)window.FindName("SnakeStatus");
            audioControls = (FrameworkElement)window.FindName("LanAudioControls");
            masterControl = (FrameworkElement)window.FindName("LanMasterControl");
            teamPicker = (ComboBox)window.FindName("LanTeamCount");
            botCount = (ComboBox)window.FindName("SnakeBotCount");
            botDifficulty = (ComboBox)window.FindName("SnakeBotDifficulty");
            botCount.SelectionChanged += (sender, e) => ConfigureBots();
            botDifficulty.SelectionChanged += (sender, e) => ConfigureBots();
            uniformRaceRole = (CheckBox)window.FindName("LanUniformRaceRole");
            uniformRaceRole.Click += (sender, e) =>
            {
                if (host != null)
                {
                    host.SetUniformRaceRole(uniformRaceRole.IsChecked == true);
                    RefreshRoom();
                }
            };
            teamLabel = (TextBlock)window.FindName("LanTeamsLabel");
            music = new RoomMusic((TextBlock)window.FindName("LanMusicStatus"), settings);
            BindVolume(window, "LanMaster", settings.RoomMasterVolume, value =>
            {
                settings.RoomMasterVolume = value;
                if (host != null)
                    host.SetMusicVolume(value);
            }, saveSettings);
            BindVolume(window, "LanMusic", settings.RoomMusicVolume, value =>
            {
                settings.RoomMusicVolume = value;
                music.UpdateVolume();
            }, saveSettings);
            BindVolume(window, "LanEffects", settings.RoomEffectsVolume, value =>
            {
                settings.RoomEffectsVolume = value;
                music.ResetEffects();
            }, saveSettings);
            teamPicker.SelectionChanged += (sender, e) =>
            {
                if (host == null || updatingTeams)
                    return;
                host.SetTeamCount(teamPicker.SelectedIndex + 2);
                RefreshRoom();
            };
            toggle.Click += (sender, e) =>
            {
                if (panel.Visibility == Visibility.Visible)
                    panel.Visibility = Visibility.Collapsed;
                else
                {
                    InteractionMotion.Reveal(panel, 12, settings.SnakeLowEffects);
                    RefreshNickname();
                    if (string.IsNullOrWhiteSpace(name.Text))
                        name.Focus();
                    else if (string.IsNullOrWhiteSpace(roomName.Text))
                        roomName.Focus();
                }
            };
            create.Click += (sender, e) => CreateRoom();
            scan.Click += async (sender, e) => await Scan();
            join.Click += async (sender, e) => await JoinRoom();
            leave.Click += (sender, e) =>
            {
                Close(true);
                panel.Visibility = Visibility.Visible;
                notice.Text = "已离开房间。";
            };
            ready.Click += (sender, e) =>
            {
                if (latest == null)
                    return;
                if (host != null && latest.Phase == MatchPhase.Finished)
                {
                    host.ReturnToLobby();
                    RefreshRoom();
                }
                else if (client != null && latest.Phase == MatchPhase.Lobby && !client.Reconnecting)
                    client.SetReady(!latest.ReadySlots.Contains(client.Slot));
            };
            rooms.SelectionChanged += (sender, e) => address.Clear();
            timer.Tick += (sender, e) => RefreshRoom();
            UpdateControls();
        }

        internal void Start()
        {
            if (host == null)
                return;
            string failure;
            host.Start(out failure);
            RefreshRoom();
            if (failure != null)
                snakeStatus.Text = failure;
        }

        private static void BindVolume(Window window, string name, int initial, Action<int> change, Action save)
        {
            var slider = (Slider)window.FindName(name + "Volume");
            var label = (TextBlock)window.FindName(name + "Level");
            slider.Value = initial;
            label.Text = initial + "%";
            slider.ValueChanged += (sender, e) =>
            {
                int value = (int)e.NewValue;
                label.Text = value + "%";
                change(value);
                save();
            };
        }

        internal void DropBall(int round)
        {
            if (host != null)
                host.DropBall(round);
            else if (client != null)
                client.DropBall(round);
        }

        internal void SetPreview(SnakeTypingPreview preview)
        {
            if (host != null)
                host.SetPreview(preview);
            else if (client != null)
                client.SetPreview(preview);
        }

        internal void SetSprint(int round, bool held)
        {
            if (host != null)
                host.SetSprint(round, held);
            else if (client != null)
                client.SetSprint(round, held);
        }

        internal void HidePanel()
        {
            panel.Visibility = Visibility.Collapsed;
        }

        internal bool InputPending
        {
            get { return client != null && (client.InputPending || client.Reconnecting || client.Disconnected || !ReferenceEquals(latest, client.State)); }
        }

        internal void Turn(string word, int group, int round, bool charge = false)
        {
            if (host != null)
            {
                host.Turn(word, group, round, charge);
                RefreshRoom();
            }
            else if (client != null)
            {
                client.Turn(word, group, round, charge);
            }
        }

        internal void SetRole(SnakeRole role)
        {
            if (host != null)
            {
                host.SetRole(role);
                RefreshRoom();
            }
            else if (client != null && !busy)
                client.SetRole(role);
        }

        internal void SetMode(SnakeMode mode)
        {
            if (host == null)
                return;
            host.SetMode(mode);
            RefreshRoom();
        }

        private async void CreateRoom()
        {
            RefreshNickname();
            string player = PracticeSettings.NormalizeLocalNickname(name.Text);
            string room = roomName.Text.Trim();
            if (!CheckName(player))
                return;
            if (!LanWire.IsName(room, 24))
            {
                notice.Text = "请输入房间名。";
                roomName.Focus();
                return;
            }

            try
            {
                host = new LanHost(room, player, role: selectedRole());
                host.SetMode(selectedMode());
                host.SetTeamCount(teamPicker.SelectedIndex + 2);
                host.SetBots(botCount.SelectedIndex, SelectedBotDifficulty);
            }
            catch (SocketException)
            {
                notice.Text = "无法创建房间。请确认本机没有另一个房间正在运行。";
                return;
            }
            try { localAddress = LanDiscovery.LocalAddresses().Select(ip => ip.ToString()).FirstOrDefault(); }
            catch (System.Net.NetworkInformation.NetworkInformationException) { }
            RememberNickname(player);
            OpenRoom();
            await music.Host(host);
        }

        private async Task Scan()
        {
            int version = ++action;
            SetBusy(true);
            notice.Text = "正在查找房间…";
            try
            {
                var found = await Task.Run(() => LanDiscovery.Scan());
                if (version != action)
                    return;
                rooms.ItemsSource = found;
                rooms.SelectedIndex = found.Length == 1 ? 0 : -1;
                notice.Text = found.Length == 0
                    ? "没找到房间。可以输入房主的 IPv4 地址加入。"
                    : "选择房间后点击「加入房间」。";
            }
            catch (Exception error)
            {
                if (!(error is SocketException || error is System.Net.NetworkInformation.NetworkInformationException))
                    throw;
                if (version == action)
                    notice.Text = "扫描失败。可以输入房主的 IPv4 地址加入。";
            }
            finally
            {
                if (version == action)
                    SetBusy(false);
            }
        }

        private async Task JoinRoom()
        {
            RefreshNickname();
            string player = PracticeSettings.NormalizeLocalNickname(name.Text);
            if (!CheckName(player))
                return;

            IPAddress target;
            var selected = rooms.SelectedItem as DiscoveredRoom;
            if (string.IsNullOrWhiteSpace(address.Text) && selected != null)
            {
                if (selected.Info.Version != LanWire.Version || selected.Info.AppVersion != LanWire.AppVersion)
                {
                    notice.Text = LanWire.VersionMismatch(LanWire.DisplayVersion(selected.Info.AppVersion, selected.Info.Version), LanWire.AppVersion);
                    return;
                }
                if (!selected.Info.Joinable)
                {
                    notice.Text = "房间已满或本局正在进行。请稍后重新扫描。";
                    return;
                }
                target = selected.Endpoint.Address;
            }
            else if (!IPAddress.TryParse(address.Text.Trim(), out target) || target.AddressFamily != AddressFamily.InterNetwork)
            {
                notice.Text = "请选择房间，或输入房主的 IPv4 地址。";
                return;
            }

            int version = ++action;
            var pending = new LanClient();
            client = pending;
            SetBusy(true);
            notice.Text = "正在加入房间…";
            string error = await pending.Join(target, player, role: selectedRole());
            if (version != action)
                return;
            if (error != null)
            {
                client = null;
                pending.Dispose();
                notice.Text = error;
                SetBusy(false);
                return;
            }
            RememberNickname(player);
            OpenRoom();
        }

        private bool CheckName(string player)
        {
            if (player != null)
                return true;
            notice.Text = "请输入 1–20 个字符的昵称。";
            name.Focus();
            return false;
        }

        private void RefreshNickname()
        {
            if (displayedNickname == settings.LocalNickname)
                return;
            displayedNickname = settings.LocalNickname;
            name.Text = displayedNickname ?? "";
        }

        private void RememberNickname(string nickname)
        {
            name.Text = displayedNickname = nickname;
            if (settings.LocalNickname == nickname)
                return;
            settings.LocalNickname = nickname;
            saveSettings();
        }

        private void OpenRoom()
        {
            entered();
            busy = false;
            panel.Visibility = Visibility.Collapsed;
            UpdateControls();
            timer.Start();
            RefreshRoom();
            command.Focus();
        }

        private void RefreshRoom()
        {
            if (host == null && client == null)
                return;
            if (client != null && client.Disconnected)
            {
                Close();
                panel.Visibility = Visibility.Visible;
                notice.Text = "与房主的连接已断开。";
                return;
            }
            bool lost = client != null && client.Reconnecting;
            if (lost != reconnecting)
            {
                reconnecting = lost;
                command.IsEnabled = !lost;
                ready.IsEnabled = !lost;
                snakeStatus.Text = lost ? "连接中断，正在尝试重连…" : "已重新连接房间。";
            }
            var state = host != null ? host.Snapshot() : client.State;
            if (ReferenceEquals(state, latest))
                return;
            int slot = host != null ? 0 : client.Slot;
            updatingBots = true;
            botCount.SelectedIndex = state.BotSlots.Length;
            botDifficulty.SelectedIndex = Array.IndexOf(Difficulties, state.BotDifficulty);
            botCount.IsEnabled = botDifficulty.IsEnabled = host != null && state.Phase == MatchPhase.Lobby;
            int available = 4 - (state.Players.Length - state.BotSlots.Length);
            for (int i = 0; i < botCount.Items.Count; i++)
                ((ComboBoxItem)botCount.Items[i]).IsEnabled = i <= available;
            updatingBots = false;
            bool teamMode = state.NextMode == SnakeMode.Teams;
            uniformRaceRole.Visibility = state.NextMode == SnakeMode.Race ? Visibility.Visible : Visibility.Collapsed;
            uniformRaceRole.IsEnabled = host != null && state.Phase != MatchPhase.Playing;
            uniformRaceRole.IsChecked = state.RaceUniformRole;
            teamPicker.Visibility = teamMode && host != null ? Visibility.Visible : Visibility.Collapsed;
            teamPicker.IsEnabled = state.Phase != MatchPhase.Playing;
            teamLabel.Visibility = teamMode ? Visibility.Visible : Visibility.Collapsed;
            teamLabel.Text = host != null ? "队伍数量" : "房主设置 " + state.NextTeamCount + " 队";
            updatingTeams = true;
            teamPicker.SelectedIndex = state.NextTeamCount - 2;
            updatingTeams = false;
            ready.Visibility = (host != null && state.Phase == MatchPhase.Finished ||
                client != null && state.Phase == MatchPhase.Lobby) ? Visibility.Visible : Visibility.Collapsed;
            ready.Content = host != null ? "返回准备" : state.ReadySlots.Contains(slot) ? "取消准备" : "准备";
            rules.Visibility = Visibility.Visible;
            rules.Text = "房主设置 · " + LanDiscovery.ModeName(state.NextMode) +
                (teamMode ? " · " + state.NextTeamCount + " 队 · 每局随机分队" :
                    state.NextMode == SnakeMode.Race && state.RaceUniformRole ? " · 统一职业" : "") +
                (state.BotSlots.Length == 0 ? "" : " · 人机 " + state.BotSlots.Length + "（" +
                    ((ComboBoxItem)botDifficulty.SelectedItem).Content + "）") + " · " + LanWire.AppVersion;
            if (latest == null || latest.Phase != state.Phase || latest.Round != state.Round || latest.Mode != state.Mode ||
                !latest.Players.Select(PlayerLine).SequenceEqual(state.Players.Select(PlayerLine)) ||
                !latest.ReadySlots.SequenceEqual(state.ReadySlots) || !latest.ReconnectingSlots.SequenceEqual(state.ReconnectingSlots) ||
                !latest.BotSlots.SequenceEqual(state.BotSlots) ||
                !latest.RelayTeams.Select(RelayLine).SequenceEqual(state.RelayTeams.Select(RelayLine)) ||
                !latest.TeamScores.SequenceEqual(state.TeamScores))
                RenderRoster(state, slot);
            music.Refresh(state, latest, client, slot);
            latest = state;
            update(state, slot);
        }

        private void RenderRoster(SnakeMatchState state, int slot)
        {
            if (state.Phase != MatchPhase.Lobby && state.Mode == SnakeMode.Relay)
            {
                var teams = state.RelayTeams;
                int winner = state.RelayWinner;
                roster.Text = state.Phase == MatchPhase.Playing ? state.Room :
                    winner < 0 ? "词牌接力 · 并列" : SnakeBoard.TeamName(winner) + "获胜";
                for (int i = 0; i < teams.Length; i++)
                {
                    var team = teams[i];
                    roster.Text += "\n" + SnakeBoard.TeamName(i) + " · " + team.Legs + "/4 棒 · " +
                        string.Join("、", state.Players.Where(player => player.Team == i).Select(player =>
                            PlayerLabel(player, slot) + (state.BotSlots.Contains(player.Slot) ? "（人机）" : "")));
                    var handoff = state.Players.FirstOrDefault(player => player.Slot == team.WaitingSlot);
                    if (handoff != null && state.Phase == MatchPhase.Playing)
                        roster.Text += " · 等待 " + handoff.Name + " 接棒";
                }
                return;
            }
            if (state.Phase != MatchPhase.Lobby && state.Mode == SnakeMode.Pulse)
            {
                var players = state.Phase == MatchPhase.Finished ? state.Rankings : state.Players;
                roster.Text = (state.Phase == MatchPhase.Finished
                    ? (state.PulseWon ? "脉冲回廊共同通关" : "脉冲回廊挑战失败") + " · 合作记录"
                    : state.Room + " · 第 " + state.PulseWave + "/12 波 · 复活储备 " + state.PulseLives) +
                    "\n" + string.Join("\n", players.Select(player => PlayerLabel(player, slot) +
                        " · 通过 " + player.PulseSurvived + " 波 · 死亡 " + player.PulseDeaths +
                        " 次 · 转向 " + player.Turns + " 次" +
                        (state.ReconnectingSlots.Contains(player.Slot) ? " · 重连中" :
                            player.RespawnRemaining > 0 ? " · 等待复活" :
                            player.Result == SnakeResult.Running || state.Phase == MatchPhase.Finished ? "" : " · 已出局")));
                return;
            }
            if (state.Phase != MatchPhase.Lobby && (state.Mode == SnakeMode.Race || state.Mode == SnakeMode.Power))
            {
                var players = state.Phase == MatchPhase.Finished ? state.Rankings : state.Players;
                roster.Text = (state.Phase != MatchPhase.Finished ? state.Room : state.Mode == SnakeMode.Race ? "竞速排名" :
                    state.PowerWon ? "四区供电成功" : "供电未完成") + "\n" + string.Join("\n", players.Select((player, i) =>
                        (state.Mode == SnakeMode.Race && state.Phase == MatchPhase.Finished ?
                            (1 + players.Take(i).Count(other => RaceAhead(other, player))) + ". " : "") +
                        PlayerLabel(player, slot) + " · " + (state.Mode == SnakeMode.Race ?
                            SnakePractice.RaceResult(player) : "充能 " + player.PowerContribution + " 格") +
                        (state.ReconnectingSlots.Contains(player.Slot) ? " · 重连中" : "")));
                return;
            }
            if (state.TeamCount > 0)
            {
                RenderTeams(state, slot);
                return;
            }
            bool territory = state.Mode == SnakeMode.Territory;
            string unit = territory ? " 格" : " 分";
            if (state.Phase == MatchPhase.Finished)
            {
                var ranked = state.Rankings;
                roster.Text = (territory && ranked.Length > 0 ? SnakePractice.TerritoryResult(state) + "\n" : "") +
                    "本局排行榜\n" + string.Join("\n", ranked.Select(player =>
                    (1 + ranked.Count(other => other.Score > player.Score)) + ". " + player.Name +
                    "  " + player.Score + unit));
                return;
            }
            string title = state.Room + (state.Phase == MatchPhase.Lobby ? " · " + state.Players.Length + "/4" : "");
            string hostAddress = host != null && state.Phase == MatchPhase.Lobby && localAddress != null
                ? " · " + localAddress : "";
            roster.Text = title + hostAddress;
            foreach (var player in state.Players)
            {
                roster.Inlines.Add(new System.Windows.Documents.LineBreak());
                var dot = new System.Windows.Documents.Run("● ")
                    { Foreground = board.PlayerColor(player.Slot, slot, territory) };
                roster.Inlines.Add(dot);
                roster.Inlines.Add(new System.Windows.Documents.Run(PlayerLabel(player, slot) + (state.BotSlots.Contains(player.Slot) ? "（人机）" : "") +
                    " · " + SnakeWords.RoleName(player.Role) +
                    (state.Phase == MatchPhase.Playing ? "  " + player.Score + unit : "") +
                    (state.ReconnectingSlots.Contains(player.Slot) ? " · 重连中" :
                        state.Phase == MatchPhase.Lobby && player.Slot != 0 ?
                            (state.ReadySlots.Contains(player.Slot) ? " · 已准备" : " · 等待准备") :
                        player.Result == SnakeResult.Left ? " · 已离开" :
                        state.Phase == MatchPhase.Playing && player.Result != SnakeResult.Running ? " · 出局" : "")));
            }
        }

        private static bool RaceAhead(SnakePlayerState other, SnakePlayerState player)
        {
            if ((other.Result == SnakeResult.Left) != (player.Result == SnakeResult.Left))
                return player.Result == SnakeResult.Left;
            if (other.RaceFinishMs >= 0 && player.RaceFinishMs >= 0)
                return other.RaceFinishMs < player.RaceFinishMs;
            return other.RaceProgress > player.RaceProgress ||
                (other.RaceProgress == player.RaceProgress && other.RaceCheckpointMs < player.RaceCheckpointMs);
        }

        private void RenderTeams(SnakeMatchState state, int slot)
        {
            int best = state.TeamScores.Max();
            var winners = Enumerable.Range(0, state.TeamCount).Where(team => state.TeamScores[team] == best).ToArray();
            roster.Text = state.Phase != MatchPhase.Finished ? state.Room :
                winners.Length == 1 ? SnakeBoard.TeamName(winners[0]) + "获胜" :
                "并列第一 · " + string.Join("、", winners.Select(SnakeBoard.TeamName));
            for (int team = 0; team < state.TeamCount; team++)
            {
                roster.Inlines.Add(new System.Windows.Documents.LineBreak());
                roster.Inlines.Add(new System.Windows.Documents.Run("● " + SnakeBoard.TeamName(team))
                    { Foreground = board.PlayerColor(team, 0, true) });
                roster.Inlines.Add(" " + state.TeamScores[team] + " 球");
                foreach (var player in state.Players.Where(player => player.Team == team))
                    roster.Inlines.Add(" · " + PlayerLabel(player, slot) +
                        (state.ReconnectingSlots.Contains(player.Slot) ? "（重连中）" :
                            player.Result == SnakeResult.Left ? "（离开）" : player.Carrying > 0 ? " ＋" + player.Carrying : ""));
            }
        }

        private static string PlayerLine(SnakePlayerState player)
        {
            return player.Slot + " " + player.Name + " " + player.Score + " " + player.Result + " " + player.Role +
                " " + player.Team + " " + player.Carrying + " " + player.RaceProgress + " " + player.RaceFinishMs +
                " " + player.RacePenaltyMs + " " + player.PowerContribution;
        }

        private static Tuple<int, int, int> RelayLine(SnakeRelayTeamState team)
        {
            return Tuple.Create(team.Legs, team.WaitingSlot, team.RunnerSlot);
        }

        private static string PlayerLabel(SnakePlayerState player, int slot)
        {
            return player.Name + (player.Slot == slot ? "（你）" : player.Slot == 0 ? "（房主）" : "");
        }

        internal void Close(bool intentional = true)
        {
            botCount.IsEnabled = botDifficulty.IsEnabled = true;
            foreach (ComboBoxItem item in botCount.Items)
                item.IsEnabled = true;
            uniformRaceRole.Visibility = Visibility.Collapsed;
            action++;
            timer.Stop();
            music.Close();
            if (host != null)
                host.Dispose();
            if (client != null)
            {
                if (intentional && !busy && !client.Reconnecting && !client.Disconnected)
                    client.Leave();
                else
                    client.Dispose();
            }
            host = null;
            client = null;
            latest = null;
            localAddress = null;
            busy = false;
            reconnecting = false;
            panel.Visibility = Visibility.Collapsed;
            UpdateControls();
            exited();
        }

        internal SnakeBotDifficulty SelectedBotDifficulty
        {
            get { return Difficulties[Math.Max(0, Math.Min(Difficulties.Length - 1, botDifficulty.SelectedIndex))]; }
        }

        private void ConfigureBots()
        {
            if (updatingBots || host == null || botCount.SelectedIndex < 0 || botDifficulty.SelectedIndex < 0)
                return;
            host.SetBots(botCount.SelectedIndex, SelectedBotDifficulty);
            RefreshRoom();
        }

        private void SetBusy(bool value)
        {
            busy = value;
            UpdateControls();
        }

        private void UpdateControls()
        {
            bool connected = host != null || (client != null && !busy);
            toggle.IsEnabled = !connected && !busy;
            toggle.Visibility = connected ? Visibility.Collapsed : Visibility.Visible;
            create.IsEnabled = scan.IsEnabled = join.IsEnabled = !connected && !busy;
            command.IsEnabled = !busy;
            leave.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            roster.Visibility = connected ? Visibility.Visible : Visibility.Collapsed;
            audioControls.Visibility = roster.Visibility;
            masterControl.Visibility = host == null ? Visibility.Collapsed : Visibility.Visible;
            if (!connected)
            {
                teamLabel.Visibility = teamPicker.Visibility = Visibility.Collapsed;
                ready.Visibility = rules.Visibility = Visibility.Collapsed;
            }
        }
    }
}
