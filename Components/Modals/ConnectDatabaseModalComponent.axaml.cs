using Avalonia.Controls;
using Avalonia.Input;
using Fdb.ViewModels;

namespace Fdb.Components;

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
