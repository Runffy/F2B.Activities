using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Windows.Forms;
using F2B.Forms.Model;

namespace F2B.Forms.Engine
{
    public sealed class FormRenderResult
    {
        public Form Form { get; set; }
        public Dictionary<string, Control> Controls { get; set; }
        public FormDefinition Definition { get; set; }
    }

    public static class FormRenderer
    {
        [ThreadStatic]
        private static string _activeFormCulture;

        public static FormRenderResult Render(FormDefinition definition)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            _activeFormCulture = definition.Culture;
            OsCulture.ApplyToCurrentThread(_activeFormCulture);

            var map = new Dictionary<string, Control>(StringComparer.OrdinalIgnoreCase);
            bool allowResize = definition.AllowResize;
            var form = new Form
            {
                Name = string.IsNullOrWhiteSpace(definition.Id) ? "form" : definition.Id.Trim(),
                Text = definition.Title ?? "Form",
                ClientSize = new Size(
                    definition.Width > 0 ? definition.Width : 640,
                    definition.Height > 0 ? definition.Height : 480),
                StartPosition = ParseStartPosition(definition.StartPosition),
                FormBorderStyle = allowResize ? FormBorderStyle.Sizable : FormBorderStyle.FixedSingle,
                MinimizeBox = true,
                MaximizeBox = allowResize,
                ShowInTaskbar = true,
                AutoScaleMode = AutoScaleMode.Font
            };

            if (!allowResize)
            {
                // Lock outer size so system maximize / size grips cannot change it.
                form.MinimumSize = form.Size;
                form.MaximumSize = form.Size;
            }

            map[form.Name] = form;
            map["form"] = form;

            if (definition.Controls != null)
            {
                foreach (ControlDefinition child in definition.Controls)
                {
                    Control created = CreateControl(child, map);
                    form.Controls.Add(created);
                }
            }

            return new FormRenderResult
            {
                Form = form,
                Controls = map,
                Definition = definition
            };
        }

        public static void ApplyValue(Control control, object value)
        {
            if (control == null)
            {
                return;
            }

            if (control is CheckBox checkBox)
            {
                checkBox.Checked = ToBool(value);
                return;
            }

            if (control is RadioButton radioButton)
            {
                radioButton.Checked = ToBool(value);
                return;
            }

            if (control is NumericUpDown numeric)
            {
                if (value != null && decimal.TryParse(Convert.ToString(value), out decimal number))
                {
                    if (number < numeric.Minimum)
                    {
                        number = numeric.Minimum;
                    }
                    else if (number > numeric.Maximum)
                    {
                        number = numeric.Maximum;
                    }

                    numeric.Value = number;
                }

                return;
            }

            if (control is ListBox listBox)
            {
                ApplyListBoxValue(listBox, value);
                return;
            }

            if (control is ComboBox comboBox)
            {
                if (value is System.Collections.IEnumerable enumerable && !(value is string))
                {
                    comboBox.Items.Clear();
                    foreach (object item in enumerable)
                    {
                        if (item != null)
                        {
                            comboBox.Items.Add(Convert.ToString(item));
                        }
                    }

                    return;
                }

                string text = Convert.ToString(value);
                int index = comboBox.FindStringExact(text ?? string.Empty);
                if (index >= 0)
                {
                    comboBox.SelectedIndex = index;
                }
                else
                {
                    comboBox.Text = text ?? string.Empty;
                }

                return;
            }

            if (control is DateTimePicker dateTimePicker)
            {
                ApplyDateTimePickerValue(dateTimePicker, value);
                return;
            }

            if (control is PictureBox pictureBox)
            {
                ApplyPicturePath(pictureBox, value == null ? null : Convert.ToString(value));
                return;
            }

            control.Text = value == null ? string.Empty : Convert.ToString(value);
        }

        /// <summary>
        /// Load a PictureBox from a local file path. Empty/null clears the image.
        /// </summary>
        public static void ApplyPicturePath(PictureBox picture, string imagePath)
        {
            TryLoadPicture(picture, imagePath);
        }

        public static string ReadPicturePath(PictureBox picture)
        {
            if (picture == null)
            {
                return string.Empty;
            }

            return picture.Tag as string ?? string.Empty;
        }

