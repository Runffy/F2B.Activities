using System;
using System.Collections;
using System.Collections.Generic;
using System.Drawing;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Modal popup: message + ComboBox + OK. Closing via X leaves <see cref="SelectedItem"/> null
    /// and returns <see cref="DialogResult.Cancel"/>.
    /// </summary>
    internal sealed class SelectPopupForm : Form
    {
        private readonly ComboBox _combo;
        private readonly Button _okButton;
        private bool _accepted;

        public object SelectedItem { get; private set; }

        public SelectPopupForm(
            string title,
            string message,
            IList<object> options,
            string buttonName)
        {
            Text = string.IsNullOrWhiteSpace(title) ? "Select" : title.Trim();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;
            ClientSize = new Size(420, 160);
            MinimumSize = new Size(360, 160);

            var messageLabel = new Label
            {
                AutoSize = false,
                Location = new Point(12, 12),
                Size = new Size(396, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = message ?? string.Empty
            };

            _combo = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(12, 60),
                Size = new Size(396, 24),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
            };

            if (options != null)
            {
                foreach (object item in options)
                {
                    _combo.Items.Add(item ?? string.Empty);
                }
            }

            if (_combo.Items.Count > 0)
            {
                _combo.SelectedIndex = -1;
            }

            _okButton = new Button
            {
                Text = string.IsNullOrWhiteSpace(buttonName) ? "OK" : buttonName.Trim(),
                Location = new Point(312, 108),
                Size = new Size(96, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.None
            };
            _okButton.Click += OnOkClick;

            AcceptButton = _okButton;
            Controls.Add(messageLabel);
            Controls.Add(_combo);
            Controls.Add(_okButton);
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            _accepted = true;
            SelectedItem = NormalizeSelected(_combo.SelectedItem);
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (!_accepted)
            {
                SelectedItem = null;
                if (DialogResult == DialogResult.None)
                {
                    DialogResult = DialogResult.Cancel;
                }
            }

            base.OnFormClosed(e);
        }

        private static object NormalizeSelected(object selected)
        {
            if (selected == null || selected == DBNull.Value)
            {
                return null;
            }

            // Empty string from a null option placeholder counts as null.
            if (selected is string s && string.IsNullOrEmpty(s))
            {
                return null;
            }

            return selected;
        }

        public static List<object> ParseOptions(object options)
        {
            var list = new List<object>();
            if (options == null)
            {
                return list;
            }

            if (options is string || options is System.Text.StringBuilder)
            {
                list.Add(Convert.ToString(options) ?? string.Empty);
                return list;
            }

            IEnumerable enumerable = options as IEnumerable;
            if (enumerable == null)
            {
                list.Add(options);
                return list;
            }

            foreach (object item in enumerable)
            {
                list.Add(item);
            }

            return list;
        }
    }
}
