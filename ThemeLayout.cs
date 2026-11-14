using System;
using System.Linq;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Shapes;

namespace EnglishTypingPractice
{
    // Each composition arranges the original controls, so events, names and saved data stay shared.
    internal sealed class ThemeLayout
    {
        private readonly Window window;
        private readonly UiTheme theme;
        private readonly Grid desk;
        private readonly Grid hero;
        private readonly WrapPanel options;
        private readonly WrapPanel actions = new WrapPanel();
        private readonly FrameworkElement sheet, statistics, ranking, music, notice;
        private readonly StackPanel brand;
        private readonly UniformGrid themeChoices;
        private readonly Grid settingsLayout = new Grid();
        private readonly FrameworkElement settingsHeader, settingsPanel, settingsFooter;
        private bool? wide;

        internal static void Attach(Window window, UiTheme theme)
        {
            new ThemeLayout(window, theme);
        }

        private ThemeLayout(Window window, UiTheme theme)
        {
            this.window = window;
            this.theme = theme;
            desk = Find<Grid>("PracticeWorkbench");
            hero = Find<Grid>("PracticeHero");
            options = Find<WrapPanel>("PracticeOptions");
            Find<ComboBox>("PracticeKind").Width = 140;
            brand = Find<StackPanel>("PracticeBrand");
            sheet = Find<FrameworkElement>("PracticeSheet");
            statistics = Find<FrameworkElement>("PracticeStatistics");
            ranking = Find<FrameworkElement>("PracticeRanking");
            music = Find<FrameworkElement>("PracticeMusic");
            notice = Find<FrameworkElement>("SettingsNotice");
            Panel.SetZIndex(brand, 3);
            foreach (string name in new[] { "OpenSnake", "OpenSettings", "NewPassage", "Retry", "AutoPlay" })
            {
                var button = Find<Button>(name);
                Detach(button);
                button.ClearValue(Grid.RowProperty);
                button.ClearValue(Grid.ColumnProperty);
                button.ClearValue(Grid.RowSpanProperty);
                button.MinHeight = 38;
                button.Margin = new Thickness(4);
                button.VerticalAlignment = VerticalAlignment.Center;
                button.HorizontalAlignment = HorizontalAlignment.Stretch;
                actions.Children.Add(button);
            }
            Find<Button>("OpenSnake").Content = "贪吃蛇";
            Find<Button>("OpenSettings").Content = "外观设置";
            if (theme == UiTheme.Modern)
            {
                // Keep the established modern header navigation within reach above long passages.
                foreach (string name in new[] { "OpenSnake", "OpenSettings" })
                {
                    var button = Find<Button>(name);
                    actions.Children.Remove(button);
                    hero.Children.Add(button);
                    Grid.SetColumn(button, 1);
                    Grid.SetRowSpan(button, 2);
                    Panel.SetZIndex(button, 3);
                    button.HorizontalAlignment = HorizontalAlignment.Right;
                    button.VerticalAlignment = name == "OpenSnake" ? VerticalAlignment.Bottom : VerticalAlignment.Top;
                    button.Margin = new Thickness(8, 13, 17, 13);
                }
            }
            var stack = Find<StackPanel>("PracticeStack");
            foreach (FrameworkElement element in new[] { hero, options, sheet, statistics, ranking, music, notice })
                Detach(element);
            desk.Children.Clear();
            stack.Children.Clear();
            stack.Children.Add(desk);
            foreach (FrameworkElement element in new[] { hero, options, actions, sheet, statistics, ranking, music, notice })
                desk.Children.Add(element);
            stack.Margin = new Thickness(24, 20, 24, 28);
            stack.MaxWidth = theme == UiTheme.Minimal ? 960 : 1390;
            if (theme == UiTheme.Minimal || theme == UiTheme.ChineseStyle)
                Find<FrameworkElement>("BackgroundGrid").Visibility = Visibility.Collapsed;
            foreach (Border panel in new[] { sheet, statistics, ranking, music }.Cast<Border>())
                Frame(panel);

            // Settings is a gallery of miniature compositions, not a list of color swatches.
            var settingsPage = Find<ScrollViewer>("SettingsPage");
            var settingsStack = (StackPanel)settingsPage.Content;
            settingsHeader = (FrameworkElement)settingsStack.Children[0];
            settingsPanel = (FrameworkElement)settingsStack.Children[1];
            settingsFooter = (FrameworkElement)settingsStack.Children[2];
            themeChoices = Descendants(settingsPanel).OfType<UniformGrid>().First();
            settingsStack.Children.Clear();
            settingsStack.MaxWidth = 1120;
            settingsStack.Children.Add(settingsLayout);
            settingsLayout.Children.Add(settingsHeader);
            settingsLayout.Children.Add(settingsPanel);
            settingsLayout.Children.Add(settingsFooter);
            Frame((Border)settingsPanel);
            BuildThemeGallery();
            DecorateOtherPages();
            window.SizeChanged += (sender, e) => Reflow();
            window.Loaded += (sender, e) => Reflow();
            Reflow();
        }

