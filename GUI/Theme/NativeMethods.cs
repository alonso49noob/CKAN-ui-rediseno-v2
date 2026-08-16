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

        #region Scrollbar painting

        [StructLayout(LayoutKind.Sequential)]
        internal struct RECT
        {
            public int Left, Top, Right, Bottom;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct PAINTSTRUCT
        {
            public IntPtr hdc;
            public bool   fErase;
            public RECT   rcPaint;
            public bool   fRestore;
            public bool   fIncUpdate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public byte[] rgbReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        internal struct SCROLLINFO
        {
            public uint cbSize;
            public uint fMask;
            public int  nMin;
            public int  nMax;
            public uint nPage;
            public int  nPos;
            public int  nTrackPos;
        }

        /// <summary>The window is a scrollbar control in its own right</summary>
        internal const int  SB_CTL  = 2;
        /// <summary>Fill in every field of SCROLLINFO</summary>
        internal const uint SIF_ALL = 0x17;

        internal const int WM_PAINT      = 0x000F;
        internal const int WM_ERASEBKGND = 0x0014;

        [DllImport("user32.dll")]
        internal static extern bool GetScrollInfo(IntPtr hWnd, int fnBar, ref SCROLLINFO si);

        [DllImport("user32.dll")]
        internal static extern IntPtr BeginPaint(IntPtr hWnd, ref PAINTSTRUCT ps);

        [DllImport("user32.dll")]
        internal static extern bool EndPaint(IntPtr hWnd, ref PAINTSTRUCT ps);

        #endregion

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
