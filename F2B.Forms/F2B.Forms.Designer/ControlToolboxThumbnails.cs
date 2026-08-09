using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using F2B.Forms.Model;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// Fluent-style outline glyphs for the controls toolbox (icon above label).
    /// </summary>
    internal static class ControlToolboxThumbnails
    {
        public const int Width = 40;
        public const int Height = 32;

        private static readonly Color Ink = Color.FromArgb(55, 55, 55);
        private static readonly Color Accent = Color.FromArgb(0, 120, 212);
        private static readonly Color Muted = Color.FromArgb(150, 150, 150);
        private static readonly Color Soft = Color.FromArgb(232, 232, 232);

        public static Image Create(string controlType)
        {
            var bmp = new Bitmap(Width, Height, PixelFormat.Format32bppArgb);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.SmoothingMode = SmoothingMode.AntiAlias;
                g.PixelOffsetMode = PixelOffsetMode.HighQuality;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.FromArgb(250, 250, 250));

                switch (controlType ?? string.Empty)
                {
                    case FormControlType.Button:
                        DrawButton(g);
                        break;
                    case FormControlType.Label:
                        DrawLabel(g);
                        break;
                    case FormControlType.TextBox:
                        DrawTextBox(g);
                        break;
                    case FormControlType.TextArea:
                        DrawTextArea(g);
                        break;
                    case FormControlType.CheckBox:
                        DrawCheckBox(g);
                        break;
                    case FormControlType.ComboBox:
                        DrawComboBox(g);
                        break;
                    case FormControlType.DatePicker:
                        DrawDatePicker(g, includeTime: false);
                        break;
                    case FormControlType.DateTimePicker:
                        DrawDatePicker(g, includeTime: true);
                        break;
                    case FormControlType.Panel:
                        DrawPanel(g);
                        break;
                    case FormControlType.ScrollContainer:
                        DrawScrollContainer(g);
                        break;
                    case FormControlType.TableLayout:
                        DrawTableLayoutThumb(g);
                        break;
                    case FormControlType.DataGrid:
                        DrawDataGridThumb(g);
                        break;
                    case FormControlType.GroupBox:
                        DrawGroupBox(g);
                        break;
                    case FormControlType.TabControl:
                        DrawTabControl(g);
                        break;
                    case FormControlType.TabPage:
                        DrawTabPage(g);
                        break;
                    default:
                        StrokeRound(g, new Rectangle(6, 6, Width - 12, Height - 12), 4);
                        break;
                }
            }

            return bmp;
        }

        private static void DrawButton(Graphics g)
        {
            var r = new Rectangle(5, 8, Width - 10, Height - 16);
            FillRound(g, r, Accent, Color.FromArgb(0, 99, 177), 5);
        }

        private static void DrawLabel(Graphics g)
        {
            using (var font = new Font("Segoe UI", 13f, FontStyle.Bold, GraphicsUnit.Pixel))
            using (var brush = new SolidBrush(Ink))
            {
                var sf = new StringFormat
                {
                    Alignment = StringAlignment.Center,
                    LineAlignment = StringAlignment.Center
                };
                g.DrawString("A", font, brush, new RectangleF(0, 0, Width, Height - 4), sf);
            }

            using (var pen = new Pen(Accent, 2f) { EndCap = LineCap.Flat, StartCap = LineCap.Flat })
            {
                g.DrawLine(pen, 12, Height - 6, Width - 12, Height - 6);
            }
        }

        private static void DrawTextBox(Graphics g)
        {
            var r = new Rectangle(4, 10, Width - 8, 12);
            FillRound(g, r, Color.White, Muted, 3);
            using (var pen = new Pen(Accent, 1.6f))
            {
                g.DrawLine(pen, r.Left + 4, r.Top + 3, r.Left + 4, r.Bottom - 3);
            }
        }

        private static void DrawTextArea(Graphics g)
        {
            var r = new Rectangle(5, 4, Width - 10, Height - 8);
            FillRound(g, r, Color.White, Muted, 3);
            using (var pen = new Pen(Soft, 1.5f))
            {
                int y = r.Top + 6;
                for (int i = 0; i < 3; i++)
                {
                    int right = i == 2 ? r.Right - 12 : r.Right - 6;
                    g.DrawLine(pen, r.Left + 4, y, right, y);
                    y += 6;
                }
            }

            using (var brush = new SolidBrush(Soft))
            {
                g.FillRectangle(brush, r.Right - 5, r.Top + 4, 3, r.Height - 8);
            }

            using (var brush = new SolidBrush(Muted))
            {
                g.FillRectangle(brush, r.Right - 5, r.Top + 5, 3, 8);
            }
        }

        private static void DrawCheckBox(Graphics g)
        {
            int size = 14;
            var box = new Rectangle((Width - size) / 2, (Height - size) / 2, size, size);
            FillRound(g, box, Color.White, Muted, 3);
            using (var pen = new Pen(Accent, 2f))
            {
                pen.StartCap = LineCap.Round;
                pen.EndCap = LineCap.Round;
                g.DrawLines(pen, new[]
                {
                    new Point(box.Left + 3, box.Top + 7),
                    new Point(box.Left + 6, box.Top + 10),
                    new Point(box.Right - 3, box.Top + 3)
                });
            }
        }

        private static void DrawComboBox(Graphics g)
        {
            var r = new Rectangle(4, 10, Width - 8, 12);
            FillRound(g, r, Color.White, Muted, 3);
            int mid = r.Right - 8;
            using (var pen = new Pen(Soft))
            {
                g.DrawLine(pen, mid - 2, r.Top + 2, mid - 2, r.Bottom - 2);
            }

            Point[] tri =
            {
                new Point(mid, r.Top + 4),
                new Point(mid + 6, r.Top + 4),
                new Point(mid + 3, r.Top + 8)
            };
            using (var brush = new SolidBrush(Ink))
            {
                g.FillPolygon(brush, tri);
            }
        }

        private static void DrawDatePicker(Graphics g, bool includeTime)
        {
            var cal = new Rectangle(8, 5, 16, 16);
            FillRound(g, cal, Color.White, Muted, 2);
            using (var brush = new SolidBrush(Accent))
            {
                g.FillRectangle(brush, cal.Left + 1, cal.Top + 1, cal.Width - 2, 4);
            }

            using (var pen = new Pen(Muted))
            {
                g.DrawLine(pen, cal.Left + 4, cal.Top - 1, cal.Left + 4, cal.Top + 3);
                g.DrawLine(pen, cal.Right - 4, cal.Top - 1, cal.Right - 4, cal.Top + 3);
            }

            using (var brush = new SolidBrush(Ink))
            {
                g.FillRectangle(brush, cal.Left + 3, cal.Top + 7, 2, 2);
                g.FillRectangle(brush, cal.Left + 7, cal.Top + 7, 2, 2);
                g.FillRectangle(brush, cal.Left + 11, cal.Top + 7, 2, 2);
                g.FillRectangle(brush, cal.Left + 3, cal.Top + 11, 2, 2);
                g.FillRectangle(brush, cal.Left + 7, cal.Top + 11, 2, 2);
            }

            if (includeTime)
            {
                using (var pen = new Pen(Accent, 1.5f))
                {
                    g.DrawEllipse(pen, 26, 10, 10, 10);
                    g.DrawLine(pen, 31, 12, 31, 15);
                    g.DrawLine(pen, 31, 15, 33, 16);
                }
            }
        }

        private static void DrawPanel(Graphics g)
        {
            var r = new Rectangle(5, 5, Width - 10, Height - 10);
            FillRound(g, r, Color.FromArgb(248, 248, 248), Muted, 4);
            using (var pen = new Pen(Soft) { DashStyle = DashStyle.Dot })
            {
                g.DrawRectangle(pen, r.Left + 3, r.Top + 3, r.Width - 6, r.Height - 6);
            }
        }

        private static void DrawScrollContainer(Graphics g)
        {
            var r = new Rectangle(4, 4, Width - 8, Height - 8);
            FillRound(g, r, Color.White, Muted, 3);
            using (var brush = new SolidBrush(Soft))
            {
                g.FillRectangle(brush, r.Right - 6, r.Top + 2, 4, r.Height - 8);
                g.FillRectangle(brush, r.Left + 2, r.Bottom - 6, r.Width - 8, 4);
            }

            using (var brush = new SolidBrush(Accent))
            {
                g.FillRectangle(brush, r.Right - 5, r.Top + 4, 2, 8);
            }
        }

        private static void DrawTableLayoutThumb(Graphics g)
        {
            var r = new Rectangle(5, 5, Width - 10, Height - 10);
            FillRound(g, r, Color.White, Muted, 2);
            using (var pen = new Pen(Muted))
            {
                int midX = r.Left + r.Width / 2;
                int midY = r.Top + r.Height / 2;
                g.DrawLine(pen, midX, r.Top, midX, r.Bottom);
                g.DrawLine(pen, r.Left, midY, r.Right, midY);
            }
        }

        private static void DrawDataGridThumb(Graphics g)
        {
            var r = new Rectangle(4, 5, Width - 8, Height - 10);
            FillRound(g, r, Color.White, Muted, 2);
            using (var brush = new SolidBrush(Soft))
            {
                g.FillRectangle(brush, r.Left + 1, r.Top + 1, r.Width - 2, 6);
            }

            using (var pen = new Pen(Muted))
            {
                g.DrawLine(pen, r.Left, r.Top + 8, r.Right, r.Top + 8);
                int x = r.Left + r.Width / 3;
                g.DrawLine(pen, x, r.Top, x, r.Bottom);
                x = r.Left + 2 * r.Width / 3;
                g.DrawLine(pen, x, r.Top, x, r.Bottom);
            }
        }

        private static void DrawGroupBox(Graphics g)
        {
            var r = new Rectangle(5, 8, Width - 10, Height - 12);
            int gapL = r.Left + 7;
            int gapR = r.Left + 18;
            using (var pen = new Pen(Muted))
            {
                // top with title gap
                g.DrawLine(pen, r.Left, r.Top, gapL, r.Top);
                g.DrawLine(pen, gapR, r.Top, r.Right - 1, r.Top);
                g.DrawLine(pen, r.Left, r.Top, r.Left, r.Bottom - 1);
                g.DrawLine(pen, r.Right - 1, r.Top, r.Right - 1, r.Bottom - 1);
                g.DrawLine(pen, r.Left, r.Bottom - 1, r.Right - 1, r.Bottom - 1);
            }

            using (var pen = new Pen(Accent, 2f))
            {
                g.DrawLine(pen, gapL + 1, r.Top, gapR - 1, r.Top);
            }
        }

        private static void DrawTabControl(Graphics g)
        {
            int bodyTop = 12;
            var body = new Rectangle(4, bodyTop, Width - 8, Height - bodyTop - 3);
            FillRound(g, body, Color.White, Muted, 3);

            int tabW = 10;
            int x = body.Left + 2;
            for (int i = 0; i < 3; i++)
            {
                bool selected = i == 0;
                var tab = new Rectangle(x, 3, tabW, 10);
                FillRound(g, tab, selected ? Color.White : Soft, Muted, 2);
                if (selected)
                {
                    using (var pen = new Pen(Color.White, 2f))
                    {
                        g.DrawLine(pen, tab.Left + 1, body.Top, tab.Right - 1, body.Top);
                    }

                    using (var pen = new Pen(Accent, 2f))
                    {
                        g.DrawLine(pen, tab.Left + 1, tab.Bottom - 1, tab.Right - 1, tab.Bottom - 1);
                    }
                }

                x += tabW + 2;
            }
        }

        private static void DrawTabPage(Graphics g)
        {
            int bodyTop = 11;
            var body = new Rectangle(6, bodyTop, Width - 12, Height - bodyTop - 3);
            FillRound(g, body, Color.White, Muted, 3);

            var tab = new Rectangle(body.Left + 4, 2, 14, 10);
            FillRound(g, tab, Color.White, Muted, 2);
            using (var pen = new Pen(Color.White, 2f))
            {
                g.DrawLine(pen, tab.Left + 1, body.Top, tab.Right - 1, body.Top);
            }

            using (var pen = new Pen(Accent, 2f))
            {
                g.DrawLine(pen, tab.Left + 1, tab.Bottom - 1, tab.Right - 1, tab.Bottom - 1);
            }
        }

        private static void StrokeRound(Graphics g, Rectangle r, int radius)
        {
            using (GraphicsPath path = Rounded(r, radius))
            using (var pen = new Pen(Muted))
            {
                g.DrawPath(pen, path);
            }
        }

        private static void FillRound(Graphics g, Rectangle r, Color fill, Color border, int radius)
        {
            using (GraphicsPath path = Rounded(r, radius))
            {
                using (var brush = new SolidBrush(fill))
                {
                    g.FillPath(brush, path);
                }

                using (var pen = new Pen(border))
                {
                    g.DrawPath(pen, path);
                }
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            if (d <= 0 || d > r.Width || d > r.Height)
            {
                path.AddRectangle(r);
                return path;
            }

            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }
    }
}
