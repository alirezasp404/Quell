# GPU vs CPU Rendering & Motion Infrastructure

Quell detects hardware graphics adapters at startup in `Services/HardwareGraphicsService.cs` and lets users choose between `Auto`, `GPU`, and `CPU` modes (persisted in SQLite `AppSettings` under `GraphicsRenderingMode`).

---

## 1. Runtime Architecture

```
HardwareGraphicsService.Instance
 ├── EffectiveRenderingMode == "GPU"  (IsGpuMotionActive = true)
 │    ├── Win32: AngleEgl (DX11) -> Wgl -> Software, LowLatencyDxgiSwapChain
 │    ├── Skia VRAM Cache: 128 MB (MaxGpuResourceSizeBytes)
 │    ├── MainWindow.Classes adds "gpu-mode"
 │    └── All GpuMotionBehaviorBase<T> behaviors activate OnGpuActivated()
 │
 └── EffectiveRenderingMode == "CPU"  (IsGpuMotionActive = false)
      ├── Win32: Software rasterizer only (OverlayPopups = true)
      ├── Skia Memory Buffer: 16 MB
      ├── MainWindow.Classes removes "gpu-mode"
      └── All GpuMotionBehaviorBase<T> behaviors call OnGpuDeactivated() & clear RenderTransform
```

Live changes in Settings (`PerformanceSettingsComponent`) call `HardwareGraphicsService.Instance.NotifyRenderingModeChanged()`, which immediately invokes `UpdateGpuModeClass()` on `MainWindow` and `OnRenderingModeChanged(isGpu)` on every attached behavior without requiring a restart.

---

## 2. XAML Styling Rules (GPU vs CPU)

### Rule A: Gate CSS Transitions and Keyframe Animations Behind `:is(TopLevel).gpu-mode`
Never attach `<Transitions>` or `<Style.Animations>` to un-scoped selectors like `Button.my-btn` or `Border.spinner`. Always scope them:

```xml
<!-- GPU Mode: Smooth 60+ fps hardware-composited transitions -->
<Style Selector=":is(TopLevel).gpu-mode Button.quell-segmented-tab">
    <Setter Property="Transitions">
        <Transitions>
            <BrushTransition Property="Background" Duration="0:0:0.16"/>
            <BrushTransition Property="BorderBrush" Duration="0:0:0.16"/>
            <TransformOperationsTransition Property="RenderTransform" Duration="0:0:0.12" Easing="CubicEaseOut"/>
        </Transitions>
    </Setter>
</Style>
<Style Selector=":is(TopLevel).gpu-mode Button.quell-segmented-tab:pressed">
    <Setter Property="RenderTransform" Value="scale(0.96)"/>
</Style>

<!-- CPU Mode: Zero transitions, zero RenderTransform allocation -->
<Style Selector=":is(TopLevel):not(.gpu-mode) Button.quell-segmented-tab">
    <Setter Property="Transitions" Value="{x:Null}"/>
</Style>
```

### Rule B: Never Hardcode Expensive Gaussian Drop Shadows on Dynamic Modals/Cards
Software Gaussian blur (`BoxShadow="0 16 40 0 #48000000"`) is one of the highest CPU sinks per repaint.
Instead:
- Omit `BoxShadow="..."` from modal/card elements in component `.axaml`.
- Define `BoxShadow` ONLY inside `:is(TopLevel).gpu-mode` in `Resources/ClassicStyles.axaml`, and flatten it under `:is(TopLevel):not(.gpu-mode)`:

```xml
<Style Selector=":is(TopLevel).gpu-mode Border.modal-dialog-card">
    <Setter Property="BoxShadow" Value="0 16 40 0 #48000000"/>
</Style>
<Style Selector=":is(TopLevel):not(.gpu-mode) Border.modal-dialog-card">
    <Setter Property="BoxShadow" Value="0 0 0 0 #00000000"/>
</Style>
```

> Note: Query Result loading card (`QueryLoadingCardBorder`) MUST NEVER have any `BoxShadow` in either GPU or CPU mode.

### Rule C: Infinite Keyframe Spinners Must Be GPU-Only
Continuous infinite animations (`IterationCount="Infinite"`) drain CPU if active in software mode. Scope infinite spinners to `:is(TopLevel).gpu-mode`:

```xml
<Style Selector=":is(TopLevel).gpu-mode Border.spin-indicator">
    <Style.Animations>
        <Animation Duration="0:0:1.0" IterationCount="Infinite">
            <KeyFrame Cue="0%"><Setter Property="RotateTransform.Angle" Value="0.0"/></KeyFrame>
            <KeyFrame Cue="100%"><Setter Property="RotateTransform.Angle" Value="360.0"/></KeyFrame>
        </Animation>
    </Style.Animations>
</Style>
```

---

## 3. Built-in Behavior Library (`Quell.Behaviors`)

Prefer declarative `i:Interaction.Behaviors` using Quell's `GpuMotionBehaviorBase<T>` classes over code-behind animations:

| Behavior | Target | GPU Behavior | CPU Behavior |
| :--- | :--- | :--- | :--- |
| `GpuButtonPressBehavior` | `Button` | Scales down (`PressScale`, default `0.94`) with `120ms CubicEaseOut` | Completely bypassed; `RenderTransform = null` |
| `GpuCardHoverBehavior` | `Control` | Lifts card (`HoverScale=1.02`, `HoverOffsetY=-2.5`) on pointerover | Bypassed; zero pointerover transforms |
| `GpuModalEntranceBehavior` | `Control` | Spring pop (`StartScale=0.92`, `StartOffsetY=14`, `240ms BackEaseOut`) on `IsVisible=true` | Appears immediately at full scale/opacity |
| `GpuSlideEntranceBehavior` | `Control` | Slide-and-fade entrance (`180ms CubicEaseOut`) for dropdowns/IntelliSense | Instant show with zero transition |
| `GpuPulseAttentionBehavior` | `Control` | Soft repeating status dot pulse (`CycleDurationMs`) | Static full opacity; zero timer ticks |
