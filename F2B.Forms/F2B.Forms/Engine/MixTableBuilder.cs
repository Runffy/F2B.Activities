using System;
using System.Collections;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Windows.Forms;
using F2B.Forms.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// Builds / fills a WinForms DataGridView used as MixTable.
    /// </summary>
    internal static class MixTableBuilder
    {
        internal const string SchemaTagKey = "F2B.Forms.MixTable.Schema";

        public static DataGridView CreateEmpty(ControlDefinition definition)
        {
            var grid = new DataGridView
            {
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoGenerateColumns = false,
                // None + per-column sizing: fixed width or autofit; overflow → horizontal scroll.
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.None,
                ScrollBars = ScrollBars.Both,
                RowHeadersVisible = false,
                BackgroundColor = SystemColors.Window,
                BorderStyle = BorderStyle.FixedSingle,
                EditMode = DataGridViewEditMode.EditOnEnter,
                // Required for per-column HeaderCell.Style.Alignment to take effect.
                EnableHeadersVisualStyles = false
            };

            // Default header align center; per-column headerAlign overrides on HeaderCell.
            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersDefaultCellStyle.Padding = Padding.Empty;

            // Suppress the default bright-blue full-row selection; keep a barely-visible cue.
            ApplySubtleSelectionStyle(grid);

            grid.Tag = new MixTableHostTag();
            grid.DataError += (s, e) => { e.ThrowException = false; };

            if (definition != null && definition.Columns != null && definition.Columns.Count > 0)
            {
                ApplyColumns(grid, definition.Columns);
                ApplyInitialRows(grid, definition);
            }

            return grid;
        }

        /// <summary>
        /// Soften / neutralize DataGridView's default Highlight selection (blue + white text).
        /// Selection still works for events; visual effect is nearly none.
        /// </summary>
        internal static void ApplySubtleSelectionStyle(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            Color back = grid.DefaultCellStyle.BackColor.IsEmpty
                ? SystemColors.Window
                : grid.DefaultCellStyle.BackColor;
            Color fore = grid.DefaultCellStyle.ForeColor.IsEmpty
                ? SystemColors.ControlText
                : grid.DefaultCellStyle.ForeColor;

            // Very light gray so selected row is barely distinguishable (not system Highlight blue).
            Color selectionBack = Color.FromArgb(236, 236, 236);
            if (selectionBack.ToArgb() == back.ToArgb())
            {
                selectionBack = Color.FromArgb(
                    Math.Max(0, back.R - 12),
                    Math.Max(0, back.G - 12),
                    Math.Max(0, back.B - 12));
            }

            grid.DefaultCellStyle.SelectionBackColor = selectionBack;
            grid.DefaultCellStyle.SelectionForeColor = fore;
            grid.RowsDefaultCellStyle.SelectionBackColor = selectionBack;
            grid.RowsDefaultCellStyle.SelectionForeColor = fore;
            grid.RowTemplate.DefaultCellStyle.SelectionBackColor = selectionBack;
            grid.RowTemplate.DefaultCellStyle.SelectionForeColor = fore;

            // Alternating / per-column styles inherit selection unless set; keep grid-level defaults.
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor =
                grid.ColumnHeadersDefaultCellStyle.BackColor.IsEmpty
                    ? SystemColors.Control
                    : grid.ColumnHeadersDefaultCellStyle.BackColor;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = fore;
        }

        /// <summary>
        /// Clicking outside any MixTable clears its row selection (form + nested containers).
        /// Safe to call once per form; also tracks controls added later.
        /// </summary>
        public static void WireClearSelectionOnOutsideClick(Control root)
        {
            if (root == null)
            {
                return;
            }

            AttachOutsideClickClear(root);
        }

        private static void AttachOutsideClickClear(Control control)
        {
            if (control == null || control.IsDisposed)
            {
                return;
            }

            if (IsMixTableGrid(control))
            {
                DataGridView grid = (DataGridView)control;
                grid.LostFocus -= MixTableOnLostFocusClearSelection;
                grid.LostFocus += MixTableOnLostFocusClearSelection;
                return;
            }

            control.MouseDown -= OutsideClickClearMixTableSelection;
            control.MouseDown += OutsideClickClearMixTableSelection;
            control.ControlAdded -= OutsideClickOnControlAdded;
            control.ControlAdded += OutsideClickOnControlAdded;

            foreach (Control child in control.Controls)
            {
                AttachOutsideClickClear(child);
            }
        }

        private static void OutsideClickOnControlAdded(object sender, ControlEventArgs e)
        {
            AttachOutsideClickClear(e.Control);
        }

        private static void OutsideClickClearMixTableSelection(object sender, MouseEventArgs e)
        {
            Control host = sender as Control;
            if (host == null)
            {
                return;
            }

            Form form = host.FindForm() ?? (host as Form);
            if (form == null || form.IsDisposed)
            {
                return;
            }

            ClearAllMixTableSelections(form);
        }

        private static void MixTableOnLostFocusClearSelection(object sender, EventArgs e)
        {
            DataGridView grid = sender as DataGridView;
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            // Defer: cell editors briefly take focus while still belonging to the grid.
            try
            {
                grid.BeginInvoke(new Action(() =>
                {
                    if (grid.IsDisposed)
                    {
                        return;
                    }

                    if (!grid.ContainsFocus)
                    {
                        grid.ClearSelection();
                    }
                }));
            }
            catch (InvalidOperationException)
            {
                // Handle not created yet / disposing.
            }
        }

        private static void ClearAllMixTableSelections(Control root)
        {
            if (root == null || root.IsDisposed)
            {
                return;
            }

            if (IsMixTableGrid(root))
            {
                ((DataGridView)root).ClearSelection();
                return;
            }

            foreach (Control child in root.Controls)
            {
                ClearAllMixTableSelections(child);
            }
        }

        private static void ApplyInitialRows(DataGridView grid, ControlDefinition definition)
        {
            ResolveLegacyInitialRows(definition);
            IList<Dictionary<string, object>> seedRows = definition.InitialRows;
            if (seedRows == null || seedRows.Count == 0)
            {
                return;
            }

            List<MixTableColumnDefinition> schema = GetSchema(grid);
            if (schema.Count == 0)
            {
                return;
            }

            // AddRow preserves ComboBox { value, items } payloads (DataTable would flatten them).
            foreach (Dictionary<string, object> values in seedRows)
            {
                if (values == null)
                {
                    continue;
                }

                AddRow(grid, values);
            }

            RefreshAutofitColumns(grid);
        }

        private static void ResolveLegacyInitialRows(ControlDefinition definition)
        {
            if (definition == null)
            {
                return;
            }

            if (definition.InitialRows != null && definition.InitialRows.Count > 0)
            {
                return;
            }

            if (definition.ExtensionData == null)
            {
                return;
            }

            JToken token;
            if (!definition.ExtensionData.TryGetValue("previewRows", out token) || token == null || token.Type == JTokenType.Null)
            {
                return;
            }

            definition.InitialRows = token.ToObject<List<Dictionary<string, object>>>();
            definition.ExtensionData.Remove("previewRows");
        }

        private static object UnwrapJsonValue(object value)
        {
            JValue jValue = value as JValue;
            return jValue != null ? jValue.Value : value;
        }

        public static List<MixTableColumnDefinition> GetSchema(DataGridView grid)
        {
            if (grid == null)
            {
                return new List<MixTableColumnDefinition>();
            }

            if (grid.Columns.Count > 0 && grid.Columns[0].Tag is MixTableColumnDefinition)
            {
                var list = new List<MixTableColumnDefinition>();
                foreach (DataGridViewColumn col in grid.Columns)
                {
                    if (col.Tag is MixTableColumnDefinition def)
                    {
                        list.Add(def);
                    }
                }

                if (list.Count > 0)
                {
                    return list;
                }
            }

            object boxed = grid.ExtendedPropertiesGet(SchemaTagKey);
            if (boxed is List<MixTableColumnDefinition> cached)
            {
                return cached;
            }

            return new List<MixTableColumnDefinition>();
        }

        public static void ApplyColumns(DataGridView grid, IList<MixTableColumnDefinition> columns)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            grid.DataSource = null;
            grid.Rows.Clear();
            grid.Columns.Clear();

            var schema = new List<MixTableColumnDefinition>();
            if (columns != null)
            {
                foreach (MixTableColumnDefinition raw in columns)
                {
                    if (raw == null || string.IsNullOrWhiteSpace(raw.Name))
                    {
                        continue;
                    }

                    var def = new MixTableColumnDefinition
                    {
                        Name = raw.Name.Trim(),
                        Type = MixTableColumnType.Require(raw.Type),
                        Text = raw.Text,
                        Items = raw.Items == null ? null : new List<string>(raw.Items),
                        Width = raw.Width,
                        ReadOnly = raw.ReadOnly,
                        HeaderAlign = raw.HeaderAlign,
                        ContentAlign = raw.ContentAlign,
                        FontFamily = raw.FontFamily,
                        FontSize = raw.FontSize,
                        FontBold = raw.FontBold,
                        FontItalic = raw.FontItalic,
                        FontUnderline = raw.FontUnderline,
                        ForeColor = raw.ForeColor,
                        BackColor = raw.BackColor
                    };
                    schema.Add(def);
                    grid.Columns.Add(CreateColumn(def));
                }
            }

            grid.ExtendedPropertiesSet(SchemaTagKey, schema);
        }

        public static void BindDataTable(DataGridView grid, DataTable dataTable)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            List<MixTableColumnDefinition> schema = GetSchema(grid);
            if (schema.Count == 0)
            {
                throw new InvalidOperationException(
                    "MixTable has no columns. Call Set MixTable Columns (or define columns in form JSON) first.");
            }

            DataTable bound = BuildBoundTable(schema, dataTable);
            EnsureColumnsMatchSchema(grid, schema);
            // Unbound rows so per-cell display mode (Control/Text/Hidden) can replace cell types.
            FillUnboundRows(grid, bound, schema);
            RefreshAutofitColumns(grid);
        }

        public static int AddRow(DataGridView grid, IDictionary<string, object> values)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            List<MixTableColumnDefinition> schema = GetSchema(grid);
            if (schema.Count == 0)
            {
                throw new InvalidOperationException("MixTable has no columns.");
            }

            // Prefer unbound so Set Cell Display can swap cell editors.
            if (grid.DataSource != null)
            {
                DataTable existing = grid.DataSource as DataTable;
                grid.DataSource = null;
                if (existing != null)
                {
                    FillUnboundRows(grid, existing, schema);
                }
            }

            int index = grid.Rows.Add();
            DataGridViewRow viewRow = grid.Rows[index];
            foreach (MixTableColumnDefinition colDef in schema)
            {
                object value = null;
                if (values != null)
                {
                    foreach (KeyValuePair<string, object> pair in values)
                    {
                        if (string.Equals(pair.Key, colDef.Name, StringComparison.OrdinalIgnoreCase))
                        {
                            value = pair.Value;
                            break;
                        }
                    }
                }

                DataGridViewColumn col = grid.Columns[colDef.Name];
                ApplyControlCell(viewRow.Cells[col.Index], col, colDef, value);
            }

            RefreshAutofitColumns(grid);
            return index;
        }

        private static void FillUnboundRows(
            DataGridView grid,
            DataTable table,
            List<MixTableColumnDefinition> schema)
        {
            grid.DataSource = null;
            grid.Rows.Clear();
            if (table == null)
            {
                return;
            }

            foreach (DataRow dataRow in table.Rows)
            {
                int index = grid.Rows.Add();
                DataGridViewRow viewRow = grid.Rows[index];
                foreach (MixTableColumnDefinition colDef in schema)
                {
                    object raw = table.Columns.Contains(colDef.Name) ? dataRow[colDef.Name] : null;
                    DataGridViewColumn col = grid.Columns[colDef.Name];
                    ApplyControlCell(viewRow.Cells[col.Index], col, colDef, raw);
                }
            }
        }

        /// <summary>
        /// Write a Control-mode cell, including ComboBox per-row items when payload is {value,items}.
        /// </summary>
        private static void ApplyControlCell(
            DataGridViewCell cell,
            DataGridViewColumn col,
            MixTableColumnDefinition def,
            object rawValue)
        {
            List<string> comboOverride = null;
            object coerceInput = rawValue;
            if (string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                MixTableComboCellValue parsed = MixTableComboCellValue.Parse(rawValue);
                coerceInput = parsed.Value;
                if (parsed.HasItemsOverride)
                {
                    comboOverride = new List<string>(parsed.ItemsOverride);
                }
            }

            object coerced = CoerceCellValue(def, coerceInput);
            DataGridViewCell target = cell;
            DataGridViewComboBoxCell comboCell = cell as DataGridViewComboBoxCell;
            if (comboCell == null && col is DataGridViewComboBoxColumn)
            {
                target = CloneTemplateCell(col);
                DataGridView grid = cell.DataGridView;
                if (grid != null && cell.RowIndex >= 0)
                {
                    grid.Rows[cell.RowIndex].Cells[cell.ColumnIndex] = target;
                }

                comboCell = target as DataGridViewComboBoxCell;
            }

            if (comboCell != null)
            {
                ApplyComboItems(comboCell, def, comboOverride);
            }

            // Pin column fore/back onto the cell so grid chrome / row styles cannot wash them out.
            ApplyColumnColorsToCell(target, def);

            target.Value = coerced;
            target.Tag = new MixTableCellDisplayState
            {
                Mode = MixTableCellDisplayMode.Control,
                Value = coerced,
                ComboItemsOverride = comboOverride
            };
        }

        private static void ApplyColumnColorsToCell(DataGridViewCell cell, MixTableColumnDefinition def)
        {
            if (cell == null || def == null)
            {
                return;
            }

            Color? fore = FontStyleUtil.ParseColor(def.ForeColor);
            if (fore.HasValue)
            {
                cell.Style.ForeColor = fore.Value;
            }

            Color? back = FontStyleUtil.ParseColor(def.BackColor);
            if (back.HasValue)
            {
                cell.Style.BackColor = back.Value;
            }
        }

        public static void RemoveRow(DataGridView grid, int rowIndex)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            DataTable table = grid.DataSource as DataTable;
            if (table != null)
            {
                if (rowIndex < 0 || rowIndex >= table.Rows.Count)
                {
                    throw new InvalidOperationException("Row index out of range: " + rowIndex);
                }

                table.Rows.RemoveAt(rowIndex);
                return;
            }

            if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
            {
                throw new InvalidOperationException("Row index out of range: " + rowIndex);
            }

            grid.Rows.RemoveAt(rowIndex);
        }

        public static void ClearRows(DataGridView grid)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            grid.DataSource = null;
            grid.Rows.Clear();
            RefreshAutofitColumns(grid);
        }

        public static object GetCellValue(DataGridView grid, int rowIndex, string column)
        {
            DataGridViewColumn col = ResolveColumn(grid, column);
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
            {
                throw new InvalidOperationException("Row index out of range: " + rowIndex);
            }

            DataGridViewCell cell = grid.Rows[rowIndex].Cells[col.Index];
            MixTableCellDisplayState state = cell.Tag as MixTableCellDisplayState;
            if (state != null)
            {
                return state.Value == null || state.Value == DBNull.Value ? string.Empty : state.Value;
            }

            object value = cell.Value;
            return value == null || value == DBNull.Value ? string.Empty : value;
        }

        public static void SetCellValue(DataGridView grid, int rowIndex, string column, object value)
        {
            DataGridViewColumn col = ResolveColumn(grid, column);
            MixTableColumnDefinition def = col.Tag as MixTableColumnDefinition
                ?? new MixTableColumnDefinition { Name = col.Name, Type = MixTableColumnType.Label };

            if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
            {
                throw new InvalidOperationException("Row index out of range: " + rowIndex);
            }

            List<string> comboOverride = null;
            object coerceInput = value;
            if (string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                MixTableComboCellValue parsed = MixTableComboCellValue.Parse(value);
                coerceInput = parsed.Value;
                if (parsed.HasItemsOverride)
                {
                    comboOverride = new List<string>(parsed.ItemsOverride);
                }
                else
                {
                    MixTableCellDisplayState existing =
                        grid.Rows[rowIndex].Cells[col.Index].Tag as MixTableCellDisplayState;
                    if (existing != null && existing.ComboItemsOverride != null)
                    {
                        // Scalar set keeps prior per-row items override when present.
                        comboOverride = new List<string>(existing.ComboItemsOverride);
                    }
                }
            }

            object coerced = CoerceCellValue(def, coerceInput);
            DataGridViewCell cell = grid.Rows[rowIndex].Cells[col.Index];
            MixTableCellDisplayState state = cell.Tag as MixTableCellDisplayState;
            string mode = state != null ? state.Mode : MixTableCellDisplayMode.Control;
            ApplyCellDisplay(grid, rowIndex, col, def, mode, coerced, comboOverride);
            RefreshAutofitColumns(grid);
        }

        /// <summary>
        /// Update one column. A scalar value fills every row; a sequence updates rows by index
        /// (extra sequence items ignored; extra MixTable rows left unchanged).
        /// </summary>
        public static int SetColumnValues(DataGridView grid, string column, object values)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

            DataGridViewColumn col = ResolveColumn(grid, column);
            MixTableColumnDefinition def = col.Tag as MixTableColumnDefinition
                ?? new MixTableColumnDefinition { Name = col.Name, Type = MixTableColumnType.Label };

            int rowCount = GetRowCount(grid);
            if (rowCount == 0)
            {
                return 0;
            }

            List<object> sequence;
            bool isSequence = TryAsValueSequence(values, out sequence);
            int updated = 0;

            if (!isSequence)
            {
                for (int r = 0; r < rowCount; r++)
                {
                    SetCellValue(grid, r, column, values);
                    updated++;
                }

                return updated;
            }
            else
            {
                int n = Math.Min(rowCount, sequence.Count);
                for (int r = 0; r < n; r++)
                {
                    SetCellValue(grid, r, column, sequence[r]);
                    updated++;
                }
            }

            RefreshAutofitColumns(grid);
            return updated;
        }

        /// <summary>
        /// True when <paramref name="values"/> is a non-string sequence (array, IList, IEnumerable).
        /// </summary>
        private static bool TryAsValueSequence(object values, out List<object> sequence)
        {
            sequence = new List<object>();
            if (values == null)
            {
                return false;
            }

            // string / StringBuilder are IEnumerable but mean a single cell value.
            if (values is string || values is System.Text.StringBuilder)
            {
                return false;
            }

            if (values is byte[] || values is char[])
            {
                return false;
            }

            IEnumerable enumerable = values as IEnumerable;
            if (enumerable == null)
            {
                return false;
            }

            // DataRow / Dictionary look like sequences of entries — treat as scalar payload instead.
            if (values is DataRow || values is IDictionary)
            {
                return false;
            }

            foreach (object item in enumerable)
            {
                sequence.Add(item);
            }

            return true;
        }

        public static void SetCellDisplay(DataGridView grid, int rowIndex, string column, string displayMode)
        {
            string mode = MixTableCellDisplayMode.Require(displayMode);
            DataGridViewColumn col = ResolveColumn(grid, column);
            MixTableColumnDefinition def = col.Tag as MixTableColumnDefinition
                ?? new MixTableColumnDefinition { Name = col.Name, Type = MixTableColumnType.Label };

            if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
            {
                throw new InvalidOperationException("Row index out of range: " + rowIndex);
            }

            object current = GetCellValue(grid, rowIndex, column);
            object coerced = CoerceCellValue(def, current);
            MixTableCellDisplayState prior = grid.Rows[rowIndex].Cells[col.Index].Tag as MixTableCellDisplayState;
            List<string> comboOverride = prior != null && prior.ComboItemsOverride != null
                ? new List<string>(prior.ComboItemsOverride)
                : null;
            ApplyCellDisplay(grid, rowIndex, col, def, mode, coerced, comboOverride);
            RefreshAutofitColumns(grid);
        }

        public static string GetCellDisplay(DataGridView grid, int rowIndex, string column)
        {
            DataGridViewColumn col = ResolveColumn(grid, column);
            if (rowIndex < 0 || rowIndex >= grid.Rows.Count)
            {
                throw new InvalidOperationException("Row index out of range: " + rowIndex);
            }

            MixTableCellDisplayState state = grid.Rows[rowIndex].Cells[col.Index].Tag as MixTableCellDisplayState;
            return state != null ? state.Mode : MixTableCellDisplayMode.Control;
        }

        private static void ApplyCellDisplay(
            DataGridView grid,
            int rowIndex,
            DataGridViewColumn col,
            MixTableColumnDefinition def,
            string mode,
            object coercedValue,
            List<string> comboItemsOverride = null)
        {
            DataGridViewCell next;
            switch (mode)
            {
                case MixTableCellDisplayMode.Text:
                    next = new DataGridViewTextBoxCell
                    {
                        Value = FormatDisplayText(def, coercedValue),
                        ReadOnly = true
                    };
                    break;
                case MixTableCellDisplayMode.Hidden:
                    next = new DataGridViewTextBoxCell
                    {
                        Value = string.Empty,
                        ReadOnly = true
                    };
                    break;
                default:
                    next = CloneTemplateCell(col);
                    if (next is DataGridViewComboBoxCell comboCell)
                    {
                        ApplyComboItems(comboCell, def, comboItemsOverride);
                    }

                    next.Value = coercedValue;
                    break;
            }

            if (mode != MixTableCellDisplayMode.Hidden)
            {
                ApplyColumnColorsToCell(next, def);
            }

            next.Tag = new MixTableCellDisplayState
            {
                Mode = mode,
                Value = coercedValue,
                ComboItemsOverride = comboItemsOverride == null ? null : new List<string>(comboItemsOverride)
            };
            grid.Rows[rowIndex].Cells[col.Index] = next;
        }

        private static void ApplyComboItems(
            DataGridViewComboBoxCell comboCell,
            MixTableColumnDefinition def,
            IList<string> itemsOverride)
        {
            if (comboCell == null)
            {
                return;
            }

            List<string> items = MixTableComboCellValue.ResolveEffectiveItems(
                def,
                itemsOverride == null
                    ? null
                    : new MixTableComboCellValue { ItemsOverride = new List<string>(itemsOverride) });

            comboCell.Items.Clear();
            foreach (string item in items)
            {
                comboCell.Items.Add(item ?? string.Empty);
            }
        }

        private static DataGridViewCell CloneTemplateCell(DataGridViewColumn col)
        {
            if (col.CellTemplate != null)
            {
                DataGridViewCell cloned = (DataGridViewCell)col.CellTemplate.Clone();
                if (cloned is DataGridViewComboBoxCell comboClone && col is DataGridViewComboBoxColumn comboCol)
                {
                    comboClone.Items.Clear();
                    foreach (object item in comboCol.Items)
                    {
                        comboClone.Items.Add(item);
                    }
                }

                return cloned;
            }

            return new DataGridViewTextBoxCell();
        }

        private static string FormatDisplayText(MixTableColumnDefinition def, object value)
        {
            if (value == null || value == DBNull.Value)
            {
                return string.Empty;
            }

            string kind = MixTableColumnType.Require(def == null ? MixTableColumnType.Label : def.Type);
            if (kind == MixTableColumnType.CheckBox)
            {
                bool on = value is bool b
                    ? b
                    : string.Equals(Convert.ToString(value), "true", StringComparison.OrdinalIgnoreCase)
                        || Convert.ToString(value) == "1";
                return on ? "True" : "False";
            }

            return Convert.ToString(value) ?? string.Empty;
        }

        private sealed class MixTableCellDisplayState
        {
            public string Mode { get; set; }
            public object Value { get; set; }

            /// <summary>Null = column default ComboBox items; non-null = per-row override.</summary>
            public List<string> ComboItemsOverride { get; set; }
        }

        public static int GetRowCount(DataGridView grid)
        {
            if (grid == null)
            {
                return 0;
            }

            DataTable table = grid.DataSource as DataTable;
            return table != null ? table.Rows.Count : grid.Rows.Count;
        }

        public static List<MixTableColumnDefinition> ParseColumnsJson(string columnsJson)
        {
            if (string.IsNullOrWhiteSpace(columnsJson))
            {
                return new List<MixTableColumnDefinition>();
            }

            List<MixTableColumnDefinition> list =
                JsonConvert.DeserializeObject<List<MixTableColumnDefinition>>(columnsJson);
            return list ?? new List<MixTableColumnDefinition>();
        }

        public static IDictionary<string, object> ParseRowValues(object values)
        {
            var map = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);
            if (values == null)
            {
                return map;
            }

            if (values is IDictionary<string, object> dict)
            {
                foreach (KeyValuePair<string, object> pair in dict)
                {
                    map[pair.Key] = pair.Value;
                }

                return map;
            }

            if (values is DataRow dataRow)
            {
                foreach (DataColumn col in dataRow.Table.Columns)
                {
                    map[col.ColumnName] = dataRow[col];
                }

                return map;
            }

            if (values is string json && json.TrimStart().StartsWith("{", StringComparison.Ordinal))
            {
                Dictionary<string, object> parsed =
                    JsonConvert.DeserializeObject<Dictionary<string, object>>(json);
                if (parsed != null)
                {
                    foreach (KeyValuePair<string, object> pair in parsed)
                    {
                        map[pair.Key] = pair.Value;
                    }
                }

                return map;
            }

            // Fallback: ignore unknown shapes.
            return map;
        }

        private static void EnsureColumnsMatchSchema(DataGridView grid, List<MixTableColumnDefinition> schema)
        {
            if (grid.Columns.Count == schema.Count)
            {
                bool ok = true;
                for (int i = 0; i < schema.Count; i++)
                {
                    if (!string.Equals(grid.Columns[i].Name, schema[i].Name, StringComparison.OrdinalIgnoreCase))
                    {
                        ok = false;
                        break;
                    }
                }

                if (ok)
                {
                    return;
                }
            }

            ApplyColumns(grid, schema);
        }

        private static DataTable BuildBoundTable(List<MixTableColumnDefinition> schema, DataTable source)
        {
            var table = new DataTable();
            foreach (MixTableColumnDefinition col in schema)
            {
                Type dataType = ColumnClrType(col.Type);
                table.Columns.Add(col.Name, dataType);
            }

            if (source == null)
            {
                return table;
            }

            foreach (DataRow src in source.Rows)
            {
                DataRow row = table.NewRow();
                foreach (MixTableColumnDefinition col in schema)
                {
                    object raw = null;
                    if (source.Columns.Contains(col.Name))
                    {
                        raw = src[col.Name];
                    }

                    row[col.Name] = CoerceCellValue(col, raw);
                }

                table.Rows.Add(row);
            }

            return table;
        }

        private static DataGridViewColumn CreateColumn(MixTableColumnDefinition def)
        {
            string kind = MixTableColumnType.Require(def.Type);
            string header = string.IsNullOrWhiteSpace(def.Text) ? def.Name : def.Text;
            DataGridViewColumn column;

            switch (kind)
            {
                case MixTableColumnType.TextBox:
                    column = new DataGridViewTextBoxColumn
                    {
                        ReadOnly = def.ReadOnly == true
                    };
                    break;
                case MixTableColumnType.ComboBox:
                    {
                        var combo = new DataGridViewComboBoxColumn
                        {
                            FlatStyle = FlatStyle.Flat,
                            DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton
                        };
                        if (def.Items != null)
                        {
                            foreach (string item in def.Items)
                            {
                                combo.Items.Add(item ?? string.Empty);
                            }
                        }

                        column = combo;
                        break;
                    }
                case MixTableColumnType.CheckBox:
                    column = new DataGridViewCheckBoxColumn
                    {
                        TrueValue = true,
                        FalseValue = false
                    };
                    break;
                case MixTableColumnType.Button:
                    column = new DataGridViewButtonColumn
                    {
                        Text = string.IsNullOrWhiteSpace(def.Text) ? "-" : def.Text,
                        // Prefer per-row cell value; CoerceCellValue fills def.Text when empty.
                        UseColumnTextForButtonValue = false,
                        FlatStyle = FlatStyle.Standard
                    };
                    break;
                case MixTableColumnType.Link:
                    column = new DataGridViewLinkColumn
                    {
                        TrackVisitedState = false,
                        LinkBehavior = LinkBehavior.HoverUnderline
                    };
                    break;
                case MixTableColumnType.Label:
                default:
                    column = new DataGridViewTextBoxColumn
                    {
                        ReadOnly = def.ReadOnly != false
                    };
                    break;
            }

            column.Name = def.Name;
            column.DataPropertyName = def.Name;
            column.Tag = def;
            column.MinimumWidth = 40;
            // NotSortable alone is not enough — stock header still reserves sort-glyph gutter,
            // which makes MiddleCenter look slightly left. Custom header paints full width.
            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            column.HeaderCell = new MixTableColumnHeaderCell();
            column.HeaderText = header;
            ApplyHeaderAlign(column, def);
            MixTableStyle.ApplyColumnDefinitionStyle(column, def);
            if (def.Width.HasValue && def.Width.Value > 0)
            {
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.None;
                column.Width = def.Width.Value;
            }
            else
            {
                // No width → autofit to header + cell content; excess total width scrolls horizontally.
                column.AutoSizeMode = DataGridViewAutoSizeColumnMode.AllCells;
            }

            return column;
        }

        private static void RefreshAutofitColumns(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            foreach (DataGridViewColumn col in grid.Columns)
            {
                if (col.AutoSizeMode == DataGridViewAutoSizeColumnMode.AllCells
                    || col.AutoSizeMode == DataGridViewAutoSizeColumnMode.AllCellsExceptHeader
                    || col.AutoSizeMode == DataGridViewAutoSizeColumnMode.DisplayedCells)
                {
                    grid.AutoResizeColumn(col.Index, col.AutoSizeMode);
                }
            }
        }

        private static Type ColumnClrType(string mixType)
        {
            string kind = MixTableColumnType.Require(mixType);
            return kind == MixTableColumnType.CheckBox ? typeof(bool) : typeof(string);
        }

        private static object CoerceCellValue(MixTableColumnDefinition def, object value)
        {
            string kind = MixTableColumnType.Require(def == null ? null : def.Type);
            if (kind == MixTableColumnType.CheckBox)
            {
                if (value == null || value == DBNull.Value)
                {
                    return false;
                }

                if (value is bool b)
                {
                    return b;
                }

                string s = Convert.ToString(value);
                return string.Equals(s, "true", StringComparison.OrdinalIgnoreCase)
                    || s == "1"
                    || string.Equals(s, "yes", StringComparison.OrdinalIgnoreCase)
                    || string.Equals(s, "y", StringComparison.OrdinalIgnoreCase);
            }

            if (value == null || value == DBNull.Value)
            {
                if (kind == MixTableColumnType.Button || kind == MixTableColumnType.Link)
                {
                    return def != null && !string.IsNullOrWhiteSpace(def.Text) ? def.Text : string.Empty;
                }

                return string.Empty;
            }

            return Convert.ToString(value) ?? string.Empty;
        }

        private static DataGridViewColumn ResolveColumn(DataGridView grid, string column)
        {
            if (grid == null)
            {
                throw new ArgumentNullException(nameof(grid));
            }

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

        private static object ExtendedPropertiesGet(this DataGridView grid, string key)
        {
            return grid.Tag is MixTableHostTag host && host.Bag.TryGetValue(key, out object value)
                ? value
                : null;
        }

        private static void ExtendedPropertiesSet(this DataGridView grid, string key, object value)
        {
            MixTableHostTag host = grid.Tag as MixTableHostTag;
            if (host == null)
            {
                host = new MixTableHostTag();
                grid.Tag = host;
            }

            host.Bag[key] = value;
        }

        private sealed class MixTableHostTag
        {
            public string TypeName { get; } = FormControlType.MixTable;
            public Dictionary<string, object> Bag { get; } = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase);

            public override string ToString()
            {
                return TypeName;
            }
        }

        public static bool IsMixTableGrid(Control control)
        {
            if (!(control is DataGridView grid))
            {
                return false;
            }

            if (grid.Tag is MixTableHostTag)
            {
                return true;
            }

            string tag = Convert.ToString(grid.Tag);
            return string.Equals(tag, FormControlType.MixTable, StringComparison.OrdinalIgnoreCase);
        }

        internal static void ApplyHeaderAlign(DataGridViewColumn column, MixTableColumnDefinition def)
        {
            if (column == null)
            {
                return;
            }

            column.SortMode = DataGridViewColumnSortMode.NotSortable;
            if (!(column.HeaderCell is MixTableColumnHeaderCell))
            {
                string text = column.HeaderText;
                column.HeaderCell = new MixTableColumnHeaderCell();
                column.HeaderText = text;
            }

            column.HeaderCell.Style.Alignment = MixTableAlign.Parse(
                def == null ? null : def.HeaderAlign,
                DataGridViewContentAlignment.MiddleCenter);
            column.HeaderCell.Style.WrapMode = DataGridViewTriState.False;
            column.HeaderCell.Style.Padding = Padding.Empty;
        }

        /// <summary>Restore headerAlign after grid chrome styles are applied.</summary>
        internal static void ReapplyHeaderAlignments(DataGridView grid)
        {
            if (grid == null)
            {
                return;
            }

            grid.ColumnHeadersDefaultCellStyle.Alignment = DataGridViewContentAlignment.MiddleCenter;
            grid.ColumnHeadersDefaultCellStyle.WrapMode = DataGridViewTriState.False;
            grid.ColumnHeadersDefaultCellStyle.Padding = Padding.Empty;

            foreach (DataGridViewColumn column in grid.Columns)
            {
                ApplyHeaderAlign(column, column.Tag as MixTableColumnDefinition);
            }
        }

        /// <summary>
        /// Header cell that paints title across the full width (no sort-glyph gutter),
        /// so middle-center / right-center align optically correctly.
        /// </summary>
        private sealed class MixTableColumnHeaderCell : DataGridViewColumnHeaderCell
        {
            protected override void Paint(
                Graphics graphics,
                Rectangle clipBounds,
                Rectangle cellBounds,
                int rowIndex,
                DataGridViewElementStates dataGridViewElementState,
                object value,
                object formattedValue,
                string errorText,
                DataGridViewCellStyle cellStyle,
                DataGridViewAdvancedBorderStyle advancedBorderStyle,
                DataGridViewPaintParts paintParts)
            {
                base.Paint(
                    graphics,
                    clipBounds,
                    cellBounds,
                    rowIndex,
                    dataGridViewElementState,
                    value,
                    formattedValue,
                    errorText,
                    cellStyle,
                    advancedBorderStyle,
                    paintParts & ~DataGridViewPaintParts.ContentForeground);

                if ((paintParts & DataGridViewPaintParts.ContentForeground) == 0)
                {
                    return;
                }

                string text = formattedValue as string ?? Convert.ToString(formattedValue) ?? string.Empty;
                if (text.Length == 0)
                {
                    return;
                }

                DataGridViewContentAlignment align = cellStyle == null
                    ? DataGridViewContentAlignment.MiddleCenter
                    : cellStyle.Alignment;
                if (align == DataGridViewContentAlignment.NotSet)
                {
                    align = DataGridViewContentAlignment.MiddleCenter;
                }

                Font font = cellStyle != null && cellStyle.Font != null
                    ? cellStyle.Font
                    : SystemFonts.DefaultFont;
                Color fore = cellStyle != null && !cellStyle.ForeColor.IsEmpty
                    ? cellStyle.ForeColor
                    : SystemColors.ControlText;

                // Full header width minus a thin border inset — no asymmetric sort gutter.
                Rectangle textBounds = new Rectangle(
                    cellBounds.X + 2,
                    cellBounds.Y + 1,
                    Math.Max(0, cellBounds.Width - 4),
                    Math.Max(0, cellBounds.Height - 2));

                TextRenderer.DrawText(
                    graphics,
                    text,
                    font,
                    textBounds,
                    fore,
                    MixTableAlign.ToTextFormatFlags(align));
            }
        }
    }
}
