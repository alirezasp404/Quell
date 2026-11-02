using Avalonia.Controls;
using Avalonia.Input;
using Fdb.Services;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class SidebarComponent : UserControl
{
    public SidebarComponent()
    {
        InitializeComponent();
    }

    private void OnSavedConnectionDoubleTapped(object? sender, TappedEventArgs e)
    {
        if ((sender as Control)?.DataContext is ConnectionRecord record)
        {
            if (DataContext is SidebarViewModel vm)
            {
                _ = vm.ActivateConnection(record);
            }
        }
    }
}
