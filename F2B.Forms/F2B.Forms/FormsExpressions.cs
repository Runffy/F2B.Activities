using System;
using F2B.Forms.Session;
using Microsoft.VisualBasic.CompilerServices;

namespace F2B.Forms
{
    /// <summary>
    /// Expression helpers for AsyncForm.
    /// OpenRPA (VB): <c>F2B.Forms.GetControlText("ControlId")</c>
    /// C#: <c>F2B.Forms.Expressions.GetControlText("ControlId")</c>
    /// </summary>
    public static class Expressions
    {
        /// <summary>
        /// Returns the current display text of the control in the active FormSession.
        /// </summary>
        public static string GetControlText(string controlId)
        {
            return RequireSession().GetControlText(controlId);
        }

        /// <summary>Control id that raised the current BindEvent (e.g. MixTable id).</summary>
        public static string GetLastControlId()
        {
            return RequireSession().LastControlId;
        }

        /// <summary>Event name of the current BindEvent (Click / Change / ...).</summary>
        public static string GetLastEventName()
        {
            return RequireSession().LastEventName;
        }

        /// <summary>Row index for MixTable / DataGrid cell events (-1 if none).</summary>
        public static int GetLastRowIndex()
        {
            return RequireSession().LastRowIndex;
        }

        /// <summary>Column name for MixTable / DataGrid cell events.</summary>
        public static string GetLastColumnName()
        {
            return RequireSession().LastColumnName;
        }

        private static FormSession RequireSession()
        {
            FormSession session = FormSessionAmbient.Current;
            if (session == null || session.IsClosed)
            {
                throw new InvalidOperationException(
                    "No active FormSession. Expression helpers must run inside AsyncForm.");
            }

            return session;
        }
    }

    /// <summary>
    /// VB StandardModule so OpenRPA expressions can call <c>F2B.Forms.GetControlText(...)</c>
    /// without the Expressions class name.
    /// </summary>
    [StandardModule]
    public sealed class FormsExpressionModule
    {
        private FormsExpressionModule()
        {
        }

        public static string GetControlText(string controlId)
        {
            return Expressions.GetControlText(controlId);
        }

        public static string GetLastControlId()
        {
            return Expressions.GetLastControlId();
        }

        public static string GetLastEventName()
        {
            return Expressions.GetLastEventName();
        }

        public static int GetLastRowIndex()
        {
            return Expressions.GetLastRowIndex();
        }

        public static string GetLastColumnName()
        {
            return Expressions.GetLastColumnName();
        }
    }
}
