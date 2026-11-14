using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Controls.Primitives;
using System.Windows.Media;
using System.Windows.Media.Animation;

namespace EnglishTypingPractice
{
    internal static class InteractionMotion
    {
        private static readonly DependencyProperty PageTranslation = DependencyProperty.RegisterAttached(
            "PageTranslation", typeof(TranslateTransform), typeof(InteractionMotion));

        internal static void Attach(Window window, Func<bool> lowEffects)
        {
            window.AddHandler(ButtonBase.ClickEvent, new RoutedEventHandler((sender, e) =>
                Pulse(e.OriginalSource as Control, lowEffects())), true);
            window.AddHandler(Selector.SelectionChangedEvent, new SelectionChangedEventHandler((sender, e) =>
                Pulse(e.OriginalSource as Control, lowEffects())), true);
        }

        private static void Pulse(Control control, bool lowEffects)
        {
            if (!CanAnimate(lowEffects) || control == null || !control.IsVisible || !control.IsEnabled || control.Template == null)
                return;
            var flash = control.Template.FindName("ActivationEdge", control) as UIElement;
            if (flash == null)
                return;
            flash.BeginAnimation(UIElement.OpacityProperty, Fade(1, 0, 225), HandoffBehavior.SnapshotAndReplace);
        }

        internal static void Reveal(FrameworkElement page, double horizontalOffset, bool lowEffects)
        {
            var translation = (TranslateTransform)page.GetValue(PageTranslation);
            if (translation != null)
                translation.BeginAnimation(TranslateTransform.XProperty, null);
            page.BeginAnimation(UIElement.OpacityProperty, null);
            page.Visibility = Visibility.Visible;
            if (!CanAnimate(lowEffects))
                return;

            if (translation == null)
            {
                translation = new TranslateTransform();
                var transform = new TransformGroup();
                if (page.RenderTransform != null && page.RenderTransform != Transform.Identity)
                    transform.Children.Add(page.RenderTransform);
                transform.Children.Add(translation);
                page.RenderTransform = transform;
                page.SetValue(PageTranslation, translation);
            }
            translation.BeginAnimation(TranslateTransform.XProperty, Fade(horizontalOffset, 0, 195));
            double baseOpacity = page.Opacity;
            page.BeginAnimation(UIElement.OpacityProperty, Fade(baseOpacity * .74, baseOpacity, 160));
        }

        private static bool CanAnimate(bool lowEffects)
        {
            return !lowEffects && SystemParameters.ClientAreaAnimation;
        }

        private static DoubleAnimation Fade(double from, double to, int milliseconds)
        {
            return new DoubleAnimation(from, to, TimeSpan.FromMilliseconds(milliseconds))
            {
                EasingFunction = new QuadraticEase { EasingMode = EasingMode.EaseOut },
                FillBehavior = FillBehavior.Stop
            };
        }
    }
}
