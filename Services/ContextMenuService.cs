using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using System.Windows.Input;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using FluentAvalonia.UI.Controls;

namespace Quell.Services;

/// <summary>
/// Base descriptor for entries in a dynamically constructed <see cref="ContextMenu"/>.
/// </summary>
public abstract class ContextMenuEntry;

/// <summary>
/// Represents a visual separator line in a <see cref="ContextMenu"/>.
/// </summary>
public sealed class ContextMenuSeparatorDescriptor : ContextMenuEntry;

/// <summary>
/// Declarative descriptor for a single <see cref="MenuItem"/> with vector icon, gesture hint, enablement, and action.
/// </summary>
public sealed class ContextMenuItemDescriptor : ContextMenuEntry
{
    public string Header { get; init; } = string.Empty;
    public ICommand? Command { get; init; }
    public object? CommandParameter { get; init; }
    public Action? OnClick { get; init; }
    public Func<Task>? OnClickAsync { get; init; }
    public FASymbol? Symbol { get; init; }
    public FluentIcons.Common.Symbol? FluentSymbol { get; init; }
    public Geometry? IconGeometry { get; init; }
    public double IconFontSize { get; init; } = 11;
    public IBrush? IconBrush { get; init; }
    public KeyGesture? InputGesture { get; init; }
    public bool IsEnabled { get; init; } = true;
    public bool IsVisible { get; init; } = true;
    public IReadOnlyList<ContextMenuEntry>? Children { get; init; }
}

/// <summary>
/// Captures context about a right-click or keyboard <see cref="InputElement.ContextRequestedEvent"/> trigger.
/// </summary>
public sealed record ContextMenuRequest(
    Control Target,
    Visual? SourceVisual,
    bool IsPointerTriggered,
    Point? Position);

/// <summary>
/// Contract for context-aware menu providers attached via <see cref="ContextMenuService"/> or XAML behaviors.
/// </summary>
public interface IContextMenuProvider
{
    bool TryBuildMenu(ContextMenuRequest request, ContextMenuBuilder builder, out Control? placementTarget);
}

/// <summary>
/// Fluent builder for composing themed, Fluent-icon <see cref="ContextMenu"/> instances without UI boilerplate.
/// </summary>
public sealed class ContextMenuBuilder
{
    private readonly List<ContextMenuEntry> _entries = new();

    public IReadOnlyList<ContextMenuEntry> Entries => _entries;

    public ContextMenuBuilder AddItem(
        string header,
        ICommand? command = null,
        object? commandParameter = null,
        Action? onClick = null,
        Func<Task>? onClickAsync = null,
        FASymbol? symbol = null,
        FluentIcons.Common.Symbol? fluentSymbol = null,
        Geometry? iconGeometry = null,
        IBrush? iconBrush = null,
        double iconFontSize = 11,
        string? gesture = null,
        KeyGesture? inputGesture = null,
        bool isEnabled = true,
        bool isVisible = true)
    {
        KeyGesture? resolvedGesture = inputGesture;
        if (resolvedGesture == null && !string.IsNullOrWhiteSpace(gesture))
        {
            resolvedGesture = KeyGesture.Parse(gesture);
        }

        _entries.Add(new ContextMenuItemDescriptor
        {
            Header = header,
            Command = command,
            CommandParameter = commandParameter,
            OnClick = onClick,
            OnClickAsync = onClickAsync,
            Symbol = symbol,
            FluentSymbol = fluentSymbol,
            IconGeometry = iconGeometry,
            IconBrush = iconBrush,
            IconFontSize = iconFontSize,
            InputGesture = resolvedGesture,
            IsEnabled = isEnabled,
            IsVisible = isVisible
        });
        return this;
    }

