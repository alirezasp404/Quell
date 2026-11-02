using Avalonia.Controls;
using Avalonia.Input;
using Fdb.ViewModels;

namespace Fdb.Components;

public partial class ThemeSelectionCardsComponent : UserControl
{
    public ThemeSelectionCardsComponent()
    {
        InitializeComponent();
    }

    private void OnLightCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectTheme("Light");
        }
    }

    private void OnDarkCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectTheme("Dark");
        }
    }

    private void OnSystemCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (DataContext is SettingsModalViewModel vm)
        {
            vm.SelectTheme("System");
        }
    }
}