        private void Reflow()
        {
            bool spacious = window.ActualWidth > 0 ? window.ActualWidth >= 1000 : window.Width >= 1000;
            if (wide == spacious) return;
            wide = spacious;
            desk.ColumnDefinitions.Clear();
            desk.RowDefinitions.Clear();
            foreach (FrameworkElement child in desk.Children)
            {
                Grid.SetColumnSpan(child, 1);
                Grid.SetRowSpan(child, 1);
                child.Margin = new Thickness(6);
                child.HorizontalAlignment = HorizontalAlignment.Stretch;
                child.VerticalAlignment = VerticalAlignment.Top;
            }
            for (int row = 0; row < 9; row++) desk.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            actions.Orientation = Orientation.Horizontal;
            actions.HorizontalAlignment = HorizontalAlignment.Left;
            hero.MinHeight = theme == UiTheme.Minimal ? 88 : 146;
            hero.MaxHeight = double.PositiveInfinity;
            hero.RowDefinitions[1].Height = new GridLength(1, GridUnitType.Star);
            brand.Width = double.NaN;
            brand.Margin = new Thickness(22, 18, 10, 18);
            brand.HorizontalAlignment = HorizontalAlignment.Left;
            var display = brand.Children.OfType<TextBlock>().First(t => t.Text.Contains("鼎山"));
            display.Text = "鼎山打字通";
            display.FontSize = theme == UiTheme.Minimal ? 29 : 38;
            var hint = Find<TextBlock>("PracticeHint");
            hint.MaxWidth = 390;
            foreach (Button button in actions.Children)
            {
                button.Width = double.NaN;
                button.Margin = new Thickness(4);
                button.RenderTransform = Transform.Identity;
            }

            if (!spacious)
            {
                Columns(desk, -1);
                var sequence = theme == UiTheme.Minimal ? new FrameworkElement[] { hero, options, sheet, actions, statistics, ranking, music, notice }
                    : theme == UiTheme.Steampunk ? new FrameworkElement[] { hero, actions, statistics, options, sheet, ranking, music, notice }
                    : theme == UiTheme.Vaporwave ? new FrameworkElement[] { hero, options, sheet, statistics, actions, music, ranking, notice }
                    : new FrameworkElement[] { hero, actions, options, sheet, statistics, ranking, music, notice };
                for (int i = 0; i < sequence.Length; i++) Place(sequence[i], i, 0);
                brand.Margin = new Thickness(16, 12, 12, 12);
            }
            else
            {
                switch (theme)
                {
                    case UiTheme.Minimal:
                        Columns(desk, -1, -1);
                        Place(hero, 0, 0, 2); Place(actions, 1, 0, 2); Place(options, 2, 0, 2);
                        Place(sheet, 3, 0, 2); Place(statistics, 4, 0, 2);
                        Place(ranking, 5, 0); Place(music, 5, 1);
                        break;
                    case UiTheme.Cyberpunk:
                        Columns(desk, 158, -1, 306);
                        Place(hero, 0, 0, 3); Place(actions, 1, 0, 1, 4); Rail(144, false);
                        Place(options, 1, 1, 2); Place(sheet, 2, 1);
                        Place(statistics, 2, 2); Place(ranking, 2, 2); ranking.Margin = new Thickness(6, 96, 6, 6);
                        Place(music, 3, 1, 2);
                        break;
                    case UiTheme.Steampunk:
                        Columns(desk, 160, -1, 308);
                        Place(hero, 0, 0, 3); Place(actions, 1, 0, 1, 4); Rail(146, false);
                        Place(statistics, 1, 1, 2); Place(options, 2, 1, 2);
                        Place(sheet, 3, 1, 1, 2); Place(ranking, 3, 2); Place(music, 4, 2);
                        break;
                    case UiTheme.Vaporwave:
                        Columns(desk, -1, 338);
                        Place(hero, 0, 0); Place(statistics, 0, 1);
                        Place(options, 1, 0, 2); Place(sheet, 2, 0, 1, 2);
                        Place(ranking, 2, 1); Place(actions, 3, 1); actions.MaxWidth = 310;
                        Place(music, 4, 0, 2);
                        hero.Margin = new Thickness(6, 6, 24, 6);
                        statistics.Margin = new Thickness(6, 35, 6, 6);
                        ranking.Margin = new Thickness(6, 22, 6, 6);
                        break;
                    case UiTheme.Constructivist:
                        Columns(desk, 190, -1, 312);
                        Place(hero, 0, 0, 3); Place(actions, 1, 0, 1, 4); Rail(166, false);
                        Place(statistics, 1, 1, 2); Place(options, 2, 1, 2);
                        Place(sheet, 3, 1, 2); Place(ranking, 4, 1); Place(music, 4, 2);
                        hero.Margin = new Thickness(42, 6, 6, 6);
                        actions.Margin = new Thickness(8, 22, 6, 6);
                        break;
                    case UiTheme.Persona5:
                        Columns(desk, 187, -1, 302);
                        Place(actions, 0, 0, 1, 5); Rail(156, true);
                        Place(hero, 0, 1, 2); Place(options, 1, 1, 2);
                        Place(sheet, 2, 1); Place(statistics, 2, 2);
                        Place(ranking, 2, 2); ranking.Margin = new Thickness(6, 98, 6, 6);
                        Place(music, 3, 1, 2);
                        hero.Margin = new Thickness(20, 6, 6, 6);
                        actions.Margin = new Thickness(0, 45, 0, 0);
                        break;
                    case UiTheme.ChineseStyle:
                        Columns(desk, 286, -1, 174);
                        Place(hero, 0, 2); Place(actions, 1, 2, 1, 3); Rail(150, false);
                        Place(statistics, 0, 0); Place(ranking, 0, 0); ranking.Margin = new Thickness(6, 96, 6, 6);
                        Place(sheet, 0, 1, 1, 2); Place(options, 2, 0, 2); Place(music, 3, 0, 2);
                        display.Text = "鼎山\n打字通";
                        display.FontSize = 34;
                        hero.MinHeight = 242;
                        brand.Width = 132;
                        hint.MaxWidth = 126;
                        brand.Margin = new Thickness(16, 25, 12, 25);
                        brand.VerticalAlignment = VerticalAlignment.Top;
                        Find<FrameworkElement>("ChineseStyleArtwork").VerticalAlignment = VerticalAlignment.Bottom;
                        Find<FrameworkElement>("ChineseStyleArtwork").Opacity = .2;
                        break;
                    default:
                        Columns(desk, -1, 342);
                        Place(hero, 0, 0, 2); Place(options, 1, 0, 2);
                        Place(sheet, 2, 0); Place(actions, 3, 0);
                        Place(statistics, 2, 1); Place(ranking, 2, 1); ranking.Margin = new Thickness(6, 96, 6, 6);
                        Place(music, 4, 0, 2);
                        break;
                }
                Place(notice, 7, 0, desk.ColumnDefinitions.Count);
            }
            // All navigation and live input remain horizontal and scrollable on small screens.
            ReflowSettings(spacious);
            ReflowSnake(spacious);
            ReflowSecondaryPages(spacious);
        }

