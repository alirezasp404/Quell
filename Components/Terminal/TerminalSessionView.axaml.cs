using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Fdb.Services.Terminal;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class TerminalSessionView : UserControl
{
    private TerminalSessionViewModel? _session;
    private bool _processLaunched;
    private bool _disposed;

    public TerminalSessionView()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
        Unloaded += OnUnloaded;

        OpenShellFallbackButton.Click += OnOpenShellFallback;
        OpenSettingsButton.Click += OnOpenSettings;
        CopyPathButton.Click += OnCopyPathClicked;

        TerminalSettingsService.Instance.SettingsChanged += OnSettingsChanged;
    }

    private async void OnCopyPathClicked(object? sender, RoutedEventArgs e)
    {
        if (_session != null && !string.IsNullOrWhiteSpace(_session.WorkingDirectory))
        {
            await Fdb.Services.ClipboardService.CopyTextAsync(_session.WorkingDirectory, this);
            _session.IsCopied = true;
            await Task.Delay(1500);
            _session.IsCopied = false;
        }
    }

    public void FocusTerminal()
    {
        Dispatcher.UIThread.Post(() =>
        {
            TermControl?.Focus();
        }, DispatcherPriority.Input);
    }

    public void ClearScreen()
    {
        try
        {
            TermControl?.ClearScreen();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalSessionView] ClearScreen error: {ex.Message}");
        }
    }

    public void DisposeSession()
    {
        if (_disposed) return;
        _disposed = true;

        DetachSessionEvents();
        TerminalSettingsService.Instance.SettingsChanged -= OnSettingsChanged;

        try
        {
            TermControl?.Kill();
        }
        catch { }

        try
        {
            TermControl?.Dispose();
        }
        catch { }

        if (_session != null)
        {
            _session.IsRunning = false;
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        DetachSessionEvents();

        _session = DataContext as TerminalSessionViewModel;

        if (_session != null)
        {
            AttachSessionEvents();
            ApplyCurrentSettings();

            if (IsLoaded && !_session.HasCliError && !_processLaunched)
            {
                LaunchProcess();
            }
        }
    }

    private void AttachSessionEvents()
    {
        if (_session == null) return;
        _session.SendTextRequested += OnSendTextRequested;
        _session.ClearScreenRequested += OnClearScreenRequested;
        _session.KillRequested += OnKillRequested;
        _session.FocusRequested += OnFocusRequested;
        _session.RestartRequested += OnRestartRequested;
        _session.CopyToClipboardRequested += OnCopyToClipboardRequested;
    }

    private void DetachSessionEvents()
    {
        if (_session == null) return;
        _session.SendTextRequested -= OnSendTextRequested;
        _session.ClearScreenRequested -= OnClearScreenRequested;
        _session.KillRequested -= OnKillRequested;
        _session.FocusRequested -= OnFocusRequested;
        _session.RestartRequested -= OnRestartRequested;
        _session.CopyToClipboardRequested -= OnCopyToClipboardRequested;
    }

    private async Task OnCopyToClipboardRequested(string text)
    {
        try
        {
            await Fdb.Services.ClipboardService.CopyTextAsync(text, this);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalSessionView] Copy clipboard error: {ex.Message}");
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (_session != null && !_session.HasCliError && !_processLaunched)
        {
            LaunchProcess();
        }
    }

    private void OnUnloaded(object? sender, RoutedEventArgs e)
    {
        // Don't kill process on temporary tab switch or container re-order
    }

    private async void LaunchProcess()
    {
        if (_session == null || _session.HasCliError || string.IsNullOrEmpty(_session.ExecutablePath))
        {
            return;
        }

        if (_processLaunched)
        {
            return;
        }

        _processLaunched = true;

        try
        {
            TermControl.ApplyTemplate();

            TermControl.StartingDirectory = !string.IsNullOrEmpty(_session.WorkingDirectory)
                ? _session.WorkingDirectory
                : Environment.CurrentDirectory;

            TermControl.Process = _session.ExecutablePath;

            if (!string.IsNullOrWhiteSpace(_session.Arguments))
            {
                TermControl.ProcessArgs = _session.Arguments.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            }
            else
            {
                TermControl.ProcessArgs = new List<string>();
            }

            if (TermControl.EnvironmentVariables != null)
            {
                foreach (var kvp in _session.EnvironmentVariables)
                {
                    TermControl.EnvironmentVariables[kvp.Key] = kvp.Value;
                }
            }

            _session.IsRunning = true;

            TermControl.ProcessExited += (s, args) =>
            {
                Dispatcher.UIThread.Post(() =>
                {
                    if (_session != null)
                    {
                        _session.IsRunning = false;
                        _session.ExitCode = args.ExitCode;
                    }
                });
            };

            await TermControl.LaunchProcess();

            FocusTerminal();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalSessionView] LaunchProcess exception: {ex.Message}");
            if (_session != null)
            {
                _session.HasCliError = true;
                _session.CliErrorTitle = "Process Launch Failed";
                _session.CliErrorMessage = ex.Message;
            }
        }
    }

    private void OnSendTextRequested(string text)
    {
        try
        {
            if (TermControl != null && _session != null && _session.IsRunning)
            {
                _ = TermControl.SendInputAsync(text, default);
                FocusTerminal();
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[TerminalSessionView] SendTextRequested error: {ex.Message}");
        }
    }

    private void OnClearScreenRequested()
    {
        ClearScreen();
    }

    private void OnKillRequested()
    {
        try
        {
            TermControl?.Kill();
        }
        catch { }

        if (_session != null)
        {
            _session.IsRunning = false;
        }
    }

    private void OnFocusRequested()
    {
        FocusTerminal();
    }

    private void OnRestartRequested()
    {
        try
        {
            TermControl?.Kill();
        }
        catch { }

        _processLaunched = false;
        if (_session != null && !_session.HasCliError)
        {
            LaunchProcess();
        }
    }

    private void ApplyCurrentSettings()
    {
        var settings = TerminalSettingsService.Instance.CurrentSettings;
        if (TermControl != null && settings != null)
        {
            if (!string.IsNullOrWhiteSpace(settings.FontFamily))
            {
                try
                {
                    TermControl.FontFamily = new FontFamily(settings.FontFamily);
                }
                catch { }
            }

            if (settings.FontSize >= 8 && settings.FontSize <= 32)
            {
                TermControl.FontSize = settings.FontSize;
            }

            TermControl.CursorBlink = settings.CursorBlink;
        }
    }

    private void OnSettingsChanged(object? sender, Models.TerminalSettingsModel settings)
    {
        Dispatcher.UIThread.Post(ApplyCurrentSettings);
    }

    private void OnOpenShellFallback(object? sender, RoutedEventArgs e)
    {
        var mainVm = (VisualRoot as Window)?.DataContext as MainViewModel;
        mainVm?.Terminal.NewSession();
    }

    private void OnOpenSettings(object? sender, RoutedEventArgs e)
    {
        var mainVm = (VisualRoot as Window)?.DataContext as MainViewModel;
        mainVm?.SettingsModal.OpenCommand.Execute(null);
    }
}
