using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Text;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using F2B.Forms.Model;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// Paints design-time controls using WinForms / VisualStyles look-alikes
    /// so Button/TextBox/CheckBox etc. are visually distinct on the canvas.
    /// </summary>
    internal enum ResizeHandle
    {
        None = 0,
        Move,
        N,
        NE,
        E,
        SE,
        S,
        SW,
        W,
        NW
    }

    internal static class DesignSurfacePainter
    {
        public const int GripSize = 6;
        public const int GripHitInflate = 3;

        public static void Draw(
            Graphics g,
            Rectangle bounds,
            string type,
            string text,
            bool enabled,
            bool isChecked,
            bool selected,
            TextHAlign textAlignH,
            TextVAlign textAlignV,
            Font font,
            Color foreColor,
            Color backColor,
            bool showResizeHandles = true)
        {
            if (g == null || bounds.Width <= 0 || bounds.Height <= 0)
            {
                return;
            }

            g.SmoothingMode = SmoothingMode.None;
            g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;

            string displayText = text ?? string.Empty;
            string normalized = type == null ? string.Empty : type.Trim();
            Font useFont = font ?? SystemFonts.DefaultFont;
            Color useFore = foreColor.IsEmpty
                ? (enabled ? SystemColors.ControlText : SystemColors.GrayText)
                : (enabled ? foreColor : SystemColors.GrayText);
            TextFormatFlags alignFlags = TextAlignUtil.ToTextFormatFlags(textAlignH, textAlignV, wordBreak: false);
            TextFormatFlags multilineFlags = TextAlignUtil.ToTextFormatFlags(textAlignH, textAlignV, wordBreak: true);

            switch (normalized)
            {
                case FormControlType.Button:
                    DrawButton(
                        g,
                        bounds,
                        displayText,
                        enabled,
                        TextAlignUtil.ToTextFormatFlags(textAlignH, textAlignV, wordBreak: true),
                        useFont,
                        useFore,
                        backColor);
                    break;
                case FormControlType.Label:
                    DrawLabel(
                        g,
                        bounds,
                        displayText,
                        enabled,
                        TextAlignUtil.ToTextFormatFlags(textAlignH, textAlignV, wordBreak: true),
                        useFont,
                        useFore,
                        backColor);
                    break;
                case FormControlType.TextBox:
                    DrawTextBox(g, bounds, displayText, enabled, multiline: false, multilineFlags, useFont, useFore, backColor);
                    break;
                case FormControlType.TextArea:
                    DrawTextBox(g, bounds, displayText, enabled, multiline: true, multilineFlags, useFont, useFore, backColor);
                    break;
                case FormControlType.CheckBox:
                    DrawCheckBox(g, bounds, displayText, enabled, isChecked, alignFlags, useFont, useFore, backColor);
                    break;
                case FormControlType.RadioButton:
                    DrawRadioButton(g, bounds, displayText, enabled, isChecked, alignFlags, useFont, useFore, backColor);
                    break;
                case FormControlType.ComboBox:
                    DrawComboBox(g, bounds, displayText, enabled, useFont, useFore, backColor);
                    break;
                case FormControlType.ListBox:
                    DrawListBox(g, bounds, displayText, enabled, checkedStyle: false, useFont, useFore, backColor);
                    break;
                case FormControlType.CheckedListBox:
                    DrawListBox(g, bounds, displayText, enabled, checkedStyle: true, useFont, useFore, backColor);
                    break;
                case FormControlType.MaskedTextBox:
                    DrawTextBox(g, bounds, displayText, enabled, multiline: false, multilineFlags, useFont, useFore, backColor);
                    break;
                case FormControlType.NumericUpDown:
                    DrawNumericUpDown(g, bounds, displayText, enabled, useFont, useFore, backColor);
                    break;
                case FormControlType.PictureBox:
                    DrawPictureBox(g, bounds, enabled, backColor);
                    break;
                case FormControlType.DatePicker:
                    DrawDatePicker(
                        g,
                        bounds,
                        string.IsNullOrEmpty(displayText) ? "yyyy-MM-dd" : displayText,
                        enabled,
                        useFont,
                        useFore,
                        backColor,
                        includeTime: false);
                    break;
                case FormControlType.DateTimePicker:
                    DrawDatePicker(
                        g,
                        bounds,
                        string.IsNullOrEmpty(displayText) ? "yyyy-MM-dd HH:mm:ss" : displayText,
                        enabled,
                        useFont,
                        useFore,
                        backColor,
                        includeTime: true);
                    break;
                case FormControlType.Panel:
                    DrawPanel(g, bounds, backColor);
                    break;
                case FormControlType.ScrollContainer:
                    DrawScrollContainer(g, bounds, backColor);
                    break;
                case FormControlType.TableLayout:
                    DrawTableLayout(g, bounds, backColor);
                    break;
                case FormControlType.DataGrid:
                    DrawDataGrid(g, bounds, useFont, useFore, backColor);
                    break;
                case FormControlType.GroupBox:
                    DrawGroupBox(g, bounds, displayText, enabled, useFont, useFore, backColor);
                    break;
                case FormControlType.TabControl:
                    // Tab headers drawn by MainForm with real page titles.
                    DrawTabControlShell(g, bounds, backColor);
                    break;
                case FormControlType.TabPage:
                    // Logical page — chrome drawn by parent TabControl.
                    break;
                default:
                    DrawFallback(g, bounds, displayText, useFont, useFore);
                    break;
            }

            if (selected)
            {
                DrawSelectionChrome(g, bounds, showResizeHandles);
            }
        }

        public static void DrawSelectionChrome(Graphics g, Rectangle bounds, bool showResizeHandles = true)
        {
            using (var pen = new Pen(Color.DodgerBlue, 2))
            {
                g.DrawRectangle(pen, bounds);
            }

            if (!showResizeHandles)
            {
                return;
            }

            foreach (ResizeHandle handle in GetResizeHandles())
            {
                DrawGrip(g, GetGripRectangle(bounds, handle));
            }
        }

        public static ResizeHandle HitTestHandle(Rectangle bounds, Point point)
        {
            // Prefer grips over body so edges are easy to grab.
            foreach (ResizeHandle handle in GetResizeHandles())
            {
                if (GetGripHitRectangle(bounds, handle).Contains(point))
                {
                    return handle;
                }
            }

            if (bounds.Contains(point))
            {
                return ResizeHandle.Move;
            }

            return ResizeHandle.None;
        }

        public static Cursor GetCursor(ResizeHandle handle)
        {
            switch (handle)
            {
                case ResizeHandle.N:
                case ResizeHandle.S:
                    return Cursors.SizeNS;
                case ResizeHandle.E:
                case ResizeHandle.W:
                    return Cursors.SizeWE;
                case ResizeHandle.NE:
                case ResizeHandle.SW:
                    return Cursors.SizeNESW;
                case ResizeHandle.NW:
                case ResizeHandle.SE:
                    return Cursors.SizeNWSE;
                case ResizeHandle.Move:
                    return Cursors.SizeAll;
                default:
                    return Cursors.Default;
            }
        }

        private static IEnumerable<ResizeHandle> GetResizeHandles()
        {
            yield return ResizeHandle.NW;
            yield return ResizeHandle.N;
            yield return ResizeHandle.NE;
            yield return ResizeHandle.E;
            yield return ResizeHandle.SE;
            yield return ResizeHandle.S;
            yield return ResizeHandle.SW;
            yield return ResizeHandle.W;
        }

        public static Rectangle GetGripRectangle(Rectangle bounds, ResizeHandle handle)
        {
            int s = GripSize;
            int cx = bounds.Left + bounds.Width / 2 - s / 2;
            int cy = bounds.Top + bounds.Height / 2 - s / 2;
            switch (handle)
            {
                case ResizeHandle.NW:
                    return new Rectangle(bounds.Left - s / 2, bounds.Top - s / 2, s, s);
                case ResizeHandle.N:
                    return new Rectangle(cx, bounds.Top - s / 2, s, s);
                case ResizeHandle.NE:
                    return new Rectangle(bounds.Right - s / 2, bounds.Top - s / 2, s, s);
                case ResizeHandle.E:
                    return new Rectangle(bounds.Right - s / 2, cy, s, s);
                case ResizeHandle.SE:
                    return new Rectangle(bounds.Right - s / 2, bounds.Bottom - s / 2, s, s);
                case ResizeHandle.S:
                    return new Rectangle(cx, bounds.Bottom - s / 2, s, s);
                case ResizeHandle.SW:
                    return new Rectangle(bounds.Left - s / 2, bounds.Bottom - s / 2, s, s);
                case ResizeHandle.W:
                    return new Rectangle(bounds.Left - s / 2, cy, s, s);
                default:
                    return Rectangle.Empty;
            }
        }

        public static Rectangle GetGripHitRectangle(Rectangle bounds, ResizeHandle handle)
        {
            Rectangle grip = GetGripRectangle(bounds, handle);
            grip.Inflate(GripHitInflate, GripHitInflate);
            return grip;
        }

        private static void DrawButton(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            TextFormatFlags flags,
            Font font,
            Color foreColor,
            Color backColor)
        {
            PushButtonState state = enabled ? PushButtonState.Normal : PushButtonState.Disabled;
            if (!backColor.IsEmpty)
            {
                using (var brush = new SolidBrush(backColor))
                {
                    g.FillRectangle(brush, bounds);
                }

                ControlPaint.DrawBorder(g, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);
                TextRenderer.DrawText(g, text, font, bounds, foreColor, flags);
                return;
            }

            if (Application.RenderWithVisualStyles)
            {
                ButtonRenderer.DrawButton(g, bounds, text, font, flags, focused: false, state);
                return;
            }

            ButtonState classic = enabled ? ButtonState.Normal : ButtonState.Inactive;
            ControlPaint.DrawButton(g, bounds, classic);
            TextRenderer.DrawText(g, text, font, bounds, foreColor, flags);
        }

        private static void DrawLabel(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            TextFormatFlags flags,
            Font font,
            Color foreColor,
            Color backColor)
        {
            if (!backColor.IsEmpty)
            {
                using (var brush = new SolidBrush(backColor))
                {
                    g.FillRectangle(brush, bounds);
                }
            }

            TextRenderer.DrawText(g, text, font, bounds, foreColor, flags);
        }

        private static void DrawTextBox(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            bool multiline,
            TextFormatFlags flags,
            Font font,
            Color foreColor,
            Color backColor)
        {
            Color fill = !backColor.IsEmpty
                ? backColor
                : (enabled ? SystemColors.Window : SystemColors.Control);
            using (var back = new SolidBrush(fill))
            {
                g.FillRectangle(back, bounds);
            }

            var textBounds = Rectangle.Inflate(bounds, -3, -2);
            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
            Color textColor = foreColor.IsEmpty
                ? (enabled ? SystemColors.WindowText : SystemColors.GrayText)
                : foreColor;
            TextRenderer.DrawText(g, text, font, textBounds, textColor, flags);
        }

        private static void DrawCheckBox(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            bool isChecked,
            TextFormatFlags flags,
            Font font,
            Color foreColor,
            Color backColor)
        {
            if (!backColor.IsEmpty)
            {
                using (var brush = new SolidBrush(backColor))
                {
                    g.FillRectangle(brush, bounds);
                }
            }

            var glyph = new Rectangle(bounds.X, bounds.Y + Math.Max(0, (bounds.Height - 13) / 2), 13, 13);
            CheckBoxState state;
            if (!enabled)
            {
                state = isChecked ? CheckBoxState.CheckedDisabled : CheckBoxState.UncheckedDisabled;
            }
            else
            {
                state = isChecked ? CheckBoxState.CheckedNormal : CheckBoxState.UncheckedNormal;
            }

            if (Application.RenderWithVisualStyles)
            {
                CheckBoxRenderer.DrawCheckBox(g, glyph.Location, state);
            }
            else
            {
                ButtonState classic = isChecked ? ButtonState.Checked : ButtonState.Normal;
                if (!enabled)
                {
                    classic |= ButtonState.Inactive;
                }

                ControlPaint.DrawCheckBox(g, glyph, classic);
            }

            var textBounds = new Rectangle(bounds.X + 18, bounds.Y, Math.Max(0, bounds.Width - 18), bounds.Height);
            TextRenderer.DrawText(g, text, font, textBounds, foreColor, flags);
        }

        private static void DrawRadioButton(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            bool isChecked,
            TextFormatFlags flags,
            Font font,
            Color foreColor,
            Color backColor)
        {
            if (!backColor.IsEmpty)
            {
                using (var brush = new SolidBrush(backColor))
                {
                    g.FillRectangle(brush, bounds);
                }
            }

            var glyph = new Rectangle(bounds.X, bounds.Y + Math.Max(0, (bounds.Height - 13) / 2), 13, 13);
            RadioButtonState state;
            if (!enabled)
            {
                state = isChecked ? RadioButtonState.CheckedDisabled : RadioButtonState.UncheckedDisabled;
            }
            else
            {
                state = isChecked ? RadioButtonState.CheckedNormal : RadioButtonState.UncheckedNormal;
            }

            if (Application.RenderWithVisualStyles)
            {
                RadioButtonRenderer.DrawRadioButton(g, glyph.Location, state);
            }
            else
            {
                ButtonState classic = isChecked ? ButtonState.Checked : ButtonState.Normal;
                if (!enabled)
                {
                    classic |= ButtonState.Inactive;
                }

                ControlPaint.DrawRadioButton(g, glyph, classic);
            }

            var textBounds = new Rectangle(bounds.X + 18, bounds.Y, Math.Max(0, bounds.Width - 18), bounds.Height);
            TextRenderer.DrawText(g, text, font, textBounds, foreColor, flags);
        }

        private static void DrawListBox(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            bool checkedStyle,
            Font font,
            Color foreColor,
            Color backColor)
        {
            Color fill = !backColor.IsEmpty
                ? backColor
                : (enabled ? SystemColors.Window : SystemColors.Control);
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
            int rowHeight = Math.Max(16, font.Height + 2);
            string line = string.IsNullOrEmpty(text) ? "Item" : text;
            Color textColor = foreColor.IsEmpty
                ? (enabled ? SystemColors.WindowText : SystemColors.GrayText)
                : foreColor;

            for (int i = 0; i < 3; i++)
            {
                int y = bounds.Y + 2 + i * rowHeight;
                if (y + rowHeight > bounds.Bottom - 2)
                {
                    break;
                }

                var row = new Rectangle(bounds.X + 2, y, Math.Max(0, bounds.Width - 4), rowHeight);
                if (checkedStyle)
                {
                    var box = new Rectangle(row.X + 2, row.Y + Math.Max(0, (row.Height - 12) / 2), 12, 12);
                    ControlPaint.DrawCheckBox(g, box, i == 0 ? ButtonState.Checked : ButtonState.Normal);
                    var textBounds = new Rectangle(box.Right + 4, row.Y, Math.Max(0, row.Right - box.Right - 4), row.Height);
                    TextRenderer.DrawText(
                        g,
                        line + (i + 1),
                        font,
                        textBounds,
                        textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }
                else
                {
                    if (i == 0)
                    {
                        using (var highlight = new SolidBrush(SystemColors.Highlight))
                        {
                            g.FillRectangle(highlight, row);
                        }

                        textColor = SystemColors.HighlightText;
                    }

                    TextRenderer.DrawText(
                        g,
                        line + (i == 0 ? string.Empty : (i + 1).ToString()),
                        font,
                        row,
                        textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                    textColor = foreColor.IsEmpty
                        ? (enabled ? SystemColors.WindowText : SystemColors.GrayText)
                        : foreColor;
                }
            }
        }

        private static void DrawNumericUpDown(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            Font font,
            Color foreColor,
            Color backColor)
        {
            Color fill = !backColor.IsEmpty
                ? backColor
                : (enabled ? SystemColors.Window : SystemColors.Control);
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
            int buttonWidth = SystemInformation.VerticalScrollBarWidth;
            var up = new Rectangle(bounds.Right - buttonWidth - 1, bounds.Y + 1, buttonWidth, Math.Max(1, bounds.Height / 2 - 1));
            var down = new Rectangle(bounds.Right - buttonWidth - 1, up.Bottom, buttonWidth, Math.Max(1, bounds.Bottom - up.Bottom - 1));
            ControlPaint.DrawScrollButton(g, up, ScrollButton.Up, enabled ? ButtonState.Normal : ButtonState.Inactive);
            ControlPaint.DrawScrollButton(g, down, ScrollButton.Down, enabled ? ButtonState.Normal : ButtonState.Inactive);

            var textBounds = new Rectangle(bounds.X + 3, bounds.Y, Math.Max(0, bounds.Width - buttonWidth - 6), bounds.Height);
            Color textColor = foreColor.IsEmpty
                ? (enabled ? SystemColors.WindowText : SystemColors.GrayText)
                : foreColor;
            TextRenderer.DrawText(
                g,
                string.IsNullOrEmpty(text) ? "0" : text,
                font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
        }

        private static void DrawPictureBox(Graphics g, Rectangle bounds, bool enabled, Color backColor)
        {
            Color fill = !backColor.IsEmpty
                ? backColor
                : (enabled ? SystemColors.Control : SystemColors.ControlLight);
            using (var brush = new SolidBrush(fill))
            {
                g.FillRectangle(brush, bounds);
            }

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
            using (var pen = new Pen(enabled ? SystemColors.GrayText : SystemColors.ControlDark))
            {
                int pad = 8;
                var inner = Rectangle.Inflate(bounds, -pad, -pad);
                if (inner.Width > 4 && inner.Height > 4)
                {
                    g.DrawRectangle(pen, inner);
                    g.DrawLine(pen, inner.Left, inner.Bottom, inner.Left + inner.Width / 3, inner.Top + inner.Height / 2);
                    g.DrawLine(pen, inner.Left + inner.Width / 3, inner.Top + inner.Height / 2, inner.Left + inner.Width * 2 / 3, inner.Bottom - 4);
                    g.DrawLine(pen, inner.Left + inner.Width * 2 / 3, inner.Bottom - 4, inner.Right, inner.Top + 4);
                    g.DrawEllipse(pen, inner.Right - inner.Width / 3, inner.Top + 2, Math.Max(4, inner.Width / 5), Math.Max(4, inner.Width / 5));
                }
            }
        }

        private static void DrawComboBox(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            Font font,
            Color foreColor,
            Color backColor)
        {
            Color fill = !backColor.IsEmpty
                ? backColor
                : (enabled ? SystemColors.Window : SystemColors.Control);
            using (var back = new SolidBrush(fill))
            {
                g.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
            int dropWidth = SystemInformation.VerticalScrollBarWidth;
            var drop = new Rectangle(bounds.Right - dropWidth - 1, bounds.Y + 1, dropWidth, bounds.Height - 2);
            ControlPaint.DrawComboButton(g, drop, enabled ? ButtonState.Normal : ButtonState.Inactive);

            var textBounds = new Rectangle(bounds.X + 3, bounds.Y, Math.Max(0, bounds.Width - dropWidth - 6), bounds.Height);
            Color textColor = foreColor.IsEmpty
                ? (enabled ? SystemColors.WindowText : SystemColors.GrayText)
                : foreColor;
            TextRenderer.DrawText(
                g,
                text,
                font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawDatePicker(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            Font font,
            Color foreColor,
            Color backColor,
            bool includeTime)
        {
            Color fill = !backColor.IsEmpty
                ? backColor
                : (enabled ? SystemColors.Window : SystemColors.Control);
            using (var back = new SolidBrush(fill))
            {
                g.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
            int dropWidth = SystemInformation.VerticalScrollBarWidth;
            var drop = new Rectangle(bounds.Right - dropWidth - 1, bounds.Y + 1, dropWidth, bounds.Height - 2);
            ControlPaint.DrawComboButton(g, drop, enabled ? ButtonState.Normal : ButtonState.Inactive);

            // Small calendar hint inside the drop button area.
            int calSize = Math.Min(12, Math.Max(8, drop.Height - 8));
            var cal = new Rectangle(
                drop.X + (drop.Width - calSize) / 2,
                drop.Y + (drop.Height - calSize) / 2,
                calSize,
                calSize);
            using (var pen = new Pen(enabled ? SystemColors.ControlText : SystemColors.GrayText))
            {
                g.DrawRectangle(pen, cal);
                int midY = cal.Y + cal.Height / 3;
                g.DrawLine(pen, cal.Left, midY, cal.Right, midY);
            }

            if (includeTime)
            {
                using (var pen = new Pen(enabled ? SystemColors.ControlDarkDark : SystemColors.GrayText))
                {
                    g.DrawLine(pen, cal.Left + 2, cal.Bottom - 3, cal.Right - 2, cal.Bottom - 3);
                }
            }

            var textBounds = new Rectangle(bounds.X + 3, bounds.Y, Math.Max(0, bounds.Width - dropWidth - 6), bounds.Height);
            Color textColor = foreColor.IsEmpty
                ? (enabled ? SystemColors.WindowText : SystemColors.GrayText)
                : foreColor;
            TextRenderer.DrawText(
                g,
                text,
                font,
                textBounds,
                textColor,
                TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        private static void DrawPanel(Graphics g, Rectangle bounds, Color backColor)
        {
            using (var back = new SolidBrush(backColor.IsEmpty ? SystemColors.Control : backColor))
            {
                g.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder3D(g, bounds, Border3DStyle.Sunken);
        }

        private static void DrawScrollContainer(Graphics g, Rectangle bounds, Color backColor)
        {
            using (var back = new SolidBrush(backColor.IsEmpty ? SystemColors.Control : backColor))
            {
                g.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder(g, bounds, SystemColors.ActiveBorder, ButtonBorderStyle.Solid);
            int bar = SystemInformation.VerticalScrollBarWidth;
            var vBar = new Rectangle(bounds.Right - bar - 1, bounds.Y + 1, bar, Math.Max(0, bounds.Height - bar - 2));
            var hBar = new Rectangle(bounds.X + 1, bounds.Bottom - bar - 1, Math.Max(0, bounds.Width - bar - 2), bar);
            using (var brush = new SolidBrush(SystemColors.ControlLight))
            {
                g.FillRectangle(brush, vBar);
                g.FillRectangle(brush, hBar);
            }

            ControlPaint.DrawScrollButton(
                g,
                new Rectangle(vBar.X, vBar.Y, vBar.Width, Math.Min(16, vBar.Height / 2)),
                ScrollButton.Up,
                ButtonState.Normal);
            ControlPaint.DrawScrollButton(
                g,
                new Rectangle(vBar.X, vBar.Bottom - Math.Min(16, vBar.Height / 2), vBar.Width, Math.Min(16, vBar.Height / 2)),
                ScrollButton.Down,
                ButtonState.Normal);
        }

        private static void DrawTableLayout(Graphics g, Rectangle bounds, Color backColor)
        {
            using (var back = new SolidBrush(backColor.IsEmpty ? SystemColors.Window : backColor))
            {
                g.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder(g, bounds, SystemColors.ActiveBorder, ButtonBorderStyle.Solid);
            using (var pen = new Pen(SystemColors.ControlDark))
            {
                int cols = 3;
                int rows = 3;
                for (int c = 1; c < cols; c++)
                {
                    int x = bounds.X + (bounds.Width * c / cols);
                    g.DrawLine(pen, x, bounds.Y, x, bounds.Bottom - 1);
                }

                for (int r = 1; r < rows; r++)
                {
                    int y = bounds.Y + (bounds.Height * r / rows);
                    g.DrawLine(pen, bounds.X, y, bounds.Right - 1, y);
                }
            }
        }

        private static void DrawDataGrid(Graphics g, Rectangle bounds, Font font, Color foreColor, Color backColor)
        {
            using (var back = new SolidBrush(backColor.IsEmpty ? SystemColors.Window : backColor))
            {
                g.FillRectangle(back, bounds);
            }

            ControlPaint.DrawBorder(g, bounds, SystemColors.ActiveBorder, ButtonBorderStyle.Solid);
            int headerH = Math.Min(24, Math.Max(16, bounds.Height / 6));
            var header = new Rectangle(bounds.X + 1, bounds.Y + 1, bounds.Width - 2, headerH);
            using (var brush = new SolidBrush(SystemColors.Control))
            {
                g.FillRectangle(brush, header);
            }

            using (var pen = new Pen(SystemColors.ControlDark))
            {
                g.DrawLine(pen, bounds.X + 1, header.Bottom, bounds.Right - 2, header.Bottom);
                int cols = 3;
                for (int c = 1; c < cols; c++)
                {
                    int x = bounds.X + (bounds.Width * c / cols);
                    g.DrawLine(pen, x, bounds.Y + 1, x, bounds.Bottom - 1);
                }

                int rowH = Math.Max(16, (bounds.Height - headerH - 2) / 4);
                for (int r = 1; r < 4; r++)
                {
                    int y = header.Bottom + r * rowH;
                    if (y >= bounds.Bottom - 1)
                    {
                        break;
                    }

                    g.DrawLine(pen, bounds.X + 1, y, bounds.Right - 2, y);
                }
            }

            TextRenderer.DrawText(
                g,
                "DataGrid",
                font,
                header,
                foreColor.IsEmpty ? SystemColors.ControlText : foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
        }

        public const int TabHeaderHeight = 24;

        public static void DrawTabControl(
            Graphics g,
            Rectangle bounds,
            IList<string> tabTitles,
            int selectedIndex,
            Font font,
            Color foreColor,
            Color backColor)
        {
            DrawTabControlShell(g, bounds, backColor);
            if (tabTitles == null || tabTitles.Count == 0)
            {
                return;
            }

            int x = bounds.Left + 2;
            int selected = selectedIndex;
            if (selected < 0 || selected >= tabTitles.Count)
            {
                selected = 0;
            }

            for (int i = 0; i < tabTitles.Count; i++)
            {
                string title = tabTitles[i] ?? ("Tab " + (i + 1));
                Size size = TextRenderer.MeasureText(title, font);
                int tabWidth = Math.Max(48, size.Width + 16);
                var tabRect = new Rectangle(x, bounds.Top + 2, tabWidth, TabHeaderHeight - 2);
                bool isSelected = i == selected;
                Color tabBack = isSelected
                    ? (backColor.IsEmpty ? SystemColors.Window : backColor)
                    : SystemColors.Control;
                using (var brush = new SolidBrush(tabBack))
                {
                    g.FillRectangle(brush, tabRect);
                }

                g.DrawRectangle(SystemPens.ControlDark, tabRect);
                TextRenderer.DrawText(
                    g,
                    title,
                    font,
                    tabRect,
                    foreColor.IsEmpty ? SystemColors.ControlText : foreColor,
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis);
                x += tabWidth + 2;
            }

            // Content area border under headers.
            var content = GetTabContentBounds(bounds);
            g.DrawRectangle(SystemPens.ControlDark, content);
        }

        public static Rectangle GetTabContentBounds(Rectangle tabControlBounds)
        {
            int top = tabControlBounds.Top + TabHeaderHeight;
            int height = Math.Max(0, tabControlBounds.Height - TabHeaderHeight - 2);
            return new Rectangle(
                tabControlBounds.Left + 2,
                top,
                Math.Max(0, tabControlBounds.Width - 4),
                height);
        }

        public static int HitTestTabHeader(Rectangle tabControlBounds, IList<string> tabTitles, Font font, Point point)
        {
            if (tabTitles == null || tabTitles.Count == 0)
            {
                return -1;
            }

            if (point.Y < tabControlBounds.Top || point.Y > tabControlBounds.Top + TabHeaderHeight)
            {
                return -1;
            }

            int x = tabControlBounds.Left + 2;
            for (int i = 0; i < tabTitles.Count; i++)
            {
                string title = tabTitles[i] ?? ("Tab " + (i + 1));
                Size size = TextRenderer.MeasureText(title, font ?? SystemFonts.DefaultFont);
                int tabWidth = Math.Max(48, size.Width + 16);
                var tabRect = new Rectangle(x, tabControlBounds.Top + 2, tabWidth, TabHeaderHeight - 2);
                if (tabRect.Contains(point))
                {
                    return i;
                }

                x += tabWidth + 2;
            }

            return -1;
        }

        private static void DrawTabControlShell(Graphics g, Rectangle bounds, Color backColor)
        {
            using (var back = new SolidBrush(backColor.IsEmpty ? SystemColors.Control : backColor))
            {
                g.FillRectangle(back, bounds);
            }

            g.DrawRectangle(SystemPens.ControlDark, bounds);
        }

        private static void DrawGroupBox(
            Graphics g,
            Rectangle bounds,
            string text,
            bool enabled,
            Font font,
            Color foreColor,
            Color backColor)
        {
            using (var back = new SolidBrush(backColor.IsEmpty ? SystemColors.Control : backColor))
            {
                g.FillRectangle(back, bounds);
            }

            string caption = text ?? string.Empty;
            if (Application.RenderWithVisualStyles)
            {
                GroupBoxRenderer.DrawGroupBox(
                    g,
                    bounds,
                    caption,
                    font,
                    foreColor,
                    TextFormatFlags.Default,
                    enabled ? GroupBoxState.Normal : GroupBoxState.Disabled);
                return;
            }

            // Fallback: classic etched border with caption gap on the top edge.
            Size textSize = TextRenderer.MeasureText(caption, font);
            int textLeft = bounds.Left + 8;
            int textTop = bounds.Top;
            int lineY = bounds.Top + Math.Max(6, textSize.Height / 2);
            Color border = enabled ? SystemColors.ControlDark : SystemColors.ControlDarkDark;

            using (var pen = new Pen(border))
            {
                g.DrawLine(pen, bounds.Left, lineY, textLeft - 2, lineY);
                g.DrawLine(pen, textLeft + textSize.Width + 2, lineY, bounds.Right - 1, lineY);
                g.DrawLine(pen, bounds.Left, lineY, bounds.Left, bounds.Bottom - 1);
                g.DrawLine(pen, bounds.Right - 1, lineY, bounds.Right - 1, bounds.Bottom - 1);
                g.DrawLine(pen, bounds.Left, bounds.Bottom - 1, bounds.Right - 1, bounds.Bottom - 1);
            }

            TextRenderer.DrawText(
                g,
                caption,
                font,
                new Point(textLeft, textTop),
                enabled ? foreColor : SystemColors.GrayText);
        }

        private static void DrawFallback(Graphics g, Rectangle bounds, string text, Font font, Color foreColor)
        {
            g.FillRectangle(SystemBrushes.Control, bounds);
            ControlPaint.DrawBorder(g, bounds, SystemColors.ControlDark, ButtonBorderStyle.Solid);
            TextRenderer.DrawText(
                g,
                text,
                font,
                bounds,
                foreColor,
                TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private static void DrawGrip(Graphics g, Rectangle rect)
        {
            if (rect.IsEmpty)
            {
                return;
            }

            g.FillRectangle(Brushes.White, rect);
            g.DrawRectangle(Pens.DodgerBlue, rect);
        }
    }
}
