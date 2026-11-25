using System;
using System.Drawing;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Modal popup: message + text input (multiline or password) + OK.
    /// Closing via X leaves <see cref="InputText"/> null and returns <see cref="DialogResult.Cancel"/>.
    /// </summary>
    internal sealed class InputPopupForm : Form
    {
        private readonly TextBox _input;
        private readonly Button _okButton;
        private bool _accepted;

        public string InputText { get; private set; }

        public InputPopupForm(
            string title,
            string message,
            bool isPassword,
            string buttonName)
        {
            Text = string.IsNullOrWhiteSpace(title) ? "Input" : title.Trim();
            FormBorderStyle = FormBorderStyle.FixedDialog;
            MaximizeBox = false;
            MinimizeBox = false;
            ShowInTaskbar = false;
            StartPosition = FormStartPosition.CenterParent;
            AutoScaleMode = AutoScaleMode.Font;

            int clientHeight = isPassword ? 160 : 220;
            ClientSize = new Size(420, clientHeight);
            MinimumSize = new Size(360, clientHeight);

            var messageLabel = new Label
            {
                AutoSize = false,
                Location = new Point(12, 12),
                Size = new Size(396, 40),
                Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                Text = message ?? string.Empty
            };

            if (isPassword)
            {
                _input = new TextBox
                {
                    Location = new Point(12, 60),
                    Size = new Size(396, 24),
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right,
                    UseSystemPasswordChar = true
                };
            }
            else
            {
                _input = new TextBox
                {
                    Location = new Point(12, 60),
                    Size = new Size(396, 100),
                    Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    AcceptsReturn = true
                };
            }

            int buttonY = clientHeight - 52;
            _okButton = new Button
            {
                Text = string.IsNullOrWhiteSpace(buttonName) ? "OK" : buttonName.Trim(),
                Location = new Point(312, buttonY),
                Size = new Size(96, 28),
                Anchor = AnchorStyles.Bottom | AnchorStyles.Right,
                DialogResult = DialogResult.None
            };
            _okButton.Click += OnOkClick;

            AcceptButton = isPassword ? _okButton : null;
            Controls.Add(messageLabel);
            Controls.Add(_input);
            Controls.Add(_okButton);

            Shown += (s, e) => _input.Focus();
        }

        private void OnOkClick(object sender, EventArgs e)
        {
            _accepted = true;
            InputText = _input.Text ?? string.Empty;
            DialogResult = DialogResult.OK;
            Close();
        }

        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            if (!_accepted)
            {
                InputText = null;
                if (DialogResult == DialogResult.None)
                {
                    DialogResult = DialogResult.Cancel;
                }
            }

            base.OnFormClosed(e);
        }
    }
}
