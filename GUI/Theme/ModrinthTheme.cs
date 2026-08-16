using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

using Newtonsoft.Json;

namespace CKAN.GUI
{
    /// <summary>
    /// A flat dark palette in the style of modern mod managers (Modrinth, MO2),
    /// applied over the stock WinForms controls.
    ///
    /// Every color is a settable property so the user can retheme the app from
    /// within it; the result is persisted to theme.json next to the other
    /// per-user CKAN files.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public static class ModrinthTheme
    {
        /// <summary>
        /// False falls back to stock CKAN appearance (system colors).
        /// </summary>
        public static bool Enabled { get; set; } = true;

        #region Palette

        /// <summary>Window background, the darkest surface</summary>
        public static Color Bg          { get; set; } = FromHex("#16181C");
        /// <summary>Cards and panels that sit above the window background</summary>
        public static Color Surface     { get; set; } = FromHex("#26292F");
        /// <summary>Text boxes, lists, anything the user types or picks in</summary>
        public static Color Field       { get; set; } = FromHex("#1D2026");
        /// <summary>Buttons and other raised, clickable chrome</summary>
        public static Color Raised      { get; set; } = FromHex("#2E323A");
        /// <summary>Raised chrome under the mouse</summary>
        public static Color RaisedHover { get; set; } = FromHex("#3A3F48");
        /// <summary>Outlines around fields, cards and buttons</summary>
        public static Color Border      { get; set; } = FromHex("#3B4048");
        /// <summary>Separator lines between list rows</summary>
        public static Color GridLine    { get; set; } = FromHex("#282C33");
        /// <summary>Primary text</summary>
        public static Color Text        { get; set; } = FromHex("#EDEDED");
        /// <summary>Secondary text: metadata, headers, hints</summary>
        public static Color TextMuted   { get; set; } = FromHex("#96A2B0");
        /// <summary>The brand color: primary actions, links, progress</summary>
        public static Color Accent      { get; set; } = FromHex("#1BD96A");
        /// <summary>Accent under the mouse</summary>
        public static Color AccentHover { get; set; } = FromHex("#40E385");
        /// <summary>Accent while being clicked</summary>
        public static Color AccentDown  { get; set; } = FromHex("#12B85A");
        /// <summary>Background of selected rows</summary>
        public static Color Selection   { get; set; } = FromHex("#195434");

        #endregion

        /// <summary>
        /// Raised after the palette changes, for controls that paint themselves
        /// and therefore can't be restyled by walking properties.
        /// </summary>
        public static event Action? ThemeChanged;

        /// <summary>
        /// True if the current palette is a dark one. Drives the parts of CKAN
        /// that already branch on dark mode (icon inversion, tab rendering).
        /// </summary>
        public static bool IsDark => Enabled && !Bg.IsLight();

        #region Persistence

        private static readonly (string Name, Func<Color> Get, Action<Color> Set)[] entries =
        {
            ("Bg",          () => Bg,          c => Bg          = c),
            ("Surface",     () => Surface,     c => Surface     = c),
            ("Field",       () => Field,       c => Field       = c),
            ("Raised",      () => Raised,      c => Raised      = c),
            ("RaisedHover", () => RaisedHover, c => RaisedHover = c),
            ("Border",      () => Border,      c => Border      = c),
            ("GridLine",    () => GridLine,    c => GridLine    = c),
            ("Text",        () => Text,        c => Text        = c),
            ("TextMuted",   () => TextMuted,   c => TextMuted   = c),
            ("Accent",      () => Accent,      c => Accent      = c),
            ("AccentHover", () => AccentHover, c => AccentHover = c),
            ("AccentDown",  () => AccentDown,  c => AccentDown  = c),
            ("Selection",   () => Selection,   c => Selection   = c),
        };

        /// <summary>
        /// The palette as name/hex pairs, in display order, for the color editor.
        /// </summary>
        public static IEnumerable<string> ColorNames => entries.Select(e => e.Name);

        public static Color Get(string name)
            => entries.FirstOrDefault(e => e.Name == name) is { Get: not null } entry
                   ? entry.Get()
                   : Bg;

        public static void Set(string name, Color value)
        {
            if (entries.FirstOrDefault(e => e.Name == name) is { Set: not null } entry)
            {
                entry.Set(value);
            }
        }

        private static readonly Dictionary<string, string> defaults =
            entries.ToDictionary(e => e.Name, e => e.Get().ToHex());

        public static void ResetToDefaults()
        {
            foreach (var entry in entries)
            {
                entry.Set(FromHex(defaults[entry.Name]));
            }
        }

        private static string ThemeFilePath
            => Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                            "CKAN", "theme.json");