        private void Rail(double width, bool stagger)
        {
            actions.Orientation = Orientation.Vertical;
            int index = 0;
            foreach (Button button in actions.Children)
            {
                button.Width = width;
                button.MinHeight = stagger ? 48 : 42;
                button.FontSize = 16;
                button.Margin = new Thickness(stagger ? (index % 3) * 8 : 3, 5, 3, stagger ? 14 : 7);
                if (stagger) button.RenderTransform = new RotateTransform(index % 2 == 0 ? -3 : 2);
                index++;
            }
        }

        private void ReflowSettings(bool spacious)
        {
            settingsLayout.ColumnDefinitions.Clear();
            settingsLayout.RowDefinitions.Clear();
            for (int i = 0; i < 3; i++) settingsLayout.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
            themeChoices.Columns = spacious ? (theme == UiTheme.Persona5 || theme == UiTheme.Constructivist ? 2 : 4) : 2;
            foreach (FrameworkElement child in settingsLayout.Children)
            {
                Grid.SetRowSpan(child, 1);
                Grid.SetColumnSpan(child, 1);
            }
            if (spacious && (theme == UiTheme.Persona5 || theme == UiTheme.ChineseStyle || theme == UiTheme.Constructivist))
            {
                bool right = theme == UiTheme.ChineseStyle;
                Columns(settingsLayout, right ? -1 : 244, right ? 244 : -1);
                Place(settingsHeader, 0, right ? 1 : 0);
                Place(settingsPanel, 0, right ? 0 : 1, 1, 2);
                Place(settingsFooter, 1, right ? 1 : 0);
                ((DockPanel)settingsHeader).LastChildFill = true;
                DockPanel.SetDock(Find<Button>("CloseSettings"), Dock.Bottom);
                DockPanel.SetDock(Find<Button>("RestartWithTheme"), Dock.Bottom);
            }
            else
            {
                Columns(settingsLayout, -1);
                Place(settingsHeader, 0, 0); Place(settingsPanel, 1, 0); Place(settingsFooter, 2, 0);
                DockPanel.SetDock(Find<Button>("CloseSettings"), Dock.Right);
                DockPanel.SetDock(Find<Button>("RestartWithTheme"), Dock.Right);
            }
            settingsPanel.Margin = new Thickness(7);
            settingsHeader.Margin = new Thickness(8, 8, 8, 14);
            settingsFooter.Margin = new Thickness(8, 8, 8, 14);
            foreach (RadioButton option in themeChoices.Children)
            {
                option.MinHeight = spacious ? 146 : 85;
                var panel = option.Content as StackPanel;
                if (panel != null && panel.Children.Count > 0)
                    panel.Children[0].Visibility = spacious ? Visibility.Visible : Visibility.Collapsed;
            }
        }

