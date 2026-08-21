using System;
using System.Drawing;
using System.Globalization;
using System.Runtime.InteropServices;
using System.Windows.Forms;

namespace F2B.Forms.Engine
{
    /// <summary>
    /// DateTimePicker that never opens the native MonthCalendar (which follows OS regional
    /// format, e.g. Chinese day names). Drop-down button / F4 / Alt+Down show a managed
    /// calendar rendered with <see cref="CalendarCulture"/> instead.
    /// </summary>
    internal sealed class CultureAwareDateTimePicker : DateTimePicker
    {
        private const int WmLButtonDown = 0x0201;
        private const int WmLButtonDblClk = 0x0203;
        private const int WmKeyDown = 0x0100;
        private const int WmSysKeyDown = 0x0104;
        private const int DtmFirst = 0x1000;
        private const int DtmCloseMonthCal = DtmFirst + 13;

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, int msg, IntPtr wParam, IntPtr lParam);

        private ToolStripDropDown _dropDown;
        private bool _suppressNativeDropDown;

        public string CalendarCulture { get; set; }

        protected override void WndProc(ref Message m)
        {
            // Intercept drop-button mouse clicks so the native calendar never opens.
            if (m.Msg == WmLButtonDown || m.Msg == WmLButtonDblClk)
            {
                int xy = m.LParam.ToInt32();
                var pt = new Point(xy & 0xFFFF, (xy >> 16) & 0xFFFF);
                if (IsOnDropDownButton(pt))
                {
                    Focus();
                    ToggleManagedCalendar();
                    return;
                }
            }

            // F4 / Alt+Down also open the native calendar — redirect to managed.
            if (m.Msg == WmKeyDown || m.Msg == WmSysKeyDown)
            {
                Keys key = (Keys)((int)m.WParam & 0xFFFF);
                bool alt = (ModifierKeys & Keys.Alt) == Keys.Alt;
                if (key == Keys.F4 || (key == Keys.Down && alt))
                {
                    ToggleManagedCalendar();
                    return;
                }
            }

            base.WndProc(ref m);
        }

        protected override void OnDropDown(EventArgs e)
        {
            // Safety net: if anything still opens the native calendar, close it immediately
            // and show managed (do not leave a Chinese calendar underneath).
            if (!_suppressNativeDropDown)
            {
                CloseNativeMonthCal();
                BeginInvoke(new Action(() =>
                {
                    CloseNativeMonthCal();
                    if (_dropDown == null || !_dropDown.Visible)
                    {
                        ShowManagedCalendar();
                    }
                }));
            }

            base.OnDropDown(e);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                CloseManagedCalendar();
            }

