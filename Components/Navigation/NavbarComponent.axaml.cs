using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.VisualTree;

namespace Quell.Components;

public partial class NavbarComponent : UserControl
{
    private Window? _hostWindow;

    public NavbarComponent()
    {
        InitializeComponent();
    }

    private Window? GetHostWindow() => _hostWindow ?? VisualRoot as Window ?? TopLevel.GetTopLevel(this) as Window;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        if (TopLevel.GetTopLevel(this) is Window win)
        {
            _hostWindow = win;
            win.PropertyChanged += OnWindowPropertyChanged;
            UpdateWindowState(win.WindowState);
        }
    }

    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        if (_hostWindow != null)
        {
            _hostWindow.PropertyChanged -= OnWindowPropertyChanged;
            _hostWindow = null;
        }
        base.OnDetachedFromVisualTree(e);
    }

    private void OnWindowPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Window.WindowStateProperty && e.NewValue is WindowState state)
        {
            UpdateWindowState(state);
        }
    }

    private void UpdateWindowState(WindowState state)
    {
        var isMaximized = state == WindowState.Maximized;
        var maxIcon = this.FindControl<PathIcon>("MaximizeIcon");
        var resIcon = this.FindControl<PathIcon>("RestoreIcon");
        if (maxIcon != null) maxIcon.IsVisible = !isMaximized;
        if (resIcon != null) resIcon.IsVisible = isMaximized;
    }

    private void OnTitleBarPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!e.GetCurrentPoint(this).Properties.IsLeftButtonPressed)
            return;

        // If user clicked inside an interactive control (Button, Menu, MenuItem, ComboBox, etc.), do nothing
        if (e.Source is Visual visual)
        {
            if (visual.FindAncestorOfType<Button>() != null ||
                visual.FindAncestorOfType<MenuItem>() != null ||
                visual.FindAncestorOfType<Menu>() != null ||
                visual.FindAncestorOfType<ComboBox>() != null ||
                visual.FindAncestorOfType<TextBox>() != null)
            {
                return;
            }
        }

        var win = GetHostWindow();
        if (win == null) return;

        if (e.ClickCount == 2)
        {
            win.WindowState = win.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }
        else
        {
            win.BeginMoveDrag(e);
        }
    }

    private void OnMinimizeClicked(object? sender, RoutedEventArgs e)
    {
        var win = GetHostWindow();
        if (win != null)
        {
            win.WindowState = WindowState.Minimized;
        }
    }

    private void OnMaximizeClicked(object? sender, RoutedEventArgs e)
    {
        var win = GetHostWindow();
        if (win != null)
        {
            win.WindowState = win.WindowState == WindowState.Maximized 
                ? WindowState.Normal 
                : WindowState.Maximized;
        }
    }

    private void OnCloseClicked(object? sender, RoutedEventArgs e)
    {
        GetHostWindow()?.Close();
    }
}
