using Avalonia.Controls;
using Quell.Services;

namespace Quell.Views;

public partial class MainView : UserControl
{
    public MainView()
    {
        InitializeComponent();
        if (HardwareGraphicsService.Instance.IsGpuMotionActive)
        {
            Classes.Add("gpu-mode");
        }
    }
}
