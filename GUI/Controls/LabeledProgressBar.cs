using System.Drawing;
using System.Windows.Forms;
using System.ComponentModel;
using System.Diagnostics.CodeAnalysis;
#if NET5_0_OR_GREATER
using System.Runtime.Versioning;
#endif

namespace CKAN.GUI
{
    /// <summary>
    /// https://stackoverflow.com/a/40824778
    /// </summary>
    #if NET5_0_OR_GREATER
    [SupportedOSPlatform("windows")]
    #endif
    public class LabeledProgressBar : ProgressBar
    {
        public LabeledProgressBar()
            : base()
        {
            SetStyle(ControlStyles.OptimizedDoubleBuffer
                     | ControlStyles.UserPaint,
                     true);
            Text = "";
        }

        [Bindable(false)]
        [Browsable(true)]
        [DefaultValue("")]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        // If we use override instead of new, the nullability never matches (!)
        public new string Text {
            get => base.Text;
            [MemberNotNull(nameof(textSize))]
            set
            {
                base.Text = value;
                textSize  = TextRenderer.MeasureText(Text, Font);
            }
        }

        [Bindable(false)]
        [Browsable(true)]
        [DesignerSerializationVisibility(DesignerSerializationVisibility.Visible)]
        [EditorBrowsable(EditorBrowsableState.Always)]
        // If we use override instead of new, the nullability never matches (!)
        public new Font Font
        {
            get => base.Font;
            [MemberNotNull(nameof(textSize))]
            set
            {
                base.Font = value;
                textSize  = TextRenderer.MeasureText(Text, Font);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            var filledRect = Rectangle.Empty;
            if (ProgressBarRenderer.IsSupported
                // ProgressBarRenderer draws the wrong background color in net10's dark mode
                && !Util.DarkMode)
            {
                ProgressBarRenderer.DrawHorizontalBar(e.Graphics, ClientRectangle);
                ProgressBarRenderer.DrawHorizontalChunks(e.Graphics,
                                                         new Rectangle(ClientRectangle.X,
                                                                       ClientRectangle.Y,
                                                                       ClientRectangle.Width * (Value   - Minimum)
                                                                                             / (Maximum - Minimum),
                                                                       ClientRectangle.Height));
            }
            else
            {
                const int borderWidth = 1;
                var innerRect = Rectangle.Inflate(ClientRectangle, -2 * borderWidth,
                                                                   -2 * borderWidth);
                innerRect.Offset(borderWidth, borderWidth);
                filledRect = new Rectangle(innerRect.X,
                                           innerRect.Y,
                                           innerRect.Width * (Value   - Minimum)
                                                           / (Maximum - Minimum),
                                           innerRect.Height);
                if (ModrinthTheme.Enabled)
                {
                    using (var borderPen  = new Pen(ModrinthTheme.Border))
                    using (var trackBrush = new SolidBrush(ModrinthTheme.Field))
                    using (var fillBrush  = new SolidBrush(ModrinthTheme.Accent))
                    {
                        e.Graphics.DrawRectangle(borderPen, ClientRectangle);
                        e.Graphics.FillRectangle(trackBrush, innerRect);
                        e.Graphics.FillRectangle(fillBrush, filledRect);
                    }
                }
                else
                {
                    e.Graphics.DrawRectangle(SystemPens.ControlDark, ClientRectangle);
                    e.Graphics.FillRectangle(SystemBrushes.Control, innerRect);
                    e.Graphics.FillRectangle(SystemBrushes.Highlight, filledRect);
                }
            }
            var textPoint = new Point((Width  - textSize.Width)  / 2,
                                      (Height - textSize.Height) / 2);
            if (ModrinthTheme.Enabled)
            {
                // The label crosses from the filled part to the empty part, so
                // draw it twice: dark where it sits on the accent, light elsewhere
                TextRenderer.DrawText(e.Graphics, Text, Font, textPoint, ModrinthTheme.Text);
                var saved = e.Graphics.Clip;
                e.Graphics.SetClip(filledRect);
                TextRenderer.DrawText(e.Graphics, Text, Font, textPoint, ModrinthTheme.Bg);
                e.Graphics.Clip = saved;
            }
            else
            {
                TextRenderer.DrawText(e.Graphics, Text, Font, textPoint, SystemColors.ControlText);
            }
        }

        private Size textSize;
    }
}
