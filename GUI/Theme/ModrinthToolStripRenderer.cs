using System.Drawing;
using System.Drawing.Drawing2D;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace CKAN.GUI
{
    /// <summary>
    /// Paints menus and toolbars with the <see cref="ModrinthTheme"/> palette.
    /// The stock renderer draws light gradients that no amount of BackColor
    /// setting will override, so the color table has to be replaced wholesale.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public class ModrinthToolStripRenderer : ToolStripProfessionalRenderer
    {
        public ModrinthToolStripRenderer()
            : base(new ModrinthColorTable())
        {
            RoundedEdges = false;
        }

        /// <summary>
        /// Toolbar entries that get the filled accent treatment, the way a web UI
        /// marks its one primary action. Matched by name because the toolbar is
        /// built by the designer, which has nowhere to hang a flag.
        /// </summary>
        private static readonly string[] primaryItemNames =
        {
            "LaunchGameToolStripMenuItem",
        };

        private static bool IsPrimary(ToolStripItem item)
            => !item.IsOnDropDown && primaryItemNames.Contains(item.Name);

        protected override void OnRenderItemText(ToolStripItemTextRenderEventArgs e)
        {
            e.TextColor = !e.Item.Enabled       ? ModrinthTheme.TextMuted
                        : IsPrimary(e.Item)     ? ModrinthTheme.Bg
                                                : ModrinthTheme.Text;
            base.OnRenderItemText(e);
        }

        protected override void OnRenderMenuItemBackground(ToolStripItemRenderEventArgs e)
        {
            if (e.Item.IsOnDropDown)
            {
                base.OnRenderMenuItemBackground(e);
                return;
            }

            // Inset so adjacent items don't touch, and so the pill reads as a button
            var bounds = new Rectangle(Point.Empty, e.Item.Size);
            bounds.Inflate(-2, -4);

            var fill = IsPrimary(e.Item)
                           ? e.Item.Pressed  ? ModrinthTheme.AccentDown
                           : e.Item.Selected ? ModrinthTheme.AccentHover
                                             : ModrinthTheme.Accent
                           : e.Item.Pressed  ? ModrinthTheme.Raised
                           : e.Item.Selected ? ModrinthTheme.RaisedHover
                                             : Color.Empty;

            if (fill == Color.Empty)
            {
                return;
            }

            e.Graphics.SmoothingMode = SmoothingMode.AntiAlias;
            using (var path = RoundedRect(bounds, cornerRadius))
            using (var brush = new SolidBrush(fill))
            {
                e.Graphics.FillPath(brush, path);
            }
            e.Graphics.SmoothingMode = SmoothingMode.Default;
        }

        private const int cornerRadius = 6;

        private static GraphicsPath RoundedRect(Rectangle r, int radius)
        {
            var d    = radius * 2;
            var path = new GraphicsPath();
            if (d > r.Width || d > r.Height || d <= 0)
            {
                path.AddRectangle(r);
                return path;
            }
            path.AddArc(r.Left,        r.Top,           d, d, 180, 90);
            path.AddArc(r.Right  - d,  r.Top,           d, d, 270, 90);
            path.AddArc(r.Right  - d,  r.Bottom - d,    d, d,   0, 90);
            path.AddArc(r.Left,        r.Bottom - d,    d, d,  90, 90);
            path.CloseFigure();
            return path;
        }

        protected override void OnRenderArrow(ToolStripArrowRenderEventArgs e)
        {
            e.ArrowColor = ModrinthTheme.Text;
            base.OnRenderArrow(e);
        }

        protected override void OnRenderToolStripBackground(ToolStripRenderEventArgs e)
        {
            using (var brush = new SolidBrush(ModrinthTheme.Surface))
            {
                e.Graphics.FillRectangle(brush, e.AffectedBounds);
            }
        }

        protected override void OnRenderSeparator(ToolStripSeparatorRenderEventArgs e)
        {
            var bounds = e.Item.ContentRectangle;
            using (var pen = new Pen(ModrinthTheme.Border))
            {
                if (e.Vertical)
                {
                    var x = bounds.Left + (bounds.Width / 2);
                    e.Graphics.DrawLine(pen, x, bounds.Top + 2, x, bounds.Bottom - 2);
                }
                else
                {
                    var y = bounds.Top + (bounds.Height / 2);
                    e.Graphics.DrawLine(pen, bounds.Left + 2, y, bounds.Right - 2, y);
                }
            }
        }
    }

    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public class ModrinthColorTable : ProfessionalColorTable
    {
        public override Color ToolStripDropDownBackground => ModrinthTheme.Surface;

        public override Color MenuStripGradientBegin => ModrinthTheme.Surface;
        public override Color MenuStripGradientEnd   => ModrinthTheme.Surface;

        public override Color ToolStripGradientBegin  => ModrinthTheme.Surface;
        public override Color ToolStripGradientMiddle => ModrinthTheme.Surface;
        public override Color ToolStripGradientEnd    => ModrinthTheme.Surface;

        public override Color ToolStripPanelGradientBegin => ModrinthTheme.Surface;
        public override Color ToolStripPanelGradientEnd   => ModrinthTheme.Surface;

        public override Color ToolStripContentPanelGradientBegin => ModrinthTheme.Bg;
        public override Color ToolStripContentPanelGradientEnd   => ModrinthTheme.Bg;

        public override Color ImageMarginGradientBegin  => ModrinthTheme.Surface;
        public override Color ImageMarginGradientMiddle => ModrinthTheme.Surface;
        public override Color ImageMarginGradientEnd    => ModrinthTheme.Surface;

        public override Color MenuItemSelected => ModrinthTheme.RaisedHover;
        public override Color MenuItemBorder   => ModrinthTheme.Border;
        public override Color MenuBorder       => ModrinthTheme.Border;

        public override Color MenuItemSelectedGradientBegin => ModrinthTheme.RaisedHover;
        public override Color MenuItemSelectedGradientEnd   => ModrinthTheme.RaisedHover;

        public override Color MenuItemPressedGradientBegin  => ModrinthTheme.Raised;
        public override Color MenuItemPressedGradientMiddle => ModrinthTheme.Raised;
        public override Color MenuItemPressedGradientEnd    => ModrinthTheme.Raised;

        public override Color ButtonSelectedGradientBegin  => ModrinthTheme.RaisedHover;
        public override Color ButtonSelectedGradientMiddle => ModrinthTheme.RaisedHover;
        public override Color ButtonSelectedGradientEnd    => ModrinthTheme.RaisedHover;
        public override Color ButtonSelectedBorder         => ModrinthTheme.Border;

        public override Color ButtonPressedGradientBegin  => ModrinthTheme.Raised;
        public override Color ButtonPressedGradientMiddle => ModrinthTheme.Raised;
        public override Color ButtonPressedGradientEnd    => ModrinthTheme.Raised;

        public override Color CheckBackground         => ModrinthTheme.Selection;
        public override Color CheckSelectedBackground => ModrinthTheme.Selection;
        public override Color CheckPressedBackground  => ModrinthTheme.Selection;

        public override Color SeparatorDark  => ModrinthTheme.Border;
        public override Color SeparatorLight => ModrinthTheme.Border;

        public override Color StatusStripGradientBegin => ModrinthTheme.Surface;
        public override Color StatusStripGradientEnd   => ModrinthTheme.Surface;
    }
}