    public ContextMenuBuilder AddSubMenu(
        string header,
        Action<ContextMenuBuilder> configure,
        FASymbol? symbol = null,
        FluentIcons.Common.Symbol? fluentSymbol = null,
        Geometry? iconGeometry = null,
        IBrush? iconBrush = null,
        double iconFontSize = 11,
        bool isEnabled = true,
        bool isVisible = true)
    {
        var childBuilder = new ContextMenuBuilder();
        configure(childBuilder);

        _entries.Add(new ContextMenuItemDescriptor
        {
            Header = header,
            Symbol = symbol,
            FluentSymbol = fluentSymbol,
            IconGeometry = iconGeometry,
            IconBrush = iconBrush,
            IconFontSize = iconFontSize,
            IsEnabled = isEnabled,
            IsVisible = isVisible,
            Children = childBuilder.Entries
        });
        return this;
    }

    public ContextMenuBuilder AddSeparator()
    {
        _entries.Add(new ContextMenuSeparatorDescriptor());
        return this;
    }

    public ContextMenuBuilder AddDescriptor(ContextMenuEntry entry)
    {
        _entries.Add(entry);
        return this;
    }

    public ContextMenu Build() => ContextMenuService.Build(this);
}

/// <summary>
/// Typed context-aware fluent builder that evaluates live state predicates against <typeparamref name="TContext"/>.
/// </summary>
public sealed class ContextMenuBuilder<TContext>
{
    public ContextMenuBuilder Inner { get; } = new();
    public TContext Context { get; }

    public ContextMenuBuilder(TContext context)
    {
        Context = context;
    }

    public ContextMenuBuilder<TContext> AddItem(
        string header,
        ICommand? command = null,
        Func<TContext, object?>? commandParameterSelector = null,
        Action<TContext>? onClick = null,
        Func<TContext, Task>? onClickAsync = null,
        FASymbol? symbol = null,
        FluentIcons.Common.Symbol? fluentSymbol = null,
        Geometry? iconGeometry = null,
        IBrush? iconBrush = null,
        double iconFontSize = 11,
        string? gesture = null,
        KeyGesture? inputGesture = null,
        Func<TContext, bool>? isEnabled = null,
        Func<TContext, bool>? isVisible = null)
    {
        bool visible = isVisible?.Invoke(Context) ?? true;
        bool enabled = isEnabled?.Invoke(Context) ?? true;
        object? parameter = commandParameterSelector != null ? commandParameterSelector(Context) : null;

        Inner.AddItem(
            header: header,
            command: command,
            commandParameter: parameter,
            onClick: onClick != null ? () => onClick(Context) : null,
            onClickAsync: onClickAsync != null ? () => onClickAsync(Context) : null,
            symbol: symbol,
            fluentSymbol: fluentSymbol,
            iconGeometry: iconGeometry,
            iconBrush: iconBrush,
            iconFontSize: iconFontSize,
            gesture: gesture,
            inputGesture: inputGesture,
            isEnabled: enabled,
            isVisible: visible);
        return this;
    }

    public ContextMenuBuilder<TContext> AddSubMenu(
        string header,
        Action<ContextMenuBuilder<TContext>> configure,
        FASymbol? symbol = null,
        FluentIcons.Common.Symbol? fluentSymbol = null,
        Geometry? iconGeometry = null,
        IBrush? iconBrush = null,
        double iconFontSize = 11,
        Func<TContext, bool>? isEnabled = null,
        Func<TContext, bool>? isVisible = null)
    {
        bool visible = isVisible?.Invoke(Context) ?? true;
        bool enabled = isEnabled?.Invoke(Context) ?? true;

        var childBuilder = new ContextMenuBuilder<TContext>(Context);
        configure(childBuilder);

        Inner.AddDescriptor(new ContextMenuItemDescriptor
        {
            Header = header,
            Symbol = symbol,
            FluentSymbol = fluentSymbol,
            IconGeometry = iconGeometry,
            IconBrush = iconBrush,
            IconFontSize = iconFontSize,
            IsEnabled = enabled,
            IsVisible = visible,
            Children = childBuilder.Inner.Entries
        });
        return this;
    }

