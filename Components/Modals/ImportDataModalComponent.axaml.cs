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

public partial class ImportDataModalComponent : UserControl
{
    private ImportDataModalViewModel? _viewModel;

    private readonly ScaleTransform _canvasScale = new(1.0, 1.0);
    private readonly TranslateTransform _canvasTranslate = new(30.0, 25.0);

    // Pan state
    private bool _isPanning;
    private Point _panStartPointer;
    private Point _panStartOffset;

    public ImportDataModalComponent()
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

        _viewModel = DataContext as ImportDataModalViewModel;

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
        if (e.PropertyName == nameof(ImportDataModalViewModel.ZoomLevel) ||
            e.PropertyName == nameof(ImportDataModalViewModel.PanX) ||
            e.PropertyName == nameof(ImportDataModalViewModel.PanY))
        {
            UpdateCanvasTransform();
        }
        else if (e.PropertyName == nameof(ImportDataModalViewModel.IsOpen))
        {
            if (_viewModel?.IsOpen == true)
            {
                if (ViewportContainer != null && _viewModel != null)
                {
                    if (ViewportContainer.Bounds.Width > 50) _viewModel.ViewportWidth = ViewportContainer.Bounds.Width;
                    if (ViewportContainer.Bounds.Height > 50) _viewModel.ViewportHeight = ViewportContainer.Bounds.Height;
                }
                UpdateCanvasTransform();
                MappingSurface?.InvalidateVisual();
            }
        }
        else if (e.PropertyName == nameof(ImportDataModalViewModel.ViewMode) ||
                 e.PropertyName == nameof(ImportDataModalViewModel.MappedColumnsCount) ||
                 e.PropertyName == nameof(ImportDataModalViewModel.IncludedColumnsCount))
        {
            UpdateCanvasTransform();
            MappingSurface?.InvalidateVisual();
        }
    }

    private void OnRequestCanvasUpdate()
    {
        UpdateCanvasTransform();
        MappingSurface?.InvalidateVisual();
    }

    private void UpdateCanvasTransform()
    {
        if (_viewModel == null) return;

        _canvasScale.ScaleX = _viewModel.ZoomLevel;
        _canvasScale.ScaleY = _viewModel.ZoomLevel;
        _canvasTranslate.X = _viewModel.PanX;
        _canvasTranslate.Y = _viewModel.PanY;
        MappingSurface?.InvalidateVisual();
    }

    private void OnBackdropPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (_viewModel != null && !_viewModel.IsImporting)
        {
            _viewModel.CloseCommand.Execute(null);
        }
    }

    #region Canvas Pan & Zoom Handlers

    private void OnCanvasPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        // Don't pan if clicking directly on a button, combobox or checkbox
        if (e.Source is Button || e.Source is ComboBox || e.Source is CheckBox ||
            (e.Source is Visual v && (v.FindAncestorOfType<Button>() != null || v.FindAncestorOfType<ComboBox>() != null || v.FindAncestorOfType<CheckBox>() != null)))
        {
            return;
        }

        var props = e.GetCurrentPoint(this).Properties;
        if (props.IsLeftButtonPressed || props.IsMiddleButtonPressed || props.IsRightButtonPressed)
        {
            _isPanning = true;
            _panStartPointer = e.GetPosition(ViewportContainer);
            _panStartOffset = new Point(_viewModel?.PanX ?? 30, _viewModel?.PanY ?? 25);
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
        double newZoom = Math.Clamp(currentZoom * factor, 0.35, 2.5);

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
        _viewModel?.FitToView();
        e.Handled = true;
    }

    #endregion
}
