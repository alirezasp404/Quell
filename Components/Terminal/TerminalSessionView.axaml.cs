using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input.Platform;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Threading;
using Avalonia;
using Fdb.Services;
using Fdb.Services.Terminal;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class TerminalSessionView : UserControl
{
    private TerminalSessionViewModel? _session;
    private bool _processLaunched;
    private bool _disposed;
    private readonly Queue<string> _pendingInput = new();

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
        ThemeService.Instance.ThemeChanged += OnThemeChanged;
        ThemeService.Instance.PanelCustomizationChanged += OnPanelCustomizationChanged;
        ApplyThemeColors();
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
        ThemeService.Instance.ThemeChanged -= OnThemeChanged;
        ThemeService.Instance.PanelCustomizationChanged -= OnPanelCustomizationChanged;

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

            while (_pendingInput.Count > 0)
            {
                var pending = _pendingInput.Dequeue();
                _ = TermControl.SendInputAsync(pending, default);
            }

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
            else
            {
                _pendingInput.Enqueue(text);
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

    private void OnThemeChanged(object? sender, string theme)
    {
        Dispatcher.UIThread.Post(ApplyThemeColors);
    }

    private void OnPanelCustomizationChanged(object? sender, PanelCustomizationRecord custom)
    {
        Dispatcher.UIThread.Post(ApplyThemeColors);
    }

    private void ApplyThemeColors()
    {
        if (TermControl == null) return;

        bool isDark = ThemeService.Instance.IsDarkTheme();

        // Background
        IBrush? bgBrush = null;
        if (Application.Current != null && Application.Current.Resources.TryGetValue("AppTerminalBackgroundBrush", out var resBg) && resBg is IBrush b)
        {
            bgBrush = b;
        }
        else
        {
            bgBrush = isDark ? new SolidColorBrush(Color.Parse("#0F172A")) : new SolidColorBrush(Color.Parse("#FFFFFF"));
        }
        TermControl.Background = bgBrush;

        // Foreground
        IBrush? fgBrush = null;
        if (Application.Current != null && Application.Current.Resources.TryGetValue("AppTerminalForegroundBrush", out var resFg) && resFg is IBrush f)
        {
            fgBrush = f;
        }
        else
        {
            fgBrush = isDark ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
        TermControl.Foreground = fgBrush;

        // Accent & Cursor Color
        Color accentCol = isDark ? Color.Parse("#3B82F6") : Color.Parse("#2563EB");
        if (Application.Current != null && Application.Current.Resources.TryGetValue("AppAccentBrush", out var resAcc) && resAcc is ISolidColorBrush scb)
        {
            accentCol = scb.Color;
        }
        TermControl.CursorColor = accentCol;
        TermControl.SelectionBrush = new SolidColorBrush(Color.FromArgb(64, accentCol.R, accentCol.G, accentCol.B));
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