            base.Dispose(disposing);
        }

        private bool IsOnDropDownButton(Point clientPoint)
        {
            if (ShowUpDown)
            {
                return false;
            }

            int buttonWidth = SystemInformation.VerticalScrollBarWidth + 4;
            return clientPoint.X >= Math.Max(0, ClientSize.Width - buttonWidth);
        }

        private void ToggleManagedCalendar()
        {
            if (_dropDown != null && _dropDown.Visible)
            {
                CloseManagedCalendar();
                return;
            }

            ShowManagedCalendar();
        }

        private void ShowManagedCalendar()
        {
            if (IsDisposed || !IsHandleCreated)
            {
                return;
            }

            CloseNativeMonthCal();
            CloseManagedCalendar();

            _suppressNativeDropDown = true;
            try
            {
                CultureInfo culture = OsCulture.Resolve(CalendarCulture) ?? CultureInfo.GetCultureInfo("en-US");
                var panel = new CultureMonthCalendarPanel(culture, Value.Date);
                panel.DateSelected += date =>
                {
                    DateTime keepTime = Value;
                    Value = date.Date
                        .AddHours(keepTime.Hour)
                        .AddMinutes(keepTime.Minute)
                        .AddSeconds(keepTime.Second);
                    CloseManagedCalendar();
                    CloseNativeMonthCal();
                };

                var host = new ToolStripControlHost(panel)
                {
                    Margin = Padding.Empty,
                    Padding = Padding.Empty,
                    AutoSize = false,
                    Size = panel.Size
                };
                panel.SizeChanged += (s, e) =>
                {
                    if (!host.IsDisposed)
                    {
                        host.Size = panel.Size;
                    }
                };

                _dropDown = new ToolStripDropDown
                {
                    Padding = Padding.Empty,
                    Margin = Padding.Empty,
                    AutoClose = true
                };
                _dropDown.Items.Add(host);
                _dropDown.Closed += (s, e) =>
                {
                    CloseNativeMonthCal();
                };

                Rectangle screen = RectangleToScreen(ClientRectangle);
                _dropDown.Show(new Point(screen.Left, screen.Bottom));
            }
            finally
            {
                _suppressNativeDropDown = false;
            }
        }

        private void CloseManagedCalendar()
        {
            if (_dropDown == null)
            {
                return;
            }

            ToolStripDropDown drop = _dropDown;
            _dropDown = null;
            try
            {
                drop.Close();
                drop.Dispose();
            }
            catch
            {
                // Ignore dispose races.
            }

            CloseNativeMonthCal();
        }

        private void CloseNativeMonthCal()
        {
            if (!IsHandleCreated)
            {
                return;
            }

            try
            {
                SendMessage(Handle, DtmCloseMonthCal, IntPtr.Zero, IntPtr.Zero);
            }
            catch
            {
                // Ignore.
            }
        }
    }

    /// <summary>Lightweight month grid that uses <see cref="CultureInfo"/> for headers and month title.</summary>
    internal sealed class CultureMonthCalendarPanel : Panel
    {
        private const int GridPadding = 8;
        private const int MinColumnWidth = 40;
        private const int HeaderHeight = 28;
        private const int TodayHeight = 26;
        private const int MinBodyHeight = 196;

        private readonly CultureInfo _culture;
        private DateTime _displayMonth;
        private readonly DateTime _selectedDate;
        private readonly Font _headerFont;

        public event Action<DateTime> DateSelected;

        public CultureMonthCalendarPanel(CultureInfo culture, DateTime selectedDate)
        {
            _culture = culture ?? CultureInfo.InvariantCulture;
            _selectedDate = selectedDate.Date;
            _displayMonth = new DateTime(_selectedDate.Year, _selectedDate.Month, 1);
            DoubleBuffered = true;
            BackColor = SystemColors.Window;
            BorderStyle = BorderStyle.FixedSingle;
            Font = new Font("Segoe UI", 9f);
            _headerFont = new Font(Font, FontStyle.Bold);
            Rebuild();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                if (_headerFont != null)
                {
                    _headerFont.Dispose();
                }
            }

            base.Dispose(disposing);
        }

        private void Rebuild()
        {
            SuspendLayout();
            Controls.Clear();

            string[] dayNames = GetDayHeaders(_culture);
            Size preferred = MeasurePreferredSize(dayNames);
            Size = preferred;
            MinimumSize = preferred;

            var title = new Label
            {
                AutoSize = false,
                TextAlign = ContentAlignment.MiddleCenter,
                Dock = DockStyle.Fill,
                Text = _displayMonth.ToString("Y", _culture),
                Font = _headerFont
            };

            var prev = new Button
            {
                Text = "<",
                Width = 28,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Left
            };
            prev.FlatAppearance.BorderSize = 0;
            prev.Click += (s, e) =>
            {
                _displayMonth = _displayMonth.AddMonths(-1);
                Rebuild();
            };

            var next = new Button
            {
                Text = ">",
                Width = 28,
                Height = 24,
                FlatStyle = FlatStyle.Flat,
                Dock = DockStyle.Right
            };
            next.FlatAppearance.BorderSize = 0;
            next.Click += (s, e) =>
            {
                _displayMonth = _displayMonth.AddMonths(1);
                Rebuild();
            };

            var header = new Panel { Dock = DockStyle.Top, Height = HeaderHeight };
            header.Controls.Add(title);
            header.Controls.Add(prev);
            header.Controls.Add(next);

            var grid = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 7,
                RowCount = 7,
                Padding = new Padding(4),
                GrowStyle = TableLayoutPanelGrowStyle.FixedSize
            };
            for (int c = 0; c < 7; c++)
            {
                grid.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f / 7f));
            }

            // Fixed-ish header row; remaining rows share space for day cells.
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 22f));
            for (int r = 1; r < 7; r++)
            {
                grid.RowStyles.Add(new RowStyle(SizeType.Percent, 100f / 6f));
            }

            DayOfWeek first = _culture.DateTimeFormat.FirstDayOfWeek;
            for (int i = 0; i < 7; i++)
            {
                grid.Controls.Add(new Label
                {
                    Text = dayNames[i],
                    TextAlign = ContentAlignment.MiddleCenter,
                    Dock = DockStyle.Fill,
                    Font = _headerFont,
                    AutoEllipsis = false,
                    // Avoid mid-word wrap ("Mo"/"n") when column is tight.
                    AutoSize = false,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                }, i, 0);
            }

            DateTime firstOfMonth = _displayMonth;
            int offset = ((int)firstOfMonth.DayOfWeek - (int)first + 7) % 7;
            DateTime cellDate = firstOfMonth.AddDays(-offset);
            DateTime today = DateTime.Today;

            for (int row = 1; row <= 6; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    DateTime date = cellDate;
                    bool inMonth = date.Month == _displayMonth.Month;
                    var dayButton = new Button
                    {
                        Text = date.Day.ToString(_culture),
                        Dock = DockStyle.Fill,
                        FlatStyle = FlatStyle.Flat,
                        Margin = new Padding(1),
                        Tag = date,
                        ForeColor = inMonth ? SystemColors.ControlText : SystemColors.GrayText,
                        BackColor = date == _selectedDate
                            ? SystemColors.Highlight
                            : (date == today ? Color.FromArgb(220, 235, 252) : SystemColors.Window)
                    };
                    if (date == _selectedDate)
                    {
                        dayButton.ForeColor = SystemColors.HighlightText;
                    }

                    dayButton.FlatAppearance.BorderSize = date == today ? 1 : 0;
                    dayButton.Click += (s, e) =>
                    {
                        var btn = (Button)s;
                        var picked = (DateTime)btn.Tag;
                        Action<DateTime> handler = DateSelected;
                        if (handler != null)
                        {
                            handler(picked);
                        }
                    };
                    grid.Controls.Add(dayButton, col, row);
                    cellDate = cellDate.AddDays(1);
                }
            }

            var todayButton = new LinkLabel
            {
                Text = GetTodayText(_culture),
                Dock = DockStyle.Bottom,
                Height = TodayHeight,
                TextAlign = ContentAlignment.MiddleCenter
            };
            todayButton.LinkClicked += (s, e) =>
            {
                Action<DateTime> handler = DateSelected;
                if (handler != null)
                {
                    handler(DateTime.Today);
                }
            };

            Controls.Add(grid);
            Controls.Add(todayButton);
            Controls.Add(header);
            ResumeLayout(true);
        }

        private Size MeasurePreferredSize(string[] dayNames)
        {
            int columnWidth = MinColumnWidth;
            using (Graphics g = CreateGraphics())
            {
                foreach (string name in dayNames)
                {
                    Size textSize = TextRenderer.MeasureText(
                        g,
                        name ?? string.Empty,
                        _headerFont,
                        new Size(int.MaxValue, int.MaxValue),
                        TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                    columnWidth = Math.Max(columnWidth, textSize.Width + 12);
                }

                Size titleSize = TextRenderer.MeasureText(
                    g,
                    _displayMonth.ToString("Y", _culture),
                    _headerFont,
                    new Size(int.MaxValue, int.MaxValue),
                    TextFormatFlags.SingleLine | TextFormatFlags.NoPadding);
                int titleWidth = titleSize.Width + 28 + 28 + 16;
                int gridWidth = columnWidth * 7 + GridPadding;
                int width = Math.Max(titleWidth, gridWidth);
                int height = HeaderHeight + TodayHeight + MinBodyHeight + GridPadding;
                return new Size(width, height);
            }
        }

        private static string[] GetDayHeaders(CultureInfo culture)
        {
            DayOfWeek first = culture.DateTimeFormat.FirstDayOfWeek;
            // Prefer abbreviated names (Sun/Mon/…) when they fit; size is measured to match.
            string[] source = culture.DateTimeFormat.AbbreviatedDayNames;
            var headers = new string[7];
            for (int i = 0; i < 7; i++)
            {
                int idx = ((int)first + i) % 7;
                headers[i] = source[idx];
            }

            return headers;
        }

        private static string GetTodayText(CultureInfo culture)
        {
            if (culture == null)
            {
                return "Today";
            }

            string name = culture.Name ?? string.Empty;
            if (name.StartsWith("zh", StringComparison.OrdinalIgnoreCase))
            {
                return "今天: " + DateTime.Today.ToString("yyyy/M/d", culture);
            }

            return "Today: " + DateTime.Today.ToString("yyyy/M/d", culture);
        }
    }
}