        /// <summary>
        /// Load the saved palette, if any. A missing or corrupt file leaves the
        /// defaults in place rather than throwing: a bad theme must never stop
        /// the app from starting.
        /// </summary>
        public static void Load()
        {
            try
            {
                var path = ThemeFilePath;
                if (!File.Exists(path))
                {
                    return;
                }
                if (JsonConvert.DeserializeObject<Dictionary<string, string>>(File.ReadAllText(path))
                    is Dictionary<string, string> saved)
                {
                    foreach (var entry in entries)
                    {
                        if (saved.TryGetValue(entry.Name, out var hex)
                            && TryFromHex(hex, out var color))
                        {
                            entry.Set(color);
                        }
                    }
                }
            }
            catch
            {
                // Unreadable or malformed theme file; defaults are already loaded
            }
        }

        public static void Save()
        {
            try
            {
                var path = ThemeFilePath;
                if (Path.GetDirectoryName(path) is string dir)
                {
                    Directory.CreateDirectory(dir);
                }
                File.WriteAllText(path,
                                  JsonConvert.SerializeObject(
                                      entries.ToDictionary(e => e.Name, e => e.Get().ToHex()),
                                      Formatting.Indented));
            }
            catch
            {
                // Read-only profile or disk full; the palette still applies for this session
            }
        }

        private static Color FromHex(string hex)
            => TryFromHex(hex, out var color) ? color : Color.Magenta;

        private static bool TryFromHex(string hex, out Color color)
        {
            color = Color.Magenta;
            var trimmed = hex.TrimStart('#');
            if (trimmed.Length != 6
                || !int.TryParse(trimmed,
                                 System.Globalization.NumberStyles.HexNumber,
                                 System.Globalization.CultureInfo.InvariantCulture,
                                 out var rgb))
            {
                return false;
            }
            color = Color.FromArgb(255, (rgb >> 16) & 0xFF, (rgb >> 8) & 0xFF, rgb & 0xFF);
            return true;
        }

        #endregion

        #region Applying

        /// <summary>
        /// Style a control and everything inside it.
        /// Safe to call more than once on the same control.
        /// </summary>
        public static void Apply(Control control)
        {
            if (!Enabled)
            {
                return;
            }
            StyleOne(control);
            foreach (Control child in control.Controls)
            {
                Apply(child);
            }
        }

        /// <summary>
        /// Restyle every open window and tell self-painting controls to refresh.
        /// Called after the user edits the palette.
        /// </summary>
        public static void Reapply()
        {
            foreach (var form in Application.OpenForms.OfType<Form>())
            {
                Apply(form);
                form.Invalidate(true);
            }
            ThemeChanged?.Invoke();
        }

        /// <summary>
        /// Style windows as they appear, so dialogs created later by any part of
        /// the app get the theme without each one having to ask for it.
        /// </summary>
        public static void Hook()
        {
            if (!Enabled)
            {
                return;
            }
            if (Platform.IsWindows && IsDark)
            {
                // Has to happen before any window exists, or per-window dark mode
                // is refused and the scrollbars come up light
                NativeMethods.SetPreferredAppMode(NativeMethods.PreferredAppModeAllowDark);
            }
            Application.Idle += StyleNewForms;
        }

        private static readonly HashSet<Form> styled = new HashSet<Form>();

        private static void StyleNewForms(object? sender, EventArgs e)
        {
            foreach (var form in Application.OpenForms.OfType<Form>().ToArray())
            {
                if (styled.Add(form))
                {
                    Apply(form);
                    UseDarkTitleBar(form);
                    form.FormClosed += (_, _) => styled.Remove(form);
                }
            }
        }

