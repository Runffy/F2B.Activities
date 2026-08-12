using System;
using System.Drawing;
using System.Runtime.CompilerServices;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// WinForms ComboBox has no ReadOnly property. This simulates TextBox-like read-only:
    /// stays Enabled (text remains readable) but blocks dropdown / keyboard / mouse-wheel changes.
    /// </summary>
    internal static class ComboBoxReadOnly
    {
        private sealed class State
        {
            public bool ReadOnly;
            public bool HandlersAttached;
            public Color? SavedForeColor;
            public Color? SavedBackColor;
        }

        private static readonly ConditionalWeakTable<ComboBox, State> States =
            new ConditionalWeakTable<ComboBox, State>();

        internal static void Set(ComboBox combo, bool readOnly)
        {
            if (combo == null || combo.IsDisposed)
            {
                return;
            }

            State state = States.GetOrCreateValue(combo);
            EnsureHandlers(combo, state);

            if (state.ReadOnly == readOnly)
            {
                ApplyVisual(combo, state, readOnly);
                return;
            }

            state.ReadOnly = readOnly;
            if (readOnly)
            {
                if (!state.SavedForeColor.HasValue)
                {
                    state.SavedForeColor = combo.ForeColor;
                    state.SavedBackColor = combo.BackColor;
                }

                if (combo.DroppedDown)
                {
                    combo.DroppedDown = false;
                }
            }

            ApplyVisual(combo, state, readOnly);
            combo.Invalidate();
        }

        internal static bool IsReadOnly(ComboBox combo)
        {
            return combo != null
                && States.TryGetValue(combo, out State state)
                && state.ReadOnly;
        }

        private static void EnsureHandlers(ComboBox combo, State state)
        {
            if (state.HandlersAttached)
            {
                return;
            }

            combo.DropDown += Combo_DropDown;
            combo.KeyDown += Combo_KeyDown;
            combo.MouseWheel += Combo_MouseWheel;
            state.HandlersAttached = true;
        }

        private static void ApplyVisual(ComboBox combo, State state, bool readOnly)
        {
            if (readOnly)
            {
                combo.ForeColor = SystemColors.GrayText;
                combo.BackColor = SystemColors.Control;
            }
            else if (state.SavedForeColor.HasValue)
            {
                combo.ForeColor = state.SavedForeColor.Value;
                combo.BackColor = state.SavedBackColor ?? SystemColors.Window;
                state.SavedForeColor = null;
                state.SavedBackColor = null;
            }
        }

        private static void Combo_DropDown(object sender, EventArgs e)
        {
            var combo = sender as ComboBox;
            if (!IsReadOnly(combo))
            {
                return;
            }

            combo.BeginInvoke(new Action(() =>
            {
                if (!combo.IsDisposed)
                {
                    combo.DroppedDown = false;
                }
            }));
        }

        private static void Combo_KeyDown(object sender, KeyEventArgs e)
        {
            var combo = sender as ComboBox;
            if (!IsReadOnly(combo))
            {
                return;
            }

            if (e.KeyCode == Keys.Up
                || e.KeyCode == Keys.Down
                || e.KeyCode == Keys.PageUp
                || e.KeyCode == Keys.PageDown
                || e.KeyCode == Keys.Home
                || e.KeyCode == Keys.End
                || e.KeyCode == Keys.F4
                || (e.Alt && (e.KeyCode == Keys.Up || e.KeyCode == Keys.Down)))
            {
                e.Handled = true;
                e.SuppressKeyPress = true;
            }
        }

        private static void Combo_MouseWheel(object sender, MouseEventArgs e)
        {
            var combo = sender as ComboBox;
            if (!IsReadOnly(combo))
            {
                return;
            }

            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }
        }
    }
}
