---
name: quell-design-system
description: >-
  Complete UI design system, component primitive catalog, Light/Dark & Color Maker theme compatibility rules,
  tab/segmented-control patterns, and GPU-vs-CPU rendering performance architecture for Quell.
  Use whenever creating, refactoring, or styling any view, modal, tab strip, segmented filter, button, card,
  or animation in Quell so the entire application stays visually unified, theme-adaptive, and lightweight on CPU.
---

# Quell Design System Skill

This skill is the canonical specification for every visual element, interactive control, layout container, and animation across **Quell** (Avalonia UI + FluentAvalonia).

Every component MUST follow these rules so the entire application feels cohesive across Light Mode, Dark Mode, runtime Color Maker overrides, GPU hardware acceleration, and lightweight CPU software rasterization.

---

## 1. Core Architectural Laws

1. **Theme-First Dynamic Tokens (`{DynamicResource App*Brush}`):**
   - **Never** hardcode hex backgrounds, text colors, or borders (e.g., `#1E293B`, `#FFFFFF`, `#4F6FFF`) on standard surfaces, chrome, cards, inputs, or buttons.
   - **Always** bind via `{DynamicResource App*Brush}` defined symmetrically in both `Themes/LightTheme.axaml` and `Themes/DarkTheme.axaml` (plus shared metrics/radii in `Themes/ThemeTokens.axaml`).
   - Preserve all keys managed by [`quell-customization-system`](../quell-customization-system/SKILL.md) so Color Maker live previews and `"Default"` resource fallbacks work identically on every component.
2. **GPU Animations vs. Lightweight CPU Rendering (`HardwareGraphicsService`):**
   - `MainWindow` toggles the CSS class `.gpu-mode` on the `TopLevel` whenever `HardwareGraphicsService.Instance.IsGpuMotionActive` changes.
   - **Animations belong strictly on the GPU:** All `<Transitions>`, hover lifts, spring modal entrances, attention pulses, and keyframe animations MUST be gated behind `:is(TopLevel).gpu-mode ...` or subclasses of `GpuMotionBehaviorBase<T>` (`GpuButtonPressBehavior`, `GpuModalEntranceBehavior`, `GpuCardHoverBehavior`, `GpuSlideEntranceBehavior`, `GpuPulseAttentionBehavior`).
   - **CPU mode must stay featherlight:** Under `:is(TopLevel):not(.gpu-mode)`, transitions are disabled, `RenderTransform` states snap instantly without allocation, infinite keyframe loops stop, and large blur `BoxShadow`s flatten to `0 0 0 0 #00000000` so the Skia software rasterizer uses minimal CPU and memory.
   - See [GPU & CPU Motion Reference](references/gpu_cpu_motion.md).
3. **Zero Black Corners on Popups, Menus, Tooltips & Flyouts:**
   - Every popup/flyout/tooltip container MUST combine:
     - `WindowManagerAddShadowHint="False"`
     - `ShouldUseOverlayLayer="True"` (paired with platform `OverlayPopups = true` in `HardwareGraphicsService`)
     - `ClipToBounds="True"` paired with explicit `Background="{DynamicResource AppMenuBackgroundBrush}"` (or `AppSurfaceBrush`) and `BorderBrush="{DynamicResource AppBorderBrush}"` (`CornerRadius="6"`–`"10"`).
4. **Zero Emojis in AXAML:**
   - Never use Unicode emojis (`🔧`, `📊`, `⚠`, `✅`). Use only vector icons from `FluentIcons.Avalonia` (`ic:SymbolIcon`), `FluentAvalonia.UI.Controls` (`ui:FASymbolIcon`), or crisp `PathIcon` vectors inheriting `Foreground`.
5. **Async UI Responsiveness & Shadow-Free Loading:**
   - All DB execution, metadata reflection, and SQLite reads run off the UI thread (`Task.Run` / true async I/O).
   - Long-running panels (such as Query Results) show a **shadow-free** overlay (`BoxShadow` omitted, `1px` `AppBorderBrush`, indeterminate `ProgressBar`, elapsed counter, and `Cancel` command) before awaiting background work.

---

## 2. Theme & Color Maker Compatibility Rules

