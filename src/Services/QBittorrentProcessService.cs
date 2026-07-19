using System.Diagnostics;
using System.Runtime.InteropServices;

namespace QBittorrentProfileManager.Services;

public sealed class QBittorrentProcessService
{
    private static readonly string[] ProcessNames = ["qBittorrent", "qbittorrent"];

    public IReadOnlyList<Process> GetRunningProcesses()
    {
        var result = new List<Process>();
        foreach (var name in ProcessNames)
        {
            try
            {
                result.AddRange(Process.GetProcessesByName(name));
            }
            catch
            {
                // Ignore inaccessible processes.
            }
        }

        return result
            .GroupBy(p => p.Id)
            .Select(g => g.First())
            .ToList();
    }

    public bool IsRunning(out int? processId)
    {
        var processes = GetRunningProcesses();
        if (processes.Count == 0)
        {
            processId = null;
            return false;
        }

        processId = processes[0].Id;
        return true;
    }

    public async Task<bool> TryCloseGracefullyAsync(TimeSpan timeout, CancellationToken cancellationToken = default)
    {
        var processes = GetRunningProcesses();
        if (processes.Count == 0)
            return true;

        foreach (var process in processes)
        {
            try
            {
                _ = process.CloseMainWindow();
                CloseProcessWindows(process);
            }
            catch
            {
                // Continue with other processes.
            }
        }

        var deadline = DateTime.UtcNow + timeout;
        while (DateTime.UtcNow < deadline)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (!IsRunning(out _))
                return true;

            await Task.Delay(500, cancellationToken);
        }

        return !IsRunning(out _);
    }

    public void ForceKill()
    {
        foreach (var process in GetRunningProcesses())
        {
            try
            {
                if (!process.HasExited)
                    process.Kill(entireProcessTree: true);
            }
            catch
            {
                // Ignore kill failures.
            }
            finally
            {
                process.Dispose();
            }
        }
    }

    private static void CloseProcessWindows(Process process)
    {
        EnumWindows((hwnd, lParam) =>
        {
            GetWindowThreadProcessId(hwnd, out var windowProcessId);
            if (windowProcessId == process.Id && IsWindowVisible(hwnd))
                PostMessage(hwnd, WM_CLOSE, IntPtr.Zero, IntPtr.Zero);

            return true;
        }, IntPtr.Zero);
    }

    private const uint WM_CLOSE = 0x0010;

    private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

    [DllImport("user32.dll")]
    private static extern bool IsWindowVisible(IntPtr hWnd);

    [DllImport("user32.dll")]
    private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

    [DllImport("user32.dll", SetLastError = true)]
    private static extern bool PostMessage(IntPtr hWnd, uint msg, IntPtr wParam, IntPtr lParam);
}
