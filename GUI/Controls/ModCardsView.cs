using System;
using System.Collections.Generic;
using System.ComponentModel;
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
    /// <summary>What a card's button does, which depends on the mod's state</summary>
    public enum ModCardAction
    {
        Install,
        Update,
        Remove,
        /// <summary>Undo a change that's queued but not applied yet</summary>
        Cancel,
    }

    public enum ModListViewMode
    {
        /// <summary>Full width rows running down the window, like CurseForge</summary>
        ExpandedList = 0,
        /// <summary>A grid of tiles, like Modrinth</summary>
        Cards = 1,
        /// <summary>The original table of columns</summary>
        ClassicList = 2,
    }

    /// <summary>
    /// Presents the mod list as rows or tiles instead of a spreadsheet.
    ///
    /// This is presentation only. The DataGridView stays alive underneath and
    /// remains the source of truth: it still does the filtering, the sorting and
    /// the change set, and this control just reads its rows and draws them. That
    /// keeps search, filters, labels, the context menu and the install logic
    /// working exactly as before, which is why the view can be swapped at any
    /// time without anything getting out of step.
    ///
    /// Only what fits on screen is drawn, so a repository of tens of thousands of
    /// mods costs no more to display than a handful.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public class ModCardsView : Control
    {
        public ModCardsView()
        {
            SetStyle(ControlStyles.AllPaintingInWmPaint
                     | ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint
                     | ControlStyles.ResizeRedraw
                     | ControlStyles.Selectable, true);
            TabStop = true;

            scrollBar = new VScrollBar()
            {
                Dock     = DockStyle.Right,
                SmallChange = 20,
            };
            scrollBar.ValueChanged += (_, _) =>
            {
                offset = scrollBar.Value;
                Invalidate();
            };
            Controls.Add(scrollBar);

            glide = new Timer() { Interval = frameMs };
            glide.Tick += OnGlide;

            // Filtering and searching just set Visible on rows, which raises
            // nothing we could subscribe to, so notice it by watching for the
            // count or the selection to change.
            watch = new Timer() { Interval = watchMs };
            watch.Tick += (_, _) =>
            {
                if (Visible && Grid != null && Signature() is var now && now != signature)
                {
                    signature = now;
                    offset    = (int)Clamp(offset);
                    target    = offset;
                    UpdateScrollBar();
                    Invalidate();
                }
            };
            watch.Start();
        }

        private (int, int) Signature()
            => Grid == null
                   ? (0, 0)
                   : (Grid.Rows.Cast<DataGridViewRow>().Count(r => r.Visible),
                      Grid.CurrentCell?.RowIndex ?? -1);

        /// <summary>
        /// The grid this view mirrors. Everything shown here comes from its rows.
        /// </summary>
        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public DataGridView? Grid { get; set; }

        private ModListViewMode mode = ModListViewMode.ExpandedList;

        [Browsable(false)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
        public ModListViewMode Mode
        {
            get => mode;
            set
            {
                mode = value;
                offset = 0;
                Invalidate();
                UpdateScrollBar();
            }
        }

        /// <summary>A mod was clicked, and should become the current selection</summary>
        public event Action<GUIMod>? ModClicked;

        /// <summary>A mod's button was pressed, or it was double clicked</summary>
        public event Action<GUIMod, ModCardAction>? ActionRequested;

        /// <summary>The user right clicked, and wants the mod list's own menu</summary>
        public event Action? ContextMenuRequested;

        #region Rows

        /// <summary>
        /// The mods to show, in the order and selection the grid currently has.
        /// Rebuilt on demand rather than cached, because filtering hides rows
        /// without raising anything we could listen for.
        /// </summary>
        private List<DataGridViewRow> VisibleRows()
            => Grid == null
                   ? new List<DataGridViewRow>()
                   : Grid.Rows.Cast<DataGridViewRow>()
                              .Where(r => r.Visible && r.Tag is GUIMod)
                              .ToList();

        #endregion

        #region Layout

        private int ItemHeight => mode == ModListViewMode.Cards ? cardHeight : rowHeight;

        private int Columns
            => mode == ModListViewMode.Cards
                   ? Math.Max(1, (Width - scrollBar.Width - pad) / (cardWidth + pad))
                   : 1;

        private int ItemWidth
            => mode == ModListViewMode.Cards
                   ? cardWidth
                   : Math.Max(50, Width - scrollBar.Width - (2 * pad));

        private int ContentHeight(int count)
        {
            var rows = (int)Math.Ceiling((double)count / Columns);
            return (rows * (ItemHeight + pad)) + pad;
        }

        private Rectangle ItemBounds(int index)
        {
            var column = index % Columns;
            var row    = index / Columns;
            return new Rectangle(pad + (column * (ItemWidth + pad)),
                                 pad + (row * (ItemHeight + pad)) - offset,
                                 ItemWidth, ItemHeight);
        }

        #endregion

        #region Painting

        protected override void OnPaint(PaintEventArgs e)
        {
            var g = e.Graphics;
            using (var background = new SolidBrush(ModrinthTheme.Bg))
            {
                g.FillRectangle(background, ClientRectangle);
            }
            if (Grid == null)
            {
                return;
            }

            var rows = VisibleRows();
            UpdateScrollBar(rows.Count);
            g.SmoothingMode     = SmoothingMode.AntiAlias;
            g.TextRenderingHint = System.Drawing.Text.TextRenderingHint.ClearTypeGridFit;

            for (var i = 0; i < rows.Count; ++i)
            {
                var bounds = ItemBounds(i);
                if (bounds.Bottom < 0)
                {
                    continue;
                }
                if (bounds.Top > Height)
                {
                    // Everything after this is below the window
                    break;
                }
                if (rows[i].Tag is GUIMod mod)
                {
                    DrawItem(g, mod, bounds, rows[i].Selected);
                }
            }
        }

        private void DrawItem(Graphics g, GUIMod mod, Rectangle bounds, bool selected)
        {
            using (var path = Rounded(bounds, corner))
            using (var fill = new SolidBrush(selected ? ModrinthTheme.Selection
                                                      : ModrinthTheme.Surface))
            {
                g.FillPath(fill, path);
                if (selected)
                {
                    using (var pen = new Pen(ModrinthTheme.Accent, 2))
                    {
                        g.DrawPath(pen, path);
                    }
                }
            }
            if (mode == ModListViewMode.Cards)
            {
                DrawCard(g, mod, bounds);
            }
            else
            {
                DrawRow(g, mod, bounds);
            }
        }

        private void DrawRow(Graphics g, GUIMod mod, Rectangle bounds)
        {
            var icon = new Rectangle(bounds.Left + inner,
                                     bounds.Top + ((bounds.Height - rowIcon) / 2),
                                     rowIcon, rowIcon);
            DrawIcon(g, mod, icon);

            var buttonBox = ButtonBounds(bounds);
            var textLeft  = icon.Right + inner;
            var textWidth = Math.Max(20, buttonBox.Left - inner - textLeft);

            var y = bounds.Top + inner;
            Draw(g, mod.Name, BoldFont, ModrinthTheme.Text,
                 new Rectangle(textLeft, y, textWidth, lineHeight + 2));
            y += lineHeight + 3;
            Draw(g, MetaLine(mod), Font, ModrinthTheme.TextMuted,
                 new Rectangle(textLeft, y, textWidth, lineHeight));
            y += lineHeight + 2;
            Draw(g, mod.Abstract, Font, ModrinthTheme.TextMuted,
                 new Rectangle(textLeft, y, textWidth, lineHeight));

            DrawButton(g, mod, buttonBox);
        }

        private void DrawCard(Graphics g, GUIMod mod, Rectangle bounds)
        {
            var icon = new Rectangle(bounds.Left + ((bounds.Width - cardIcon) / 2),
                                     bounds.Top + inner, cardIcon, cardIcon);
            DrawIcon(g, mod, icon);

            var textLeft  = bounds.Left + inner;
            var textWidth = bounds.Width - (2 * inner);
            var y         = icon.Bottom + inner;

            Draw(g, mod.Name, BoldFont, ModrinthTheme.Text,
                 new Rectangle(textLeft, y, textWidth, lineHeight + 2),
                 TextFormatFlags.HorizontalCenter);
            y += lineHeight + 4;
            Draw(g, MetaLine(mod), Font, ModrinthTheme.TextMuted,
                 new Rectangle(textLeft, y, textWidth, lineHeight),
                 TextFormatFlags.HorizontalCenter);
            y += lineHeight + 4;
            // As much description as fits above the button, cut with an ellipsis.
            // Clamped rather than fixed, or a long one runs under the button.
            var button    = ButtonBounds(bounds);
            var available = Math.Max(0, button.Top - inner - y);
            // Whole lines only, so the last one isn't sliced through the middle
            var lines     = Math.Min(2, available / lineHeight);
            Draw(g, mod.Abstract, Font, ModrinthTheme.TextMuted,
                 new Rectangle(textLeft, y, textWidth, lines * lineHeight),
                 TextFormatFlags.WordBreak);

            DrawButton(g, mod, button);
        }

        /// <summary>
        /// Falls back to a tile with the mod's initial when there's no image,
        /// which most mods don't have.
        /// </summary>
        private void DrawIcon(Graphics g, GUIMod mod, Rectangle box)
        {
            using (var path = Rounded(box, corner))
            {
                var saved = g.Clip;
                g.SetClip(path);
                if (ModIcons.Get(mod, this) is Bitmap image)
                {
                    g.DrawImage(image, box);
                }
                else
                {
                    using (var fill = new SolidBrush(mod.IsInstalled
                                                         ? ModrinthTheme.Selection
                                                         : ModrinthTheme.Raised))
                    {
                        g.FillRectangle(fill, box);
                    }
                    var initial = string.IsNullOrEmpty(mod.Name) ? "?" : mod.Name.Substring(0, 1);
                    Draw(g, initial, BoldFont, ModrinthTheme.TextMuted, box,
                         TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                }
                g.Clip = saved;
            }
        }

        /// <summary>
        /// True when the mod is already queued for a change that hasn't been
        /// applied yet: what the user asked for differs from what's installed.
        /// </summary>
        internal static bool IsPending(GUIMod mod)
            => !Equals(mod.SelectedMod?.version, mod.InstalledMod?.Module.version);

        /// <summary>
        /// The change already queued, which is what the button reports while it
        /// waits for Apply.
        /// </summary>
        private static ModCardAction PendingAction(GUIMod mod)
            => !mod.IsInstalled        ? ModCardAction.Install
             : mod.SelectedMod == null ? ModCardAction.Remove
                                       : ModCardAction.Update;

        /// <summary>
        /// What pressing the button does. Anything already queued is undone,
        /// so a click is always reversible by clicking again. Otherwise
        /// updating takes priority over removing, since that's the thing worth
        /// doing when a newer version is out; removal stays on the context menu.
        /// </summary>
        internal static ModCardAction ActionFor(GUIMod mod)
            => IsPending(mod)   ? ModCardAction.Cancel
             : !mod.IsInstalled ? ModCardAction.Install
             : mod.HasUpdate    ? ModCardAction.Update
                                : ModCardAction.Remove;

        private void DrawButton(Graphics g, GUIMod mod, Rectangle box)
        {
            if (mod.IsAutodetected || box.Width <= 0)
            {
                // Manually installed mods aren't ours to add or remove
                return;
            }
            if (!mod.IsInstalled && !mod.IsInstallable())
            {
                Draw(g, Properties.Resources.MainModListIncompatible, Font,
                     ModrinthTheme.TextMuted, box,
                     TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }
            var action  = ActionFor(mod);
            var pending = action == ModCardAction.Cancel;
            var label   = (pending ? PendingAction(mod) : action) switch
                          {
                              ModCardAction.Install => Properties.Resources.ChangeTypeInstall,
                              ModCardAction.Update  => Properties.Resources.ChangeTypeUpdate,
                              _                     => Properties.Resources.ChangeTypeRemove,
                          };
            using (var path = Rounded(box, corner))
            {
                if (pending)
                {
                    // Queued: outlined rather than filled, so a change that's
                    // been asked for reads differently from one on offer, and
                    // pressing it again takes it back
                    using (var pen = new Pen(ModrinthTheme.Accent, 2))
                    {
                        g.DrawPath(pen, path);
                    }
                }
                else
                {
                    using (var fill = new SolidBrush(action == ModCardAction.Remove
                                                         ? ModrinthTheme.Raised
                                                         : ModrinthTheme.Accent))
                    {
                        g.FillPath(fill, path);
                    }
                }
            }
            Draw(g, pending ? $"✓ {label}" : label,
                 BoldFont,
                 pending                      ? ModrinthTheme.Accent
                 : action == ModCardAction.Remove ? ModrinthTheme.Text
                                                  : ModrinthTheme.Bg,
                 box, TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
        }

        private Rectangle ButtonBounds(Rectangle bounds)
            => mode == ModListViewMode.Cards
                   ? new Rectangle(bounds.Left + inner,
                                   bounds.Bottom - inner - buttonHeight,
                                   bounds.Width - (2 * inner), buttonHeight)
                   : new Rectangle(bounds.Right - inner - buttonWidth,
                                   bounds.Top + ((bounds.Height - buttonHeight) / 2),
                                   buttonWidth, buttonHeight);

        private string MetaLine(GUIMod mod)
        {
            // Show both versions when there's an update, so the card says what
            // pressing Update would actually get you
            var version = mod.IsInstalled
                              ? mod.HasUpdate && mod.InstalledVersion != null
                                    ? $"{mod.InstalledVersion} → {mod.LatestVersion}"
                                    : mod.InstalledVersion ?? mod.LatestVersion
                              : mod.LatestVersion;
            var authors = string.Join(", ", mod.Authors);
            var parts   = new List<string>();
            if (!string.IsNullOrEmpty(version))
            {
                parts.Add(version);
            }
            if (!string.IsNullOrEmpty(authors))
            {
                parts.Add(authors);
            }
            if (mod.DownloadCount is int count)
            {
                parts.Add($"↓ {count:N0}");
            }
            return string.Join("  ·  ", parts);
        }

        private static void Draw(Graphics g, string? text, Font font, Color color,
                                 Rectangle box, TextFormatFlags extra = TextFormatFlags.Default)
        {
            if (!string.IsNullOrEmpty(text))
            {
                TextRenderer.DrawText(g, text, font, box, color,
                                      extra | TextFormatFlags.EndEllipsis
                                            | TextFormatFlags.NoPrefix);
            }
        }

        private Font BoldFont => boldFont ??= new Font(Font, FontStyle.Bold);
        private Font? boldFont;

        private static GraphicsPath Rounded(Rectangle r, int radius)
        {
            var path = new GraphicsPath();
            var d    = radius * 2;
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

        #endregion

        #region Input

        protected override void OnMouseDown(MouseEventArgs e)
        {
            base.OnMouseDown(e);
            Focus();
            if (HitTest(e.Location) is not (GUIMod mod, Rectangle bounds))
            {
                return;
            }
            if (e.Button == MouseButtons.Right)
            {
                ModClicked?.Invoke(mod);
                ContextMenuRequested?.Invoke();
                return;
            }
            if (e.Button != MouseButtons.Left)
            {
                return;
            }
            ModClicked?.Invoke(mod);
            if (!mod.IsAutodetected
                && (mod.IsInstalled || mod.IsInstallable())
                && ButtonBounds(bounds).Contains(e.Location))
            {
                ActionRequested?.Invoke(mod, ActionFor(mod));
            }
            Invalidate();
        }

        protected override void OnMouseDoubleClick(MouseEventArgs e)
        {
            base.OnMouseDoubleClick(e);
            if (e.Button == MouseButtons.Left
                && HitTest(e.Location) is (GUIMod mod, Rectangle _)
                && !mod.IsAutodetected
                && (mod.IsInstalled || mod.IsInstallable()))
            {
                ActionRequested?.Invoke(mod, ActionFor(mod));
                Invalidate();
            }
        }

        private (GUIMod, Rectangle)? HitTest(Point point)
        {
            var rows = VisibleRows();
            for (var i = 0; i < rows.Count; ++i)
            {
                var bounds = ItemBounds(i);
                if (bounds.Top > Height)
                {
                    break;
                }
                if (bounds.Contains(point) && rows[i].Tag is GUIMod mod)
                {
                    return (mod, bounds);
                }
            }
            return null;
        }

        #endregion

        #region Scrolling

        protected override void OnMouseWheel(MouseEventArgs e)
        {
            base.OnMouseWheel(e);
            // Nothing here is tied to row boundaries, so this can move by pixels:
            // a notch sets a target and the view travels to it at a steady speed
            target = Clamp(target - ((e.Delta / 120f) * pixelsPerNotch));
            if (!glide.Enabled)
            {
                glide.Start();
            }
        }

        private void OnGlide(object? sender, EventArgs e)
        {
            var remaining = target - offset;
            if (Math.Abs(remaining) < 1)
            {
                offset = (int)Math.Round(target);
                glide.Stop();
            }
            else
            {
                offset += (int)(Math.Sign(remaining) * Math.Min(Math.Abs(remaining), speed));
            }
            SyncScrollBar();
            Invalidate();
        }

        private float Clamp(float value)
            => Math.Max(0, Math.Min(value, MaxOffset()));

        private int MaxOffset()
            => Math.Max(0, ContentHeight(VisibleRows().Count) - Height);

        private void UpdateScrollBar() => UpdateScrollBar(VisibleRows().Count);

        private void UpdateScrollBar(int count)
        {
            var content = ContentHeight(count);
            scrollBar.Visible     = content > Height;
            scrollBar.Maximum     = Math.Max(0, content);
            scrollBar.LargeChange = Math.Max(1, Height);
            offset = (int)Clamp(offset);
            SyncScrollBar();
        }

        private void SyncScrollBar()
        {
            if (scrollBar.Value != offset
                && offset >= scrollBar.Minimum
                && offset <= scrollBar.Maximum)
            {
                scrollBar.Value = offset;
            }
        }

        protected override void OnResize(EventArgs e)
        {
            base.OnResize(e);
            target = offset = (int)Clamp(offset);
            UpdateScrollBar();
        }

        #endregion

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                glide.Dispose();
                watch.Dispose();
                boldFont?.Dispose();
            }
            base.Dispose(disposing);
        }

        private readonly VScrollBar scrollBar;
        private readonly Timer      glide;
        private readonly Timer      watch;
        private          (int, int) signature;
        private          int        offset;
        private          float      target;

        private const int pad          = 8;
        private const int inner        = 12;
        private const int corner       = 6;
        private const int rowHeight    = 76;
        private const int rowIcon      = 48;
        private const int cardWidth    = 230;
        private const int cardHeight   = 214;
        private const int cardIcon     = 72;
        private const int buttonWidth  = 96;
        private const int buttonHeight = 30;
        private const int lineHeight   = 17;

        private const int   frameMs        = 16;
        private const int   watchMs        = 250;
        private const int   pixelsPerNotch = 110;
        private const float speed          = 14;
    }
}
