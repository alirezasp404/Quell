using System;
using System.ComponentModel;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.VisualTree;
using Quell.ViewModels;

namespace Quell.Components;

public partial class DatabaseDiagramModalComponent : UserControl
{
    private DatabaseDiagramModalViewModel? _viewModel;

    private readonly ScaleTransform _canvasScale = new(1.0, 1.0);
    private readonly TranslateTransform _canvasTranslate = new(40.0, 40.0);

    // Pan state
    private bool _isPanning;
    private Point _panStartPointer;
    private Point _panStartOffset;

    // Table card drag state
    private DiagramTableItem? _draggedTable;
    private Point _dragStartPointer;
    private Point _tableStartPos;

    public DatabaseDiagramModalComponent()
    {
        InitializeComponent();

        if (WorldCanvas != null)
        {
            WorldCanvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
            WorldCanvas.RenderTransform = new TransformGroup
            {
                Children = { _canvasScale, _canvasTranslate }
            };
        }

        if (ViewportContainer != null)
        {
            ViewportContainer.SizeChanged += (s, e) =>
            {
                if (_viewModel != null && e.NewSize.Width > 50 && e.NewSize.Height > 50)
                {
                    _viewModel.ViewportWidth = e.NewSize.Width;
                    _viewModel.ViewportHeight = e.NewSize.Height;
                }
            };
        }

        DataContextChanged += OnDataContextChanged;
        Loaded += OnLoaded;
    }

    private void OnDataContextChanged(object? sender, EventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.PropertyChanged -= OnViewModelPropertyChanged;
            _viewModel.RequestCanvasUpdate -= OnRequestCanvasUpdate;
        }

        _viewModel = DataContext as DatabaseDiagramModalViewModel;

