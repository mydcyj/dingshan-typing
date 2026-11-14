using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.Reflection;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Markup;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    // One control contract keeps keyboard behavior and names stable across all eight silhouettes.
    internal static class ThemeControlDesign
    {
        private static readonly DependencyProperty TitleTheme = DependencyProperty.RegisterAttached(
            "TitleTheme", typeof(UiTheme?), typeof(ThemeControlDesign), new PropertyMetadata(null));

        internal static void Apply(Window window, UiTheme theme)
        {
            window.FontFamily = ThemeLettering.InterfaceFamily(theme);
            var originalPrimary = window.FindResource("PrimaryButton") as Style;
            string assembly = Assembly.GetExecutingAssembly().GetName().Name;
            string markup = Templates.Replace("$ASSEMBLY$", assembly).Replace("$THEME$", theme.ToString());
            var resources = (ResourceDictionary)XamlReader.Parse(markup);
            var templates = new Dictionary<Type, ControlTemplate>();
            foreach (Type type in new[] { typeof(Button), typeof(CheckBox), typeof(RadioButton),
                typeof(ComboBox), typeof(TextBox), typeof(Expander), typeof(Slider) })
            {
                var template = (ControlTemplate)resources[type.Name + "Design"];
                templates[type] = template;
                var prior = window.TryFindResource(type) as Style;
                var style = new Style(type, prior);
                style.Setters.Add(new Setter(Control.TemplateProperty, template));
                if (type != typeof(TextBox) && type != typeof(Slider))
                {
                    style.Setters.Add(new Setter(Control.FontFamilyProperty, ThemeLettering.Family(theme)));
                    style.Setters.Add(new Setter(Control.FontWeightProperty, ThemeLettering.CaptionWeight(theme)));
                }
                if (type == typeof(Button))
                    style.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(17, 8, 17, 8)));
                window.Resources[type] = style;
            }
            var primary = new Style(typeof(Button), originalPrimary);
            primary.Setters.Add(new Setter(Control.TemplateProperty, templates[typeof(Button)]));
            primary.Setters.Add(new Setter(Control.PaddingProperty, new Thickness(18, 9, 18, 9)));
            primary.Setters.Add(new Setter(Control.FontFamilyProperty, ThemeLettering.Family(theme)));
            primary.Setters.Add(new Setter(Control.FontWeightProperty, ThemeLettering.CaptionWeight(theme)));
            window.Resources["PrimaryButton"] = primary;

            // Popup containers use a separate visual tree, so give them the caption face explicitly.
            foreach (Type type in new[] { typeof(ComboBoxItem), typeof(ListBoxItem) })
            {
                var style = new Style(type, window.TryFindResource(type) as Style);
                style.Setters.Add(new Setter(Control.FontFamilyProperty, ThemeLettering.Family(theme)));
                style.Setters.Add(new Setter(Control.FontWeightProperty, ThemeLettering.CaptionWeight(theme)));
                window.Resources[type] = style;
            }

            var displayTitle = window.FindResource("DisplayTitle");
            var sectionTitle = window.FindResource("SectionTitle");
            var statValue = window.FindResource("StatValue");
            foreach (DependencyObject item in Descendants(window))
            {
                var control = item as Control;
                if (control != null && templates.ContainsKey(control.GetType()))
                    control.Template = templates[control.GetType()];
                var title = item as TextBlock;
                if (title != null)
                {
                    if (title.Style == displayTitle)
                        StylizeTitle(title, theme);
                    else if (title.Style == sectionTitle)
                        title.FontFamily = ThemeLettering.Family(theme);
                    else if (title.Style == statValue || title.Name == "SnakeScore" ||
                        title.Name == "MusicTime" || title.Name == "Count")
                    {
                        title.FontFamily = ThemeLettering.NumberFamily(theme);
                        Typography.SetNumeralAlignment(title, FontNumeralAlignment.Tabular);
                        if (title.Style == statValue && theme == UiTheme.ChineseStyle)
                            title.FontSize = 27;
                    }
                }
            }
        }

        internal static void StylizeTitle(TextBlock title, UiTheme theme)
        {
            if (title.GetValue(TitleTheme) != null) return;
            title.SetValue(TitleTheme, theme);
            title.FontFamily = ThemeLettering.Family(theme);
            title.FontWeight = theme == UiTheme.Minimal ? FontWeights.Light : FontWeights.Bold;
            // The real text still measures, wraps and supplies accessibility semantics.
            // The adorner draws the display lettering only; practice text never uses this path.
            bool installed = false;
            Action install = () =>
            {
                if (installed) return;
                var layer = AdornerLayer.GetAdornerLayer(title);
                if (layer == null) return;
                var ink = title.Foreground;
                var adornment = new TitleLettering(title, theme, ink);
                layer.Add(adornment);
                adornment.WatchText();
                title.Foreground = Brushes.Transparent;
                installed = true;
            };
            title.Loaded += (s, e) => install();
            if (title.IsLoaded) install();
        }

        private static IEnumerable<DependencyObject> Descendants(DependencyObject parent)
        {
            foreach (object child in LogicalTreeHelper.GetChildren(parent))
            {
                var node = child as DependencyObject;
                if (node == null) continue;
                yield return node;
                foreach (var descendant in Descendants(node)) yield return descendant;
            }
        }

        private sealed class TitleLettering : Adorner
        {
            private readonly TextBlock title;
            private readonly UiTheme theme;
            private readonly Brush ink;
            private readonly DependencyPropertyDescriptor textProperty = DependencyPropertyDescriptor.FromProperty(TextBlock.TextProperty, typeof(TextBlock));
            private bool watching;
            internal TitleLettering(TextBlock title, UiTheme theme, Brush ink) : base(title)
            {
                this.title = title; this.theme = theme; this.ink = ink;
                IsHitTestVisible = false;
                title.SizeChanged += (s, e) => InvalidateVisual();
                // Adorners live outside the page's visual subtree. Mirror its visibility
                // instead of caching an empty drawing while the page is collapsed.
                SetBinding(VisibilityProperty, new Binding("IsVisible")
                {
                    Source = title,
                    Converter = new BooleanToVisibilityConverter()
                });
                title.IsVisibleChanged += (s, e) => InvalidateVisual();
                title.Loaded += (s, e) => { WatchText(); InvalidateVisual(); };
                title.Unloaded += (s, e) =>
                {
                    if (!watching) return;
                    textProperty.RemoveValueChanged(title, TextChanged);
                    watching = false;
                };
            }
            internal void WatchText()
            {
                if (watching) return;
                textProperty.AddValueChanged(title, TextChanged);
                watching = true;
            }
            private void TextChanged(object sender, EventArgs args) { InvalidateVisual(); }
            protected override void OnRender(DrawingContext dc)
            {
                ThemeLettering.Draw(dc, title.Text, title.FontSize, theme, ink,
                    new Rect(0, 0, title.ActualWidth, title.ActualHeight), true);
            }
        }

        private const string Templates = @"