        public static string ReadDateTimePickerValue(DateTimePicker picker)
        {
            if (picker == null)
            {
                return string.Empty;
            }

            return IsDateOnlyPicker(picker)
                ? picker.Value.ToString("yyyy-MM-dd")
                : picker.Value.ToString("yyyy-MM-dd HH:mm:ss");
        }

        public static void ApplyDateTimePickerValue(DateTimePicker picker, object value)
        {
            if (picker == null)
            {
                return;
            }

            if (!TryParseDateTime(value, out DateTime parsed))
            {
                return;
            }

            if (IsDateOnlyPicker(picker))
            {
                parsed = parsed.Date;
            }

            if (parsed < picker.MinDate)
            {
                parsed = picker.MinDate;
            }
            else if (parsed > picker.MaxDate)
            {
                parsed = picker.MaxDate;
            }

            picker.Value = parsed;
        }

        public static bool TryParseDateTime(object value, out DateTime result)
        {
            result = default(DateTime);
            if (value == null)
            {
                return false;
            }

            if (value is DateTime dt)
            {
                result = dt;
                return true;
            }

            if (value is DateTimeOffset dto)
            {
                result = dto.LocalDateTime;
                return true;
            }

            string text = Convert.ToString(value);
            if (string.IsNullOrWhiteSpace(text))
            {
                return false;
            }

            text = text.Trim();
            string[] formats =
            {
                "yyyy-MM-dd HH:mm:ss",
                "yyyy-MM-dd HH:mm",
                "yyyy-MM-dd",
                "yyyy/MM/dd HH:mm:ss",
                "yyyy/MM/dd HH:mm",
                "yyyy/MM/dd"
            };

            if (DateTime.TryParseExact(
                text,
                formats,
                System.Globalization.CultureInfo.InvariantCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out result))
            {
                return true;
            }

            return DateTime.TryParse(
                text,
                System.Globalization.CultureInfo.CurrentCulture,
                System.Globalization.DateTimeStyles.AllowWhiteSpaces,
                out result);
        }

        private static bool IsDateOnlyPicker(DateTimePicker picker)
        {
            return picker != null
                && string.Equals(
                    Convert.ToString(picker.Tag),
                    FormControlType.DatePicker,
                    StringComparison.OrdinalIgnoreCase);
        }

        private static Control CreateControl(ControlDefinition definition, Dictionary<string, Control> map)
        {
            string type = definition.Type == null ? string.Empty : definition.Type.Trim();
            Control control;

            switch (type)
            {
                case FormControlType.Button:
                    control = new Button { Text = definition.Text ?? string.Empty };
                    break;
                case FormControlType.Label:
                    control = new Label
                    {
                        Text = definition.Text ?? string.Empty,
                        AutoSize = definition.Width <= 0
                    };
                    break;
                case FormControlType.TextBox:
                    control = CreateTextBox(definition, multiline: false);
                    break;
                case FormControlType.TextArea:
                    control = CreateTextBox(definition, multiline: true);
                    break;
                case FormControlType.CheckBox:
                    control = new CheckBox
                    {
                        Text = definition.Text ?? string.Empty,
                        Checked = definition.Checked == true,
                        AutoSize = false
                    };
                    break;
                case FormControlType.RadioButton:
                    control = new RadioButton
                    {
                        Text = definition.Text ?? string.Empty,
                        Checked = definition.Checked == true,
                        AutoSize = false
                    };
                    break;
                case FormControlType.ComboBox:
                    control = CreateComboBox(definition);
                    break;
                case FormControlType.ListBox:
                    control = CreateListBox(definition);
                    break;
                case FormControlType.CheckedListBox:
                    control = CreateCheckedListBox(definition);
                    break;
                case FormControlType.MaskedTextBox:
                    control = CreateMaskedTextBox(definition);
                    break;
                case FormControlType.NumericUpDown:
                    control = CreateNumericUpDown(definition);
                    break;
                case FormControlType.PictureBox:
                    control = CreatePictureBox(definition);
                    break;
                case FormControlType.DatePicker:
                    control = CreateDateTimePicker(definition, includeTime: false);
                    break;
                case FormControlType.DateTimePicker:
                    control = CreateDateTimePicker(definition, includeTime: true);
                    break;
                case FormControlType.Panel:
                    control = CreatePanel(definition, map);
                    break;
                case FormControlType.ScrollContainer:
                    control = CreateScrollContainer(definition, map);
                    break;
                case FormControlType.TableLayout:
                    control = CreateTableLayout(definition, map);
                    break;
                case FormControlType.DataGrid:
                    control = CreateDataGrid(definition);
                    break;
                case FormControlType.GroupBox:
                    control = CreateGroupBox(definition, map);
                    break;
                case FormControlType.TabControl:
                    control = CreateTabControl(definition, map);
                    break;
                case FormControlType.TabPage:
                    control = new TabPage
                    {
                        Text = definition.Text ?? definition.Id ?? "Tab",
                        UseVisualStyleBackColor = true
                    };
                    break;
                default:
                    throw new InvalidOperationException("Unsupported control type: " + type);
            }

            control.Name = definition.Id.Trim();
            control.Location = new Point(definition.X, definition.Y);

            if (definition.Enabled.HasValue)
            {
                control.Enabled = definition.Enabled.Value;
            }

            if (definition.Visible.HasValue)
            {
                control.Visible = definition.Visible.Value;
            }

            if (!string.IsNullOrWhiteSpace(definition.Anchor))
            {
                control.Anchor = ParseAnchor(definition.Anchor);
            }

            ApplyTextAlign(control, type, definition);
            ApplyFontAndColors(control, type, definition);
            // Apply size last: TextBox/ComboBox may ignore Height unless prepared above.
            if (!FormControlType.IsTabPage(type))
            {
                ApplyDesignedSize(control, definition);
            }

            map[control.Name] = control;
            return control;
        }

