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
        public MessageBoxActivity()
        {
            Title = new InArgument<string>("OpenRPA");
            Timeout = new InArgument<int>(0);
        }

        [RequiredArgument]
        [DisplayName("Message")]
        public InArgument<string> Message { get; set; }

        [DisplayName("Title")]
        public InArgument<string> Title { get; set; }

        [DisplayName("Timeout (ms)")]
        [Description("Automatically closes the dialog after this many milliseconds. Use 0 for no auto-close.")]
        public InArgument<int> Timeout { get; set; }

        public Activity Create(DependencyObject target)
        {
            return new MessageBoxActivity
            {
                Title = new InArgument<string>("OpenRPA"),
                Timeout = new InArgument<int>(0)
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

            ShowMessageBoxOnStaThread(message, title, timeoutMs);
        }

        private static void ShowMessageBoxOnStaThread(string message, string title, int timeoutMs)
        {
            Exception capturedException = null;
            var completed = new ManualResetEvent(false);

            var thread = new Thread(() =>
            {
                try
                {
                    ShowClassicMessageBox(message, title, timeoutMs);
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

        private static void ShowClassicMessageBox(string message, string title, int timeoutMs)
        {
            if (timeoutMs <= 0)
            {
                WinForms.MessageBox.Show(
                    message,
                    title,
                    WinForms.MessageBoxButtons.OK,
                    WinForms.MessageBoxIcon.Information);
                return;
            }

            MessageBoxTimeout(
                IntPtr.Zero,
                message,
                title,
                0,
                0,
                timeoutMs);
        }

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
