using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;

namespace F2B.Forms.Designer
{
    /// <summary>
    /// Soft hover / pressed chrome for the controls toolbox (no hard button borders).
    /// </summary>
    internal sealed class ControlsToolStripRenderer : ToolStripProfessionalRenderer
    {
        private static readonly Color Hover = Color.FromArgb(232, 240, 254);
        private static readonly Color Pressed = Color.FromArgb(204, 228, 247);
        private static readonly Color StripBg = Color.FromArgb(250, 250, 250);

        public ControlsToolStripRenderer()
            : base(new ControlsColorTable())
        {
            RoundedEdges = false;
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            e.Graphics.Clear(StripBg);
        }

        protected override void OnRenderToolStripBorder(ToolStripRenderEventArgs e)
        {
            using (var pen = new Pen(Color.FromArgb(225, 225, 225)))
            {
                int y = e.AffectedBounds.Bottom - 1;
                e.Graphics.DrawLine(pen, e.AffectedBounds.Left, y, e.AffectedBounds.Right, y);
            }
        }

        protected override void OnRenderButtonBackground(ToolStripItemRenderEventArgs e)
        {
            var button = e.Item as ToolStripButton;
            if (button == null)
            {
                base.OnRenderButtonBackground(e);
                return;
            }

            Color? fill = null;
            if (button.Pressed || button.Checked)
            {
                fill = Pressed;
            }
            else if (button.Selected)
            {
                fill = Hover;
            }

            if (!fill.HasValue)
            {
                return;
            }

            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            bounds.Inflate(-1, -1);
            using (GraphicsPath path = Rounded(bounds, 4))
            using (var brush = new SolidBrush(fill.Value))
            {
                e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
                e.Graphics.FillPath(brush, path);
            }
        }

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            int d = radius * 2;
            var path = new GraphicsPath();
            path.AddArc(r.Left, r.Top, d, d, 180, 90);
            path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
            path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
            path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
            path.CloseFigure();
            return path;
        }

        private sealed class ControlsColorTable : ProfessionalColorTable
        {
            public override Color ToolStripGradientBegin => StripBg;
            public override Color ToolStripGradientMiddle => StripBg;
            public override Color ToolStripGradientEnd => StripBg;
            public override Color ImageMarginGradientBegin => StripBg;
            public override Color ImageMarginGradientMiddle => StripBg;
            public override Color ImageMarginGradientEnd => StripBg;
            public override Color ButtonSelectedBorder => Color.Transparent;
            public override Color ButtonPressedBorder => Color.Transparent;
            public override Color ButtonCheckedGradientBegin => Pressed;
            public override Color ButtonCheckedGradientMiddle => Pressed;
            public override Color ButtonCheckedGradientEnd => Pressed;
            public override Color ButtonSelectedGradientBegin => Hover;
            public override Color ButtonSelectedGradientMiddle => Hover;
            public override Color ButtonSelectedGradientEnd => Hover;
            public override Color ButtonPressedGradientBegin => Pressed;
            public override Color ButtonPressedGradientMiddle => Pressed;
            public override Color ButtonPressedGradientEnd => Pressed;
        }
    }
}