        /// <summary>Create a control instance and register it in <paramref name="map"/> (does not parent it).</summary>
        public static Control CreateControlInstance(ControlDefinition definition, Dictionary<string, Control> map)
        {
            if (definition == null)
            {
                throw new ArgumentNullException(nameof(definition));
            }

            if (map == null)
            {
                throw new ArgumentNullException(nameof(map));
            }

            if (string.IsNullOrWhiteSpace(definition.Id))
            {
                throw new InvalidOperationException("Control id is required.");
            }

            if (!FormControlType.IsKnown(definition.Type))
            {
                throw new InvalidOperationException("Unsupported control type: " + definition.Type);
            }

            string id = definition.Id.Trim();
            if (map.ContainsKey(id))
            {
                throw new InvalidOperationException("Duplicate control id: '" + id + "'.");
            }

            return CreateControl(definition, map);
        }

        private static void ApplyFontAndColors(Control control, string type, ControlDefinition definition)
        {
            if (control == null || definition == null)
            {
                return;
            }

            bool hasFontProps = !string.IsNullOrWhiteSpace(definition.FontFamily)
                || definition.FontSize.HasValue
                || definition.FontBold == true
                || definition.FontItalic == true
                || definition.FontUnderline == true;

            if (hasFontProps && SupportsFont(type))
            {
                Font old = control.Font;
                control.Font = FontStyleUtil.CreateFont(
                    definition.FontFamily,
                    definition.FontSize ?? FontStyleUtil.DefaultSize,
                    definition.FontBold == true,
                    definition.FontItalic == true,
                    definition.FontUnderline == true);
                if (old != null && !ReferenceEquals(old, control.Font) && old != SystemFonts.DefaultFont)
                {
                    // Do not dispose system fonts; only dispose if we created a previous custom one.
                }
            }

            Color? fore = FontStyleUtil.ParseColor(definition.ForeColor);
            if (fore.HasValue && SupportsFont(type))
            {
                ApplyForeColor(control, fore.Value);
            }

            Color? back = FontStyleUtil.ParseColor(definition.BackColor);
            if (back.HasValue && SupportsBackColor(type))
            {
                control.BackColor = back.Value;
            }
        }

        /// <summary>
        /// WinForms TextBox/TextArea (especially ReadOnly) often ignore ForeColor until BackColor
        /// is explicitly set away from the internal "default color" mode.
        /// </summary>
        private static void ApplyForeColor(Control control, Color color)
        {
            if (control is TextBoxBase textBox)
            {
                Color back = textBox.BackColor;
                int controlArgb = SystemColors.Control.ToArgb();
                int windowArgb = SystemColors.Window.ToArgb();

                if (textBox.ReadOnly
                    || back.ToArgb() == controlArgb
                    || back.ToArgb() == windowArgb)
                {
                    textBox.BackColor = SystemColors.Window;
                }
                else
                {
                    textBox.BackColor = back;
                }

                textBox.ForeColor = color;
                return;
            }

            control.ForeColor = color;
        }

