using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.Design;
using System.Drawing;
using System.Drawing.Design;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Drawing.Text;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using F2B.Forms.Engine;
using F2B.Forms.Model;
using Newtonsoft.Json;

namespace F2B.Forms.Designer
{
    public sealed class MainForm : Form
    {
        private const int GridSize = 10;
        private const float MinZoom = 0.25f;
        private const float MaxZoom = 4f;
        private const float ZoomStep = 0.1f;
        private const int MinFormSize = 100;
        private const string ToolboxDragFormat = "F2B.Forms.Designer.ControlType";

        private readonly Panel _viewport;
        private readonly Panel _canvas;
        private readonly PropertyGrid _propertyGrid;
        private readonly TreeView _controlTree;
        private readonly Label _contextLabel;
        private readonly FormDesignSettings _formSettings = new FormDesignSettings();
        private readonly List<DesignItem> _roots = new List<DesignItem>();
        private DesignItem _selected;
        private readonly List<DesignItem> _selection = new List<DesignItem>();
        private readonly DesignClipboard _clipboard = new DesignClipboard();
        private readonly Dictionary<DesignItem, Rectangle> _dragStartAbsByItem =
            new Dictionary<DesignItem, Rectangle>();
        private string _currentPath;
        private int _idCounter = 1;
        private bool _dragging;
        private bool _draggingForm;
        private bool _draggingMultiMove;
        private ResizeHandle _dragHandle = ResizeHandle.None;
        private Rectangle _dragStartBoundsAbs;
        private Point _dragStartMouse;
        private bool _panning;
        private Point _panStartMouse;
        private Point _panStartScroll;
        private float _zoom = 1f;
        private bool _syncingTree;
        private bool _isDirty;
        private bool _updatingDesignArea;
        private Bitmap _formPaintBuffer;
        private readonly DesignHistory _history = new DesignHistory();
        private DesignSnapshot _stableSnapshot;
        private bool _suspendHistory;
        private bool _dragHistoryPushed;
        private Point _toolboxDragStart;
        private string _toolboxDragType;
        private bool _toolboxDidDrag;
        private readonly bool _isViewer;

        public MainForm()
            : this(isViewer: false)
        {
        }

        public MainForm(bool isViewer)
        {
            _isViewer = isViewer;
            Text = _isViewer ? "F2B.Forms.Viewer" : "F2B.Forms.Designer（New Form）";
            Width = 1200;
            Height = 780;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(900, 600);
            KeyPreview = true;
            FormClosing += MainForm_FormClosing;
            KeyDown += MainForm_KeyDown;

            var menu = BuildMenuStrip();
            Control controlsBar = _isViewer ? null : BuildControlsToolbox();

            _viewport = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(70, 70, 70),
                BorderStyle = BorderStyle.None,
                TabStop = true
            };
            _viewport.Resize += (s, e) => UpdateDesignAreaSize();
            _viewport.MouseDown += ViewportOnMouseDown;
            _viewport.MouseMove += ViewportOnMouseMove;
            _viewport.MouseUp += ViewportOnMouseUp;
            _viewport.MouseWheel += ViewportOnMouseWheel;

            _canvas = new DoubleBufferedPanel
            {
                BackColor = Color.FromArgb(235, 235, 235),
                BorderStyle = BorderStyle.None,
                TabStop = true,
                Location = Point.Empty,
                AllowDrop = !_isViewer
            };
            _canvas.Paint += CanvasOnPaint;
            _canvas.MouseDown += CanvasOnMouseDown;
            _canvas.MouseMove += CanvasOnMouseMove;
            _canvas.MouseUp += CanvasOnMouseUp;
            _canvas.MouseWheel += ViewportOnMouseWheel;
            if (!_isViewer)
            {
                _canvas.DragEnter += CanvasOnDragEnter;
                _canvas.DragOver += CanvasOnDragOver;
                _canvas.DragDrop += CanvasOnDragDrop;
            }

            _viewport.Controls.Add(_canvas);

            _propertyGrid = new PropertyGrid
            {
                Dock = DockStyle.Fill,
                ToolbarVisible = false,
                HelpVisible = false, // hide bottom description pane (looks like an extra "Height" strip)
                PropertySort = PropertySort.CategorizedAlphabetical
            };
            _propertyGrid.PropertyValueChanged += (s, e) =>
            {
                if (_isViewer)
                {
                    return;
                }

                // PropertyGrid raises this after the value changed — undo restores the prior stable snapshot.
                if (!_suspendHistory && _stableSnapshot != null)
                {
                    _history.PushUndo(_stableSnapshot);
                }

                // Properties allow fine (non-grid) values; canvas move/resize snaps to 10px.
                MarkDirty();
                RebuildTreePreserveSelection();
                if (_propertyGrid.SelectedObject == _formSettings)
                {
                    UpdateDesignAreaSize();
                }

                RelayoutAffectedTableLayouts();
                _canvas.Invalidate();
                CaptureStableSnapshot();
            };

            _contextLabel = new Label
            {
                Dock = DockStyle.Top,
                Height = _isViewer ? 0 : 22,
                Visible = !_isViewer,
                Text = "Add target: Form (root)",
                ForeColor = Color.DimGray,
                Padding = new Padding(6, 3, 0, 0)
            };

            _controlTree = new TreeView
            {
                Dock = DockStyle.Fill,
                HideSelection = false,
                ShowLines = true,
                ShowPlusMinus = true,
                FullRowSelect = true,
                BorderStyle = BorderStyle.None
            };
            _controlTree.AfterSelect += OnTreeAfterSelect;

            // Layout: left Tree View | center Designer/Viewer Area | right Properties (full height).
            // Use TableLayoutPanel instead of SplitContainer to avoid startup SplitterDistance exceptions.
            var treePanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            treePanel.Controls.Add(_controlTree);
            treePanel.Controls.Add(_contextLabel);
            treePanel.Controls.Add(CreateSectionHeader("Tree View"));

            var surfacePanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            surfacePanel.Controls.Add(_viewport);
            surfacePanel.Controls.Add(CreateSectionHeader(_isViewer ? "Viewer Area" : "Designer Area"));

            var propertiesPanel = new Panel { Dock = DockStyle.Fill, BorderStyle = BorderStyle.FixedSingle };
            propertiesPanel.Controls.Add(_propertyGrid);
            propertiesPanel.Controls.Add(CreateSectionHeader("Properties"));

