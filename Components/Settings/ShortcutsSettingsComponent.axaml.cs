using Avalonia.Controls;
using Avalonia.Input;
using Fdb.Services;

namespace Fdb.Components;

public partial class ShortcutsSettingsComponent : UserControl
{
    public ShortcutsSettingsComponent()
    {
        InitializeComponent();
    }

    private void OnShortcutTextBoxKeyDown(object? sender, KeyEventArgs e)
    {
        string gesture = ShortcutService.KeyEventArgsToGesture(e);
        if (!string.IsNullOrWhiteSpace(gesture) && sender is TextBox tb)
        {
            tb.Text = gesture;
            e.Handled = true;
        }
    }
}
