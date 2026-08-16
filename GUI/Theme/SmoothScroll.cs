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
    /// Smooths out wheel scrolling in the mod list.
    ///
    /// A notch travels at a constant speed until it arrives, rather than
    /// jumping the whole distance at once or coasting to a stop.
    ///
    /// It is still quantised to whole rows, and that is a limit of the control
    /// rather than a choice: DataGridView scrolls by rows, and its internal
    /// pixel offset refuses intermediate positions -- writing one and reading it
    /// straight back returns zero. Genuinely per-pixel motion would mean
    /// replacing the grid with a custom virtual list.
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

        /// <summary>
        /// Rows travelled per wheel notch. One row keeps each step as small as
        /// the control allows, which is as close to continuous as it gets.
        /// </summary>
        private const int rowsPerNotch = 2;

        /// <summary>One row per frame, so the speed doesn't taper off</summary>
        private const int rowsPerFrame = 1;

        /// <summary>Roughly 60 per second</summary>
        private const int frameMs = 16;

        /// <summary>
        /// Cap on how far a fast spin can queue up, so the list doesn't keep
        /// travelling long after the wheel has stopped.
        /// </summary>
        private const int maxPending = 30;

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
                // Stop the grid from also scrolling this notch itself
                if (e is HandledMouseEventArgs handled)
                {
                    handled.Handled = true;
                }
                pending += -(e.Delta / 120f) * rowsPerNotch;
                pending  = Math.Max(-maxPending, Math.Min(maxPending, pending));
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
                var rows  = Math.Sign(pending) * rowsPerFrame;
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

            private readonly DataGridView               grid;
            private readonly System.Windows.Forms.Timer timer;
            private          float                      pending;
        }
    }
}
