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

        /// <summary>
        /// Switches a control to the dark visual style, which is the only way to
        /// get dark scrollbars: they're drawn by the OS, not by WinForms, so no
        /// amount of BackColor reaches them.
        /// </summary>
        [DllImport("uxtheme.dll", CharSet = CharSet.Unicode)]
        internal static extern int SetWindowTheme(IntPtr hWnd, string? subAppName, string? subIdList);

        // Dark scrollbars need two undocumented uxtheme calls before SetWindowTheme
        // will take: one to let the process use dark mode, one per window. They're
        // exported by ordinal only, so every call site must tolerate their absence
        // on older Windows builds.

        [DllImport("uxtheme.dll", EntryPoint = "#133", SetLastError = true)]
        private static extern bool AllowDarkModeForWindowNative(IntPtr hWnd, bool allow);

        [DllImport("uxtheme.dll", EntryPoint = "#135", SetLastError = true)]
        private static extern int SetPreferredAppModeNative(int mode);

        /// <summary>AllowDark, so the process may use dark controls</summary>
        internal const int PreferredAppModeAllowDark = 1;

        internal static void AllowDarkModeForWindow(IntPtr hWnd)
        {
            try
            {
                AllowDarkModeForWindowNative(hWnd, true);
            }
            catch (EntryPointNotFoundException)
            {
                // Windows too old to have it; scrollbars stay light
            }
            catch (DllNotFoundException)
            {
            }
        }

        internal static void SetPreferredAppMode(int mode)
        {
            try
            {
                SetPreferredAppModeNative(mode);
            }
            catch (EntryPointNotFoundException)
            {
            }
            catch (DllNotFoundException)
            {
            }
        }

        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE_BEFORE_20H1 = 19;
        internal const int DWMWA_USE_IMMERSIVE_DARK_MODE             = 20;
    }
}
