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
        private readonly TextBlock roster;
        private readonly TextBox command;
        private readonly Action entered;
        private readonly Action<SnakeMatchState, int> update;
        private readonly Action exited;
        private readonly Func<SnakeRole> selectedRole;
        private readonly Func<SnakeMode> selectedMode;
        private readonly DispatcherTimer timer = new DispatcherTimer { Interval = TimeSpan.FromMilliseconds(45) };
        private LanHost host;
        private LanClient client;
        private SnakeMatchState latest;
        private string localAddress;
        private int action;
        private bool busy;

        internal LanLobby(Window window, Action entered, Action<SnakeMatchState, int> update, Action exited,
            Func<SnakeRole> selectedRole, Func<SnakeMode> selectedMode)
        {
            this.entered = entered;
            this.update = update;
            this.exited = exited;
            this.selectedRole = selectedRole;
            this.selectedMode = selectedMode;
            panel = (FrameworkElement)window.FindName("LanPanel");
            name = (TextBox)window.FindName("LanName");
            roomName = (TextBox)window.FindName("LanRoomName");
            address = (TextBox)window.FindName("LanAddress");
            rooms = (ListBox)window.FindName("LanRooms");
            notice = (TextBlock)window.FindName("LanNotice");
            toggle = (Button)window.FindName("LanToggle");
            create = (Button)window.FindName("LanCreate");
            scan = (Button)window.FindName("LanScan");
            join = (Button)window.FindName("LanJoin");
            leave = (Button)window.FindName("LanLeave");
            roster = (TextBlock)window.FindName("LanRoster");
            command = (TextBox)window.FindName("SnakeCommand");
            toggle.Click += (sender, e) =>
            {
                panel.Visibility = panel.Visibility == Visibility.Visible ? Visibility.Collapsed : Visibility.Visible;
                name.Focus();
            };
            create.Click += (sender, e) => CreateRoom();
            scan.Click += async (sender, e) => await Scan();
            join.Click += async (sender, e) => await JoinRoom();
            leave.Click += (sender, e) =>
            {
                Close();
                panel.Visibility = Visibility.Visible;
                notice.Text = "已离开房间。";
            };
            rooms.SelectionChanged += (sender, e) => address.Clear();
            timer.Tick += (sender, e) => RefreshRoom();
            UpdateControls();
        }

        internal void Start()
        {
            if (host == null)
                return;
            host.Start();
            RefreshRoom();
        }

        internal void HidePanel()
        {
            panel.Visibility = Visibility.Collapsed;
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

        private void CreateRoom()
        {
            string player = name.Text.Trim();
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
            }
            catch (SocketException)
            {
                notice.Text = "无法创建房间。请确认本机没有另一个房间正在运行。";
                return;
            }
            try { localAddress = LanDiscovery.LocalAddresses().Select(ip => ip.ToString()).FirstOrDefault(); }
            catch (System.Net.NetworkInformation.NetworkInformationException) { }
            OpenRoom();
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
            string player = name.Text.Trim();
            if (!CheckName(player))
                return;

            IPAddress target;
            var selected = rooms.SelectedItem as DiscoveredRoom;
            if (string.IsNullOrWhiteSpace(address.Text) && selected != null)
            {
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
            OpenRoom();
        }

        private bool CheckName(string player)
        {
            if (LanWire.IsName(player, 20))
                return true;
            notice.Text = "请输入昵称。";
            name.Focus();
            return false;
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
            var state = host != null ? host.Snapshot() : client.State;
            if (ReferenceEquals(state, latest))
                return;
            int slot = host != null ? 0 : client.Slot;
            if (latest == null || latest.Phase != state.Phase || latest.Round != state.Round || latest.Mode != state.Mode ||
                !latest.Players.Select(PlayerLine).SequenceEqual(state.Players.Select(PlayerLine)))
                RenderRoster(state, slot);
            latest = state;
            update(state, slot);
        }

        private void RenderRoster(SnakeMatchState state, int slot)
        {
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
                    { Foreground = SnakeBoard.PlayerColor(player.Slot, slot, territory) };
                roster.Inlines.Add(dot);
                roster.Inlines.Add(new System.Windows.Documents.Run(PlayerLabel(player, slot) +
                    " · " + SnakeWords.RoleName(player.Role) +
                    (state.Phase == MatchPhase.Playing ? "  " + player.Score + unit : "") +
                    (player.Result == SnakeResult.Left ? " · 已离开" :
                        state.Phase == MatchPhase.Playing && player.Result != SnakeResult.Running ? " · 出局" : "")));
            }
        }

        private static string PlayerLine(SnakePlayerState player)
        {
            return player.Slot + " " + player.Name + " " + player.Score + " " + player.Result + " " + player.Role;
        }

        private static string PlayerLabel(SnakePlayerState player, int slot)
        {
            return player.Name + (player.Slot == slot ? "（你）" : player.Slot == 0 ? "（房主）" : "");
        }

        internal void Close()
        {
            action++;
            timer.Stop();
            if (host != null)
                host.Dispose();
            if (client != null)
                client.Dispose();
            host = null;
            client = null;
            latest = null;
            localAddress = null;
            busy = false;
            panel.Visibility = Visibility.Collapsed;
            UpdateControls();
            exited();
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
        }
    }
}