            var rootHost = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            rootHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 18f));  // Tree View
            rootHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 57f));  // Surface
            rootHost.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 25f));  // Properties
            rootHost.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
            rootHost.Controls.Add(treePanel, 0, 0);
            rootHost.Controls.Add(surfacePanel, 1, 0);
            rootHost.Controls.Add(propertiesPanel, 2, 0);

            // Dock order: Fill first, then Top strips (last Top added sits under the previous Top).
            Controls.Add(rootHost);
            if (controlsBar != null)
            {
                Controls.Add(controlsBar);
            }

            Controls.Add(menu);
            MainMenuStrip = menu;

            Shown += (s, e) => UpdateDesignAreaSize();
            NewForm();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            if (IsMouseOverDesignSurface())
            {
                Point screen = Cursor.Position;
                Control target = _canvas.RectangleToScreen(_canvas.ClientRectangle).Contains(screen)
                    ? (Control)_canvas
                    : _viewport;
                Point client = target.PointToClient(screen);
                ViewportOnMouseWheel(target, new MouseEventArgs(e.Button, e.Clicks, client.X, client.Y, e.Delta));
                if (e is HandledMouseEventArgs handled)
                {
                    handled.Handled = true;
                }

                return;
            }

            base.OnMouseWheel(e);
        }

        private bool IsMouseOverDesignSurface()
        {
            return _viewport != null
                && _viewport.RectangleToScreen(_viewport.ClientRectangle).Contains(Cursor.Position);
        }

        private Size GetViewportLogicalSize()
        {
            // Use the full viewport (ignore scrollbar inset) so area sizing does not oscillate.
            int pixelW = _viewport.ClientSize.Width;
            int pixelH = _viewport.ClientSize.Height;
            if (_viewport.VerticalScroll.Visible)
            {
                pixelW += SystemInformation.VerticalScrollBarWidth;
            }

            if (_viewport.HorizontalScroll.Visible)
            {
                pixelH += SystemInformation.HorizontalScrollBarHeight;
            }

            int w = Math.Max(1, (int)Math.Floor(pixelW / _zoom));
            int h = Math.Max(1, (int)Math.Floor(pixelH / _zoom));
            return new Size(w, h);
        }

        /// <summary>
        /// Area fills the viewport at minimum; when form exceeds the viewport,
        /// area is at least 150% of the form on that axis.
        /// </summary>
        private Size ComputeDesignAreaLogicalSize()
        {
            Size viewport = GetViewportLogicalSize();
            int formW = Math.Max(MinFormSize, _formSettings.Width);
            int formH = Math.Max(MinFormSize, _formSettings.Height);

            int areaW = viewport.Width;
            int areaH = viewport.Height;

            if (formW > viewport.Width)
            {
                areaW = Math.Max(areaW, (int)Math.Ceiling(formW * 1.5));
            }
            else
            {
                areaW = Math.Max(areaW, formW);
            }

            if (formH > viewport.Height)
            {
                areaH = Math.Max(areaH, (int)Math.Ceiling(formH * 1.5));
            }
            else
            {
                areaH = Math.Max(areaH, formH);
            }

            return new Size(areaW, areaH);
        }

        private void UpdateDesignAreaSize()
        {
            if (_updatingDesignArea || _viewport == null || _canvas == null)
            {
                return;
            }

            _updatingDesignArea = true;
            try
            {
                Size area = ComputeDesignAreaLogicalSize();
                int pixelW = Math.Max(1, (int)Math.Ceiling(area.Width * _zoom));
                int pixelH = Math.Max(1, (int)Math.Ceiling(area.Height * _zoom));
                if (_canvas.Width != pixelW || _canvas.Height != pixelH)
                {
                    _canvas.Size = new Size(pixelW, pixelH);
                }

                _canvas.Invalidate();
            }
            finally
            {
                _updatingDesignArea = false;
            }
        }

        private Point ToLogical(Point surfacePoint)
        {
            return new Point(
                (int)Math.Round(surfacePoint.X / _zoom),
                (int)Math.Round(surfacePoint.Y / _zoom));
        }

        private Rectangle ToSurface(Rectangle logicalBounds)
        {
            return new Rectangle(
                (int)Math.Round(logicalBounds.X * _zoom),
                (int)Math.Round(logicalBounds.Y * _zoom),
                Math.Max(1, (int)Math.Round(logicalBounds.Width * _zoom)),
                Math.Max(1, (int)Math.Round(logicalBounds.Height * _zoom)));
        }

        private ResizeHandle HitTestHandleZoomAware(Rectangle logicalBounds, Point surfacePoint)
        {
            // Keep grip hit targets ~constant in screen pixels across zoom levels.
            return DesignSurfacePainter.HitTestHandle(ToSurface(logicalBounds), surfacePoint);
        }

        private Rectangle GetFormBounds()
        {
            return new Rectangle(0, 0, _formSettings.Width, _formSettings.Height);
        }

        private void SetZoom(float zoom, Point canvasClientAnchor)
        {
            float next = Math.Max(MinZoom, Math.Min(MaxZoom, zoom));
            next = (float)(Math.Round(next / ZoomStep) * ZoomStep);
            if (Math.Abs(next - _zoom) < 0.001f)
            {
                return;
            }

            Point logical = ToLogical(canvasClientAnchor);
            Point viewOffset = _viewport.PointToClient(_canvas.PointToScreen(canvasClientAnchor));

            _zoom = next;
            UpdateDesignAreaSize();

            int newSurfaceX = (int)Math.Round(logical.X * _zoom);
            int newSurfaceY = (int)Math.Round(logical.Y * _zoom);
            _viewport.AutoScrollPosition = new Point(
                Math.Max(0, newSurfaceX - viewOffset.X),
                Math.Max(0, newSurfaceY - viewOffset.Y));
            _canvas.Invalidate();
        }

        private void ViewportOnMouseWheel(object sender, MouseEventArgs e)
        {
            if ((ModifierKeys & Keys.Control) == Keys.Control)
            {
                Point canvasPt = sender == _canvas
                    ? e.Location
                    : _canvas.PointToClient(_viewport.PointToScreen(e.Location));
                if (canvasPt.X < 0 || canvasPt.Y < 0)
                {
                    canvasPt = new Point(
                        -_viewport.AutoScrollPosition.X + _viewport.ClientSize.Width / 2,
                        -_viewport.AutoScrollPosition.Y + _viewport.ClientSize.Height / 2);
                }

                float next = _zoom + (e.Delta > 0 ? ZoomStep : -ZoomStep);
                SetZoom(next, canvasPt);
                return;
            }

            Point scroll = new Point(-_viewport.AutoScrollPosition.X, -_viewport.AutoScrollPosition.Y);
            _viewport.AutoScrollPosition = new Point(scroll.X, Math.Max(0, scroll.Y - e.Delta));
        }

        private void ViewportOnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                BeginPan(e.Location, fromCanvas: false);
            }
        }

        private void ViewportOnMouseMove(object sender, MouseEventArgs e)
        {
            if (_panning)
            {
                ApplyPan(sender == _canvas ? _viewport.PointToClient(_canvas.PointToScreen(e.Location)) : e.Location);
            }
        }

        private void ViewportOnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                EndPan();
            }
        }

        private void BeginPan(Point viewportOrCanvasPoint, bool fromCanvas)
        {
            Point viewportPt = fromCanvas
                ? _viewport.PointToClient(_canvas.PointToScreen(viewportOrCanvasPoint))
                : viewportOrCanvasPoint;
            _panning = true;
            _panStartMouse = viewportPt;
            _panStartScroll = new Point(-_viewport.AutoScrollPosition.X, -_viewport.AutoScrollPosition.Y);
            _viewport.Capture = true;
            _canvas.Cursor = Cursors.SizeAll;
            _viewport.Cursor = Cursors.SizeAll;
        }

        private void ApplyPan(Point viewportPoint)
        {
            if (!_panning)
            {
                return;
            }

            int dx = viewportPoint.X - _panStartMouse.X;
            int dy = viewportPoint.Y - _panStartMouse.Y;
            _viewport.AutoScrollPosition = new Point(
                Math.Max(0, _panStartScroll.X - dx),
                Math.Max(0, _panStartScroll.Y - dy));
        }

        private void EndPan()
        {
            if (!_panning)
            {
                return;
            }

            _panning = false;
            _viewport.Capture = false;
            _canvas.Cursor = Cursors.Default;
            _viewport.Cursor = Cursors.Default;
        }

        private MenuStrip BuildMenuStrip()
        {
            var menu = new MenuStrip();
            if (_isViewer)
            {
                menu.Items.Add(new ToolStripMenuItem("Open", null, (s, e) => TryOpenForm()));
                menu.Items.Add(new ToolStripMenuItem("Preview", null, (s, e) => Preview()));
                return menu;
            }

            menu.Items.Add(new ToolStripMenuItem("New", null, (s, e) => TryNewForm()));
            menu.Items.Add(new ToolStripMenuItem("Open", null, (s, e) => TryOpenForm()));
            menu.Items.Add(new ToolStripMenuItem("Save", null, (s, e) => SaveForm(false)));
            menu.Items.Add(new ToolStripMenuItem("SaveAs", null, (s, e) => SaveForm(true)));
            menu.Items.Add(new ToolStripMenuItem("Preview", null, (s, e) => Preview()));
            return menu;
        }

        private Control BuildControlsToolbox()
        {
            var root = new Panel
            {
                Dock = DockStyle.Top,
                Height = 118,
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = Padding.Empty
            };
            root.Paint += (s, e) =>
            {
                using (var pen = new Pen(Color.FromArgb(225, 225, 225)))
                {
                    int y = root.ClientSize.Height - 1;
                    e.Graphics.DrawLine(pen, 0, y, root.ClientSize.Width, y);
                }
            };

            var split = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 3,
                RowCount = 1,
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                BackColor = Color.FromArgb(250, 250, 250)
            };
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 55f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 1f));
            split.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 45f));
            split.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

            Control operators = BuildToolboxCategoryScroll(
                "Operators",
                new[]
                {
                    Tuple.Create("Button", FormControlType.Button),
                    Tuple.Create("Label", FormControlType.Label),
                    Tuple.Create("TextBox", FormControlType.TextBox),
                    Tuple.Create("TextArea", FormControlType.TextArea),
                    Tuple.Create("CheckBox", FormControlType.CheckBox),
                    Tuple.Create("RadioButton", FormControlType.RadioButton),
                    Tuple.Create("ComboBox", FormControlType.ComboBox),
                    Tuple.Create("ListBox", FormControlType.ListBox),
                    Tuple.Create("CheckedListBox", FormControlType.CheckedListBox),
                    Tuple.Create("MaskedTextBox", FormControlType.MaskedTextBox),
                    Tuple.Create("NumericUpDown", FormControlType.NumericUpDown),
                    Tuple.Create("DatePicker", FormControlType.DatePicker),
                    Tuple.Create("DateTimePicker", FormControlType.DateTimePicker),
                    Tuple.Create("PictureBox", FormControlType.PictureBox),
                    Tuple.Create("DataGrid", FormControlType.DataGrid)
                });

            var divider = new Panel
            {
                Dock = DockStyle.Fill,
                BackColor = Color.FromArgb(210, 210, 210),
                Margin = Padding.Empty
            };

            Control containers = BuildToolboxCategoryScroll(
                "Containers",
                new[]
                {
                    Tuple.Create("Panel", FormControlType.Panel),
                    Tuple.Create("ScrollContainer", FormControlType.ScrollContainer),
                    Tuple.Create("TableLayout", FormControlType.TableLayout),
                    Tuple.Create("GroupBox", FormControlType.GroupBox),
                    Tuple.Create("TabControl", FormControlType.TabControl),
                    Tuple.Create("TabPage", FormControlType.TabPage)
                });

            split.Controls.Add(operators, 0, 0);
            split.Controls.Add(divider, 1, 0);
            split.Controls.Add(containers, 2, 0);
            root.Controls.Add(split);
            return root;
        }

        private Control BuildToolboxCategoryScroll(string title, Tuple<string, string>[] items)
        {
            var host = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = false,
                BackColor = Color.FromArgb(250, 250, 250),
                Margin = Padding.Empty,
                Padding = new Padding(4, 4, 4, 2)
            };

            var header = new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 20,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Segoe UI", 8.25f, FontStyle.Bold),
                ForeColor = Color.FromArgb(90, 90, 90),
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };

            var strip = new ToolStrip
            {
                GripStyle = ToolStripGripStyle.Hidden,
                Renderer = new ControlsToolStripRenderer(),
                BackColor = Color.FromArgb(250, 250, 250),
                Padding = new Padding(4, 2, 4, 2),
                ImageScalingSize = new Size(ControlToolboxThumbnails.Width, ControlToolboxThumbnails.Height),
                AutoSize = false,
                CanOverflow = false,
                Font = new Font("Segoe UI", 8.25f),
                Dock = DockStyle.None,
                Anchor = AnchorStyles.Top | AnchorStyles.Left,
                Margin = Padding.Empty,
                Location = new Point(0, 0)
            };

            foreach (Tuple<string, string> item in items)
            {
                strip.Items.Add(MakeAddToolButton(item.Item1, item.Item2));
            }

            Size preferred = strip.GetPreferredSize(Size.Empty);
            strip.Size = preferred;

            // AutoScroll needs a child that can grow wider than the host.
            var stripHost = new Panel
            {
                AutoSize = false,
                Size = preferred,
                BackColor = Color.FromArgb(250, 250, 250),
                Location = Point.Empty,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            stripHost.Controls.Add(strip);

            void SyncStripHostSize(object sender, EventArgs e)
            {
                Size next = strip.GetPreferredSize(Size.Empty);
                if (next.Width < 1)
                {
                    next = strip.PreferredSize;
                }

                strip.Size = next;
                stripHost.Size = next;
            }

            strip.Layout += SyncStripHostSize;
            strip.SizeChanged += SyncStripHostSize;
            SyncStripHostSize(strip, EventArgs.Empty);

            // Dock Top header first in z-order last; fill area is the scroll body under header.
            var body = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 250, 250),
                Margin = Padding.Empty,
                Padding = Padding.Empty,
                Tag = "ControlsToolboxScroll"
            };
            body.Controls.Add(stripHost);

            host.Controls.Add(body);
            host.Controls.Add(header);
            AttachControlsToolboxMouseWheel(host);
            return host;
        }

        private void AttachControlsToolboxMouseWheel(Control root)
        {
            if (root == null)
            {
                return;
            }

            root.MouseWheel += ControlsToolbox_MouseWheel;
            foreach (Control child in root.Controls)
            {
                AttachControlsToolboxMouseWheel(child);
            }
        }

        private void ControlsToolbox_MouseWheel(object sender, MouseEventArgs e)
        {
            Panel host = FindControlsToolboxScrollHost(sender as Control);
            if (host == null || !host.HorizontalScroll.Visible)
            {
                return;
            }

            int x = -host.AutoScrollPosition.X - Math.Sign(e.Delta) * 48;
            if (x < 0)
            {
                x = 0;
            }

            host.AutoScrollPosition = new Point(x, 0);
            if (e is HandledMouseEventArgs handled)
            {
                handled.Handled = true;
            }
        }

        private static Panel FindControlsToolboxScrollHost(Control control)
        {
            Control current = control;
            while (current != null)
            {
                if (current is Panel panel
                    && panel.Tag as string == "ControlsToolboxScroll"
                    && panel.AutoScroll)
                {
                    return panel;
                }

                current = current.Parent;
            }

            return null;
        }

        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (_isViewer)
            {
                return;
            }

            if (e.Control && e.Alt && !e.Shift && e.KeyCode == Keys.S)
            {
                SaveForm(true);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.S)
            {
                SaveForm(false);
                e.Handled = true;
                e.SuppressKeyPress = true;
                return;
            }

            if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.C)
            {
                if (IsCanvasOrTreeFocused() && CopySelection())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                return;
            }

            if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.V)
            {
                if (IsCanvasOrTreeFocused() && PasteClipboard())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                return;
            }

            if (e.Control && !e.Alt && !e.Shift && e.KeyCode == Keys.D)
            {
                if (IsCanvasOrTreeFocused() && DuplicateSelection())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                return;
            }

            if (e.Control && !e.Alt && e.KeyCode == Keys.Z && !e.Shift)
            {
                if (TryUndo())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                return;
            }

            if (e.Control && !e.Alt && (e.KeyCode == Keys.Y || (e.KeyCode == Keys.Z && e.Shift)))
            {
                if (TryRedo())
                {
                    e.Handled = true;
                    e.SuppressKeyPress = true;
                }

                return;
            }

            if (e.KeyCode != Keys.Delete)
            {
                return;
            }

            if (_selection.Count == 0 || !IsCanvasOrTreeFocused())
            {
                return;
            }

            DeleteSelected();
            e.Handled = true;
            e.SuppressKeyPress = true;
        }

        private bool IsCanvasOrTreeFocused()
        {
            Control focused = ActiveControl;
            while (focused != null)
            {
                if (focused == _canvas || focused == _viewport || focused == _controlTree)
                {
                    return true;
                }

                focused = focused.Parent;
            }

            return false;
        }

        private static Label CreateSectionHeader(string title)
        {
            return new Label
            {
                Text = title,
                Dock = DockStyle.Top,
                Height = 26,
                TextAlign = ContentAlignment.MiddleLeft,
                Padding = new Padding(8, 0, 0, 0),
                Font = new Font(SystemFonts.DefaultFont, FontStyle.Bold),
                BackColor = Color.FromArgb(240, 240, 240),
                BorderStyle = BorderStyle.FixedSingle
            };
        }

        private ToolStripButton MakeAddToolButton(string text, string type)
        {
            var button = new ToolStripButton(text, ControlToolboxThumbnails.Create(type))
            {
                Tag = type,
                DisplayStyle = ToolStripItemDisplayStyle.ImageAndText,
                TextImageRelation = TextImageRelation.ImageAboveText,
                AutoSize = true,
                Margin = new Padding(3, 0, 3, 0),
                Padding = new Padding(8, 6, 8, 4),
                ForeColor = Color.FromArgb(60, 60, 60)
            };
            button.MouseDown += ToolboxButton_MouseDown;
            button.MouseMove += ToolboxButton_MouseMove;
            button.Click += ToolboxButton_Click;
            return button;
        }

        private void ToolboxButton_Click(object sender, EventArgs e)
        {
            if (_toolboxDidDrag)
            {
                _toolboxDidDrag = false;
                return;
            }

            var button = sender as ToolStripButton;
            string type = button == null ? null : button.Tag as string;
            if (!string.IsNullOrEmpty(type))
            {
                AddControl(type);
            }
        }

        private void ToolboxButton_MouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            var button = sender as ToolStripButton;
            _toolboxDidDrag = false;
            _toolboxDragStart = e.Location;
            _toolboxDragType = button == null ? null : button.Tag as string;
        }

        private void ToolboxButton_MouseMove(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left || string.IsNullOrEmpty(_toolboxDragType))
            {
                return;
            }

            if (Math.Abs(e.X - _toolboxDragStart.X) < SystemInformation.DragSize.Width
                && Math.Abs(e.Y - _toolboxDragStart.Y) < SystemInformation.DragSize.Height)
            {
                return;
            }

            var button = sender as ToolStripButton;
            if (button == null)
            {
                return;
            }

            _toolboxDragType = null;
            _toolboxDidDrag = true;
            var data = new DataObject();
            data.SetData(ToolboxDragFormat, button.Tag as string);
            button.DoDragDrop(data, DragDropEffects.Copy);
        }

        private void CanvasOnDragEnter(object sender, DragEventArgs e)
        {
            e.Effect = CanAcceptToolboxDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void CanvasOnDragOver(object sender, DragEventArgs e)
        {
            e.Effect = CanAcceptToolboxDrag(e) ? DragDropEffects.Copy : DragDropEffects.None;
        }

        private void CanvasOnDragDrop(object sender, DragEventArgs e)
        {
            if (!TryGetToolboxType(e.Data, out string type))
            {
                return;
            }

            Point logical = ToLogical(_canvas.PointToClient(new Point(e.X, e.Y)));
            AddControl(type, logical);
        }

        private bool CanAcceptToolboxDrag(DragEventArgs e)
        {
            if (e == null || !TryGetToolboxType(e.Data, out string type))
            {
                return false;
            }

            Point logical = ToLogical(_canvas.PointToClient(new Point(e.X, e.Y)));
            if (!GetFormBounds().Contains(logical))
            {
                return false;
            }

            if (FormControlType.IsTabPage(type))
            {
                return FindTabControlAtPoint(logical) != null || FindTabControlForNewPage() != null;
            }

            return true;
        }

        private static bool TryGetToolboxType(IDataObject data, out string type)
        {
            type = null;
            if (data == null || !data.GetDataPresent(ToolboxDragFormat))
            {
                return false;
            }

            type = data.GetData(ToolboxDragFormat) as string;
            return !string.IsNullOrWhiteSpace(type);
        }

        private void OnTreeAfterSelect(object sender, TreeViewEventArgs e)
        {
            if (_syncingTree)
            {
                return;
            }

            if (e.Node != null && e.Node.Tag is DesignItem item)
            {
                SelectItem(item, syncTree: false);
            }
            else if (e.Node != null && e.Node.Tag == null)
            {
                SelectFormRoot(syncTree: false);
            }
        }

        private void SelectFormRoot(bool syncTree)
        {
            _selection.Clear();
            _selected = null;
            _propertyGrid.SelectedObjects = null;
            _propertyGrid.SelectedObject = _formSettings;
            UpdateContextLabel();
            if (syncTree && _controlTree.Nodes.Count > 0)
            {
                _syncingTree = true;
                try
                {
                    _controlTree.SelectedNode = _controlTree.Nodes[0];
                }
                finally
                {
                    _syncingTree = false;
                }
            }

            _canvas.Invalidate();
        }

        private void MainForm_FormClosing(object sender, FormClosingEventArgs e)
        {
            if (!PromptSaveIfDirty())
            {
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Returns false if the user cancels (keep editing).
        /// </summary>
        private bool PromptSaveIfDirty()
        {
            if (_isViewer || !_isDirty)
            {
                return true;
            }

            DialogResult result = MessageBox.Show(
                this,
                "当前设计已修改但尚未保存，是否保存？",
                "F2B.Forms Designer",
                MessageBoxButtons.YesNoCancel,
                MessageBoxIcon.Warning);

            if (result == DialogResult.Cancel)
            {
                return false;
            }

            if (result == DialogResult.Yes)
            {
                return SaveForm(false);
            }

            // No — discard changes
            return true;
        }

        private void MarkDirty()
        {
            if (_isViewer || _isDirty)
            {
                return;
            }

            _isDirty = true;
            UpdateWindowTitle();
        }

        private void ClearDirty()
        {
            _isDirty = false;
            UpdateWindowTitle();
        }

        private void ResetHistory()
        {
            _history.Clear();
            _dragHistoryPushed = false;
            CaptureStableSnapshot();
        }

        private void PushHistoryBeforeEdit()
        {
            if (_suspendHistory)
            {
                return;
            }

            _history.PushUndo(TakeSnapshot());
        }

        private void CaptureStableSnapshot()
        {
            if (_suspendHistory)
            {
                return;
            }

            _stableSnapshot = TakeSnapshot();
        }

        private DesignSnapshot TakeSnapshot()
        {
            return new DesignSnapshot
            {
                Json = FormJsonLoader.ToJson(BuildDefinition()),
                SelectedId = _selected == null ? null : _selected.Id,
                SelectedIds = _selection.Select(i => i.Id).ToArray(),
                IdCounter = _idCounter
            };
        }

        private void FinishDragHistory()
        {
            if (!_dragHistoryPushed)
            {
                return;
            }

            _dragHistoryPushed = false;
            DesignSnapshot current = TakeSnapshot();
            if (_stableSnapshot != null && current.ContentEquals(_stableSnapshot))
            {
                // Drag started but nothing changed — drop the empty undo entry.
                _history.DiscardLastUndo();
            }

            CaptureStableSnapshot();
        }

        private bool TryUndo()
        {
            if (!_history.CanUndo || _dragging || _panning)
            {
                return false;
            }

            DesignSnapshot previous = _history.Undo(TakeSnapshot());
            if (previous == null)
            {
                return false;
            }

            ApplySnapshot(previous);
            MarkDirty();
            return true;
        }

        private bool TryRedo()
        {
            if (!_history.CanRedo || _dragging || _panning)
            {
                return false;
            }

            DesignSnapshot next = _history.Redo(TakeSnapshot());
            if (next == null)
            {
                return false;
            }

            ApplySnapshot(next);
            MarkDirty();
            return true;
        }

        private void ApplySnapshot(DesignSnapshot snapshot)
        {
            if (snapshot == null)
            {
                return;
            }

            _suspendHistory = true;
            try
            {
                FormDefinition def;
                try
                {
                    def = FormJsonLoader.LoadFromJson(snapshot.Json);
                }
                catch (InvalidOperationException ex)
                {
                    MessageBox.Show(
                        this,
                        "无法恢复该历史步骤：" + ex.Message,
                        "F2B.Forms Designer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Warning);
                    return;
                }
                _roots.Clear();
                _formSettings.Title = def.Title ?? "Form";
                _formSettings.Width = def.Width > 0 ? def.Width : 600;
                _formSettings.Height = def.Height > 0 ? def.Height : 800;
                _formSettings.AllowResize = def.AllowResize;
                if (def.Controls != null)
                {
                    foreach (ControlDefinition c in def.Controls)
                    {
                        _roots.Add(DesignItem.FromDefinition(c, parent: null));
                    }
                }

                _idCounter = Math.Max(1, snapshot.IdCounter);
                RebuildTreePreserveSelection();
                RestoreSelectionFromSnapshot(snapshot);
                UpdateDesignAreaSize();
                _canvas.Invalidate();
            }
            finally
            {
                _suspendHistory = false;
            }

            CaptureStableSnapshot();
        }

        private void RestoreSelectionFromSnapshot(DesignSnapshot snapshot)
        {
            _selection.Clear();
            if (snapshot.SelectedIds != null)
            {
                foreach (string id in snapshot.SelectedIds)
                {
                    DesignItem item = FindById(_roots, id);
                    if (item != null)
                    {
                        _selection.Add(item);
                    }
                }
            }
            else if (!string.IsNullOrEmpty(snapshot.SelectedId))
            {
                DesignItem item = FindById(_roots, snapshot.SelectedId);
                if (item != null)
                {
                    _selection.Add(item);
                }
            }

            _selected = _selection.Count > 0 ? _selection[_selection.Count - 1] : null;
            if (_selected != null)
            {
                EnsureTabSelection(_selected);
                BindPropertyGridToSelection();
                UpdateContextLabel();
                SelectTreeNode(_selected);
            }
            else
            {
                SelectFormRoot(syncTree: true);
            }
        }

        private void UpdateWindowTitle()
        {
            string app = _isViewer ? "F2B.Forms.Viewer" : "F2B.Forms.Designer";
            string doc = string.IsNullOrEmpty(_currentPath)
                ? (_isViewer ? "No Form" : "New Form")
                : _currentPath;
            Text = app + "（" + doc + "）" + (!_isViewer && _isDirty ? " *" : string.Empty);
        }

        private void TryNewForm()
        {
            if (!PromptSaveIfDirty())
            {
                return;
            }

            NewForm();
        }

        private void TryOpenForm()
        {
            if (!PromptSaveIfDirty())
            {
                return;
            }

            OpenForm();
        }

        private void NewForm()
        {
            _roots.Clear();
            _selected = null;
            _currentPath = null;
            _idCounter = 1;
            _zoom = 1f;
            _formSettings.Title = "Sample Form";
            _formSettings.Width = 600;
            _formSettings.Height = 800;
            _formSettings.AllowResize = true;
            RebuildTreePreserveSelection();
            SelectFormRoot(syncTree: true);
            UpdateDesignAreaSize();
            ClearDirty();
            ResetHistory();
        }

        private void AddControl(string type)
        {
            AddControl(type, logicalLocation: null);
        }

        private void AddControl(string type, Point? logicalLocation)
        {
            if (FormControlType.IsTabPage(type))
            {
                DesignItem tabControl = logicalLocation.HasValue
                    ? FindTabControlAtPoint(logicalLocation.Value) ?? FindTabControlForNewPage()
                    : FindTabControlForNewPage();
                if (tabControl == null)
                {
                    MessageBox.Show(
                        this,
                        "Select a TabControl (or one of its TabPages) before adding a TabPage.",
                        "F2B.Forms Designer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                PushHistoryBeforeEdit();
                DesignItem page = CreateDesignItem(FormControlType.TabPage, tabControl, 0, 0);
                tabControl.Children.Add(page);
                tabControl.SelectedIndex = tabControl.Children.Count - 1;
                MarkDirty();
                RebuildTreePreserveSelection();
                SelectItem(page, syncTree: true);
                _canvas.Invalidate();
                CaptureStableSnapshot();
                return;
            }

            DesignItem parent;
            Point parentOrigin;
            int x;
            int y;
            if (logicalLocation.HasValue)
            {
                parent = ResolveDropParent(logicalLocation.Value, out parentOrigin);
                x = Snap(Math.Max(0, logicalLocation.Value.X - parentOrigin.X));
                y = Snap(Math.Max(0, logicalLocation.Value.Y - parentOrigin.Y));
            }
            else
            {
                parent = GetAddTargetParent(type);
                IList<DesignItem> existing = parent == null ? _roots : parent.Children;
                int offset = Snap(20 + (existing.Count % 8) * 10);
                x = offset;
                y = offset;
            }

            IList<DesignItem> siblings = parent == null ? _roots : parent.Children;
            PushHistoryBeforeEdit();
            DesignItem item = CreateDesignItem(type, parent, x, y);

            if (type == FormControlType.ComboBox
                || type == FormControlType.ListBox
                || type == FormControlType.CheckedListBox)
            {
                item.Items = new List<string> { "Option1", "Option2" };
                item.SelectedIndex = 0;
                item.Text = string.Empty;
            }

            if (type == FormControlType.NumericUpDown)
            {
                item.Text = "0";
                item.Minimum = 0;
                item.Maximum = 100;
                item.Increment = 1;
                item.DecimalPlaces = 0;
            }

            if (type == FormControlType.MaskedTextBox)
            {
                item.Mask = "000-000-0000";
                item.Text = string.Empty;
            }

            if (type == FormControlType.PictureBox)
            {
                item.SizeMode = "Zoom";
                item.Text = string.Empty;
            }

            if (FormControlType.IsTabControl(type))
            {
                item.SelectedIndex = 0;
                DesignItem page1 = CreateDesignItem(FormControlType.TabPage, item, 0, 0);
                page1.Text = "Tab 1";
                DesignItem page2 = CreateDesignItem(FormControlType.TabPage, item, 0, 0);
                page2.Text = "Tab 2";
                item.Children.Add(page1);
                item.Children.Add(page2);
            }

            if (FormControlType.IsTableLayout(type))
            {
                item.RowCount = 3;
                item.ColumnCount = 3;
            }

            if (parent != null && FormControlType.IsTableLayout(parent.Type))
            {
                Point tableAbs = GetAbsoluteLocation(parent);
                GetTableCellAt(parent, logical: new Point(tableAbs.X + x, tableAbs.Y + y), out int row, out int col);
                if (IsTableCellOccupied(parent, row, col, except: null))
                {
                    // Find first free cell.
                    if (!TryFindFreeTableCell(parent, out row, out col))
                    {
                        _history.DiscardLastUndo();
                        MessageBox.Show(
                            this,
                            "All TableLayout cells are occupied.",
                            "F2B.Forms Designer",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }
                }

                item.Row = row;
                item.Column = col;
                DesignItem.ApplyTableCellBounds(parent, item);
            }

            siblings.Add(item);
            MarkDirty();
            RebuildTreePreserveSelection();
            SelectItem(item, syncTree: true);
            _canvas.Invalidate();
            CaptureStableSnapshot();
        }

        private DesignItem CreateDesignItem(string type, DesignItem parent, int x, int y)
        {
            return new DesignItem
            {
                Id = NextId(type.ToLowerInvariant()),
                Type = type,
                Text = GetDefaultText(type),
                X = FormControlType.IsTabPage(type) ? 0 : x,
                Y = FormControlType.IsTabPage(type) ? 0 : y,
                Width = Snap(GetDefaultWidth(type)),
                Height = Snap(GetDefaultHeight(type)),
                Parent = parent
            };
        }

        /// <summary>
        /// Resolve container (or root) and its absolute origin for a toolbox drop point.
        /// </summary>
        private DesignItem ResolveDropParent(Point logical, out Point parentOrigin)
        {
            parentOrigin = Point.Empty;
            DesignItem hit = HitTest(logical);
            if (hit == null)
            {
                return null;
            }

            if (FormControlType.IsTabControl(hit.Type))
            {
                DesignItem page = GetSelectedTabPage(hit);
                parentOrigin = GetTabContentAbsolute(hit);
                return page;
            }

            if (FormControlType.IsTabPage(hit.Type))
            {
                parentOrigin = hit.Parent == null ? Point.Empty : GetTabContentAbsolute(hit.Parent);
                return hit;
            }

            if (FormControlType.IsContainer(hit.Type))
            {
                parentOrigin = GetAbsoluteLocation(hit);
                return hit;
            }

            if (hit.Parent != null)
            {
                if (FormControlType.IsTabPage(hit.Parent.Type) && hit.Parent.Parent != null)
                {
                    parentOrigin = GetTabContentAbsolute(hit.Parent.Parent);
                    return hit.Parent;
                }

                parentOrigin = GetAbsoluteLocation(hit.Parent);
                return hit.Parent;
            }

            return null;
        }

        private DesignItem FindTabControlAtPoint(Point logical)
        {
            DesignItem hit = HitTest(logical);
            if (hit == null)
            {
                return null;
            }

            if (FormControlType.IsTabControl(hit.Type))
            {
                return hit;
            }

            if (FormControlType.IsTabPage(hit.Type))
            {
                return hit.Parent;
            }

            DesignItem current = hit.Parent;
            while (current != null)
            {
                if (FormControlType.IsTabControl(current.Type))
                {
                    return current;
                }

                current = current.Parent;
            }

            return null;
        }

        private string NextId(string prefix)
        {
            string id = prefix + _idCounter++;
            while (FindById(_roots, id) != null)
            {
                id = prefix + _idCounter++;
            }

            return id;
        }

        private DesignItem FindTabControlForNewPage()
        {
            if (_selected == null)
            {
                return null;
            }

            if (FormControlType.IsTabControl(_selected.Type))
            {
                return _selected;
            }

            if (FormControlType.IsTabPage(_selected.Type))
            {
                return _selected.Parent;
            }

            return null;
        }

        /// <summary>
        /// Container / active TabPage selected → add into that host.
        /// TabControl selected → add into its selected TabPage.
        /// Non-container selected → add as sibling under same parent (or root).
        /// </summary>
        private DesignItem GetAddTargetParent(string addingType = null)
        {
            if (_selected == null)
            {
                return null;
            }

            if (FormControlType.IsTabPage(addingType))
            {
                return FindTabControlForNewPage();
            }

            if (FormControlType.IsTabControl(_selected.Type))
            {
                return GetSelectedTabPage(_selected) ?? _selected;
            }

            if (FormControlType.IsContainer(_selected.Type))
            {
                return _selected;
            }

            if (_selected.Parent != null && FormControlType.IsTabPage(_selected.Parent.Type))
            {
                return _selected.Parent;
            }

            return _selected.Parent;
        }

        private static DesignItem GetSelectedTabPage(DesignItem tabControl)
        {
            if (tabControl == null || tabControl.Children == null || tabControl.Children.Count == 0)
            {
                return null;
            }

            int index = tabControl.SelectedIndex;
            if (index < 0 || index >= tabControl.Children.Count)
            {
                index = 0;
            }

            return tabControl.Children[index];
        }

        private void DeleteSelected()
        {
            List<DesignItem> toDelete = GetTopLevelSelection();
            if (toDelete.Count == 0)
            {
                return;
            }

            foreach (DesignItem item in toDelete)
            {
                if (FormControlType.IsTabPage(item.Type)
                    && item.Parent != null
                    && item.Parent.Children.Count <= 1)
                {
                    MessageBox.Show(
                        this,
                        "A TabControl must keep at least one TabPage.",
                        "F2B.Forms Designer",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                // Also block deleting every page of a TabControl in one multi-delete.
                if (FormControlType.IsTabPage(item.Type) && item.Parent != null)
                {
                    int deletingPages = toDelete.Count(
                        x => x.Parent == item.Parent && FormControlType.IsTabPage(x.Type));
                    if (deletingPages >= item.Parent.Children.Count)
                    {
                        MessageBox.Show(
                            this,
                            "A TabControl must keep at least one TabPage.",
                            "F2B.Forms Designer",
                            MessageBoxButtons.OK,
                            MessageBoxIcon.Information);
                        return;
                    }
                }
            }

            PushHistoryBeforeEdit();
            DesignItem focusAfter = null;
            foreach (DesignItem item in toDelete)
            {
                DesignItem parent = item.Parent;
                IList<DesignItem> list = parent == null ? _roots : parent.Children;
                list.Remove(item);
                if (parent != null && FormControlType.IsTabControl(parent.Type))
                {
                    if (parent.SelectedIndex >= parent.Children.Count)
                    {
                        parent.SelectedIndex = Math.Max(0, parent.Children.Count - 1);
                    }
                }

                if (focusAfter == null)
                {
                    focusAfter = parent;
                }
            }

            MarkDirty();
            RebuildTreePreserveSelection();
            if (focusAfter != null && FindById(_roots, focusAfter.Id) != null)
            {
                SelectItem(focusAfter, syncTree: true);
            }
            else
            {
                SelectFormRoot(syncTree: true);
            }

            _canvas.Invalidate();
            CaptureStableSnapshot();
        }

        private void SelectItem(DesignItem item, bool syncTree)
        {
            if (item == null)
            {
                SelectFormRoot(syncTree);
                return;
            }

            _selection.Clear();
            _selection.Add(item);
            _selected = item;
            EnsureTabSelection(item);
            BindPropertyGridToSelection();
            UpdateContextLabel();

            if (syncTree)
            {
                SelectTreeNode(item);
            }

            _canvas.Invalidate();
        }

        private void ToggleSelection(DesignItem item, bool syncTree)
        {
            if (item == null)
            {
                return;
            }

            if (FormControlType.IsTabPage(item.Type))
            {
                // TabPage stays single-select for page switching clarity.
                SelectItem(item, syncTree);
                return;
            }

            if (_selection.Contains(item))
            {
                _selection.Remove(item);
            }
            else
            {
                _selection.Add(item);
                EnsureTabSelection(item);
            }

            _selected = _selection.Count > 0 ? _selection[_selection.Count - 1] : null;
            BindPropertyGridToSelection();
            UpdateContextLabel();
            if (syncTree)
            {
                if (_selected != null)
                {
                    SelectTreeNode(_selected);
                }
                else if (_controlTree.Nodes.Count > 0)
                {
                    _syncingTree = true;
                    try
                    {
                        _controlTree.SelectedNode = _controlTree.Nodes[0];
                    }
                    finally
                    {
                        _syncingTree = false;
                    }
                }
            }

            _canvas.Invalidate();
        }

        private void BindPropertyGridToSelection()
        {
            if (_selection.Count == 0)
            {
                _propertyGrid.SelectedObjects = null;
                _propertyGrid.SelectedObject = _formSettings;
            }
            else if (_selection.Count == 1)
            {
                _propertyGrid.SelectedObjects = null;
                _propertyGrid.SelectedObject = null;
                _propertyGrid.SelectedObject = _selection[0];
            }
            else
            {
                _propertyGrid.SelectedObject = null;
                _propertyGrid.SelectedObjects = _selection.Cast<object>().ToArray();
            }

            if (_isViewer)
            {
                MakePropertyGridSelectionReadOnly();
                _propertyGrid.Refresh();
            }
        }

        private void MakePropertyGridSelectionReadOnly()
        {
            foreach (DesignItem item in _selection)
            {
                if (item != null)
                {
                    item.ViewOnlyProperties = true;
                }
            }

            TypeDescriptor.AddAttributes(_formSettings, new ReadOnlyAttribute(true));

            object[] targets = _propertyGrid.SelectedObjects;
            if (targets != null && targets.Length > 0)
            {
                _propertyGrid.SelectedObjects = targets;
                return;
            }

            object single = _propertyGrid.SelectedObject;
            if (single != null)
            {
                _propertyGrid.SelectedObject = single;
            }
        }

        private bool IsItemSelected(DesignItem item)
        {
            return item != null && _selection.Contains(item);
        }

        /// <summary>
        /// Selection items whose parent is not also selected (avoid duplicating nested trees).
        /// </summary>
        private List<DesignItem> GetTopLevelSelection()
        {
            var result = new List<DesignItem>();
            foreach (DesignItem item in _selection)
            {
                if (item == null)
                {
                    continue;
                }

                bool ancestorSelected = false;
                DesignItem parent = item.Parent;
                while (parent != null)
                {
                    if (_selection.Contains(parent))
                    {
                        ancestorSelected = true;
                        break;
                    }

                    parent = parent.Parent;
                }

                if (!ancestorSelected)
                {
                    result.Add(item);
                }
            }

            return result;
        }

        private bool CopySelection()
        {
            List<DesignItem> tops = GetTopLevelSelection();
            if (tops.Count == 0)
            {
                return false;
            }

            var entries = new List<DesignClipboardItem>();
            foreach (DesignItem item in tops)
            {
                entries.Add(new DesignClipboardItem
                {
                    // Deep-clone so later edits / paste cannot alias the live tree.
                    Definition = CloneControlDefinition(item.ToDefinition()),
                    ParentId = item.Parent == null ? null : item.Parent.Id,
                    X = item.X,
                    Y = item.Y
                });
            }

            _clipboard.Set(entries);
            return true;
        }

        private bool PasteClipboard(bool intoCurrentScope = true)
        {
            if (!_clipboard.HasContent)
            {
                return false;
            }

            _clipboard.PasteCount++;
            int offset = GridSize * _clipboard.PasteCount;
            PushHistoryBeforeEdit();
            var pasted = new List<DesignItem>();

            // Avoid PropertyGrid side-effects pushing extra undo entries mid-paste.
            _suspendHistory = true;
            try
            {
                foreach (DesignClipboardItem entry in _clipboard.Items)
                {
                    if (entry == null || entry.Definition == null)
                    {
                        continue;
                    }

                    DesignItem created = PasteClipboardEntry(entry, offset, intoCurrentScope);
                    if (created != null)
                    {
                        pasted.Add(created);
                    }
                }

                if (pasted.Count == 0)
                {
                    _history.DiscardLastUndo();
                    return false;
                }

                MarkDirty();
                RebuildTreePreserveSelection();
                _selection.Clear();
                _selection.AddRange(pasted);
                _selected = pasted[pasted.Count - 1];
                BindPropertyGridToSelection();
                UpdateContextLabel();
                SelectTreeNode(_selected);
                _canvas.Invalidate();
            }
            finally
            {
                _suspendHistory = false;
            }

            CaptureStableSnapshot();
            return true;
        }

        private DesignItem PasteClipboardEntry(DesignClipboardItem entry, int offset, bool intoCurrentScope)
        {
            string type = entry.Definition.Type;
            DesignItem parent = intoCurrentScope
                ? GetAddTargetParent(type)
                : ResolveClipboardParent(entry, type);

            if (FormControlType.IsTabPage(type))
            {
                if (parent == null || !FormControlType.IsTabControl(parent.Type))
                {
                    parent = FindTabControlForNewPage();
                }

                if (parent == null)
                {
                    return null;
                }

                DesignItem page = CloneFromDefinition(entry.Definition, parent);
                page.X = 0;
                page.Y = 0;
                parent.Children.Add(page);
                parent.SelectedIndex = parent.Children.Count - 1;
                return page;
            }

            IList<DesignItem> siblings = parent == null ? _roots : parent.Children;
            DesignItem item = CloneFromDefinition(entry.Definition, parent);
            item.X = Snap(Math.Max(0, entry.X + offset));
            item.Y = Snap(Math.Max(0, entry.Y + offset));
            siblings.Add(item);
            return item;
        }

        private DesignItem ResolveClipboardParent(DesignClipboardItem entry, string type)
        {
            DesignItem parent = string.IsNullOrEmpty(entry.ParentId)
                ? null
                : FindById(_roots, entry.ParentId);

            if (FormControlType.IsTabPage(type))
            {
                return parent;
            }

            if (parent != null && FormControlType.IsTabControl(parent.Type))
            {
                return GetSelectedTabPage(parent) ?? parent;
            }

            if (parent != null
                && !FormControlType.IsTabPage(parent.Type)
                && !FormControlType.IsContainer(parent.Type)
                && !FormControlType.IsTabControl(parent.Type))
            {
                return parent.Parent;
            }

            return parent;
        }

        private DesignItem CloneFromDefinition(ControlDefinition definition, DesignItem parent)
        {
            ControlDefinition clonedDef = CloneControlDefinition(definition);
            DesignItem item = DesignItem.FromDefinition(clonedDef, parent);
            HashSet<string> usedIds = CollectAllIds(_roots);
            ReassignIdsRecursive(item, usedIds);
            return item;
        }

        private static ControlDefinition CloneControlDefinition(ControlDefinition definition)
        {
            if (definition == null)
            {
                return null;
            }

            return JsonConvert.DeserializeObject<ControlDefinition>(
                JsonConvert.SerializeObject(definition));
        }

        private static HashSet<string> CollectAllIds(IEnumerable<DesignItem> items)
        {
            var used = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            CollectAllIdsRecursive(items, used);
            return used;
        }

        private static void CollectAllIdsRecursive(IEnumerable<DesignItem> items, HashSet<string> used)
        {
            if (items == null)
            {
                return;
            }

            foreach (DesignItem item in items)
            {
                if (item == null)
                {
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(item.Id))
                {
                    used.Add(item.Id.Trim());
                }

                CollectAllIdsRecursive(item.Children, used);
            }
        }

        private void ReassignIdsRecursive(DesignItem item, HashSet<string> usedIds)
        {
            if (item == null)
            {
                return;
            }

            string baseId = SanitizeIdBase(item.Id);
            if (string.IsNullOrEmpty(baseId))
            {
                baseId = string.IsNullOrWhiteSpace(item.Type)
                    ? "control"
                    : item.Type.ToLowerInvariant();
            }

            item.Id = AllocateUniqueId(baseId, usedIds);
            if (item.Children == null)
            {
                return;
            }

            foreach (DesignItem child in item.Children)
            {
                child.Parent = item;
                ReassignIdsRecursive(child, usedIds);
            }
        }

        private static string SanitizeIdBase(string id)
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                return null;
            }

            string trimmed = id.Trim();
            // Strip trailing _2 / _3... from previous paste generations.
            int underscore = trimmed.LastIndexOf('_');
            if (underscore > 0 && underscore < trimmed.Length - 1)
            {
                string suffix = trimmed.Substring(underscore + 1);
                int n;
                if (int.TryParse(suffix, out n) && n >= 2)
                {
                    return trimmed.Substring(0, underscore);
                }
            }

            return trimmed;
        }

        private string AllocateUniqueId(string baseId, HashSet<string> usedIds)
        {
            string candidate = baseId;
            int n = 2;
            while (!usedIds.Add(candidate))
            {
                candidate = baseId + "_" + n;
                n++;
            }

            // Keep NextId counter moving for toolbox-created controls.
            _idCounter++;
            return candidate;
        }

        private bool DuplicateSelection()
        {
            if (!CopySelection())
            {
                return false;
            }

            // Ctrl+D stays under the original parent(s); Ctrl+V follows the current scope.
            return PasteClipboard(intoCurrentScope: false);
        }

        private void UpdateContextLabel()
        {
            if (_isViewer || _contextLabel == null)
            {
                return;
            }

            DesignItem target = GetAddTargetParent();
            if (target == null)
            {
                _contextLabel.Text = "Add target: Form (root)";
            }
            else
            {
                _contextLabel.Text = "Add target: " + target.Type + " '" + target.Id + "'";
            }
        }

        private void EnsureTabSelection(DesignItem item)
        {
            if (item == null)
            {
                return;
            }

            if (FormControlType.IsTabPage(item.Type) && item.Parent != null && FormControlType.IsTabControl(item.Parent.Type))
            {
                int index = item.Parent.Children.IndexOf(item);
                if (index >= 0 && item.Parent.SelectedIndex != index)
                {
                    item.Parent.SelectedIndex = index;
                }
            }
        }

        private void RebuildTreePreserveSelection()
        {
            string selectedId = _selected == null ? null : _selected.Id;
            _syncingTree = true;
            try
            {
                _controlTree.BeginUpdate();
                _controlTree.Nodes.Clear();
                TreeNode formNode = _controlTree.Nodes.Add("form (Form)");
                formNode.Tag = null;
                foreach (DesignItem item in _roots)
                {
                    formNode.Nodes.Add(CreateTreeNode(item));
                }

                _controlTree.ExpandAll();

                if (!string.IsNullOrEmpty(selectedId))
                {
                    TreeNode node = FindTreeNode(formNode, selectedId);
                    if (node != null)
                    {
                        _controlTree.SelectedNode = node;
                    }
                }
                else
                {
                    _controlTree.SelectedNode = formNode;
                }
            }
            finally
            {
                _controlTree.EndUpdate();
                _syncingTree = false;
            }

            UpdateContextLabel();
        }

        private static TreeNode CreateTreeNode(DesignItem item)
        {
            var node = new TreeNode(item.Display) { Tag = item };
            if (item.Children != null)
            {
                foreach (DesignItem child in item.Children)
                {
                    child.Parent = item;
                    node.Nodes.Add(CreateTreeNode(child));
                }
            }

            return node;
        }

        private void SelectTreeNode(DesignItem item)
        {
            if (item == null || _controlTree.Nodes.Count == 0)
            {
                return;
            }

            _syncingTree = true;
            try
            {
                TreeNode node = FindTreeNode(_controlTree.Nodes[0], item.Id);
                if (node != null)
                {
                    _controlTree.SelectedNode = node;
                }
            }
            finally
            {
                _syncingTree = false;
            }
        }

        private static TreeNode FindTreeNode(TreeNode node, string id)
        {
            if (node.Tag is DesignItem item && string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
            {
                return node;
            }

            foreach (TreeNode child in node.Nodes)
            {
                TreeNode found = FindTreeNode(child, id);
                if (found != null)
                {
                    return found;
                }
            }

            return null;
        }

        private void OpenForm()
        {
            using (var dialog = new OpenFileDialog())
            {
                dialog.Filter = "Form JSON (*.json)|*.json|All files (*.*)|*.*";
                if (dialog.ShowDialog(this) != DialogResult.OK)
                {
                    return;
                }

                FormDefinition def = FormJsonLoader.LoadFromFile(dialog.FileName);
                _roots.Clear();
                _formSettings.Title = def.Title ?? "Form";
                _formSettings.Width = def.Width > 0 ? def.Width : 640;
                _formSettings.Height = def.Height > 0 ? def.Height : 480;
                _formSettings.AllowResize = def.AllowResize;
                if (def.Controls != null)
                {
                    foreach (ControlDefinition c in def.Controls)
                    {
                        _roots.Add(DesignItem.FromDefinition(c, parent: null));
                    }
                }

                _currentPath = dialog.FileName;
                RebuildTreePreserveSelection();
                SelectFormRoot(syncTree: true);
                UpdateDesignAreaSize();
                ClearDirty();
                ResetHistory();
            }
        }

        /// <summary>
        /// Returns false if save was cancelled or failed.
        /// </summary>
        private bool SaveForm(bool saveAs)
        {
            if (_isViewer)
            {
                return true;
            }

            if (saveAs || string.IsNullOrEmpty(_currentPath))
            {
                using (var dialog = new SaveFileDialog())
                {
                    dialog.Filter = "Form JSON (*.json)|*.json";
                    dialog.FileName = string.IsNullOrEmpty(_currentPath) ? "form.json" : Path.GetFileName(_currentPath);
                    if (dialog.ShowDialog(this) != DialogResult.OK)
                    {
                        return false;
                    }

                    _currentPath = dialog.FileName;
                }
            }

            FormDefinition def = BuildDefinition();
            File.WriteAllText(_currentPath, FormJsonLoader.ToJson(def));
            ClearDirty(); // clears title dirty marker (*)
            return true;
        }

        private void Preview()
        {
            FormDefinition def = BuildDefinition();
            FormRenderResult rendered = FormRenderer.Render(def);
            rendered.Form.ShowDialog(this);
            rendered.Form.Dispose();
        }

        private FormDefinition BuildDefinition()
        {
            return new FormDefinition
            {
                SchemaVersion = "1.0",
                Id = "form",
                Title = _formSettings.Title,
                Width = _formSettings.Width,
                Height = _formSettings.Height,
                AllowResize = _formSettings.AllowResize,
                Controls = _roots.Select(i => i.ToDefinition()).ToList()
            };
        }

        private void CanvasOnMouseDown(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                BeginPan(e.Location, fromCanvas: true);
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (!_canvas.Focused)
            {
                _canvas.Focus();
            }

            Point logical = ToLogical(e.Location);
            bool ctrl = (ModifierKeys & Keys.Control) == Keys.Control;

            if (_isViewer)
            {
                CanvasOnMouseDownViewOnly(logical, ctrl);
                return;
            }

            DesignItem tabHit = HitTestTabHeader(logical);
            if (tabHit != null)
            {
                if (ctrl)
                {
                    ToggleSelection(tabHit, syncTree: true);
                }
                else
                {
                    SelectItem(tabHit, syncTree: true);
                }

                return;
            }

            // Prefer resize grips of the primary selected control (single-select only).
            if (!ctrl
                && _selection.Count == 1
                && _selected != null
                && !FormControlType.IsTabPage(_selected.Type))
            {
                Rectangle selectedBounds = GetAbsoluteBounds(_selected);
                ResizeHandle handle = HitTestHandleZoomAware(selectedBounds, e.Location);
                if (handle != ResizeHandle.None && handle != ResizeHandle.Move)
                {
                    BeginDrag(_selected, handle, selectedBounds, logical);
                    return;
                }
            }

            // Form resize grips when the form root is selected.
            if (!ctrl && _selection.Count == 0)
            {
                ResizeHandle formHandle = HitTestHandleZoomAware(GetFormBounds(), e.Location);
                if (formHandle != ResizeHandle.None && formHandle != ResizeHandle.Move)
                {
                    BeginFormResize(formHandle, logical);
                    return;
                }
            }

            DesignItem hit = HitTest(logical);
            if (hit != null)
            {
                if (FormControlType.IsTabPage(hit.Type))
                {
                    if (ctrl)
                    {
                        ToggleSelection(hit, syncTree: true);
                    }
                    else
                    {
                        SelectItem(hit, syncTree: true);
                    }

                    return;
                }

                if (ctrl)
                {
                    ToggleSelection(hit, syncTree: true);
                    return;
                }

                Rectangle bounds = GetAbsoluteBounds(hit);
                ResizeHandle handle = HitTestHandleZoomAware(bounds, e.Location);
                if (handle == ResizeHandle.None || handle == ResizeHandle.Move)
                {
                    handle = ResizeHandle.Move;
                }

                // Clicking an already-selected item keeps multi-selection for batch move.
                if (!IsItemSelected(hit) || _selection.Count <= 1 || handle != ResizeHandle.Move)
                {
                    SelectItem(hit, syncTree: true);
                }
                else
                {
                    _selection.Remove(hit);
                    _selection.Add(hit);
                    _selected = hit;
                    BindPropertyGridToSelection();
                }

                BeginDrag(hit, handle, bounds, logical);
            }
            else if (!ctrl)
            {
                SelectFormRoot(syncTree: true);
                ResizeHandle formHandle = HitTestHandleZoomAware(GetFormBounds(), e.Location);
                if (formHandle != ResizeHandle.None && formHandle != ResizeHandle.Move)
                {
                    BeginFormResize(formHandle, logical);
                }
                else
                {
                    _canvas.Cursor = Cursors.Default;
                }
            }
        }

        /// <summary>
        /// Viewer: select controls only (no move / resize / form size edit).
        /// </summary>
        private void CanvasOnMouseDownViewOnly(Point logical, bool ctrl)
        {
            DesignItem tabHit = HitTestTabHeader(logical);
            if (tabHit != null)
            {
                if (ctrl)
                {
                    ToggleSelection(tabHit, syncTree: true);
                }
                else
                {
                    SelectItem(tabHit, syncTree: true);
                }

                return;
            }

            DesignItem hit = HitTest(logical);
            if (hit != null)
            {
                if (ctrl)
                {
                    ToggleSelection(hit, syncTree: true);
                }
                else
                {
                    SelectItem(hit, syncTree: true);
                }

                return;
            }

            if (!ctrl)
            {
                SelectFormRoot(syncTree: true);
            }
        }

        private DesignItem HitTestTabHeader(Point point)
        {
            return HitTestTabHeaderRecursive(_roots, Point.Empty, point);
        }

        private static DesignItem HitTestTabHeaderRecursive(IList<DesignItem> items, Point parentAbs, Point point)
        {
            if (items == null)
            {
                return null;
            }

            for (int i = items.Count - 1; i >= 0; i--)
            {
                DesignItem item = items[i];
                var abs = new Point(parentAbs.X + item.X, parentAbs.Y + item.Y);

                if (FormControlType.IsTabControl(item.Type))
                {
                    var bounds = new Rectangle(abs.X, abs.Y, item.Width, item.Height);
                    using (Font font = item.CreatePaintFont())
                    {
                        int tabIndex = DesignSurfacePainter.HitTestTabHeader(
                            bounds,
                            GetTabTitles(item),
                            font,
                            point);
                        if (tabIndex >= 0 && tabIndex < item.Children.Count)
                        {
                            item.SelectedIndex = tabIndex;
                            return item.Children[tabIndex];
                        }
                    }
                }

                if (item.Children != null && item.Children.Count > 0)
                {
                    if (FormControlType.IsTabControl(item.Type))
                    {
                        DesignItem page = GetSelectedTabPage(item);
                        if (page != null)
                        {
                            Point contentAbs = GetTabContentAbsolute(item);
                            DesignItem nested = HitTestTabHeaderRecursive(page.Children, contentAbs, point);
                            if (nested != null)
                            {
                                return nested;
                            }
                        }
                    }
                    else
                    {
                        DesignItem nested = HitTestTabHeaderRecursive(item.Children, abs, point);
                        if (nested != null)
                        {
                            return nested;
                        }
                    }
                }
            }

            return null;
        }

        private static List<string> GetTabTitles(DesignItem tabControl)
        {
            var titles = new List<string>();
            if (tabControl == null || tabControl.Children == null)
            {
                return titles;
            }

            foreach (DesignItem page in tabControl.Children)
            {
                titles.Add(string.IsNullOrWhiteSpace(page.Text) ? page.Id : page.Text);
            }

            return titles;
        }

        private static Point GetTabContentAbsolute(DesignItem tabControl)
        {
            Point abs = GetAbsoluteLocation(tabControl);
            Rectangle content = DesignSurfacePainter.GetTabContentBounds(
                new Rectangle(abs.X, abs.Y, tabControl.Width, tabControl.Height));
            return new Point(content.X, content.Y);
        }

        private void BeginDrag(DesignItem item, ResizeHandle handle, Rectangle absBounds, Point logicalMouse)
        {
            if (!_dragHistoryPushed)
            {
                PushHistoryBeforeEdit();
                _dragHistoryPushed = true;
            }

            _selected = item;
            _dragging = true;
            _draggingForm = false;
            _draggingMultiMove = handle == ResizeHandle.Move && _selection.Count > 1;
            _dragHandle = handle;
            _dragStartBoundsAbs = absBounds;
            _dragStartMouse = logicalMouse;
            _dragStartAbsByItem.Clear();
            if (_draggingMultiMove)
            {
                foreach (DesignItem selected in GetTopLevelSelection())
                {
                    if (selected == null || FormControlType.IsTabPage(selected.Type))
                    {
                        continue;
                    }

                    _dragStartAbsByItem[selected] = GetAbsoluteBounds(selected);
                }
            }
            else if (item != null)
            {
                _dragStartAbsByItem[item] = absBounds;
            }

            _canvas.Cursor = DesignSurfacePainter.GetCursor(handle);
            _canvas.Invalidate();
        }

        private void BeginFormResize(ResizeHandle handle, Point logicalMouse)
        {
            if (!_dragHistoryPushed)
            {
                PushHistoryBeforeEdit();
                _dragHistoryPushed = true;
            }

            _selected = null;
            _dragging = true;
            _draggingForm = true;
            _dragHandle = handle;
            _dragStartBoundsAbs = GetFormBounds();
            _dragStartMouse = logicalMouse;
            _propertyGrid.SelectedObject = _formSettings;
            _canvas.Cursor = DesignSurfacePainter.GetCursor(handle);
            _canvas.Invalidate();
        }

        private void CanvasOnMouseMove(object sender, MouseEventArgs e)
        {
            if (_panning)
            {
                ApplyPan(_viewport.PointToClient(_canvas.PointToScreen(e.Location)));
                return;
            }

            Point logical = ToLogical(e.Location);
            if (!_dragging)
            {
                UpdateHoverCursor(e.Location);
                return;
            }

            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            if (_draggingForm)
            {
                ApplyFormResizeDrag(logical);
                return;
            }

            if (_selected == null)
            {
                return;
            }

            if (_dragHandle == ResizeHandle.Move)
            {
                ApplyMoveDrag(logical);
            }
            else
            {
                ApplyResizeDrag(logical);
            }
        }

        private void UpdateHoverCursor(Point surfacePoint)
        {
            if (_isViewer)
            {
                _canvas.Cursor = Cursors.Default;
                return;
            }

            if (_selection.Count == 1 && _selected != null && !FormControlType.IsTabPage(_selected.Type))
            {
                ResizeHandle handle = HitTestHandleZoomAware(GetAbsoluteBounds(_selected), surfacePoint);
                _canvas.Cursor = DesignSurfacePainter.GetCursor(handle);
                return;
            }

            if (_selection.Count > 1)
            {
                foreach (DesignItem item in _selection)
                {
                    if (item == null || FormControlType.IsTabPage(item.Type))
                    {
                        continue;
                    }

                    if (GetAbsoluteBounds(item).Contains(ToLogical(surfacePoint)))
                    {
                        _canvas.Cursor = Cursors.SizeAll;
                        return;
                    }
                }
            }

            if (_selection.Count == 0)
            {
                ResizeHandle formHandle = HitTestHandleZoomAware(GetFormBounds(), surfacePoint);
                if (formHandle != ResizeHandle.None && formHandle != ResizeHandle.Move)
                {
                    _canvas.Cursor = DesignSurfacePainter.GetCursor(formHandle);
                    return;
                }
            }

            _canvas.Cursor = Cursors.Default;
        }

        private void ApplyMoveDrag(Point mouse)
        {
            int dx = mouse.X - _dragStartMouse.X;
            int dy = mouse.Y - _dragStartMouse.Y;
            int primaryAbsX = Snap(Math.Max(0, _dragStartBoundsAbs.X + dx));
            int primaryAbsY = Snap(Math.Max(0, _dragStartBoundsAbs.Y + dy));
            int deltaX = primaryAbsX - _dragStartBoundsAbs.X;
            int deltaY = primaryAbsY - _dragStartBoundsAbs.Y;
            if (deltaX == 0 && deltaY == 0)
            {
                return;
            }

            bool changed = false;
            IEnumerable<KeyValuePair<DesignItem, Rectangle>> targets = _dragStartAbsByItem.Count > 0
                ? _dragStartAbsByItem
                : new[] { new KeyValuePair<DesignItem, Rectangle>(_selected, _dragStartBoundsAbs) };

            foreach (KeyValuePair<DesignItem, Rectangle> pair in targets)
            {
                DesignItem item = pair.Key;
                if (item == null || FormControlType.IsTabPage(item.Type))
                {
                    continue;
                }

                Point parentAbs = item.Parent == null ? Point.Empty : GetAbsoluteLocation(item.Parent);
                int absX = Snap(Math.Max(0, pair.Value.X + deltaX));
                int absY = Snap(Math.Max(0, pair.Value.Y + deltaY));
                int newX = Math.Max(0, absX - parentAbs.X);
                int newY = Math.Max(0, absY - parentAbs.Y);
                if (newX == item.X && newY == item.Y)
                {
                    continue;
                }

                item.X = newX;
                item.Y = newY;
                changed = true;
            }

            if (!changed)
            {
                return;
            }

            MarkDirty();
            _canvas.Invalidate();
        }

        private void ApplyResizeDrag(Point mouse)
        {
            Rectangle next = BuildResizedBounds(_dragStartBoundsAbs, _dragHandle, _dragStartMouse, mouse);
            Point parentAbs = _selected.Parent == null ? Point.Empty : GetAbsoluteLocation(_selected.Parent);

            int newX = Math.Max(0, next.X - parentAbs.X);
            int newY = Math.Max(0, next.Y - parentAbs.Y);
            int newW = Math.Max(GridSize, next.Width);
            int newH = Math.Max(GridSize, next.Height);

            if (newX == _selected.X && newY == _selected.Y
                && newW == _selected.Width && newH == _selected.Height)
            {
                return;
            }

            _selected.X = newX;
            _selected.Y = newY;
            _selected.Width = newW;
            _selected.Height = newH;
            MarkDirty();
            _canvas.Invalidate();
        }

        private void ApplyFormResizeDrag(Point logicalMouse)
        {
            // Form is pinned at (0,0); map every grip to width/height only (10px snap).
            int dx = logicalMouse.X - _dragStartMouse.X;
            int dy = logicalMouse.Y - _dragStartMouse.Y;
            int right = _dragStartBoundsAbs.Right;
            int bottom = _dragStartBoundsAbs.Bottom;

            bool changeLeft = _dragHandle == ResizeHandle.W || _dragHandle == ResizeHandle.NW || _dragHandle == ResizeHandle.SW;
            bool changeRight = _dragHandle == ResizeHandle.E || _dragHandle == ResizeHandle.NE || _dragHandle == ResizeHandle.SE;
            bool changeTop = _dragHandle == ResizeHandle.N || _dragHandle == ResizeHandle.NW || _dragHandle == ResizeHandle.NE;
            bool changeBottom = _dragHandle == ResizeHandle.S || _dragHandle == ResizeHandle.SW || _dragHandle == ResizeHandle.SE;

            if (changeRight)
            {
                right = Snap(_dragStartBoundsAbs.Right + dx);
            }
            else if (changeLeft)
            {
                right = Snap(_dragStartBoundsAbs.Right - dx);
            }

            if (changeBottom)
            {
                bottom = Snap(_dragStartBoundsAbs.Bottom + dy);
            }
            else if (changeTop)
            {
                bottom = Snap(_dragStartBoundsAbs.Bottom - dy);
            }

            int newW = Math.Max(MinFormSize, right);
            int newH = Math.Max(MinFormSize, bottom);
            newW = Snap(newW);
            newH = Snap(newH);
            if (newW < MinFormSize)
            {
                newW = MinFormSize;
            }

            if (newH < MinFormSize)
            {
                newH = MinFormSize;
            }

            if (newW == _formSettings.Width && newH == _formSettings.Height)
            {
                return;
            }

            _formSettings.Width = newW;
            _formSettings.Height = newH;
            MarkDirty();
            UpdateDesignAreaSize();
            if (_propertyGrid.SelectedObject == _formSettings)
            {
                _propertyGrid.Refresh();
            }

            _canvas.Invalidate();
        }

        private Rectangle BuildResizedBounds(
            Rectangle start,
            ResizeHandle handle,
            Point startMouse,
            Point mouse)
        {
            int left = start.Left;
            int top = start.Top;
            int right = start.Right;
            int bottom = start.Bottom;
            int dx = mouse.X - startMouse.X;
            int dy = mouse.Y - startMouse.Y;

            bool changeLeft = handle == ResizeHandle.W || handle == ResizeHandle.NW || handle == ResizeHandle.SW;
            bool changeRight = handle == ResizeHandle.E || handle == ResizeHandle.NE || handle == ResizeHandle.SE;
            bool changeTop = handle == ResizeHandle.N || handle == ResizeHandle.NW || handle == ResizeHandle.NE;
            bool changeBottom = handle == ResizeHandle.S || handle == ResizeHandle.SW || handle == ResizeHandle.SE;

            if (changeLeft)
            {
                left = Snap(start.Left + dx);
            }

            if (changeRight)
            {
                right = Snap(start.Right + dx);
            }

            if (changeTop)
            {
                top = Snap(start.Top + dy);
            }

            if (changeBottom)
            {
                bottom = Snap(start.Bottom + dy);
            }

            // Enforce minimum size on the moving edge(s).
            if (right - left < GridSize)
            {
                if (changeLeft && !changeRight)
                {
                    left = right - GridSize;
                }
                else
                {
                    right = left + GridSize;
                }
            }

            if (bottom - top < GridSize)
            {
                if (changeTop && !changeBottom)
                {
                    top = bottom - GridSize;
                }
                else
                {
                    bottom = top + GridSize;
                }
            }

            if (left < 0)
            {
                left = 0;
            }

            if (top < 0)
            {
                top = 0;
            }

            return Rectangle.FromLTRB(left, top, right, bottom);
        }

        private void CanvasOnMouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Middle)
            {
                EndPan();
                return;
            }

            if (!_dragging)
            {
                return;
            }

            _dragging = false;
            _draggingForm = false;
            _draggingMultiMove = false;
            _dragHandle = ResizeHandle.None;
            _dragStartAbsByItem.Clear();
            FinishDragHistory();
            RelayoutAffectedTableLayouts();
            BindPropertyGridToSelection();
            _propertyGrid.Refresh();

            UpdateHoverCursor(e.Location);
            _canvas.Invalidate();
        }

        private static int Snap(int value)
        {
            if (value <= 0)
            {
                return 0;
            }

            return (int)(Math.Round(value / (double)GridSize) * GridSize);
        }

        private static Rectangle GetAbsoluteBounds(DesignItem item)
        {
            Point abs = GetAbsoluteLocation(item);
            return new Rectangle(abs.X, abs.Y, item.Width, item.Height);
        }

        private DesignItem HitTest(Point point)
        {
            // Deepest-first
            DesignItem hit = null;
            HitTestRecursive(_roots, Point.Empty, point, ref hit);
            return hit;
        }

        private static void HitTestRecursive(
            IList<DesignItem> items,
            Point parentAbs,
            Point point,
            ref DesignItem hit)
        {
            if (items == null)
            {
                return;
            }

            for (int i = 0; i < items.Count; i++)
            {
                DesignItem item = items[i];
                var abs = new Point(parentAbs.X + item.X, parentAbs.Y + item.Y);

                if (FormControlType.IsTabControl(item.Type))
                {
                    var rect = new Rectangle(abs.X, abs.Y, item.Width, item.Height);
                    if (rect.Contains(point))
                    {
                        hit = item;
                    }

                    DesignItem page = GetSelectedTabPage(item);
                    if (page != null)
                    {
                        // Selecting empty page body selects the TabPage.
                        Rectangle content = DesignSurfacePainter.GetTabContentBounds(rect);
                        if (content.Contains(point))
                        {
                            hit = page;
                        }

                        HitTestRecursive(page.Children, new Point(content.X, content.Y), point, ref hit);
                    }

                    continue;
                }

                if (FormControlType.IsTabPage(item.Type))
                {
                    // TabPages are hit via parent TabControl path.
                    continue;
                }

                var itemRect = new Rectangle(abs.X, abs.Y, item.Width, item.Height);
                if (itemRect.Contains(point))
                {
                    hit = item;
                }

                if (item.Children != null && item.Children.Count > 0)
                {
                    HitTestRecursive(item.Children, abs, point, ref hit);
                }
            }
        }

        private static Point GetAbsoluteLocation(DesignItem item)
        {
            if (item == null)
            {
                return Point.Empty;
            }

            if (FormControlType.IsTabPage(item.Type) && item.Parent != null && FormControlType.IsTabControl(item.Parent.Type))
            {
                return GetTabContentAbsolute(item.Parent);
            }

            int x = 0;
            int y = 0;
            DesignItem current = item;
            while (current != null)
            {
                if (FormControlType.IsTabPage(current.Type)
                    && current.Parent != null
                    && FormControlType.IsTabControl(current.Parent.Type))
                {
                    // Content origin already includes the TabControl's absolute location.
                    Point content = GetTabContentAbsolute(current.Parent);
                    x += content.X;
                    y += content.Y;
                    current = current.Parent.Parent;
                    continue;
                }

                x += current.X;
                y += current.Y;
                current = current.Parent;
            }

            return new Point(x, y);
        }

        private void CanvasOnPaint(object sender, PaintEventArgs e)
        {
            e.Graphics.Clear(Color.FromArgb(235, 235, 235));

            int w = Math.Max(1, _formSettings.Width);
            int h = Math.Max(1, _formSettings.Height);
            Bitmap buffer = EnsureFormPaintBuffer(w, h);
            using (Graphics g = Graphics.FromImage(buffer))
            {
                // Paint at 1:1 logical pixels. ScaleTransform breaks TextRenderer / tab metrics.
                g.SmoothingMode = SmoothingMode.None;
                g.TextRenderingHint = TextRenderingHint.ClearTypeGridFit;
                g.Clear(Color.White);
                g.DrawRectangle(Pens.DarkGray, 0, 0, w - 1, h - 1);

                // While dragging: others -> grid -> lifted control(s) with children.
                HashSet<DesignItem> lifted = GetLiftedDragItems();
                PaintItems(_roots, Point.Empty, g, skipSubtrees: lifted);

                if (_dragging || _draggingForm)
                {
                    DrawAlignmentGrid(g, w, h);
                }

                if (lifted != null)
                {
                    foreach (DesignItem item in lifted)
                    {
                        PaintLiftedItemGroup(item, g);
                    }
                }
                else if (_selection.Count == 0)
                {
                    DesignSurfacePainter.DrawSelectionChrome(g, new Rectangle(0, 0, w, h), showResizeHandles: !_isViewer);
                }
            }

            var dest = new Rectangle(
                0,
                0,
                Math.Max(1, (int)Math.Round(w * _zoom)),
                Math.Max(1, (int)Math.Round(h * _zoom)));
            e.Graphics.InterpolationMode = _zoom < 0.999f
                ? InterpolationMode.HighQualityBilinear
                : InterpolationMode.NearestNeighbor;
            e.Graphics.PixelOffsetMode = PixelOffsetMode.HighQuality;
            e.Graphics.DrawImage(buffer, dest);
        }

        /// <summary>
        /// Controls temporarily raised above the alignment grid while dragging.
        /// </summary>
        private HashSet<DesignItem> GetLiftedDragItems()
        {
            if (!_dragging || _draggingForm)
            {
                return null;
            }

            var lifted = new HashSet<DesignItem>();
            foreach (DesignItem item in GetTopLevelSelection())
            {
                if (item != null && !FormControlType.IsTabPage(item.Type))
                {
                    lifted.Add(item);
                }
            }

            return lifted.Count > 0 ? lifted : null;
        }

        private void PaintLiftedItemGroup(DesignItem item, Graphics g)
        {
            if (item == null)
            {
                return;
            }

            Point abs = GetAbsoluteLocation(item);
            var fakeParentAbs = new Point(abs.X - item.X, abs.Y - item.Y);
            PaintItems(new[] { item }, fakeParentAbs, g, skipSubtrees: null);
        }

        private Bitmap EnsureFormPaintBuffer(int width, int height)
        {
            if (_formPaintBuffer != null
                && _formPaintBuffer.Width == width
                && _formPaintBuffer.Height == height)
            {
                return _formPaintBuffer;
            }

            if (_formPaintBuffer != null)
            {
                _formPaintBuffer.Dispose();
                _formPaintBuffer = null;
            }

            _formPaintBuffer = new Bitmap(width, height, PixelFormat.Format32bppArgb);
            return _formPaintBuffer;
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _formPaintBuffer != null)
            {
                _formPaintBuffer.Dispose();
                _formPaintBuffer = null;
            }

            base.Dispose(disposing);
        }

        private static void DrawAlignmentGrid(Graphics g, int width, int height)
        {
            if (g == null || width <= 0 || height <= 0)
            {
                return;
            }

            using (var minorPen = new Pen(Color.FromArgb(55, 160, 160, 160)))
            using (var majorPen = new Pen(Color.FromArgb(90, 100, 100, 100)))
            {
                for (int x = 0; x <= width; x += GridSize)
                {
                    Pen pen = (x % (GridSize * 5) == 0) ? majorPen : minorPen;
                    g.DrawLine(pen, x, 0, x, height);
                }

                for (int y = 0; y <= height; y += GridSize)
                {
                    Pen pen = (y % (GridSize * 5) == 0) ? majorPen : minorPen;
                    g.DrawLine(pen, 0, y, width, y);
                }
            }
        }

        private void PaintItems(
            IList<DesignItem> items,
            Point parentAbs,
            Graphics g,
            ICollection<DesignItem> skipSubtrees)
        {
            if (items == null)
            {
                return;
            }

            foreach (DesignItem item in items)
            {
                if (skipSubtrees != null && skipSubtrees.Contains(item))
                {
                    continue;
                }

                var abs = new Point(parentAbs.X + item.X, parentAbs.Y + item.Y);
                var rect = new Rectangle(abs.X, abs.Y, item.Width, item.Height);
                bool selected = IsItemSelected(item)
                    || (FormControlType.IsTabControl(item.Type)
                        && _selected != null
                        && FormControlType.IsTabPage(_selected.Type)
                        && IsItemSelected(_selected)
                        && _selected.Parent == item);

                if (FormControlType.IsTabControl(item.Type))
                {
                    using (Font paintFont = item.CreatePaintFont())
                    {
                        DesignSurfacePainter.DrawTabControl(
                            g,
                            rect,
                            GetTabTitles(item),
                            item.SelectedIndex,
                            paintFont,
                            item.ForeColor,
                            item.BackColor);
                        if (selected)
                        {
                            DesignSurfacePainter.DrawSelectionChrome(g, rect, showResizeHandles: !_isViewer);
                        }
                    }

                    DesignItem page = GetSelectedTabPage(item);
                    if (page != null && page.Children != null && page.Children.Count > 0)
                    {
                        Rectangle content = DesignSurfacePainter.GetTabContentBounds(rect);
                        PaintItems(page.Children, new Point(content.X, content.Y), g, skipSubtrees);
                    }

                    continue;
                }

                string paintText = GetPaintText(item);
                using (Font paintFont = item.CreatePaintFont())
                {
                    DesignSurfacePainter.Draw(
                        g,
                        rect,
                        item.Type,
                        paintText,
                        item.Enabled,
                        item.Checked,
                        selected,
                        item.TextAlignH,
                        item.TextAlignV,
                        paintFont,
                        item.ForeColor,
                        item.BackColor,
                        showResizeHandles: !_isViewer);
                }

                if (item.Children != null && item.Children.Count > 0)
                {
                    PaintItems(item.Children, abs, g, skipSubtrees);
                }
            }
        }

        private static string GetPaintText(DesignItem item)
        {
            if (item == null)
            {
                return string.Empty;
            }

            if (IsComboBox(item) || IsListControl(item))
            {
                if (item.Items != null
                    && item.SelectedIndex >= 0
                    && item.SelectedIndex < item.Items.Count)
                {
                    return item.Items[item.SelectedIndex] ?? string.Empty;
                }

                if (item.Items != null && item.Items.Count > 0)
                {
                    return item.Items[0] ?? string.Empty;
                }
            }

            if (FormControlType.IsNumericUpDown(item.Type))
            {
                return string.IsNullOrWhiteSpace(item.Text) ? "0" : item.Text;
            }

            if (FormControlType.IsMaskedTextBox(item.Type))
            {
                return string.IsNullOrEmpty(item.Text) ? (item.Mask ?? string.Empty) : item.Text;
            }

            return item.Text ?? string.Empty;
        }

        private static bool IsComboBox(DesignItem item)
        {
            return item != null
                && string.Equals(item.Type, FormControlType.ComboBox, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsListControl(DesignItem item)
        {
            return item != null
                && (string.Equals(item.Type, FormControlType.ListBox, StringComparison.OrdinalIgnoreCase)
                    || string.Equals(item.Type, FormControlType.CheckedListBox, StringComparison.OrdinalIgnoreCase));
        }

        private static string GetDefaultText(string type)
        {
            switch (type)
            {
                case FormControlType.Button:
                    return "Button";
                case FormControlType.Label:
                    return "Label";
                case FormControlType.CheckBox:
                    return "CheckBox";
                case FormControlType.RadioButton:
                    return "RadioButton";
                case FormControlType.TextBox:
                case FormControlType.TextArea:
                case FormControlType.ComboBox:
                case FormControlType.ListBox:
                case FormControlType.CheckedListBox:
                case FormControlType.MaskedTextBox:
                case FormControlType.NumericUpDown:
                case FormControlType.DatePicker:
                case FormControlType.DateTimePicker:
                case FormControlType.PictureBox:
                case FormControlType.Panel:
                case FormControlType.ScrollContainer:
                case FormControlType.TableLayout:
                case FormControlType.DataGrid:
                case FormControlType.TabControl:
                    return string.Empty;
                case FormControlType.GroupBox:
                    return "GroupBox";
                case FormControlType.TabPage:
                    return "Tab";
                default:
                    return type ?? string.Empty;
            }
        }

        private static int GetDefaultWidth(string type)
        {
            // All defaults are multiples of GridSize (10).
            switch (type)
            {
                case FormControlType.TextArea:
                    return 240;
                case FormControlType.ListBox:
                case FormControlType.CheckedListBox:
                    return 160;
                case FormControlType.PictureBox:
                    return 120;
                case FormControlType.Panel:
                case FormControlType.GroupBox:
                    return 200;
                case FormControlType.ScrollContainer:
                    return 300;
                case FormControlType.TableLayout:
                case FormControlType.DataGrid:
                    return 400;
                case FormControlType.TabControl:
                    return 320;
                case FormControlType.TabPage:
                    return 0;
                case FormControlType.Label:
                    return 80;
                case FormControlType.CheckBox:
                case FormControlType.RadioButton:
                    return 100;
                case FormControlType.Button:
                    return 90;
                case FormControlType.DateTimePicker:
                case FormControlType.MaskedTextBox:
                    return 160;
                case FormControlType.NumericUpDown:
                    return 100;
                default:
                    return 120;
            }
        }

        private static int GetDefaultHeight(string type)
        {
            // All defaults are multiples of GridSize (10).
            switch (type)
            {
                case FormControlType.TextArea:
                    return 80;
                case FormControlType.ListBox:
                case FormControlType.CheckedListBox:
                    return 100;
                case FormControlType.PictureBox:
                    return 120;
                case FormControlType.Panel:
                case FormControlType.GroupBox:
                    return 140;
                case FormControlType.ScrollContainer:
                case FormControlType.TableLayout:
                case FormControlType.DataGrid:
                    return 200;
                case FormControlType.TabControl:
                    return 220;
                case FormControlType.TabPage:
                    return 0;
                case FormControlType.ComboBox:
                case FormControlType.DatePicker:
                case FormControlType.DateTimePicker:
                    return 30;
                default:
                    return 30;
            }
        }

        private void RelayoutAffectedTableLayouts()
        {
            foreach (DesignItem item in _selection)
            {
                if (item == null)
                {
                    continue;
                }

                if (FormControlType.IsTableLayout(item.Type))
                {
                    DesignItem.RelayoutTableChildren(item);
                }
                else if (item.Parent != null && FormControlType.IsTableLayout(item.Parent.Type))
                {
                    DesignItem.ApplyTableCellBounds(item.Parent, item);
                }
            }
        }

        private void GetTableCellAt(DesignItem table, Point logical, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (table == null)
            {
                return;
            }

            Point abs = GetAbsoluteLocation(table);
            int rows = Math.Max(1, table.RowCount);
            int cols = Math.Max(1, table.ColumnCount);
            int cellW = Math.Max(1, table.Width / cols);
            int cellH = Math.Max(1, table.Height / rows);
            int localX = Math.Max(0, Math.Min(table.Width - 1, logical.X - abs.X));
            int localY = Math.Max(0, Math.Min(table.Height - 1, logical.Y - abs.Y));
            column = Math.Min(cols - 1, localX / cellW);
            row = Math.Min(rows - 1, localY / cellH);
        }

        private static bool IsTableCellOccupied(DesignItem table, int row, int column, DesignItem except)
        {
            return DesignItem.IsTableCellOccupied(table, row, column, except);
        }

        private static bool TryFindFreeTableCell(DesignItem table, out int row, out int column)
        {
            return DesignItem.TryFindFreeTableCell(table, out row, out column);
        }

        private static DesignItem FindById(IEnumerable<DesignItem> items, string id)
        {
            if (items == null)
            {
                return null;
            }

            foreach (DesignItem item in items)
            {
                if (string.Equals(item.Id, id, StringComparison.OrdinalIgnoreCase))
                {
                    return item;
                }

                DesignItem nested = FindById(item.Children, id);
                if (nested != null)
                {
                    return nested;
                }
            }

            return null;
        }
    }

    /// <summary>
    /// Panel with double-buffering to avoid grid/control flicker while dragging.
    /// </summary>
    internal sealed class DoubleBufferedPanel : Panel
    {
        public DoubleBufferedPanel()
        {
            SetStyle(
                ControlStyles.AllPaintingInWmPaint
                | ControlStyles.UserPaint
                | ControlStyles.OptimizedDoubleBuffer
                | ControlStyles.ResizeRedraw,
                true);
            UpdateStyles();
        }

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            // Ensure MouseWheel event fires even when the panel is not focused.
            base.OnMouseWheel(e);
        }
    }

    /// <summary>
    /// Form-level properties shown in the Properties panel when the form root is selected.
    /// </summary>
    public sealed class FormDesignSettings
    {
        [Category("Layout")]
        [DisplayName("Id")]
        [ReadOnly(true)]
        public string Id
        {
            get { return "form"; }
        }

        [Category("Appearance")]
        [DisplayName("Title")]
        public string Title { get; set; } = "Sample Form";

        [Category("Layout")]
        [DisplayName("Width")]
        public int Width { get; set; } = 600;

        [Category("Layout")]
        [DisplayName("Height")]
        public int Height { get; set; } = 800;

        [Category("Behavior")]
        [DisplayName("Allow Resize")]
        [Description("When false, the runtime form cannot be resized by dragging edges or maximizing.")]
        public bool AllowResize { get; set; } = true;
    }

    public sealed class DesignItem : ICustomTypeDescriptor
    {
        public DesignItem()
        {
            Children = new BindingList<DesignItem>();
        }

        [Category("Design")]
        [DisplayName("Id")]
        public string Id { get; set; }

        [Category("Design")]
        [DisplayName("Type")]
        [ReadOnly(true)]
        public string Type { get; set; }

        [Category("Appearance")]
        [DisplayName("Text")]
        [Editor(typeof(MultilineStringEditor), typeof(UITypeEditor))]
        public string Text { get; set; }

        [Category("Appearance")]
        [DisplayName("Text Align H")]
        [Description("Horizontal text alignment: Left, Center, Right.")]
        public TextHAlign TextAlignH { get; set; } = TextHAlign.Left;

        [Category("Appearance")]
        [DisplayName("Text Align V")]
        [Description("Vertical text alignment: Top, Middle, Bottom.")]
        public TextVAlign TextAlignV { get; set; } = TextVAlign.Middle;

        [Category("Font")]
        [DisplayName("Font Family")]
        public string FontFamily { get; set; } = FontStyleUtil.DefaultFamily;

        [Category("Font")]
        [DisplayName("Font Size")]
        public float FontSize { get; set; } = FontStyleUtil.DefaultSize;

        [Category("Font")]
        [DisplayName("Bold")]
        public bool FontBold { get; set; }

        [Category("Font")]
        [DisplayName("Italic")]
        public bool FontItalic { get; set; }

        [Category("Font")]
        [DisplayName("Underline")]
        public bool FontUnderline { get; set; }

        [Category("Appearance")]
        [DisplayName("Fore Color")]
        public Color ForeColor { get; set; } = Color.Empty;

        [Category("Appearance")]
        [DisplayName("Back Color")]
        public Color BackColor { get; set; } = Color.Empty;

        [Category("Layout")]
        [DisplayName("X")]
        public int X { get; set; }

        [Category("Layout")]
        [DisplayName("Y")]
        public int Y { get; set; }

        [Category("Table")]
        [DisplayName("Row Count")]
        public int RowCount { get; set; } = 3;

        [Category("Table")]
        [DisplayName("Column Count")]
        public int ColumnCount { get; set; } = 3;

        [Category("Table")]
        [DisplayName("Row")]
        public int Row { get; set; }

        [Category("Table")]
        [DisplayName("Column")]
        public int Column { get; set; }

        [Category("Layout")]
        [DisplayName("Width")]
        public int Width { get; set; }

        [Category("Layout")]
        [DisplayName("Height")]
        public int Height { get; set; }

        [Category("Behavior")]
        [DisplayName("Enabled")]
        public bool Enabled { get; set; } = true;

        [Category("Behavior")]
        [DisplayName("Visible")]
        public bool Visible { get; set; } = true;

        [Category("Appearance")]
        [DisplayName("Read Only")]
        public bool ReadOnly { get; set; }

        [Category("Appearance")]
        [DisplayName("Checked")]
        public bool Checked { get; set; }

        [Category("Data")]
        [DisplayName("Items")]
        public List<string> Items { get; set; }

        [Category("Data")]
        [DisplayName("Selected Index")]
        public int SelectedIndex { get; set; } = -1;

        [Category("Appearance")]
        [DisplayName("Mask")]
        public string Mask { get; set; }

        [Category("Data")]
        [DisplayName("Minimum")]
        public decimal Minimum { get; set; }

        [Category("Data")]
        [DisplayName("Maximum")]
        public decimal Maximum { get; set; } = 100;

        [Category("Data")]
        [DisplayName("Increment")]
        public decimal Increment { get; set; } = 1;

        [Category("Data")]
        [DisplayName("Decimal Places")]
        public int DecimalPlaces { get; set; }

        [Category("Appearance")]
        [DisplayName("Image Path")]
        public string ImagePath { get; set; }

        [Category("Appearance")]
        [DisplayName("Size Mode")]
        [Description("Normal | StretchImage | Zoom | CenterImage | AutoSize")]
        public string SizeMode { get; set; } = "Zoom";

        [Browsable(false)]
        public DesignItem Parent { get; set; }

        [Browsable(false)]
        public BindingList<DesignItem> Children { get; private set; }

        /// <summary>
        /// When true, PropertyGrid shows all applicable properties as read-only (Viewer mode).
        /// </summary>
        [Browsable(false)]
        public bool ViewOnlyProperties { get; set; }

        [Browsable(false)]
        public string Display
        {
            get { return Id + " (" + Type + ")"; }
        }

        public Font CreatePaintFont()
        {
            return FontStyleUtil.CreateFont(FontFamily, FontSize, FontBold, FontItalic, FontUnderline);
        }

        #region ICustomTypeDescriptor — show only properties valid for this control Type

        AttributeCollection ICustomTypeDescriptor.GetAttributes()
        {
            AttributeCollection typeAttrs = TypeDescriptor.GetAttributes(typeof(DesignItem));
            if (!ViewOnlyProperties)
            {
                return typeAttrs;
            }

            var merged = new Attribute[typeAttrs.Count + 1];
            typeAttrs.CopyTo(merged, 0);
            merged[merged.Length - 1] = new ReadOnlyAttribute(true);
            return new AttributeCollection(merged);
        }

        string ICustomTypeDescriptor.GetClassName()
        {
            return typeof(DesignItem).Name;
        }

        string ICustomTypeDescriptor.GetComponentName()
        {
            return Id;
        }

        TypeConverter ICustomTypeDescriptor.GetConverter()
        {
            return TypeDescriptor.GetConverter(typeof(DesignItem));
        }

        EventDescriptor ICustomTypeDescriptor.GetDefaultEvent()
        {
            return TypeDescriptor.GetDefaultEvent(typeof(DesignItem));
        }

        PropertyDescriptor ICustomTypeDescriptor.GetDefaultProperty()
        {
            return TypeDescriptor.GetDefaultProperty(typeof(DesignItem));
        }

        object ICustomTypeDescriptor.GetEditor(Type editorBaseType)
        {
            return TypeDescriptor.GetEditor(typeof(DesignItem), editorBaseType);
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents()
        {
            return TypeDescriptor.GetEvents(typeof(DesignItem));
        }

        EventDescriptorCollection ICustomTypeDescriptor.GetEvents(Attribute[] attributes)
        {
            return TypeDescriptor.GetEvents(typeof(DesignItem), attributes);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties()
        {
            return GetFilteredProperties(null);
        }

        PropertyDescriptorCollection ICustomTypeDescriptor.GetProperties(Attribute[] attributes)
        {
            return GetFilteredProperties(attributes);
        }

        object ICustomTypeDescriptor.GetPropertyOwner(PropertyDescriptor pd)
        {
            return this;
        }

        private PropertyDescriptorCollection GetFilteredProperties(Attribute[] attributes)
        {
            PropertyDescriptorCollection all = attributes == null
                ? TypeDescriptor.GetProperties(typeof(DesignItem))
                : TypeDescriptor.GetProperties(typeof(DesignItem), attributes);

            var filtered = new List<PropertyDescriptor>();
            foreach (PropertyDescriptor property in all)
            {
                if (!IsPropertyApplicable(property.Name))
                {
                    continue;
                }

                filtered.Add(ViewOnlyProperties
                    ? TypeDescriptor.CreateProperty(
                        typeof(DesignItem),
                        property,
                        new ReadOnlyAttribute(true))
                    : property);
            }

            return new PropertyDescriptorCollection(filtered.ToArray());
        }

        private bool IsPropertyApplicable(string propertyName)
        {
            // Structural / always-hidden
            if (propertyName == "Parent" || propertyName == "Children" || propertyName == "Display")
            {
                return false;
            }

            // Common to all controls
            if (propertyName == "Id" || propertyName == "Type"
                || propertyName == "Enabled" || propertyName == "Visible")
            {
                return true;
            }

            if (propertyName == "X" || propertyName == "Y"
                || propertyName == "Width" || propertyName == "Height")
            {
                // TabPage layout is owned by TabControl.
                if (FormControlType.IsTabPage(Type))
                {
                    return false;
                }

                // TableLayout children use Row/Column; size comes from the cell.
                if (Parent != null && FormControlType.IsTableLayout(Parent.Type)
                    && (propertyName == "X" || propertyName == "Y"
                        || propertyName == "Width" || propertyName == "Height"))
                {
                    return false;
                }

                return true;
            }

            string type = Type ?? string.Empty;
            switch (propertyName)
            {
                case "Text":
                    return type == FormControlType.Button
                        || type == FormControlType.Label
                        || type == FormControlType.TextBox
                        || type == FormControlType.TextArea
                        || type == FormControlType.CheckBox
                        || type == FormControlType.RadioButton
                        || type == FormControlType.MaskedTextBox
                        || type == FormControlType.NumericUpDown
                        || type == FormControlType.DatePicker
                        || type == FormControlType.DateTimePicker
                        || type == FormControlType.GroupBox
                        || type == FormControlType.TabPage;

                case "TextAlignH":
                    return type == FormControlType.Button
                        || type == FormControlType.Label
                        || type == FormControlType.TextBox
                        || type == FormControlType.TextArea
                        || type == FormControlType.MaskedTextBox
                        || type == FormControlType.CheckBox
                        || type == FormControlType.RadioButton;

                case "TextAlignV":
                    // WinForms TextBox only supports horizontal alignment.
                    return type == FormControlType.Button
                        || type == FormControlType.Label
                        || type == FormControlType.CheckBox
                        || type == FormControlType.RadioButton;

                case "FontFamily":
                case "FontSize":
                case "FontBold":
                case "FontItalic":
                case "FontUnderline":
                case "ForeColor":
                    return SupportsFont(type);

                case "BackColor":
                    return SupportsFont(type)
                        || FormControlType.IsContainer(type)
                        || FormControlType.IsTabControl(type)
                        || FormControlType.IsDataGrid(type)
                        || FormControlType.IsPictureBox(type);

                case "ReadOnly":
                    return type == FormControlType.TextBox
                        || type == FormControlType.TextArea
                        || type == FormControlType.MaskedTextBox;

                case "Checked":
                    return type == FormControlType.CheckBox
                        || type == FormControlType.RadioButton;

                case "Items":
                    return type == FormControlType.ComboBox
                        || type == FormControlType.ListBox
                        || type == FormControlType.CheckedListBox;

                case "SelectedIndex":
                    return type == FormControlType.ComboBox
                        || type == FormControlType.ListBox
                        || type == FormControlType.CheckedListBox
                        || type == FormControlType.TabControl;

                case "Mask":
                    return type == FormControlType.MaskedTextBox;

                case "Minimum":
                case "Maximum":
                case "Increment":
                case "DecimalPlaces":
                    return type == FormControlType.NumericUpDown;

                case "ImagePath":
                case "SizeMode":
                    return type == FormControlType.PictureBox;

                case "RowCount":
                case "ColumnCount":
                    return FormControlType.IsTableLayout(type);

                case "Row":
                case "Column":
                    return Parent != null && FormControlType.IsTableLayout(Parent.Type);

                default:
                    return false;
            }
        }

        #endregion

        public ControlDefinition ToDefinition()
        {
            var def = new ControlDefinition
            {
                Id = Id,
                Type = Type,
                Text = Text,
                X = X,
                Y = Y,
                Width = Width,
                Height = Height,
                Enabled = Enabled,
                Visible = Visible
            };

            if (SupportsTextAlign(Type))
            {
                def.TextAlignH = TextAlignH.ToString();
                if (SupportsTextAlignV(Type))
                {
                    def.TextAlignV = TextAlignV.ToString();
                }
            }

            if (SupportsFont(Type))
            {
                def.FontFamily = FontFamily;
                def.FontSize = FontSize;
                def.FontBold = FontBold;
                def.FontItalic = FontItalic;
                def.FontUnderline = FontUnderline;
                if (!ForeColor.IsEmpty)
                {
                    def.ForeColor = FontStyleUtil.ToHtmlColor(ForeColor);
                }
            }

            if ((SupportsFont(Type) || FormControlType.IsContainer(Type) || FormControlType.IsTabControl(Type))
                && !BackColor.IsEmpty)
            {
                def.BackColor = FontStyleUtil.ToHtmlColor(BackColor);
            }

            if (Type == FormControlType.TextBox || Type == FormControlType.TextArea
                || Type == FormControlType.MaskedTextBox)
            {
                def.ReadOnly = ReadOnly;
            }

            if (Type == FormControlType.CheckBox || Type == FormControlType.RadioButton)
            {
                def.Checked = Checked;
            }

            if (Type == FormControlType.ComboBox
                || Type == FormControlType.ListBox
                || Type == FormControlType.CheckedListBox)
            {
                def.Items = Items ?? new List<string>();
                if (SelectedIndex >= 0)
                {
                    def.SelectedIndex = SelectedIndex;
                }
            }

            if (Type == FormControlType.MaskedTextBox && !string.IsNullOrEmpty(Mask))
            {
                def.Mask = Mask;
            }

            if (Type == FormControlType.NumericUpDown)
            {
                def.Minimum = Minimum;
                def.Maximum = Maximum;
                def.Increment = Increment;
                def.DecimalPlaces = DecimalPlaces;
            }

            if (Type == FormControlType.PictureBox)
            {
                if (!string.IsNullOrWhiteSpace(ImagePath))
                {
                    def.ImagePath = ImagePath;
                }

                if (!string.IsNullOrWhiteSpace(SizeMode))
                {
                    def.SizeMode = SizeMode;
                }
            }

            if (FormControlType.IsTabControl(Type) && SelectedIndex >= 0)
            {
                def.SelectedIndex = SelectedIndex;
            }

            if (FormControlType.IsTableLayout(Type))
            {
                def.RowCount = Math.Max(1, RowCount);
                def.ColumnCount = Math.Max(1, ColumnCount);
            }

            if (Parent != null && FormControlType.IsTableLayout(Parent.Type))
            {
                def.Row = Row;
                def.Column = Column;
            }

            if (Type == FormControlType.TextArea)
            {
                def.ScrollBars = "Vertical";
            }

            if (FormControlType.IsTabPage(Type))
            {
                def.X = 0;
                def.Y = 0;
                def.Width = 0;
                def.Height = 0;
            }

            if (FormControlType.HasChildControls(Type))
            {
                def.Controls = Children.Select(c => c.ToDefinition()).ToList();
            }

            return def;
        }

        private static bool SupportsTextAlign(string type)
        {
            return type == FormControlType.Button
                || type == FormControlType.Label
                || type == FormControlType.TextBox
                || type == FormControlType.TextArea
                || type == FormControlType.MaskedTextBox
                || type == FormControlType.CheckBox
                || type == FormControlType.RadioButton;
        }

        private static bool SupportsTextAlignV(string type)
        {
            return type == FormControlType.Button
                || type == FormControlType.Label
                || type == FormControlType.CheckBox
                || type == FormControlType.RadioButton;
        }

        private static bool SupportsFont(string type)
        {
            return type == FormControlType.Button
                || type == FormControlType.Label
                || type == FormControlType.TextBox
                || type == FormControlType.TextArea
                || type == FormControlType.MaskedTextBox
                || type == FormControlType.NumericUpDown
                || type == FormControlType.CheckBox
                || type == FormControlType.RadioButton
                || type == FormControlType.ComboBox
                || type == FormControlType.ListBox
                || type == FormControlType.CheckedListBox
                || type == FormControlType.DatePicker
                || type == FormControlType.DateTimePicker
                || type == FormControlType.GroupBox
                || type == FormControlType.ScrollContainer
                || type == FormControlType.TableLayout
                || type == FormControlType.DataGrid
                || type == FormControlType.TabControl
                || type == FormControlType.TabPage;
        }

        public static DesignItem FromDefinition(ControlDefinition c, DesignItem parent)
        {
            var item = new DesignItem
            {
                Id = c.Id,
                Type = c.Type,
                Text = c.Text,
                X = c.X,
                Y = c.Y,
                Width = c.Width,
                Height = c.Height,
                Enabled = c.Enabled ?? true,
                Visible = c.Visible ?? true,
                ReadOnly = c.ReadOnly == true,
                Checked = c.Checked == true,
                Items = c.Items,
                SelectedIndex = c.SelectedIndex ?? (FormControlType.IsTabControl(c.Type) ? 0 : -1),
                Mask = c.Mask,
                Minimum = c.Minimum ?? 0m,
                Maximum = c.Maximum ?? 100m,
                Increment = c.Increment ?? 1m,
                DecimalPlaces = c.DecimalPlaces ?? 0,
                ImagePath = c.ImagePath,
                SizeMode = string.IsNullOrWhiteSpace(c.SizeMode) ? "Zoom" : c.SizeMode,
                RowCount = c.RowCount ?? 3,
                ColumnCount = c.ColumnCount ?? 3,
                Row = c.Row ?? 0,
                Column = c.Column ?? 0,
                TextAlignH = TextAlignUtil.ParseH(c.TextAlignH),
                TextAlignV = TextAlignUtil.ParseV(c.TextAlignV),
                FontFamily = string.IsNullOrWhiteSpace(c.FontFamily) ? FontStyleUtil.DefaultFamily : c.FontFamily,
                FontSize = c.FontSize ?? FontStyleUtil.DefaultSize,
                FontBold = c.FontBold == true,
                FontItalic = c.FontItalic == true,
                FontUnderline = c.FontUnderline == true,
                ForeColor = FontStyleUtil.ParseColor(c.ForeColor) ?? Color.Empty,
                BackColor = FontStyleUtil.ParseColor(c.BackColor) ?? Color.Empty,
                Parent = parent
            };

            if (c.Controls != null)
            {
                foreach (ControlDefinition child in c.Controls)
                {
                    item.Children.Add(FromDefinition(child, item));
                }
            }

            if (FormControlType.IsTableLayout(item.Type))
            {
                RelayoutTableChildren(item);
            }
            else if (parent != null && FormControlType.IsTableLayout(parent.Type))
            {
                ApplyTableCellBounds(parent, item);
            }

            return item;
        }

        public static void RelayoutTableChildren(DesignItem table)
        {
            if (table == null || table.Children == null)
            {
                return;
            }

            foreach (DesignItem child in table.Children)
            {
                ApplyTableCellBounds(table, child);
            }
        }

        public static void ApplyTableCellBounds(DesignItem table, DesignItem child)
        {
            if (table == null || child == null)
            {
                return;
            }

            int rows = Math.Max(1, table.RowCount);
            int cols = Math.Max(1, table.ColumnCount);
            int row = Math.Max(0, Math.Min(child.Row, rows - 1));
            int col = Math.Max(0, Math.Min(child.Column, cols - 1));
            child.Row = row;
            child.Column = col;

            int cellW = Math.Max(1, table.Width / cols);
            int cellH = Math.Max(1, table.Height / rows);
            child.X = col * cellW;
            child.Y = row * cellH;
            child.Width = cellW;
            child.Height = cellH;
        }

        public static bool IsTableCellOccupied(DesignItem table, int row, int column, DesignItem except)
        {
            if (table == null || table.Children == null)
            {
                return false;
            }

            foreach (DesignItem child in table.Children)
            {
                if (except != null && ReferenceEquals(child, except))
                {
                    continue;
                }

                if (child.Row == row && child.Column == column)
                {
                    return true;
                }
            }

            return false;
        }

        public static bool TryFindFreeTableCell(DesignItem table, out int row, out int column)
        {
            row = 0;
            column = 0;
            if (table == null)
            {
                return false;
            }

            int rows = Math.Max(1, table.RowCount);
            int cols = Math.Max(1, table.ColumnCount);
            for (int r = 0; r < rows; r++)
            {
                for (int c = 0; c < cols; c++)
                {
                    if (!IsTableCellOccupied(table, r, c, except: null))
                    {
                        row = r;
                        column = c;
                        return true;
                    }
                }
            }

            return false;
        }
    }
}
