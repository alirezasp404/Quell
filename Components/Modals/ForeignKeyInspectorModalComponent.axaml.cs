using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class ForeignKeyInspectorModalComponent : UserControl
{
    public ForeignKeyInspectorModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ForeignKeyInspectorModalViewModel vm)
        {
            vm.Close();
        }
    }
}
