# Tabs, Segmented Controls, Buttons & Modal Blueprints

---

## 1. Segmented Tab Bar Pattern (`Tables | Views | Procs` / `All | Failed | This DB`)

Never render category tabs as wrapping bordered ovals in a `WrapPanel`. Always use an inset segmented track (`quell-segmented-track`) with equal-width `quell-segmented-tab` buttons:

```xml
<!-- Segmented 3-Tab Category Strip + Companion Filter Toggle -->
<Grid RowDefinitions="Auto, Auto" RowSpacing="6" Margin="12,0,12,8">
    <Border Grid.Row="0" Classes="quell-segmented-track">
        <UniformGrid Rows="1" Columns="3">
            <Button Classes="quell-segmented-tab"
                    Classes.active="{Binding IsTablesCategory}"
                    Command="{Binding SelectExplorerCategoryCommand}"
                    CommandParameter="Tables">
                <StackPanel Orientation="Horizontal" Spacing="5" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <ic:SymbolIcon Classes="seg-icon" Symbol="Table" FontSize="12"/>
                    <TextBlock Classes="seg-label" Text="Tables"/>
                </StackPanel>
            </Button>
            <Button Classes="quell-segmented-tab"
                    Classes.active="{Binding IsViewsCategory}"
                    Command="{Binding SelectExplorerCategoryCommand}"
                    CommandParameter="Views">
                <StackPanel Orientation="Horizontal" Spacing="5" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <ic:SymbolIcon Classes="seg-icon" Symbol="LayerDiagonal" FontSize="12"/>
                    <TextBlock Classes="seg-label" Text="Views"/>
                </StackPanel>
            </Button>
            <Button Classes="quell-segmented-tab"
                    Classes.active="{Binding IsProcsCategory}"
                    Command="{Binding SelectExplorerCategoryCommand}"
                    CommandParameter="Procs">
                <StackPanel Orientation="Horizontal" Spacing="5" HorizontalAlignment="Center" VerticalAlignment="Center">
                    <ic:SymbolIcon Classes="seg-icon" Symbol="Code" FontSize="12"/>
                    <TextBlock Classes="seg-label" Text="Procs"/>
                </StackPanel>
            </Button>
        </UniformGrid>
    </Border>

    <!-- Companion Active DB Scope Row (never crowds tabs onto wrapped ovals) -->
    <Grid Grid.Row="1" ColumnDefinitions="*, Auto" IsVisible="{Binding IsConnected}">
        <TextBlock Grid.Column="0"
                   Text="{Binding StatusText}"
                   FontSize="11"
                   Foreground="{DynamicResource AppTextMutedBrush}"
                   VerticalAlignment="Center"
                   TextTrimming="CharacterEllipsis"/>
        <ToggleButton Grid.Column="1"
                      Classes="quell-filter-toggle"
                      IsChecked="{Binding FilterActiveOnly}"
                      ToolTip.Tip="Show only the currently active connected database">
            <StackPanel Orientation="Horizontal" Spacing="6" VerticalAlignment="Center">
                <Border Width="6" Height="6" CornerRadius="3" Background="{DynamicResource AppStatusActiveBrush}"/>
                <TextBlock Text="Active DB" FontSize="11" VerticalAlignment="Center"/>
            </StackPanel>
        </ToggleButton>
    </Grid>
</Grid>
```

### Visual Behavior of `.quell-segmented-tab`:
- **Track (`quell-segmented-track`):** Inset container (`Background="{DynamicResource AppSegmentedTrackBrush}"`, `BorderBrush="{DynamicResource AppSegmentedTrackBorderBrush}"`, `BorderThickness="1"`, `CornerRadius="8"`, `Padding="3"`).
- **Inactive Tab:** Transparent fill, `AppTextMutedBrush` label & icon. On `:pointerover`, subtle `AppSidebarHoverBrush` fill + `AppTextPrimaryBrush`.
- **Active Tab (`.active`):** Elevated pill (`Background="{DynamicResource AppSegmentedItemActiveBrush}"`, `BorderBrush="{DynamicResource AppSegmentedItemActiveBorderBrush}"`, `BorderThickness="1"`, `CornerRadius="6"`), `AppChipActiveForegroundBrush` bold/semi-bold text + accent icon, plus subtle top/bottom micro-accent bar or elevated fill contrast in both Light and Dark themes.

