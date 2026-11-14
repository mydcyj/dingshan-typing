using System;
using System.Windows;
using System.Windows.Controls;

namespace EnglishTypingPractice
{
    internal sealed class SnakeTutorial
    {
        private enum Step { Idle, Turn, Camera, Hold, Release, Clear, Role, Complete }

        private readonly FrameworkElement banner;
        private readonly TextBlock instruction;
        private readonly TextBlock rulesNotice;
        private readonly Button exit;
        private readonly Action prepareFruit;
        private readonly Action<bool> allowClock;
        private Step step;
        private SnakeRole role;
        private bool moving;
        private bool locked;
        private bool completeAfterMove;
        private long skillEnds;

        internal SnakeTutorial(Window window, Func<SnakeRole, bool> start, Action stop,
            Action prepareFruit, Action<bool> allowClock)
        {
            this.prepareFruit = prepareFruit;
            this.allowClock = allowClock;
            banner = (FrameworkElement)window.FindName("SnakeTutorialBanner");
            instruction = (TextBlock)window.FindName("SnakeTutorialInstruction");
            rulesNotice = (TextBlock)window.FindName("SnakeTutorialNotice");
            exit = (Button)window.FindName("ExitSnakeTutorial");
            var startButton = (Button)window.FindName("StartSnakeTutorial");
            var roles = (ComboBox)window.FindName("SnakeTutorialRole");
            var openRules = (Button)window.FindName("OpenSnakeRules");
            var closeRules = (Button)window.FindName("CloseSnakeRules");
            foreach (SnakeRole choice in Enum.GetValues(typeof(SnakeRole)))
                roles.Items.Add(SnakeWords.RoleName(choice));
            roles.SelectedIndex = 0;
            startButton.Click += (sender, e) =>
            {
                rulesNotice.Text = "";
                role = (SnakeRole)roles.SelectedIndex;
                SetStep(Step.Turn);
                closeRules.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                if (start(role))
                    return;
                Cancel();
                openRules.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
                rulesNotice.Text = "请先退出局域网房间，再开始单人引导。";
            };
            exit.Click += (sender, e) =>
            {
                Cancel();
                stop();
            };
            openRules.Click += (sender, e) =>
            {
                rulesNotice.Text = "";
                if (!Active)
                    return;
                Cancel();
                stop();
            };
        }

        internal bool Active { get { return step != Step.Idle; } }
        internal bool CanMove { get { return moving; } }
        internal bool AcceptsDirection { get { return !moving && (step == Step.Turn || step == Step.Role); } }
        internal bool AcceptsSkill { get { return !moving && step == Step.Role && SnakeWords.HasSkill(role); } }

        internal void Cancel()
        {
            if (Active)
                SetStep(Step.Idle);
        }

        internal void TurnAccepted(bool turned, int extraMoves, int letters)
        {
            if (step == Step.Turn)
            {
                completeAfterMove = turned;
                SetMoving(true);
            }
            else if (step == Step.Role && !moving)
            {
                completeAfterMove = role == SnakeRole.Timewalker ? extraMoves > 0 :
                    role == SnakeRole.Navigator ? letters == SnakeWords.NavigatorShortLengthFor(SnakeMode.Classic) :
                    (role == SnakeRole.Standard || role == SnakeRole.Timid) && turned;
                SetMoving(true);
            }
        }

        internal void CameraChanged(bool cameraLocked)
        {
            locked = cameraLocked;
            if (step == Step.Camera && locked)
                SetStep(Step.Hold);
            else if ((step == Step.Hold || step == Step.Release) && !locked)
                SetStep(Step.Camera);
            else if (step == Step.Camera)
                ShowInstruction();
        }

        internal void LookChanged(bool held)
        {
            if (step == Step.Hold && held)
                SetStep(Step.Release);
            else if (step == Step.Release && !held)
                SetStep(Step.Clear);
        }

        internal void InputCleared(bool hadText)
        {
            if (step != Step.Clear || !hadText)
                return;
            SetStep(Step.Role);
            if (role == SnakeRole.Collector)
            {
                prepareFruit();
                SetMoving(true);
            }
        }

        internal void SkillAccepted(long elapsed)
        {
            if (!AcceptsSkill)
                return;
            skillEnds = elapsed + (role == SnakeRole.Bard ? 420 : role == SnakeRole.Stabilizer
                ? SnakeGame.StabilizerDurationFor(SnakeMode.Classic) : 1000);
            instruction.Text = "技能已发动。看一看它在棋盘上的效果。";
            SetMoving(true);
        }

        internal void FruitCollected()
        {
            if (step == Step.Role && role == SnakeRole.Collector)
                SetStep(Step.Complete);
        }

        internal void AfterTick(long elapsed, bool moved)
        {
            if (!moving)
                return;
            if (skillEnds > 0)
            {
                if (elapsed >= skillEnds)
                    SetStep(Step.Complete);
                return;
            }
            if (!moved)
                return;
            if (step == Step.Turn)
            {
                if (completeAfterMove)
                    SetStep(Step.Camera);
                else
                    SetMoving(false);
            }
            else if (completeAfterMove)
                SetStep(Step.Complete);
            else
                SetMoving(false);
        }

        private void SetStep(Step value)
        {
            step = value;
            completeAfterMove = false;
            skillEnds = 0;
            SetMoving(false);
            banner.Visibility = Active ? Visibility.Visible : Visibility.Collapsed;
            exit.Content = step == Step.Complete ? "结束引导" : "退出引导";
            ShowInstruction();
        }

        private void SetMoving(bool value)
        {
            if (moving == value)
                return;
            moving = value;
            allowClock(value);
        }

        private void ShowInstruction()
        {
            switch (step)
            {
                case Step.Turn: instruction.Text = "先看方向词。照着左或右方向的英文词输入，完成第一次转向。"; break;
                case Step.Camera: instruction.Text = locked ? "按 Alt 切回自由视角，再按一次锁定。" : "按 Alt 切换到锁定视角。"; break;
                case Step.Hold: instruction.Text = "按住任意方向键，观察小地图上的视野框。"; break;
                case Step.Release: instruction.Text = "松开方向键，镜头会回到蛇头。"; break;
                case Step.Clear: instruction.Text = "在方向词输入框随便输入一两个字母，然后按空格清空。"; break;
                case Step.Role: instruction.Text = RoleInstruction(); break;
                case Step.Complete: instruction.Text = "引导完成。结束引导后，输入 start 开始一局。"; break;
            }
        }

        private string RoleInstruction()
        {
            switch (role)
            {
                case SnakeRole.Timewalker: return "时空者练习　打完一个 8 或 10 字母的可用方向词，试试额外位移。";
                case SnakeRole.Timid: return "胆怯者练习　试试用 5–6 字母的词再转一次弯。";
                case SnakeRole.Navigator: return "领航者练习　继续打词转向，直到出现并打完一个 4 字母短词。";
                case SnakeRole.Collector: return "采集者练习　观察蛇吃下前方的小果，得分增加 14 分。";
                default: return SnakeWords.HasSkill(role)
                    ? SnakeWords.RoleName(role) + "练习　按 Tab 切换到技能框，照着提示打完技能词。"
                    : "平凡者练习　打出另一个可用方向词，完成一次连续转向。";
            }
        }
    }
}
