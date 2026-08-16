using System;
using System.Drawing;
using System.Windows.Forms;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace CKAN.GUI
{
    /// <summary>
    /// A TabControl that obeys system colors to look less awful in a dark theme
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    public class ThemedTabControl : TabControl
    {
        public ThemedTabControl() : base()
        {
            // We need default rendering for dark mode, unless our own palette is
            // in charge, in which case default rendering would ignore it
            if (ModrinthTheme.Enabled || !Util.DarkMode)
            {
                // Tell the base class that we want to draw things ourselves
                DrawMode = TabDrawMode.OwnerDrawFixed;
            }
        }

        private const int WM_PAINT = 0x000F;

        protected override void WndProc(ref Message m)
        {
            base.WndProc(ref m);
            if (m.Msg == WM_PAINT && ModrinthTheme.Enabled)
            {
                PaintOverFrame();
            }
        }

        /// <summary>
        /// A TabControl draws a raised frame around the page area in system
        /// colors, and offers no property to turn it off, so it gets covered up
        /// once the control has finished painting. The tab row is left alone.
        /// </summary>
        private void PaintOverFrame()
        {
            var page   = DisplayRectangle;
            var client = ClientRectangle;
            if (page.Width <= 0 || page.Height <= 0)
            {
                return;
            }
            using (var g     = Graphics.FromHwnd(Handle))
            using (var brush = new SolidBrush(ModrinthTheme.Bg))
            {
                // Everything outside the page but below the tab row: the frame
                var top = page.Top - frameWidth;
                g.FillRectangle(brush, client.Left, top,
                                page.Left - client.Left, client.Bottom - top);
                g.FillRectangle(brush, page.Right, top,
                                client.Right - page.Right, client.Bottom - top);
                g.FillRectangle(brush, client.Left, page.Bottom,
                                client.Width, client.Bottom - page.Bottom);
                g.FillRectangle(brush, page.Left, top, page.Width, frameWidth);
            }
        }

        /// <summary>How far the frame extends beyond the page area</summary>
        private const int frameWidth = 3;

        protected override void OnDrawItem(DrawItemEventArgs e)
        {
            var selected = e.State == DrawItemState.Selected;
            // Background
            Rectangle bgRect = e.Bounds;
            bgRect.Inflate(-2, -1);
            bgRect.Offset(0, 1);
            using (SolidBrush bgBrush = new SolidBrush(
                       ModrinthTheme.Enabled
                           ? selected ? ModrinthTheme.Surface : ModrinthTheme.Bg
                           : BackColor))
            {
                e.Graphics.FillRectangle(bgBrush, bgRect);
            }
            if (ModrinthTheme.Enabled && selected)
            {
                // Accent rule under the active tab, the way web UIs mark them
                using (var accentBrush = new SolidBrush(ModrinthTheme.Accent))
                {
                    e.Graphics.FillRectangle(accentBrush,
                                             new Rectangle(bgRect.Left, bgRect.Bottom - 2,
                                                           bgRect.Width, 2));
                }
            }
            // e.Index can be invalid (!!), so we need try/catch
            try
            {
                var tabPage  = TabPages[e.Index];
                var textRect = e.Bounds;

                // Image
                if (ImageList != null
                    && (!string.IsNullOrEmpty(tabPage.ImageKey)
                            ? ImageList.Images.IndexOfKey(tabPage.ImageKey)
                            : tabPage.ImageIndex) is int imageIndex
                    && imageIndex > -1)
                {
                    var image = ImageList.Images[imageIndex];
                    var offsetY = (e.Bounds.Height - image.Height) / 2;
                    // Tab is wider when selected, don't move image left 1px
                    var offsetX = e.State == DrawItemState.Selected ? offsetY + 3
                                                                    : offsetY + 2;
                    // e.Graphics.DrawImage doesn't work on Mono, but this does
                    ImageList.Draw(e.Graphics, e.Bounds.Location + new Size(offsetX, offsetY), imageIndex);

                    // Don't overlap text on image
                    textRect.X     += image.Width;
                    textRect.Width -= image.Width;
                }

                // Text
                TextRenderer.DrawText(e.Graphics, tabPage.Text, tabPage.Font,
                                      textRect,
                                      ModrinthTheme.Enabled
                                          ? selected ? ModrinthTheme.Text : ModrinthTheme.TextMuted
                                          : tabPage.ForeColor);
            }
            catch (ArgumentOutOfRangeException)
            {
                // No such tab page, oh well
            }
            // Alert event subscribers
            base.OnDrawItem(e);
        }
    }
}
