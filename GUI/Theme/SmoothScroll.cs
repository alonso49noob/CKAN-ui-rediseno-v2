using System;
using System.Collections.Generic;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace CKAN.GUI
{
    /// <summary>
    /// Eases mouse wheel scrolling in a DataGridView.
    ///
    /// By default each wheel notch jumps several rows at once, which on a long
    /// mod list reads as the content teleporting. This intercepts the wheel and
    /// walks to the destination over a few frames instead.
    ///
    /// The grid can only scroll whole rows -- there's no public way to offset it
    /// by a partial row -- so this is eased, not pixel-smooth.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public static class SmoothScroll
    {
        public static void Attach(DataGridView grid)
        {
            if (!attached.Add(grid))
            {
                return;
            }
            var scroller = new Scroller(grid);
            grid.MouseWheel += scroller.OnMouseWheel;
            grid.Disposed   += (_, _) =>
            {
                scroller.Dispose();
                attached.Remove(grid);
            };
        }

        private static readonly HashSet<DataGridView> attached = new HashSet<DataGridView>();

        /// <summary>How many rows one wheel notch travels</summary>
        private const int rowsPerNotch = 3;

        /// <summary>Fraction of the remaining distance covered per frame</summary>
        private const float easing = 0.28f;

        private const int frameMs = 15;

        private sealed class Scroller : IDisposable
        {
            internal Scroller(DataGridView grid)
            {
                this.grid = grid;
                timer = new System.Windows.Forms.Timer() { Interval = frameMs };
                timer.Tick += OnTick;
            }

            internal void OnMouseWheel(object? sender, MouseEventArgs e)
            {
                pending += -(e.Delta / 120f) * rowsPerNotch;
                // Stop the grid from also scrolling this notch itself
                if (e is HandledMouseEventArgs handled)
                {
                    handled.Handled = true;
                }
                if (!timer.Enabled)
                {
                    timer.Start();
                }
            }

            private void OnTick(object? sender, EventArgs e)
            {
                if (Math.Abs(pending) < 1)
                {
                    Stop();
                    return;
                }
                var step = pending * easing;
                // Always cover at least one row, or a slow tail never arrives
                var rows = step > 0 ? Math.Max(1,  (int)Math.Ceiling(step))
                                    : Math.Min(-1, (int)Math.Floor(step));
                var moved = Scroll(rows);
                if (moved == 0)
                {
                    // Hit the top or the bottom
                    Stop();
                    return;
                }
                pending -= moved;
            }

            private void Stop()
            {
                pending = 0;
                timer.Stop();
            }

            /// <summary>
            /// Scroll by a number of visible rows, stepping row by row so that
            /// rows hidden by the current filter are skipped rather than landed
            /// on, which the grid rejects.
            /// </summary>
            /// <returns>How many rows it actually moved, signed</returns>
            private int Scroll(int rows)
            {
                var index = grid.FirstDisplayedScrollingRowIndex;
                if (index < 0)
                {
                    return 0;
                }
                var forward = rows > 0;
                var moved   = 0;
                for (var i = 0; i < Math.Abs(rows); ++i)
                {
                    var next = forward
                                   ? grid.Rows.GetNextRow(index, DataGridViewElementStates.Visible)
                                   : grid.Rows.GetPreviousRow(index, DataGridViewElementStates.Visible);
                    if (next < 0)
                    {
                        break;
                    }
                    index = next;
                    ++moved;
                }
                if (moved == 0)
                {
                    return 0;
                }
                try
                {
                    grid.FirstDisplayedScrollingRowIndex = index;
                }
                catch (InvalidOperationException)
                {
                    // Grid isn't in a state where it can scroll right now
                    return 0;
                }
                return forward ? moved : -moved;
            }

            public void Dispose()
            {
                timer.Tick -= OnTick;
                timer.Dispose();
            }

            private readonly DataGridView                 grid;
            private readonly System.Windows.Forms.Timer   timer;
            private          float                        pending;
        }
    }
}
