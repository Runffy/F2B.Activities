using System;
using System.Activities;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;
using WinForms = System.Windows.Forms;

namespace F2B.Basic
{
    [Designer(typeof(MessageBoxDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Message Box")]
    public sealed class MessageBoxActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private const uint MbOk = 0x00000000;
        private const uint MbIconInformation = 0x00000040;
        private const uint MbTopmost = 0x00040000;

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
        [Description("When true, the message box is displayed at the top-most level.")]
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
                    ShowClassicMessageBox(message, title, timeoutMs, topMost);
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

        private static void ShowClassicMessageBox(string message, string title, int timeoutMs, bool topMost)
        {
            if (timeoutMs <= 0 && !topMost)
            {
                WinForms.MessageBox.Show(
                    message,
                    title,
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon.Information);
                return;
            }

            uint type = MbOk | MbIconInformation;
            if (topMost)
            {
                type |= MbTopmost;
            }

            if (timeoutMs <= 0)
            {
                MessageBox(IntPtr.Zero, message, title, type);
                return;
            }

            MessageBoxTimeout(
                IntPtr.Zero,
                message,
                title,
                type,
                0,
                timeoutMs);
        }

        [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern int MessageBox(IntPtr hWnd, string text, string caption, uint type);

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