        private void BuildThemeGallery()
        {
            var names = new[] { "现代", "极简", "赛博朋克", "蒸汽朋克", "蒸汽波", "构成主义", "P5 风格", "国风" };
            var themes = new[] { UiTheme.Modern, UiTheme.Minimal, UiTheme.Cyberpunk, UiTheme.Steampunk, UiTheme.Vaporwave,
                UiTheme.Constructivist, UiTheme.Persona5, UiTheme.ChineseStyle };
            var descriptions = new[] { "坐标 · 几何 · 光轨", "留白 · 阅读 · 专注", "街区终端 · 飞行交通", "黄铜仪表 · 蒸汽机械", "梦境桌面 · 复古窗口", "斜向构图 · 红黑印版", "阶梯菜单 · 拼贴纸片", "山水册页 · 朱印题签" };
            for (int i = 0; i < themes.Length; i++)
            {
                var choice = Find<RadioButton>("Theme" + themes[i]);
                var stack = new StackPanel();
                stack.Children.Add(new ThemeThumbnail(themes[i]) { Height = 66, Margin = new Thickness(0, 0, 0, 8) });
                stack.Children.Add(new TextBlock { Text = names[i], FontSize = 16,
                    FontFamily = ThemeLettering.Family(themes[i]), FontWeight = ThemeLettering.CaptionWeight(themes[i]) });
                stack.Children.Add(new TextBlock { Text = descriptions[i], FontSize = 11, TextWrapping = TextWrapping.Wrap,
                    Foreground = Brush("Muted"), Margin = new Thickness(0, 4, 0, 0) });
                choice.Content = stack;
                choice.Padding = new Thickness(12);
                choice.Margin = new Thickness(5);
            }
        }

        private void ReflowSnake(bool spacious)
        {
            var layout = Find<Grid>("SnakeLayout");
            var play = Find<Grid>("SnakePlayArea");
            var controls = Find<DockPanel>("SnakeControls");
            bool left = theme == UiTheme.Persona5 || theme == UiTheme.Constructivist || theme == UiTheme.Cyberpunk;
            layout.ColumnDefinitions.Clear();
            double controlsWidth = spacious ? (theme == UiTheme.Persona5 ? 300 : 312) : 228;
            Columns(layout, left ? controlsWidth : -1, left ? -1 : controlsWidth);
            Grid.SetColumn(play, left ? 1 : 0);
            Grid.SetColumn(controls, left ? 0 : 1);
            controls.Margin = left ? new Thickness(0, 0, 13, 0) : new Thickness(13, 0, 0, 0);
            var command = (FrameworkElement)controls.Children[0];
            DockPanel.SetDock(command, theme == UiTheme.Steampunk || theme == UiTheme.Cyberpunk ? Dock.Top : Dock.Bottom);
            command.Margin = new Thickness(0, 6, 0, 8);
            var page = Find<Grid>("SnakePage");
            page.Margin = spacious ? new Thickness(26, 22, 26, 24) : new Thickness(12);
            var header = Find<DockPanel>("SnakeHeader");
            DockPanel.SetDock(Find<Button>("CloseSnake"), left ? Dock.Left : Dock.Right);
            DockPanel.SetDock(Find<Button>("OpenSnakeRules"), left ? Dock.Left : Dock.Right);
            header.LastChildFill = true;
            if (theme == UiTheme.Vaporwave)
                Find<Border>("SnakeFrame").Margin = new Thickness(12, 5, 0, 0);
        }

