using System;
using System.ComponentModel;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Quell.Services;
using Quell.ViewModels;

namespace Quell.Views;

public partial class MainWindow : Window
{
    private GridLength _lastSidebarWidth = new(240, GridUnitType.Pixel);
    private GridLength _lastFooterHeight = new(140, GridUnitType.Pixel);
    private MainViewModel? _viewModel;

    public MainWindow()
    {
        InitializeComponent();
        DataContextChanged += OnDataContextChanged;

        if (SidebarSplitter != null)
        {
            SidebarSplitter.DragCompleted += OnSidebarSplitterDragCompleted;
        }

        if (FooterSplitter != null)
        {
            FooterSplitter.DragCompleted += OnFooterSplitterDragCompleted;
        }

        // Global Tunneling Key Handler: intercepts application shortcuts (Ctrl+B, F5, Ctrl+J, etc.)
        // at the Window root level before any child control (e.g. AvaloniaEdit TextEditor) can swallow them!
        AddHandler(KeyDownEvent, OnWindowKeyDownTunnel, RoutingStrategies.Tunnel);
    }

    private void OnWindowKeyDownTunnel(object? sender, KeyEventArgs e)
    {
        HandleApplicationShortcut(e);
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Sidebar.PropertyChanged -= OnSidebarPropertyChanged;
            _viewModel.Footer.PropertyChanged -= OnFooterPropertyChanged;
        }

        _viewModel = DataContext as MainViewModel;

