using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using FluentAvalonia.UI.Controls;

namespace Fdb.Components;

public partial class ActionButtonComponent : UserControl
{
    public static readonly StyledProperty<string> TextProperty =
        AvaloniaProperty.Register<ActionButtonComponent, string>(nameof(Text), "Action");

    public static readonly StyledProperty<FASymbol> SymbolProperty =
        AvaloniaProperty.Register<ActionButtonComponent, FASymbol>(nameof(Symbol), FASymbol.Play);

    public static readonly StyledProperty<ICommand?> CommandProperty =
        AvaloniaProperty.Register<ActionButtonComponent, ICommand?>(nameof(Command));

    public static readonly StyledProperty<object?> CommandParameterProperty =
        AvaloniaProperty.Register<ActionButtonComponent, object?>(nameof(CommandParameter));

    public string Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

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

    public ActionButtonComponent()
    {
        InitializeComponent();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);

        if (change.Property == TextProperty)
        {
            ButtonText.Text = change.GetNewValue<string>();
        }
        else if (change.Property == SymbolProperty)
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
