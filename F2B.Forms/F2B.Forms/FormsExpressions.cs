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
            FormSession session = FormSessionAmbient.Current;
            if (session == null || session.IsClosed)
            {
                throw new InvalidOperationException(
                    "No active FormSession. GetControlText must run inside AsyncForm.");
            }

            return session.GetControlText(controlId);
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
    }
}