<ResourceDictionary xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'
 xmlns:x='http://schemas.microsoft.com/winfx/2006/xaml' xmlns:d='clr-namespace:EnglishTypingPractice;assembly=$ASSEMBLY$'>
 <ControlTemplate x:Key='ButtonDesign' TargetType='Button'>
  <Grid x:Name='Chrome' Background='Transparent'>
   <d:ThemePlate Theme='$THEME$' Fill='{TemplateBinding Background}' Line='{TemplateBinding BorderBrush}'
    Accent='{DynamicResource Accent}' Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}'
    Down='{Binding IsPressed, RelativeSource={RelativeSource TemplatedParent}}'
    Focused='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource TemplatedParent}}'/>
   <Border x:Name='ActivationEdge' IsHitTestVisible='False' Opacity='0' BorderBrush='{DynamicResource Accent}' BorderThickness='2' Margin='4'/>
   <d:ThemeCaption Theme='$THEME$' Text='{Binding Content, RelativeSource={RelativeSource TemplatedParent}}'
    Ink='{TemplateBinding Foreground}' LetterSize='{TemplateBinding FontSize}' Margin='{TemplateBinding Padding}' IsHitTestVisible='False'/>
  </Grid>
  <ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key='CheckBoxDesign' TargetType='CheckBox'>
  <DockPanel x:Name='Chrome' Background='Transparent'>
   <Grid Width='21' Height='21' Margin='0,0,6,0' VerticalAlignment='Center'>
    <d:ThemePlate Theme='$THEME$' Fill='{DynamicResource Field}' Line='{DynamicResource Line}' Accent='{DynamicResource Accent}'
     Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}' Down='{Binding IsPressed, RelativeSource={RelativeSource TemplatedParent}}'
     Focused='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource TemplatedParent}}'/>
    <TextBlock x:Name='Tick' Text='✓' FontWeight='Bold' FontSize='15' Foreground='{DynamicResource Accent}' Visibility='Hidden' HorizontalAlignment='Center' VerticalAlignment='Center'/>
    <Border x:Name='ActivationEdge' IsHitTestVisible='False' BorderBrush='{DynamicResource Accent}' BorderThickness='2' Opacity='0'/>
   </Grid>
   <ContentPresenter VerticalAlignment='Center' RecognizesAccessKey='True'
    TextElement.FontFamily='{TemplateBinding FontFamily}' TextElement.FontWeight='{TemplateBinding FontWeight}'/>
  </DockPanel>
  <ControlTemplate.Triggers>
   <Trigger Property='IsChecked' Value='True'><Setter TargetName='Tick' Property='Visibility' Value='Visible'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key='RadioButtonDesign' TargetType='RadioButton'>
  <Grid x:Name='Chrome' Background='Transparent'>
   <d:ThemePlate x:Name='Plate' Theme='$THEME$' Fill='{DynamicResource Field}' Line='{DynamicResource Line}' Accent='{DynamicResource Accent}'
    Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}' Down='{Binding IsPressed, RelativeSource={RelativeSource TemplatedParent}}'
    Focused='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource TemplatedParent}}'/>
   <Border x:Name='ActivationEdge' IsHitTestVisible='False' BorderBrush='{DynamicResource Accent}' BorderThickness='2' Margin='3' Opacity='0'/>
   <DockPanel Margin='{TemplateBinding Padding}'>
    <TextBlock x:Name='Mark' Text='◇' Foreground='{DynamicResource Accent}' FontSize='18' Margin='3,0,8,0' VerticalAlignment='Center'/>
    <ContentPresenter VerticalAlignment='Center' RecognizesAccessKey='True'
     TextElement.FontFamily='{TemplateBinding FontFamily}' TextElement.FontWeight='{TemplateBinding FontWeight}'/>
   </DockPanel>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property='IsChecked' Value='True'><Setter TargetName='Plate' Property='Line' Value='{DynamicResource Accent}'/><Setter TargetName='Mark' Property='Text' Value='◆'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key='TextBoxDesign' TargetType='TextBox'>
  <Grid x:Name='Chrome' Background='Transparent'>
   <d:ThemePlate Theme='$THEME$' Field='True' Fill='{TemplateBinding Background}' Line='{TemplateBinding BorderBrush}' Accent='{DynamicResource Accent}'
    Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}' Focused='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource TemplatedParent}}'/>
   <ScrollViewer x:Name='PART_ContentHost' Margin='4' Padding='{TemplateBinding Padding}'/>
  </Grid>
  <ControlTemplate.Triggers><Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.5'/></Trigger></ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key='ComboBoxDesign' TargetType='ComboBox'>
  <Grid x:Name='Chrome'>
   <ToggleButton Focusable='False' ClickMode='Press' IsChecked='{Binding IsDropDownOpen, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'>
     <Grid Background='Transparent'>
      <d:ThemePlate Theme='$THEME$' Fill='{DynamicResource Raised}' Line='{DynamicResource Line}' Accent='{DynamicResource Accent}'
       Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}' Down='{Binding IsPressed, RelativeSource={RelativeSource TemplatedParent}}'/>
      <TextBlock Text='⌄' FontSize='17' Foreground='{DynamicResource Accent}' HorizontalAlignment='Right' VerticalAlignment='Center' Margin='0,0,11,3'/>
     </Grid>
    </ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter IsHitTestVisible='False' Margin='9,4,24,4' VerticalAlignment='Center'
    TextElement.FontFamily='{TemplateBinding FontFamily}' TextElement.FontWeight='{TemplateBinding FontWeight}'
    Content='{TemplateBinding SelectionBoxItem}' ContentTemplate='{TemplateBinding SelectionBoxItemTemplate}' ContentTemplateSelector='{TemplateBinding ItemTemplateSelector}'/>
   <Border x:Name='FocusEdge' IsHitTestVisible='False' BorderBrush='{DynamicResource Accent}' BorderThickness='1' Margin='3' Visibility='Collapsed'/>
   <Border x:Name='ActivationEdge' IsHitTestVisible='False' BorderBrush='{DynamicResource Accent}' BorderThickness='2' Margin='3' Opacity='0'/>
   <Popup x:Name='PART_Popup' IsOpen='{TemplateBinding IsDropDownOpen}' Placement='Bottom' AllowsTransparency='True' Focusable='False' PopupAnimation='None'>
    <Border Background='{DynamicResource Field}' BorderBrush='{DynamicResource Accent}' BorderThickness='1' MinWidth='{Binding ActualWidth, RelativeSource={RelativeSource TemplatedParent}}'
     MaxHeight='{TemplateBinding MaxDropDownHeight}' Padding='3'>
     <ScrollViewer CanContentScroll='True' VerticalScrollBarVisibility='Auto'><ItemsPresenter KeyboardNavigation.DirectionalNavigation='Contained'/></ScrollViewer>
    </Border>
   </Popup>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property='IsKeyboardFocusWithin' Value='True'><Setter TargetName='FocusEdge' Property='Visibility' Value='Visible'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key='ExpanderDesign' TargetType='Expander'>
  <StackPanel x:Name='Chrome'>
   <ToggleButton x:Name='Header' HorizontalContentAlignment='Stretch' Content='{TemplateBinding Header}'
    FontFamily='{TemplateBinding FontFamily}' FontWeight='{TemplateBinding FontWeight}'
    IsChecked='{Binding IsExpanded, RelativeSource={RelativeSource TemplatedParent}, Mode=TwoWay}'>
    <ToggleButton.Template><ControlTemplate TargetType='ToggleButton'>
     <Grid Background='Transparent'>
      <d:ThemePlate Theme='$THEME$' Fill='{DynamicResource Raised}' Line='{DynamicResource Line}' Accent='{DynamicResource Accent}'
       Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}' Down='{Binding IsPressed, RelativeSource={RelativeSource TemplatedParent}}'
       Focused='{Binding IsKeyboardFocusWithin, RelativeSource={RelativeSource TemplatedParent}}'/>
      <DockPanel Margin='12,7'><TextBlock x:Name='Arrow' Text='+' Margin='0,0,9,0' Foreground='{DynamicResource Accent}'/><ContentPresenter TextElement.FontFamily='{TemplateBinding FontFamily}' TextElement.FontWeight='{TemplateBinding FontWeight}'/></DockPanel>
      <Border x:Name='ActivationEdge' IsHitTestVisible='False' BorderBrush='{DynamicResource Accent}' BorderThickness='1' Margin='3' Opacity='0'/>
     </Grid>
     <ControlTemplate.Triggers><Trigger Property='IsChecked' Value='True'><Setter TargetName='Arrow' Property='Text' Value='−'/></Trigger></ControlTemplate.Triggers>
    </ControlTemplate></ToggleButton.Template>
   </ToggleButton>
   <ContentPresenter x:Name='Body' Visibility='Collapsed' Margin='0,5,0,0'/>
  </StackPanel>
  <ControlTemplate.Triggers>
   <Trigger Property='IsExpanded' Value='True'><Setter TargetName='Body' Property='Visibility' Value='Visible'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate>
 <ControlTemplate x:Key='SliderDesign' TargetType='Slider'>
  <Grid x:Name='Chrome' Background='Transparent'>
   <Border Height='3' VerticalAlignment='Center' Background='{DynamicResource Line}'/>
   <Track x:Name='PART_Track' VerticalAlignment='Center'>
    <Track.DecreaseRepeatButton><RepeatButton Command='Slider.DecreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Grid Background='Transparent'><Border Height='3' VerticalAlignment='Center' Background='{DynamicResource Accent}'/></Grid></ControlTemplate></RepeatButton.Template></RepeatButton></Track.DecreaseRepeatButton>
    <Track.Thumb><Thumb Width='21' Height='21' Cursor='SizeWE'><Thumb.Template><ControlTemplate TargetType='Thumb'>
     <d:ThemePlate Theme='$THEME$' Fill='{DynamicResource Accent}' Line='{DynamicResource Text}' Accent='{DynamicResource Accent}'
      Hot='{Binding IsMouseOver, RelativeSource={RelativeSource TemplatedParent}}' Down='{Binding IsDragging, RelativeSource={RelativeSource TemplatedParent}}'/>
    </ControlTemplate></Thumb.Template></Thumb></Track.Thumb>
    <Track.IncreaseRepeatButton><RepeatButton Command='Slider.IncreaseLarge' Focusable='False'><RepeatButton.Template><ControlTemplate TargetType='RepeatButton'><Border Background='Transparent'/></ControlTemplate></RepeatButton.Template></RepeatButton></Track.IncreaseRepeatButton>
   </Track>
   <Border x:Name='FocusEdge' BorderBrush='{DynamicResource Accent}' BorderThickness='1' Visibility='Collapsed' IsHitTestVisible='False'/>
  </Grid>
  <ControlTemplate.Triggers>
   <Trigger Property='IsKeyboardFocused' Value='True'><Setter TargetName='FocusEdge' Property='Visibility' Value='Visible'/></Trigger>
   <Trigger Property='IsEnabled' Value='False'><Setter TargetName='Chrome' Property='Opacity' Value='.45'/></Trigger>
  </ControlTemplate.Triggers>
 </ControlTemplate>
 </ResourceDictionary>";
    }

    // Geometries remain inside the clickable rectangular control surface.
    public sealed class ThemePlate : FrameworkElement
    {
        private static DependencyProperty Register(string name, Type type, object value)
        { return DependencyProperty.Register(name, type, typeof(ThemePlate), new FrameworkPropertyMetadata(value, FrameworkPropertyMetadataOptions.AffectsRender)); }
        public static readonly DependencyProperty ThemeProperty = Register("Theme", typeof(UiTheme), UiTheme.Modern);
        public static readonly DependencyProperty FillProperty = Register("Fill", typeof(Brush), Brushes.Transparent);
        public static readonly DependencyProperty LineProperty = Register("Line", typeof(Brush), Brushes.Gray);
        public static readonly DependencyProperty AccentProperty = Register("Accent", typeof(Brush), Brushes.White);
        public static readonly DependencyProperty HotProperty = Register("Hot", typeof(bool), false);
        public static readonly DependencyProperty DownProperty = Register("Down", typeof(bool), false);
        public static readonly DependencyProperty FocusedProperty = Register("Focused", typeof(bool), false);
        public static readonly DependencyProperty FieldProperty = Register("Field", typeof(bool), false);
        public UiTheme Theme { get { return (UiTheme)GetValue(ThemeProperty); } set { SetValue(ThemeProperty, value); } }
        public Brush Fill { get { return (Brush)GetValue(FillProperty); } set { SetValue(FillProperty, value); } }
        public Brush Line { get { return (Brush)GetValue(LineProperty); } set { SetValue(LineProperty, value); } }
        public Brush Accent { get { return (Brush)GetValue(AccentProperty); } set { SetValue(AccentProperty, value); } }
        public bool Hot { get { return (bool)GetValue(HotProperty); } set { SetValue(HotProperty, value); } }
        public bool Down { get { return (bool)GetValue(DownProperty); } set { SetValue(DownProperty, value); } }
        public bool Focused { get { return (bool)GetValue(FocusedProperty); } set { SetValue(FocusedProperty, value); } }
        public bool Field { get { return (bool)GetValue(FieldProperty); } set { SetValue(FieldProperty, value); } }

        protected override void OnRender(DrawingContext dc)
        {
            double w = ActualWidth, h = ActualHeight;
            if (w < 7 || h < 7) return;
            Brush edge = Hot || Focused ? Accent : Line;
            double inset = Theme == UiTheme.Persona5 ? 3 : 1.5;
            var area = new Rect(inset, inset, Math.Max(1, w - inset * 2 - 2), Math.Max(1, h - inset * 2 - 2));
            var outline = new Pen(edge, Theme == UiTheme.Persona5 || Theme == UiTheme.Vaporwave ? 2 : 1);
            dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, w, h));
            if (Down) dc.PushTransform(new TranslateTransform(1, 1));
            var shape = Outline(area, Theme, Field);
            if (Theme == UiTheme.Persona5 || Theme == UiTheme.Constructivist || Theme == UiTheme.Vaporwave)
            {
                dc.PushTransform(new TranslateTransform(2, 2));
                dc.DrawGeometry(Theme == UiTheme.Persona5 ? Brushes.Black : Line, null, shape);
                dc.Pop();
            }
            if (Theme == UiTheme.Persona5)
                dc.DrawGeometry(Fill, new Pen(Brushes.White, 4), shape);
            dc.DrawGeometry(Fill, outline, shape);
            if (Hot || Down)
            {
                dc.PushOpacity(Down ? .16 : .08);
                dc.DrawGeometry(Accent, null, shape);
                dc.Pop();
            }
            double left = area.Left, right = area.Right, top = area.Top, bottom = area.Bottom;
            if (Theme == UiTheme.Steampunk)
            {
                foreach (double x in new[] { left + 5, right - 5 })
                    foreach (double y in new[] { top + 5, bottom - 5 })
                    {
                        dc.DrawEllipse(edge, null, new Point(x, y), 1.5, 1.5);
                        dc.DrawLine(new Pen(Fill, .7), new Point(x - .8, y + .8), new Point(x + .8, y - .8));
                    }
                if (w > 60 && h > 26)
                    dc.DrawLine(new Pen(edge, .7), new Point(left + 12, bottom - 4), new Point(right - 12, bottom - 4));
            }
            else if (Theme == UiTheme.Cyberpunk)
            {
                dc.DrawLine(new Pen(Accent, 2), new Point(left + 9, bottom - 3), new Point(left + Math.Min(28, w * .28), bottom - 3));
                dc.DrawLine(new Pen(edge, 1), new Point(right - 6, top + 7), new Point(right - 6, Math.Min(bottom - 4, top + 16)));
            }
            else if (Theme == UiTheme.ChineseStyle)
            {
                var red = new SolidColorBrush(Color.FromRgb(170, 55, 36));
                dc.DrawRectangle(red, null, new Rect(right - 6, bottom - 6, 3, 3));
                dc.DrawLine(new Pen(edge, .65), new Point(left + 5, top + 4), new Point(left + Math.Min(17, w * .25), top + 4));
            }
            else if (Theme == UiTheme.Constructivist)
                dc.DrawRectangle(Accent, null, new Rect(left + 2, top + 3, 3, Math.Max(1, area.Height - 6)));
            else if (Theme == UiTheme.Vaporwave)
            {
                dc.DrawLine(new Pen(Brushes.White, 1.5), new Point(left + 5, top + 3), new Point(right - 4, top + 3));
                dc.DrawLine(new Pen(Brushes.White, 1), new Point(left + 3, top + 5), new Point(left + 3, bottom - 4));
            }
            else if (Theme == UiTheme.Modern)
                dc.DrawLine(new Pen(Accent, 1.5), new Point(left + 4, top + 3), new Point(left + Math.Min(17, w * .3), top + 3));
            if (Focused)
                dc.DrawRectangle(null, new Pen(Accent, 1) { DashStyle = DashStyles.Dot },
                    new Rect(5, 5, Math.Max(1, w - 12), Math.Max(1, h - 12)));
            if (Down) dc.Pop();
        }

        private static Geometry Outline(Rect r, UiTheme theme, bool field)
        {
            if (theme == UiTheme.Minimal) return new RectangleGeometry(r, field ? 3 : 7, field ? 3 : 7);
            double l = r.Left, t = r.Top, b = r.Bottom, right = r.Right;
            double cut = Math.Min(9, Math.Min(r.Width, r.Height) * .22);
            if (field) cut = Math.Min(cut, 4);
            switch (theme)
            {
                case UiTheme.Cyberpunk:
                    return Polygon(l + cut, t, right, t, right, b - cut, right - cut, b, l, b, l, t + cut);
                case UiTheme.Steampunk:
                    return Polygon(l + cut, t, right - cut, t, right, t + cut, right, b - cut, right - cut, b, l + cut, b, l, b - cut, l, t + cut);
                case UiTheme.Persona5:
                    return Polygon(l + cut, t + 2, right - 1, t, right - cut * .35, b - 3, l, b);
                case UiTheme.Constructivist:
                    return Polygon(l + cut, t, right, t, right - cut, b, l, b);
                case UiTheme.Vaporwave:
                    return Polygon(l + 3, t, right - 3, t, right - 3, t + 3, right, t + 3, right, b - 3, right - 3, b - 3, right - 3, b, l + 3, b, l + 3, b - 3, l, b - 3, l, t + 3, l + 3, t + 3);
                case UiTheme.ChineseStyle:
                    return Polygon(l + 1, t + 1, right - 2, t, right, t + 2, right - 1, b, l, b - 1);
                default:
                    return Polygon(l, t, right - cut, t, right, t + cut, right, b, l, b);
            }
        }
        internal static Geometry Polygon(params double[] coordinates)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(new Point(coordinates[0], coordinates[1]), true, true);
                for (int i = 2; i < coordinates.Length; i += 2)
                    ctx.LineTo(new Point(coordinates[i], coordinates[i + 1]), true, false);
            }
            geometry.Freeze(); return geometry;
        }
    }

    public sealed class ThemeCaption : FrameworkElement
    {
        public static readonly DependencyProperty ThemeProperty = DependencyProperty.Register("Theme", typeof(UiTheme), typeof(ThemeCaption),
            new FrameworkPropertyMetadata(UiTheme.Modern, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty TextProperty = DependencyProperty.Register("Text", typeof(string), typeof(ThemeCaption),
            new FrameworkPropertyMetadata("", FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty LetterSizeProperty = DependencyProperty.Register("LetterSize", typeof(double), typeof(ThemeCaption),
            new FrameworkPropertyMetadata(14d, FrameworkPropertyMetadataOptions.AffectsMeasure | FrameworkPropertyMetadataOptions.AffectsRender));
        public static readonly DependencyProperty InkProperty = DependencyProperty.Register("Ink", typeof(Brush), typeof(ThemeCaption),
            new FrameworkPropertyMetadata(Brushes.White, FrameworkPropertyMetadataOptions.AffectsRender));
        public UiTheme Theme { get { return (UiTheme)GetValue(ThemeProperty); } set { SetValue(ThemeProperty, value); } }
        public string Text { get { return (string)GetValue(TextProperty); } set { SetValue(TextProperty, value); } }
        public double LetterSize { get { return (double)GetValue(LetterSizeProperty); } set { SetValue(LetterSizeProperty, value); } }
        public Brush Ink { get { return (Brush)GetValue(InkProperty); } set { SetValue(InkProperty, value); } }
        protected override Size MeasureOverride(Size available)
        {
            var text = ThemeLettering.Format(Text ?? "", LetterSize, Theme, Ink);
            return new Size(text.WidthIncludingTrailingWhitespace * (Theme == UiTheme.Persona5 ? 1.08 : 1) + 4, text.Height + 4);
        }
        protected override void OnRender(DrawingContext dc)
        { ThemeLettering.Draw(dc, Text ?? "", LetterSize, Theme, Ink, new Rect(RenderSize), false); }
    }

    internal static class ThemeLettering
    {
        internal static FontFamily InterfaceFamily(UiTheme theme)
        {
            // Use related reading faces for instructions and other long UI text.
            switch (theme)
            {
                case UiTheme.Cyberpunk: return new FontFamily("Bahnschrift SemiCondensed, DengXian, Microsoft YaHei UI");
                case UiTheme.Minimal: return new FontFamily("Segoe UI, DengXian, Microsoft YaHei UI");
                case UiTheme.Steampunk: return new FontFamily("Cambria, SimSun, Microsoft YaHei UI");
                case UiTheme.Vaporwave: return new FontFamily("Consolas, Microsoft YaHei UI");
                case UiTheme.Constructivist: return new FontFamily("Arial Narrow, SimHei, Microsoft YaHei UI");
                case UiTheme.Persona5: return new FontFamily("Trebuchet MS, Microsoft YaHei UI");
                case UiTheme.ChineseStyle: return new FontFamily("KaiTi, SimSun, Microsoft YaHei UI");
                default: return new FontFamily("Bahnschrift, Microsoft YaHei UI");
            }
        }

        internal static FontFamily Family(UiTheme theme)
        {
            switch (theme)
            {
                case UiTheme.Cyberpunk: return new FontFamily("Bahnschrift SemiCondensed, SimHei, Microsoft YaHei UI");
                case UiTheme.Minimal: return new FontFamily("Segoe UI Light, DengXian, Microsoft YaHei UI");
                case UiTheme.Steampunk: return new FontFamily("Georgia, SimSun, Microsoft YaHei UI");
                case UiTheme.Vaporwave: return new FontFamily("Consolas, YouYuan, Microsoft YaHei UI");
                case UiTheme.Constructivist: return new FontFamily("Impact, SimHei, Microsoft YaHei UI");
                case UiTheme.Persona5: return new FontFamily("Arial Black, DengXian, Microsoft YaHei UI");
                case UiTheme.ChineseStyle: return new FontFamily("KaiTi, SimSun, Microsoft YaHei UI");
                default: return new FontFamily("Bahnschrift, Microsoft YaHei UI");
            }
        }

        internal static FontWeight CaptionWeight(UiTheme theme)
        {
            return theme == UiTheme.Minimal || theme == UiTheme.ChineseStyle ? FontWeights.Normal : FontWeights.Bold;
        }

        internal static FontFamily NumberFamily(UiTheme theme)
        {
            // Scores must fit decimal accuracies and multi-digit times in narrow HUD columns.
            if (theme == UiTheme.Persona5) return new FontFamily("Arial Narrow, Microsoft YaHei UI");
            if (theme == UiTheme.ChineseStyle) return new FontFamily("Times New Roman, KaiTi, SimSun");
            return Family(theme);
        }

        internal static FormattedText Format(string value, double size, UiTheme theme, Brush ink)
        {
            return new FormattedText(value, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight,
                new Typeface(Family(theme), FontStyles.Normal,
                    CaptionWeight(theme), FontStretches.Normal), size, ink, 1.0);
        }

        internal static void Draw(DrawingContext dc, string value, double size, UiTheme theme, Brush ink, Rect area, bool title)
        {
            if (string.IsNullOrEmpty(value) || area.Width < 1 || area.Height < 1) return;
            var formatted = Format(value, size, theme, ink);
            double extra = theme == UiTheme.Persona5 ? 1.08 : 1;
            double naturalWidth = formatted.WidthIncludingTrailingWhitespace * extra + 4;
            double scale = Math.Min(1, Math.Min(area.Width / naturalWidth, area.Height / (formatted.Height + 3)));
            double x = title ? 0 : Math.Max(0, (area.Width - naturalWidth * scale) / 2);
            double y = Math.Max(0, (area.Height - formatted.Height * scale) / 2);
            dc.PushClip(new RectangleGeometry(area));
            dc.PushTransform(new TranslateTransform(area.X + x, area.Y + y));
            dc.PushTransform(new ScaleTransform(scale, scale));
            if (theme == UiTheme.Persona5)
            {
                double advance = 2;
                for (int i = 0; i < value.Length; i++)
                {
                    string character = value.Substring(i, 1);
                    var glyph = Format(character, size, theme, ink);
                    double width = glyph.WidthIncludingTrailingWhitespace;
                    if (char.IsWhiteSpace(value[i])) { advance += width; continue; }
                    bool invert = i % 3 != 1;
                    Brush paper = invert ? Brushes.White : Brushes.Black;
                    Brush pigment = invert ? Brushes.Black : Brushes.White;
                    dc.PushTransform(new TranslateTransform(advance, i % 2 == 0 ? 0 : 1.2));
                    dc.PushTransform(new RotateTransform(i % 2 == 0 ? -3 : 3, width / 2, glyph.Height / 2));
                    dc.DrawGeometry(paper, new Pen(pigment, .8), ThemePlate.Polygon(-1, 1, width + 1, 0, width + 2, glyph.Height - 1, -2, glyph.Height));
                    dc.DrawGeometry(pigment, null, glyph.BuildGeometry(new Point(0, 0)));
                    dc.Pop(); dc.Pop();
                    advance += width * 1.08;
                }
            }
            else
            {
                var outline = formatted.BuildGeometry(new Point(2, 0));
                if (theme == UiTheme.Cyberpunk || theme == UiTheme.Constructivist)
                {
                    // A small forward rake makes the label read like an industrial nameplate.
                    double rake = theme == UiTheme.Cyberpunk ? -.12 : -.08;
                    outline.Transform = new MatrixTransform(1, 0, rake, 1, -rake * formatted.Height * .7, 0);
                }
                if (theme == UiTheme.Cyberpunk || theme == UiTheme.Vaporwave)
                {
                    dc.PushOpacity(title ? .65 : .34);
                    dc.PushTransform(new TranslateTransform(theme == UiTheme.Vaporwave ? 1.7 : 1, 1));
                    dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(218, 84, 161)), null, outline);
                    dc.Pop(); dc.Pop();
                }
                else if (theme == UiTheme.Steampunk)
                {
                    dc.PushTransform(new TranslateTransform(1, 1));
                    dc.DrawGeometry(new SolidColorBrush(Color.FromRgb(39, 24, 13)), new Pen(new SolidColorBrush(Color.FromRgb(207, 156, 91)), .6), outline);
                    dc.Pop();
                }
                else if (theme == UiTheme.Constructivist && title)
                {
                    dc.DrawRectangle(new SolidColorBrush(Color.FromRgb(167, 39, 34)), null, new Rect(1, formatted.Height - 2, Math.Max(3, formatted.Width * .42), 2));
                }
                else if (theme == UiTheme.ChineseStyle && title)
                {
                    dc.PushOpacity(.2);
                    dc.DrawGeometry(ink, new Pen(ink, .8), outline);
                    dc.Pop();
                }
                dc.DrawGeometry(ink, null, outline);
                if (theme == UiTheme.Modern && title)
                    dc.DrawLine(new Pen(ink, .7), new Point(2, formatted.Height - 1), new Point(Math.Min(26, formatted.Width), formatted.Height - 1));
            }
            dc.Pop(); dc.Pop(); dc.Pop();
        }
    }
}
