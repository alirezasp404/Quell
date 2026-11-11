using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Media.Transformation;
using Avalonia.Threading;

namespace Quell.Behaviors;

/// <summary>
/// Provides a sleek GPU-accelerated slide-and-fade entrance animation for dropdown overlays,
/// SQL IntelliSense popups, and floating tool windows.
/// Bypassed in CPU mode.
/// </summary>
public class GpuSlideEntranceBehavior : GpuMotionBehaviorBase<Control>
{
    public static readonly StyledProperty<double> StartOffsetYProperty =
        AvaloniaProperty.Register<GpuSlideEntranceBehavior, double>(nameof(StartOffsetY), -10.0);

    public static readonly StyledProperty<int> DurationMsProperty =
        AvaloniaProperty.Register<GpuSlideEntranceBehavior, int>(nameof(DurationMs), 180);

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
                TriggerSlideAnimation();
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
            TriggerSlideAnimation();
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
                TriggerSlideAnimation();
            }
        }
    }

    private void TriggerSlideAnimation()
    {
        if (AssociatedObject == null || !IsGpuActive) return;

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
                Easing = new CubicEaseOut()
            });
        }

        if (!hasOpacity)
        {
            AssociatedObject.Transitions.Add(new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(DurationMs),
                Easing = new CubicEaseOut()
            });
        }

        var transitions = AssociatedObject.Transitions;
        AssociatedObject.Transitions = null;
        AssociatedObject.Opacity = 0.0;
        string offsetStr = StartOffsetY.ToString(System.Globalization.CultureInfo.InvariantCulture);
        AssociatedObject.RenderTransform = TransformOperations.Parse($"translateY({offsetStr}px)");

        Dispatcher.UIThread.Post(() =>
        {
            if (AssociatedObject != null && IsGpuActive)
            {
                AssociatedObject.Transitions = transitions;
                AssociatedObject.Opacity = 1.0;
                AssociatedObject.RenderTransform = TransformOperations.Parse("translateY(0px)");
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
