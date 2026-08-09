using System.Collections.Generic;

namespace F2B.Forms.Designer
{
    internal sealed class DesignSnapshot
    {
        public string Json { get; set; }
        public string SelectedId { get; set; }
        public string[] SelectedIds { get; set; }
        public int IdCounter { get; set; }

        public bool ContentEquals(DesignSnapshot other)
        {
            return other != null
                && IdCounter == other.IdCounter
                && string.Equals(Json, other.Json, System.StringComparison.Ordinal);
        }
    }

    /// <summary>
    /// Snapshot-based undo/redo for the form designer document.
    /// </summary>
    internal sealed class DesignHistory
    {
        private const int MaxDepth = 100;
        private readonly List<DesignSnapshot> _undo = new List<DesignSnapshot>();
        private readonly List<DesignSnapshot> _redo = new List<DesignSnapshot>();

        public bool CanUndo
        {
            get { return _undo.Count > 0; }
        }

        public bool CanRedo
        {
            get { return _redo.Count > 0; }
        }

        public void Clear()
        {
            _undo.Clear();
            _redo.Clear();
        }

        public void PushUndo(DesignSnapshot beforeEdit)
        {
            if (beforeEdit == null)
            {
                return;
            }

            _undo.Add(beforeEdit);
            _redo.Clear();
            Trim(_undo);
        }

        public void DiscardLastUndo()
        {
            if (_undo.Count > 0)
            {
                _undo.RemoveAt(_undo.Count - 1);
            }
        }

        public DesignSnapshot Undo(DesignSnapshot current)
        {
            if (!CanUndo || current == null)
            {
                return null;
            }

            DesignSnapshot previous = Pop(_undo);
            _redo.Add(current);
            Trim(_redo);
            return previous;
        }

        public DesignSnapshot Redo(DesignSnapshot current)
        {
            if (!CanRedo || current == null)
            {
                return null;
            }

            DesignSnapshot next = Pop(_redo);
            _undo.Add(current);
            Trim(_undo);
            return next;
        }

        private static DesignSnapshot Pop(List<DesignSnapshot> stack)
        {
            int index = stack.Count - 1;
            DesignSnapshot item = stack[index];
            stack.RemoveAt(index);
            return item;
        }

        private static void Trim(List<DesignSnapshot> stack)
        {
            while (stack.Count > MaxDepth)
            {
                stack.RemoveAt(0);
            }
        }
    }
}
