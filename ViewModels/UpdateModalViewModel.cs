using System;
using System.Diagnostics;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Models;
using Quell.Services;

namespace Quell.ViewModels;

public partial class UpdateModalViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isCornerBadgeVisible;

    [ObservableProperty]
    private string _currentVersion = AppUpdateService.CurrentVersionString;

    [ObservableProperty]
    private string _latestVersion = "v1.0.0";

    [ObservableProperty]
    private string _releaseTitle = "New Quell Studio Update Available";

    [ObservableProperty]
    private string _releaseNotes = "A new release is ready on GitHub with performance improvements and new features.";

    [ObservableProperty]
    private string _releaseUrl = AppUpdateService.DefaultReleasesPageUrl;

    [ObservableProperty]
    private string _publishedAt = string.Empty;

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public UpdateModalViewModel()
    {
        AppUpdateService.Instance.UpdateAvailable += OnUpdateAvailable;
    }

    private void OnUpdateAvailable(object? sender, UpdateReleaseModel release)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ApplyRelease(release);
            Open();
        });
    }

    public void ApplyRelease(UpdateReleaseModel release)
    {
        LatestVersion = release.TagName;
        ReleaseTitle = release.Name;
        ReleaseNotes = string.IsNullOrWhiteSpace(release.Body) 
            ? "• General performance enhancements and stability fixes.\n• View full release notes on GitHub." 
            : release.Body;
        ReleaseUrl = release.HtmlUrl;
        PublishedAt = string.IsNullOrWhiteSpace(release.PublishedAt) ? "" : $"Published: {release.PublishedAt}";
        IsUpdateAvailable = release.IsNewer;
    }

    [RelayCommand]
    public void Open()
    {
        IsOpen = true;
        IsCornerBadgeVisible = false; // Hide corner badge while the modal itself is open
    }

    [RelayCommand]
    public void Dismiss()
    {
        IsOpen = false;
        IsCornerBadgeVisible = false;
    }

    [RelayCommand]
    public void OpenReleaseUrl()
    {
        try
        {
            string url = string.IsNullOrWhiteSpace(ReleaseUrl) ? AppUpdateService.DefaultReleasesPageUrl : ReleaseUrl;
            Process.Start(new ProcessStartInfo
            {
                FileName = url,
                UseShellExecute = true
            });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateModalViewModel] Error opening release URL: {ex.Message}");
        }

        Dismiss();
    }

    [RelayCommand]
    public async Task CheckForUpdatesAsync()
    {
        IsChecking = true;
        StatusMessage = "Checking GitHub for updates...";
        try
        {
            var release = await AppUpdateService.Instance.CheckForUpdatesAsync(isManualCheck: true).ConfigureAwait(false);
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                if (release != null && release.IsNewer)
                {
                    ApplyRelease(release);
                    StatusMessage = $"New version {release.TagName} is available!";
                    Open();
                }
                else
                {
                    StatusMessage = "Quell Studio is up to date!";
                }
            });
        }
        finally
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => IsChecking = false);
        }
    }
}
