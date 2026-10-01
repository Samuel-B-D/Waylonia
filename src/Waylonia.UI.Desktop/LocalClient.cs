using System.Diagnostics;
using System.Globalization;
using System.Runtime.InteropServices;

namespace Waylonia;

/// <summary>
/// Starts a local client through <c>/bin/sh -c</c> against the compositor's socket and stops it with
/// its whole process tree.
/// </summary>
internal static class LocalClient
{
    private const int Sigkill = 9;

    private const int MaxTreeDepth = 8;

    /// <summary>Starts <paramref name="command"/> with <c>WAYLAND_DISPLAY</c> set to <paramref name="socket"/>.</summary>
    /// <param name="command">The shell command line; null or blank starts nothing.</param>
    /// <param name="socket">The Wayland socket name the client connects to.</param>
    /// <param name="environment">Variables to set, or to remove when the value is null.</param>
    /// <returns>The started process, or null when nothing was started.</returns>
    public static Process? Start(
        string? command,
        string socket,
        IReadOnlyList<(string Name, string? Value)>? environment = null)
    {
        ArgumentNullException.ThrowIfNull(socket);
        if (string.IsNullOrWhiteSpace(command))
        {
            return null;
        }

        var info = new ProcessStartInfo("/bin/sh") { UseShellExecute = false };
        info.ArgumentList.Add("-c");
        info.ArgumentList.Add(command);
        info.Environment["WAYLAND_DISPLAY"] = socket;
        info.Environment["XDG_SESSION_TYPE"] = "wayland";
        if (environment is not null)
        {
            foreach (var (name, value) in environment)
            {
                if (value is null)
                {
                    info.Environment.Remove(name);
                }
                else
                {
                    info.Environment[name] = value;
                }
            }
        }

        return Process.Start(info);
    }

    /// <summary>Kills <paramref name="process"/> and its descendants, waits for it and disposes it.</summary>
    /// <param name="process">The process <see cref="Start"/> returned.</param>
    /// <param name="timeoutMs">How long to wait for the exit.</param>
    public static void Stop(Process? process, int timeoutMs = 2000)
    {
        if (process is { HasExited: false })
        {
            if (!TryKillTree(process.Id, 0))
            {
                process.Kill(entireProcessTree: true);
            }

            process.WaitForExit(timeoutMs);
        }

        process?.Dispose();
    }

    [DllImport("libc", SetLastError = true, EntryPoint = "kill")]
    private static extern int Kill(int pid, int signal);

    private static bool TryKillTree(int pid, int depth)
    {
        var tasks = $"/proc/{pid.ToString(CultureInfo.InvariantCulture)}/task";
        if (!Directory.Exists(tasks))
        {
            return false;
        }

        if (depth < MaxTreeDepth)
        {
            foreach (var task in Directory.EnumerateDirectories(tasks))
            {
                var children = Path.Combine(task, "children");
                if (!File.Exists(children))
                {
                    return false;
                }

                string listed;
                try
                {
                    listed = File.ReadAllText(children);
                }
                catch (IOException)
                {
                    continue;
                }

                foreach (var entry in listed.Split(' ', StringSplitOptions.RemoveEmptyEntries))
                {
                    if (int.TryParse(entry, CultureInfo.InvariantCulture, out var child))
                    {
                        TryKillTree(child, depth + 1);
                    }
                }
            }
        }

        _ = Kill(pid, Sigkill);
        return true;
    }
}
