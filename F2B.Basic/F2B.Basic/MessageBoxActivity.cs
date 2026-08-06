using System;
using System.Activities;
using System.ComponentModel;
using System.Drawing;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using System.Windows.Forms;
using DrawingPoint = System.Drawing.Point;
using DrawingSize = System.Drawing.Size;
using WinForms = System.Windows.Forms;

namespace F2B.Basic
{
    [Designer(typeof(MessageBoxDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Message Box")]
    public sealed class MessageBoxActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private static readonly IntPtr HwndTopMost = new IntPtr(-1);
        private const uint SwpNosize = 0x0001;
        private const uint SwpNomove = 0x0002;
        private const uint SwpShowwindow = 0x0040;

        public MessageBoxActivity()
        {
            DisplayName = "Message Box";
            Title = new InArgument<string>("OpenRPA");
            Timeout = new InArgument<int>(0);
            TopMost = new InArgument<bool>(true);
        }

        [RequiredArgument]
        [DisplayName("Message")]
        [Category("Input.A")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Title")]
        [Category("Input.A")]
        public InArgument<string> Title { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Automatically closes the dialog after this many milliseconds. Use 0 for no auto-close.")]
        [Category("Input.Z")]
        public InArgument<int> Timeout { get; set; }

        [DisplayName("Top Most")]
        [Description("When true, the message box stays above other windows for its lifetime.")]
        [Category("Input.A")]
        public InArgument<bool> TopMost { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new MessageBoxActivity
            {
                Title = new InArgument<string>("OpenRPA"),
                Timeout = new InArgument<int>(0),
                TopMost = new InArgument<bool>(true)
            };
        }

        protected override void Execute(CodeActivityContext context)
        {
            string message = Message.Get(context) ?? string.Empty;
            if (string.IsNullOrWhiteSpace(message))
            {
                throw new ArgumentException("Message is required.", nameof(Message));
            }

            string title = Title.Get(context) ?? "OpenRPA";
            int timeoutMs = Timeout.Get(context);
            if (timeoutMs < 0)
            {
                timeoutMs = 0;
            }

            bool topMost = TopMost.Get(context);
            ShowMessageBoxOnStaThread(message, title, timeoutMs, topMost);
        }

        private static void ShowMessageBoxOnStaThread(string message, string title, int timeoutMs, bool topMost)
        {
            Exception capturedException = null;
            var completed = new ManualResetEvent(false);

            var thread = new Thread(() =>
            {
                try
                {
                    if (topMost)
                    {
                        ShowTopMostForm(message, title, timeoutMs);
                    }
                    else
                    {
                        ShowNormalMessageBox(message, title, timeoutMs);
                    }
                }
                catch (Exception ex)
                {
                    capturedException = ex;
                }
                finally
                {
                    completed.Set();
                }
            });

            thread.SetApartmentState(ApartmentState.STA);
            thread.IsBackground = true;
            thread.Start();

            completed.WaitOne();
            if (capturedException != null)
            {
                throw capturedException;
            }
        }

        private static void ShowNormalMessageBox(string message, string title, int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                WinForms.MessageBox.Show(
                    message,
                    title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            MessageBoxTimeout(
                IntPtr.Zero,
                message,
                title,
                0x00000000u | 0x00000040u,
                0,
                timeoutMs);
        }

        private static void ShowTopMostForm(string message, string title, int timeoutMs)
        {
            using (var form = new Form())
            {
                form.Text = title ?? "OpenRPA";
                form.StartPosition = FormStartPosition.CenterScreen;
                form.FormBorderStyle = FormBorderStyle.FixedDialog;
                form.MaximizeBox = false;
                form.MinimizeBox = false;
                form.ShowInTaskbar = true;
                form.TopMost = true;
                form.AutoScaleMode = AutoScaleMode.Font;
                form.ClientSize = new DrawingSize(420, 160);
                form.MinimumSize = new DrawingSize(360, 140);

                var iconBox = new PictureBox
                {
                    Image = SystemIcons.Information.ToBitmap(),
                    SizeMode = PictureBoxSizeMode.CenterImage,
                    Location = new DrawingPoint(16, 24),
                    Size = new DrawingSize(32, 32)
                };

                var messageLabel = new Label
                {
                    AutoSize = false,
                    Location = new DrawingPoint(64, 20),
                    Size = new DrawingSize(form.ClientSize.Width - 80, 70),
                    Text = message ?? string.Empty,
                    Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right
                };

                var okButton = new Button
                {
                    Text = "OK",
                    DialogResult = DialogResult.OK,
                    Size = new DrawingSize(88, 28),
                    Anchor = AnchorStyles.Bottom | AnchorStyles.Right
                };
                okButton.Location = new DrawingPoint(form.ClientSize.Width - okButton.Width - 16, form.ClientSize.Height - okButton.Height - 16);
                form.AcceptButton = okButton;

                form.Controls.Add(iconBox);
                form.Controls.Add(messageLabel);
                form.Controls.Add(okButton);

                System.Windows.Forms.Timer keepTopTimer = null;
                System.Windows.Forms.Timer closeTimer = null;

                form.Shown += (s, e) =>
                {
                    ForceTopMost(form);
                    form.Activate();
                    okButton.Focus();

                    keepTopTimer = new System.Windows.Forms.Timer { Interval = 250 };
                    keepTopTimer.Tick += (sender, args) => ForceTopMost(form);
                    keepTopTimer.Start();

                    if (timeoutMs > 0)
                    {
                        closeTimer = new System.Windows.Forms.Timer { Interval = timeoutMs };
                        closeTimer.Tick += (sender, args) =>
                        {
                            closeTimer.Stop();
                            form.DialogResult = DialogResult.OK;
                            form.Close();
                        };
                        closeTimer.Start();
                    }
                };

                form.FormClosed += (s, e) =>
                {
                    if (keepTopTimer != null)
                    {
                        keepTopTimer.Stop();
                        keepTopTimer.Dispose();
                    }

                    if (closeTimer != null)
                    {
                        closeTimer.Stop();
                        closeTimer.Dispose();
                    }
                };

                form.ShowDialog();
            }
        }

        private static void ForceTopMost(Form form)
        {
            if (form == null || form.IsDisposed || !form.IsHandleCreated)
            {
                return;
            }

            form.TopMost = true;
            SetWindowPos(form.Handle, HwndTopMost, 0, 0, 0, 0, SwpNomove | SwpNosize | SwpShowwindow);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool SetWindowPos(
            IntPtr hWnd,
            IntPtr hWndInsertAfter,
            int x,
            int y,
            int cx,
            int cy,
            uint flags);

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBoxTimeout(
            IntPtr hWnd,
            string text,
            string caption,
            uint type,
            short languageId,
            int milliseconds);
    }
}