---

## 2. Document & Panel Tabs (`EditorTabStripComponent` & `FooterComponent`)

- Use `CornerRadius="7,7,0,0"` with a flush bottom edge meeting the editor/panel border.
- Include a clean **top accent indicator strip** (`Height="2"`, `CornerRadius="2"`, `Background="{DynamicResource AppAccentBrush}"`, visible only when `.active`).
- Close `×` buttons inside tabs always use `Classes="classic-menu-btn quell-tab-close-btn"` (`Width="18"`, `Height="18"`, `CornerRadius="4"`, `Padding="0"`).

---

## 3. Unified Modal Blueprint

All modals (`420px` confirm up to `1100px` diagram/import) follow this exact skeleton:

```xml
<Grid IsVisible="{Binding IsOpen}">
    <Border Classes="modal-backdrop"
            Background="{DynamicResource AppModalOverlayBrush}"
            PointerPressed="OnBackdropPointerPressed"/>

    <Border Classes="modal-dialog-card"
            Width="520"
            HorizontalAlignment="Center"
            VerticalAlignment="Center">
        <i:Interaction.Behaviors>
            <b:GpuModalEntranceBehavior StartScale="0.94" DurationMs="220" />
        </i:Interaction.Behaviors>

        <Grid RowDefinitions="Auto, *, Auto">
            <!-- 1. Modal Header -->
            <Border Grid.Row="0" Classes="quell-modal-header">
                <!-- Leading Icon Badge (30x30 CornerRadius=8) + Title/Subtitle + Dismiss panel-icon-btn -->
            </Border>

            <!-- 2. Modal Body -->
            <Border Grid.Row="1" Padding="20,16">
                <!-- Content -->
            </Border>

            <!-- 3. Modal Footer -->
            <Border Grid.Row="2" Classes="quell-modal-footer">
                <!-- Left optional secondary action | Right primary CTA (`quell-btn-primary` / `quell-btn-danger`) + Cancel (`classic-tool-btn`) -->
            </Border>
        </Grid>
    </Border>
</Grid>
```

---

## 4. Button Class Inventory

| Class | Purpose | Visual Style |
| :--- | :--- | :--- |
| `Button.quell-btn-primary` | Main positive CTA (`Run`, `Save`, `Run again`, `Apply`) | `AppPrimaryButtonBackgroundBrush` (`AppAccentBrush`), white foreground, `CornerRadius="7"`, GPU press scale |
| `Button.classic-tool-btn` | Secondary / toolbar actions (`Cancel`, `Export`, `Open in tab`) | `AppButtonBackgroundBrush`, `1px AppButtonBorderBrush`, `CornerRadius="7"` |
| `Button.quell-btn-danger` | Destructive confirm (`Truncate`, `Clear All`, `Delete`) | `#E11D48` / `#F2677A` alert surface (`AppDangerBrush`), white foreground |
| `Button.classic-menu-btn` | Ghost buttons in strips/headers | Transparent, subtle `AppToolHoverBrush` on hover |
| `Button.panel-icon-btn` | `28x28` header/drawer icon triggers | Transparent, `CornerRadius="7"`, `AppSidebarHoverBrush` hover |
| `Button.dock-rail-btn` | `40x40` left rail navigation icons | `CornerRadius="10"`, `AppChipActiveBackgroundBrush` active pill |
| `Button.quell-segmented-tab` | Segmented tab items inside `quell-segmented-track` | Equal-width `UniformGrid` segments with `CornerRadius="6"` |
| `ToggleButton.quell-filter-toggle` | Pill toggle chip (`Active DB`, boolean filters) | `CornerRadius="999"`, `1px` border, emerald dot + `AppChipActiveBackgroundBrush` when `:checked` |