        private static bool SupportsFont(string type)
        {
            return type == FormControlType.Button
                || type == FormControlType.Label
                || type == FormControlType.TextBox
                || type == FormControlType.TextArea
                || type == FormControlType.CheckBox
                || type == FormControlType.RadioButton
                || type == FormControlType.ComboBox
                || type == FormControlType.ListBox
                || type == FormControlType.CheckedListBox
                || type == FormControlType.MaskedTextBox
                || type == FormControlType.NumericUpDown
                || type == FormControlType.DatePicker
                || type == FormControlType.DateTimePicker
                || type == FormControlType.GroupBox
                || type == FormControlType.ScrollContainer
                || type == FormControlType.TableLayout
                || type == FormControlType.DataGrid
                || type == FormControlType.TabControl
                || type == FormControlType.TabPage
                || type == FormControlType.PictureBox;
        }

        private static bool SupportsBackColor(string type)
        {
            return SupportsFont(type)
                || FormControlType.IsContainer(type)
                || FormControlType.IsDataGrid(type)
                || FormControlType.IsPictureBox(type);
        }

        private static void ApplyDesignedSize(Control control, ControlDefinition definition)
        {
            if (control == null || definition == null)
            {
                return;
            }

            int width = definition.Width > 0 ? definition.Width : control.Width;
            int height = definition.Height > 0 ? definition.Height : control.Height;
            control.Size = new Size(width, height);

            // ComboBox may still clamp; re-assert after ItemHeight is set.
            var combo = control as ComboBox;
            if (combo != null && definition.Height > 0)
            {
                combo.IntegralHeight = false;
                combo.Height = definition.Height;
            }

            var textBox = control as TextBox;
            if (textBox != null && definition.Height > 0 && !IsTrueMultilineTextArea(definition))
            {
                // Ensure single-line style TextBox keeps designed height.
                textBox.Height = definition.Height;
            }
        }

        private static bool IsTrueMultilineTextArea(ControlDefinition definition)
        {
            return definition != null
                && string.Equals(definition.Type, FormControlType.TextArea, StringComparison.OrdinalIgnoreCase);
        }

        private static void ApplyTextAlign(Control control, string type, ControlDefinition definition)
        {
            TextHAlign h = TextAlignUtil.ParseH(definition.TextAlignH);
            TextVAlign v = TextAlignUtil.ParseV(definition.TextAlignV);

            switch (type)
            {
                case FormControlType.Label:
                    var label = (Label)control;
                    label.AutoSize = false;
                    label.TextAlign = TextAlignUtil.ToContentAlignment(h, v);
                    break;
                case FormControlType.Button:
                    ((Button)control).TextAlign = TextAlignUtil.ToContentAlignment(h, v);
                    break;
                case FormControlType.CheckBox:
                    ((CheckBox)control).TextAlign = TextAlignUtil.ToContentAlignment(h, v);
                    ((CheckBox)control).AutoSize = false;
                    break;
                case FormControlType.RadioButton:
                    ((RadioButton)control).TextAlign = TextAlignUtil.ToContentAlignment(h, v);
                    ((RadioButton)control).AutoSize = false;
                    break;
                case FormControlType.TextBox:
                case FormControlType.TextArea:
                    // TextBox only supports horizontal alignment.
                    ((TextBox)control).TextAlign = TextAlignUtil.ToHorizontalAlignment(h);
                    break;
                case FormControlType.MaskedTextBox:
                    ((MaskedTextBox)control).TextAlign = TextAlignUtil.ToHorizontalAlignment(h);
                    break;
            }
        }

