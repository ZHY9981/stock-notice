using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace StockNotice
{
    public enum AppGlyph
    {
        Add,
        Refresh,
        Edit,
        Delete,
        Pause,
        Play,
        Settings,
        Search,
        List,
        Calendar,
        More
    }

    public static class UiIconFactory
    {
        public static Bitmap Create(AppGlyph glyph, Color color)
        {
            int size = AppResources.S(16);
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                float scale = size / 16f;
                using (Pen pen = new Pen(color, Math.Max(1.25f, 1.45f * scale)))
                {
                    pen.StartCap = LineCap.Round;
                    pen.EndCap = LineCap.Round;
                    pen.LineJoin = LineJoin.Round;
                    DrawGlyph(graphics, pen, glyph, scale);
                }

                if (glyph == AppGlyph.More)
                {
                    using (SolidBrush brush = new SolidBrush(color))
                    {
                        graphics.FillEllipse(brush, 3.2f * scale, 7f * scale, 1.8f * scale, 1.8f * scale);
                        graphics.FillEllipse(brush, 7.1f * scale, 7f * scale, 1.8f * scale, 1.8f * scale);
                        graphics.FillEllipse(brush, 11f * scale, 7f * scale, 1.8f * scale, 1.8f * scale);
                    }
                }
            }
            return bitmap;
        }

        private static void DrawGlyph(Graphics graphics, Pen pen, AppGlyph glyph, float scale)
        {
            Func<float, float> S = delegate(float value) { return value * scale; };

            if (glyph == AppGlyph.Add)
            {
                graphics.DrawLine(pen, S(8), S(3), S(8), S(13));
                graphics.DrawLine(pen, S(3), S(8), S(13), S(8));
            }
            else if (glyph == AppGlyph.Refresh)
            {
                graphics.DrawArc(pen, S(2.5f), S(2.5f), S(11), S(11), -55, 285);
                graphics.DrawLine(pen, S(12.5f), S(2.3f), S(13.1f), S(6));
                graphics.DrawLine(pen, S(13.1f), S(6), S(9.5f), S(5.2f));
            }
            else if (glyph == AppGlyph.Edit)
            {
                graphics.DrawLine(pen, S(3), S(13), S(5.8f), S(12.3f));
                graphics.DrawLine(pen, S(5.8f), S(12.3f), S(13.1f), S(5));
                graphics.DrawLine(pen, S(11.1f), S(3), S(13.1f), S(5));
                graphics.DrawLine(pen, S(3.2f), S(13.2f), S(3.2f), S(15));
                graphics.DrawLine(pen, S(5), S(15), S(5), S(14.1f));
            }
            else if (glyph == AppGlyph.Delete)
            {
                graphics.DrawLine(pen, S(3), S(4.5f), S(13), S(4.5f));
                graphics.DrawRectangle(pen, S(5), S(2.5f), S(6), S(3));
                graphics.DrawLine(pen, S(5.5f), S(4.5f), S(6.3f), S(14));
                graphics.DrawLine(pen, S(10.5f), S(4.5f), S(9.7f), S(14));
                graphics.DrawLine(pen, S(6.3f), S(14), S(9.7f), S(14));
                graphics.DrawLine(pen, S(7), S(7), S(7.2f), S(11.5f));
                graphics.DrawLine(pen, S(9), S(7), S(8.8f), S(11.5f));
            }
            else if (glyph == AppGlyph.Pause)
            {
                graphics.DrawLine(pen, S(6), S(4), S(6), S(12));
                graphics.DrawLine(pen, S(10), S(4), S(10), S(12));
            }
            else if (glyph == AppGlyph.Play)
            {
                PointF[] points = new PointF[]
                {
                    new PointF(S(5.5f), S(3.5f)),
                    new PointF(S(12.2f), S(8)),
                    new PointF(S(5.5f), S(12.5f))
                };
                graphics.DrawPolygon(pen, points);
            }
            else if (glyph == AppGlyph.Settings)
            {
                graphics.DrawEllipse(pen, S(5), S(5), S(6), S(6));
                graphics.DrawEllipse(pen, S(7), S(7), S(2), S(2));
                for (int i = 0; i < 8; i++)
                {
                    double angle = Math.PI * i / 4d;
                    float x1 = S(8f + (float)Math.Cos(angle) * 4.3f);
                    float y1 = S(8f + (float)Math.Sin(angle) * 4.3f);
                    float x2 = S(8f + (float)Math.Cos(angle) * 6.1f);
                    float y2 = S(8f + (float)Math.Sin(angle) * 6.1f);
                    graphics.DrawLine(pen, x1, y1, x2, y2);
                }
            }
            else if (glyph == AppGlyph.Search)
            {
                graphics.DrawEllipse(pen, S(3), S(3), S(7.5f), S(7.5f));
                graphics.DrawLine(pen, S(9.5f), S(9.5f), S(13.2f), S(13.2f));
            }
            else if (glyph == AppGlyph.List)
            {
                graphics.DrawLine(pen, S(5.5f), S(4), S(13), S(4));
                graphics.DrawLine(pen, S(5.5f), S(8), S(13), S(8));
                graphics.DrawLine(pen, S(5.5f), S(12), S(13), S(12));
                graphics.DrawEllipse(pen, S(3), S(3.2f), S(1.2f), S(1.2f));
                graphics.DrawEllipse(pen, S(3), S(7.2f), S(1.2f), S(1.2f));
                graphics.DrawEllipse(pen, S(3), S(11.2f), S(1.2f), S(1.2f));
            }
            else if (glyph == AppGlyph.Calendar)
            {
                graphics.DrawRectangle(pen, S(2.8f), S(4), S(10.4f), S(9));
                graphics.DrawLine(pen, S(2.8f), S(7), S(13.2f), S(7));
                graphics.DrawLine(pen, S(5.5f), S(2.5f), S(5.5f), S(5));
                graphics.DrawLine(pen, S(10.5f), S(2.5f), S(10.5f), S(5));
            }
        }
    }

    public class RoundedPanel : Panel
    {
        public int CornerRadius { get; set; }
        public Color BorderColor { get; set; }
        public bool DrawBorder { get; set; }

        public RoundedPanel()
        {
            CornerRadius = AppResources.S(6);
            BorderColor = AppResources.Border;
            DrawBorder = true;
            DoubleBuffered = true;
            ResizeRedraw = true;
        }

        protected override void OnResize(EventArgs eventargs)
        {
            base.OnResize(eventargs);
            UpdateRegion();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            if (!DrawBorder || Width <= 1 || Height <= 1) return;

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, Width - 1, Height - 1), CornerRadius))
            using (Pen pen = new Pen(BorderColor))
            {
                e.Graphics.DrawPath(pen, path);
            }
        }

        private void UpdateRegion()
        {
            if (Width <= 0 || Height <= 0) return;
            using (GraphicsPath path = CreateRoundedPath(new Rectangle(0, 0, Width, Height), CornerRadius))
            {
                Region old = Region;
                Region = new Region(path);
                if (old != null) old.Dispose();
            }
        }

        private static GraphicsPath CreateRoundedPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class StatusPill : Label
    {
        public Color PillColor { get; set; }
        public Color PillBorderColor { get; set; }
        public bool DrawPillBorder { get; set; }

        public StatusPill()
        {
            AutoSize = true;
            PillColor = AppResources.FieldBackground;
            PillBorderColor = AppResources.Border;
            DrawPillBorder = false;
            Padding = AppResources.SPad(9, 3, 9, 3);
            Font = AppResources.Font(8.5F, FontStyle.Bold);
            TextAlign = ContentAlignment.MiddleCenter;
            DoubleBuffered = true;
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = new Rectangle(0, 0, Width - 1, Height - 1);
            int radius = Math.Min(Height, AppResources.S(12));
            using (GraphicsPath path = CreatePillPath(bounds, radius))
            using (SolidBrush fill = new SolidBrush(PillColor))
            {
                e.Graphics.FillPath(fill, path);
                if (DrawPillBorder)
                {
                    using (Pen pen = new Pen(PillBorderColor))
                    {
                        e.Graphics.DrawPath(pen, path);
                    }
                }
            }

            TextRenderer.DrawText(
                e.Graphics,
                Text,
                Font,
                ClientRectangle,
                ForeColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.NoPadding);
        }

        private static GraphicsPath CreatePillPath(Rectangle bounds, int radius)
        {
            GraphicsPath path = new GraphicsPath();
            int diameter = Math.Max(1, Math.Min(radius * 2, Math.Min(bounds.Width, bounds.Height)));
            Rectangle arc = new Rectangle(bounds.Location, new Size(diameter, diameter));
            path.AddArc(arc, 180, 90);
            arc.X = bounds.Right - diameter;
            path.AddArc(arc, 270, 90);
            arc.Y = bounds.Bottom - diameter;
            path.AddArc(arc, 0, 90);
            arc.X = bounds.Left;
            path.AddArc(arc, 90, 90);
            path.CloseFigure();
            return path;
        }
    }

    public class QuoteSparkline : Control
    {
        private List<Quote> _quotes;
        private decimal _previousClose;

        public QuoteSparkline()
        {
            DoubleBuffered = true;
            ResizeRedraw = true;
            BackColor = AppResources.FieldBackground;
            _quotes = new List<Quote>();
        }

        public void SetQuotes(IList<Quote> quotes, decimal previousClose)
        {
            _quotes = new List<Quote>();
            if (quotes != null)
            {
                for (int i = 0; i < quotes.Count; i++)
                {
                    if (quotes[i] != null && quotes[i].Price > 0m) _quotes.Add(quotes[i]);
                }
            }
            _previousClose = previousClose;
            Invalidate();
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            Rectangle bounds = ClientRectangle;
            if (bounds.Width < 20 || bounds.Height < 20) return;

            using (SolidBrush background = new SolidBrush(AppResources.FieldBackground))
            {
                e.Graphics.FillRectangle(background, bounds);
            }

            if (_quotes.Count == 0)
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    "等待价格样本",
                    AppResources.Font(8.5F, FontStyle.Regular),
                    bounds,
                    AppResources.Muted,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            List<decimal> values = new List<decimal>();
            if (_previousClose > 0m) values.Add(_previousClose);
            for (int i = 0; i < _quotes.Count; i++) values.Add(_quotes[i].Price);
            if (values.Count == 1) values.Add(values[0]);

            decimal minimum = values[0];
            decimal maximum = values[0];
            for (int i = 1; i < values.Count; i++)
            {
                if (values[i] < minimum) minimum = values[i];
                if (values[i] > maximum) maximum = values[i];
            }
            if (maximum == minimum)
            {
                maximum += 0.01m;
                minimum -= 0.01m;
            }

            int left = AppResources.S(8);
            int right = bounds.Width - AppResources.S(8);
            int top = AppResources.S(8);
            int bottom = bounds.Height - AppResources.S(8);
            PointF[] points = new PointF[values.Count];
            for (int i = 0; i < values.Count; i++)
            {
                float x = values.Count == 1
                    ? left
                    : left + (right - left) * i / (float)(values.Count - 1);
                float ratio = (float)((values[i] - minimum) / (maximum - minimum));
                float y = bottom - ratio * (bottom - top);
                points[i] = new PointF(x, y);
            }

            Color lineColor = _previousClose > 0m && _quotes[_quotes.Count - 1].Price < _previousClose
                ? AppResources.Down
                : AppResources.Up;
            using (SolidBrush fill = new SolidBrush(Color.FromArgb(18, lineColor)))
            using (GraphicsPath fillPath = new GraphicsPath())
            {
                fillPath.AddLines(points);
                fillPath.AddLine(points[points.Length - 1].X, bottom, points[0].X, bottom);
                fillPath.CloseFigure();
                e.Graphics.FillPath(fill, fillPath);
            }

            using (Pen pen = new Pen(lineColor, Math.Max(1.5f, AppResources.SF(1.8f))))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                pen.LineJoin = LineJoin.Round;
                if (points.Length > 1) e.Graphics.DrawLines(pen, points);
                e.Graphics.DrawEllipse(
                    pen,
                    points[points.Length - 1].X - AppResources.SF(2.2f),
                    points[points.Length - 1].Y - AppResources.SF(2.2f),
                    AppResources.SF(4.4f),
                    AppResources.SF(4.4f));
            }
        }
    }

    public class BufferedDataGridView : DataGridView
    {
        public BufferedDataGridView()
        {
            DoubleBuffered = true;
        }
    }
}
