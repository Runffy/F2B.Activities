using System;
using System.Activities;

namespace F2B.Forms.Session
{
    public static class FormSessionAccess
    {
        public static FormSession GetRequired(ActivityContext context)
        {
            if (context == null)
            {
                throw new ArgumentNullException(nameof(context));
            }

            FormSession session = Resolve(context);
            if (session == null || session.IsClosed)
            {
                throw new InvalidOperationException(
                    "No active FormSession. This activity must run inside AsyncForm (InitScope or BindEvent handler), including nested Invoke OpenRPA workflows.");
            }

            return session;
        }

        public static FormSession TryGet(ActivityContext context)
        {
            return Resolve(context);
        }

        private static FormSession Resolve(ActivityContext context)
        {
            if (context != null)
            {
                FormSessionHolder holder = context.GetExtension<FormSessionHolder>();
                if (holder != null && holder.Current != null)
                {
                    return holder.Current;
                }
            }

            // Nested Invoke OpenRPA starts a new WorkflowApplication without parent extensions.
            return FormSessionAmbient.Current;
        }

        public static UiBehavior ParseUiBehavior(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return UiBehavior.NoLock;
            }

            if (Enum.TryParse(value.Trim(), true, out UiBehavior behavior))
            {
                return behavior;
            }

            return UiBehavior.NoLock;
        }
    }
}
