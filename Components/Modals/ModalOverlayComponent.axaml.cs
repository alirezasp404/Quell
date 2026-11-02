using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Metadata;

namespace Fdb.Components;

public partial class ModalOverlayComponent : UserControl
{
    public static readonly StyledProperty<bool> IsOpenProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, bool>(nameof(IsOpen), false);

    public static readonly StyledProperty<string> TitleProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, string>(nameof(Title), "Dialog");

    public static readonly StyledProperty<string?> SubtitleProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, string?>(nameof(Subtitle));

    public static readonly StyledProperty<string> IconProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, string>(nameof(Icon), "Important");

    public static readonly StyledProperty<ICommand?> CloseCommandProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, ICommand?>(nameof(CloseCommand));

    public static readonly StyledProperty<string?> PrimaryButtonTextProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, string?>(nameof(PrimaryButtonText), "OK");

    public static readonly StyledProperty<ICommand?> PrimaryCommandProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, ICommand?>(nameof(PrimaryCommand));

    public static readonly StyledProperty<string?> SecondaryButtonTextProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, string?>(nameof(SecondaryButtonText));

    public static readonly StyledProperty<ICommand?> SecondaryCommandProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, ICommand?>(nameof(SecondaryCommand));

    public static readonly StyledProperty<string> CancelButtonTextProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, string>(nameof(CancelButtonText), "Cancel");

    public static readonly StyledProperty<double> DialogWidthProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, double>(nameof(DialogWidth), 520.0);

    public static readonly StyledProperty<object?> ModalContentProperty =
        AvaloniaProperty.Register<ModalOverlayComponent, object?>(nameof(ModalContent));

    public bool IsOpen
    {
        get => GetValue(IsOpenProperty);
        set => SetValue(IsOpenProperty, value);
    }

    public string Title
    {
        get => GetValue(TitleProperty);
        set => SetValue(TitleProperty, value);
    }

    public string? Subtitle
    {
        get => GetValue(SubtitleProperty);
        set => SetValue(SubtitleProperty, value);
    }

    public string Icon
    {
        get => GetValue(IconProperty);
        set => SetValue(IconProperty, value);
    }

    public ICommand? CloseCommand
    {
        get => GetValue(CloseCommandProperty);
        set => SetValue(CloseCommandProperty, value);
    }

    public string? PrimaryButtonText
    {
        get => GetValue(PrimaryButtonTextProperty);
        set => SetValue(PrimaryButtonTextProperty, value);
    }

    public ICommand? PrimaryCommand
    {
        get => GetValue(PrimaryCommandProperty);
        set => SetValue(PrimaryCommandProperty, value);
    }

    public string? SecondaryButtonText
    {
        get => GetValue(SecondaryButtonTextProperty);
        set => SetValue(SecondaryButtonTextProperty, value);
    }

    public ICommand? SecondaryCommand
    {
        get => GetValue(SecondaryCommandProperty);
        set => SetValue(SecondaryCommandProperty, value);
    }

    public string CancelButtonText
    {
        get => GetValue(CancelButtonTextProperty);
        set => SetValue(CancelButtonTextProperty, value);
    }

    public double DialogWidth
    {
        get => GetValue(DialogWidthProperty);
        set => SetValue(DialogWidthProperty, value);
    }

    [Content]
    public object? ModalContent
    {
        get => GetValue(ModalContentProperty);
        set => SetValue(ModalContentProperty, value);
    }

    public bool HasSubtitle => !string.IsNullOrWhiteSpace(Subtitle);
    public bool HasPrimaryButton => !string.IsNullOrWhiteSpace(PrimaryButtonText);
    public bool HasSecondaryButton => !string.IsNullOrWhiteSpace(SecondaryButtonText);

    public ModalOverlayComponent()
    {
        InitializeComponent();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Dismiss dialog on clicking outside if CloseCommand is provided
        if (CloseCommand != null && CloseCommand.CanExecute(null))
        {
            CloseCommand.Execute(null);
        }
        else
        {
            IsOpen = false;
        }
    }
}
