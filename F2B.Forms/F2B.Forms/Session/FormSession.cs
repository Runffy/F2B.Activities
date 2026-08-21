using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Reflection;
using System.Threading;
using System.Windows.Forms;
using F2B.Forms.Engine;
using F2B.Forms.Model;
using Newtonsoft.Json;

namespace F2B.Forms.Session
{
    public sealed class FormSession : IDisposable
    {
        public const string PropertyName = "F2B.Forms.FormSession";
        public const string EventBookmarkName = "F2B.Forms.FormEvent";

        private readonly object _sync = new object();
        private readonly ConcurrentQueue<FormEvent> _pendingEvents = new ConcurrentQueue<FormEvent>();
        private readonly Dictionary<string, List<BoundEvent>> _bindings =
            new Dictionary<string, List<BoundEvent>>(StringComparer.OrdinalIgnoreCase);
        private readonly Dictionary<string, Action> _wiredNatives =
            new Dictionary<string, Action>(StringComparer.OrdinalIgnoreCase);

        private Thread _uiThread;
        private Form _form;
        private Dictionary<string, Control> _controls;
        private FormDefinition _definition;
        private ManualResetEventSlim _uiReady;
        private ManualResetEventSlim _uiClosed;
        private Exception _uiException;
        private volatile bool _handlerRunning;
        private volatile bool _uiLocked;
        private volatile bool _closed;
        private volatile bool _acceptEvents = true;
        private UiBehavior _activeBehavior = UiBehavior.LockQueue;
        private System.Windows.Forms.Timer _timeoutTimer;
        private WorkflowInstanceExtension _workflow;
        private string _bookmarkName = EventBookmarkName;
        private bool _ambientPushed;

        public FormCloseReason CloseReason { get; private set; } = FormCloseReason.None;
        public string ErrorMessage { get; private set; }
        public string LastControlId { get; set; }
        public string LastEventName { get; set; }
        /// <summary>Set when a DataGrid CellClick / SelectionChanged is raised.</summary>
        public int LastRowIndex { get; set; } = -1;
        /// <summary>Set when a DataGrid CellClick is raised (column name or index string).</summary>
        public string LastColumnName { get; set; }
        public bool IsClosed => _closed;
        public bool IsHandlerRunning => _handlerRunning;
        public bool IsRegistering { get; set; }

        /// <summary>
        /// When true, user close (X) is cancelled and a Closing event is queued for AsyncForm Close Scope.
        /// </summary>
        public bool InterceptUserClose { get; set; }

        public void Open(
            string formPath,
            int timeoutMs,
            WorkflowInstanceExtension workflow,
            string bookmarkName = null)
        {
            if (_uiThread != null)
            {
                throw new InvalidOperationException("FormSession is already open.");
            }

            _workflow = workflow ?? throw new ArgumentNullException(nameof(workflow));
            if (!string.IsNullOrWhiteSpace(bookmarkName))
            {
                _bookmarkName = bookmarkName;
            }

            FormDefinition definition = FormJsonLoader.LoadFromFile(formPath);
            _uiReady = new ManualResetEventSlim(false);
            _uiClosed = new ManualResetEventSlim(false);

            _uiThread = new Thread(() => UiThreadMain(definition, timeoutMs));
            _uiThread.SetApartmentState(ApartmentState.STA);
            _uiThread.IsBackground = true;
            _uiThread.Name = "F2B.Forms.UI";
            _uiThread.Start();

            _uiReady.Wait();
            if (_uiException != null)
            {
                throw new InvalidOperationException("Failed to open form.", _uiException);
            }

            FormSessionAmbient.Push(this);
            _ambientPushed = true;
        }

        public void RegisterBinding(
            string controlId,
            string eventName,
            UiBehavior behavior,
            object activityKey,
            bool replaceExisting = false)
        {
            if (string.IsNullOrWhiteSpace(controlId) || string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("ControlId and EventName are required.");
            }

            string id = controlId.Trim();
            string name = eventName.Trim();
            string key = MakeKey(id, name);
            lock (_sync)
            {
                if (replaceExisting || !_bindings.TryGetValue(key, out List<BoundEvent> list))
                {
                    list = new List<BoundEvent>();
                    _bindings[key] = list;
                }

                list.Add(new BoundEvent
                {
                    ControlId = id,
                    EventName = name,
                    Behavior = behavior,
                    ActivityKey = activityKey
                });
            }

            InvokeOnUi(() => EnsureNativeEventWired(id, name));
        }

        /// <summary>Remove workflow binding and native handler for one control event.</summary>
        public void UnregisterBinding(string controlId, string eventName)
        {
            if (string.IsNullOrWhiteSpace(controlId) || string.IsNullOrWhiteSpace(eventName))
            {
                throw new ArgumentException("ControlId and EventName are required.");
            }

            string key = MakeKey(controlId, eventName);
            lock (_sync)
            {
                _bindings.Remove(key);
            }

            InvokeOnUi(() => UnwireNativeEvent(controlId.Trim(), eventName.Trim()));
        }

        /// <summary>
        /// Remove all bindings for a control. When <paramref name="eventName"/> is null/empty, clears every event on that control.
        /// </summary>
        public void UnregisterBindings(string controlId, string eventName = null)
        {
            if (string.IsNullOrWhiteSpace(controlId))
            {
                throw new ArgumentException("ControlId is required.", nameof(controlId));
            }

            if (!string.IsNullOrWhiteSpace(eventName))
            {
                UnregisterBinding(controlId, eventName);
                return;
            }

            string id = controlId.Trim();
            var keys = new List<string>();
            lock (_sync)
            {
                foreach (string key in _bindings.Keys)
                {
                    if (KeyBelongsToControl(key, id))
                    {
                        keys.Add(key);
                    }
                }

                foreach (string key in keys)
                {
                    _bindings.Remove(key);
                }
            }

            InvokeOnUi(() =>
            {
                foreach (string key in keys)
                {
                    if (TrySplitKey(key, out string cid, out string ename))
                    {
                        UnwireNativeEvent(cid, ename);
                    }
                }
            });
        }

        public bool TryGetBindings(string controlId, string eventName, out List<BoundEvent> bindings)
        {
            string key = MakeKey(controlId, eventName);
            lock (_sync)
            {
                if (_bindings.TryGetValue(key, out List<BoundEvent> list))
                {
                    bindings = list.ToList();
                    return true;
                }
            }

            bindings = null;
            return false;
        }

        public bool TryDequeueEvent(out FormEvent formEvent)
        {
            return _pendingEvents.TryDequeue(out formEvent);
        }

        public void BeginHandler(UiBehavior behavior)
        {
            _handlerRunning = true;
            _activeBehavior = behavior;
            if (behavior == UiBehavior.LockQueue || behavior == UiBehavior.LockIgnore)
            {
                SetUiEnabled(false);
                _uiLocked = true;
            }
        }

        public void EndHandler()
        {
            _handlerRunning = false;
            if (_uiLocked)
            {
                SetUiEnabled(true);
                _uiLocked = false;
            }

            // Drain queued events into workflow if any arrived during handler.
            PumpPendingToWorkflow();
        }

        public void Close(FormCloseReason reason, string errorMessage = null)
        {
            if (_closed)
            {
                return;
            }

            CloseReason = reason;
            if (!string.IsNullOrEmpty(errorMessage))
            {
                ErrorMessage = errorMessage;
            }

            _acceptEvents = false;
            _closed = true;

            InvokeOnUi(() =>
            {
                try
                {
                    if (_timeoutTimer != null)
                    {
                        _timeoutTimer.Stop();
                        _timeoutTimer.Dispose();
                        _timeoutTimer = null;
                    }

                    if (_form != null && !_form.IsDisposed)
                    {
                        _form.Close();
                    }
                }
                catch
                {
                    // ignored
                }
            });
        }

        public void AbortFromWorkflow(string errorMessage = null)
        {
            Close(FormCloseReason.WorkflowAbort, errorMessage);
            WaitUiClosed(5000);
        }

        public void WaitUiClosed(int timeoutMs = Timeout.Infinite)
        {
            if (_uiClosed != null)
            {
                _uiClosed.Wait(timeoutMs);
            }
        }

        public Control GetControl(string controlId)
        {
            EnsureOpen();
            if (string.IsNullOrWhiteSpace(controlId))
            {
                throw new ArgumentException("ControlId is required.", nameof(controlId));
            }

            if (!_controls.TryGetValue(controlId.Trim(), out Control control) || control == null || control.IsDisposed)
            {
                throw new InvalidOperationException("Control not found: '" + controlId + "'.");
            }

            return control;
        }

