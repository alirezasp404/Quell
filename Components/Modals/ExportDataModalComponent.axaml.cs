using Avalonia.Controls;
using Avalonia.Input;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class ExportDataModalComponent : UserControl
{
    public ExportDataModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ExportDataModalViewModel vm)
        {
            vm.CloseCommand.Execute(null);
        }
    }
}
