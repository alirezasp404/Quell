using Avalonia.Controls;
using Avalonia.Input;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class SyntaxColorSelectorComponent : UserControl
{
    public SyntaxColorSelectorComponent()
    {
        InitializeComponent();
    }

    private void OnBlueColorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectKeywordColor("Blue");
        }
    }

    private void OnGreenColorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectKeywordColor("Green");
        }
    }

    private void OnPurpleColorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectKeywordColor("Purple");
        }
    }

    private void OnAmberColorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectKeywordColor("Amber");
        }
    }

    private void OnRoseColorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectKeywordColor("Rose");
        }
    }

    private void OnCyanColorPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectKeywordColor("Cyan");
        }
    }
}