When adding or refactoring any component, verify it passes all 4 theme states:
1. **Light Theme Baseline** (`Themes/LightTheme.axaml`) — Crisp `#FFFFFF` / `#F8FAFC` surfaces, `#0F172A` primary text, `#2563EB` accent.
2. **Dark Theme Baseline** (`Themes/DarkTheme.axaml`) — Deep slate `#0F1115` workspace, `#14171D` sidebar drawer, `#12151B` rail/top bar, `#E6E8EC` text, `#4C7DFF` accent.
3. **Color Maker Live Override** (`ThemeService.SetOrRemoveBrushOverride`) — Components using `AppAccentBrush`, `AppButtonBackgroundBrush`, `AppButtonBorderBrush`, `AppBorderBrush`, `AppSurfaceBrush`, `AppSidebarBackgroundBrush`, `AppTable*Brush`, `AppEditorBackgroundBrush`, and `AppTerminal*Brush` adapt immediately with zero restart.
4. **Contrast Rule:** Never pair hardcoded dark text (`#000000`) or hardcoded white (`#FFFFFF`) against theme-variable surfaces (`AppChipActiveBackgroundBrush`, `AppButtonBackgroundBrush`). Always pair matching tokens:
   - Accent CTA fills (`AppAccentBrush` / `AppPrimaryButtonBackgroundBrush`) -> `AppPrimaryButtonForegroundBrush` (`White`)
   - Soft chip / segmented active fills (`AppChipActiveBackgroundBrush`) -> `AppChipActiveForegroundBrush`
   - Neutral button fills (`AppButtonBackgroundBrush`) -> `AppButtonForegroundBrush`
   - Status badges (`AppSuccess*Brush`, `AppWarning*Brush`, `AppDanger*Brush`) -> matching Background + Border + Foreground triad in both themes.

---

## 3. Core Token & Geometry Hierarchy

Defined across `Themes/ThemeTokens.axaml`, `Themes/LightTheme.axaml`, and `Themes/DarkTheme.axaml`:

| Category | Token / Resource | Usage |
| :--- | :--- | :--- |
| **Radii** | `6` (`sm`), `8` (`md`), `10–12` (`lg`), `999` (`pill`) | Rows/Buttons (`6`), Inputs/Cards/Segmented Track (`8`), Modals/Drawers (`10–12`), Badges/Toggles (`999`) |
| **Surfaces** | `AppWorkspaceBrush`, `AppSurfaceBrush`, `AppSurfaceMutedBrush`, `AppCardBackgroundBrush`, `AppInputBackgroundBrush` | Layered elevation hierarchy (Workspace < Surface < Card < Elevated Menu) |
| **Chrome** | `AppTopBarBackgroundBrush`, `AppRailBackgroundBrush`, `AppSidebarBackgroundBrush`, `AppSidebarHeaderBackgroundBrush` | Top bar (`#12151B` dark), 56px Rail, Sidebar drawer (`#14171D` dark) |
| **Segmented Controls** | `AppSegmentedTrackBrush`, `AppSegmentedTrackBorderBrush`, `AppSegmentedItemActiveBrush`, `AppSegmentedItemActiveBorderBrush` | Unified segmented tab bars (`Tables / Views / Procs`, `All / Failed / This DB`, Settings toggles) |
| **Status Triads** | `AppSuccess*Brush`, `AppWarning*Brush`, `AppDanger*Brush`, `AppStatusActiveBrush`, `AppStatusDeletedBrush` | Semantic emerald OK (`#3DD68C`/`#059669`), rose Failed/Danger (`#F2677A`/`#DC2626`), amber Warning (`#F5A623`/`#D97706`) |
| **Star / Pin** | `AppStarGoldBrush` | Pinned Saved Queries icon (`#E5C07B` dark / `#D97706` light) |

---

## 4. Standardized Component Primitives (`Resources/ClassicStyles.axaml`)

Every view and modal MUST compose using these shared style classes and the reusable `UserControl` catalog in [COMPONENTS.md](../../../COMPONENTS.md) (via the [`quell-components`](../quell-components/SKILL.md) skill) instead of ad-hoc inline button/border styling. Full markup patterns are in [Tabs, Controls & Modals Reference](references/tabs_and_controls.md).

### 4.1 Segmented Tab Controls (`quell-segmented-track` + `quell-segmented-tab`)
Used for compact switchable views & filters such as **`Tables | Views | Procs`**, **`All | Failed | This DB`**, **`Dark | Light | System`**, and **`Canvas | List`**.
- **Never** dump category tabs into a wrapping `WrapPanel` of loose outlined ovals.
- Wrap options in an inset `Border.quell-segmented-track` (`CornerRadius="8"`, `Padding="3"`, `Background="{DynamicResource AppSegmentedTrackBrush}"`, `BorderBrush="{DynamicResource AppSegmentedTrackBorderBrush}"`) containing an equal-column `UniformGrid Rows="1"`.
- Each item uses `Button.quell-segmented-tab` with `Classes.active="{Binding ...}"` and displays an elevated active pill (`AppSegmentedItemActiveBrush` + `AppChipActiveForegroundBrush` + optional `1px` `AppSegmentedItemActiveBorderBrush` or accent dot/bar).
- Companion scope filters (e.g., **`Active DB`**) sit either cleanly aligned in a dedicated single-row bar (`Grid ColumnDefinitions="*, Auto"` or crisp status chip `ToggleButton.quell-filter-toggle`) so nothing wraps awkwardly onto an orphaned line.