    public ContextMenuBuilder<TContext> AddSeparator()
    {
        Inner.AddSeparator();
        return this;
    }
}

/// <summary>
/// Centralized project-wide right-click context menu builder and lifecycle service.
/// Produces theme-compliant Avalonia <see cref="ContextMenu"/> instances styled by ClassicStyles.axaml.
/// </summary>
public static class ContextMenuService
{
    public static ContextMenu Build(ContextMenuBuilder builder)
    {
        var menu = new ContextMenu();
        PopulateItems(menu.Items, builder.Entries);
        return menu;
    }

    public static ContextMenu Build<TContext>(TContext context, Action<ContextMenuBuilder<TContext>> configure)
    {
        var builder = new ContextMenuBuilder<TContext>(context);
        configure(builder);
        return Build(builder.Inner);
    }

    public static bool Show<TContext>(
        Control owner,
        TContext context,
        Action<ContextMenuBuilder<TContext>, TContext> configure,
        Control? placementTarget = null,
        bool isPointer = true)
    {
        var builder = new ContextMenuBuilder<TContext>(context);
        configure(builder, context);

        var menu = Build(builder.Inner);
        if (menu.Items.Count == 0)
        {
            owner.ContextMenu = null;
            return false;
        }

        OpenMenu(owner, placementTarget ?? owner, menu, isPointer);
        return true;
    }

    /// <summary>
    /// Attaches a dynamic, context-aware right-click and keyboard (<c>Shift+F10</c> / Menu key) handler to <paramref name="target"/>.
    /// </summary>
    public static IDisposable Attach<TContext>(
        Control target,
        Func<ContextMenuRequest, TContext?> contextResolver,
        Action<ContextMenuBuilder<TContext>, TContext> configure,
        Func<TContext, Control?>? placementTargetResolver = null,
        RoutingStrategies routing = RoutingStrategies.Bubble)
    {
        void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            bool isPointer = e.TryGetPosition(target, out Point pos);
            var request = new ContextMenuRequest(target, e.Source as Visual, isPointer, isPointer ? pos : null);
            var context = contextResolver(request);

            if (context == null)
            {
                target.ContextMenu = null;
                return;
            }

            Control placementTarget = placementTargetResolver?.Invoke(context) ?? target;
            if (Show(target, context, configure, placementTarget, isPointer))
            {
                e.Handled = true;
            }
        }

