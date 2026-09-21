using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Interop;

namespace TheEye.Services;

public static class DisplayGeometry
{
    public static void CoverMonitor(Window window)
    {
        var screen = System.Windows.Forms.Screen.FromPoint(System.Windows.Forms.Cursor.Position);
        var handle = new WindowInteropHelper(window).EnsureHandle();
        SetWindowPos(handle, new IntPtr(-1), screen.Bounds.X, screen.Bounds.Y,
            screen.Bounds.Width, screen.Bounds.Height, 0x0040);
    }
    [DllImport("user32.dll")]
    private static extern bool SetWindowPos(IntPtr window, IntPtr insertAfter, int x, int y, int width, int height, uint flags);
}
