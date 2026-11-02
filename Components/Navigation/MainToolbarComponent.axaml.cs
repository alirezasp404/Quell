using Avalonia.Controls;

namespace Fdb.Components;

public partial class MainToolbarComponent : UserControl
{
    public Button? ConnectBtn => this.FindControl<Button>("ConnectButton");

    public MainToolbarComponent()
    {
        InitializeComponent();
    }
}