        private static TextBox CreateTextBox(ControlDefinition definition, bool multiline)
        {
            char? passwordChar = ParsePasswordChar(definition == null ? null : definition.PasswordChar);
            // WinForms ignores PasswordChar when Multiline is true — password boxes must be single-line.
            bool passwordMode = passwordChar.HasValue && !multiline;

            var textBox = new TextBox
            {
                Text = definition.Text ?? string.Empty,
                ReadOnly = definition.ReadOnly == true
            };

            if (definition.MaxLength.HasValue && definition.MaxLength.Value > 0)
            {
                textBox.MaxLength = definition.MaxLength.Value;
            }

            if (multiline)
            {
                // True TextArea
                textBox.Multiline = true;
                textBox.AcceptsReturn = true;
                bool wordWrap = definition.WordWrap ?? true;
                textBox.WordWrap = wordWrap;

                string scrollBars = definition.ScrollBars;
                // Without wrap, Vertical alone never shows a horizontal bar for long lines.
                if (string.IsNullOrWhiteSpace(scrollBars))
                {
                    scrollBars = wordWrap ? "Vertical" : "Both";
                }

                textBox.ScrollBars = ParseScrollBars(scrollBars);
            }
            else if (passwordMode)
            {
                textBox.Multiline = false;
                textBox.AcceptsReturn = false;
                textBox.WordWrap = false;
                textBox.ScrollBars = ScrollBars.None;
                textBox.PasswordChar = passwordChar.Value;
                textBox.UseSystemPasswordChar = false;
            }
            else
            {
                // WinForms single-line TextBox ignores Height (font PreferredHeight).
                // Multiline + no AcceptsReturn lets designed Height stick, still acts as one line.
                textBox.Multiline = true;
                textBox.AcceptsReturn = false;
                textBox.WordWrap = false;
                textBox.ScrollBars = ScrollBars.None;
            }

            return textBox;
        }