        private void ReflowSecondaryPages(bool spacious)
        {
            var rules = Find<Grid>("SnakeRules");
            var header = Find<DockPanel>("RulesHeader");
            var body = Find<Border>("RulesFrame");
            rules.MaxWidth = spacious ? 1180 : 850;
            rules.ColumnDefinitions.Clear();
            rules.RowDefinitions.Clear();
            bool side = spacious && (theme == UiTheme.ChineseStyle || theme == UiTheme.Persona5 || theme == UiTheme.Constructivist);
            bool right = theme == UiTheme.ChineseStyle;
            if (side)
            {
                Columns(rules, right ? -1 : 232, right ? 232 : -1);
                rules.RowDefinitions.Add(new RowDefinition());
                Place(header, 0, right ? 1 : 0); Place(body, 0, right ? 0 : 1);
                header.Margin = new Thickness(12, 16, 12, 0);
                header.VerticalAlignment = VerticalAlignment.Top;
                DockPanel.SetDock(Find<Button>("CloseSnakeRules"), Dock.Bottom);
            }
            else
            {
                Columns(rules, -1);
                rules.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
                rules.RowDefinitions.Add(new RowDefinition());
                Place(header, 0, 0); Place(body, 1, 0);
                header.Margin = new Thickness(0, 0, 0, 14);
                DockPanel.SetDock(Find<Button>("CloseSnakeRules"), Dock.Right);
            }
            var form = Find<Border>("StartupForm");
            form.HorizontalAlignment = spacious && (theme == UiTheme.Cyberpunk || theme == UiTheme.Persona5) ? HorizontalAlignment.Left
                : spacious && theme == UiTheme.ChineseStyle ? HorizontalAlignment.Right : HorizontalAlignment.Center;
            form.Margin = form.HorizontalAlignment == HorizontalAlignment.Left ? new Thickness(100, 0, 0, 0)
                : form.HorizontalAlignment == HorizontalAlignment.Right ? new Thickness(0, 0, 100, 0) : new Thickness(0);
            var confirm = Find<Button>("StartupConfirm");
            confirm.HorizontalAlignment = theme == UiTheme.ChineseStyle ? HorizontalAlignment.Right
                : theme == UiTheme.Minimal || theme == UiTheme.Vaporwave ? HorizontalAlignment.Stretch : HorizontalAlignment.Left;
        }

        private void DecorateOtherPages()
        {
            foreach (string name in new[] { "SnakeFrame", "RulesFrame", "LanPanel", "StartupForm" }) Frame(Find<Border>(name));
            var record = Find<Grid>("PracticeRecordOverlay");
            Frame((Border)record.Children[0]);
            var lobby = (StackPanel)((ScrollViewer)Find<Border>("LanPanel").Child).Content;
            // Group the two tasks as distinct panels while retaining the actual room fields.
            var children = lobby.Children.Cast<UIElement>().ToArray();
            lobby.Children.Clear();
            lobby.Children.Add(children[0]);
            lobby.Children.Add(children[1]);
            var create = new StackPanel();
            var join = new StackPanel();
            for (int i = 2; i <= 6; i++) create.Children.Add(children[i]);
            for (int i = 7; i < children.Length; i++) join.Children.Add(children[i]);
            var creation = new Border { Child = create, Padding = new Thickness(12), Margin = new Thickness(0, 8, 0, 9) };
            var joining = new Border { Child = join, Padding = new Thickness(12), Margin = new Thickness(0, 4, 0, 0) };
            Frame(creation); Frame(joining);
            if (theme == UiTheme.Persona5 || theme == UiTheme.Vaporwave)
            {
                lobby.Children.Add(joining);
                lobby.Children.Add(creation);
            }
            else
            {
                lobby.Children.Add(creation);
                lobby.Children.Add(joining);
            }
        }