        /// <summary>
        /// Ask the OS to draw this control's scrollbars dark. Has to wait for the
        /// handle, and has to be re-sent on recreation or the light ones come back.
        /// </summary>
        private static void UseDarkScrollBars(Control control)
        {
            if (!Platform.IsWindows || !IsDark)
            {
                return;
            }
            if (control.IsHandleCreated)
            {
                ApplyDarkWindowTheme(control.Handle);
            }
            if (darkScrollBarHooked.Add(control))
            {
                control.HandleCreated += (_, _) => ApplyDarkWindowTheme(control.Handle);
            }
        }

        private static void ApplyDarkWindowTheme(IntPtr handle)
        {
            NativeMethods.AllowDarkModeForWindow(handle);
            NativeMethods.SetWindowTheme(handle, "DarkMode_Explorer", null);
        }

        private static readonly HashSet<Control> darkScrollBarHooked = new HashSet<Control>();

        /// <summary>
        /// Ask the desktop window manager for a dark title bar, so the frame
        /// matches the window instead of being a white strip above it.
        /// </summary>
        public static void UseDarkTitleBar(Form form)
        {
            if (!Platform.IsWindows || !IsDark || !form.IsHandleCreated)
            {
                return;
            }
            var on = 1;
            NativeMethods.DwmSetWindowAttribute(form.Handle,
                                                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1,
                                                ref on, sizeof(int));
            NativeMethods.DwmSetWindowAttribute(form.Handle,
                                                NativeMethods.DWMWA_USE_IMMERSIVE_DARK_MODE,
                                                ref on, sizeof(int));
        }

        private static void StyleOne(Control control)
        {
            switch (control)
            {
                case Form form:
                    form.BackColor = Bg;
                    form.ForeColor = Text;
                    break;

                case Button button:
                    button.FlatStyle = FlatStyle.Flat;
                    button.BackColor = Raised;
                    button.ForeColor = Text;
                    button.UseVisualStyleBackColor = false;
                    button.FlatAppearance.BorderColor          = Border;
                    button.FlatAppearance.BorderSize           = 1;
                    button.FlatAppearance.MouseOverBackColor   = RaisedHover;
                    button.FlatAppearance.MouseDownBackColor   = Border;
                    break;

                case LinkLabel link:
                    link.BackColor        = Color.Transparent;
                    link.ForeColor        = Text;
                    link.LinkColor        = Accent;
                    link.ActiveLinkColor  = AccentHover;
                    link.VisitedLinkColor = Accent;
                    link.DisabledLinkColor = TextMuted;
                    break;

                case Label label:
                    label.BackColor = Color.Transparent;
                    label.ForeColor = Text;
                    break;

                case TextBoxBase textBox:
                    textBox.BackColor   = Field;
                    textBox.ForeColor   = Text;
                    textBox.BorderStyle = BorderStyle.FixedSingle;
                    UseDarkScrollBars(textBox);
                    break;

                case ComboBox combo:
                    combo.FlatStyle = FlatStyle.Flat;
                    combo.BackColor = Field;
                    combo.ForeColor = Text;
                    break;

                case ListBox listBox:
                    listBox.BackColor   = Field;
                    listBox.ForeColor   = Text;
                    listBox.BorderStyle = BorderStyle.FixedSingle;
                    UseDarkScrollBars(listBox);
                    break;

                case ListView listView:
                    listView.BackColor   = Field;
                    listView.ForeColor   = Text;
                    listView.BorderStyle = BorderStyle.FixedSingle;
                    UseDarkScrollBars(listView);
                    break;

                case TreeView treeView:
                    treeView.BackColor   = Field;
                    treeView.ForeColor   = Text;
                    treeView.LineColor   = Border;
                    treeView.BorderStyle = BorderStyle.FixedSingle;
                    UseDarkScrollBars(treeView);
                    break;

                case DataGridView grid:
                    StyleGrid(grid);
                    break;

                case NumericUpDown numeric:
                    numeric.BackColor   = Field;
                    numeric.ForeColor   = Text;
                    numeric.BorderStyle = BorderStyle.FixedSingle;
                    break;

                case CheckBox checkBox:
                    checkBox.BackColor = Color.Transparent;
                    checkBox.ForeColor = Text;
                    checkBox.FlatStyle = FlatStyle.Flat;
                    checkBox.FlatAppearance.BorderColor        = Border;
                    checkBox.FlatAppearance.CheckedBackColor    = Accent;
                    checkBox.FlatAppearance.MouseOverBackColor  = RaisedHover;
                    break;

                case RadioButton radio:
                    radio.BackColor = Color.Transparent;
                    radio.ForeColor = Text;
                    radio.FlatStyle = FlatStyle.Flat;
                    radio.FlatAppearance.BorderColor       = Border;
                    radio.FlatAppearance.CheckedBackColor   = Accent;
                    radio.FlatAppearance.MouseOverBackColor = RaisedHover;
                    break;

                case GroupBox groupBox:
                    groupBox.BackColor = Color.Transparent;
                    groupBox.ForeColor = TextMuted;
                    break;

                case ProgressBar progressBar:
                    progressBar.BackColor = Field;
                    progressBar.ForeColor = Accent;
                    break;

                case ToolStrip toolStrip:
                    StyleToolStrip(toolStrip);
                    break;

                case TabControl tabControl:
                    tabControl.BackColor = Bg;
                    tabControl.ForeColor = Text;
                    break;

                case TabPage tabPage:
                    tabPage.BackColor = Bg;
                    tabPage.ForeColor = Text;
                    break;

                case SplitContainer split:
                    // The splitter itself is the container's background, so this
                    // paints it as a hairline rule between the two panes
                    split.BackColor       = Border;
                    split.Panel1.BackColor = Bg;
                    split.Panel2.BackColor = Bg;
                    break;

                case ScrollBar scrollBar:
                    // Native dark theming only reaches the non-client scrollbars of
                    // a scrolling window, not standalone SCROLLBAR controls like the
                    // ones a DataGridView hosts, so this is a no-op there for now
                    UseDarkScrollBars(scrollBar);
                    break;

                case PictureBox:
                    // Whatever the image is, don't paint behind it
                    break;

                default:
                    control.BackColor = Bg;
                    control.ForeColor = Text;
                    break;
            }
        }

