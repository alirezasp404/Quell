using CommunityToolkit.Mvvm.ComponentModel;

namespace Quell.ViewModels;

public partial class SplashScreenViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _statusText = "Initializing Quell...";

    [ObservableProperty]
    private double _progressValue = 0;

    [ObservableProperty]
    private string _subStatusText = "Connecting to local storage engine...";

    [ObservableProperty]
    private string _version = "v1.0.0 Developer Preview";
}