        private static char? ParsePasswordChar(string value)
        {
            if (string.IsNullOrEmpty(value))
            {
                return null;
            }

            string trimmed = value.Trim();
            if (trimmed.Length == 0)
            {
                return null;
            }

            // Allow "\\0" / "none" to mean clear (no mask).
            if (string.Equals(trimmed, "\\0", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "none", StringComparison.OrdinalIgnoreCase)
                || string.Equals(trimmed, "false", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            return trimmed[0];
        }

        private static DateTimePicker CreateDateTimePicker(ControlDefinition definition, bool includeTime)
        {
            OsCulture.ApplyToCurrentThread(_activeFormCulture);

            var picker = new DateTimePicker
            {
                Tag = includeTime ? FormControlType.DateTimePicker : FormControlType.DatePicker,
                Format = DateTimePickerFormat.Custom,
                // Keep ISO display for stable RPA read/write; calendar UI still follows culture.
                CustomFormat = includeTime ? "yyyy-MM-dd HH:mm:ss" : "yyyy-MM-dd",
                ShowUpDown = false
            };

            if (TryParseDateTime(definition == null ? null : definition.Text, out DateTime initial))
            {
                if (!includeTime)
                {
                    initial = initial.Date;
                }

                if (initial < picker.MinDate)
                {
                    initial = picker.MinDate;
                }
                else if (initial > picker.MaxDate)
                {
                    initial = picker.MaxDate;
                }

                picker.Value = initial;
            }

            return picker;
        }

        private static ComboBox CreateComboBox(ControlDefinition definition)
        {
            int designedHeight = definition.Height > 0 ? definition.Height : 30;
            // Border + padding roughly; keep item area usable.
            int itemHeight = Math.Max(12, designedHeight - 8);

            var combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                IntegralHeight = false,
                // Default ComboBox height is font-locked; OwnerDrawFixed allows matching designed Height.
                DrawMode = DrawMode.OwnerDrawFixed,
                ItemHeight = itemHeight
            };

            combo.DrawItem += (sender, e) =>
            {
                var box = (ComboBox)sender;
                bool disabled = !box.Enabled
                    || (e.State & DrawItemState.Disabled) == DrawItemState.Disabled;
                bool selected = !disabled
                    && (e.State & DrawItemState.Selected) == DrawItemState.Selected;

                if (disabled)
                {
                    using (var brush = new SolidBrush(SystemColors.Control))
                    {
                        e.Graphics.FillRectangle(brush, e.Bounds);
                    }
                }
                else
                {
                    e.DrawBackground();
                }

                if (e.Index >= 0 && e.Index < box.Items.Count)
                {
                    string itemText = Convert.ToString(box.Items[e.Index]) ?? string.Empty;
                    Color textColor = disabled
                        ? SystemColors.GrayText
                        : selected
                            ? SystemColors.HighlightText
                            : box.ForeColor;
                    TextRenderer.DrawText(
                        e.Graphics,
                        itemText,
                        box.Font,
                        e.Bounds,
                        textColor,
                        TextFormatFlags.Left | TextFormatFlags.VerticalCenter | TextFormatFlags.EndEllipsis | TextFormatFlags.NoPrefix);
                }

                if (!disabled)
                {
                    e.DrawFocusRectangle();
                }
            };

            if (definition.Items != null)
            {
                foreach (string item in definition.Items)
                {
                    combo.Items.Add(item ?? string.Empty);
                }
            }

            if (definition.SelectedIndex.HasValue
                && definition.SelectedIndex.Value >= 0
                && definition.SelectedIndex.Value < combo.Items.Count)
            {
                combo.SelectedIndex = definition.SelectedIndex.Value;
            }

            if (definition.ReadOnly == true)
            {
                ComboBoxReadOnly.Set(combo, true);
            }

            return combo;
        }

        private static ListBox CreateListBox(ControlDefinition definition)
        {
            var list = new ListBox
            {
                IntegralHeight = false,
                SelectionMode = SelectionMode.One
            };
            PopulateItems(item => list.Items.Add(item), definition);
            ApplySelectedIndex(list, definition);
            return list;
        }

        private static CheckedListBox CreateCheckedListBox(ControlDefinition definition)
        {
            var list = new CheckedListBox
            {
                IntegralHeight = false,
                CheckOnClick = true
            };
            PopulateItems(item => list.Items.Add(item), definition);
            ApplySelectedIndex(list, definition);
            return list;
        }

        private static MaskedTextBox CreateMaskedTextBox(ControlDefinition definition)
        {
            var box = new MaskedTextBox
            {
                Text = definition.Text ?? string.Empty,
                ReadOnly = definition.ReadOnly == true,
                Mask = definition.Mask ?? string.Empty,
                AsciiOnly = false,
                AllowPromptAsInput = true
            };

            if (definition.MaxLength.HasValue && definition.MaxLength.Value > 0)
            {
                box.MaxLength = definition.MaxLength.Value;
            }

            return box;
        }

        private static NumericUpDown CreateNumericUpDown(ControlDefinition definition)
        {
            var numeric = new NumericUpDown
            {
                Minimum = definition.Minimum ?? 0m,
                Maximum = definition.Maximum ?? 100m,
                Increment = definition.Increment ?? 1m,
                DecimalPlaces = definition.DecimalPlaces ?? 0,
                ThousandsSeparator = false
            };

            if (numeric.Minimum > numeric.Maximum)
            {
                decimal swap = numeric.Minimum;
                numeric.Minimum = numeric.Maximum;
                numeric.Maximum = swap;
            }

            decimal value = numeric.Minimum;
            if (!string.IsNullOrWhiteSpace(definition.Text)
                && decimal.TryParse(definition.Text.Trim(), out decimal parsed))
            {
                value = parsed;
            }

            if (value < numeric.Minimum)
            {
                value = numeric.Minimum;
            }
            else if (value > numeric.Maximum)
            {
                value = numeric.Maximum;
            }

            numeric.Value = value;
            return numeric;
        }

        private static PictureBox CreatePictureBox(ControlDefinition definition)
        {
            var picture = new PictureBox
            {
                BorderStyle = BorderStyle.FixedSingle,
                SizeMode = ParsePictureSizeMode(definition.SizeMode),
                WaitOnLoad = false
            };

            TryLoadPicture(picture, definition.ImagePath);
            return picture;
        }

        private static void PopulateItems(Action<string> add, ControlDefinition definition)
        {
            if (add == null || definition == null || definition.Items == null)
            {
                return;
            }

            foreach (string item in definition.Items)
            {
                add(item ?? string.Empty);
            }
        }

        private static void ApplySelectedIndex(ListControl list, ControlDefinition definition)
        {
            if (list == null || definition == null || !definition.SelectedIndex.HasValue)
            {
                return;
            }

            int index = definition.SelectedIndex.Value;
            int count = list is ListBox listBox
                ? listBox.Items.Count
                : (list is ComboBox combo ? combo.Items.Count : 0);
            if (index >= 0 && index < count)
            {
                list.SelectedIndex = index;
            }
        }
        private static void ApplyListBoxValue(ListBox listBox, object value)
        {
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                listBox.Items.Clear();
                foreach (object item in enumerable)
                {
                    if (item != null)
                    {
                        listBox.Items.Add(Convert.ToString(item));
                    }
                }

                return;
            }

            string text = Convert.ToString(value) ?? string.Empty;
            int index = listBox.FindStringExact(text);
            listBox.SelectedIndex = index;
        }

