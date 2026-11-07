using Avalonia.Controls;
using Avalonia.Input;
using Quell.ViewModels;

namespace Quell.Components;

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
