using System;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace EnglishTypingPractice
{
    internal static class DenseCityArtwork
    {
        internal static void Apply(Window window, UiTheme theme)
        {
            if (theme != UiTheme.Cyberpunk && theme != UiTheme.Steampunk)
                return;

            bool steam = theme == UiTheme.Steampunk;
            string prefix = steam ? "Steampunk" : "Cyberpunk";
            var background = (Viewbox)window.FindName(prefix + "City");
            var header = (Viewbox)window.FindName(prefix + "Artwork");
            background.Height = 432;
            background.Child = DrawCity(1200, 432, steam, false);
            header.Child = DrawCity((int)header.Width, 124, steam, true);
        }

        private static Image DrawCity(int width, int height, bool steam, bool header)
        {
            var random = new Random(steam ? 28341 : 90127);
            double scale = header ? .43 : 1;
            double horizon = height * (header ? .87 : .76);
            var sky = steam ? Paint(80, 54, 35) : Paint(38, 19, 62);
            var dark = steam ? Paint(42, 29, 23) : Paint(15, 13, 30);
            var highlight = steam ? Paint(219, 165, 101) : Paint(92, 245, 234);
            var signal = steam ? Paint(245, 204, 144) : Paint(251, 100, 193);
            var drawing = new DrawingGroup();
            using (var dc = drawing.Open())
            {
                dc.DrawRectangle(Brushes.Transparent, null, new Rect(0, 0, width, height));
                dc.PushClip(new RectangleGeometry(new Rect(0, 0, width, height)));
                dc.DrawRectangle(new LinearGradientBrush(Color.FromArgb(0, sky.Color.R, sky.Color.G, sky.Color.B),
                    Color.FromArgb(header ? (byte)185 : (byte)122, sky.Color.R, sky.Color.G, sky.Color.B), 90),
                    null, new Rect(0, 0, width, height));
                double glowWidth = width * .43, glowHeight = height * .7;
                var glow = new RadialGradientBrush(steam ? Color.FromArgb(78, 213, 145, 86) :
                    Color.FromArgb(85, 216, 76, 172), Colors.Transparent);
                dc.DrawEllipse(glow, null, new Point(width * .58, horizon * .51), glowWidth, glowHeight);

                for (int depth = 0; depth < 4; depth++)
                {
                    double minWidth = (18 + depth * 6) * scale;
                    double maxWidth = (30 + depth * 13) * scale;
                    double baseline = horizon + (depth - 1) * 17 * scale;
                    double opening = steam ? (12 + depth * 18) * scale : 0;
                    var front = steam ? Paint(93 - depth * 12, 65 - depth * 9, 45 - depth * 5)
                                      : Paint(64 - depth * 12, 39 - depth * 8, 86 - depth * 15);
                    var side = steam ? Paint(67 - depth * 8, 47 - depth * 5, 36 - depth * 4)
                                     : Paint(48 - depth * 9, 30 - depth * 5, 65 - depth * 11);
                    var trim = steam ? Paint(120 - depth * 3, 87 - depth, 58 - depth)
                                     : Paint(108 - depth * 9, 65 - depth * 5, 117 - depth * 5);
                    var lit = steam ? Paint(206, 157, 100, 102 + depth * 15)
                                    : Paint(101, 212, 217, 78 + depth * 20);
                    var neon = steam ? Paint(242, 187, 123, 100 + depth * 15)
                                     : Paint(243, 87, 175, 83 + depth * 25);
                    var outline = new Pen(trim, (.5 + depth * .17) * scale);
                    for (double x = -maxWidth * .3; x < width; )
                    {
                        double w = Between(random, minWidth, maxWidth);
                        double bank = Math.Abs(x + w / 2 - width * .515) / (width / 2);
                        double rise = (28 + depth * 9 + (bank * (steam ? 72 : 33))) * scale;
                        double buildingHeight = Between(random, rise * .64, rise * 1.72);
                        if (depth == 1 && random.Next(6) == 0)
                            buildingHeight += 42 * scale;
                        buildingHeight = Math.Min(baseline - (header ? 11 : 42) * scale, buildingHeight);
                        if (!steam || Math.Abs(x + w / 2 - width * .515) > opening + w / 2)
                            DrawBuilding(dc, random, new Rect(x, baseline - buildingHeight, w, buildingHeight),
                                depth, scale, steam, front, side, outline, lit, neon);
                        x += w + Between(random, 1, 6 + depth * 2) * scale;
                    }
                }

                if (steam)
                    DrawCanal(dc, random, width, height, horizon, scale, dark, highlight, signal);
                else
                    DrawExpressway(dc, width, height, horizon, scale, dark, highlight, signal);
                if (header)
                    DrawFrame(dc, width, height, steam, highlight, signal);
                dc.Pop();
            }
            drawing.Freeze();
            var image = new DrawingImage(drawing);
            image.Freeze();
            return new Image { Source = image, Width = width, Height = height, IsHitTestVisible = false };
        }

        private static void DrawBuilding(DrawingContext dc, Random random, Rect bounds, int depth, double scale, bool steam,
            Brush front, Brush side, Pen outline, Brush lit, Brush signal)
        {
            double x = bounds.X, y = bounds.Y, w = bounds.Width, bottom = bounds.Bottom;
            double facet = w * .23, roof = Math.Min(w * .27, 14 * scale);
            bool stepped = random.Next(3) == 0;
            Point upper = new Point(x + w * .43, y - roof);
            var left = new Point(x, y);
            var right = new Point(x + w - facet, y);
            dc.DrawGeometry(side, outline, Shape(right, new Point(x + w, y + roof * .8),
                new Point(x + w, bottom), new Point(right.X, bottom)));
            if (steam && !stepped)
                dc.DrawGeometry(front, outline, Shape(left, upper, right, new Point(right.X, bottom), new Point(x, bottom)));
            else
            {
                double notch = stepped ? roof : roof * .46;
                dc.DrawGeometry(front, outline, Shape(left, new Point(x + w * .16, y), new Point(x + w * .16, y - notch),
                    new Point(right.X - w * .13, y - notch), new Point(right.X - w * .13, y), right,
                    new Point(right.X, bottom), new Point(x, bottom)));
            }
            if (random.Next(3) == 0)
            {
                double mast = Between(random, 5, steam ? 12 : 26) * scale;
                if (steam)
                    dc.DrawRectangle(side, outline, new Rect(x + w * .52, y - roof - mast * .65, 4 * scale, mast));
                else
                {
                    var top = new Point(upper.X, y - roof - mast);
                    dc.DrawLine(outline, new Point(upper.X, y - roof), top);
                    dc.DrawEllipse(signal, null, top, 1.5 * scale, 1.5 * scale);
                }
            }
            int columns = Math.Max(2, (int)((right.X - x) / (8 * scale)));
            double gap = (right.X - x) / (columns + 1);
            double windowWidth = Math.Min(4.5 * scale, gap * .57);
            double rowStep = (7.4 + depth * 1.15) * scale;
            bool sign = depth > 0 && random.Next(5) == 0;
            for (double row = y + rowStep; row < bottom - rowStep / 2; row += rowStep)
            {
                for (int column = 1; column <= columns; column++)
                {
                    if (random.Next(5) < 2)
                        continue;
                    double height = (steam ? 3.8 : 1.8) * scale;
                    Brush light = random.Next(4) == 0 ? signal : lit;
                    dc.DrawRectangle(light, null, new Rect(x + column * gap - windowWidth / 2, row,
                        windowWidth, height));
                }
                if (depth > 1 && random.Next(3) == 0)
                    dc.DrawRectangle(lit, null, new Rect(right.X + facet * .38, row + 1 * scale, facet * .35, 1.4 * scale));
            }
            if (sign)
            {
                if (steam)
                {
                    dc.DrawLine(outline, new Point(x, y + rowStep * 2), new Point(right.X, y + rowStep * 2));
                    dc.DrawEllipse(side, outline, new Point(x + gap * 1.2, bottom - rowStep), 2.5 * scale, 2.5 * scale);
                }
                else
                {
                    var neon = new Pen(signal, 1.3 * scale);
                    dc.DrawGeometry(null, neon, Shape(new Point(x + gap / 2, y + rowStep),
                        new Point(right.X - gap / 2, y + rowStep), new Point(right.X - gap / 2, y + rowStep * 1.5),
                        new Point(right.X - gap, y + rowStep * 1.9), new Point(x + gap / 2, y + rowStep * 1.9)));
                }
            }
        }

        private static void DrawCanal(DrawingContext dc, Random random, int width, int height, double horizon, double scale,
            Brush dark, Brush highlight, Brush signal)
        {
            double middle = width * .515, canal = width * .28;
            double shoreline = horizon + 25 * scale;
            dc.DrawGeometry(dark, null, Shape(new Point(middle - 16 * scale, horizon - 10 * scale),
                new Point(middle + 16 * scale, horizon - 10 * scale), new Point(middle + canal, height),
                new Point(middle - canal, height)));
            var reflection = new Pen(highlight, .8 * scale);
            reflection.Brush = Paint(193, 139, 91, 124);
            for (int i = 0; i < 34; i++)
            {
                double y = Between(random, horizon, height);
                double extent = Math.Max(7 * scale, (y - horizon + 10 * scale) * canal / (height - horizon));
                double start = middle + Between(random, -extent * .78, extent * .61);
                dc.DrawLine(reflection, new Point(start, y), new Point(start + Between(random, 5 * scale, extent * .33), y));
            }
            double bridge = 58 * scale;
            var rail = new Pen(highlight, 1.2 * scale);
            dc.DrawLine(rail, new Point(middle - bridge, shoreline), new Point(middle + bridge, shoreline));
            var bridgeLip = new Pen(signal, .75 * scale);
            for (int i = -6; i <= 6; i++)
                dc.DrawLine(bridgeLip, new Point(middle + bridge * i / 6, shoreline - 6 * scale),
                    new Point(middle + bridge * i / 6, shoreline));
            dc.DrawLine(bridgeLip, new Point(middle - bridge, shoreline - 6 * scale),
                new Point(middle + bridge, shoreline - 6 * scale));
            for (int bank = 0; bank < 2; bank++)
            {
                double edge = bank == 0 ? 0 : width;
                double sign = bank == 0 ? 1 : -1;
                double length = width * .34;
                for (int pipe = 0; pipe < 3; pipe++)
                {
                    double y = shoreline + (pipe * 9 + 6) * scale;
                    Point joint = new Point(edge + sign * (length - pipe * 13 * scale), y);
                    Point outlet = new Point(joint.X + sign * 14 * scale, y + 9 * scale);
                    dc.DrawLine(rail, new Point(edge, y), joint);
                    dc.DrawLine(rail, joint, outlet);
                    for (int rivet = 1; rivet <= 7; rivet++)
                    {
                        double x = edge + sign * length * rivet / 9;
                        dc.DrawLine(bridgeLip, new Point(x, y - 2.5 * scale), new Point(x, y + 2.5 * scale));
                    }
                }
            }
            DrawAirship(dc, width * .71, horizon * .29, 36 * scale, dark, reflection);
            DrawAirship(dc, width * .27, horizon * .4, 22 * scale, dark, reflection);
        }

        private static void DrawAirship(DrawingContext dc, double x, double y, double radius, Brush fill, Pen trim)
        {
            dc.DrawEllipse(fill, trim, new Point(x, y), radius, radius * .27);
            dc.DrawLine(trim, new Point(x - radius * .82, y), new Point(x + radius * .8, y));
            dc.DrawGeometry(fill, trim, Shape(new Point(x - radius * .26, y + radius * .22),
                new Point(x + radius * .23, y + radius * .22), new Point(x + radius * .12, y + radius * .42),
                new Point(x - radius * .13, y + radius * .42)));
        }

        private static void DrawExpressway(DrawingContext dc, int width, int height, double horizon, double scale,
            Brush dark, Brush highlight, Brush signal)
        {
            double y = horizon + 39 * scale;
            dc.DrawGeometry(dark, null, Shape(new Point(0, y), new Point(width * .28, y),
                new Point(width * .35, y - 13 * scale), new Point(width * .74, y - 13 * scale),
                new Point(width, y + 9 * scale), new Point(width, height), new Point(0, height)));
            var dimCyan = new Pen(Paint(88, 247, 228, 119), 1 * scale);
            var dimPink = new Pen(Paint(248, 99, 192, 109), 1.2 * scale);
            for (int lane = 0; lane < 3; lane++)
            {
                double row = y + (lane * 6 + 3) * scale;
                dc.DrawGeometry(null, lane == 1 ? dimPink : dimCyan, Line(new Point(0, row),
                    new Point(width * .29, row), new Point(width * .35, row - 12 * scale),
                    new Point(width * .73, row - 12 * scale), new Point(width, row + 9 * scale)));
            }
            for (int car = 1; car <= 11; car++)
            {
                double x = width * car / 13;
                double row = y + (x < width * .29 ? 3 : x < width * .73 ? -8 : 8) * scale;
                dc.DrawRectangle(car % 2 == 0 ? highlight : signal, null, new Rect(x, row, 5 * scale, 1.2 * scale));
            }
        }

        private static void DrawFrame(DrawingContext dc, int width, int height, bool steam, Brush highlight, Brush signal)
        {
            var top = new Pen(highlight, 1);
            var bottom = new Pen(signal, 1.5);
            dc.DrawGeometry(null, top, Line(new Point(12, 71), new Point(12, 23), new Point(29, 8),
                new Point(width * .61, 8), new Point(width * .66, 15), new Point(width - 25, 15)));
            dc.DrawGeometry(null, bottom, Line(new Point(26, height - 10), new Point(width * .47, height - 10),
                new Point(width * .51, height - 16), new Point(width - 23, height - 16),
                new Point(width - 10, height - 27), new Point(width - 10, 34)));
            for (int mark = 0; mark < 6; mark++)
            {
                double x = 34 + mark * 7;
                dc.DrawLine(steam ? top : bottom, new Point(x, 12), new Point(x + 3, 12));
            }
        }

        private static StreamGeometry Shape(params Point[] points) { return Outline(points, true); }
        private static StreamGeometry Line(params Point[] points) { return Outline(points, false); }

        private static StreamGeometry Outline(Point[] points, bool closed)
        {
            var geometry = new StreamGeometry();
            using (var context = geometry.Open())
            {
                context.BeginFigure(points[0], closed, closed);
                for (int i = 1; i < points.Length; i++)
                    context.LineTo(points[i], true, false);
            }
            return geometry;
        }

        private static double Between(Random random, double first, double last)
        {
            return first + (last - first) * random.NextDouble();
        }

        private static SolidColorBrush Paint(int red, int green, int blue, int alpha = 255)
        {
            var brush = new SolidColorBrush(Color.FromArgb((byte)alpha, (byte)red, (byte)green, (byte)blue));
            brush.Freeze();
            return brush;
        }
    }
}
