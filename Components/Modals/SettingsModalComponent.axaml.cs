using Avalonia.Controls;
using Avalonia.Input;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class SettingsModalComponent : UserControl
{
    public SettingsModalComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.Close();
        }
    }
}
