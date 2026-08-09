using System;
using System.Activities.Presentation;
using System.Activities.Presentation.Model;
using System.ComponentModel;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;

namespace F2B.Forms.Designers
{
    public sealed class SetFontDesigner : ActivityDesigner
    {
        private readonly TextBox _controlIdBox;
        private readonly TextBox _fontFamilyBox;
        private readonly TextBox _fontSizeBox;
        private readonly ComboBox _boldCombo;
        private readonly ComboBox _italicCombo;
        private readonly ComboBox _underlineCombo;
        private readonly TextBox _foreColorBox;
        private bool _suppressUi;

        public SetFontDesigner()
        {
            var root = new StackPanel();

            root.Children.Add(CreateHint(
                "Leave Family / Size / Color empty to keep current. Style: blank=keep, None=clear, True=apply."));

            root.Children.Add(CreateLabel("Control Id"));
            _controlIdBox = CreateTextBox();
            _controlIdBox.LostFocus += (s, e) => CommitText(_controlIdBox, "ControlId");
            root.Children.Add(_controlIdBox);

            root.Children.Add(CreateLabel("Font Family"));
            _fontFamilyBox = CreateTextBox();
            _fontFamilyBox.LostFocus += (s, e) => CommitOptionalText(_fontFamilyBox, "FontFamily");
            root.Children.Add(_fontFamilyBox);

            root.Children.Add(CreateLabel("Font Size"));
            _fontSizeBox = CreateTextBox();
            _fontSizeBox.LostFocus += (s, e) => CommitOptionalText(_fontSizeBox, "FontSize");
            root.Children.Add(_fontSizeBox);

            root.Children.Add(CreateLabel("Bold"));
            _boldCombo = CreateStyleCombo();
            _boldCombo.SelectionChanged += (s, e) => CommitStyle(_boldCombo, "Bold");
            root.Children.Add(_boldCombo);

            root.Children.Add(CreateLabel("Italic"));
            _italicCombo = CreateStyleCombo();
            _italicCombo.SelectionChanged += (s, e) => CommitStyle(_italicCombo, "Italic");
            root.Children.Add(_italicCombo);

            root.Children.Add(CreateLabel("Underline"));
            _underlineCombo = CreateStyleCombo();
            _underlineCombo.SelectionChanged += (s, e) => CommitStyle(_underlineCombo, "Underline");
            root.Children.Add(_underlineCombo);

            root.Children.Add(CreateLabel("Fore Color"));
            _foreColorBox = CreateTextBox();
            _foreColorBox.LostFocus += (s, e) => CommitOptionalText(_foreColorBox, "ForeColor");
            root.Children.Add(_foreColorBox);

            Content = new Border
            {
                BorderBrush = Brushes.Gray,
                BorderThickness = new Thickness(1),
                Padding = new Thickness(8),
                MinWidth = 280,
                Child = root
            };

            Loaded += (s, e) =>
            {
                HookModelItem();
                SyncFromModel();
            };
        }

        private void HookModelItem()
        {
            if (ModelItem == null)
            {
                return;
            }

            ModelItem.PropertyChanged -= OnModelItemPropertyChanged;
            ModelItem.PropertyChanged += OnModelItemPropertyChanged;
        }

        private void OnModelItemPropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            if (e.PropertyName == "ControlId"
                || e.PropertyName == "FontFamily"
                || e.PropertyName == "FontSize"
                || e.PropertyName == "Bold"
                || e.PropertyName == "Italic"
                || e.PropertyName == "Underline"
                || e.PropertyName == "ForeColor")
            {
                SyncFromModel();
            }
        }