        /// <summary>
        /// Collects child control Ids under a container, grouped by Forms type name.
        /// Container may be a Panel / GroupBox / TabPage / ScrollContainer / TableLayout / TabControl, or the form ("form").
        /// </summary>
        /// <param name="containerId">Parent control Id (use "form" for the root form).</param>
        /// <param name="deepDive">True = recurse into nested containers; False = direct children only.</param>
        /// <param name="typeFilterFlags">Property-pane Flags filter; None/All = include all types.</param>
        /// <param name="typeFilterNames">Optional runtime string[] filter; empty = no extra filter.</param>
        public Dictionary<string, string[]> GetChildControlsByType(
            string containerId,
            bool deepDive,
            FormControlTypeFilter typeFilterFlags,
            string[] typeFilterNames)
        {
            var result = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);

            InvokeOnUi(() =>
            {
                Control container = GetControl(containerId);
                CollectChildControls(container, deepDive, typeFilterFlags, typeFilterNames, result);
            });

            var output = new Dictionary<string, string[]>(StringComparer.OrdinalIgnoreCase);
            foreach (KeyValuePair<string, List<string>> pair in result)
            {
                output[pair.Key] = pair.Value.ToArray();
            }

            return output;
        }

        private void CollectChildControls(
            Control parent,
            bool deepDive,
            FormControlTypeFilter typeFilterFlags,
            string[] typeFilterNames,
            Dictionary<string, List<string>> sink)
        {
            if (parent == null || sink == null)
            {
                return;
            }

            foreach (Control child in EnumerateDirectChildren(parent))
            {
                if (child == null || child.IsDisposed)
                {
                    continue;
                }

                string id = child.Name;
                bool registered = !string.IsNullOrWhiteSpace(id)
                    && _controls != null
                    && _controls.ContainsKey(id);

                if (registered)
                {
                    string typeName = FormControlTypeResolver.Resolve(child);
                    if (!string.IsNullOrWhiteSpace(typeName)
                        && !string.Equals(typeName, FormControlType.Form, StringComparison.OrdinalIgnoreCase)
                        && FormControlTypeFilterUtil.PassesFilter(typeFilterFlags, typeName)
                        && FormControlTypeFilterUtil.PassesStringFilter(typeFilterNames, typeName))
                    {
                        if (!sink.TryGetValue(typeName, out List<string> list))
                        {
                            list = new List<string>();
                            sink[typeName] = list;
                        }

                        if (!list.Exists(existing => string.Equals(existing, id, StringComparison.OrdinalIgnoreCase)))
                        {
                            list.Add(id);
                        }
                    }
                }

                if (deepDive)
                {
                    CollectChildControls(child, deepDive: true, typeFilterFlags, typeFilterNames, sink);
                }
            }
        }

        private static IEnumerable<Control> EnumerateDirectChildren(Control parent)
        {
            if (parent is TabControl tabControl)
            {
                foreach (TabPage page in tabControl.TabPages)
                {
                    yield return page;
                }

                yield break;
            }

            if (parent == null)
            {
                yield break;
            }

            foreach (Control child in parent.Controls)
            {
                yield return child;
            }
        }

