using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class EditCellModalComponent : UserControl
{
    public EditCellModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is EditCellModalViewModel vm)
        {
            vm.Cancel();
        }
    }
}
