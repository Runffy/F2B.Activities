using System;
using System.Activities;
using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Threading;
using System.Windows;

namespace F2B.Basic
{
    [Designer(typeof(MessageBoxDesigner), typeof(System.ComponentModel.Design.IDesigner))]
    [DisplayName("Message Box")]
    [Description("Shows a native Win32 MessageBox (auto-sizes for multi-line text). Optional Top Most uses MB_TOPMOST for the dialog lifetime.")]
    public sealed class MessageBoxActivity : CodeActivity, System.Activities.Presentation.IActivityTemplateFactory
    {
        private const uint MbOk = 0x00000000;
        private const uint MbIconInformation = 0x00000040;
        private const uint MbSetForeground = 0x00010000;
        private const uint MbTopMost = 0x00040000;

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
        [Description("When true, uses Win32 MB_TOPMOST so the message box stays above other windows for its lifetime.")]
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
                    ShowNativeMessageBox(message, title, timeoutMs, topMost);
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

        private static void ShowNativeMessageBox(string message, string title, int timeoutMs, bool topMost)
        {
            uint type = MbOk | MbIconInformation | MbSetForeground;
            if (topMost)
            {
                type |= MbTopMost;
            }

            // Native MessageBox auto-sizes for multi-line text.
            // MessageBoxTimeout with 0 ms behaves like a normal MessageBox on supported Windows.
            MessageBoxTimeout(
                IntPtr.Zero,
                message ?? string.Empty,
                title ?? "OpenRPA",
                type,
                0,
                timeoutMs <= 0 ? 0 : timeoutMs);
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
