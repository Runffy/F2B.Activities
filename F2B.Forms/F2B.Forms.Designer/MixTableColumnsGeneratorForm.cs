using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using F2B.Forms.Model;
using Newtonsoft.Json;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// Standalone UI to add/edit MixTable columns and generate Columns Json.
    /// </summary>
    public sealed class MixTableColumnsGeneratorForm : Form
    {
        private readonly BindingList<ColumnEditRow> _rows = new BindingList<ColumnEditRow>();
        private readonly DataGridView _grid;
        private readonly TextBox _preview;
        private readonly Label _status;

        public string GeneratedJson { get; private set; }

        /// <summary>
        /// Empty / whitespace is valid (no columns). Malformed JSON or non-array payload is invalid.
        /// </summary>
        public static bool TryValidateColumnsJson(string json, out string error)
        {
            error = null;
            if (string.IsNullOrWhiteSpace(json))
            {
                return true;
            }

            try
            {
                List<MixTableColumnDefinition> list =
                    JsonConvert.DeserializeObject<List<MixTableColumnDefinition>>(json);
                if (list == null)
                {
                    error = "Columns Json must be a JSON array of column objects.";
                    return false;
                }

                return true;
            }
            catch (Exception ex)
            {
                error = ex.Message;
                return false;
            }
        }

        public MixTableColumnsGeneratorForm(string existingColumnsJson = null)
        {
            Text = "MixTable Columns Json Generator";
            StartPosition = FormStartPosition.CenterParent;
            MinimizeBox = false;
            MaximizeBox = true;
            ShowInTaskbar = false;
            FormBorderStyle = FormBorderStyle.Sizable;
            Size = new Size(980, 620);
            MinimumSize = new Size(760, 480);
            Font = new Font("Microsoft YaHei UI", 9f);

            // Caller should validate first; still load safely if already parsed OK.
            LoadRows(existingColumnsJson);

            var top = new Panel
            {
                Dock = DockStyle.Top,
                Height = 40,
                Padding = new Padding(8, 6, 8, 4)
            };
            var btnAdd = MakeButton("Add", OnAdd);
            var btnRemove = MakeButton("Remove", OnRemove);
            var btnUp = MakeButton("Move Up", OnMoveUp);
            var btnDown = MakeButton("Move Down", OnMoveDown);
            var btnRefresh = MakeButton("Refresh Preview", (s, e) => RefreshPreview());
            int x = 8;
            foreach (Button b in new[] { btnAdd, btnRemove, btnUp, btnDown, btnRefresh })
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
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                EditMode = DataGridViewEditMode.EditOnEnter
            };
            BuildGridColumns();
            _grid.DataSource = _rows;
            _grid.CellValueChanged += (s, e) => RefreshPreview();
            _grid.CurrentCellDirtyStateChanged += (s, e) =>
            {
                if (_grid.IsCurrentCellDirty)
                {
                    _grid.CommitEdit(DataGridViewDataErrorContexts.Commit);
                }
            };

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
                Height = 22,
                Text = "Generated Columns Json (read-only preview)"
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
                RefreshPreview();
            };
        }

        private void BuildGridColumns()
        {
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Name",
                HeaderText = "name *",
                FillWeight = 90
            });
            var typeCol = new DataGridViewComboBoxColumn
            {
                DataPropertyName = "Type",
                HeaderText = "type *",
                FlatStyle = FlatStyle.Flat,
                FillWeight = 80
            };
            typeCol.Items.AddRange(
                MixTableColumnType.Label,
                MixTableColumnType.TextBox,
                MixTableColumnType.ComboBox,
                MixTableColumnType.CheckBox,
                MixTableColumnType.Button,
                MixTableColumnType.Link);
            _grid.Columns.Add(typeCol);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "Text",
                HeaderText = "text (header)",
                FillWeight = 100
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "WidthText",
                HeaderText = "width",
                FillWeight = 50
            });
            var alignCol = new DataGridViewComboBoxColumn
            {
                DataPropertyName = "HeaderAlign",
                HeaderText = "headerAlign",
                FlatStyle = FlatStyle.Flat,
                FillWeight = 90
            };
            alignCol.Items.AddRange(
                "",
                "left-top", "middle-top", "right-top",
                "left-center", "middle-center", "right-center",
                "left-bottom", "middle-bottom", "right-bottom");
            _grid.Columns.Add(alignCol);
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ItemsText",
                HeaderText = "items (Combo, comma-separated)",
                FillWeight = 120
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "ReadOnly",
                HeaderText = "readOnly",
                FillWeight = 50
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FontFamily",
                HeaderText = "fontFamily",
                FillWeight = 80
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "FontSizeText",
                HeaderText = "fontSize",
                FillWeight = 50
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "FontBold",
                HeaderText = "bold",
                FillWeight = 40
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "FontItalic",
                HeaderText = "italic",
                FillWeight = 40
            });
            _grid.Columns.Add(new DataGridViewCheckBoxColumn
            {
                DataPropertyName = "FontUnderline",
                HeaderText = "underline",
                FillWeight = 55
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "ForeColor",
                HeaderText = "foreColor",
                FillWeight = 70
            });
            _grid.Columns.Add(new DataGridViewTextBoxColumn
            {
                DataPropertyName = "BackColor",
                HeaderText = "backColor",
                FillWeight = 70
            });
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

        private void LoadRows(string json)
        {
            _rows.Clear();
            if (string.IsNullOrWhiteSpace(json))
            {
                return;
            }

            List<MixTableColumnDefinition> list =
                JsonConvert.DeserializeObject<List<MixTableColumnDefinition>>(json);
            if (list == null)
            {
                return;
            }

            foreach (MixTableColumnDefinition def in list)
            {
                if (def == null)
                {
                    continue;
                }

                _rows.Add(ColumnEditRow.FromDefinition(def));
            }
        }

        private void OnAdd(object sender, EventArgs e)
        {
            _rows.Add(new ColumnEditRow
            {
                Name = "column" + (_rows.Count + 1),
                Type = MixTableColumnType.Label,
                Text = "Column " + (_rows.Count + 1),
                HeaderAlign = "middle-center"
            });
            RefreshPreview();
        }

        private void OnRemove(object sender, EventArgs e)
        {
            if (_grid.CurrentRow == null || _grid.CurrentRow.Index < 0 || _grid.CurrentRow.Index >= _rows.Count)
            {
                return;
            }

            _rows.RemoveAt(_grid.CurrentRow.Index);
            RefreshPreview();
        }

        private void OnMoveUp(object sender, EventArgs e)
        {
            int i = _grid.CurrentRow == null ? -1 : _grid.CurrentRow.Index;
            if (i <= 0 || i >= _rows.Count)
            {
                return;
            }

            ColumnEditRow row = _rows[i];
            _rows.RemoveAt(i);
            _rows.Insert(i - 1, row);
            _grid.ClearSelection();
            _grid.Rows[i - 1].Selected = true;
            RefreshPreview();
        }

        private void OnMoveDown(object sender, EventArgs e)
        {
            int i = _grid.CurrentRow == null ? -1 : _grid.CurrentRow.Index;
            if (i < 0 || i >= _rows.Count - 1)
            {
                return;
            }

            ColumnEditRow row = _rows[i];
            _rows.RemoveAt(i);
            _rows.Insert(i + 1, row);
            _grid.ClearSelection();
            _grid.Rows[i + 1].Selected = true;
            RefreshPreview();
        }

        private void OnOk(object sender, EventArgs e)
        {
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

        private void RefreshPreview()
        {
            string error;
            string json = BuildJson(out error);
            _preview.Text = json ?? string.Empty;
            _status.Text = error ?? ("OK — " + _rows.Count + " column(s)");
            _status.ForeColor = error == null ? Color.DarkGreen : Color.Firebrick;
        }

        private string BuildJson(out string error)
        {
            error = null;
            var defs = new List<MixTableColumnDefinition>();
            var names = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 0; i < _rows.Count; i++)
            {
                ColumnEditRow row = _rows[i];
                string name = row.Name == null ? string.Empty : row.Name.Trim();
                if (string.IsNullOrEmpty(name))
                {
                    error = "Row " + (i + 1) + ": name is required.";
                    return FormatPartial(defs);
                }

                if (!names.Add(name))
                {
                    error = "Duplicate column name: '" + name + "'.";
                    return FormatPartial(defs);
                }

                string type = row.Type == null ? string.Empty : row.Type.Trim();
                if (!MixTableColumnType.IsKnown(type))
                {
                    error = "Row " + (i + 1) + ": type must be Label|TextBox|ComboBox|CheckBox|Button|Link.";
                    return FormatPartial(defs);
                }

                MixTableColumnDefinition def;
                try
                {
                    def = row.ToDefinition();
                }
                catch (Exception ex)
                {
                    error = "Row " + (i + 1) + ": " + ex.Message;
                    return FormatPartial(defs);
                }

                defs.Add(def);
            }

            return JsonConvert.SerializeObject(defs, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        private static string FormatPartial(List<MixTableColumnDefinition> defs)
        {
            return JsonConvert.SerializeObject(defs, Formatting.Indented, new JsonSerializerSettings
            {
                NullValueHandling = NullValueHandling.Ignore
            });
        }

        private sealed class ColumnEditRow
        {
            public string Name { get; set; }
            public string Type { get; set; } = MixTableColumnType.Label;
            public string Text { get; set; }
            public string WidthText { get; set; }
            public string HeaderAlign { get; set; }
            public string ItemsText { get; set; }
            public bool ReadOnly { get; set; }
            public string FontFamily { get; set; }
            public string FontSizeText { get; set; }
            public bool FontBold { get; set; }
            public bool FontItalic { get; set; }
            public bool FontUnderline { get; set; }
            public string ForeColor { get; set; }
            public string BackColor { get; set; }

            public static ColumnEditRow FromDefinition(MixTableColumnDefinition def)
            {
                return new ColumnEditRow
                {
                    Name = def.Name,
                    Type = string.IsNullOrWhiteSpace(def.Type) ? MixTableColumnType.Label : def.Type,
                    Text = def.Text,
                    WidthText = def.Width.HasValue ? def.Width.Value.ToString() : string.Empty,
                    HeaderAlign = def.HeaderAlign ?? string.Empty,
                    ItemsText = def.Items == null || def.Items.Count == 0
                        ? string.Empty
                        : string.Join(", ", def.Items),
                    ReadOnly = def.ReadOnly == true,
                    FontFamily = def.FontFamily,
                    FontSizeText = def.FontSize.HasValue ? def.FontSize.Value.ToString() : string.Empty,
                    FontBold = def.FontBold == true,
                    FontItalic = def.FontItalic == true,
                    FontUnderline = def.FontUnderline == true,
                    ForeColor = def.ForeColor,
                    BackColor = def.BackColor
                };
            }

            public MixTableColumnDefinition ToDefinition()
            {
                var def = new MixTableColumnDefinition
                {
                    Name = Name == null ? null : Name.Trim(),
                    Type = MixTableColumnType.Require(Type),
                    Text = string.IsNullOrWhiteSpace(Text) ? null : Text.Trim(),
                    HeaderAlign = string.IsNullOrWhiteSpace(HeaderAlign) ? null : HeaderAlign.Trim(),
                    ReadOnly = ReadOnly ? true : (bool?)null,
                    FontFamily = string.IsNullOrWhiteSpace(FontFamily) ? null : FontFamily.Trim(),
                    FontBold = FontBold ? true : (bool?)null,
                    FontItalic = FontItalic ? true : (bool?)null,
                    FontUnderline = FontUnderline ? true : (bool?)null,
                    ForeColor = string.IsNullOrWhiteSpace(ForeColor) ? null : ForeColor.Trim(),
                    BackColor = string.IsNullOrWhiteSpace(BackColor) ? null : BackColor.Trim()
                };

                if (!string.IsNullOrWhiteSpace(WidthText))
                {
                    int width;
                    if (!int.TryParse(WidthText.Trim(), out width) || width <= 0)
                    {
                        throw new InvalidOperationException("width must be a positive integer, or empty.");
                    }

                    def.Width = width;
                }

                if (!string.IsNullOrWhiteSpace(FontSizeText))
                {
                    float size;
                    if (!float.TryParse(FontSizeText.Trim(), out size) || size <= 0)
                    {
                        throw new InvalidOperationException("fontSize must be a positive number, or empty.");
                    }

                    def.FontSize = size;
                }

                if (!string.IsNullOrWhiteSpace(ItemsText))
                {
                    def.Items = ItemsText.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => p.Trim())
                        .Where(p => p.Length > 0)
                        .ToList();
                }

                return def;
            }
        }
    }
}
