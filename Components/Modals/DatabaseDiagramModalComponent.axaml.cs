using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class DatabaseDiagramModalComponent : UserControl
{
    public DatabaseDiagramModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is DatabaseDiagramModalViewModel vm)
        {
            vm.Close();
        }
    }
}
