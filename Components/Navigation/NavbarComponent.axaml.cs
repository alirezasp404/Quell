using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;
using Avalonia.VisualTree;
using Quell.Services;
using Quell.ViewModels;

namespace Quell.Components;

public partial class NavbarComponent : UserControl
{
    private Window? _hostWindow;
    private NavbarViewModel? _viewModel;
    private CancellationTokenSource? _cascadeCts;

    public NavbarComponent()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;
        UpdateGpuModeClass();
        HardwareGraphicsService.Instance.RenderingModeChanged += (s, e) => 
            Avalonia.Threading.Dispatcher.UIThread.Post(UpdateGpuModeClass);
    }

    public void UpdateGpuModeClass()
    {
        bool isGpu = HardwareGraphicsService.Instance.IsGpuMotionActive;
        if (isGpu)
        {
            if (!Classes.Contains("gpu-mode"))
            {
                Classes.Add("gpu-mode");
            }
        }
        else
        {
            Classes.Remove("gpu-mode");
        }
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
        }
        _viewModel = DataContext as NavbarViewModel;
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            ApplyNavMenuVisualState(_viewModel.IsNavMenuOpen);
        }
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(NavbarViewModel.IsNavMenuOpen) && _viewModel != null)
        {
            ApplyNavMenuVisualState(_viewModel.IsNavMenuOpen);
        }
    }

    public void OnHamburgerClicked(object? sender, RoutedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.IsNavMenuOpen = !_viewModel.IsNavMenuOpen;
        }
        else
        {
            var container = this.FindControl<Border>("NavMenuContainer");
            bool isOpen = container != null && container.Classes.Contains("menu-open");
            ApplyNavMenuVisualState(!isOpen);
        }
    }

    public async void ApplyNavMenuVisualState(bool isOpen)
    {
        _cascadeCts?.Cancel();
        _cascadeCts = new CancellationTokenSource();
        var token = _cascadeCts.Token;

        var container = this.FindControl<Border>("NavMenuContainer");
        var hamburger = this.FindControl<Button>("HamburgerButton");
        var menuBar = this.FindControl<Menu>("NavMenuBar");

        if (container == null || hamburger == null) return;

        bool isGpu = HardwareGraphicsService.Instance.IsGpuMotionActive;

        if (isOpen)
        {
            hamburger.Classes.Add("active-menu");
            container.Classes.Add("menu-open");

            if (isGpu && menuBar != null)
            {
                // GPU-Accelerated Staggered Cascade Motion Pipeline:
                // Smoothly cascades each top-level MenuItem into the navbar
                var items = GetTopLevelMenuItems(menuBar);
                for (int i = 0; i < items.Count; i++)
                {
                    var item = items[i];
                    item.Opacity = 0.0;
                    item.RenderTransform = TransformOperations.Parse("translateX(-14px) scale(0.92)");
                }

                for (int i = 0; i < items.Count; i++)
                {
                    if (token.IsCancellationRequested) break;
                    var item = items[i];
                    item.Opacity = 1.0;
                    item.RenderTransform = TransformOperations.Parse("translateX(0px) scale(1.0)");
                    try
                    {
                        await Task.Delay(24, token);
                    }
                    catch (TaskCanceledException)
                    {
                        break;
                    }
                }
            }
            else if (menuBar != null)
            {
                // CPU Mode: Lightweight immediate display without multi-step delay loops
                var items = GetTopLevelMenuItems(menuBar);
                foreach (var item in items)
                {
                    item.Opacity = 1.0;
                    item.RenderTransform = null;
                }
            }
        }
        else
        {
            hamburger.Classes.Remove("active-menu");
            container.Classes.Remove("menu-open");

            if (menuBar != null)
            {
                var items = GetTopLevelMenuItems(menuBar);
                foreach (var item in items)
                {
                    item.Opacity = 1.0;
                    item.RenderTransform = null;
                }
            }
        }
    }

    private List<MenuItem> GetTopLevelMenuItems(Menu menuBar)
    {
        var list = new List<MenuItem>();
        foreach (var item in menuBar.Items)
        {
            if (item is MenuItem mi)
            {
                list.Add(mi);
            }
        }
        return list;
    }

    private Window? GetHostWindow() => _hostWindow ?? VisualRoot as Window ?? TopLevel.GetTopLevel(this) as Window;

    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        UpdateGpuModeClass();
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
