using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace Quell.Behaviors;

/// <summary>
/// Provides a fluid, spring-like GPU-accelerated entrance animation for modal dialog cards and popup windows.
/// When hardware GPU is detected and active, the dialog scales smoothly from 0.92 to 1.0 with a soft spring ease,
/// while fading in opacity. In CPU mode, animations are completely bypassed to save compute cycles.
/// </summary>
public class GpuModalEntranceBehavior : GpuMotionBehaviorBase<Control>
{
    public static readonly StyledProperty<double> StartScaleProperty =
        AvaloniaProperty.Register<GpuModalEntranceBehavior, double>(nameof(StartScale), 0.92);

    public static readonly StyledProperty<double> StartOffsetYProperty =
        AvaloniaProperty.Register<GpuModalEntranceBehavior, double>(nameof(StartOffsetY), 14.0);

    public static readonly StyledProperty<int> DurationMsProperty =
        AvaloniaProperty.Register<GpuModalEntranceBehavior, int>(nameof(DurationMs), 240);

    public double StartScale
    {
        get => GetValue(StartScaleProperty);
        set => SetValue(StartScaleProperty, value);
    }

    public double StartOffsetY
    {
        get => GetValue(StartOffsetYProperty);
        set => SetValue(StartOffsetYProperty, value);
    }

    public int DurationMs
    {
        get => GetValue(DurationMsProperty);
        set => SetValue(DurationMsProperty, value);
    }

    protected override void OnAttached()
    {
        base.OnAttached();

        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged += OnAssociatedObjectPropertyChanged;
            if (AssociatedObject.IsVisible && IsGpuActive)
            {
                TriggerEntranceAnimation();
            }
        }
    }

    protected override void OnDetaching()
    {
        if (AssociatedObject != null)
        {
            AssociatedObject.PropertyChanged -= OnAssociatedObjectPropertyChanged;
        }

        RestoreOriginalState();
        base.OnDetaching();
    }

    protected override void OnGpuActivated()
    {
        if (AssociatedObject != null && AssociatedObject.IsVisible)
        {
            TriggerEntranceAnimation();
        }
    }

    protected override void OnGpuDeactivated()
    {
        RestoreOriginalState();
    }

    private void OnAssociatedObjectPropertyChanged(object? sender, AvaloniaPropertyChangedEventArgs e)
    {
        if (e.Property == Visual.IsVisibleProperty && e.NewValue is bool isVisible)
        {
            if (isVisible && IsGpuActive)
            {
                TriggerEntranceAnimation();
            }
        }
    }

    private void TriggerEntranceAnimation()
    {
        if (AssociatedObject == null || !IsGpuActive) return;

        AssociatedObject.RenderTransformOrigin = new RelativePoint(0.5, 0.5, RelativeUnit.Relative);

        // Ensure Transitions are present
        if (AssociatedObject.Transitions == null)
        {
            AssociatedObject.Transitions = new Transitions();
        }

        bool hasTransform = false;
        bool hasOpacity = false;
        foreach (var t in AssociatedObject.Transitions)
        {
            if (t is TransformOperationsTransition) hasTransform = true;
            if (t is DoubleTransition dt && dt.Property == Visual.OpacityProperty) hasOpacity = true;
        }

        if (!hasTransform)
        {
            AssociatedObject.Transitions.Add(new TransformOperationsTransition
            {
                Property = Visual.RenderTransformProperty,
                Duration = TimeSpan.FromMilliseconds(DurationMs),
                Easing = new BackEaseOut()
            });
        }

        if (!hasOpacity)
        {
            AssociatedObject.Transitions.Add(new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(DurationMs - 40),
                Easing = new CubicEaseOut()
            });
        }

        // Set initial state without transitions (instant)
        var transitions = AssociatedObject.Transitions;
        AssociatedObject.Transitions = null;
        AssociatedObject.Opacity = 0.0;
        string scaleStr = StartScale.ToString(System.Globalization.CultureInfo.InvariantCulture);
        string offsetStr = StartOffsetY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AssociatedObject.RenderTransform = TransformOperations.Parse($"scale({scaleStr}) translateY({offsetStr}px)");

        // Post on next frame to trigger animated transition to target state
        Dispatcher.UIThread.Post(() =>
        {
            if (AssociatedObject != null && IsGpuActive)
            {
                AssociatedObject.Transitions = transitions;
                AssociatedObject.Opacity = 1.0;
                AssociatedObject.RenderTransform = TransformOperations.Parse("scale(1.0) translateY(0px)");
            }
        }, DispatcherPriority.Render);
    }

    private void RestoreOriginalState()
    {
        if (AssociatedObject == null) return;
        AssociatedObject.RenderTransform = null;
        AssociatedObject.Opacity = 1.0;
    }
}
