using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Models;

namespace Quell.ViewModels;

public partial class TerminalSessionViewModel : ViewModelBase
{
    public string Id { get; } = Guid.NewGuid().ToString("N");

    [ObservableProperty]
    private string _title = "Terminal";

    [ObservableProperty]
    private string _iconKey = "Terminal";

    [ObservableProperty]
    private string _executablePath = string.Empty;

    [ObservableProperty]
    private string _arguments = string.Empty;

    [ObservableProperty]
    private string _workingDirectory = string.Empty;

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private bool _isRunning;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusText))]
    private int? _exitCode;

    [ObservableProperty]
    private bool _hasCliError;

    [ObservableProperty]
    private string _cliErrorTitle = string.Empty;

    [ObservableProperty]
    private string _cliErrorMessage = string.Empty;

    [ObservableProperty]
    private string _cliDownloadUrl = string.Empty;

    [ObservableProperty]
    private bool _isCopied;

    public string StatusText => IsRunning ? "Running" : (ExitCode.HasValue ? $"Exited ({ExitCode.Value})" : "Ready");
    public string StatusColor => IsRunning ? "#10B981" : (ExitCode.HasValue && ExitCode.Value != 0 ? "#EF4444" : "#94A3B8");

    public string ShellBadge
    {
        get
        {
            if (Title.Contains("PowerShell", StringComparison.OrdinalIgnoreCase)) return "PowerShell";
            if (Title.Contains("Command Prompt", StringComparison.OrdinalIgnoreCase) || Title.Contains("cmd", StringComparison.OrdinalIgnoreCase)) return "CMD";
            if (Title.Contains("Git Bash", StringComparison.OrdinalIgnoreCase) || Title.Contains("bash", StringComparison.OrdinalIgnoreCase)) return "Bash";
            if (Title.Contains("WSL", StringComparison.OrdinalIgnoreCase)) return "WSL";
            if (IconKey == "Database") return "DB CLI";
            return "Shell";
        }
    }

    public Dictionary<string, string> EnvironmentVariables { get; } = new();

    public event Action<string>? SendTextRequested;
    public event Action? ClearScreenRequested;
    public event Action? KillRequested;
    public event Action? FocusRequested;
    public event Action<TerminalSessionViewModel>? CloseRequested;
    public event Action? RestartRequested;
    public event Func<string, Task>? CopyToClipboardRequested;

    public void SendText(string text)
    {
        SendTextRequested?.Invoke(text);
    }

    public void ClearScreen()
    {
        ClearScreenRequested?.Invoke();
    }

    public void Kill()
    {
        KillRequested?.Invoke();
    }

    public void Focus()
    {
        FocusRequested?.Invoke();
    }

    [RelayCommand]
    public async Task CopyWorkingDirectoryAsync()
    {
        if (string.IsNullOrWhiteSpace(WorkingDirectory)) return;

        try
        {
            if (CopyToClipboardRequested != null)
            {
                await CopyToClipboardRequested.Invoke(WorkingDirectory);
            }
            else
            {
                await Quell.Services.ClipboardService.CopyTextAsync(WorkingDirectory);
            }

            IsCopied = true;
            await Task.Delay(1500);
            IsCopied = false;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalSessionViewModel] Copy to clipboard failed: {ex.Message}");
        }
    }

    [RelayCommand]
    public void CloseSession()
    {
        CloseRequested?.Invoke(this);
    }

    [RelayCommand]
    public void RestartSession()
    {
        RestartRequested?.Invoke();
    }

    [RelayCommand]
    public void OpenDownloadUrl()
    {
        if (string.IsNullOrWhiteSpace(CliDownloadUrl)) return;

        try
        {
            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = CliDownloadUrl,
                    UseShellExecute = true
                });
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            {
                Process.Start("xdg-open", CliDownloadUrl);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            {
                Process.Start("open", CliDownloadUrl);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalSessionViewModel] Failed to open download url: {ex.Message}");
        }
    }
}
