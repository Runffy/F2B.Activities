using System;
using System.Activities;
using F2B.Forms.Engine;

namespace F2B.Forms.Session
{
    /// <summary>
    /// Routes "On Form" dialogs to the active <see cref="FormSession"/> when present;
    /// otherwise shows a standalone modal (no form required). Always blocks until closed.
    /// </summary>
    public static class FormDialogAccess
    {
        public static void ShowMessage(ActivityContext context, string message, string title)
        {
            FormSession session = TryActiveSession(context);
            if (session != null)
            {
                session.ShowMessage(message, title);
                return;
            }

            StandaloneFormDialogs.ShowMessage(message, title);
        }

        public static bool RequestConfirm(
            ActivityContext context,
            string message,
            string title,
            string yesText = null,
            string noText = null)
        {
            FormSession session = TryActiveSession(context);
            if (session != null)
            {
                return session.RequestConfirm(message, title, yesText, noText);
            }

            return StandaloneFormDialogs.RequestConfirm(message, title);
        }

        public static object ShowSelect(
            ActivityContext context,
            string title,
            string message,
            object options,
            string buttonName,
            bool allowNull)
        {
            object selected;
            FormSession session = TryActiveSession(context);
            if (session != null)
            {
                selected = session.ShowSelectPopup(title, message, options, buttonName, allowNull: true);
            }
            else
            {
                selected = StandaloneFormDialogs.ShowSelect(title, message, options, buttonName);
            }

            if (!allowNull && selected == null)
            {
                throw new InvalidOperationException(
                    "Select popup returned null but Allow Null is false "
                    + "(no selection, null option, or the dialog was closed).");
            }

            return selected;
        }

        public static string ShowInput(
            ActivityContext context,
            string title,
            string message,
            bool isPassword,
            string buttonName,
            bool allowNull)
        {
            string input;
            FormSession session = TryActiveSession(context);
            if (session != null)
            {
                input = session.ShowInputPopup(title, message, isPassword, buttonName, allowNull: true);
            }
            else
            {
                input = StandaloneFormDialogs.ShowInput(title, message, isPassword, buttonName);
            }

            if (!allowNull && input == null)
            {
                throw new InvalidOperationException(
                    "Input popup returned null but Allow Null is false "
                    + "(the dialog was closed without OK).");
            }

            return input;
        }

        private static FormSession TryActiveSession(ActivityContext context)
        {
            FormSession session = FormSessionAccess.TryGet(context);
            if (session == null || session.IsClosed)
            {
                return null;
            }

            return session;
        }
    }
}
