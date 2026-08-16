using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace CKAN.GUI
{
    /// <summary>
    /// Draws a thin flat scrollbar in place of the Windows one.
    ///
    /// Windows only themes the non-client scrollbars of a scrolling window; the
    /// standalone SCROLLBAR controls that a DataGridView hosts stay white no
    /// matter what SetWindowTheme is told, so the only way to get a dark one is
    /// to paint it.
    ///
    /// Only painting is taken over. Dragging, clicking the track, auto-repeat and
    /// the keyboard all stay with the default window procedure, which keeps the
    /// scrollbar behaving exactly like every other one on the system.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public static class FlatScrollBar
    {
        public static void Attach(ScrollBar scrollBar)
        {
            if (!attached.Add(scrollBar))
            {
                return;
            }
            var painter = new Painter(scrollBar);
            if (scrollBar.IsHandleCreated)
            {
                painter.AssignHandle(scrollBar.Handle);
            }
            scrollBar.HandleCreated   += (_, _) => painter.AssignHandle(scrollBar.Handle);
            scrollBar.HandleDestroyed += (_, _) => painter.ReleaseHandle();
            scrollBar.MouseEnter      += (_, _) => painter.SetHovered(true);
            scrollBar.MouseLeave      += (_, _) => painter.SetHovered(false);
            scrollBar.Disposed        += (_, _) => attached.Remove(scrollBar);
        }

        private static readonly HashSet<ScrollBar> attached = new HashSet<ScrollBar>();

        /// <summary>Gap between the thumb and the edge of the bar</summary>
        private const int thumbInset = 3;

        private const int minThumbLength = 24;

        private const int thumbRadius = 4;

        private sealed class Painter : NativeWindow
        {
            internal Painter(ScrollBar scrollBar)
            {
                this.scrollBar = scrollBar;
                vertical       = scrollBar is VScrollBar;
            }

            internal void SetHovered(bool value)
            {
                hovered = value;
                if (scrollBar.IsHandleCreated)
                {
                    scrollBar.Invalidate();
                }
            }

            protected override void WndProc(ref Message m)
            {
                switch (m.Msg)
                {
                    case NativeMethods.WM_ERASEBKGND:
                        // Painting covers every pixel, so erasing would only flicker
                        m.Result = (IntPtr)1;
                        return;

                    case NativeMethods.WM_PAINT:
                        if (ModrinthTheme.Enabled)
                        {
                            Paint();
                            return;
                        }
                        break;
                }
                base.WndProc(ref m);
            }

            private void Paint()
            {
                var ps = new NativeMethods.PAINTSTRUCT();
                var hdc = NativeMethods.BeginPaint(Handle, ref ps);
                if (hdc == IntPtr.Zero)
                {
                    return;
                }
                try
                {
                    using (var g = Graphics.FromHdc(hdc))
                    {
                        Draw(g, scrollBar.ClientRectangle);
                    }
                }
                finally
                {
                    NativeMethods.EndPaint(Handle, ref ps);
                }
            }

            private void Draw(Graphics g, Rectangle bounds)
            {
                using (var trackBrush = new SolidBrush(ModrinthTheme.Bg))
                {
                    g.FillRectangle(trackBrush, bounds);
                }
                if (ThumbRect(bounds) is not Rectangle thumb)
                {
                    // Nothing to scroll; an empty track reads as "all of it is here"
                    return;
                }
                g.SmoothingMode = SmoothingMode.AntiAlias;
                using (var path  = RoundedRect(thumb, thumbRadius))
                using (var brush = new SolidBrush(hovered
                                                      ? ModrinthTheme.RaisedHover
                                                      : ModrinthTheme.Raised))
                {
                    g.FillPath(brush, path);
                }
            }

            /// <summary>
            /// Where the thumb goes, or null when the content fits and there is
            /// nothing to drag.
            /// </summary>
            private Rectangle? ThumbRect(Rectangle bounds)
            {
                var info = new NativeMethods.SCROLLINFO()
                {
                    cbSize = (uint)System.Runtime.InteropServices.Marshal.SizeOf<NativeMethods.SCROLLINFO>(),
                    fMask  = NativeMethods.SIF_ALL,
                };
                if (!NativeMethods.GetScrollInfo(Handle, NativeMethods.SB_CTL, ref info))
                {
                    return null;
                }
                // nMax is inclusive, hence the +1
                var range = info.nMax - info.nMin + 1;
                var page  = (int)Math.Max(1, info.nPage);
                if (range <= page)
                {
                    return null;
                }

                var trackLength = vertical ? bounds.Height : bounds.Width;
                var thumbLength = Math.Max(minThumbLength,
                                           (int)((long)trackLength * page / range));
                if (thumbLength >= trackLength)
                {
                    return null;
                }
                // How far the thumb can travel, mapped onto the scrollable range
                var scrollable = range - page;
                var offset     = scrollable <= 0
                                     ? 0
                                     : (int)((long)(info.nPos - info.nMin)
                                             * (trackLength - thumbLength) / scrollable);

                return vertical
                    ? new Rectangle(bounds.Left + thumbInset,
                                    bounds.Top  + offset,
                                    Math.Max(1, bounds.Width - (2 * thumbInset)),
                                    thumbLength)
                    : new Rectangle(bounds.Left + offset,
                                    bounds.Top  + thumbInset,
                                    thumbLength,
                                    Math.Max(1, bounds.Height - (2 * thumbInset)));
            }

            private static GraphicsPath RoundedRect(Rectangle r, int radius)
            {
                var d    = radius * 2;
                var path = new GraphicsPath();
                if (d > r.Width || d > r.Height || d <= 0)
                {
                    path.AddRectangle(r);
                    return path;
                }
                path.AddArc(r.Left,      r.Top,        d, d, 180, 90);
                path.AddArc(r.Right - d, r.Top,        d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d,   0, 90);
                path.AddArc(r.Left,      r.Bottom - d, d, d,  90, 90);
                path.CloseFigure();
                return path;
            }

            private readonly ScrollBar scrollBar;
            private readonly bool      vertical;
            private          bool      hovered;
        }
    }
}
