using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class ConnectDatabaseModalComponent : UserControl
{
    public ConnectDatabaseModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ConnectDatabaseModalViewModel vm)
        {
            vm.CloseCommand.Execute(null);
        }
    }
}