        if (_viewModel != null)
        {
            _viewModel.Sidebar.PropertyChanged += OnSidebarPropertyChanged;
            _viewModel.Footer.PropertyChanged += OnFooterPropertyChanged;
            UpdateSidebarLayout();
            UpdateFooterLayout();
        }
    }

    private void OnSidebarPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(SidebarViewModel.IsVisible))
        {
            UpdateSidebarLayout();
        }
    }

    private void OnFooterPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(FooterViewModel.IsVisible) || e.PropertyName == nameof(FooterViewModel.IsMaximized))
        {
            UpdateFooterLayout();
        }
    }

    private void OnSidebarSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        if (MainGrid == null || MainGrid.ColumnDefinitions.Count < 3) return;

        var col0 = MainGrid.ColumnDefinitions[0];
        if (col0.Width.IsAbsolute && col0.Width.Value >= 160)
        {
            _lastSidebarWidth = col0.Width;
        }

        // Always ensure center workspace remains Star (*) so it dynamically fills space!
        MainGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
    }

    private void OnFooterSplitterDragCompleted(object? sender, VectorEventArgs e)
    {
        if (CenterGrid == null || CenterGrid.RowDefinitions.Count < 3) return;

        var row2 = CenterGrid.RowDefinitions[2];
        if (row2.Height.IsAbsolute && row2.Height.Value >= 80)
        {
            _lastFooterHeight = row2.Height;
        }

        // Always ensure document editor remains Star (*) so it dynamically fills space!
        CenterGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
    }

    public void UpdateSidebarLayout()
    {
        if (_viewModel == null || MainGrid == null || MainGrid.ColumnDefinitions.Count < 3) return;
        var col = MainGrid.ColumnDefinitions[0];

        if (_viewModel.Sidebar.IsVisible)
        {
            col.MinWidth = 160;
            col.MaxWidth = 450;
            double w = _lastSidebarWidth.Value;
            if (w < 160) w = 240;
            if (w > 450) w = 450;
            col.Width = new GridLength(w, GridUnitType.Pixel);
        }
        else
        {
            if (col.Width.IsAbsolute && col.Width.Value >= 160)
            {
                _lastSidebarWidth = col.Width;
            }
            col.MinWidth = 0;
            col.Width = new GridLength(0, GridUnitType.Pixel);
        }

        // Ensure center workspace takes all remaining horizontal space
        MainGrid.ColumnDefinitions[2].Width = new GridLength(1, GridUnitType.Star);
    }

    public void UpdateFooterLayout()
    {
        if (_viewModel == null || CenterGrid == null || CenterGrid.RowDefinitions.Count < 3) return;
        var row = CenterGrid.RowDefinitions[2];

        if (_viewModel.Footer.IsVisible)
        {
            if (_viewModel.Footer.IsMaximized)
            {
                // Maximize Footer / Excel to fill entire workspace height
                CenterGrid.RowDefinitions[0].MinHeight = 0;
                CenterGrid.RowDefinitions[0].Height = new GridLength(0, GridUnitType.Pixel);
                CenterGrid.RowDefinitions[1].Height = new GridLength(0, GridUnitType.Pixel);
                if (FooterSplitter != null)
                {
                    FooterSplitter.IsVisible = false;
                }

                row.MinHeight = 0;
                row.MaxHeight = double.PositiveInfinity;
                row.Height = new GridLength(1, GridUnitType.Star);
            }
            else
            {
                // Restore standard split workspace layout
                CenterGrid.RowDefinitions[0].MinHeight = 180;
                CenterGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
                CenterGrid.RowDefinitions[1].Height = GridLength.Auto;
                if (FooterSplitter != null)
                {
                    FooterSplitter.IsVisible = true;
                }

                row.MinHeight = 70;
                row.MaxHeight = 450;
                double h = _lastFooterHeight.Value;
                if (h < 70) h = 140;
                if (h > 450) h = 450;
                row.Height = new GridLength(h, GridUnitType.Pixel);
            }
        }
        else
        {
            if (row.Height.IsAbsolute && row.Height.Value >= 70)
            {
                _lastFooterHeight = row.Height;
            }
            row.MinHeight = 0;
            row.Height = new GridLength(0, GridUnitType.Pixel);
            CenterGrid.RowDefinitions[0].MinHeight = 180;
            CenterGrid.RowDefinitions[0].Height = new GridLength(1, GridUnitType.Star);
            if (FooterSplitter != null)
            {
                FooterSplitter.IsVisible = false;
            }
        }
    }

    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);
        HandleApplicationShortcut(e);
    }

    private void HandleApplicationShortcut(KeyEventArgs e)
    {
        if (e.Handled || _viewModel == null) return;

        // If a modal dialog is open, do not execute workspace shortcuts (except Escape)
        if (_viewModel.SettingsModal.IsOpen || _viewModel.ConnectModal.IsOpen || _viewModel.ConfirmModal.IsOpen || _viewModel.ExportModal.IsOpen || _viewModel.DiagramModal.IsOpen)
        {
            if (e.Key == Key.Escape)
            {
                if (_viewModel.DiagramModal.IsOpen)
                {
                    _viewModel.DiagramModal.IsOpen = false;
                    e.Handled = true;
                }
                else if (_viewModel.ConnectModal.IsOpen)
                {
                    _viewModel.ConnectModal.IsOpen = false;
                    e.Handled = true;
                }
                else if (_viewModel.ConfirmModal.IsOpen)
                {
                    _viewModel.ConfirmModal.IsOpen = false;
                    e.Handled = true;
                }
                else if (_viewModel.ExportModal.IsOpen)
                {
                    _viewModel.ExportModal.IsOpen = false;
                    e.Handled = true;
                }
                else if (_viewModel.SettingsModal.IsOpen)
                {
                    _viewModel.SettingsModal.CloseCommand.Execute(null);
                    e.Handled = true;
                }
            }
            return;
        }

        // Toggle or Close Explorer Sidebar (Ctrl+B)
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ToggleSidebar, e) || 
            (e.Key == Key.B && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.ToggleSidebarCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Toggle Fullscreen Table/Footer
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ToggleFullscreen, e) ||
            e.Key == Key.F11 || (e.KeyModifiers.HasFlag(KeyModifiers.Control) && e.KeyModifiers.HasFlag(KeyModifiers.Shift) && e.Key == Key.F))
        {
            _viewModel.ToggleFooterMaximizedCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Run active SQL query
        if (ShortcutService.Instance.IsMatch(ShortcutActions.RunQuery, e) || e.Key == Key.F5)
        {
            _viewModel.ExecuteQueryCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Save current query
        if (ShortcutService.Instance.IsMatch(ShortcutActions.SaveQuery, e) || (e.Key == Key.S && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.SaveQueryCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // New query tab
        if (ShortcutService.Instance.IsMatch(ShortcutActions.NewTab, e) || (e.Key == Key.N && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.Editor.AddTabCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Close query tab
        if (ShortcutService.Instance.IsMatch(ShortcutActions.CloseTab, e) || (e.Key == Key.W && e.KeyModifiers == KeyModifiers.Control))
        {
            if (_viewModel.Editor.SelectedTab != null)
            {
                _viewModel.Editor.CloseTabCommand.Execute(_viewModel.Editor.SelectedTab);
                e.Handled = true;
                return;
            }
        }

        // Connect Database
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ConnectDatabase, e) || (e.Key == Key.O && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.ConnectModal.IsOpen = true;
            e.Handled = true;
            return;
        }

        // Toggle Footer
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ToggleFooter, e) || (e.Key == Key.J && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.ToggleFooterCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Toggle Terminal (Ctrl+` or Ctrl+Tilde)
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ToggleTerminal, e) || 
            (e.KeyModifiers == KeyModifiers.Control && (e.Key == Key.Oem3 || e.Key == Key.OemTilde)))
        {
            _viewModel.ToggleTerminalCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Run in Terminal (Ctrl+Shift+Enter)
        if (ShortcutService.Instance.IsMatch(ShortcutActions.RunInTerminal, e) || 
            (e.KeyModifiers == (KeyModifiers.Control | KeyModifiers.Shift) && e.Key == Key.Enter))
        {
            _viewModel.RunSelectionInTerminalCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Export Data
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ExportData, e) || (e.Key == Key.E && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.ExportModal.OpenCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Open Settings
        if (ShortcutService.Instance.IsMatch(ShortcutActions.OpenSettings, e) || (e.Key == Key.OemComma && e.KeyModifiers == KeyModifiers.Control))
        {
            _viewModel.SettingsModal.OpenCommand.Execute(null);
            e.Handled = true;
            return;
        }

        // Zoom In SQL Editor Text
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ZoomIn, e) || (e.KeyModifiers.HasFlag(KeyModifiers.Control) && (e.Key is Key.OemPlus or Key.Add)))
        {
            MainEditor?.ZoomIn();
            e.Handled = true;
            return;
        }

        // Zoom Out SQL Editor Text
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ZoomOut, e) || (e.KeyModifiers.HasFlag(KeyModifiers.Control) && (e.Key is Key.OemMinus or Key.Subtract)))
        {
            MainEditor?.ZoomOut();
            e.Handled = true;
            return;
        }

        // Reset SQL Editor Zoom
        if (ShortcutService.Instance.IsMatch(ShortcutActions.ResetZoom, e) || (e.KeyModifiers.HasFlag(KeyModifiers.Control) && (e.Key is Key.D0 or Key.NumPad0)))
        {
            MainEditor?.ResetZoom();
            e.Handled = true;
            return;
        }

        // Escape: restore footer if maximized
        if (e.Key == Key.Escape)
        {
            if (_viewModel.Footer.IsMaximized)
            {
                _viewModel.Footer.IsMaximized = false;
                e.Handled = true;
            }
        }
    }
}