        private void Frame(Border border)
        {
            border.BorderBrush = Brush("Line");
            border.BorderThickness = new Thickness(1);
            switch (theme)
            {
                case UiTheme.Minimal: border.CornerRadius = new CornerRadius(9); border.BorderThickness = new Thickness(0, 0, 0, 1); break;
                case UiTheme.Cyberpunk: border.BorderThickness = new Thickness(3, 1, 1, 3); break;
                case UiTheme.Steampunk: border.CornerRadius = new CornerRadius(10, 3, 10, 3); border.BorderThickness = new Thickness(3); break;
                case UiTheme.Vaporwave: border.CornerRadius = new CornerRadius(7); border.BorderThickness = new Thickness(2, 7, 3, 3); break;
                case UiTheme.Constructivist: border.BorderBrush = Brush("Text"); border.BorderThickness = new Thickness(6, 2, 2, 2); break;
                case UiTheme.Persona5: border.BorderBrush = Brush("Text"); border.BorderThickness = new Thickness(4, 3, 7, 5); break;
                case UiTheme.ChineseStyle: border.CornerRadius = new CornerRadius(1); border.BorderThickness = new Thickness(3, 1, 3, 1); break;
            }
        }

        private Brush Brush(string name) { return (Brush)window.FindResource(name); }
        private T Find<T>(string name) where T : FrameworkElement { return (T)window.FindName(name); }

        private static void Columns(Grid grid, params double[] widths)
        {
            foreach (double width in widths) grid.ColumnDefinitions.Add(new ColumnDefinition {
                Width = width < 0 ? new GridLength(-width, GridUnitType.Star) : new GridLength(width) });
        }

        private static void Place(FrameworkElement element, int row, int column, int columns = 1, int rows = 1)
        {
            Grid.SetRow(element, row); Grid.SetColumn(element, column);
            Grid.SetColumnSpan(element, columns); Grid.SetRowSpan(element, rows);
        }

        private static void Detach(FrameworkElement element)
        {
            var parent = element.Parent as Panel;
            if (parent != null) parent.Children.Remove(element);
        }

        private static System.Collections.Generic.IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            foreach (object item in LogicalTreeHelper.GetChildren(parent))
            {
                var child = item as DependencyObject;
                if (child == null) continue;
                yield return child;
                foreach (var descendant in Descendants(child)) yield return descendant;
            }
        }
    }

    internal sealed class ThemeThumbnail : FrameworkElement
    {
        private readonly UiTheme theme;
        internal ThemeThumbnail(UiTheme theme) { this.theme = theme; IsHitTestVisible = false; }
        protected override void OnRender(DrawingContext dc)
        {
            string[] backgrounds = { "#17111F", "#F1F2EF", "#241A14", "#101C24", "#F4C3EB", "#E4CFB3", "#171417", "#E6E1CE" };
            string[] accents = { "#56F4E5", "#266858", "#F0BF73", "#ECF664", "#61399B", "#A72722", "#DE102B", "#962F25" };
            Brush background = (Brush)new BrushConverter().ConvertFromString(backgrounds[(int)theme]);
            Brush accent = (Brush)new BrushConverter().ConvertFromString(accents[(int)theme]);
            var outline = new Pen(accent, 1);
            double w = Math.Max(1, ActualWidth), h = ActualHeight;
            dc.DrawRectangle(background, outline, new Rect(0, 0, w, h));
            bool left = theme == UiTheme.Persona5 || theme == UiTheme.Cyberpunk || theme == UiTheme.Constructivist;
            bool right = theme == UiTheme.ChineseStyle;
            if (left || right)
            {
                for (int i = 0; i < 4; i++)
                    dc.DrawRectangle(accent, null, new Rect(right ? w - 24 : 5 + (theme == UiTheme.Persona5 ? i * 2 : 0), 8 + i * 13, 17, 7));
            }
            else dc.DrawRectangle(accent, null, new Rect(8, 7, w - 16, 5));
            double x = left ? 32 : 8, body = w - (left || right ? 41 : 16);
            dc.DrawRectangle(null, outline, new Rect(x, left || right ? 7 : 20, body, 30));
            dc.DrawLine(outline, new Point(x + 5, 44), new Point(x + body - 6, 44));
            dc.DrawLine(outline, new Point(x + 5, 51), new Point(x + body * .65, 51));
            if (theme == UiTheme.Steampunk) dc.DrawEllipse(null, outline, new Point(w - 18, 51), 6, 6);
        }
    }
}
