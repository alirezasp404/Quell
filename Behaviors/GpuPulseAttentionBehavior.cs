using System;
using Avalonia;
using Avalonia.Animation;
using Avalonia.Animation.Easings;
using Avalonia.Controls;
using Avalonia.Threading;

namespace Quell.Behaviors;

/// <summary>
/// Provides a subtle, breathing attention pulse (opacity) for status indicators and active notifications.
/// Runs only when hardware GPU motion is active; stays static and zero-overhead in CPU mode.
/// </summary>
public class GpuPulseAttentionBehavior : GpuMotionBehaviorBase<Control>
{
    private DispatcherTimer? _pulseTimer;
    private bool _pulseHigh = true;

    public static readonly StyledProperty<double> MinOpacityProperty =
        AvaloniaProperty.Register<GpuPulseAttentionBehavior, double>(nameof(MinOpacity), 0.50);

    public static readonly StyledProperty<int> CycleDurationMsProperty =
        AvaloniaProperty.Register<GpuPulseAttentionBehavior, int>(nameof(CycleDurationMs), 1600);

    public double MinOpacity
    {
        get => GetValue(MinOpacityProperty);
        set => SetValue(MinOpacityProperty, value);
    }

    public int CycleDurationMs
    {
        get => GetValue(CycleDurationMsProperty);
        set => SetValue(CycleDurationMsProperty, value);
    }

    protected override void OnGpuActivated()
    {
        StartPulse();
    }

    protected override void OnGpuDeactivated()
    {
        StopPulse();
    }

    private void StartPulse()
    {
        StopPulse();
        if (AssociatedObject == null || !IsGpuActive) return;

        // Ensure smooth transition for opacity
        if (AssociatedObject.Transitions == null)
        {
            AssociatedObject.Transitions = new Transitions();
        }

        bool hasOpacityTransition = false;
        foreach (var t in AssociatedObject.Transitions)
        {
            if (t is DoubleTransition dt && dt.Property == Visual.OpacityProperty)
            {
                hasOpacityTransition = true;
                break;
            }
        }

        if (!hasOpacityTransition)
        {
            AssociatedObject.Transitions.Add(new DoubleTransition
            {
                Property = Visual.OpacityProperty,
                Duration = TimeSpan.FromMilliseconds(CycleDurationMs / 2),
                Easing = new SineEaseInOut()
            });
        }

        int halfCycle = Math.Max(200, CycleDurationMs / 2);
        _pulseTimer = new DispatcherTimer
        {
            Interval = TimeSpan.FromMilliseconds(halfCycle)
        };

        _pulseTimer.Tick += (s, e) =>
        {
            if (AssociatedObject != null && IsGpuActive)
            {
                _pulseHigh = !_pulseHigh;
                AssociatedObject.Opacity = _pulseHigh ? 1.0 : MinOpacity;
            }
        };

        _pulseHigh = true;
        AssociatedObject.Opacity = 1.0;
        _pulseTimer.Start();
    }

    private void StopPulse()
    {
        if (_pulseTimer != null)
        {
            _pulseTimer.Stop();
            _pulseTimer = null;
        }

        if (AssociatedObject != null)
        {
            AssociatedObject.Opacity = 1.0;
        }
    }
}
