using System;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Threading;
using Avalonia.Xaml.Interactivity;
using Quell.Services;

namespace Quell.Behaviors;

/// <summary>
/// Base class for all GPU-accelerated XAML behaviors.
/// Seamlessly activates rich hardware-rendered animations when GPU mode is active,
/// and cleanly disables them in CPU mode to preserve system resources.
/// </summary>
public abstract class GpuMotionBehaviorBase<T> : Behavior<T> where T : Control
{
    private EventHandler? _renderingModeHandler;

    public bool IsGpuActive => HardwareGraphicsService.Instance.IsGpuMotionActive;

    protected override void OnAttached()
    {
        base.OnAttached();

        _renderingModeHandler = (s, e) =>
        {
            Dispatcher.UIThread.Post(() =>
            {
                if (AssociatedObject != null)
                {
                    OnRenderingModeChanged(IsGpuActive);
                }
            });
        };

        HardwareGraphicsService.Instance.RenderingModeChanged += _renderingModeHandler;

        if (IsGpuActive)
        {
            if (Dispatcher.UIThread.CheckAccess())
            {
                OnGpuActivated();
            }
            else
            {
                Dispatcher.UIThread.Post(OnGpuActivated);
            }
        }
    }

    protected override void OnDetaching()
    {
        if (_renderingModeHandler != null)
        {
            HardwareGraphicsService.Instance.RenderingModeChanged -= _renderingModeHandler;
            _renderingModeHandler = null;
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            OnGpuDeactivated();
        }
        else
        {
            Dispatcher.UIThread.Post(OnGpuDeactivated);
        }

        base.OnDetaching();
    }

    private void OnRenderingModeChanged(bool isGpu)
    {
        if (isGpu)
        {
            OnGpuActivated();
        }
        else
        {
            OnGpuDeactivated();
        }
    }

    /// <summary>
    /// Invoked on the UI thread when GPU motion is active (or becomes active via Settings).
    /// </summary>
    protected virtual void OnGpuActivated() { }

    /// <summary>
    /// Invoked on the UI thread when GPU motion is deactivated (or in CPU mode) to clean up transforms/animations.
    /// </summary>
    protected virtual void OnGpuDeactivated() { }
}
