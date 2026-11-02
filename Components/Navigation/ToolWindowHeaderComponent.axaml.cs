using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;

namespace Fdb.Components;

public partial class ToolWindowHeaderComponent : UserControl
{
    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ToolWindowHeaderComponent, string>(nameof(Title), "Tool Window");

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ToolWindowHeaderComponent, ICommand?>(nameof(CloseCommand));

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public ToolWindowHeaderComponent()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TitleProperty && TitleText != null)
        {
            TitleText.Text = change.GetNewValue<string>();
        }
    }
}
