using System;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace StockNotice
{
    // Every colour used by the interface lives here. To restyle the app,
    // change a palette below instead of hunting through the forms.
    // Pick a palette at startup with --theme=default|ink|graphite
    // or the STOCKNOTICE_THEME environment variable.
    public static class AppResources
    {
        public static readonly string ThemeName;

        public static readonly Color Background;
        public static readonly Color Surface;
        public static readonly Color Header;
        public static readonly Color HeaderMuted;
        public static readonly Color HeaderText;
        public static readonly Color Text;
        public static readonly Color Muted;
        public static readonly Color Border;
        public static readonly Color Up;
        public static readonly Color Down;
        public static readonly Color Accent;
        public static readonly Color AccentHover;
        public static readonly Color Secondary;

        public static readonly Color GridHeaderBackground;
        public static readonly Color GridRowSelection;
        public static readonly Color GridAltRow;
        public static readonly Color ButtonBorder;
        public static readonly Color FieldBackground;
        public static readonly Color OnAccent;
        public static readonly Color SessionActive;
        public static readonly Color IconCircle;
        public static readonly Color IconGlyph;

        static AppResources()
        {
            string theme = ResolveRequestedTheme();
            ThemeName = theme;

            Palette palette;
            if (theme == "ink") palette = Ink();
            else if (theme == "graphite") palette = Graphite();
            else palette = Default();

            Background = palette.Background;
            Surface = palette.Surface;
            Header = palette.Header;
            HeaderMuted = palette.HeaderMuted;
            HeaderText = palette.HeaderText;
            Text = palette.Text;
            Muted = palette.Muted;
            Border = palette.Border;
            Up = palette.Up;
            Down = palette.Down;
            Accent = palette.Accent;
            AccentHover = palette.AccentHover;
            Secondary = palette.Secondary;
            GridHeaderBackground = palette.GridHeaderBackground;
            GridRowSelection = palette.GridRowSelection;
            GridAltRow = palette.GridAltRow;
            ButtonBorder = palette.ButtonBorder;
            FieldBackground = palette.FieldBackground;
            OnAccent = palette.OnAccent;
            SessionActive = palette.SessionActive;
            IconCircle = palette.IconCircle;
            IconGlyph = palette.IconGlyph;
        }

        private sealed class Palette
        {
            public Color Background;
            public Color Surface;
            public Color Header;
            public Color HeaderMuted;
            public Color HeaderText;
            public Color Text;
            public Color Muted;
            public Color Border;
            public Color Up;
            public Color Down;
            public Color Accent;
            public Color AccentHover;
            public Color Secondary;
            public Color GridHeaderBackground;
            public Color GridRowSelection;
            public Color GridAltRow;
            public Color ButtonBorder;
            public Color FieldBackground;
            public Color OnAccent;
            public Color SessionActive;
            public Color IconCircle;
            public Color IconGlyph;
        }

        private static Palette Default()
        {
            Palette p = new Palette();
            p.Background = Color.FromArgb(244, 246, 248);
            p.Surface = Color.White;
            p.Header = Color.FromArgb(24, 32, 46);
            p.HeaderMuted = Color.FromArgb(177, 186, 201);
            p.HeaderText = Color.White;
            p.Text = Color.FromArgb(31, 41, 55);
            p.Muted = Color.FromArgb(107, 114, 128);
            p.Border = Color.FromArgb(226, 232, 240);
            p.Up = Color.FromArgb(210, 46, 58);
            p.Down = Color.FromArgb(7, 148, 85);
            p.Accent = Color.FromArgb(214, 62, 53);
            p.AccentHover = Color.FromArgb(188, 47, 40);
            p.Secondary = Color.FromArgb(72, 84, 103);
            p.GridHeaderBackground = Color.FromArgb(248, 250, 252);
            p.GridRowSelection = Color.FromArgb(255, 241, 240);
            p.GridAltRow = Color.FromArgb(252, 253, 254);
            p.ButtonBorder = Color.FromArgb(203, 213, 225);
            p.FieldBackground = Color.FromArgb(248, 250, 252);
            p.OnAccent = Color.White;
            p.SessionActive = Color.FromArgb(105, 240, 174);
            p.IconCircle = Color.FromArgb(24, 32, 46);
            p.IconGlyph = Color.White;
            return p;
        }

        private static Palette Ink()
        {
            Palette p = new Palette();
            p.Background = Color.FromArgb(18, 20, 24);
            p.Surface = Color.FromArgb(26, 29, 34);
            p.Header = Color.FromArgb(12, 14, 17);
            p.HeaderMuted = Color.FromArgb(140, 148, 160);
            p.HeaderText = Color.FromArgb(236, 239, 243);
            p.Text = Color.FromArgb(226, 230, 236);
            p.Muted = Color.FromArgb(148, 156, 168);
            p.Border = Color.FromArgb(44, 49, 58);
            p.Up = Color.FromArgb(255, 92, 92);
            p.Down = Color.FromArgb(46, 204, 113);
            p.Accent = Color.FromArgb(224, 74, 64);
            p.AccentHover = Color.FromArgb(240, 102, 92);
            p.Secondary = Color.FromArgb(150, 158, 170);
            p.GridHeaderBackground = Color.FromArgb(32, 36, 42);
            p.GridRowSelection = Color.FromArgb(62, 40, 42);
            p.GridAltRow = Color.FromArgb(31, 34, 40);
            p.ButtonBorder = Color.FromArgb(62, 68, 78);
            p.FieldBackground = Color.FromArgb(34, 38, 44);
            p.OnAccent = Color.White;
            p.SessionActive = Color.FromArgb(90, 220, 160);
            p.IconCircle = Color.FromArgb(236, 239, 243);
            p.IconGlyph = Color.FromArgb(18, 20, 24);
            return p;
        }

        private static Palette Graphite()
        {
            Palette p = new Palette();
            p.Background = Color.FromArgb(236, 238, 241);
            p.Surface = Color.White;
            p.Header = Color.FromArgb(22, 24, 28);
            p.HeaderMuted = Color.FromArgb(160, 166, 175);
            p.HeaderText = Color.White;
            p.Text = Color.FromArgb(17, 19, 23);
            p.Muted = Color.FromArgb(95, 102, 112);
            p.Border = Color.FromArgb(202, 208, 216);
            p.Up = Color.FromArgb(200, 32, 45);
            p.Down = Color.FromArgb(6, 130, 74);
            p.Accent = Color.FromArgb(200, 32, 45);
            p.AccentHover = Color.FromArgb(168, 24, 36);
            p.Secondary = Color.FromArgb(60, 68, 80);
            p.GridHeaderBackground = Color.FromArgb(238, 240, 243);
            p.GridRowSelection = Color.FromArgb(236, 240, 246);
            p.GridAltRow = Color.FromArgb(249, 250, 252);
            p.ButtonBorder = Color.FromArgb(176, 184, 194);
            p.FieldBackground = Color.FromArgb(244, 246, 249);
            p.OnAccent = Color.White;
            p.SessionActive = Color.FromArgb(22, 140, 90);
            p.IconCircle = Color.FromArgb(22, 24, 28);
            p.IconGlyph = Color.White;
            return p;
        }

        private static string ResolveRequestedTheme()
        {
            string value = null;

            string[] args = Environment.GetCommandLineArgs();
            for (int i = 0; i < args.Length; i++)
            {
                string arg = args[i];
                if (arg != null && arg.StartsWith("--theme=", StringComparison.OrdinalIgnoreCase))
                {
                    value = arg.Substring("--theme=".Length);
                    break;
                }
            }

            if (string.IsNullOrEmpty(value))
            {
                value = Environment.GetEnvironmentVariable("STOCKNOTICE_THEME");
            }
            if (string.IsNullOrEmpty(value)) return "default";

            value = value.Trim().ToLowerInvariant();
            if (value == "ink" || value == "graphite" || value == "default") return value;
            return "default";
        }

        public static Font Font(float size, FontStyle style)
        {
            return new Font("Microsoft YaHei UI", size, style, GraphicsUnit.Point);
        }

        public static Icon CreateAppIcon()
        {
            using (Bitmap bitmap = CreateIconBitmap(64))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        public static Icon CreateTrayIcon()
        {
            using (Bitmap bitmap = CreateIconBitmap(32))
            {
                IntPtr handle = bitmap.GetHicon();
                try
                {
                    using (Icon temporary = Icon.FromHandle(handle))
                    {
                        return (Icon)temporary.Clone();
                    }
                }
                finally
                {
                    DestroyIcon(handle);
                }
            }
        }

        private static Bitmap CreateIconBitmap(int size)
        {
            Bitmap bitmap = new Bitmap(size, size);
            using (Graphics graphics = Graphics.FromImage(bitmap))
            {
                graphics.SmoothingMode = SmoothingMode.AntiAlias;
                graphics.Clear(Color.Transparent);

                using (SolidBrush background = new SolidBrush(IconCircle))
                {
                    graphics.FillEllipse(background, 0, 0, size - 1, size - 1);
                }

                using (Pen chartPen = new Pen(IconGlyph, Math.Max(2f, size / 12f)))
                {
                    chartPen.StartCap = LineCap.Round;
                    chartPen.EndCap = LineCap.Round;
                    chartPen.LineJoin = LineJoin.Round;
                    PointF[] points = new PointF[]
                    {
                        new PointF(size * 0.20f, size * 0.62f),
                        new PointF(size * 0.38f, size * 0.46f),
                        new PointF(size * 0.55f, size * 0.56f),
                        new PointF(size * 0.78f, size * 0.28f)
                    };
                    graphics.DrawLines(chartPen, points);
                }

                using (SolidBrush accent = new SolidBrush(Accent))
                {
                    float dot = Math.Max(4f, size * 0.16f);
                    graphics.FillEllipse(accent, size * 0.70f, size * 0.20f, dot, dot);
                }
            }
            return bitmap;
        }

        public static string FormatPrice(decimal value)
        {
            if (value <= 0m) return "--";
            return value.ToString("0.00");
        }

        public static string FormatPercent(decimal value)
        {
            return (value >= 0m ? "+" : string.Empty) + value.ToString("0.00") + "%";
        }

        public static string FormatTime(DateTime value)
        {
            if (value == DateTime.MinValue) return "--";
            return value.ToString("HH:mm:ss");
        }

        public static string FormatDateTime(DateTime value)
        {
            if (value == DateTime.MinValue) return "--";
            return value.ToString("MM-dd HH:mm:ss");
        }

        public static Color ChangeColor(decimal value)
        {
            if (value > 0m) return Up;
            if (value < 0m) return Down;
            return Muted;
        }

        public static string DirectionText(string direction)
        {
            return direction == "down" ? "下跌" : "上涨";
        }

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern bool DestroyIcon(IntPtr handle);
    }
}