        target.AddHandler(InputElement.ContextRequestedEvent, OnContextRequested, routing);
        return new Subscription(() => target.RemoveHandler(InputElement.ContextRequestedEvent, OnContextRequested));
    }

    /// <summary>
    /// Attaches an <see cref="IContextMenuProvider"/> implementation to <paramref name="target"/>.
    /// </summary>
    public static IDisposable AttachProvider(
        Control target,
        IContextMenuProvider provider,
        RoutingStrategies routing = RoutingStrategies.Bubble)
    {
        void OnContextRequested(object? sender, ContextRequestedEventArgs e)
        {
            bool isPointer = e.TryGetPosition(target, out Point pos);
            var request = new ContextMenuRequest(target, e.Source as Visual, isPointer, isPointer ? pos : null);
            var builder = new ContextMenuBuilder();

            if (!provider.TryBuildMenu(request, builder, out var customPlacementTarget))
            {
                target.ContextMenu = null;
                return;
            }

            var menu = Build(builder);
            if (menu.Items.Count == 0)
            {
                target.ContextMenu = null;
                return;
            }

            Control placementTarget = customPlacementTarget ?? target;
            OpenMenu(target, placementTarget, menu, isPointer);
            e.Handled = true;
        }

        target.AddHandler(InputElement.ContextRequestedEvent, OnContextRequested, routing);
        return new Subscription(() => target.RemoveHandler(InputElement.ContextRequestedEvent, OnContextRequested));
    }

    private static void OpenMenu(Control owner, Control placementTarget, ContextMenu menu, bool isPointer)
    {
        if (!ReferenceEquals(owner, placementTarget))
        {
            owner.ContextMenu = null;
        }

        menu.Placement = isPointer ? PlacementMode.Pointer : PlacementMode.BottomEdgeAlignedLeft;
        menu.PlacementTarget = placementTarget;

        void OnClosed(object? sender, RoutedEventArgs args)
        {
            menu.Closed -= OnClosed;
            if (ReferenceEquals(placementTarget.ContextMenu, menu))
            {
                placementTarget.ContextMenu = null;
            }
        }

        menu.Closed += OnClosed;
        placementTarget.ContextMenu = menu;
        menu.Open(placementTarget);
    }

    private static void PopulateItems(ItemCollection targetItems, IReadOnlyList<ContextMenuEntry> entries)
    {
        bool pendingSeparator = false;
        bool hasAnyItem = false;

        foreach (var entry in entries)
        {
            if (entry is ContextMenuSeparatorDescriptor)
            {
                if (hasAnyItem)
                {
                    pendingSeparator = true;
                }
                continue;
            }

            if (entry is not ContextMenuItemDescriptor item || !item.IsVisible)
            {
                continue;
            }

            if (pendingSeparator)
            {
                targetItems.Add(new Separator());
                pendingSeparator = false;
            }

            targetItems.Add(CreateMenuItem(item));
            hasAnyItem = true;
        }
    }

    private static MenuItem CreateMenuItem(ContextMenuItemDescriptor descriptor)
    {
        var menuItem = new MenuItem
        {
            Header = descriptor.Header,
            IsEnabled = descriptor.IsEnabled,
            InputGesture = descriptor.InputGesture,
            Icon = CreateIcon(descriptor)
        };

        if (descriptor.Command != null)
        {
            menuItem.Command = descriptor.Command;
            if (descriptor.CommandParameter != null)
            {
                menuItem.CommandParameter = descriptor.CommandParameter;
            }
        }
        else if (descriptor.OnClickAsync != null)
        {
            var asyncHandler = descriptor.OnClickAsync;
            menuItem.Click += async (_, _) =>
            {
                try
                {
                    await asyncHandler();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[ContextMenuService] Async menu action '{descriptor.Header}' failed: {ex.Message}");
                }
            };
        }
        else if (descriptor.OnClick != null)
        {
            var syncHandler = descriptor.OnClick;
            menuItem.Click += (_, _) => syncHandler();
        }

        if (descriptor.Children is { Count: > 0 })
        {
            PopulateItems(menuItem.Items, descriptor.Children);
        }

        return menuItem;
    }

    private static Control? CreateIcon(ContextMenuItemDescriptor descriptor)
    {
        if (descriptor.Symbol.HasValue)
        {
            var icon = new FASymbolIcon
            {
                Symbol = descriptor.Symbol.Value,
                FontSize = descriptor.IconFontSize
            };
            if (descriptor.IconBrush != null)
            {
                icon.Foreground = descriptor.IconBrush;
            }
            return icon;
        }

        if (descriptor.FluentSymbol.HasValue)
        {
            var icon = new FluentIcons.Avalonia.SymbolIcon
            {
                Symbol = descriptor.FluentSymbol.Value,
                FontSize = descriptor.IconFontSize
            };
            if (descriptor.IconBrush != null)
            {
                icon.Foreground = descriptor.IconBrush;
            }
            return icon;
        }

        if (descriptor.IconGeometry != null)
        {
            var icon = new PathIcon
            {
                Data = descriptor.IconGeometry,
                Width = descriptor.IconFontSize,
                Height = descriptor.IconFontSize
            };
            if (descriptor.IconBrush != null)
            {
                icon.Foreground = descriptor.IconBrush;
            }
            return icon;
        }

        return null;
    }

    private sealed class Subscription : IDisposable
    {
        private Action? _dispose;

        public Subscription(Action dispose)
        {
            _dispose = dispose;
        }

        public void Dispose()
        {
            _dispose?.Invoke();
            _dispose = null;
        }
    }
}
