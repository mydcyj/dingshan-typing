using System;
using System.Windows;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    internal sealed class TypingEffectVisuals : FrameworkElement
    {
        private static readonly Brush Steam = MakeSteamBrush();

        internal static readonly DependencyProperty ProgressProperty = DependencyProperty.Register(
            "Progress", typeof(double), typeof(TypingEffectVisuals),
            new FrameworkPropertyMetadata(0.0, FrameworkPropertyMetadataOptions.AffectsRender));

        private readonly UiTheme theme;
        private readonly int level;
        private readonly Brush primary, secondary;
        private readonly Pen thin, bold;

        internal TypingEffectVisuals(UiTheme theme, int level, Brush primary, Brush secondary)
        {
            this.theme = theme;
            this.level = level;
            this.primary = primary;
            this.secondary = secondary;
            thin = new Pen(primary, 1.4);
            bold = new Pen(secondary, level == 0 ? 2 : 3);
            thin.Freeze();
            bold.Freeze();
        }

        protected override void OnRender(DrawingContext drawing)
        {
            double time = (double)GetValue(ProgressProperty);
            double reveal = Math.Min(1, .42 + time * 9);
            drawing.PushOpacity((level == 0 ? .52 : .82) * reveal * (1 - time));
            drawing.PushTransform(new ScaleTransform(ActualWidth / 100, ActualHeight / 68));
            switch (theme)
            {
                case UiTheme.Steampunk: DrawSteam(drawing, time); break;
                case UiTheme.Cyberpunk: DrawScan(drawing, time); break;
                case UiTheme.Vaporwave: DrawVapor(drawing, time); break;
                case UiTheme.Constructivist: DrawStamp(drawing, time); break;
                case UiTheme.Persona5: DrawComic(drawing, time); break;
                case UiTheme.ChineseStyle: DrawInk(drawing, time); break;
                default: DrawBrackets(drawing, time); break;
            }
            drawing.Pop();
            drawing.Pop();
        }

        private void DrawSteam(DrawingContext drawing, double time)
        {
            int count = 2 + level * 3;
            for (int i = 0; i < count; i++)
            {
                double side = i % 2 == 0 ? -1 : 1;
                double drift = Math.Max(0, time - .04 * i);
                var center = new Point(50 + side * (13 + drift * 39), 43 - drift * (27 + i * 2));
                drawing.DrawEllipse(Steam, null, center, 7 + 13 * drift, 5 + 10 * drift);
            }
            DrawGear(drawing, new Point(19, 37), 10 + level * 2, time * 136);
            if (level > 0)
                DrawGear(drawing, new Point(81, 31), 7 + level * 2, -time * 173);
        }

        private void DrawGear(DrawingContext drawing, Point center, double radius, double angle)
        {
            const int teeth = 8;
            var outline = new Point[teeth * 4];
            for (int i = 0; i < outline.Length; i++)
            {
                double a = i * Math.PI * 2 / outline.Length;
                double r = i % 4 == 1 || i % 4 == 2 ? radius : radius * .78;
                outline[i] = new Point(center.X + r * Math.Cos(a), center.Y + r * Math.Sin(a));
            }
            drawing.PushTransform(new RotateTransform(angle, center.X, center.Y));
            DrawPolygon(drawing, null, thin, outline);
            drawing.DrawEllipse(null, bold, center, radius * .34, radius * .34);
            drawing.Pop();
        }

        private void DrawScan(DrawingContext drawing, double time)
        {
            double scan = 12 + 72 * time;
            for (int i = 0; i < level + 2; i++)
            {
                double y = 17 + i * 10;
                drawing.DrawLine(i == 0 ? bold : thin, new Point(9 + i * 5, y), new Point(scan + i * 2, y));
                drawing.DrawRectangle(i % 2 == 0 ? primary : secondary, null, new Rect(scan + i * 3, y - 2, 9, 4));
            }
            drawing.DrawLine(thin, new Point(scan, 8), new Point(scan, 53));
            drawing.DrawLine(bold, new Point(88, 53), new Point(96, 45));
        }

        private void DrawVapor(DrawingContext drawing, double time)
        {
            var sun = new Point(75, 27 - time * 18);
            drawing.PushClip(new EllipseGeometry(sun, 14, 14));
            for (double dy = -12; dy < 14; dy += 4)
                drawing.DrawLine(bold, new Point(58, sun.Y + dy), new Point(92, sun.Y + dy));
            drawing.Pop();
            double edge = 15 * time;
            drawing.DrawRectangle(null, thin, new Rect(17 - edge / 2, 23 - edge / 2, 36 + edge, 26 + edge));
            drawing.DrawLine(thin, new Point(17 - edge / 2, 29 - edge / 2), new Point(53 + edge / 2, 29 - edge / 2));
            if (level > 0)
                drawing.DrawRectangle(null, bold, new Rect(57, 38, 19 + edge, 12 + edge / 2));
        }

        private void DrawStamp(DrawingContext drawing, double time)
        {
            drawing.PushTransform(new RotateTransform(-15 + 5 * time, 50, 34));
            DrawPolygon(drawing, primary, null, new Point(8, 41), new Point(63, 36),
                new Point(73, 46), new Point(8, 48));
            DrawPolygon(drawing, secondary, null, new Point(62, 30), new Point(94, 11), new Point(92, 33));
            if (level > 0)
                drawing.DrawRectangle(null, bold, new Rect(25, 15, 32, 21));
            drawing.Pop();
        }

        private void DrawComic(DrawingContext drawing, double time)
        {
            double scale = .75 + time * .4;
            drawing.PushTransform(new ScaleTransform(scale, scale, 50, 34));
            var burst = new[]
            {
                new Point(7, 23), new Point(24, 24), new Point(21, 10), new Point(42, 20),
                new Point(54, 9), new Point(65, 22), new Point(86, 12), new Point(82, 29),
                new Point(96, 38), new Point(79, 43), new Point(81, 58), new Point(62, 53),
                new Point(53, 62), new Point(39, 52), new Point(19, 58), new Point(23, 45), new Point(6, 39)
            };
            DrawPolygon(drawing, null, bold, burst);
            drawing.DrawLine(thin, new Point(6, 11), new Point(22, 18));
            drawing.DrawLine(thin, new Point(97, 53), new Point(87, 45));
            if (level > 0)
            {
                drawing.DrawLine(thin, new Point(39, 5), new Point(43, 15));
                drawing.DrawLine(thin, new Point(72, 63), new Point(67, 52));
            }
            drawing.Pop();
        }

        private void DrawInk(DrawingContext drawing, double time)
        {
            double width = 10 + 25 * time;
            drawing.DrawEllipse(null, thin, new Point(47, 35), width, width * .51);
            if (level > 0)
                drawing.DrawEllipse(null, thin, new Point(47, 35), width * .75, width * .33);
            for (int i = 0; i < level + 3; i++)
            {
                double size = 1 + i % 2;
                var dot = new Point(19 + i * 11, 47 - i % 3 * 6 - time * 14);
                drawing.DrawEllipse(primary, null, dot, size * 1.45, size);
            }
            if (level > 0)
                drawing.DrawRectangle(null, bold, new Rect(78, 16 + 4 * time, 11, 11));
        }

        private void DrawBrackets(DrawingContext drawing, double time)
        {
            double spread = 11 + 29 * time;
            drawing.DrawLine(thin, new Point(50 - spread, 43), new Point(45, 43));
            drawing.DrawLine(thin, new Point(55, 43), new Point(50 + spread, 43));
            if (theme == UiTheme.Minimal)
            {
                drawing.DrawEllipse(primary, null, new Point(50, 43), 2, 2);
                return;
            }
            DrawPolygon(drawing, secondary, null, new Point(50 - spread, 33), new Point(59 - spread, 36),
                new Point(50 - spread, 39));
            DrawPolygon(drawing, secondary, null, new Point(50 + spread, 33), new Point(41 + spread, 36),
                new Point(50 + spread, 39));
        }

        private static void DrawPolygon(DrawingContext drawing, Brush fill, Pen outline, params Point[] points)
        {
            var geometry = new StreamGeometry();
            using (var path = geometry.Open())
            {
                path.BeginFigure(points[0], fill != null, true);
                for (int i = 1; i < points.Length; i++)
                    path.LineTo(points[i], true, false);
            }
            drawing.DrawGeometry(fill, outline, geometry);
        }

        private static Brush MakeSteamBrush()
        {
            var brush = new RadialGradientBrush(Color.FromArgb(173, 253, 234, 203), Colors.Transparent);
            brush.Freeze();
            return brush;
        }
    }
}