        private static void ApplyCheckedListBoxValue(CheckedListBox list, object value)
        {
            if (value is System.Collections.IEnumerable enumerable && !(value is string))
            {
                list.Items.Clear();
                foreach (object item in enumerable)
                {
                    if (item != null)
                    {
                        list.Items.Add(Convert.ToString(item));
                    }
                }

                return;
            }

            string text = Convert.ToString(value) ?? string.Empty;
            int index = list.FindStringExact(text);
            list.SelectedIndex = index;
        }

        private static PictureBoxSizeMode ParsePictureSizeMode(string sizeMode)
        {
            if (string.IsNullOrWhiteSpace(sizeMode))
            {
                return PictureBoxSizeMode.Zoom;
            }

            switch (sizeMode.Trim())
            {
                case "Normal":
                    return PictureBoxSizeMode.Normal;
                case "StretchImage":
                    return PictureBoxSizeMode.StretchImage;
                case "AutoSize":
                    return PictureBoxSizeMode.AutoSize;
                case "CenterImage":
                    return PictureBoxSizeMode.CenterImage;
                case "Zoom":
                default:
                    return PictureBoxSizeMode.Zoom;
            }
        }

        private static void TryLoadPicture(PictureBox picture, string imagePath)
        {
            if (picture == null)
            {
                return;
            }

            Image previous = picture.Image;
            picture.Image = null;
            if (previous != null)
            {
                previous.Dispose();
            }

            if (string.IsNullOrWhiteSpace(imagePath))
            {
                picture.Tag = string.Empty;
                return;
            }

            string path = imagePath.Trim();
            picture.Tag = path;
            if (!File.Exists(path))
            {
                return;
            }

            try
            {
                // Clone so the file is not locked by the PictureBox.
                using (var loaded = Image.FromFile(path))
                {
                    picture.Image = new Bitmap(loaded);
                }
            }
            catch
            {
                // Keep empty PictureBox when path/file is invalid; Tag still holds the requested path.
            }
        }

        private static Panel CreatePanel(ControlDefinition definition, Dictionary<string, Control> map)
        {
            var panel = new Panel
            {
                BorderStyle = BorderStyle.FixedSingle,
                Tag = FormControlType.Panel
            };

            AddChildControls(panel, definition, map);
            return panel;
        }

        private static Panel CreateScrollContainer(ControlDefinition definition, Dictionary<string, Control> map)
        {
            var panel = new Panel
            {
                AutoScroll = true,
                BorderStyle = BorderStyle.FixedSingle,
                Tag = FormControlType.ScrollContainer
            };

            AddChildControls(panel, definition, map);
            return panel;
        }

        private static TableLayoutPanel CreateTableLayout(ControlDefinition definition, Dictionary<string, Control> map)
        {
            int rows = definition.RowCount.HasValue && definition.RowCount.Value > 0
                ? definition.RowCount.Value
                : 3;
            int cols = definition.ColumnCount.HasValue && definition.ColumnCount.Value > 0
                ? definition.ColumnCount.Value
                : 3;

            var table = new TableLayoutPanel
            {
                Tag = FormControlType.TableLayout,
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize,
                RowCount = rows,
                ColumnCount = cols,
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };

            ApplyTableLayoutStyles(table, rows, cols);

            if (definition.Controls != null)
            {
                foreach (ControlDefinition child in definition.Controls)
                {
                    if (child == null)
                    {
                        continue;
                    }

                    Control created = CreateControl(child, map);
                    PlaceInTableCell(
                        table,
                        created,
                        child.Row ?? 0,
                        child.Column ?? 0);
                }
            }

            return table;
        }

