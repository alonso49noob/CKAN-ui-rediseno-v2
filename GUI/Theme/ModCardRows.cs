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
    /// Draws the rows of the mod list as separated cards rather than the flush
    /// rows of a spreadsheet, while leaving it a real DataGridView: sorting,
    /// filtering, the install checkboxes, multi-select, the context menu and
    /// keyboard navigation all keep working, because only the background is
    /// taken over and the cells still paint their own content.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public static class ModCardRows
    {
        public static void Attach(DataGridView grid)
        {
            if (!attached.Add(grid))
            {
                return;
            }
            grid.CellPainting += OnCellPainting;
            // Taller rows, so a card has room to read as one. Setting Height on
            // the rows would be undone: the grid auto-sizes them to their
            // content, so the height has to come from the padding instead.
            var padding = grid.DefaultCellStyle.Padding;
            grid.DefaultCellStyle.Padding = new Padding(padding.Left,
                                                        padding.Top    + rowPadding,
                                                        padding.Right,
                                                        padding.Bottom + rowPadding);

            // Indent the name so the thumbnail has somewhere to sit. Padding is
            // what reserves it; the icon is drawn into that gap when painting.
            if (grid.Columns[nameColumn] is DataGridViewColumn column)
            {
                nameColumnIndex[grid] = column.Index;
                var namePadding = column.DefaultCellStyle.Padding;
                column.DefaultCellStyle.Padding =
                    new Padding(namePadding.Left + ModIcons.Size + (2 * iconMargin),
                                namePadding.Top,
                                namePadding.Right,
                                namePadding.Bottom);
            }

            grid.Disposed += (_, _) =>
            {
                attached.Remove(grid);
                nameColumnIndex.Remove(grid);
            };
        }

        private const string nameColumn = "ModName";

        private const int iconMargin = 4;

        private static readonly Dictionary<DataGridView, int> nameColumnIndex =
            new Dictionary<DataGridView, int>();

        /// <summary>Extra space above and below the content of every cell</summary>
        private const int rowPadding = 5;

        private static readonly HashSet<DataGridView> attached = new HashSet<DataGridView>();

        /// <summary>Space above and below each card, which the window shows through</summary>
        private const int gap = 2;

        private const int cornerRadius = 5;

        /// <summary>Width of the accent stripe down the side of the selected card</summary>
        private const int accentWidth = 3;

        private static void OnCellPainting(object? sender, DataGridViewCellPaintingEventArgs e)
        {
            if (!ModrinthTheme.Enabled
                || sender is not DataGridView grid
                || e.Graphics is not Graphics g
                // Negative means a header, which is not part of a card
                || e.RowIndex < 0 || e.ColumnIndex < 0
                || e.CellBounds.Width <= 0 || e.CellBounds.Height <= 2 * gap)
            {
                return;
            }

            var selected = (e.State & DataGridViewElementStates.Selected)
                           == DataGridViewElementStates.Selected;
            var first = e.ColumnIndex == grid.Columns.GetFirstColumn(DataGridViewElementStates.Visible)?.Index;
            var last  = e.ColumnIndex == grid.Columns.GetLastColumn(DataGridViewElementStates.Visible,
                                                                   DataGridViewElementStates.None)?.Index;

            // The gap between cards is the window showing through
            using (var gapBrush = new SolidBrush(ModrinthTheme.Bg))
            {
                g.FillRectangle(gapBrush, e.CellBounds);
            }

            var card = new Rectangle(e.CellBounds.X,
                                     e.CellBounds.Y + gap,
                                     e.CellBounds.Width,
                                     e.CellBounds.Height - (2 * gap));
            var fill = CardColor(grid.Rows[e.RowIndex], selected);

            // Antialias only the rounded ends. Smoothing a plain rectangle leaves
            // half-covered pixels down its edges, which show up as seams where
            // one cell's card meets the next.
            var rounded = first || last;
            g.SmoothingMode = rounded ? SmoothingMode.AntiAlias : SmoothingMode.None;
            using (var path  = CardPath(card, first, last))
            using (var brush = new SolidBrush(fill))
            {
                g.FillPath(brush, path);
            }
            g.SmoothingMode = SmoothingMode.Default;

            if (selected && first)
            {
                // A stripe down the leading edge, so the selected mod is obvious
                // even when a label color is already tinting the card
                using (var accentBrush = new SolidBrush(ModrinthTheme.Accent))
                {
                    g.FillRectangle(accentBrush,
                                             new Rectangle(card.Left, card.Top + cornerRadius,
                                                           accentWidth,
                                                           card.Height - (2 * cornerRadius)));
                }
            }

            DrawIcon(g, grid, e.RowIndex, e.ColumnIndex, card);

            // Let the cell draw its own text, checkbox or image on top
            e.PaintContent(e.CellBounds);
            e.Handled = true;
        }

        /// <summary>
        /// Draws the mod's thumbnail into the gap reserved by the name column's
        /// padding. Does nothing until the image has been fetched, at which
        /// point the grid repaints and it appears.
        /// </summary>
        private static void DrawIcon(Graphics g, DataGridView grid,
                                     int rowIndex, int columnIndex, Rectangle card)
        {
            if (!nameColumnIndex.TryGetValue(grid, out var index)
                || columnIndex != index
                || grid.Rows[rowIndex].Tag is not GUIMod mod
                || ModIcons.Get(mod, grid) is not Bitmap icon
                || card.Height < ModIcons.Size)
            {
                return;
            }
            var box = new Rectangle(card.Left + iconMargin,
                                    card.Top + ((card.Height - ModIcons.Size) / 2),
                                    ModIcons.Size, ModIcons.Size);
            var saved = g.Clip;
            g.SmoothingMode = SmoothingMode.AntiAlias;
            using (var rounded = CardPath(box, true, true))
            {
                g.SetClip(rounded);
                g.DrawImage(icon, box);
            }
            g.Clip = saved;
            g.SmoothingMode = SmoothingMode.Default;
        }

        /// <summary>
        /// A card keeps the row's own color when a label has given it one, so
        /// the label colors the user has set still read.
        /// </summary>
        private static Color CardColor(DataGridViewRow row, bool selected)
            => selected
                   ? row.DefaultCellStyle.SelectionBackColor is { IsEmpty: false } sel
                         ? sel
                         : ModrinthTheme.Selection
                   : row.DefaultCellStyle.BackColor is { IsEmpty: false } back
                         ? back
                         : ModrinthTheme.Surface;

        /// <summary>
        /// Rounds only the outer ends of the row, so the cells in between join
        /// up into one continuous card.
        /// </summary>
        private static GraphicsPath CardPath(Rectangle r, bool roundLeft, bool roundRight)
        {
            var path = new GraphicsPath();
            var d    = cornerRadius * 2;
            if (d > r.Height || (!roundLeft && !roundRight))
            {
                path.AddRectangle(r);
                return path;
            }
            // Top edge, right side, bottom edge, left side
            if (roundRight && d <= r.Width)
            {
                path.AddLine(r.Left, r.Top, r.Right - d, r.Top);
                path.AddArc(r.Right - d, r.Top, d, d, 270, 90);
                path.AddArc(r.Right - d, r.Bottom - d, d, d, 0, 90);
                path.AddLine(r.Right - d, r.Bottom, r.Left, r.Bottom);
            }
            else
            {
                path.AddLine(r.Left, r.Top, r.Right, r.Top);
                path.AddLine(r.Right, r.Bottom, r.Left, r.Bottom);
            }
            if (roundLeft && d <= r.Width)
            {
                path.AddArc(r.Left, r.Bottom - d, d, d, 90, 90);
                path.AddArc(r.Left, r.Top, d, d, 180, 90);
            }
            path.CloseFigure();
            return path;
        }
    }
}
