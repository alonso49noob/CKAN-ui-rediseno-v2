using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace CKAN.GUI
{
    /// <summary>
    /// Lets the user set every color in the palette, with the whole application
    /// repainting as they go so a choice can be judged in place rather than
    /// guessed at. Built in code rather than the designer, since the rows are
    /// generated from the palette itself and would otherwise drift from it.
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    [ExcludeFromCodeCoverage]
    public class ThemeColorsDialog : Form
    {
        public ThemeColorsDialog()
        {
            // Remember where everything started, so Cancel really cancels
            original = ModrinthTheme.ColorNames.ToDictionary(name => name,
                                                            ModrinthTheme.Get);

            Text            = Properties.Resources.ThemeColorsTitle;
            FormBorderStyle = FormBorderStyle.FixedDialog;
            StartPosition   = FormStartPosition.CenterParent;
            MaximizeBox     = false;
            MinimizeBox     = false;
            AutoScaleMode   = AutoScaleMode.Font;

            var table = new TableLayoutPanel()
            {
                // Docked to the top rather than filling: a filled table hands
                // its leftover height to the last row, which then sits adrift
                Dock        = DockStyle.Top,
                ColumnCount = 2,
                AutoSize    = true,
                Padding     = new Padding(12),
            };
            table.ColumnStyles.Add(new ColumnStyle(SizeType.AutoSize));
            table.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 150));

            foreach (var name in ModrinthTheme.ColorNames)
            {
                table.Controls.Add(new Label()
                {
                    Text      = Describe(name),
                    AutoSize  = true,
                    Anchor    = AnchorStyles.Left,
                    Margin    = new Padding(3, 8, 12, 3),
                });
                table.Controls.Add(MakeSwatch(name));
            }

            var buttons = new FlowLayoutPanel()
            {
                Dock          = DockStyle.Bottom,
                FlowDirection = FlowDirection.RightToLeft,
                AutoSize      = true,
                Padding       = new Padding(12, 6, 12, 12),
            };
            buttons.Controls.Add(MakeButton(Properties.Resources.ThemeColorsOK, OnOK));
            buttons.Controls.Add(MakeButton(Properties.Resources.ThemeColorsCancel, OnCancel));
            buttons.Controls.Add(MakeButton(Properties.Resources.ThemeColorsReset, OnReset));

            var scroll = new Panel() { Dock = DockStyle.Fill, AutoScroll = true };
            scroll.Controls.Add(table);
            Controls.Add(scroll);
            Controls.Add(buttons);

            // Tall enough for the whole palette at once: scrolling a list of
            // colors makes them impossible to compare
            ClientSize = new Size(430, 610);
            ModrinthTheme.Apply(this);
            // Apply skips the swatches, but paint them after it all the same so
            // they're right whatever order things happen in
            RepaintSwatches();
        }

        private Button MakeButton(string text, Action onClick)
        {
            var button = new Button()
            {
                Text     = text,
                AutoSize = true,
                Margin   = new Padding(6, 3, 6, 3),
            };
            button.Click += (_, _) => onClick();
            return button;
        }

        /// <summary>
        /// A button showing the color it stands for, with its hex on it, which
        /// opens the system color picker.
        /// </summary>
        private Button MakeSwatch(string name)
        {
            var swatch = new Button()
            {
                // Anchored rather than docked, or the last row stretches it to
                // fill whatever vertical space is left over
                Anchor    = AnchorStyles.Left | AnchorStyles.Right,
                Height    = 28,
                Width     = 144,
                FlatStyle = FlatStyle.Flat,
                Margin    = new Padding(3, 4, 3, 4),
                // The theme must not restyle these: their color is the content
                Tag       = ModrinthTheme.SkipTag,
            };
            swatches[name] = swatch;
            PaintSwatch(name);
            swatch.Click += (_, _) =>
            {
                using (var picker = new ColorDialog()
                                    {
                                        Color        = ModrinthTheme.Get(name),
                                        FullOpen     = true,
                                        AnyColor     = true,
                                        CustomColors = CustomColors(),
                                    })
                {
                    if (picker.ShowDialog(this) == DialogResult.OK)
                    {
                        ModrinthTheme.Set(name, picker.Color);
                        // Repaint everything, including this dialog
                        ModrinthTheme.Reapply();
                        RepaintSwatches();
                    }
                }
            };
            return swatch;
        }

        /// <summary>Seed the picker's custom slots with the rest of the palette</summary>
        private static int[] CustomColors()
            => ModrinthTheme.ColorNames
                            .Select(ModrinthTheme.Get)
                            // The dialog wants BGR, not RGB
                            .Select(c => c.R | (c.G << 8) | (c.B << 16))
                            .ToArray();

        private void PaintSwatch(string name)
        {
            if (swatches.TryGetValue(name, out var swatch))
            {
                var color = ModrinthTheme.Get(name);
                swatch.BackColor = color;
                swatch.ForeColor = color.IsLight() ? Color.Black : Color.White;
                swatch.Text      = color.ToHex();
                swatch.FlatAppearance.BorderColor = ModrinthTheme.Border;
                swatch.UseVisualStyleBackColor    = false;
            }
        }

        private void RepaintSwatches()
        {
            foreach (var name in swatches.Keys.ToArray())
            {
                PaintSwatch(name);
            }
        }

        private void OnReset()
        {
            ModrinthTheme.ResetToDefaults();
            ModrinthTheme.Reapply();
            RepaintSwatches();
        }

        private void OnOK()
        {
            ModrinthTheme.Save();
            DialogResult = DialogResult.OK;
            Close();
        }

        private void OnCancel()
        {
            foreach (var (name, color) in original)
            {
                ModrinthTheme.Set(name, color);
            }
            ModrinthTheme.Reapply();
            DialogResult = DialogResult.Cancel;
            Close();
        }

        /// <summary>
        /// Turns a palette key into something worth reading. Anything without a
        /// description falls back to its own name rather than going blank.
        /// </summary>
        private static string Describe(string name)
            => name switch
               {
                   "Bg"          => Properties.Resources.ThemeColorBg,
                   "Surface"     => Properties.Resources.ThemeColorSurface,
                   "Field"       => Properties.Resources.ThemeColorField,
                   "Raised"      => Properties.Resources.ThemeColorRaised,
                   "RaisedHover" => Properties.Resources.ThemeColorRaisedHover,
                   "Border"      => Properties.Resources.ThemeColorBorder,
                   "GridLine"    => Properties.Resources.ThemeColorGridLine,
                   "Text"        => Properties.Resources.ThemeColorText,
                   "TextMuted"   => Properties.Resources.ThemeColorTextMuted,
                   "Accent"      => Properties.Resources.ThemeColorAccent,
                   "AccentHover" => Properties.Resources.ThemeColorAccentHover,
                   "AccentDown"  => Properties.Resources.ThemeColorAccentDown,
                   "Selection"   => Properties.Resources.ThemeColorSelection,
                   _             => name,
               };

        private readonly Dictionary<string, Color>  original;
        private readonly Dictionary<string, Button> swatches = new Dictionary<string, Button>();
    }
}
