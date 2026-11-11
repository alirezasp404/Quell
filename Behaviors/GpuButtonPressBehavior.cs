using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media.Transformation;

namespace Quell.Behaviors;

/// <summary>
/// Provides a tactile, spring-like button press compression animation.
/// Seamlessly runs on the GPU when hardware acceleration is active,
/// and is completely bypassed in CPU mode.
/// </summary>
public class GpuButtonPressBehavior : GpuMotionBehaviorBase<Button>
{
    public static readonly StyledProperty<double> PressScaleProperty =
        AvaloniaProperty.Register<GpuButtonPressBehavior, double>(nameof(PressScale), 0.94);

    public double PressScale
    {
        get => GetValue(PressScaleProperty);
        set => SetValue(PressScaleProperty, value);
    }

    private bool _isPressed;

    protected override void OnAttached()
    {
        base.OnAttached();
        if (AssociatedObject != null)
        {
            AssociatedObject.AddHandler(InputElement.PointerPressedEvent, OnPointerPressed, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            AssociatedObject.AddHandler(InputElement.PointerReleasedEvent, OnPointerReleased, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
            AssociatedObject.AddHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost, RoutingStrategies.Tunnel | RoutingStrategies.Bubble);
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.RemoveHandler(InputElement.PointerPressedEvent, OnPointerPressed);
            AssociatedObject.RemoveHandler(InputElement.PointerReleasedEvent, OnPointerReleased);
            AssociatedObject.RemoveHandler(InputElement.PointerCaptureLostEvent, OnPointerCaptureLost);
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
            AssociatedObject.Transitions = new Transitions
            {
                new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(120),
                    Easing = new CubicEaseOut()
                }
            };
        }
        else
        {
            bool hasTransformTransition = false;
            foreach (var t in AssociatedObject.Transitions)
            {
                if (t is TransformOperationsTransition)
                {
                    hasTransformTransition = true;
                    break;
                }
            }

            if (!hasTransformTransition)
            {
                AssociatedObject.Transitions.Add(new TransformOperationsTransition
                {
                    Property = Visual.RenderTransformProperty,
                    Duration = TimeSpan.FromMilliseconds(120),
                    Easing = new CubicEaseOut()
                });
            }
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
        _isPressed = false;
    }

    private void OnPointerPressed(object? sender, PointerPressedEventArgs e)
    {
        if (!IsGpuActive || AssociatedObject == null) return;
        _isPressed = true;
        AssociatedObject.RenderTransform = TransformOperations.Parse($"scale({PressScale.ToString(System.Globalization.CultureInfo.InvariantCulture)})");
    }

    private void OnPointerReleased(object? sender, PointerReleasedEventArgs e)
    {
        if (!IsGpuActive || AssociatedObject == null || !_isPressed) return;
        _isPressed = false;
        AssociatedObject.RenderTransform = TransformOperations.Parse("scale(1.0)");
    }

    private void OnPointerCaptureLost(object? sender, PointerCaptureLostEventArgs e)
    {
        if (!IsGpuActive || AssociatedObject == null || !_isPressed) return;
        _isPressed = false;
        AssociatedObject.RenderTransform = TransformOperations.Parse("scale(1.0)");
    }
}