        if (_viewModel != null)
        {
            _viewModel.PropertyChanged += OnViewModelPropertyChanged;
            _viewModel.RequestCanvasUpdate += OnRequestCanvasUpdate;
            UpdateCanvasTransform();
        }
    }

    private void OnLoaded(object? sender, RoutedEventArgs e)
    {
        if (WorldCanvas != null)
        {
            WorldCanvas.RenderTransformOrigin = new RelativePoint(0, 0, RelativeUnit.Relative);
            if (WorldCanvas.RenderTransform is not TransformGroup)
            {
                WorldCanvas.RenderTransform = new TransformGroup
                {
                    Children = { _canvasScale, _canvasTranslate }
                };
            }
        }

        if (ViewportContainer != null && _viewModel != null)
        {
            if (ViewportContainer.Bounds.Width > 50) _viewModel.ViewportWidth = ViewportContainer.Bounds.Width;
            if (ViewportContainer.Bounds.Height > 50) _viewModel.ViewportHeight = ViewportContainer.Bounds.Height;
        }

        UpdateCanvasTransform();
    }

    private void OnViewModelPropertyChanged(object? sender, PropertyChangedEventArgs e)
    {
        if (e.PropertyName == nameof(DatabaseDiagramModalViewModel.ZoomLevel) ||
            e.PropertyName == nameof(DatabaseDiagramModalViewModel.PanX) ||
            e.PropertyName == nameof(DatabaseDiagramModalViewModel.PanY))
        {
            UpdateCanvasTransform();
        }
        else if (e.PropertyName == nameof(DatabaseDiagramModalViewModel.IsOpen))
        {
            if (_viewModel?.IsOpen == true)
            {
                if (ViewportContainer != null && _viewModel != null)
                {
                    if (ViewportContainer.Bounds.Width > 50) _viewModel.ViewportWidth = ViewportContainer.Bounds.Width;
                    if (ViewportContainer.Bounds.Height > 50) _viewModel.ViewportHeight = ViewportContainer.Bounds.Height;
                }
                UpdateCanvasTransform();
                DiagramSurface?.InvalidateVisual();
            }
        }
    }

    private void OnRequestCanvasUpdate()
    {
        UpdateCanvasTransform();
        DiagramSurface?.InvalidateVisual();
    }

    private void UpdateCanvasTransform()
    {
        if (_viewModel == null) return;

        _canvasScale.ScaleX = _viewModel.ZoomLevel;
        _canvasScale.ScaleY = _viewModel.ZoomLevel;
        _canvasTranslate.X = _viewModel.PanX;
        _canvasTranslate.Y = _viewModel.PanY;
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel != null)
        {
            _viewModel.Close();
        }
    }

    #region Canvas Pan & Zoom

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't pan if clicking directly on a button or interactive child
        if (e.Source is Button || (e.Source is Visual v && v.FindAncestorOfType<Button>() != null))
        {
            return;
        }

        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed || props.IsMiddleButtonPressed || props.IsRightButtonPressed)
        {
            _isPanning = true;
            _panStartPointer = e.GetPosition(ViewportContainer);
            _panStartOffset = new Point(_viewModel?.PanX ?? 40, _viewModel?.PanY ?? 40);
            if (ViewportContainer != null)
            {
                e.Pointer.Capture(ViewportContainer);
                ViewportContainer.Cursor = new Cursor(StandardCursorType.Hand);
            }
            e.Handled = true;
        }
    }

    private void OnCanvasPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_isPanning && _viewModel != null && ViewportContainer != null)
        {
            var current = e.GetPosition(ViewportContainer);
            var delta = current - _panStartPointer;
            _viewModel.PanX = _panStartOffset.X + delta.X;
            _viewModel.PanY = _panStartOffset.Y + delta.Y;
            UpdateCanvasTransform();
            e.Handled = true;
        }
    }

    private void OnCanvasPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_isPanning)
        {
            _isPanning = false;
            e.Pointer.Capture(null);
            if (ViewportContainer != null)
            {
                ViewportContainer.Cursor = Cursor.Default;
            }
            e.Handled = true;
        }
    }

    private void OnCanvasPointerWheelChanged(object? sender, PointerWheelEventArgs e)
    {
        if (_viewModel == null || ViewportContainer == null) return;

        var screenPos = e.GetPosition(ViewportContainer);
        double currentZoom = _viewModel.ZoomLevel;

        // Position relative to canvas before zoom
        double canvasX = (screenPos.X - _viewModel.PanX) / currentZoom;
        double canvasY = (screenPos.Y - _viewModel.PanY) / currentZoom;

        // Smooth zoom step
        double factor = e.Delta.Y > 0 ? 1.12 : 0.88;
        double newZoom = Math.Clamp(currentZoom * factor, 0.3, 2.5);

        if (Math.Abs(newZoom - currentZoom) > 0.001)
        {
            // Adjust PanX and PanY so cursor anchor stays stationary
            _viewModel.PanX = screenPos.X - canvasX * newZoom;
            _viewModel.PanY = screenPos.Y - canvasY * newZoom;
            _viewModel.ZoomLevel = Math.Round(newZoom, 2);

            UpdateCanvasTransform();
            e.Handled = true;
        }
    }

    private void OnCanvasDoubleTapped(object? sender, TappedEventArgs e)
    {
        // Don't fit if clicking on interactive buttons
        if (e.Source is Button || (e.Source is Visual v && v.FindAncestorOfType<Button>() != null))
        {
            return;
        }

        if (_viewModel != null)
        {
            if (ViewportContainer != null && ViewportContainer.Bounds.Width > 50)
            {
                _viewModel.ViewportWidth = ViewportContainer.Bounds.Width;
                _viewModel.ViewportHeight = ViewportContainer.Bounds.Height;
            }
            _viewModel.FitToView();
            e.Handled = true;
        }
    }

    #endregion

    #region Table Card Dragging & Highlighting

    private void OnTableCardPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't drag if clicking buttons inside header
        if (e.Source is Button || (e.Source is Visual v && v.FindAncestorOfType<Button>() != null))
        {
            return;
        }

        if (e.GetCurrentPoint(this).Properties.IsLeftButtonPressed &&
            sender is Visual visual &&
            visual.DataContext is DiagramTableItem table)
        {
            _draggedTable = table;
            _dragStartPointer = e.GetPosition(this);
            _tableStartPos = new Point(table.X, table.Y);
            if (visual is IInputElement inputElem)
            {
                e.Pointer.Capture(inputElem);
            }
            e.Handled = true;
        }
    }

    private void OnTableCardPointerMoved(object? sender, PointerEventArgs e)
    {
        if (_draggedTable != null && _viewModel != null)
        {
            var current = e.GetPosition(this);
            var delta = (current - _dragStartPointer) / _viewModel.ZoomLevel;

            _draggedTable.X = Math.Max(20, _tableStartPos.X + delta.X);
            _draggedTable.Y = Math.Max(20, _tableStartPos.Y + delta.Y);

            _viewModel.RecalculateRelationPoints();
            DiagramSurface?.InvalidateVisual();
            e.Handled = true;
        }
    }

    private void OnTableCardPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (_draggedTable != null)
        {
            _draggedTable = null;
            _viewModel?.SyncPositionsToCache();
            e.Pointer.Capture(null);
            e.Handled = true;
        }
    }

    private void OnTableCardPointerEntered(object? sender, PointerEventArgs e)
    {
        if (sender is Visual visual && visual.DataContext is DiagramTableItem table && _viewModel != null)
        {
            table.IsHighlighted = true;
            foreach (var rel in _viewModel.Relations)
            {
                if (rel.SourceTable == table || rel.TargetTable == table)
                {
                    rel.IsHighlighted = true;
                }
            }
            DiagramSurface?.InvalidateVisual();
        }
    }

    private void OnTableCardPointerExited(object? sender, PointerEventArgs e)
    {
        if (sender is Visual visual && visual.DataContext is DiagramTableItem table && _viewModel != null)
        {
            table.IsHighlighted = false;
            foreach (var rel in _viewModel.Relations)
            {
                if (rel.SourceTable == table || rel.TargetTable == table)
                {
                    rel.IsHighlighted = false;
                }
            }
            DiagramSurface?.InvalidateVisual();
        }
    }

    #endregion
}