        public static void ApplyTableLayoutStyles(TableLayoutPanel table, int rows, int cols)
        {
            if (table == null)
            {
                return;
            }

            table.SuspendLayout();
            try
            {
                table.RowStyles.Clear();
                table.ColumnStyles.Clear();
                table.RowCount = rows;
                table.ColumnCount = cols;

                float rowPercent = rows > 0 ? 100f / rows : 100f;
                float colPercent = cols > 0 ? 100f / cols : 100f;
                for (int r = 0; r < rows; r++)
                {
                    table.RowStyles.Add(new RowStyle(SizeType.Percent, rowPercent));
                }

                for (int c = 0; c < cols; c++)
                {
                    table.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, colPercent));
                }
            }
            finally
            {
                table.ResumeLayout();
            }
        }

        public static void PlaceInTableCell(TableLayoutPanel table, Control control, int row, int column)
        {
            if (table == null || control == null)
            {
                return;
            }

            if (row < 0 || column < 0 || row >= table.RowCount || column >= table.ColumnCount)
            {
                throw new InvalidOperationException(
                    "Cell (" + row + "," + column + ") is outside TableLayout size "
                    + table.RowCount + "x" + table.ColumnCount + ".");
            }

            control.Dock = DockStyle.Fill;
            control.Margin = new Padding(2);
            table.Controls.Add(control, column, row);
        }

        public static Control GetTableCellControl(TableLayoutPanel table, int row, int column)
        {
            if (table == null)
            {
                return null;
            }

            return table.GetControlFromPosition(column, row);
        }

        private static DataGridView CreateDataGrid(ControlDefinition definition)
        {
            var grid = new DataGridView
            {
                Tag = FormControlType.DataGrid,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle
            };

            return grid;
        }

        private static GroupBox CreateGroupBox(ControlDefinition definition, Dictionary<string, Control> map)
        {
            var group = new GroupBox
            {
                Text = definition.Text ?? string.Empty
            };

            AddChildControls(group, definition, map);
            return group;
        }

        private static TabControl CreateTabControl(ControlDefinition definition, Dictionary<string, Control> map)
        {
            var tabs = new TabControl();

            if (definition.Controls != null)
            {
                foreach (ControlDefinition pageDef in definition.Controls)
                {
                    if (pageDef == null || !FormControlType.IsTabPage(pageDef.Type))
                    {
                        continue;
                    }

                    var page = new TabPage
                    {
                        Name = pageDef.Id == null ? string.Empty : pageDef.Id.Trim(),
                        Text = pageDef.Text ?? pageDef.Id ?? "Tab",
                        UseVisualStyleBackColor = true
                    };

                    if (pageDef.Enabled.HasValue)
                    {
                        page.Enabled = pageDef.Enabled.Value;
                    }

                    if (pageDef.Visible.HasValue)
                    {
                        page.Visible = pageDef.Visible.Value;
                    }

                    AddChildControls(page, pageDef, map);
                    ApplyFontAndColors(page, FormControlType.TabPage, pageDef);
                    if (!string.IsNullOrWhiteSpace(pageDef.BackColor))
                    {
                        page.UseVisualStyleBackColor = false;
                    }

                    map[page.Name] = page;
                    tabs.TabPages.Add(page);
                }
            }

            if (definition.SelectedIndex.HasValue
                && definition.SelectedIndex.Value >= 0
                && definition.SelectedIndex.Value < tabs.TabPages.Count)
            {
                tabs.SelectedIndex = definition.SelectedIndex.Value;
            }

            return tabs;
        }

        private static void AddChildControls(
            Control parent,
            ControlDefinition definition,
            Dictionary<string, Control> map)
        {
            if (parent == null || definition == null || definition.Controls == null)
            {
                return;
            }

            foreach (ControlDefinition child in definition.Controls)
            {
                parent.Controls.Add(CreateControl(child, map));
            }
        }

        private static FormStartPosition ParseStartPosition(string value)
        {
            if (Enum.TryParse(value, true, out FormStartPosition position))
            {
                return position;
            }

            return FormStartPosition.CenterScreen;
        }

        private static AnchorStyles ParseAnchor(string value)
        {
            AnchorStyles result = AnchorStyles.None;
            foreach (string part in value.Split(new[] { ',', '|', ' ' }, StringSplitOptions.RemoveEmptyEntries))
            {
                if (Enum.TryParse(part.Trim(), true, out AnchorStyles style))
                {
                    result |= style;
                }
            }

            return result == AnchorStyles.None ? AnchorStyles.Top | AnchorStyles.Left : result;
        }

        private static ScrollBars ParseScrollBars(string value)
        {
            if (Enum.TryParse(value, true, out ScrollBars bars))
            {
                return bars;
            }

            return ScrollBars.Vertical;
        }

        private static bool ToBool(object value)
        {
            if (value is bool b)
            {
                return b;
            }

            if (value == null)
            {
                return false;
            }

            bool.TryParse(Convert.ToString(value), out bool parsed);
            return parsed;
        }
    }
}
