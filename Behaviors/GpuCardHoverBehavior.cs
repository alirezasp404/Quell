using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media.Transformation;

namespace Quell.Behaviors;

/// <summary>
/// Provides fluid GPU-accelerated hover elevation and spring scale for diagram cards,
/// database table nodes, and interactive canvas items.
/// Cleanly bypassed when running in CPU software mode.
/// </summary>
public class GpuCardHoverBehavior : GpuMotionBehaviorBase<Control>
{
    public static readonly StyledProperty<double> HoverScaleProperty =
        AvaloniaProperty.Register<GpuCardHoverBehavior, double>(nameof(HoverScale), 1.02);

    public static readonly StyledProperty<double> HoverOffsetYProperty =
        AvaloniaProperty.Register<GpuCardHoverBehavior, double>(nameof(HoverOffsetY), -2.5);

    public double HoverScale
    {
        get => GetValue(HoverScaleProperty);
        set => SetValue(HoverScaleProperty, value);
    }

    public double HoverOffsetY
    {
        get => GetValue(HoverOffsetYProperty);
        set => SetValue(HoverOffsetYProperty, value);
    }

    private bool _isHovered;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerEntered += OnPointerEntered;
            AssociatedObject.PointerExited += OnPointerExited;
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PointerEntered -= OnPointerEntered;
            AssociatedObject.PointerExited -= OnPointerExited;
        }
        RestoreOriginalState();
        base.OnDetaching();
    }

    protected override void OnGpuActivated()
    {
        if (AssociatedObject == null) return;

        AssociatedObject.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        if (AssociatedObject.Transitions == null)
        {
            AssociatedObject.Transitions = new Transitions();
        }

        bool hasTransform = false;
        foreach (var t in AssociatedObject.Transitions)
        {
            if (t is TransformOperationsTransition)
            {
                hasTransform = true;
                break;
            }
        }

        if (!hasTransform)
        {
            AssociatedObject.Transitions.Add(new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(160),
                Easing = new CubicEaseOut()
            });
        }
    }

    protected override void OnGpuDeactivated()
    {
        RestoreOriginalState();
    }

    private void RestoreOriginalState()
    {
        if (AssociatedObject == null) return;
        AssociatedObject.RenderTransform = null;
        _isHovered = false;
    }

    private void OnPointerEntered(object? sender, PointerEventArgs e)
    {
        if (!IsGpuActive || AssociatedObject == null) return;
        _isHovered = true;
        string scaleStr = HoverScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string offsetStr = HoverOffsetY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AssociatedObject.RenderTransform = TransformOperations.Parse($"scale({scaleStr}) translateY({offsetStr}px)");
    }

    private void OnPointerExited(object? sender, PointerEventArgs e)
    {
        if (!IsGpuActive || AssociatedObject == null || !_isHovered) return;
        _isHovered = false;
        AssociatedObject.RenderTransform = TransformOperations.Parse("scale(1.0) translateY(0px)");
    }
}
