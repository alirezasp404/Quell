using System;
using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

public partial class EditorTabStripComponent : UserControl
{
    public event EventHandler<EditorTabItem>? TabSelected;

    public EditorTabStripComponent()
    {
        InitializeComponent();
    }

    private void OnTabPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if ((sender as Border)?.DataContext is EditorTabItem clickedTab)
        {
            if (DataContext is EditorViewModel vm)
            {
                vm.SelectTab(clickedTab);
            }
            TabSelected?.Invoke(this, clickedTab);
        }
    }
}