        private void SyncFromModel()
        {
            if (ModelItem == null)
            {
                return;
            }

            _suppressUi = true;
            try
            {
                _controlIdBox.Text = GetLiteral("ControlId") ?? string.Empty;
                _fontFamilyBox.Text = GetLiteral("FontFamily") ?? string.Empty;
                _fontSizeBox.Text = GetLiteral("FontSize") ?? string.Empty;
                _foreColorBox.Text = GetLiteral("ForeColor") ?? string.Empty;
                SetStyleCombo(_boldCombo, GetLiteral("Bold"));
                SetStyleCombo(_italicCombo, GetLiteral("Italic"));
                SetStyleCombo(_underlineCombo, GetLiteral("Underline"));
            }
            finally
            {
                _suppressUi = false;
            }
        }

        private string GetLiteral(string propertyName)
        {
            ModelProperty property = ModelItem.Properties[propertyName];
            if (property == null || property.Value == null)
            {
                return null;
            }

            DesignTimeFormPath.TryGetLiteralString(property.Value, out string value);
            return value;
        }

        private void CommitText(TextBox box, string propertyName)
        {
            if (_suppressUi || ModelItem == null || box == null)
            {
                return;
            }

            DesignTimeFormPath.SetLiteralString(ModelItem, propertyName, box.Text == null ? string.Empty : box.Text.Trim());
        }

        private void CommitOptionalText(TextBox box, string propertyName)
        {
            if (_suppressUi || ModelItem == null || box == null)
            {
                return;
            }

            string text = box.Text == null ? string.Empty : box.Text.Trim();
            DesignTimeFormPath.SetOptionalLiteralString(ModelItem, propertyName, text);
        }

        private void CommitStyle(ComboBox combo, string propertyName)
        {
            if (_suppressUi || ModelItem == null || combo == null)
            {
                return;
            }

            string selected = GetStyleComboValue(combo);
            DesignTimeFormPath.SetOptionalLiteralString(ModelItem, propertyName, selected);
        }

        private static string GetStyleComboValue(ComboBox combo)
        {
            if (combo.SelectedItem is ComboBoxItem item)
            {
                return item.Tag == null ? string.Empty : Convert.ToString(item.Tag);
            }

            return combo.SelectedItem == null ? string.Empty : Convert.ToString(combo.SelectedItem);
        }

        private static void SetStyleCombo(ComboBox combo, string value)
        {
            string normalized = string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
            if (string.Equals(normalized, "Yes", StringComparison.OrdinalIgnoreCase))
            {
                normalized = "True";
            }

            foreach (object entry in combo.Items)
            {
                string tag = entry is ComboBoxItem cbi
                    ? (cbi.Tag == null ? string.Empty : Convert.ToString(cbi.Tag))
                    : Convert.ToString(entry);
                if (string.Equals(tag, normalized, StringComparison.OrdinalIgnoreCase))
                {
                    combo.SelectedItem = entry;
                    return;
                }
            }

            combo.SelectedIndex = 0;
        }

        private static ComboBox CreateStyleCombo()
        {
            var combo = new ComboBox
            {
                IsEditable = false,
                MinWidth = 260,
                Margin = new Thickness(0, 0, 0, 6)
            };
            // blank = keep, None = clear style, True = apply style
            combo.Items.Add(new ComboBoxItem { Content = string.Empty, Tag = string.Empty });
            combo.Items.Add(new ComboBoxItem { Content = "None", Tag = "None" });
            combo.Items.Add(new ComboBoxItem { Content = "True", Tag = "True" });
            combo.SelectedIndex = 0;
            return combo;
        }

        private static TextBox CreateTextBox()
        {
            return new TextBox
            {
                MinWidth = 260,
                Margin = new Thickness(0, 0, 0, 6)
            };
        }

        private static TextBlock CreateLabel(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontWeight = FontWeights.SemiBold,
                Margin = new Thickness(0, 0, 0, 2)
            };
        }

        private static TextBlock CreateHint(string text)
        {
            return new TextBlock
            {
                Text = text,
                FontSize = 10,
                Foreground = Brushes.DimGray,
                TextWrapping = TextWrapping.Wrap,
                Margin = new Thickness(0, 0, 0, 8)
            };
        }
    }
}
