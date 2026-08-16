using System;
using System.Runtime.InteropServices;
using System.Diagnostics.CodeAnalysis;

namespace CKAN.GUI
{
    [ExcludeFromCodeCoverage]
    internal static class NativeMethods
    {
        [DllImport("dwmapi.dll")]
        internal static extern int DwmSetWindowAttribute(IntPtr hwnd, int attr, ref int attrValue, int attrSize);

        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE             = 20;
    }
}
