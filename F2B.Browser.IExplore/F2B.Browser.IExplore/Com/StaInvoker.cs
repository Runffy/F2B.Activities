using System;
using System.Threading;

namespace F2B.Browser.IExplore.Com
{
    /// <summary>Runs COM/IE work on an STA thread (required for InternetExplorer.Application from OpenRPA/Workflow).</summary>
    internal static class StaInvoker
    {
        public static void Invoke(Action action)
        {
            if (action == null)
                throw new ArgumentNullException(nameof(action));

            if (Thread.CurrentThread.GetApartmentState() == ApartmentState.STA)
            {
                action();
                return;
            }

            Exception captured = null;
            var thread = new Thread(() =>
            {
                try
                {
                    action();
                }
                catch (Exception ex)
                {
                    captured = ex;
                }
            })
            {
                IsBackground = true,
                Name = "F2B.IExplore.STA"
            };
            thread.SetApartmentState(ApartmentState.STA);
            thread.Start();
            thread.Join();
            if (captured != null)
                throw captured;
        }
    }
}
