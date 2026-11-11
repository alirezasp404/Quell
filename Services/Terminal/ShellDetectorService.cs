using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Quell.Models;

namespace Quell.Services.Terminal;

public class ShellDetectorService
{
    private static ShellDetectorService? _instance;
    public static ShellDetectorService Instance => _instance ??= new ShellDetectorService();

    private readonly List<ShellProfile> _cachedShells = new();
    private readonly object _lock = new();
    private bool _isPreloaded;

    public bool IsPreloaded
    {
        get { lock (_lock) return _isPreloaded; }
    }

    /// <summary>
    /// Asynchronously preloads and caches all available system shells during the splash screen sequence.
    /// </summary>
    public async Task PreloadShellsAsync(Action<string>? onProgress = null)
    {
        await Task.Run(() =>
        {
            var shells = DetectSystemShells();
            lock (_lock)
            {
                _cachedShells.Clear();
                _cachedShells.AddRange(shells);
                _isPreloaded = true;
            }
            onProgress?.Invoke($"Found {shells.Count} shells: {string.Join(", ", shells.Select(s => s.DisplayName))}");
        }).ConfigureAwait(false);
    }

    /// <summary>
    /// Returns the detected system shells. If already preloaded during the splash screen, returns instantly from cache.
    /// </summary>
    public List<ShellProfile> GetAvailableShells(bool forceReload = false)
    {
        lock (_lock)
        {
            if (!_isPreloaded || forceReload || _cachedShells.Count == 0)
            {
                _cachedShells.Clear();
                _cachedShells.AddRange(DetectSystemShells());
                _isPreloaded = true;
            }

            return new List<ShellProfile>(_cachedShells);
        }
    }

    private List<ShellProfile> DetectSystemShells()
    {
        var shells = new List<ShellProfile>();
        var seenIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        var seenPaths = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        void AddShell(string id, string name, string path, string args = "", string iconKey = "Terminal")
        {
            if (string.IsNullOrWhiteSpace(path)) return;

            try
            {
                if (!File.Exists(path)) return;
                var fullPath = Path.GetFullPath(path);

                if (!seenPaths.Add(fullPath)) return;
                if (!seenIds.Add(id)) return;

                shells.Add(new ShellProfile
                {
                    Id = id,
                    DisplayName = name,
                    ExecutablePath = fullPath,
                    Arguments = args,
                    IconKey = iconKey
                });
            }
            catch
            {
                // Ignore path access / permission errors
            }
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            // 1. PowerShell 7+ Core (pwsh.exe)
            var pwshPath = FindInPath("pwsh.exe");
            if (string.IsNullOrEmpty(pwshPath))
            {
                var candidatePwsh = new[]
                {
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7\pwsh.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"PowerShell\7-preview\pwsh.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Microsoft\PowerShell\pwsh.exe"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), @"PowerShell\7\pwsh.exe")
                };
                pwshPath = candidatePwsh.FirstOrDefault(File.Exists);
            }
            if (!string.IsNullOrEmpty(pwshPath))
            {
                AddShell("pwsh", "PowerShell 7+", pwshPath, iconKey: "PowerShell");
            }

