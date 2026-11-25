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
    private string _latestVersion = "v1.1.0";

    [ObservableProperty]
    private string _releaseTitle = "Quell 1.1.0 is available";

    [ObservableProperty]
    private string _releaseNotes = "• Faster results grid for large tables\n• Schema diagram with foreign-key navigation\n• Export to Excel and Markdown\n• Fixes for connection time-outs";

    [ObservableProperty]
    private string _releaseUrl = AppUpdateService.DefaultReleasesPageUrl;

    [ObservableProperty]
    private string _publishedAt = "Nov 22, 2026";

    [ObservableProperty]
    private string _downloadSize = "84 MB";

    [ObservableProperty]
    private bool _isUpdateAvailable;

    [ObservableProperty]
    private bool _isChecking;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    // 3-step modern update flow: 1 = Available, 2 = Downloading, 3 = Ready to restart
    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStep1Available))]
    [NotifyPropertyChangedFor(nameof(IsStep2Downloading))]
    [NotifyPropertyChangedFor(nameof(IsStep3Ready))]
    private int _updateStep = 1;

    public bool IsStep1Available => UpdateStep == 1;
    public bool IsStep2Downloading => UpdateStep == 2;
    public bool IsStep3Ready => UpdateStep == 3;

    [ObservableProperty]
    private int _downloadProgress = 42;

    [ObservableProperty]
    private string _downloadSpeed = "6.2 MB/s";

    [ObservableProperty]
    private string _downloadProgressText = "35 of 84 MB";

    [ObservableProperty]
    private string _timeRemaining = "about 8 s left";

    [ObservableProperty]
    private bool _isDownloading;

    [ObservableProperty]
    private bool _isReadyToRestart;

    [ObservableProperty]
    private bool _isQueryRunning;

    public UpdateModalViewModel()
    {
        AppUpdateService.Instance.UpdateAvailable += OnUpdateAvailable;
    }

    private void OnUpdateAvailable(object? sender, UpdateReleaseModel release)
    {
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            ApplyRelease(release);
            UpdateStep = 1;
            Open();
        });
    }

    public void ApplyRelease(UpdateReleaseModel release)
    {
        LatestVersion = release.TagName;
        ReleaseTitle = $"Quell {release.TagName.TrimStart('v')} is available";
        ReleaseNotes = string.IsNullOrWhiteSpace(release.Body) 
            ? "• Faster results grid for large tables\n• Schema diagram with foreign-key navigation\n• Export to Excel and Markdown\n• Fixes for connection time-outs" 
            : release.Body;
        ReleaseUrl = release.HtmlUrl;
        PublishedAt = string.IsNullOrWhiteSpace(release.PublishedAt) ? "Recently" : release.PublishedAt;
        IsUpdateAvailable = release.IsNewer;
        UpdateStep = 1;
    }

    [RelayCommand]
    public void Open()
    {
        IsOpen = true;
        IsCornerBadgeVisible = false;
    }

    [RelayCommand]
    public void Dismiss()
    {
        IsOpen = false;
        IsCornerBadgeVisible = false;
    }

    [RelayCommand]
    public async Task StartDownloadAsync()
    {
        UpdateStep = 2;
        IsDownloading = true;

        try
        {
            for (int p = 15; p <= 100; p += 15)
            {
                if (!IsDownloading) break;
                DownloadProgress = Math.Min(100, p);
                DownloadProgressText = $"{DownloadProgress * 84 / 100} of 84 MB";
                await Task.Delay(250).ConfigureAwait(true);
            }

            if (IsDownloading)
            {
                IsDownloading = false;
                IsReadyToRestart = true;
                UpdateStep = 3;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[UpdateModalViewModel] Download error: {ex.Message}");
            IsDownloading = false;
            UpdateStep = 1;
        }
    }

    [RelayCommand]
    public void CancelDownload()
    {
        IsDownloading = false;
        UpdateStep = 1;
        Dismiss();
    }

    [RelayCommand]
    public void RunInBackground()
    {
        IsOpen = false;
    }

    [RelayCommand]
    public void RestartNow()
    {
        OpenReleaseUrl();
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
