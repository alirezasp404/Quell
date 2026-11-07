using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class UpdateModalComponent : UserControl
{
    public UpdateModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is UpdateModalViewModel vm)
        {
            vm.DismissCommand.Execute(null);
        }
    }
}
