using System;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using Avalonia.Threading;

namespace Quell.Services;

/// <summary>
/// Cross-platform resilient clipboard service that handles Windows OLE/Win32 locks with retries
/// and safe fallback to Avalonia clipboard APIs on UI Dispatcher.
/// </summary>
public static class ClipboardService
{
    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool OpenClipboard(IntPtr hWndNewOwner);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool CloseClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool EmptyClipboard();

    [DllImport("user32.dll", SetLastError = true)]
    private static extern IntPtr SetClipboardData(uint uFormat, IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalAlloc(uint uFlags, UIntPtr dwBytes);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalLock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern bool GlobalUnlock(IntPtr hMem);

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern IntPtr GlobalFree(IntPtr hMem);

    private const uint CF_UNICODETEXT = 13;
    private const uint GMEM_MOVEABLE = 0x0002;

    /// <summary>
    /// Copies text to the system clipboard across platforms with retry logic and exception shielding.
    /// </summary>
    public static async Task<bool> CopyTextAsync(string text, Visual? visual = null)
    {
        if (string.IsNullOrEmpty(text))
        {
            return false;
        }

        // 1. Direct Win32 clipboard API on Windows (immune to Avalonia Dispatcher thread violations & OLE apartment mismatches)
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            if (TrySetTextWindows(text))
            {
                return true;
            }
        }

        // 2. Avalonia Clipboard API fallback (scheduled on Dispatcher.UIThread)
        try
        {
            return await Dispatcher.UIThread.InvokeAsync(async () =>
            {
                IClipboard? clipboard = null;
                if (visual != null)
                {
                    clipboard = TopLevel.GetTopLevel(visual)?.Clipboard;
                }

                if (clipboard == null && Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop)
                {
                    clipboard = desktop.MainWindow?.Clipboard;
                }

                if (clipboard != null)
                {
                    for (int attempt = 0; attempt < 5; attempt++)
                    {
                        try
                        {
                            await clipboard.SetTextAsync(text);
                            return true;
                        }
                        catch when (attempt < 4)
                        {
                            await Task.Delay(50);
                        }
                    }
                }

                return false;
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ClipboardService] Copy failed: {ex.Message}");
            return false;
        }
    }

    private static bool TrySetTextWindows(string text)
    {
        for (int attempt = 0; attempt < 5; attempt++)
        {
            if (OpenClipboard(IntPtr.Zero))
            {
                try
                {
                    EmptyClipboard();
                    int bytes = (text.Length + 1) * 2;
                    IntPtr hGlobal = GlobalAlloc(GMEM_MOVEABLE, (UIntPtr)bytes);
                    if (hGlobal != IntPtr.Zero)
                    {
                        IntPtr target = GlobalLock(hGlobal);
                        if (target != IntPtr.Zero)
                        {
                            try
                            {
                                Marshal.Copy(text.ToCharArray(), 0, target, text.Length);
                                Marshal.WriteInt16(target + (text.Length * 2), 0);
                                SetClipboardData(CF_UNICODETEXT, hGlobal);
                                hGlobal = IntPtr.Zero; // ownership successfully transferred to system
                                return true;
                            }
                            finally
                            {
                                GlobalUnlock(target);
                            }
                        }

                        if (hGlobal != IntPtr.Zero)
                        {
                            GlobalFree(hGlobal);
                        }
                    }
                }
                finally
                {
                    CloseClipboard();
                }
            }

            Thread.Sleep(50);
        }

        return false;
    }
}
