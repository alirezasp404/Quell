using CommunityToolkit.Mvvm.ComponentModel;
using System.Collections.ObjectModel;
using Fdb.Services.DatabaseProviders;

namespace Fdb.ViewModels;

public partial class NavbarViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _title = "Fdb";

    [ObservableProperty]
    private string _connectionStatusText = "master [localhost:1433]";

    [ObservableProperty]
    private bool _isConnected = true;

    public string ConnectionDotColor => IsConnected ? "#10B981" : "#94A3B8";

    public ObservableCollection<string> MenuItems { get; } = new()
    {
        "File",
        "Edit",
        "View",
        "Query",
        "Help",
        "windows"
    };

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