        private static void StyleGrid(DataGridView grid)
        {
            grid.EnableHeadersVisualStyles = false;
            grid.BackgroundColor           = Bg;
            grid.GridColor                 = GridLine;
            grid.BorderStyle               = BorderStyle.None;
            grid.ForeColor                 = Text;

            grid.ColumnHeadersDefaultCellStyle.BackColor          = Bg;
            grid.ColumnHeadersDefaultCellStyle.ForeColor          = TextMuted;
            grid.ColumnHeadersDefaultCellStyle.SelectionBackColor = Bg;
            grid.ColumnHeadersDefaultCellStyle.SelectionForeColor = TextMuted;
            grid.ColumnHeadersBorderStyle = DataGridViewHeaderBorderStyle.Single;

            grid.RowHeadersDefaultCellStyle.BackColor = Bg;
            grid.RowHeadersDefaultCellStyle.ForeColor = TextMuted;

            grid.DefaultCellStyle.BackColor          = Surface;
            grid.DefaultCellStyle.ForeColor          = Text;
            grid.DefaultCellStyle.SelectionBackColor = Selection;
            grid.DefaultCellStyle.SelectionForeColor = Text;

            grid.RowsDefaultCellStyle.BackColor          = Surface;
            grid.RowsDefaultCellStyle.ForeColor          = Text;
            grid.RowsDefaultCellStyle.SelectionBackColor = Selection;
            grid.RowsDefaultCellStyle.SelectionForeColor = Text;

            UseDarkScrollBars(grid);
            SmoothScroll.Attach(grid);
        }

        private static void StyleToolStrip(ToolStrip toolStrip)
        {
            toolStrip.BackColor = Surface;
            toolStrip.ForeColor = Text;
            toolStrip.Renderer  = new ModrinthToolStripRenderer();
            foreach (var item in toolStrip.Items.OfType<ToolStripItem>())
            {
                StyleToolStripItem(item);
            }
        }

        private static void StyleToolStripItem(ToolStripItem item)
        {
            item.BackColor = Surface;
            item.ForeColor = Text;
            if (item is ToolStripDropDownItem dropDown)
            {
                dropDown.DropDown.BackColor = Surface;
                dropDown.DropDown.ForeColor = Text;
                foreach (var child in dropDown.DropDownItems.OfType<ToolStripItem>())
                {
                    StyleToolStripItem(child);
                }
            }
        }

        #endregion
    }
}
