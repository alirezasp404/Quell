using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.InteropServices;
using Fdb.Models;

namespace Fdb.Services.Terminal;

public class ShellDetectorService
{
    private static ShellDetectorService? _instance;
    public static ShellDetectorService Instance => _instance ??= new ShellDetectorService();

    public List<ShellProfile> GetAvailableShells()
    {
        var shells = new List<ShellProfile>();

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 1. PowerShell Core (pwsh)
            var pwshPath = FindInPath("pwsh.exe");
            if (!string.IsNullOrEmpty(pwshPath))
            {
                shells.Add(new ShellProfile
                {
                    Id = "pwsh",
                    DisplayName = "PowerShell 7+",
                    ExecutablePath = pwshPath,
                    IconKey = "PowerShell"
                });
            }

            // 2. Windows PowerShell (powershell.exe)
            var winPowerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");

            if (File.Exists(winPowerShell))
            {
                shells.Add(new ShellProfile
                {
                    Id = "powershell",
                    DisplayName = "Windows PowerShell",
                    ExecutablePath = winPowerShell,
                    IconKey = "PowerShell"
                });
            }

            // 3. Command Prompt (cmd.exe)
            var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (File.Exists(cmdPath))
            {
                shells.Add(new ShellProfile
                {
                    Id = "cmd",
                    DisplayName = "Command Prompt",
                    ExecutablePath = cmdPath,
                    IconKey = "Cmd"
                });
            }

            // 4. Git Bash
            var gitBashPaths = new[]
            {
                @"C:\Program Files\Git\bin\bash.exe",
                @"C:\Program Files (x86)\Git\bin\bash.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\bash.exe")
            };

            foreach (var path in gitBashPaths)
            {
                if (File.Exists(path))
                {
                    shells.Add(new ShellProfile
                    {
                        Id = "gitbash",
                        DisplayName = "Git Bash",
                        ExecutablePath = path,
                        Arguments = "--login -i",
                        IconKey = "Git"
                    });
                    break;
                }
            }

            // 5. WSL (wsl.exe)
            var wslPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
            if (File.Exists(wslPath))
            {
                shells.Add(new ShellProfile
                {
                    Id = "wsl",
                    DisplayName = "WSL (Linux)",
                    ExecutablePath = wslPath,
                    IconKey = "Linux"
                });
            }
        }
        else
        {
            // Unix/Linux/macOS
            var candidates = new (string id, string name, string path)[]
            {
                ("zsh", "Zsh", "/bin/zsh"),
                ("bash", "Bash", "/bin/bash"),
                ("fish", "Fish", "/usr/bin/fish"),
                ("pwsh", "PowerShell", "/usr/bin/pwsh")
            };

            foreach (var (id, name, path) in candidates)
            {
                if (File.Exists(path))
                {
                    shells.Add(new ShellProfile
                    {
                        Id = id,
                        DisplayName = name,
                        ExecutablePath = path,
                        IconKey = "Terminal"
                    });
                }
                else
                {
                    var inPath = FindInPath(id);
                    if (!string.IsNullOrEmpty(inPath))
                    {
                        shells.Add(new ShellProfile
                        {
                            Id = id,
                            DisplayName = name,
                            ExecutablePath = inPath,
                            IconKey = "Terminal"
                        });
                    }
                }
            }
        }

        // Fallback default if none detected
        if (shells.Count == 0)
        {
            var fallback = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) ? "cmd.exe" : "/bin/sh";
            shells.Add(new ShellProfile
            {
                Id = "default",
                DisplayName = "System Shell",
                ExecutablePath = fallback,
                IconKey = "Terminal"
            });
        }

        return shells;
    }

    public ShellProfile GetDefaultShell(string? preferredId = null)
    {
        var shells = GetAvailableShells();
        if (!string.IsNullOrEmpty(preferredId))
        {
            var matched = shells.Find(s => string.Equals(s.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return matched;
        }

        // Default preference order: pwsh > powershell > zsh > bash > cmd
        var preferredOrder = new[] { "pwsh", "powershell", "zsh", "bash", "cmd" };
        foreach (var pref in preferredOrder)
        {
            var found = shells.Find(s => s.Id == pref);
            if (found != null) return found;
        }

        return shells[0];
    }

    public static string? FindInPath(string executableName)
    {
        if (File.Exists(executableName)) return Path.GetFullPath(executableName);

        var pathEnv = Environment.GetEnvironmentVariable("PATH");
        if (string.IsNullOrEmpty(pathEnv)) return null;

        var paths = pathEnv.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries);
        var extensions = RuntimeInformation.IsOSPlatform(OSPlatform.Windows)
            ? (Environment.GetEnvironmentVariable("PATHEXT")?.Split(';') ?? new[] { ".exe", ".cmd", ".bat" })
            : new[] { "" };

        foreach (var dir in paths)
        {
            try
            {
                var trimmed = dir.Trim().Trim('"');
                if (!Directory.Exists(trimmed)) continue;

                var directPath = Path.Combine(trimmed, executableName);
                if (File.Exists(directPath)) return directPath;

                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && !Path.HasExtension(executableName))
                {
                    foreach (var ext in extensions)
                    {
                        var withExt = Path.Combine(trimmed, executableName + ext);
                        if (File.Exists(withExt)) return withExt;
                    }
                }
            }
            catch
            {
                // Ignore path access errors
            }
        }

        return null;
    }
}
