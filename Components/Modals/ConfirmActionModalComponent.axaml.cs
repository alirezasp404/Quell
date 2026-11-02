using Avalonia.Controls;
using Avalonia.Input;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class ConfirmActionModalComponent : UserControl
{
    public ConfirmActionModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ConfirmActionModalViewModel vm)
        {
            vm.CancelCommand.Execute(null);
        }
    }
}
