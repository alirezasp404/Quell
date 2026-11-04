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

        // Preload all startup data, run SQLite schema migrations, cache settings, theme, keywords and connections
        await DatabaseStorageService.Instance.PreloadStartupDataAsync((progress, status) =>
        {
            UpdateProgress(progress, status);
        }).ConfigureAwait(false);

        _isDatabaseReady = true;

        UpdateProgress(100, "Studio Environment Ready — SQLite Engine Initialized.");

        // Short smooth transition grace period so 100% progress is cleanly visible
        await Task.Delay(180).ConfigureAwait(false);
    }
}