        public object GetControlValue(string controlId)
        {
            object result = null;
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                if (control is CheckBox checkBox)
                {
                    result = checkBox.Checked;
                }
                else if (control is RadioButton radioButton)
                {
                    result = radioButton.Checked;
                }
                else if (control is NumericUpDown numeric)
                {
                    result = numeric.Value;
                }
                else if (control is CheckedListBox checkedList)
                {
                    var checkedItems = new List<string>();
                    foreach (object item in checkedList.CheckedItems)
                    {
                        checkedItems.Add(Convert.ToString(item) ?? string.Empty);
                    }

                    result = checkedItems;
                }
                else if (control is ListBox listBox)
                {
                    result = listBox.SelectedItem == null ? string.Empty : Convert.ToString(listBox.SelectedItem);
                }
                else if (control is ComboBox comboBox)
                {
                    result = comboBox.SelectedItem == null ? string.Empty : Convert.ToString(comboBox.SelectedItem);
                }
                else if (control is DateTimePicker dateTimePicker)
                {
                    result = FormRenderer.ReadDateTimePickerValue(dateTimePicker);
                }
                else if (control is PictureBox pictureBox)
                {
                    result = FormRenderer.ReadPicturePath(pictureBox);
                }
                else if (control is Form)
                {
                    result = control.Text;
                }
                else
                {
                    result = control.Text;
                }
            });
            return result;
        }

        public void SetControlValue(string controlId, object value)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                FormRenderer.ApplyValue(control, value);
                FlushControlPaint(control);
            });
        }

        public void SetControlText(string controlId, string text)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                if (control is DateTimePicker dateTimePicker)
                {
                    FormRenderer.ApplyDateTimePickerValue(dateTimePicker, text);
                }
                else
                {
                    control.Text = text ?? string.Empty;
                }

                FlushControlPaint(control);
            });
        }

        /// <summary>
        /// Append text to a control. <paramref name="separator"/> is inserted between existing content and the new text
        /// when the control is non-empty. Empty separator = concatenate only; otherwise used as-is.
        /// Caller resolves unset → newline.
        /// </summary>
        public void AppendControlText(string controlId, string text, string separator, bool scrollToEnd)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                string append = text ?? string.Empty;
                string sep = separator ?? string.Empty;

                if (control is TextBoxBase textBox)
                {
                    if (textBox.TextLength > 0 && sep.Length > 0)
                    {
                        textBox.AppendText(sep);
                    }

                    textBox.AppendText(append);

                    if (scrollToEnd)
                    {
                        textBox.SelectionStart = textBox.TextLength;
                        textBox.SelectionLength = 0;
                        textBox.ScrollToCaret();
                    }
                }
                else
                {
                    string existing = control.Text ?? string.Empty;
                    if (existing.Length > 0 && sep.Length > 0)
                    {
                        control.Text = existing + sep + append;
                    }
                    else
                    {
                        control.Text = existing + append;
                    }
                }

                FlushControlPaint(control);
            });
        }

        /// <summary>
        /// Activate a TabPage by its control Id (selects the owning TabControl's selected tab).
        /// </summary>
        public void ActivateTab(string tabPageId)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(tabPageId);
                var page = control as TabPage;
                if (page == null)
                {
                    throw new InvalidOperationException(
                        "Control '" + tabPageId + "' is not a TabPage.");
                }

                var tabs = page.Parent as TabControl;
                if (tabs == null)
                {
                    throw new InvalidOperationException(
                        "TabPage '" + tabPageId + "' is not hosted under a TabControl.");
                }

                if (!page.Enabled)
                {
                    throw new InvalidOperationException(
                        "TabPage '" + tabPageId + "' is disabled and cannot be selected.");
                }

                tabs.SelectedTab = page;
                FlushControlPaint(tabs);
            });
        }

        /// <summary>
        /// Force the form to repaint now. Optionally pump the UI message queue
        /// so pending paints (and other UI messages) are processed even while a
        /// BindEvent handler is still running on the UI synchronization context.
        /// </summary>
        public void RefreshUi(bool pumpMessages = true)
        {
            InvokeOnUi(() =>
            {
                if (_form == null || _form.IsDisposed)
                {
                    return;
                }

                _form.Refresh();
                if (pumpMessages)
                {
                    Application.DoEvents();
                }
            });
        }

        /// <summary>
        /// Soft bring-to-front: briefly uses TopMost then clears it so the form is not sticky.
        /// Other windows can cover it again when activated.
        /// </summary>
        public void BringFormToFrontSoft()
        {
            InvokeOnUi(() =>
            {
                EnsureOpen();
                if (_form == null || _form.IsDisposed)
                {
                    throw new InvalidOperationException("Form is not available.");
                }

                FormNativeWindowActivator.SoftBringToFront(_form);
            });
        }

        /// <param name="fontFamily">Null/empty = keep.</param>
        /// <param name="fontSize">Null = keep.</param>
        /// <param name="bold">Null = keep; false = clear bold; true = bold.</param>
        /// <param name="italic">Null = keep; false = clear italic; true = italic.</param>
        /// <param name="underline">Null = keep; false = clear underline; true = underline.</param>
        /// <param name="foreColor">Null/empty = keep.</param>
        public void SetControlFont(
            string controlId,
            string fontFamily,
            float? fontSize,
            bool? bold,
            bool? italic,
            bool? underline,
            string foreColor)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                Font current = control.Font ?? SystemFonts.DefaultFont;

                bool familyChanged = !string.IsNullOrWhiteSpace(fontFamily);
                bool sizeChanged = fontSize.HasValue && fontSize.Value > 0;
                bool styleChanged = bold.HasValue || italic.HasValue || underline.HasValue;

                if (familyChanged || sizeChanged || styleChanged)
                {
                    string family = familyChanged
                        ? fontFamily.Trim()
                        : current.FontFamily.Name;
                    float size = sizeChanged
                        ? fontSize.Value
                        : (current.Size > 0 ? current.Size : FontStyleUtil.DefaultSize);
                    bool useBold = bold ?? current.Bold;
                    bool useItalic = italic ?? current.Italic;
                    bool useUnderline = underline ?? current.Underline;

                    Font created = FontStyleUtil.CreateFont(family, size, useBold, useItalic, useUnderline);
                    Font previous = control.Font;
                    control.Font = created;
                    if (previous != null
                        && !ReferenceEquals(previous, created)
                        && previous != SystemFonts.DefaultFont)
                    {
                        try
                        {
                            previous.Dispose();
                        }
                        catch
                        {
                            // ignored
                        }
                    }
                }

                Color? fore = FontStyleUtil.ParseColor(foreColor);
                if (fore.HasValue)
                {
                    ApplyForeColor(control, fore.Value);
                }

                FlushControlPaint(control);
            });
        }

        public void UpdateOptionsList(string controlId, IEnumerable<string> options, bool clearExisting = true)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                if (control is ComboBox combo)
                {
                    string previous = combo.SelectedItem == null ? null : Convert.ToString(combo.SelectedItem);
                    if (clearExisting)
                    {
                        combo.Items.Clear();
                    }

                    if (options != null)
                    {
                        foreach (string option in options)
                        {
                            combo.Items.Add(option ?? string.Empty);
                        }
                    }

                    if (previous != null)
                    {
                        int index = combo.FindStringExact(previous);
                        combo.SelectedIndex = index;
                    }
                    else
                    {
                        combo.SelectedIndex = -1;
                    }

                    FlushControlPaint(combo);
                    return;
                }

                if (control is CheckedListBox checkedList)
                {
                    string previous = checkedList.SelectedItem == null ? null : Convert.ToString(checkedList.SelectedItem);
                    if (clearExisting)
                    {
                        checkedList.Items.Clear();
                    }

                    if (options != null)
                    {
                        foreach (string option in options)
                        {
                            checkedList.Items.Add(option ?? string.Empty);
                        }
                    }

                    if (previous != null)
                    {
                        checkedList.SelectedIndex = checkedList.FindStringExact(previous);
                    }
                    else
                    {
                        checkedList.SelectedIndex = -1;
                    }

                    FlushControlPaint(checkedList);
                    return;
                }

                if (control is ListBox listBox)
                {
                    string previous = listBox.SelectedItem == null ? null : Convert.ToString(listBox.SelectedItem);
                    if (clearExisting)
                    {
                        listBox.Items.Clear();
                    }

                    if (options != null)
                    {
                        foreach (string option in options)
                        {
                            listBox.Items.Add(option ?? string.Empty);
                        }
                    }

                    if (previous != null)
                    {
                        listBox.SelectedIndex = listBox.FindStringExact(previous);
                    }
                    else
                    {
                        listBox.SelectedIndex = -1;
                    }

                    FlushControlPaint(listBox);
                    return;
                }

                throw new InvalidOperationException(
                    "UpdateOptionsList requires ComboBox / ListBox / CheckedListBox. Control '"
                    + controlId + "' is '" + control.GetType().Name + "'.");
            });
        }

        public void SetControlEnabled(string controlId, bool enabled)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                control.Enabled = enabled;
                FlushControlPaint(control);
            });
        }

        public void SetControlReadOnly(string controlId, bool readOnly)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                ApplyReadOnly(control, controlId, readOnly);
                FlushControlPaint(control);
            });
        }

        public void SetControlVisible(string controlId, bool visible)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                control.Visible = visible;
                FlushControlPaint(control);
            });
        }

        /// <summary>
        /// Create a control under a container (form / Panel / GroupBox / TabPage).
        /// TabPage may only be created under a TabControl. Coordinates are relative to the parent.
        /// </summary>
        public void CreateControl(
            string parentControlId,
            string type,
            string controlId,
            string text,
            int x,
            int y,
            int width,
            int height)
        {
            InvokeOnUi(() =>
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    throw new ArgumentException("Type is required.", nameof(type));
                }

                if (string.IsNullOrWhiteSpace(controlId))
                {
                    throw new ArgumentException("ControlId is required.", nameof(controlId));
                }

                if (_controls == null)
                {
                    throw new InvalidOperationException("Form controls are not available.");
                }

                string id = controlId.Trim();
                string controlType = type.Trim();
                if (!FormControlType.IsKnown(controlType))
                {
                    throw new InvalidOperationException("Unsupported control type: " + controlType);
                }

                if (_controls.ContainsKey(id))
                {
                    throw new InvalidOperationException("Duplicate control id: '" + id + "'.");
                }

                Control parent = GetControl(parentControlId);
                bool parentIsTabControl = parent is TabControl;
                bool creatingTabPage = FormControlType.IsTabPage(controlType);

                if (creatingTabPage && !parentIsTabControl)
                {
                    throw new InvalidOperationException(
                        "TabPage '" + id + "' must be created under a TabControl.");
                }

                if (!creatingTabPage && parentIsTabControl)
                {
                    throw new InvalidOperationException(
                        "Only TabPage can be created directly under a TabControl. Parent: '"
                        + parentControlId + "'.");
                }

                if (parent is TableLayoutPanel)
                {
                    throw new InvalidOperationException(
                        "Use Create Control In Cell for TableLayout parent '" + parentControlId + "'.");
                }

                if (!creatingTabPage
                    && !(parent is Form
                         || parent is Panel
                         || parent is GroupBox
                         || parent is TabPage))
                {
                    throw new InvalidOperationException(
                        "Parent '" + parentControlId
                        + "' is not a container (Form/Panel/ScrollContainer/GroupBox/TabPage).");
                }

                int w = width > 0 ? width : 120;
                int h = height > 0 ? height : 30;
                if (FormControlType.IsTableLayout(controlType))
                {
                    w = width > 0 ? width : 400;
                    h = height > 0 ? height : 200;
                }
                else if (FormControlType.IsDataGrid(controlType) || FormControlType.IsScrollContainer(controlType))
                {
                    w = width > 0 ? width : 300;
                    h = height > 0 ? height : 200;
                }

                var definition = new ControlDefinition
                {
                    Id = id,
                    Type = controlType,
                    Text = text ?? string.Empty,
                    X = x,
                    Y = y,
                    Width = w,
                    Height = h,
                    RowCount = FormControlType.IsTableLayout(controlType) ? 3 : (int?)null,
                    ColumnCount = FormControlType.IsTableLayout(controlType) ? 3 : (int?)null
                };

                Control created = FormRenderer.CreateControlInstance(definition, _controls);
                if (creatingTabPage)
                {
                    ((TabControl)parent).TabPages.Add((TabPage)created);
                }
                else
                {
                    parent.Controls.Add(created);
                }

                FlushControlPaint(created);
                FlushControlPaint(parent);
            });
        }

        public void SetTableLayoutSize(string tableId, int rowCount, int columnCount)
        {
            InvokeOnUi(() =>
            {
                TableLayoutPanel table = GetTableLayout(tableId);
                if (rowCount < 1 || columnCount < 1)
                {
                    throw new InvalidOperationException("RowCount and ColumnCount must be at least 1.");
                }

                var toRemove = new List<Control>();
                foreach (Control child in table.Controls)
                {
                    TableLayoutPanelCellPosition pos = table.GetPositionFromControl(child);
                    if (pos.Row >= rowCount || pos.Column >= columnCount)
                    {
                        toRemove.Add(child);
                    }
                }

                foreach (Control child in toRemove)
                {
                    RemoveControlInstance(child);
                }

                FormRenderer.ApplyTableLayoutStyles(table, rowCount, columnCount);
                FlushControlPaint(table);
            });
        }

        public void CreateControlInCell(
            string tableId,
            int row,
            int column,
            string type,
            string controlId,
            string text,
            int width = 0,
            int height = 0)
        {
            InvokeOnUi(() =>
            {
                if (string.IsNullOrWhiteSpace(type))
                {
                    throw new ArgumentException("Type is required.", nameof(type));
                }

                if (string.IsNullOrWhiteSpace(controlId))
                {
                    throw new ArgumentException("ControlId is required.", nameof(controlId));
                }

                TableLayoutPanel table = GetTableLayout(tableId);
                if (FormRenderer.GetTableCellControl(table, row, column) != null)
                {
                    throw new InvalidOperationException(
                        "Cell (" + row + "," + column + ") in '" + tableId
                        + "' is already occupied. Clear the cell first.");
                }

                string id = controlId.Trim();
                if (_controls.ContainsKey(id))
                {
                    throw new InvalidOperationException("Duplicate control id: '" + id + "'.");
                }

                string controlType = type.Trim();
                if (!FormControlType.IsKnown(controlType)
                    || FormControlType.IsTabPage(controlType)
                    || FormControlType.IsTabControl(controlType)
                    || FormControlType.IsTableLayout(controlType)
                    || FormControlType.IsDataGrid(controlType))
                {
                    throw new InvalidOperationException(
                        "Type '" + controlType + "' cannot be placed in a TableLayout cell.");
                }

                int w = width > 0 ? width : 80;
                int h = height > 0 ? height : 24;
                var definition = new ControlDefinition
                {
                    Id = id,
                    Type = controlType,
                    Text = text ?? string.Empty,
                    Width = w,
                    Height = h,
                    Row = row,
                    Column = column
                };

                Control created = FormRenderer.CreateControlInstance(definition, _controls);
                FormRenderer.PlaceInTableCell(table, created, row, column);
                FlushControlPaint(created);
                FlushControlPaint(table);
            });
        }

        public void ClearTableCell(string tableId, int row, int column)
        {
            InvokeOnUi(() =>
            {
                TableLayoutPanel table = GetTableLayout(tableId);
                Control existing = FormRenderer.GetTableCellControl(table, row, column);
                if (existing == null)
                {
                    return;
                }

                RemoveControlInstance(existing);
                FlushControlPaint(table);
            });
        }

        public void ClearTable(string tableId)
        {
            InvokeOnUi(() =>
            {
                TableLayoutPanel table = GetTableLayout(tableId);
                var children = table.Controls.Cast<Control>().ToList();
                foreach (Control child in children)
                {
                    RemoveControlInstance(child);
                }

                FlushControlPaint(table);
            });
        }

        /// <summary>
        /// Fill a TableLayout from a DataTable. Optionally uses row 0 for headers.
        /// Editable column names (comma-separated) become TextBox; others Label.
        /// Resizes the table to fit.
        /// </summary>
        public void FillTableFromDataTable(
            string tableId,
            DataTable dataTable,
            bool headerRow,
            string editableColumnNames)
        {
            InvokeOnUi(() =>
            {
                if (dataTable == null)
                {
                    throw new ArgumentNullException(nameof(dataTable));
                }

                TableLayoutPanel table = GetTableLayout(tableId);
                ClearTableLocal(table);

                var editable = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                if (!string.IsNullOrWhiteSpace(editableColumnNames))
                {
                    foreach (string part in editableColumnNames.Split(
                        new[] { ',', ';' },
                        StringSplitOptions.RemoveEmptyEntries))
                    {
                        editable.Add(part.Trim());
                    }
                }

                int dataRows = dataTable.Rows.Count;
                int cols = dataTable.Columns.Count;
                if (cols < 1)
                {
                    FormRenderer.ApplyTableLayoutStyles(table, 1, 1);
                    FlushControlPaint(table);
                    return;
                }

                int rows = dataRows + (headerRow ? 1 : 0);
                if (rows < 1)
                {
                    rows = headerRow ? 1 : 1;
                }

                FormRenderer.ApplyTableLayoutStyles(table, Math.Max(1, rows), cols);

                int rowOffset = 0;
                if (headerRow)
                {
                    for (int c = 0; c < cols; c++)
                    {
                        string colName = dataTable.Columns[c].ColumnName;
                        string id = MakeCellControlId(tableId, 0, c, "hdr");
                        CreateInCellLocal(
                            table,
                            0,
                            c,
                            FormControlType.Label,
                            id,
                            colName);
                    }

                    rowOffset = 1;
                }

                for (int r = 0; r < dataRows; r++)
                {
                    DataRow dataRow = dataTable.Rows[r];
                    for (int c = 0; c < cols; c++)
                    {
                        string colName = dataTable.Columns[c].ColumnName;
                        string cellText = dataRow[c] == DBNull.Value || dataRow[c] == null
                            ? string.Empty
                            : Convert.ToString(dataRow[c]);
                        bool useTextBox = editable.Contains(colName);
                        string type = useTextBox ? FormControlType.TextBox : FormControlType.Label;
                        string prefix = useTextBox ? "tb" : "lbl";
                        string id = MakeCellControlId(tableId, rowOffset + r, c, prefix);
                        CreateInCellLocal(table, rowOffset + r, c, type, id, cellText);
                    }
                }

                FlushControlPaint(table);
            });
        }

        public void BindDataTable(string controlId, DataTable dataTable)
        {
            InvokeOnUi(() =>
            {
                DataGridView grid = GetDataGrid(controlId);
                if (dataTable == null)
                {
                    throw new ArgumentNullException(nameof(dataTable));
                }

                grid.DataSource = dataTable;
                FlushControlPaint(grid);
            });
        }

        public object GetDataGridCellValue(string controlId, int rowIndex, string column)
        {
            object result = null;
            InvokeOnUi(() =>
            {
                DataGridView grid = GetDataGrid(controlId);
                if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
                {
                    throw new InvalidOperationException(
                        "Row index " + rowIndex + " is out of range for '" + controlId + "'.");
                }

                DataGridViewColumn col = ResolveDataGridColumn(grid, column);
                object value = grid.Rows[rowIndex].Cells[col.Index].Value;
                result = value == null || value == DBNull.Value ? string.Empty : value;
            });
            return result;
        }

        public int GetDataGridSelectedRowIndex(string controlId)
        {
            int result = -1;
            InvokeOnUi(() =>
            {
                DataGridView grid = GetDataGrid(controlId);
                if (grid.CurrentRow != null && !grid.CurrentRow.IsNewRow)
                {
                    result = grid.CurrentRow.Index;
                }
                else if (grid.SelectedRows.Count > 0)
                {
                    result = grid.SelectedRows[0].Index;
                }
            });
            return result;
        }

        private TableLayoutPanel GetTableLayout(string tableId)
        {
            Control control = GetControl(tableId);
            var table = control as TableLayoutPanel;
            if (table == null)
            {
                throw new InvalidOperationException(
                    "Control '" + tableId + "' is not a TableLayout.");
            }

            return table;
        }

        private DataGridView GetDataGrid(string controlId)
        {
            Control control = GetControl(controlId);
            var grid = control as DataGridView;
            if (grid == null)
            {
                throw new InvalidOperationException(
                    "Control '" + controlId + "' is not a DataGrid.");
            }

            return grid;
        }

        private static DataGridViewColumn ResolveDataGridColumn(DataGridView grid, string column)
        {
            if (string.IsNullOrWhiteSpace(column))
            {
                throw new ArgumentException("Column is required.", nameof(column));
            }

            string text = column.Trim();
            if (int.TryParse(text, out int index))
            {
                if (index < 0 || index >= grid.Columns.Count)
                {
                    throw new InvalidOperationException("Column index out of range: " + index);
                }

                return grid.Columns[index];
            }

            if (grid.Columns.Contains(text))
            {
                return grid.Columns[text];
            }

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (string.Equals(col.HeaderText, text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(col.Name, text, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(col.DataPropertyName, text, StringComparison.OrdinalIgnoreCase))
                {
                    return col;
                }
            }

            throw new InvalidOperationException("Column not found: '" + column + "'.");
        }

        private void ClearTableLocal(TableLayoutPanel table)
        {
            var children = table.Controls.Cast<Control>().ToList();
            foreach (Control child in children)
            {
                RemoveControlInstance(child);
            }
        }

        private void CreateInCellLocal(
            TableLayoutPanel table,
            int row,
            int column,
            string type,
            string id,
            string text)
        {
            if (_controls.ContainsKey(id))
            {
                int n = 2;
                string baseId = id;
                while (_controls.ContainsKey(id))
                {
                    id = baseId + "_" + n;
                    n++;
                }
            }

            var definition = new ControlDefinition
            {
                Id = id,
                Type = type,
                Text = text ?? string.Empty,
                Width = 80,
                Height = 24,
                Row = row,
                Column = column
            };
            Control created = FormRenderer.CreateControlInstance(definition, _controls);
            FormRenderer.PlaceInTableCell(table, created, row, column);
        }

        private static string MakeCellControlId(string tableId, int row, int column, string prefix)
        {
            string safeTable = string.IsNullOrWhiteSpace(tableId) ? "table" : tableId.Trim();
            return prefix + "_" + safeTable + "_" + row + "_" + column;
        }

        private void RemoveControlInstance(Control control)
        {
            if (control == null)
            {
                return;
            }

            Control parent = control.Parent;
            UnregisterControlTree(control);
            if (parent is TableLayoutPanel table)
            {
                table.Controls.Remove(control);
            }
            else if (control is TabPage tabPage && parent is TabControl tabControl)
            {
                tabControl.TabPages.Remove(tabPage);
            }
            else if (parent != null)
            {
                parent.Controls.Remove(control);
            }

            try
            {
                control.Dispose();
            }
            catch
            {
                // ignored
            }
        }

        /// <summary>
        /// Remove a control (and nested children) by id. The form itself cannot be deleted.
        /// </summary>
        public void DeleteControl(string controlId)
        {
            InvokeOnUi(() =>
            {
                if (_controls == null)
                {
                    throw new InvalidOperationException("Form controls are not available.");
                }

                Control control = GetControl(controlId);
                if (control is Form)
                {
                    throw new InvalidOperationException(
                        "Cannot delete the form. Use Close Form to close the window.");
                }

                string id = control.Name;
                Control parent = control.Parent;
                UnregisterControlTree(control);

                if (control is TabPage tabPage && parent is TabControl tabControl)
                {
                    tabControl.TabPages.Remove(tabPage);
                }
                else if (parent != null)
                {
                    parent.Controls.Remove(control);
                }

                try
                {
                    control.Dispose();
                }
                catch
                {
                    // ignored
                }

                if (parent != null && !parent.IsDisposed)
                {
                    FlushControlPaint(parent);
                }
                else if (_form != null && !_form.IsDisposed)
                {
                    FlushControlPaint(_form);
                }
            });
        }

        /// <summary>
        /// Set Location relative to the parent container. Null keeps that coordinate.
        /// </summary>
        public void SetControlPosition(string controlId, int? x, int? y)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                if (control is Form)
                {
                    throw new InvalidOperationException(
                        "SetControlPosition applies to child controls, not the form. Use screen Location only via the host window if needed.");
                }

                if (control is TabPage)
                {
                    throw new InvalidOperationException(
                        "TabPage position is managed by its TabControl and cannot be set with X/Y.");
                }

                if (!x.HasValue && !y.HasValue)
                {
                    throw new InvalidOperationException(
                        "SetControlPosition requires X and/or Y.");
                }

                int newX = x ?? control.Left;
                int newY = y ?? control.Top;
                control.Location = new Point(newX, newY);
                FlushControlPaint(control);
                if (control.Parent != null)
                {
                    FlushControlPaint(control.Parent);
                }
            });
        }

        /// <summary>
        /// Resize a control or the form. Pass null / &lt;= 0 for width or height to keep that dimension.
        /// For Form, sizes match JSON/designer units (client area). Fixed-size forms stay locked at the new size.
        /// </summary>
        public void ResizeControl(string controlId, int? width, int? height)
        {
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                bool changeWidth = width.HasValue && width.Value > 0;
                bool changeHeight = height.HasValue && height.Value > 0;
                if (!changeWidth && !changeHeight)
                {
                    throw new InvalidOperationException(
                        "ResizeControl requires a positive Width and/or Height.");
                }

                if (control is Form form)
                {
                    int w = changeWidth ? width.Value : form.ClientSize.Width;
                    int h = changeHeight ? height.Value : form.ClientSize.Height;
                    bool sizeLocked = form.MinimumSize.Width > 0
                        && form.MinimumSize == form.MaximumSize;

                    if (sizeLocked)
                    {
                        form.MinimumSize = Size.Empty;
                        form.MaximumSize = Size.Empty;
                    }

                    form.ClientSize = new Size(w, h);

                    if (sizeLocked)
                    {
                        form.MinimumSize = form.Size;
                        form.MaximumSize = form.Size;
                    }
                }
                else
                {
                    int w = changeWidth ? width.Value : control.Width;
                    int h = changeHeight ? height.Value : control.Height;
                    control.Size = new Size(w, h);
                }

                FlushControlPaint(control);
            });
        }

        public void UpdateControlProperty(string controlId, string propertyName, object value)
        {
            if (string.IsNullOrWhiteSpace(propertyName))
            {
                throw new ArgumentException("PropertyName is required.", nameof(propertyName));
            }

            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                string name = propertyName.Trim();

                // Semantic aliases / non-CLR properties first, then any public settable property via reflection.
                if (TryApplySemanticControlProperty(control, controlId, name, value))
                {
                    FlushControlPaint(control);
                    return;
                }

                if (TrySetControlPropertyByReflection(control, name, value))
                {
                    FlushControlPaint(control);
                    return;
                }

                throw new InvalidOperationException(
                    "Property '" + propertyName + "' was not found or is not writable on '"
                    + control.GetType().Name + "' (control '" + controlId + "').");
            });
        }

        /// <summary>
        /// Form-level semantics that are not 1:1 with a single CLR setter (Value routing, fake ReadOnly, color aliases, Items).
        /// </summary>
        private static bool TryApplySemanticControlProperty(
            Control control,
            string controlId,
            string name,
            object value)
        {
            if (string.Equals(name, "Value", StringComparison.OrdinalIgnoreCase))
            {
                if (control is DateTimePicker dateTimePicker)
                {
                    FormRenderer.ApplyDateTimePickerValue(dateTimePicker, value);
                    return true;
                }

                if (control is PictureBox pictureBox)
                {
                    FormRenderer.ApplyPicturePath(pictureBox, value == null ? null : Convert.ToString(value));
                    return true;
                }

                FormRenderer.ApplyValue(control, value);
                return true;
            }

            if (control is PictureBox picture
                && (string.Equals(name, "ImagePath", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(name, "Text", StringComparison.OrdinalIgnoreCase)))
            {
                FormRenderer.ApplyPicturePath(picture, value == null ? null : Convert.ToString(value));
                return true;
            }

            if (string.Equals(name, "ReadOnly", StringComparison.OrdinalIgnoreCase))
            {
                // ComboBox has no real ReadOnly; TextBoxBase etc. do — ApplyReadOnly covers both.
                bool readOnly = value is bool b ? b : Convert.ToBoolean(value);
                ApplyReadOnly(control, controlId, readOnly);
                return true;
            }

            if (string.Equals(name, "BackColor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "BackgroundColor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Background", StringComparison.OrdinalIgnoreCase))
            {
                ApplyControlColor(control, isBackColor: true, value);
                return true;
            }

            if (string.Equals(name, "ForeColor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "ForegroundColor", StringComparison.OrdinalIgnoreCase)
                || string.Equals(name, "Color", StringComparison.OrdinalIgnoreCase))
            {
                ApplyControlColor(control, isBackColor: false, value);
                return true;
            }

            if (string.Equals(name, "Items", StringComparison.OrdinalIgnoreCase)
                && (control is ComboBox || control is ListBox || control is CheckedListBox))
            {
                // Items is get-only ObjectCollection; replace contents via ApplyValue.
                FormRenderer.ApplyValue(control, value);
                return true;
            }

            return false;
        }

        private static bool TrySetControlPropertyByReflection(Control control, string propertyName, object value)
        {
            PropertyInfo property = FindWritableProperty(control.GetType(), propertyName);
            if (property == null)
            {
                return false;
            }

            object converted = ConvertToPropertyType(value, property.PropertyType, property.Name);
            property.SetValue(control, converted, null);
            return true;
        }

        private static PropertyInfo FindWritableProperty(Type type, string propertyName)
        {
            if (type == null || string.IsNullOrWhiteSpace(propertyName))
            {
                return null;
            }

            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase;
            PropertyInfo property = type.GetProperty(propertyName.Trim(), flags);
            if (property == null || !property.CanWrite || property.GetIndexParameters().Length > 0)
            {
                return null;
            }

            return property;
        }

        private static object ConvertToPropertyType(object value, Type targetType, string propertyName)
        {
            if (targetType == null)
            {
                return value;
            }

            Type underlying = Nullable.GetUnderlyingType(targetType) ?? targetType;
            if (value == null)
            {
                if (!targetType.IsValueType || Nullable.GetUnderlyingType(targetType) != null)
                {
                    return null;
                }

                throw new InvalidOperationException(
                    "Property '" + propertyName + "' is of non-nullable type '" + targetType.Name
                    + "' and cannot be set to null.");
            }

            if (underlying.IsInstanceOfType(value))
            {
                return value;
            }

            if (underlying == typeof(string))
            {
                return Convert.ToString(value);
            }

            if (underlying == typeof(Color))
            {
                return ResolveColor(value, propertyName);
            }

            if (underlying.IsEnum)
            {
                if (value is string enumText)
                {
                    return Enum.Parse(underlying, enumText.Trim(), ignoreCase: true);
                }

                return Enum.ToObject(underlying, Convert.ChangeType(value, Enum.GetUnderlyingType(underlying)));
            }

            if (underlying == typeof(bool))
            {
                if (value is string boolText)
                {
                    if (bool.TryParse(boolText, out bool parsedBool))
                    {
                        return parsedBool;
                    }

                    if (string.Equals(boolText, "1", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(boolText, "yes", StringComparison.OrdinalIgnoreCase))
                    {
                        return true;
                    }

                    if (string.Equals(boolText, "0", StringComparison.OrdinalIgnoreCase)
                        || string.Equals(boolText, "no", StringComparison.OrdinalIgnoreCase))
                    {
                        return false;
                    }
                }

                return Convert.ToBoolean(value);
            }

            if (value is string text)
            {
                TypeConverter converter = TypeDescriptor.GetConverter(underlying);
                if (converter != null && converter.CanConvertFrom(typeof(string)))
                {
                    try
                    {
                        return converter.ConvertFromInvariantString(text)
                            ?? converter.ConvertFromString(text);
                    }
                    catch
                    {
                        // Fall through to ChangeType.
                    }
                }
            }

            try
            {
                return Convert.ChangeType(value, underlying, CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                throw new InvalidOperationException(
                    "Cannot convert value '" + value + "' to type '" + underlying.Name
                    + "' for property '" + propertyName + "'.",
                    ex);
            }
        }

        public void ShowMessage(string message, string title = null)
        {
            InvokeOnUi(() =>
            {
                if (_form != null && !_form.IsDisposed)
                {
                    FormNativeWindowActivator.SoftBringToFront(_form);
                }

                MessageBox.Show(
                    _form,
                    message ?? string.Empty,
                    string.IsNullOrWhiteSpace(title) ? (_form == null ? "Form" : _form.Text) : title,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
            });
        }

        /// <summary>
        /// Show a native Win32 Yes/No confirmation owned by the active form. Returns true when Yes is chosen.
        /// Button captions follow the OS language (yesText/noText are ignored for native MessageBox).
        /// </summary>
        public bool RequestConfirm(
            string message,
            string title = null,
            string yesText = null,
            string noText = null)
        {
            bool confirmed = false;
            InvokeOnUi(() =>
            {
                if (_form != null && !_form.IsDisposed)
                {
                    FormNativeWindowActivator.SoftBringToFront(_form);
                }

                DialogResult result = MessageBox.Show(
                    _form,
                    message ?? string.Empty,
                    string.IsNullOrWhiteSpace(title) ? (_form == null ? "Form" : _form.Text) : title,
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Question);
                confirmed = result == DialogResult.Yes;
            });
            return confirmed;
        }

        public string GetControlText(string controlId)
        {
            string result = string.Empty;
            InvokeOnUi(() =>
            {
                Control control = GetControl(controlId);
                result = ReadTextLocal(control);
            });
            return result ?? string.Empty;
        }

        private static string ReadTextLocal(Control control)
        {
            if (control == null)
            {
                return string.Empty;
            }

            if (control is DateTimePicker dateTimePicker)
            {
                return FormRenderer.ReadDateTimePickerValue(dateTimePicker);
            }

            if (control is NumericUpDown numeric)
            {
                return Convert.ToString(numeric.Value) ?? string.Empty;
            }

            if (control is CheckBox checkBox)
            {
                return checkBox.Text ?? string.Empty;
            }

            if (control is RadioButton radioButton)
            {
                return radioButton.Text ?? string.Empty;
            }

            if (control is ListBox listBox && !(control is CheckedListBox))
            {
                return listBox.SelectedItem == null
                    ? string.Empty
                    : (Convert.ToString(listBox.SelectedItem) ?? string.Empty);
            }

            if (control is CheckedListBox checkedList)
            {
                return checkedList.SelectedItem == null
                    ? string.Empty
                    : (Convert.ToString(checkedList.SelectedItem) ?? string.Empty);
            }

            if (control is ComboBox comboBox)
            {
                return comboBox.SelectedItem == null
                    ? (comboBox.Text ?? string.Empty)
                    : (Convert.ToString(comboBox.SelectedItem) ?? string.Empty);
            }

            if (control is PictureBox pictureBox)
            {
                return FormRenderer.ReadPicturePath(pictureBox);
            }

            return control.Text ?? string.Empty;
        }

        public string BuildResultJson()
        {
            var snapshot = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            InvokeOnUi(() =>
            {
                if (_controls == null)
                {
                    return;
                }

                foreach (KeyValuePair<string, Control> pair in _controls)
                {
                    if (pair.Value is Form)
                    {
                        continue;
                    }

                    if (string.Equals(pair.Key, "form", StringComparison.OrdinalIgnoreCase))
                    {
                        continue;
                    }

                    snapshot[pair.Key] = ReadValueLocal(pair.Value);
                }
            });

            var result = new
            {
                closeReason = CloseReason.ToString(),
                errorMessage = ErrorMessage,
                lastControlId = LastControlId,
                lastEventName = LastEventName,
                controlValues = snapshot
            };

            return JsonConvert.SerializeObject(result);
        }

        public void Dispose()
        {
            if (!_closed)
            {
                Close(FormCloseReason.WorkflowAbort);
            }

            WaitUiClosed(3000);
            if (_ambientPushed)
            {
                FormSessionAmbient.Pop(this);
                _ambientPushed = false;
            }

            _uiReady?.Dispose();
            _uiClosed?.Dispose();
        }

        private void UiThreadMain(FormDefinition definition, int timeoutMs)
        {
            try
            {
                // Before any control creation (including later CreateControlInstance on this thread).
                OsCulture.ApplyToCurrentThread(definition != null ? definition.Culture : null);

                _definition = definition;
                FormRenderResult rendered = FormRenderer.Render(definition);
                _form = rendered.Form;
                _controls = rendered.Controls;

                _form.FormClosing += (s, e) =>
                {
                    if (!_closed)
                    {
                        // User clicked X — Close Scope intercepts; otherwise allow close.
                        if (_acceptEvents && InterceptUserClose)
                        {
                            e.Cancel = true;
                            EnqueueAndSignal(new FormEvent
                            {
                                ControlId = "form",
                                EventName = "Closing"
                            });
                            return;
                        }

                        CloseReason = FormCloseReason.UserClose;
                        _closed = true;
                        _acceptEvents = false;
                    }
                };

                _form.FormClosed += (s, e) =>
                {
                    _closed = true;
                    _acceptEvents = false;
                    if (CloseReason == FormCloseReason.None)
                    {
                        CloseReason = FormCloseReason.UserClose;
                    }

                    EnqueueAndSignal(new FormEvent
                    {
                        ControlId = "form",
                        EventName = "Closed"
                    });
                    _uiClosed.Set();
                };

                if (timeoutMs > 0)
                {
                    _timeoutTimer = new System.Windows.Forms.Timer { Interval = timeoutMs };
                    _timeoutTimer.Tick += (s, e) =>
                    {
                        _timeoutTimer.Stop();
                        Close(FormCloseReason.Timeout);
                    };
                    _timeoutTimer.Start();
                }

                _form.Shown += (s, e) => _uiReady.Set();
                Application.Run(_form);
            }
            catch (Exception ex)
            {
                _uiException = ex;
                _closed = true;
                CloseReason = FormCloseReason.Error;
                ErrorMessage = ex.Message;
                _uiReady.Set();
                _uiClosed.Set();
            }
        }

        private void EnsureNativeEventWired(string controlId, string eventName)
        {
            if (_controls == null)
            {
                throw new InvalidOperationException("Form controls are not available.");
            }

            string key = MakeKey(controlId, eventName);
            lock (_sync)
            {
                if (_wiredNatives.ContainsKey(key))
                {
                    return;
                }
            }

            if (!_controls.TryGetValue(controlId, out Control control) || control == null || control.IsDisposed)
            {
                if (string.Equals(controlId, "form", StringComparison.OrdinalIgnoreCase))
                {
                    control = _form;
                }
                else
                {
                    throw new InvalidOperationException(
                        "Cannot bind event '" + eventName + "': control not found '" + controlId + "'.");
                }
            }

            if (control == null || control.IsDisposed)
            {
                throw new InvalidOperationException(
                    "Cannot bind event '" + eventName + "': control not found '" + controlId + "'.");
            }

            string normalized = eventName.Trim();

            // Form Closing/Closed handled in FormClosing/FormClosed.
            if (control is Form &&
                (string.Equals(normalized, "Closing", StringComparison.OrdinalIgnoreCase)
                 || string.Equals(normalized, "Closed", StringComparison.OrdinalIgnoreCase)))
            {
                return;
            }

            Action unwire = CreateNativeUnwire(control, controlId, normalized);
            if (unwire == null)
            {
                throw new InvalidOperationException(
                    "Unsupported or unwired event '" + eventName + "' for control '" + controlId + "'.");
            }

            lock (_sync)
            {
                if (_wiredNatives.ContainsKey(key))
                {
                    unwire();
                    return;
                }

                _wiredNatives[key] = unwire;
            }
        }

        private void UnwireNativeEvent(string controlId, string eventName)
        {
            string key = MakeKey(controlId, eventName);
            Action unwire;
            lock (_sync)
            {
                if (!_wiredNatives.TryGetValue(key, out unwire))
                {
                    return;
                }

                _wiredNatives.Remove(key);
            }

            try
            {
                unwire();
            }
            catch
            {
                // ignored — control may already be disposed
            }
        }

        private Action CreateNativeUnwire(Control control, string controlId, string eventName)
        {
            string normalized = eventName.Trim();
            switch (normalized.ToLowerInvariant())
            {
                case "click":
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "Click", e);
                        control.Click += handler;
                        return () => control.Click -= handler;
                    }
                case "textchanged":
                    if (control is TextBox textBox)
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "TextChanged", e);
                        textBox.TextChanged += handler;
                        return () => textBox.TextChanged -= handler;
                    }
                    else
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "TextChanged", e);
                        control.TextChanged += handler;
                        return () => control.TextChanged -= handler;
                    }
                case "check":
                    if (control is CheckBox checkOn)
                    {
                        EventHandler handler = (s, e) =>
                        {
                            if (checkOn.Checked)
                            {
                                Raise(controlId, "Check", e);
                            }
                        };
                        checkOn.CheckedChanged += handler;
                        return () => checkOn.CheckedChanged -= handler;
                    }

                    if (control is RadioButton radioOn)
                    {
                        EventHandler handler = (s, e) =>
                        {
                            if (radioOn.Checked)
                            {
                                Raise(controlId, "Check", e);
                            }
                        };
                        radioOn.CheckedChanged += handler;
                        return () => radioOn.CheckedChanged -= handler;
                    }

                    return null;
                case "uncheck":
                    if (control is CheckBox checkOff)
                    {
                        EventHandler handler = (s, e) =>
                        {
                            if (!checkOff.Checked)
                            {
                                Raise(controlId, "Uncheck", e);
                            }
                        };
                        checkOff.CheckedChanged += handler;
                        return () => checkOff.CheckedChanged -= handler;
                    }

                    if (control is RadioButton radioOff)
                    {
                        EventHandler handler = (s, e) =>
                        {
                            if (!radioOff.Checked)
                            {
                                Raise(controlId, "Uncheck", e);
                            }
                        };
                        radioOff.CheckedChanged += handler;
                        return () => radioOff.CheckedChanged -= handler;
                    }

                    return null;
                case "checkedchanged":
                    if (control is CheckBox checkBox)
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "CheckedChanged", e);
                        checkBox.CheckedChanged += handler;
                        return () => checkBox.CheckedChanged -= handler;
                    }

                    if (control is RadioButton radioChanged)
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "CheckedChanged", e);
                        radioChanged.CheckedChanged += handler;
                        return () => radioChanged.CheckedChanged -= handler;
                    }

                    return null;
                case "change":
                case "selectedindexchanged":
                case "valuechanged":
                    {
                        string raiseAs;
                        if (string.Equals(normalized, "SelectedIndexChanged", StringComparison.OrdinalIgnoreCase))
                        {
                            raiseAs = "SelectedIndexChanged";
                        }
                        else if (string.Equals(normalized, "ValueChanged", StringComparison.OrdinalIgnoreCase))
                        {
                            raiseAs = "ValueChanged";
                        }
                        else
                        {
                            raiseAs = "Change";
                        }

                        if (control is ComboBox comboBox)
                        {
                            EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                            comboBox.SelectedIndexChanged += handler;
                            return () => comboBox.SelectedIndexChanged -= handler;
                        }

                        if (control is CheckedListBox checkedList)
                        {
                            EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                            checkedList.SelectedIndexChanged += handler;
                            return () => checkedList.SelectedIndexChanged -= handler;
                        }

                        if (control is ListBox listBox)
                        {
                            EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                            listBox.SelectedIndexChanged += handler;
                            return () => listBox.SelectedIndexChanged -= handler;
                        }

                        if (control is TabControl tabControl)
                        {
                            EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                            tabControl.SelectedIndexChanged += handler;
                            return () => tabControl.SelectedIndexChanged -= handler;
                        }

                        if (control is DateTimePicker dateTimePicker)
                        {
                            EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                            dateTimePicker.ValueChanged += handler;
                            return () => dateTimePicker.ValueChanged -= handler;
                        }

                        if (control is NumericUpDown numeric)
                        {
                            EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                            numeric.ValueChanged += handler;
                            return () => numeric.ValueChanged -= handler;
                        }

                        return null;
                    }
                case "focus":
                case "gotfocus":
                    {
                        string raiseAs = string.Equals(normalized, "Focus", StringComparison.OrdinalIgnoreCase)
                            ? "Focus"
                            : "GotFocus";
                        EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                        control.GotFocus += handler;
                        return () => control.GotFocus -= handler;
                    }
                case "blur":
                case "lostfocus":
                    {
                        string raiseAs = string.Equals(normalized, "Blur", StringComparison.OrdinalIgnoreCase)
                            ? "Blur"
                            : "LostFocus";
                        EventHandler handler = (s, e) => Raise(controlId, raiseAs, e);
                        control.LostFocus += handler;
                        return () => control.LostFocus -= handler;
                    }
                case "keyup":
                    {
                        KeyEventHandler handler = (s, e) => Raise(controlId, "KeyUp", e);
                        control.KeyUp += handler;
                        return () => control.KeyUp -= handler;
                    }
                case "keydown":
                    {
                        KeyEventHandler handler = (s, e) => Raise(controlId, "KeyDown", e);
                        control.KeyDown += handler;
                        return () => control.KeyDown -= handler;
                    }
                case "enter":
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "Enter", e);
                        control.Enter += handler;
                        return () => control.Enter -= handler;
                    }
                case "leave":
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "Leave", e);
                        control.Leave += handler;
                        return () => control.Leave -= handler;
                    }
                case "doubleclick":
                    {
                        EventHandler handler = (s, e) => Raise(controlId, "DoubleClick", e);
                        control.DoubleClick += handler;
                        return () => control.DoubleClick -= handler;
                    }
                case "mousemove":
                    {
                        MouseEventHandler handler = (s, e) => Raise(controlId, "MouseMove", e);
                        control.MouseMove += handler;
                        return () => control.MouseMove -= handler;
                    }
                case "mousedown":
                    {
                        MouseEventHandler handler = (s, e) => Raise(controlId, "MouseDown", e);
                        control.MouseDown += handler;
                        return () => control.MouseDown -= handler;
                    }
                case "mouseup":
                    {
                        MouseEventHandler handler = (s, e) => Raise(controlId, "MouseUp", e);
                        control.MouseUp += handler;
                        return () => control.MouseUp -= handler;
                    }
                case "selectionchanged":
                    if (control is DataGridView gridSel)
                    {
                        EventHandler handler = (s, e) =>
                        {
                            LastRowIndex = gridSel.CurrentRow == null || gridSel.CurrentRow.IsNewRow
                                ? -1
                                : gridSel.CurrentRow.Index;
                            LastColumnName = gridSel.CurrentCell == null
                                ? null
                                : (gridSel.Columns[gridSel.CurrentCell.ColumnIndex].DataPropertyName
                                   ?? gridSel.Columns[gridSel.CurrentCell.ColumnIndex].Name);
                            Raise(controlId, "SelectionChanged", e);
                        };
                        gridSel.SelectionChanged += handler;
                        return () => gridSel.SelectionChanged -= handler;
                    }

                    return null;
                case "cellclick":
                    if (control is DataGridView gridClick)
                    {
                        DataGridViewCellEventHandler handler = (s, e) =>
                        {
                            LastRowIndex = e.RowIndex;
                            LastColumnName = e.ColumnIndex >= 0 && e.ColumnIndex < gridClick.Columns.Count
                                ? (gridClick.Columns[e.ColumnIndex].DataPropertyName
                                   ?? gridClick.Columns[e.ColumnIndex].Name)
                                : null;
                            Raise(controlId, "CellClick", e);
                        };
                        gridClick.CellClick += handler;
                        return () => gridClick.CellClick -= handler;
                    }

                    return null;
                default:
                    return TryWireByReflection(control, controlId, normalized);
            }
        }

        private Action TryWireByReflection(Control control, string controlId, string eventName)
        {
            System.Reflection.EventInfo info = control.GetType().GetEvent(eventName);
            if (info == null)
            {
                return null;
            }

            EventHandler handler = (s, e) => Raise(controlId, eventName, e);
            try
            {
                info.AddEventHandler(control, handler);
                return () =>
                {
                    try
                    {
                        info.RemoveEventHandler(control, handler);
                    }
                    catch
                    {
                        // ignored
                    }
                };
            }
            catch
            {
                return null;
            }
        }

        private void Raise(string controlId, string eventName, object eventArgs)
        {
            if (!_acceptEvents || _closed)
            {
                return;
            }

            EnqueueAndSignal(new FormEvent
            {
                ControlId = controlId,
                EventName = eventName,
                EventArgs = eventArgs
            });
        }

        private void EnqueueAndSignal(FormEvent formEvent)
        {
            if (formEvent == null)
            {
                return;
            }

            if (_handlerRunning)
            {
                if (_activeBehavior == UiBehavior.LockIgnore)
                {
                    return;
                }

                _pendingEvents.Enqueue(formEvent);
                return;
            }

            _pendingEvents.Enqueue(formEvent);
            PumpPendingToWorkflow();
        }

        private void PumpPendingToWorkflow()
        {
            if (_workflow == null || _handlerRunning || _pendingEvents.IsEmpty)
            {
                return;
            }

            _workflow.TryBeginResumeBookmark(_bookmarkName, null);
        }

        private bool HasBinding(string controlId, string eventName)
        {
            lock (_sync)
            {
                return _bindings.ContainsKey(MakeKey(controlId, eventName));
            }
        }

        private void SetUiEnabled(bool enabled)
        {
            InvokeOnUi(() =>
            {
                if (_form == null || _form.IsDisposed)
                {
                    return;
                }

                foreach (Control control in _form.Controls)
                {
                    control.Enabled = enabled;
                }

                FlushControlPaint(_form);
            });
        }

        private void UnregisterControlTree(Control control)
        {
            if (control == null || _controls == null)
            {
                return;
            }

            if (control is TabControl tabControl)
            {
                foreach (TabPage page in tabControl.TabPages)
                {
                    UnregisterControlTree(page);
                }
            }
            else
            {
                foreach (Control child in control.Controls)
                {
                    UnregisterControlTree(child);
                }
            }

            string name = control.Name;
            if (!string.IsNullOrWhiteSpace(name))
            {
                ClearBindingsForControlLocal(name.Trim());
                _controls.Remove(name.Trim());
            }

            // Drop any map aliases that still point at this instance.
            var staleKeys = new List<string>();
            foreach (KeyValuePair<string, Control> pair in _controls)
            {
                if (ReferenceEquals(pair.Value, control))
                {
                    staleKeys.Add(pair.Key);
                }
            }

            foreach (string key in staleKeys)
            {
                ClearBindingsForControlLocal(key);
                _controls.Remove(key);
            }
        }

        /// <summary>Must run on UI thread (or with no UI callbacks). Clears bindings + native wires.</summary>
        private void ClearBindingsForControlLocal(string controlId)
        {
            if (string.IsNullOrWhiteSpace(controlId))
            {
                return;
            }

            string id = controlId.Trim();
            var keys = new List<string>();
            lock (_sync)
            {
                foreach (string key in _bindings.Keys)
                {
                    if (KeyBelongsToControl(key, id))
                    {
                        keys.Add(key);
                    }
                }

                foreach (string key in keys)
                {
                    _bindings.Remove(key);
                }
            }

            foreach (string key in keys)
            {
                if (TrySplitKey(key, out string cid, out string ename))
                {
                    UnwireNativeEvent(cid, ename);
                }
            }
        }

        private static bool KeyBelongsToControl(string key, string controlId)
        {
            if (string.IsNullOrEmpty(key) || string.IsNullOrEmpty(controlId))
            {
                return false;
            }

            int sep = key.IndexOf("::", StringComparison.Ordinal);
            if (sep <= 0)
            {
                return false;
            }

            return string.Equals(key.Substring(0, sep), controlId, StringComparison.OrdinalIgnoreCase);
        }

        private static bool TrySplitKey(string key, out string controlId, out string eventName)
        {
            controlId = null;
            eventName = null;
            if (string.IsNullOrEmpty(key))
            {
                return false;
            }

            int sep = key.IndexOf("::", StringComparison.Ordinal);
            if (sep <= 0 || sep + 2 >= key.Length)
            {
                return false;
            }

            controlId = key.Substring(0, sep);
            eventName = key.Substring(sep + 2);
            return true;
        }

        private static void ApplyReadOnly(Control control, string controlId, bool readOnly)
        {
            if (control is TextBoxBase textBoxBase)
            {
                textBoxBase.ReadOnly = readOnly;
                return;
            }

            if (control is MaskedTextBox masked)
            {
                masked.ReadOnly = readOnly;
                return;
            }

            if (control is NumericUpDown numeric)
            {
                numeric.ReadOnly = readOnly;
                return;
            }

            if (control is DataGridView grid)
            {
                grid.ReadOnly = readOnly;
                return;
            }

            if (control is ComboBox combo)
            {
                ComboBoxReadOnly.Set(combo, readOnly);
                return;
            }

            throw new InvalidOperationException(
                "SetControlReadOnly is not supported for '" + control.GetType().Name
                + "' (control '" + controlId + "'). Supported: TextBox, TextArea, MaskedTextBox, ComboBox, NumericUpDown, DataGridView.");
        }

        private static void ApplyControlColor(Control control, bool isBackColor, object value)
        {
            if (control == null)
            {
                throw new ArgumentNullException(nameof(control));
            }

            Color color = ResolveColor(value, isBackColor ? "BackColor" : "ForeColor");

            if (isBackColor)
            {
                // Buttons / TabPages ignore BackColor while visual styles are on.
                if (control is ButtonBase button)
                {
                    button.UseVisualStyleBackColor = false;
                    if (button.FlatStyle == FlatStyle.Standard)
                    {
                        button.FlatStyle = FlatStyle.Flat;
                    }
                }

                if (control is TabPage page)
                {
                    page.UseVisualStyleBackColor = false;
                }

                control.BackColor = color;
            }
            else
            {
                ApplyForeColor(control, color);
            }
        }

        /// <summary>
        /// WinForms TextBox/TextArea (especially ReadOnly) often ignore ForeColor until BackColor
        /// is explicitly set away from the internal "default color" mode.
        /// </summary>
        private static void ApplyForeColor(Control control, Color color)
        {
            if (control is TextBoxBase textBox)
            {
                Color back = textBox.BackColor;
                int controlArgb = SystemColors.Control.ToArgb();
                int windowArgb = SystemColors.Window.ToArgb();

                if (textBox.ReadOnly
                    || back.ToArgb() == controlArgb
                    || back.ToArgb() == windowArgb)
                {
                    // Keep a normal editable look, but force an explicit BackColor so ForeColor paints.
                    textBox.BackColor = SystemColors.Window;
                }
                else
                {
                    // Re-assign to clear WinForms default-color tracking without changing the color.
                    textBox.BackColor = back;
                }

                textBox.ForeColor = color;
                return;
            }

            control.ForeColor = color;
        }

        private static Color ResolveColor(object value, string propertyName)
        {
            if (value == null)
            {
                throw new ArgumentException(propertyName + " value is required (e.g. \"#FFCC00\" or \"Red\").");
            }

            if (value is Color color)
            {
                return color;
            }

            string text = Convert.ToString(value);
            Color? parsed = FontStyleUtil.ParseColor(text);
            if (!parsed.HasValue || parsed.Value.IsEmpty)
            {
                throw new ArgumentException(
                    propertyName + " must be an HTML color (e.g. \"#FFCC00\") or named color (e.g. \"Red\"). Value: '"
                    + text + "'.");
            }

            if (parsed.Value.A == 0
                && !string.Equals(text.Trim(), "Transparent", StringComparison.OrdinalIgnoreCase)
                && !text.Trim().StartsWith("#", StringComparison.Ordinal))
            {
                Color named = Color.FromName(text.Trim());
                if (!named.IsKnownColor && named.ToArgb() == 0)
                {
                    throw new ArgumentException(
                        propertyName + " must be an HTML color (e.g. \"#FFCC00\") or named color (e.g. \"Red\"). Value: '"
                        + text + "'.");
                }
            }

            return parsed.Value;
        }

        private static void FlushControlPaint(Control control)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            try
            {
                // Force immediate repaint even when the BindEvent handler is
                // occupying the UI synchronization context (no message pump yet).
                control.Refresh();
            }
            catch
            {
                // ignored — disposed / handle not created
            }
        }

        private void InvokeOnUi(Action action)
        {
            if (action == null)
            {
                return;
            }

            EnsureOpen();
            if (_form == null || _form.IsDisposed)
            {
                throw new InvalidOperationException("Form is not available.");
            }

            if (_form.InvokeRequired)
            {
                _form.Invoke(action);
            }
            else
            {
                action();
            }
        }

        private void EnsureOpen()
        {
            if (_uiThread == null || _uiException != null)
            {
                throw new InvalidOperationException("FormSession is not open.");
            }
        }

        private static object ReadValueLocal(Control control)
        {
            if (control is CheckBox checkBox)
            {
                return checkBox.Checked;
            }

            if (control is RadioButton radioButton)
            {
                return radioButton.Checked;
            }

            if (control is NumericUpDown numeric)
            {
                return numeric.Value;
            }

            if (control is ComboBox comboBox)
            {
                return comboBox.SelectedItem == null ? string.Empty : Convert.ToString(comboBox.SelectedItem);
            }

            if (control is CheckedListBox checkedList)
            {
                var checkedItems = new List<string>();
                foreach (object item in checkedList.CheckedItems)
                {
                    checkedItems.Add(Convert.ToString(item) ?? string.Empty);
                }

                return checkedItems;
            }

            if (control is ListBox listBox)
            {
                return listBox.SelectedItem == null ? string.Empty : Convert.ToString(listBox.SelectedItem);
            }

            if (control is DateTimePicker dateTimePicker)
            {
                return FormRenderer.ReadDateTimePickerValue(dateTimePicker);
            }

            if (control is PictureBox pictureBox)
            {
                return FormRenderer.ReadPicturePath(pictureBox);
            }

            return control.Text;
        }

        private static string MakeKey(string controlId, string eventName)
        {
            return (controlId ?? string.Empty).Trim() + "::" + (eventName ?? string.Empty).Trim();
        }

        public sealed class BoundEvent
        {
            public string ControlId { get; set; }
            public string EventName { get; set; }
            public UiBehavior Behavior { get; set; }
            public object ActivityKey { get; set; }
        }
    }
}
