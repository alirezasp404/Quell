using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Fdb.Services;
using Fdb.ViewModels;

namespace Fdb.Views;

public partial class SplashScreenWindow : Window
{
    private bool _isDismissed;
    private bool _isDatabaseReady;
    private readonly TaskCompletionSource<bool> _dismissTcs = new();

    public SplashScreenViewModel ViewModel { get; } = new();

    public SplashScreenWindow()
    {
        InitializeComponent();
        DataContext = ViewModel;
        PointerPressed += OnWindowPointerPressed;
        KeyDown += OnWindowKeyDown;
    }

    private void OnWindowPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_isDatabaseReady)
        {
            Dismiss();
        }
    }

    private void OnWindowKeyDown(object? sender, KeyEventArgs e)
    {
        if (e.Key is Key.Escape or Key.Space or Key.Enter && _isDatabaseReady)
        {
            Dismiss();
        }
    }

    public void Dismiss()
    {
        if (_isDismissed) return;
        _isDismissed = true;
        _dismissTcs.TrySetResult(true);
    }

    public async Task RunStartupSequenceAsync(int durationMs = 0)
    {
        var vm = ViewModel;
        _isDatabaseReady = false;

        void UpdateProgress(double progress, string status)
        {
            vm.ProgressValue = progress;
            vm.StatusText = status;
            if (!Dispatcher.UIThread.CheckAccess())
            {
                Dispatcher.UIThread.Post(() =>
                {
                    vm.ProgressValue = progress;
                    vm.StatusText = status;
                });
            }
        }

        UpdateProgress(5, "Connecting to SQLite Storage Engine...");

        // Execute real SQLite queries from Assets/init_database.sql
        await DatabaseStorageService.Instance.InitializeWithProgressAsync((progress, status) =>
        {
            UpdateProgress(progress, status);
        }).ConfigureAwait(false);

        // Load and apply saved user theme and keyword syntax color from SQLite during splash screen
        UpdateProgress(95, "Applying User Appearance & Theme Preferences...");
        string savedTheme = await DatabaseStorageService.Instance.GetThemeAsync().ConfigureAwait(false);
        ThemeService.Instance.ApplyTheme(savedTheme);
        string savedKeywordColor = await DatabaseStorageService.Instance.GetKeywordColorAsync().ConfigureAwait(false);
        ThemeService.Instance.SetKeywordColor(savedKeywordColor);
        var panelCustom = await DatabaseStorageService.Instance.GetPanelCustomizationAsync().ConfigureAwait(false);
        ThemeService.Instance.ApplyPanelCustomization(panelCustom);
        await ShortcutService.Instance.InitializeAsync().ConfigureAwait(false);

        _isDatabaseReady = true;

        UpdateProgress(100, "Studio Environment Ready — SQLite Engine Initialized.");

        // Short smooth transition grace period so 100% progress is cleanly visible
        await Task.Delay(180).ConfigureAwait(false);
    }
}
