using System.Collections.Generic;

namespace F2B.Forms.Session
{
    /// <summary>
    /// Process-wide active FormSession stack so activities in a nested Invoke OpenRPA
    /// workflow can still resolve the parent AsyncForm session (WF extensions do not cross instances).
    /// </summary>
    internal static class FormSessionAmbient
    {
        private static readonly object Sync = new object();
        private static readonly Stack<FormSession> Stack = new Stack<FormSession>();

        public static void Push(FormSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (Sync)
            {
                Stack.Push(session);
            }
        }

        public static void Pop(FormSession session)
        {
            if (session == null)
            {
                return;
            }

            lock (Sync)
            {
                if (Stack.Count == 0)
                {
                    return;
                }

                if (ReferenceEquals(Stack.Peek(), session))
                {
                    Stack.Pop();
                    return;
                }

                // Defensive: remove the matching instance if stack order was disturbed.
                var kept = new Stack<FormSession>();
                while (Stack.Count > 0)
                {
                    FormSession top = Stack.Pop();
                    if (ReferenceEquals(top, session))
                    {
                        break;
                    }

                    kept.Push(top);
                }

                while (kept.Count > 0)
                {
                    Stack.Push(kept.Pop());
                }
            }
        }

        public static FormSession Current
        {
            get
            {
                lock (Sync)
                {
                    return Stack.Count > 0 ? Stack.Peek() : null;
                }
            }
        }
    }
}
