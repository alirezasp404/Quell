using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class ImportDataModalComponent : UserControl
{
    public ImportDataModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is ImportDataModalViewModel vm && !vm.IsImporting)
        {
            vm.CloseCommand.Execute(null);
        }
    }
}