### 4.2 Document & Panel Tab Strips (`quell-doc-tab` & `footer-tab-btn`)
Used in `EditorTabStripComponent` and `FooterComponent`:
- Sleek folder/underline hybrid pill tab (`CornerRadius="7,7,0,0"`, `Padding="10,5"`), bottom/top `2px` accent bar inside `.active`, integrated close button (`quell-tab-close-btn`), dirty/active dot (`#4C7DFF` via `AppAccentBrush`), smooth GPU hover transition, instant CPU snap.

### 4.3 Modal Dialog Chrome (`modal-backdrop` + `modal-dialog-card`)
All modals (`ConfirmAction`, `ConnectDatabase`, `DatabaseDiagram`, `EditCell`, `ExportData`, `ForeignKeyInspector`, `ImportData`, `ModalOverlay`, `SettingsModal`, `UpdateModal`) follow ONE layout:
- Outer `Grid` -> `Border.modal-backdrop` -> `Border.modal-dialog-card` (`CornerRadius="10"`, `ClipToBounds="True"`, `Background="{DynamicResource AppWorkspaceBrush}"`, `BorderBrush="{DynamicResource AppBorderBrush}"`, `BorderThickness="1"`).
- **No hardcoded `BoxShadow` attribute on `Border.modal-dialog-card` in component XAML!** `ClassicStyles.axaml` applies `BoxShadow` exclusively under `:is(TopLevel).gpu-mode Border.modal-dialog-card` and zeros it under `:is(TopLevel):not(.gpu-mode)`!
- Every modal attaches `<b:GpuModalEntranceBehavior StartScale="0.94" DurationMs="220" />` so modal opening is animated on GPU and instant on CPU.
- Uniform Header (`AppSurfaceBrush`, `1px` bottom border, icon badge + bold title + subtitle + `panel-icon-btn` Close `×`) and Footer (`AppSurfaceBrush`, `1px` top border, right-aligned `quell-btn-secondary` Cancel + `quell-btn-primary` / `quell-btn-danger` Confirm).

### 4.4 Buttons & Inputs
- `Button.quell-btn-primary` — Accent CTA (`Run (F5)`, `Save & Apply`, `Run again`) + `<b:GpuButtonPressBehavior/>`.
- `Button.classic-tool-btn` / `Button.quell-btn-secondary` — Secondary outlined actions (`New Query`, `Cancel`, `Open in tab`).
- `Button.quell-btn-danger` — Destructive confirmations (`Truncate`, `Clear All`, `Delete`).
- `Button.classic-menu-btn` / `Button.panel-icon-btn` / `Button.row-inline-btn` — Chromeless ghost/icon buttons (`28x28` header icons, `22x22` inline actions).
- `Border.quell-input-box` — Unified search/text input shell (`Height="34"`, `CornerRadius="8"`, `AppInputBackgroundBrush`, `1px` `AppBorderBrush`, `:pointerover` / `:focus-within` highlight on GPU).

---

## 5. Checklist Before Shipping Any UI Change

1. **Theme Check:** Verify Light Theme, Dark Theme, and at least one Color Maker override (`AppAccentBrush`, `AppSurfaceBrush`, `AppBorderBrush`) render cleanly with high legibility.
2. **GPU vs CPU Check:**
   - Ensure no bare `Transitions` or keyframe `<Animation>` blocks run outside `:is(TopLevel).gpu-mode` or `GpuMotionBehaviorBase<T>`.
   - Ensure no heavy `BoxShadow` exists on modal cards/overlays in CPU mode (and ZERO `BoxShadow` on query loading overlay).
3. **Zero Regression Build & Verification:**
   ```powershell
   dotnet build Quell.csproj --configuration Release
   dotnet run --configuration Release --no-build -- --verify
   ```
   (Also smoke-test `HardwareGraphicsService` switching between `GPU` and `CPU` modes in `PerformanceSettingsComponent`.)
