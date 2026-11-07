using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace Quell.Components;

public partial class IconButtonComponent : UserControl
{
    public static readonly StyledProperty<FASymbol> SymbolProperty =
        AvaloniaProperty.Register<IconButtonComponent, FASymbol>(nameof(Symbol), FASymbol.Refresh);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<IconButtonComponent, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<IconButtonComponent, object?>(nameof(CommandParameter));

    public FASymbol Symbol
    {
        get => GetValue(SymbolProperty);
        set => SetValue(SymbolProperty, value);
    }

    public ICommand? Command
    {
        get => GetValue(CommandProperty);
        set => SetValue(CommandProperty, value);
    }

    public object? CommandParameter
    {
        get => GetValue(CommandParameterProperty);
        set => SetValue(CommandParameterProperty, value);
    }

    public IconButtonComponent()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == SymbolProperty)
        {
            ButtonIcon.Symbol = change.GetNewValue<FASymbol>();
        }
        else if (change.Property == CommandProperty)
        {
            InnerButton.Command = change.GetNewValue<ICommand?>();
        }
        else if (change.Property == CommandParameterProperty)
        {
            InnerButton.CommandParameter = change.GetNewValue<object?>();
        }
    }
}
