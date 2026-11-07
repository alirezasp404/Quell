using System;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Threading;
using Quell.Services;
using Quell.ViewModels;

namespace Quell.Views;

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

    public void UpdateProgress(double progress, string status, string subStatus = "")
    {
        var vm = ViewModel;
        vm.ProgressValue = progress;
        vm.StatusText = status;
        if (!string.IsNullOrWhiteSpace(subStatus))
        {
            vm.SubStatusText = subStatus;
        }

        if (!Dispatcher.UIThread.CheckAccess())
        {
            Dispatcher.UIThread.Post(() =>
            {
                vm.ProgressValue = progress;
                vm.StatusText = status;
                if (!string.IsNullOrWhiteSpace(subStatus))
                {
                    vm.SubStatusText = subStatus;
                }
            });
        }
    }

    public async Task RunStartupSequenceAsync(bool completeTo100 = true)
    {
        _isDatabaseReady = false;

        UpdateProgress(5, "Connecting to SQLite Storage Engine...", "Verifying database file and schemas...");

        // Preload all startup data, run SQLite schema migrations, cache settings, theme, keywords and connections
        await DatabaseStorageService.Instance.PreloadStartupDataAsync(async (progress, status, subStatus) =>
        {
            UpdateProgress(progress, status, subStatus);
            // Brief micro-delay so the UI thread renders this real step's progress and the user can see it
            await Task.Delay(40).ConfigureAwait(false);
        }).ConfigureAwait(false);

        _isDatabaseReady = true;

        if (completeTo100)
        {
            UpdateProgress(100, "Studio Environment Ready — SQLite Engine Initialized.", "All configurations loaded.");
            await Task.Delay(180).ConfigureAwait(false);
        }
    }
}
