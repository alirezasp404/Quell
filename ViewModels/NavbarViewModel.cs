using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using Quell.Services;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

public partial class NavbarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Quell";

    [ObservableProperty]
    private string _connectionStatusText = "master [localhost:1433]";

    [ObservableProperty]
    private bool _isConnected = true;

    [ObservableProperty]
    private bool _isNavMenuOpen;

    public string ConnectionDotColor => IsConnected ? "#10B981" : "#94A3B8";

    public string GpuTooltipText => HardwareGraphicsService.Instance.IsGpuMotionActive
        ? $"Toggle Application Menu (Alt+M) • GPU Hardware Acceleration Active ({HardwareGraphicsService.Instance.GpuAdapterName})"
        : "Toggle Application Menu (Alt+M) • CPU Lightweight Motion";

    public ObservableCollection<string> MenuItems { get; } = new()
    {
        "File",
        "Edit",
        "View",
        "Query",
        "Help",
        "windows"
    };

    [RelayCommand]
    public void ToggleNavMenu()
    {
        IsNavMenuOpen = !IsNavMenuOpen;
    }

    public void UpdateConnectionStatus()
    {
        var session = DatabaseSessionService.Instance;
        if (session.IsConnected)
        {
            ConnectionStatusText = session.ConnectionDisplayText;
            IsConnected = true;
        }
        else
        {
            ConnectionStatusText = "Disconnected";
            IsConnected = false;
        }
        OnPropertyChanged(nameof(ConnectionDotColor));
    }
}
