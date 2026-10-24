using System.Runtime.InteropServices;
using System.Text;

namespace Eraf.Windows;

/// <summary>A captured picture: top-down BGRA pixels (the alpha byte is ignored).</summary>
public sealed record CapturedFrame(byte[] Bgra, int Width, int Height);

/// <summary>A top-level window that can be offered for capture.</summary>
public sealed record CapturableWindow(IntPtr Handle, string Title);

/// <summary>Takes pictures of the screen or of one window with plain GDI, so it needs no extra permission and no extra package.</summary>
public static class ScreenCapture
{
    // ------------------------------------------------------------------ the whole screen

    /// <summary>Every monitor together (the "virtual screen"), in real pixels.</summary>
    public static CapturedFrame? CaptureVirtualScreen()
    {
        int x = GetSystemMetrics(SM_XVIRTUALSCREEN);
        int y = GetSystemMetrics(SM_YVIRTUALSCREEN);
        int width = GetSystemMetrics(SM_CXVIRTUALSCREEN);
        int height = GetSystemMetrics(SM_CYVIRTUALSCREEN);
        if (!ScreenshotRules.IsSaneSize(width, height))
        {
            return null;
        }

        IntPtr screen = GetDC(IntPtr.Zero);
        if (screen == IntPtr.Zero)
        {
            return null;
        }

        try
        {
            return CopyFromDc(screen, x, y, width, height);
        }
        finally
        {
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    // ------------------------------------------------------------------ one window

    /// <summary>
    /// The window as it looks (even if something is in front of it), without its invisible shadow border. Null if it cannot be
    /// captured (closed, minimized, or protected by the app that owns it).
    /// </summary>
    public static CapturedFrame? CaptureWindow(IntPtr window)
    {
        if (!GetWindowRect(window, out RECT outer) || IsIconic(window))
        {
            return null;
        }

        int width = outer.Right - outer.Left;
        int height = outer.Bottom - outer.Top;
        if (!ScreenshotRules.IsSaneSize(width, height))
        {
            return null;
        }

        IntPtr screen = GetDC(IntPtr.Zero);
        IntPtr memory = CreateCompatibleDC(screen);
        IntPtr bitmap = CreateCompatibleBitmap(screen, width, height);
        IntPtr old = SelectObject(memory, bitmap);
        try
        {
            // PW_RENDERFULLCONTENT (2) asks Windows to draw modern apps too; if that fails, copy what is on screen instead.
            if (!PrintWindow(window, memory, PW_RENDERFULLCONTENT))
            {
                BitBlt(memory, 0, 0, width, height, screen, outer.Left, outer.Top, SRCCOPY);
            }

            CapturedFrame? full = ReadPixels(memory, bitmap, width, height);
            if (full is null)
            {
                return null;
            }

            // Crop to the visible frame (the outer rectangle includes a transparent drop-shadow margin on current Windows).
            if (DwmGetWindowAttribute(window, DWMWA_EXTENDED_FRAME_BOUNDS, out RECT visible, Marshal.SizeOf<RECT>()) == 0)
            {
                int left = Math.Clamp(visible.Left - outer.Left, 0, width - 1);
                int top = Math.Clamp(visible.Top - outer.Top, 0, height - 1);
                int cropWidth = Math.Clamp(visible.Right - visible.Left, 1, width - left);
                int cropHeight = Math.Clamp(visible.Bottom - visible.Top, 1, height - top);
                return Crop(full, left, top, cropWidth, cropHeight);
            }

            return full;
        }
        finally
        {
            SelectObject(memory, old);
            DeleteObject(bitmap);
            DeleteDC(memory);
            ReleaseDC(IntPtr.Zero, screen);
        }
    }

    /// <summary>Visible, titled, non-minimized top-level windows in front-to-back order, without <paramref name="exclude"/> (the app itself).</summary>
    public static List<CapturableWindow> ListWindows(IntPtr exclude)
    {
        var found = new List<CapturableWindow>();
        EnumWindows((handle, _) =>
        {
            if (handle == exclude || !IsWindowVisible(handle))
            {
                return true;
            }

            long style = GetWindowLongPtr(handle, GWL_EXSTYLE).ToInt64();
            if ((style & WS_EX_TOOLWINDOW) != 0 || (DwmGetWindowAttribute(handle, DWMWA_CLOAKED, out int cloaked, sizeof(int)) == 0 && cloaked != 0))
            {
                return true;
            }

            int length = GetWindowTextLengthW(handle);
            if (length <= 0 || !GetWindowRect(handle, out RECT rect))
            {
                return true;
            }

            var title = new StringBuilder(length + 1);
            GetWindowTextW(handle, title, title.Capacity);
            if (ScreenshotRules.IsCapturableWindow(title.ToString(), rect.Right - rect.Left, rect.Bottom - rect.Top, IsIconic(handle)))
            {
                found.Add(new CapturableWindow(handle, title.ToString()));
            }

            return true;
        }, IntPtr.Zero);
        return found;
    }

    // ------------------------------------------------------------------ pixels

    private static CapturedFrame? CopyFromDc(IntPtr source, int x, int y, int width, int height)
    {
        IntPtr memory = CreateCompatibleDC(source);
        IntPtr bitmap = CreateCompatibleBitmap(source, width, height);
        IntPtr old = SelectObject(memory, bitmap);
        try
        {
            // CAPTUREBLT also copies windows that are layered or transparent.
            return BitBlt(memory, 0, 0, width, height, source, x, y, SRCCOPY | CAPTUREBLT)
                ? ReadPixels(memory, bitmap, width, height)
                : null;
        }
        finally
        {
            SelectObject(memory, old);
            DeleteObject(bitmap);
            DeleteDC(memory);
        }
    }

    private static CapturedFrame? ReadPixels(IntPtr memory, IntPtr bitmap, int width, int height)
    {
        var info = new BITMAPINFO
        {
            Header = new BITMAPINFOHEADER
            {
                Size = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
                Width = width,
                Height = -height, // negative: rows run top to bottom
                Planes = 1,
                BitCount = 32,
                Compression = 0,
            },
        };
        byte[] pixels = new byte[checked(width * height * 4)];
        return GetDIBits(memory, bitmap, 0, (uint)height, pixels, ref info, 0) == height
            ? new CapturedFrame(pixels, width, height)
            : null;
    }

    private static CapturedFrame Crop(CapturedFrame frame, int left, int top, int width, int height)
    {
        if (left == 0 && top == 0 && width == frame.Width && height == frame.Height)
        {
            return frame;
        }

        byte[] cropped = new byte[width * height * 4];
        for (int row = 0; row < height; row++)
        {
            Buffer.BlockCopy(frame.Bgra, ((top + row) * frame.Width + left) * 4, cropped, row * width * 4, width * 4);
        }

        return new CapturedFrame(cropped, width, height);
    }

    // ------------------------------------------------------------------ Win32

    private const int SM_XVIRTUALSCREEN = 76;
    private const int SM_YVIRTUALSCREEN = 77;
    private const int SM_CXVIRTUALSCREEN = 78;
    private const int SM_CYVIRTUALSCREEN = 79;
    private const uint SRCCOPY = 0x00CC0020;
    private const uint CAPTUREBLT = 0x40000000;
    private const uint PW_RENDERFULLCONTENT = 2;
    private const int GWL_EXSTYLE = -20;
    private const long WS_EX_TOOLWINDOW = 0x80;
    private const int DWMWA_EXTENDED_FRAME_BOUNDS = 9;
    private const int DWMWA_CLOAKED = 14;

    private delegate bool EnumWindowsProc(IntPtr handle, IntPtr parameter);

    [StructLayout(LayoutKind.Sequential)]
    private struct RECT
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint Size;
        public int Width;
        public int Height;
        public ushort Planes;
        public ushort BitCount;
        public uint Compression;
        public uint SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public uint ColorsUsed;
        public uint ColorsImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFO
    {
        public BITMAPINFOHEADER Header;
        public uint Colors;
    }

    [DllImport("user32.dll")] private static extern int GetSystemMetrics(int index);
    [DllImport("user32.dll")] private static extern IntPtr GetDC(IntPtr window);
    [DllImport("user32.dll")] private static extern int ReleaseDC(IntPtr window, IntPtr dc);
    [DllImport("user32.dll")] private static extern bool GetWindowRect(IntPtr window, out RECT rect);
    [DllImport("user32.dll")] private static extern bool IsIconic(IntPtr window);
    [DllImport("user32.dll")] private static extern bool IsWindowVisible(IntPtr window);
    [DllImport("user32.dll")] private static extern bool PrintWindow(IntPtr window, IntPtr dc, uint flags);
    [DllImport("user32.dll")] private static extern bool EnumWindows(EnumWindowsProc callback, IntPtr parameter);
    [DllImport("user32.dll", CharSet = CharSet.Unicode)] private static extern int GetWindowTextW(IntPtr window, StringBuilder text, int max);
    [DllImport("user32.dll")] private static extern int GetWindowTextLengthW(IntPtr window);
    [DllImport("user32.dll", EntryPoint = "GetWindowLongPtrW")] private static extern IntPtr GetWindowLongPtr(IntPtr window, int index);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern IntPtr CreateCompatibleBitmap(IntPtr dc, int width, int height);
    [DllImport("gdi32.dll")] private static extern IntPtr SelectObject(IntPtr dc, IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteObject(IntPtr obj);
    [DllImport("gdi32.dll")] private static extern bool DeleteDC(IntPtr dc);
    [DllImport("gdi32.dll")] private static extern bool BitBlt(IntPtr dest, int x, int y, int width, int height, IntPtr source, int sx, int sy, uint rop);
    [DllImport("gdi32.dll")] private static extern int GetDIBits(IntPtr dc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFO info, uint usage);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out RECT value, int size);
    [DllImport("dwmapi.dll")] private static extern int DwmGetWindowAttribute(IntPtr window, int attribute, out int value, int size);
}
