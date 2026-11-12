using System;
using System.Collections.Generic;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using F2B.Forms.Model;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// Standalone UI to edit MixTable Initial Rows Json against a fixed Columns schema.
    /// ComboBox columns: main grid edits selected value; double-click / … edits per-row items.
    /// </summary>
    public sealed class MixTableInitialRowsGeneratorForm : Form
    {
        private const int ComboEllipsisWidth = 22;

        private readonly List<MixTableColumnDefinition> _schema;
        private readonly DataTable _table;
        /// <summary>Parallel to table rows: column name → non-empty items override (null dict = none).</summary>
        private readonly List<Dictionary<string, List<string>>> _comboOverrides =
            new List<Dictionary<string, List<string>>>();
        private readonly DataGridView _grid;
        private readonly TextBox _preview;
        private readonly Label _status;

        public string GeneratedJson { get; private set; }

        public static bool TryValidateInitialRowsJson(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                JToken token = JToken.Parse(json);
                if (token == null || token.Type != JTokenType.Array)
                {
                    error = "Initial Rows Json must be a JSON array of row objects.";
                    return false;
                }

                foreach (JToken item in token)
                {
                    if (item == null || item.Type == JTokenType.Null)
                    {
                        continue;
                    }

                    if (item.Type != JTokenType.Object)
                    {
                        error = "Each initial row must be a JSON object keyed by column name.";
                        return false;
                    }
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public static bool TryGetRequiredColumns(
            string columnsJson,
            out List<MixTableColumnDefinition> columns,
            out string error)
        {
            columns = null;
            error = null;

            if (string.IsNullOrWhiteSpace(columnsJson))
            {
                error = "Columns Json is empty. Design at least one column in Columns Json first.";
                return false;
            }

            string columnsError;
            if (!MixTableColumnsGeneratorForm.TryValidateColumnsJson(columnsJson, out columnsError))
            {
                error = "Columns Json is invalid. Fix Columns Json first.\r\n\r\n" + columnsError;
                return false;
            }

            List<MixTableColumnDefinition> parsed;
            try
            {
                parsed = JsonConvert.DeserializeObject<List<MixTableColumnDefinition>>(columnsJson)
                    ?? new List<MixTableColumnDefinition>();
            }
            catch (Exception ex)
            {
                error = "Columns Json is invalid. Fix Columns Json first.\r\n\r\n" + ex.Message;
                return false;
            }

            var valid = new List<MixTableColumnDefinition>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (MixTableColumnDefinition def in parsed)
            {
                if (def == null || string.IsNullOrWhiteSpace(def.Name))
                {
                    continue;
                }

                string name = def.Name.Trim();
                if (!names.Add(name))
                {
                    continue;
                }

                string type = string.IsNullOrWhiteSpace(def.Type) ? MixTableColumnType.Label : def.Type.Trim();
                if (!MixTableColumnType.IsKnown(type))
                {
                    error = "Columns Json has invalid type on column '" + name
                        + "'. Fix Columns Json first.";
                    return false;
                }

                valid.Add(new MixTableColumnDefinition
                {
                    Name = name,
                    Type = type,
                    Text = def.Text,
                    Items = def.Items == null ? null : new List<string>(def.Items),
                    Width = def.Width,
                    ReadOnly = def.ReadOnly,
                    HeaderAlign = def.HeaderAlign
                });
            }

            if (valid.Count == 0)
            {
                error = "Columns Json has no valid column. Design at least one column "
                    + "(with a non-empty name) in Columns Json first.";
                return false;
            }

            columns = valid;
            return true;
        }

        public MixTableInitialRowsGeneratorForm(
            IList<MixTableColumnDefinition> schema,
            string existingInitialRowsJson = null)
        {
            if (schema == null || schema.Count == 0)
            {
                throw new ArgumentException("schema must contain at least one column.", nameof(schema));
            }

            _schema = schema.ToList();
            _table = BuildTable(_schema);

            Text = "MixTable Initial Rows Json Generator";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Size = new Size(980, 620);
            MinimumSize = new Size(760, 480);
            Font = new Font("Microsoft YaHei UI", 9f);

            LoadRows(existingInitialRowsJson);

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 6, 8, 4)
            };
            var btnAdd = MakeButton("Add Row", OnAdd);
            var btnRemove = MakeButton("Remove Row", OnRemove);
            var btnUp = MakeButton("Move Up", OnMoveUp);
            var btnDown = MakeButton("Move Down", OnMoveDown);
            var btnEditItems = MakeButton("Edit Combo Items…", OnEditComboItems);
            var btnRefresh = MakeButton("Refresh Preview", (s, e) => RefreshPreview(commitEdit: true));
            int x = 8;
            foreach (Button b in new[] { btnAdd, btnRemove, btnUp, btnDown, btnEditItems, btnRefresh })
            {
                b.Location = new Point(x, 6);
                top.Controls.Add(b);
                x += b.Width + 6;
            }

            _grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                AutoGenerateColumns = false,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                RowHeadersVisible = true,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.DisplayedCells,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            BuildGridColumns();
            // Column.Items must include row overrides / current values BEFORE bind,
            // otherwise DataGridViewComboBox shows blank for unknown values.
            SyncAllComboColumnItems();
            _grid.DataSource = _table;
            // Do NOT CommitEdit on every keystroke for text cells — that restarts the editor
            // and SelectAll, so each new character overwrites the previous input.
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty && _grid.CurrentCell is DataGridViewCheckBoxCell)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };
            _grid.CellValueChanged += (s, e) =>
            {
                if (e.RowIndex >= 0
                    && e.ColumnIndex >= 0
                    && _grid.Columns[e.ColumnIndex] is DataGridViewCheckBoxColumn)
                {
                    RefreshPreview(commitEdit: false);
                }
            };
            _grid.CellEndEdit += (s, e) =>
            {
                if (e.ColumnIndex >= 0 && e.ColumnIndex < _grid.Columns.Count)
                {
                    MixTableColumnDefinition edited = FindSchemaColumn(_grid.Columns[e.ColumnIndex].Name);
                    if (edited != null
                        && string.Equals(edited.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
                    {
                        SyncComboColumnItems(edited);
                    }
                }

                RefreshPreview(commitEdit: false);
            };
            _grid.DataError += (s, e) => { e.ThrowException = false; };
            _grid.EditingControlShowing += OnEditingControlShowing;
            _grid.CellPainting += OnComboCellPainting;
            _grid.CellMouseClick += OnComboCellMouseClick;
            _grid.CellDoubleClick += OnComboCellDoubleClick;
            _grid.CellToolTipTextNeeded += OnCellToolTipTextNeeded;
            _grid.CellFormatting += OnCellFormatting;

            _preview = new TextBox
            {
                Dock = DockStyle.Fill,
                Multiline = true,
                ScrollBars = ScrollBars.Both,
                Font = new Font("Consolas", 9.5f),
                WordWrap = false,
                ReadOnly = true
            };

            var previewHost = new Panel { Dock = DockStyle.Bottom, Height = 200, Padding = new Padding(8) };
            var previewLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = 36,
                Text = "Generated Initial Rows Json (read-only preview)\r\n"
                    + "ComboBox: edit value in grid; click … / double-click / Edit Combo Items… for per-row options."
            };
            previewHost.Controls.Add(_preview);
            previewHost.Controls.Add(previewLabel);

            _status = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 22,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0)
            };

            var bottom = new Panel
            {
                Dock = DockStyle.Bottom,
                Height = 48,
                Padding = new Padding(8)
            };
            var btnOk = MakeButton("OK — Apply Json", OnOk);
            btnOk.Width = 140;
            var btnCancel = MakeButton("Cancel", (s, e) =>
            {
                DialogResult = DialogResult.Cancel;
                Close();
            });
            btnCancel.Width = 90;
            btnOk.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            btnCancel.Anchor = AnchorStyles.Right | AnchorStyles.Top;
            bottom.Controls.Add(btnCancel);
            bottom.Controls.Add(btnOk);
            bottom.Resize += (s, e) =>
            {
                btnCancel.Location = new Point(bottom.ClientSize.Width - btnCancel.Width - 12, 10);
                btnOk.Location = new Point(btnCancel.Left - btnOk.Width - 8, 10);
            };

            var center = new Panel { Dock = DockStyle.Fill, Padding = new Padding(8, 0, 8, 0) };
            center.Controls.Add(_grid);

            Controls.Add(center);
            Controls.Add(previewHost);
            Controls.Add(_status);
            Controls.Add(bottom);
            Controls.Add(top);

            AcceptButton = btnOk;
            CancelButton = btnCancel;

            Shown += (s, e) =>
            {
                bottom.PerformLayout();
                btnCancel.Location = new Point(bottom.ClientSize.Width - btnCancel.Width - 12, 10);
                btnOk.Location = new Point(btnCancel.Left - btnOk.Width - 8, 10);
                SyncAllComboColumnItems();
                RefreshPreview(commitEdit: false);
            };
        }

        /// <summary>
        /// DataGridViewComboBox only displays values present in column.Items.
        /// Merge column defaults + all row overrides + current cell values so custom options show.
        /// </summary>
        private void SyncAllComboColumnItems()
        {
            foreach (MixTableColumnDefinition def in _schema)
            {
                if (string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
                {
                    SyncComboColumnItems(def);
                }
            }
        }

        private void SyncComboColumnItems(MixTableColumnDefinition def)
        {
            if (_grid == null || def == null)
            {
                return;
            }

            DataGridViewComboBoxColumn column = _grid.Columns[def.Name] as DataGridViewComboBoxColumn;
            if (column == null)
            {
                return;
            }

            var items = new List<string>();
            var seen = new HashSet<string>(StringComparer.Ordinal);
            Action<string> add = text =>
            {
                string value = text ?? string.Empty;
                if (seen.Add(value))
                {
                    items.Add(value);
                }
            };

            add(string.Empty);
            foreach (string item in GetColumnDefaultItems(def))
            {
                add(item);
            }

            for (int r = 0; r < _table.Rows.Count; r++)
            {
                List<string> rowItems = GetComboOverride(r, def.Name);
                if (rowItems != null)
                {
                    foreach (string item in rowItems)
                    {
                        add(item);
                    }
                }

                object raw = _table.Rows[r][def.Name];
                if (raw != null && raw != DBNull.Value)
                {
                    add(Convert.ToString(raw));
                }
            }

            column.Items.Clear();
            foreach (string item in items)
            {
                column.Items.Add(item);
            }
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Clear();
            foreach (MixTableColumnDefinition def in _schema)
            {
                string header = string.IsNullOrWhiteSpace(def.Text) ? def.Name : def.Text.Trim();
                header = header + " (" + def.Type + ")";

                if (string.Equals(def.Type, MixTableColumnType.CheckBox, StringComparison.Ordinal))
                {
                    _grid.Columns.Add(new DataGridViewCheckBoxColumn
                    {
                        Name = def.Name,
                        DataPropertyName = def.Name,
                        HeaderText = header,
                        SortMode = DataGridViewColumnSortMode.NotSortable
                    });
                    continue;
                }

                if (string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
                {
                    var combo = new DataGridViewComboBoxColumn
                    {
                        Name = def.Name,
                        DataPropertyName = def.Name,
                        HeaderText = header,
                        FlatStyle = FlatStyle.Flat,
                        DisplayStyle = DataGridViewComboBoxDisplayStyle.DropDownButton,
                        SortMode = DataGridViewColumnSortMode.NotSortable,
                        // Items filled per-row in EditingControlShowing; keep a seed for display.
                        DisplayStyleForCurrentCellOnly = true
                    };
                    foreach (string item in GetColumnDefaultItems(def))
                    {
                        combo.Items.Add(item);
                    }

                    if (!combo.Items.Contains(string.Empty))
                    {
                        combo.Items.Insert(0, string.Empty);
                    }

                    _grid.Columns.Add(combo);
                    continue;
                }

                _grid.Columns.Add(new DataGridViewTextBoxColumn
                {
                    Name = def.Name,
                    DataPropertyName = def.Name,
                    HeaderText = header,
                    SortMode = DataGridViewColumnSortMode.NotSortable
                });
            }
        }

        private static DataTable BuildTable(IList<MixTableColumnDefinition> schema)
        {
            var table = new DataTable();
            foreach (MixTableColumnDefinition def in schema)
            {
                Type dataType = string.Equals(def.Type, MixTableColumnType.CheckBox, StringComparison.Ordinal)
                    ? typeof(bool)
                    : typeof(string);
                table.Columns.Add(def.Name, dataType);
            }

            return table;
        }

        private static Button MakeButton(string text, EventHandler onClick)
        {
            var button = new Button
            {
                Text = text,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                Padding = new Padding(8, 2, 8, 2)
            };
            button.Click += onClick;
            return button;
        }

        private static List<string> GetColumnDefaultItems(MixTableColumnDefinition def)
        {
            return MixTableComboCellValue.NormalizeItems(def == null ? null : def.Items);
        }

        private List<string> GetEffectiveItems(int rowIndex, MixTableColumnDefinition def)
        {
            List<string> overrideItems = GetComboOverride(rowIndex, def.Name);
            return MixTableComboCellValue.ResolveEffectiveItems(
                def,
                overrideItems == null
                    ? null
                    : new MixTableComboCellValue { ItemsOverride = overrideItems });
        }

        private List<string> GetComboOverride(int rowIndex, string columnName)
        {
            if (rowIndex < 0 || rowIndex >= _comboOverrides.Count)
            {
                return null;
            }

            Dictionary<string, List<string>> map = _comboOverrides[rowIndex];
            if (map == null)
            {
                return null;
            }

            List<string> items;
            if (!map.TryGetValue(columnName, out items))
            {
                return null;
            }

            return items == null || items.Count == 0 ? null : new List<string>(items);
        }

        private void SetComboOverride(int rowIndex, string columnName, List<string> items)
        {
            while (_comboOverrides.Count <= rowIndex)
            {
                _comboOverrides.Add(null);
            }

            List<string> normalized = MixTableComboCellValue.NormalizeItems(items);
            if (normalized.Count == 0)
            {
                Dictionary<string, List<string>> existing = _comboOverrides[rowIndex];
                if (existing != null)
                {
                    existing.Remove(columnName);
                    if (existing.Count == 0)
                    {
                        _comboOverrides[rowIndex] = null;
                    }
                }

                return;
            }

            if (_comboOverrides[rowIndex] == null)
            {
                _comboOverrides[rowIndex] = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
            }

            _comboOverrides[rowIndex][columnName] = normalized;
        }

        private void LoadRows(string json)
        {
            _table.Rows.Clear();
            _comboOverrides.Clear();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            List<Dictionary<string, object>> rows =
                JsonConvert.DeserializeObject<List<Dictionary<string, object>>>(json)
                ?? new List<Dictionary<string, object>>();

            foreach (Dictionary<string, object> source in rows)
            {
                if (source == null)
                {
                    continue;
                }

                DataRow row = _table.NewRow();
                var overrides = new Dictionary<string, List<string>>(StringComparer.OrdinalIgnoreCase);
                foreach (MixTableColumnDefinition def in _schema)
                {
                    object raw = FindValue(source, def.Name);
                    if (string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
                    {
                        MixTableComboCellValue parsed = MixTableComboCellValue.Parse(raw);
                        row[def.Name] = parsed.Value ?? string.Empty;
                        if (parsed.HasItemsOverride)
                        {
                            overrides[def.Name] = new List<string>(parsed.ItemsOverride);
                        }
                    }
                    else
                    {
                        row[def.Name] = CoerceForColumn(def, raw);
                    }
                }

                _table.Rows.Add(row);
                _comboOverrides.Add(overrides.Count == 0 ? null : overrides);
            }
        }

        private static object FindValue(Dictionary<string, object> source, string columnName)
        {
            if (source.TryGetValue(columnName, out object exact))
            {
                return exact;
            }

            foreach (KeyValuePair<string, object> pair in source)
            {
                if (string.Equals(pair.Key, columnName, StringComparison.OrdinalIgnoreCase))
                {
                    return pair.Value;
                }
            }

            return null;
        }

        private static object CoerceForColumn(MixTableColumnDefinition def, object raw)
        {
            if (string.Equals(def.Type, MixTableColumnType.CheckBox, StringComparison.Ordinal))
            {
                if (raw == null || raw == DBNull.Value)
                {
                    return false;
                }

                if (raw is bool b)
                {
                    return b;
                }

                if (raw is JValue jValue)
                {
                    raw = jValue.Value;
                }

                bool parsed;
                if (bool.TryParse(Convert.ToString(raw), out parsed))
                {
                    return parsed;
                }

                return false;
            }

            if (raw == null || raw == DBNull.Value)
            {
                return string.Empty;
            }

            if (raw is JValue jv)
            {
                return jv.Value == null ? string.Empty : Convert.ToString(jv.Value) ?? string.Empty;
            }

            return Convert.ToString(raw) ?? string.Empty;
        }

        private void OnAdd(object sender, EventArgs e)
        {
            DataRow row = _table.NewRow();
            foreach (MixTableColumnDefinition def in _schema)
            {
                if (string.Equals(def.Type, MixTableColumnType.CheckBox, StringComparison.Ordinal))
                {
                    row[def.Name] = false;
                }
                else if (string.Equals(def.Type, MixTableColumnType.Button, StringComparison.Ordinal)
                    || string.Equals(def.Type, MixTableColumnType.Link, StringComparison.Ordinal))
                {
                    row[def.Name] = string.IsNullOrWhiteSpace(def.Text) ? string.Empty : def.Text.Trim();
                }
                else
                {
                    row[def.Name] = string.Empty;
                }
            }

            _table.Rows.Add(row);
            _comboOverrides.Add(null);
            int index = _table.Rows.Count - 1;
            if (index >= 0 && index < _grid.Rows.Count)
            {
                _grid.ClearSelection();
                _grid.Rows[index].Selected = true;
                _grid.CurrentCell = _grid.Rows[index].Cells[0];
            }

            RefreshPreview();
        }

        private void OnRemove(object sender, EventArgs e)
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index < 0
                || _grid.CurrentRow.Index >= _table.Rows.Count)
            {
                return;
            }

            int index = _grid.CurrentRow.Index;
            _table.Rows.RemoveAt(index);
            if (index < _comboOverrides.Count)
            {
                _comboOverrides.RemoveAt(index);
            }

            RefreshPreview();
        }

        private void OnMoveUp(object sender, EventArgs e)
        {
            int i = _grid.CurrentRow == null ? -1 : _grid.CurrentRow.Index;
            if (i <= 0 || i >= _table.Rows.Count)
            {
                return;
            }

            SwapRows(i, i - 1);
            _grid.ClearSelection();
            _grid.Rows[i - 1].Selected = true;
            _grid.CurrentCell = _grid.Rows[i - 1].Cells[0];
            RefreshPreview();
        }

        private void OnMoveDown(object sender, EventArgs e)
        {
            int i = _grid.CurrentRow == null ? -1 : _grid.CurrentRow.Index;
            if (i < 0 || i >= _table.Rows.Count - 1)
            {
                return;
            }

            SwapRows(i, i + 1);
            _grid.ClearSelection();
            _grid.Rows[i + 1].Selected = true;
            _grid.CurrentCell = _grid.Rows[i + 1].Cells[0];
            RefreshPreview();
        }

        private void SwapRows(int a, int b)
        {
            object[] valuesA = _table.Rows[a].ItemArray;
            object[] valuesB = _table.Rows[b].ItemArray;
            _table.Rows[a].ItemArray = valuesB;
            _table.Rows[b].ItemArray = valuesA;

            while (_comboOverrides.Count <= Math.Max(a, b))
            {
                _comboOverrides.Add(null);
            }

            Dictionary<string, List<string>> tmp = _comboOverrides[a];
            _comboOverrides[a] = _comboOverrides[b];
            _comboOverrides[b] = tmp;
        }

        private void OnEditComboItems(object sender, EventArgs e)
        {
            if (_grid.CurrentCell == null)
            {
                MessageBox.Show(
                    this,
                    "Select a ComboBox cell first.",
                    Text,
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            TryOpenComboItemsEditor(_grid.CurrentCell.RowIndex, _grid.CurrentCell.ColumnIndex);
        }

        private void OnComboCellDoubleClick(object sender, DataGridViewCellEventArgs e)
        {
            TryOpenComboItemsEditor(e.RowIndex, e.ColumnIndex);
        }

        private void OnComboCellMouseClick(object sender, DataGridViewCellMouseEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0 || e.Button != MouseButtons.Left)
            {
                return;
            }

            MixTableColumnDefinition def = FindSchemaColumn(_grid.Columns[e.ColumnIndex].Name);
            if (def == null || !string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                return;
            }

            // e.Location is relative to the cell.
            Rectangle cellBounds = _grid.GetCellDisplayRectangle(e.ColumnIndex, e.RowIndex, false);
            if (e.X >= Math.Max(0, cellBounds.Width - ComboEllipsisWidth))
            {
                TryOpenComboItemsEditor(e.RowIndex, e.ColumnIndex);
            }
        }

        private void OnComboCellPainting(object sender, DataGridViewCellPaintingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            MixTableColumnDefinition def = FindSchemaColumn(_grid.Columns[e.ColumnIndex].Name);
            if (def == null || !string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                return;
            }

            e.Paint(e.ClipBounds, DataGridViewPaintParts.All & ~DataGridViewPaintParts.ErrorIcon);
            var btn = new Rectangle(
                e.CellBounds.Right - ComboEllipsisWidth - 1,
                e.CellBounds.Y + 1,
                ComboEllipsisWidth,
                e.CellBounds.Height - 2);
            ButtonRenderer.DrawButton(
                e.Graphics,
                btn,
                "…",
                e.CellStyle.Font,
                false,
                System.Windows.Forms.VisualStyles.PushButtonState.Normal);
            e.Handled = true;
        }

        private void OnCellToolTipTextNeeded(object sender, DataGridViewCellToolTipTextNeededEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            MixTableColumnDefinition def = FindSchemaColumn(_grid.Columns[e.ColumnIndex].Name);
            if (def == null || !string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                return;
            }

            if (GetComboOverride(e.RowIndex, def.Name) != null)
            {
                e.ToolTipText = "Custom items (row override). Click … or double-click to edit.";
            }
            else
            {
                e.ToolTipText = "Using column default items. Click … or double-click to override.";
            }
        }

        private void OnCellFormatting(object sender, DataGridViewCellFormattingEventArgs e)
        {
            if (e.RowIndex < 0 || e.ColumnIndex < 0)
            {
                return;
            }

            MixTableColumnDefinition def = FindSchemaColumn(_grid.Columns[e.ColumnIndex].Name);
            if (def == null || !string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                return;
            }

            if (GetComboOverride(e.RowIndex, def.Name) != null)
            {
                e.CellStyle.BackColor = Color.FromArgb(255, 250, 230);
            }
        }

        private void OnEditingControlShowing(object sender, DataGridViewEditingControlShowingEventArgs e)
        {
            ComboBox combo = e.Control as ComboBox;
            if (combo == null || _grid.CurrentCell == null)
            {
                return;
            }

            MixTableColumnDefinition def = FindSchemaColumn(_grid.Columns[_grid.CurrentCell.ColumnIndex].Name);
            if (def == null || !string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                return;
            }

            string current = Convert.ToString(_grid.CurrentCell.Value) ?? string.Empty;
            List<string> items = GetEffectiveItems(_grid.CurrentCell.RowIndex, def);
            combo.Items.Clear();
            combo.Items.Add(string.Empty);
            foreach (string item in items)
            {
                if (!string.Equals(item, string.Empty, StringComparison.Ordinal)
                    && !combo.Items.Contains(item))
                {
                    combo.Items.Add(item);
                }
            }

            if (!string.IsNullOrEmpty(current) && !combo.Items.Contains(current))
            {
                combo.Items.Add(current);
            }

            combo.SelectedItem = current;
            combo.DropDownStyle = ComboBoxStyle.DropDownList;
        }

        private bool TryOpenComboItemsEditor(int rowIndex, int columnIndex)
        {
            if (rowIndex < 0 || columnIndex < 0
                || rowIndex >= _table.Rows.Count
                || columnIndex >= _grid.Columns.Count)
            {
                return false;
            }

            MixTableColumnDefinition def = FindSchemaColumn(_grid.Columns[columnIndex].Name);
            if (def == null || !string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
            {
                return false;
            }

            EndEdit();
            string currentValue = _table.Rows[rowIndex][def.Name] == DBNull.Value
                ? string.Empty
                : Convert.ToString(_table.Rows[rowIndex][def.Name]) ?? string.Empty;
            List<string> columnDefaults = GetColumnDefaultItems(def);
            List<string> currentOverride = GetComboOverride(rowIndex, def.Name);

            using (var dialog = new ComboRowItemsDialog(
                def.Name,
                columnDefaults,
                currentOverride,
                currentValue))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return true;
                }

                if (dialog.UseColumnDefault)
                {
                    SetComboOverride(rowIndex, def.Name, null);
                }
                else
                {
                    SetComboOverride(rowIndex, def.Name, dialog.Items);
                }

                string selected = dialog.SelectedValue ?? string.Empty;
                List<string> effective = GetEffectiveItems(rowIndex, def);
                if (!string.IsNullOrEmpty(selected)
                    && !effective.Any(i => string.Equals(i, selected, StringComparison.Ordinal)))
                {
                    selected = string.Empty;
                }

                // Must sync Items before assigning Value, or the Combo cell paints blank.
                SyncComboColumnItems(def);
                _table.Rows[rowIndex][def.Name] = selected;
                if (rowIndex < _grid.Rows.Count && columnIndex < _grid.Columns.Count)
                {
                    _grid.Rows[rowIndex].Cells[columnIndex].Value = selected;
                }

                _grid.InvalidateCell(columnIndex, rowIndex);
                RefreshPreview(commitEdit: false);
            }

            return true;
        }

        private MixTableColumnDefinition FindSchemaColumn(string name)
        {
            foreach (MixTableColumnDefinition def in _schema)
            {
                if (string.Equals(def.Name, name, StringComparison.OrdinalIgnoreCase))
                {
                    return def;
                }
            }

            return null;
        }

        private void OnOk(object sender, EventArgs e)
        {
            EndEdit();
            string error;
            string json = BuildJson(out error);
            if (error != null)
            {
                MessageBox.Show(this, error, Text, MessageBoxButtons.OK, MessageBoxIcon.Warning);
                return;
            }

            GeneratedJson = json;
            DialogResult = DialogResult.OK;
            Close();
        }

        private void RefreshPreview(bool commitEdit = true)
        {
            if (commitEdit)
            {
                EndEdit();
            }

            string error;
            string json = BuildJson(out error);
            _preview.Text = json ?? string.Empty;
            _status.Text = error ?? ("OK — " + _table.Rows.Count + " row(s), " + _schema.Count + " column(s)");
            _status.ForeColor = error == null ? Color.DarkGreen : Color.Firebrick;
        }

        private void EndEdit()
        {
            try
            {
                if (_grid.IsCurrentCellInEditMode)
                {
                    _grid.EndEdit();
                }

                if (_grid.BindingContext != null && _grid.BindingContext[_table] != null)
                {
                    _grid.BindingContext[_table].EndCurrentEdit();
                }
            }
            catch
            {
                // Preview/status will reflect validation issues if any.
            }
        }

        private string BuildJson(out string error)
        {
            error = null;
            var rows = new List<Dictionary<string, object>>();

            for (int i = 0; i < _table.Rows.Count; i++)
            {
                DataRow dataRow = _table.Rows[i];
                if (dataRow.RowState == DataRowState.Deleted)
                {
                    continue;
                }

                var dict = new Dictionary<string, object>(StringComparer.Ordinal);
                foreach (MixTableColumnDefinition def in _schema)
                {
                    object raw = dataRow[def.Name];
                    if (string.Equals(def.Type, MixTableColumnType.CheckBox, StringComparison.Ordinal))
                    {
                        dict[def.Name] = raw == null || raw == DBNull.Value
                            ? false
                            : Convert.ToBoolean(raw);
                        continue;
                    }

                    string text = raw == null || raw == DBNull.Value
                        ? string.Empty
                        : Convert.ToString(raw) ?? string.Empty;

                    if (string.Equals(def.Type, MixTableColumnType.ComboBox, StringComparison.Ordinal))
                    {
                        List<string> overrideItems = GetComboOverride(i, def.Name);
                        List<string> effective = GetEffectiveItems(i, def);
                        if (!string.IsNullOrEmpty(text)
                            && !effective.Any(item => string.Equals(item, text, StringComparison.Ordinal)))
                        {
                            error = "Row " + (i + 1) + ", column '" + def.Name
                                + "': value must be one of the effective ComboBox items (or empty).";
                            return FormatPartial(rows);
                        }

                        dict[def.Name] = MixTableComboCellValue.ToJsonPayload(text, overrideItems);
                        continue;
                    }

                    dict[def.Name] = text;
                }

                rows.Add(dict);
            }

            return JsonConvert.SerializeObject(rows, Formatting.Indented);
        }

        private static string FormatPartial(List<Dictionary<string, object>> rows)
        {
            return JsonConvert.SerializeObject(rows, Formatting.Indented);
        }

        /// <summary>Small dialog to edit per-row ComboBox items.</summary>
        private sealed class ComboRowItemsDialog : Form
        {
            private readonly TextBox _itemsBox;
            private readonly ComboBox _selected;
            private readonly List<string> _columnDefaults;

            public bool UseColumnDefault { get; private set; }
            public List<string> Items { get; private set; }
            public string SelectedValue { get; private set; }

            public ComboRowItemsDialog(
                string columnName,
                IList<string> columnDefaults,
                IList<string> currentOverride,
                string selectedValue)
            {
                _columnDefaults = MixTableComboCellValue.NormalizeItems(columnDefaults);
                Text = "Combo items — " + columnName;
                StartPosition = FormStartPosition.CenterParent;
                FormBorderStyle = FormBorderStyle.FixedDialog;
                MinimizeBox = false;
                MaximizeBox = false;
                ShowInTaskbar = false;
                ClientSize = new Size(420, 360);
                Font = new Font("Microsoft YaHei UI", 9f);

                var hint = new Label
                {
                    Dock = DockStyle.Top,
                    Height = 48,
                    Padding = new Padding(10, 8, 10, 0),
                    Text = "One option per line. Leave custom list empty and click Reset to use Columns Json items."
                };

                _itemsBox = new TextBox
                {
                    Dock = DockStyle.Fill,
                    Multiline = true,
                    ScrollBars = ScrollBars.Vertical,
                    AcceptsReturn = true,
                    Font = new Font("Consolas", 9.5f)
                };

                bool hasOverride = currentOverride != null && currentOverride.Count > 0;
                IList<string> seed = hasOverride ? currentOverride : _columnDefaults;
                _itemsBox.Text = string.Join(Environment.NewLine, seed ?? Array.Empty<string>());
                if (!hasOverride)
                {
                    // Show defaults for editing convenience; Reset still clears override.
                }

                var selectedHost = new Panel { Dock = DockStyle.Bottom, Height = 36, Padding = new Padding(10, 4, 10, 4) };
                selectedHost.Controls.Add(new Label
                {
                    Text = "Selected:",
                    AutoSize = true,
                    Location = new Point(10, 8)
                });
                _selected = new ComboBox
                {
                    DropDownStyle = ComboBoxStyle.DropDownList,
                    Location = new Point(80, 4),
                    Width = 320
                };
                selectedHost.Controls.Add(_selected);
                RefreshSelectedList(selectedValue);

                _itemsBox.TextChanged += (s, e) => RefreshSelectedList(_selected.SelectedItem as string ?? selectedValue);

                var buttons = new Panel { Dock = DockStyle.Bottom, Height = 44, Padding = new Padding(8) };
                var btnOk = new Button { Text = "OK", Width = 80, DialogResult = DialogResult.None };
                var btnCancel = new Button { Text = "Cancel", Width = 80, DialogResult = DialogResult.Cancel };
                var btnReset = new Button { Text = "Reset to column default", AutoSize = true };
                btnOk.Click += (s, e) =>
                {
                    Items = ParseItemsBox();
                    UseColumnDefault = false;
                    if (Items.Count == 0)
                    {
                        // Empty custom list → treat as reset to column default.
                        UseColumnDefault = true;
                        Items = null;
                    }

                    SelectedValue = _selected.SelectedItem as string ?? string.Empty;
                    DialogResult = DialogResult.OK;
                    Close();
                };
                btnReset.Click += (s, e) =>
                {
                    UseColumnDefault = true;
                    Items = null;
                    SelectedValue = _selected.SelectedItem as string ?? string.Empty;
                    List<string> effective = MixTableComboCellValue.NormalizeItems(_columnDefaults);
                    if (!string.IsNullOrEmpty(SelectedValue)
                        && !effective.Any(i => string.Equals(i, SelectedValue, StringComparison.Ordinal)))
                    {
                        SelectedValue = string.Empty;
                    }

                    DialogResult = DialogResult.OK;
                    Close();
                };

                buttons.Controls.Add(btnReset);
                buttons.Controls.Add(btnCancel);
                buttons.Controls.Add(btnOk);
                buttons.Resize += (s, e) =>
                {
                    btnCancel.Location = new Point(buttons.ClientSize.Width - btnCancel.Width - 12, 8);
                    btnOk.Location = new Point(btnCancel.Left - btnOk.Width - 8, 8);
                    btnReset.Location = new Point(10, 8);
                };

                var itemsHost = new Panel { Dock = DockStyle.Fill, Padding = new Padding(10, 0, 10, 0) };
                itemsHost.Controls.Add(_itemsBox);

                Controls.Add(itemsHost);
                Controls.Add(selectedHost);
                Controls.Add(buttons);
                Controls.Add(hint);
                CancelButton = btnCancel;
            }

            private void RefreshSelectedList(string preferred)
            {
                List<string> items = ParseItemsBox();
                if (items.Count == 0)
                {
                    items = new List<string>(_columnDefaults);
                }

                string keep = preferred ?? string.Empty;
                _selected.Items.Clear();
                _selected.Items.Add(string.Empty);
                foreach (string item in items)
                {
                    if (!string.IsNullOrEmpty(item) && !_selected.Items.Contains(item))
                    {
                        _selected.Items.Add(item);
                    }
                }

                if (!string.IsNullOrEmpty(keep) && !_selected.Items.Contains(keep))
                {
                    _selected.Items.Add(keep);
                }

                _selected.SelectedItem = _selected.Items.Contains(keep) ? keep : string.Empty;
            }

            private List<string> ParseItemsBox()
            {
                string[] lines = (_itemsBox.Text ?? string.Empty).Split(
                    new[] { "\r\n", "\n", "\r" },
                    StringSplitOptions.None);
                return MixTableComboCellValue.NormalizeItems(
                    lines.Select(l => l.Trim()).Where(l => l.Length > 0));
            }
        }
    }
}
