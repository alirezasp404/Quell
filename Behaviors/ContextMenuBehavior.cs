using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Xaml.Interactivity;
using Quell.Services;

namespace Quell.Behaviors;

/// <summary>
/// Reusable XAML behavior and attached-property helper that binds an <see cref="IContextMenuProvider"/>
/// to any Avalonia <see cref="Control"/> via <see cref="ContextMenuService"/>.
/// </summary>
public sealed class ContextMenuBehavior : Behavior<Control>
{
    public static readonly StyledProperty<IContextMenuProvider?> ProviderProperty =
        AvaloniaProperty.Register<ContextMenuBehavior, IContextMenuProvider?>(nameof(Provider));

    public static readonly StyledProperty<RoutingStrategies> RoutingProperty =
        AvaloniaProperty.Register<ContextMenuBehavior, RoutingStrategies>(nameof(Routing), RoutingStrategies.Bubble);

    public static readonly AttachedProperty<IContextMenuProvider?> AttachedProviderProperty =
        AvaloniaProperty.RegisterAttached<ContextMenuBehavior, Control, IContextMenuProvider?>("AttachedProvider");

    private static readonly AttachedProperty<IDisposable?> AttachedSubscriptionProperty =
        AvaloniaProperty.RegisterAttached<ContextMenuBehavior, Control, IDisposable?>("AttachedSubscription");

    private IDisposable? _subscription;

    static ContextMenuBehavior()
    {
        AttachedProviderProperty.Changed.AddClassHandler<Control>(OnAttachedProviderChanged);
    }

    public IContextMenuProvider? Provider
    {
        get => GetValue(ProviderProperty);
        set => SetValue(ProviderProperty, value);
    }

    public RoutingStrategies Routing
    {
        get => GetValue(RoutingProperty);
        set => SetValue(RoutingProperty, value);
    }

    public static IContextMenuProvider? GetAttachedProvider(Control control) =>
        control.GetValue(AttachedProviderProperty);

    public static void SetAttachedProvider(Control control, IContextMenuProvider? value) =>
        control.SetValue(AttachedProviderProperty, value);

    protected override void OnAttached()
    {
        base.OnAttached();
        Rebind();
    }

    protected override void OnDetaching()
    {
        _subscription?.Dispose();
        _subscription = null;
        base.OnDetaching();
    }

    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == ProviderProperty || change.Property == RoutingProperty)
        {
            Rebind();
        }
    }

    private void Rebind()
    {
        _subscription?.Dispose();
        _subscription = null;

        if (AssociatedObject != null && Provider != null)
        {
            _subscription = ContextMenuService.AttachProvider(AssociatedObject, Provider, Routing);
        }
    }

    private static void OnAttachedProviderChanged(Control control, AvaloniaPropertyChangedEventArgs e)
    {
        control.GetValue(AttachedSubscriptionProperty)?.Dispose();
        control.SetValue(AttachedSubscriptionProperty, null);

        if (e.NewValue is IContextMenuProvider provider)
        {
            var subscription = ContextMenuService.AttachProvider(control, provider);
            control.SetValue(AttachedSubscriptionProperty, subscription);
        }
    }
}