            // 2. Windows PowerShell (powershell.exe)
            var winPowerShell = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.System),
                @"WindowsPowerShell\v1.0\powershell.exe");
            if (!File.Exists(winPowerShell))
            {
                winPowerShell = FindInPath("powershell.exe");
            }
            if (!string.IsNullOrEmpty(winPowerShell))
            {
                AddShell("powershell", "Windows PowerShell", winPowerShell, iconKey: "PowerShell");
            }

            // 3. Command Prompt (cmd.exe)
            var cmdPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
            if (!File.Exists(cmdPath))
            {
                cmdPath = FindInPath("cmd.exe");
            }
            if (!string.IsNullOrEmpty(cmdPath))
            {
                AddShell("cmd", "Command Prompt", cmdPath, iconKey: "Cmd");
            }

            // 4. Git Bash (bash.exe)
            string? gitBashPath = null;
            var gitBashCandidates = new[]
            {
                @"C:\Program Files\Git\bin\bash.exe",
                @"C:\Program Files\Git\usr\bin\bash.exe",
                @"C:\Program Files (x86)\Git\bin\bash.exe",
                @"C:\Program Files (x86)\Git\usr\bin\bash.exe",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\bin\bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), @"Programs\Git\usr\bin\bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\bin\bash.exe"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), @"Git\usr\bin\bash.exe")
            };
            gitBashPath = gitBashCandidates.FirstOrDefault(File.Exists);

            // Also check relative to git.exe in PATH (for portable or custom Git installations)
            if (string.IsNullOrEmpty(gitBashPath))
            {
                var gitPath = FindInPath("git.exe");
                if (!string.IsNullOrEmpty(gitPath))
                {
                    var gitDir = Path.GetDirectoryName(gitPath);
                    if (!string.IsNullOrEmpty(gitDir))
                    {
                        var relBash1 = Path.GetFullPath(Path.Combine(gitDir, @"..\bin\bash.exe"));
                        var relBash2 = Path.GetFullPath(Path.Combine(gitDir, @"..\usr\bin\bash.exe"));
                        if (File.Exists(relBash1)) gitBashPath = relBash1;
                        else if (File.Exists(relBash2)) gitBashPath = relBash2;
                    }
                }
            }

            if (!string.IsNullOrEmpty(gitBashPath))
            {
                AddShell("gitbash", "Git Bash", gitBashPath, args: "--login -i", iconKey: "Git");
            }

            // 5. WSL (wsl.exe)
            var wslPath = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "wsl.exe");
            if (!File.Exists(wslPath))
            {
                wslPath = FindInPath("wsl.exe");
            }
            if (!string.IsNullOrEmpty(wslPath))
            {
                AddShell("wsl", "WSL (Linux)", wslPath, iconKey: "Linux");
            }

            // 6. MSYS2 / MinGW Bash
            var msysCandidates = new[]
            {
                @"C:\msys64\usr\bin\bash.exe",
                @"C:\msys2\usr\bin\bash.exe"
            };
            var msysPath = msysCandidates.FirstOrDefault(File.Exists);
            if (!string.IsNullOrEmpty(msysPath))
            {
                AddShell("msys2", "MSYS2 Bash", msysPath, args: "--login -i", iconKey: "Terminal");
            }

            // 7. Nushell
            var nuPath = FindInPath("nu.exe");
            if (string.IsNullOrEmpty(nuPath))
            {
                var userCargoNu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), @".cargo\bin\nu.exe");
                if (File.Exists(userCargoNu)) nuPath = userCargoNu;
            }
            if (!string.IsNullOrEmpty(nuPath))
            {
                AddShell("nu", "Nushell", nuPath, iconKey: "Terminal");
            }
        }
        else
        {
            // Unix (Linux & macOS)

            // 1. Standard Unix /etc/shells file (cross-platform Linux/macOS standard)
            if (File.Exists("/etc/shells"))
            {
                try
                {
                    var lines = File.ReadAllLines("/etc/shells");
                    foreach (var rawLine in lines)
                    {
                        var line = rawLine.Trim();
                        if (string.IsNullOrEmpty(line) || line.StartsWith("#")) continue;

                        if (File.Exists(line))
                        {
                            var exeName = Path.GetFileName(line).ToLowerInvariant();
                            var (id, name, iconKey) = ResolveUnixShellInfo(exeName);
                            AddShell(id, name, line, iconKey: iconKey);
                        }
                    }
                }
                catch
                {
                    // Ignore permissions / access issues
                }
            }

            // 2. macOS Homebrew & Apple Silicon standard locations
            if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                var macCandidates = new (string id, string name, string path, string iconKey)[]
                {
                    ("zsh", "Zsh", "/bin/zsh", "Terminal"),
                    ("zsh", "Zsh", "/opt/homebrew/bin/zsh", "Terminal"),
                    ("zsh", "Zsh", "/usr/local/bin/zsh", "Terminal"),
                    ("bash", "Bash", "/bin/bash", "Terminal"),
                    ("bash", "Bash", "/opt/homebrew/bin/bash", "Terminal"),
                    ("bash", "Bash", "/usr/local/bin/bash", "Terminal"),
                    ("fish", "Fish", "/opt/homebrew/bin/fish", "Terminal"),
                    ("fish", "Fish", "/usr/local/bin/fish", "Terminal"),
                    ("pwsh", "PowerShell", "/opt/homebrew/bin/pwsh", "PowerShell"),
                    ("pwsh", "PowerShell", "/usr/local/bin/pwsh", "PowerShell"),
                    ("nu", "Nushell", "/opt/homebrew/bin/nu", "Terminal"),
                    ("nu", "Nushell", "/usr/local/bin/nu", "Terminal"),
                    ("sh", "Sh", "/bin/sh", "Terminal")
                };

                foreach (var (id, name, path, iconKey) in macCandidates)
                {
                    AddShell(id, name, path, iconKey: iconKey);
                }
            }
            else
            {
                // Linux standard candidate paths
                var linuxCandidates = new (string id, string name, string path, string iconKey)[]
                {
                    ("bash", "Bash", "/bin/bash", "Terminal"),
                    ("bash", "Bash", "/usr/bin/bash", "Terminal"),
                    ("zsh", "Zsh", "/bin/zsh", "Terminal"),
                    ("zsh", "Zsh", "/usr/bin/zsh", "Terminal"),
                    ("fish", "Fish", "/usr/bin/fish", "Terminal"),
                    ("fish", "Fish", "/bin/fish", "Terminal"),
                    ("pwsh", "PowerShell", "/usr/bin/pwsh", "PowerShell"),
                    ("pwsh", "PowerShell", "/snap/bin/pwsh", "PowerShell"),
                    ("dash", "Dash", "/bin/dash", "Terminal"),
                    ("dash", "Dash", "/usr/bin/dash", "Terminal"),
                    ("sh", "Sh", "/bin/sh", "Terminal"),
                    ("nu", "Nushell", "/usr/bin/nu", "Terminal")
                };

                foreach (var (id, name, path, iconKey) in linuxCandidates)
                {
                    AddShell(id, name, path, iconKey: iconKey);
                }
            }

            // 3. Fallback search via PATH for common shells
            var commonBinaries = new[] { "zsh", "bash", "fish", "pwsh", "nu" };
            foreach (var bin in commonBinaries)
            {
                var inPath = FindInPath(bin);
                if (!string.IsNullOrEmpty(inPath))
                {
                    var (id, name, iconKey) = ResolveUnixShellInfo(bin);
                    AddShell(id, name, inPath, iconKey: iconKey);
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

    private static (string id, string name, string iconKey) ResolveUnixShellInfo(string exeName)
    {
        return exeName.ToLowerInvariant() switch
        {
            "zsh" => ("zsh", "Zsh", "Terminal"),
            "bash" => ("bash", "Bash", "Terminal"),
            "fish" => ("fish", "Fish", "Terminal"),
            "pwsh" => ("pwsh", "PowerShell", "PowerShell"),
            "dash" => ("dash", "Dash", "Terminal"),
            "sh" => ("sh", "Sh", "Terminal"),
            "nu" => ("nu", "Nushell", "Terminal"),
            "ksh" => ("ksh", "Ksh", "Terminal"),
            "tcsh" => ("tcsh", "Tcsh", "Terminal"),
            "csh" => ("csh", "Csh", "Terminal"),
            _ => (exeName, char.ToUpperInvariant(exeName[0]) + (exeName.Length > 1 ? exeName[1..] : ""), "Terminal")
        };
    }

    public ShellProfile GetDefaultShell(string? preferredId = null)
    {
        var shells = GetAvailableShells();
        if (!string.IsNullOrEmpty(preferredId) && !preferredId.Equals("default", StringComparison.OrdinalIgnoreCase))
        {
            var matched = shells.Find(s => string.Equals(s.Id, preferredId, StringComparison.OrdinalIgnoreCase));
            if (matched != null) return matched;
        }

        // Default preference order based on OS platform:
        // Windows: pwsh > powershell > cmd > gitbash > wsl
        // macOS: zsh > bash > fish > pwsh
        // Linux: bash > zsh > fish > pwsh
        string[] preferredOrder;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            preferredOrder = new[] { "pwsh", "powershell", "cmd", "gitbash" };
        }
        else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
        {
            preferredOrder = new[] { "zsh", "bash", "fish", "pwsh" };
        }
        else
        {
            preferredOrder = new[] { "bash", "zsh", "fish", "pwsh" };
        }

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
