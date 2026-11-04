---
name: fdb-customization-system
description: >-
  Architectural patterns, procedural runbook, and token catalog for Fdb Studio's customization and color system.
  Use when adding, modifying, or extending UI customization, Color Maker, theme tokens, button/border/sidebar styling,
  or SQLite persistence of user preferences in Fdb.
---

# Fdb Studio Customization & Color System Skill

This skill defines the architectural blueprint, token taxonomy, and step-by-step procedures for extending Fdb Studio's UI customization, Color Maker, theme brushes, and SQLite configuration engine.

Follow this standard whenever you add new customizable elements, modify existing colors, or implement theme tokens.

---

## 1. Architectural Overview (The 4-Layer System)

Fdb Studio's customization engine operates across four synchronized layers:

```
┌────────────────────────────────────────────────────────┐
│ 1. Storage & Pre-Caching (DatabaseStorageService)      │
│    - SQLite `settings` table (key/value)               │
│    - Pre-cached during Splash Screen (0 startup lag)   │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 2. ViewModel & Color Maker (SettingsModalViewModel)    │
│    - Reactive ObservableProperties                     │
│    - Bidirectional RGB <-> HEX Sync Engine             │
│    - Live Preview & Revert Mechanism                  │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 3. Theme Engine (ThemeService)                         │
│    - SetOrRemoveBrushOverride()                        │
│    - Overrides Application.Current.Resources at runtime│
│    - Automatic Dark/Light variant contrast adaptation │
└───────────────────────────┬────────────────────────────┘
                            │
┌───────────────────────────▼────────────────────────────┐
│ 4. Avalonia UI Views (DynamicResource Binding)         │
│    - ClassicStyles.axaml, Themes/LightTheme.axaml      │
│    - All controls bind via {DynamicResource App*Brush} │
│    - Instant, flicker-free updates without restarting  │
└────────────────────────────────────────────────────────┘
```

---

## 2. The Core Rules of Customization

1. **Always use `{DynamicResource}` for Customizable Brushes:**
   Never use `StaticResource` for brushes, colors, font sizes, or dimensions that the user can customize.
2. **The "Default" Keyword Convention:**
   When a customization property is `"Default"`, call `Application.Current.Resources.Remove(key)`. This allows Avalonia's `ThemeDictionaries` (`LightTheme.axaml` and `DarkTheme.axaml`) to resolve the clean baseline theme color dynamically.
3. **Pre-caching in Splash Screen:**
   All settings MUST be pre-loaded in `DatabaseStorageService.InitializeStartupCacheAsync` during the splash screen sequence. Never execute ad-hoc blocking SQLite queries on the main UI thread during window initialization.
4. **Zero Emojis in AXAML:**
   Use only Microsoft Fluent vector icons (`ui:FASymbolIcon`, `PathIcon`) across all dialogs, templates, and buttons.
5. **Bidirectional Color Maker Sync:**
   Always use a reentrancy guard flag (`_isSyncingColorMaker`) when syncing between RGB sliders and HEX text input to prevent infinite recursive change notification loops.

---

## 3. Step-by-Step: Adding a New Customizable Element

Whenever you add a new customizable element (e.g. a new panel background, status pill, or navbar border), follow these exact 8 steps:

### Step 1: Register Semantic Brush in Theme Dictionaries
Define baseline values in both light and dark dictionaries:

- In `Themes/LightTheme.axaml`:
  ```xml
  <SolidColorBrush x:Key="AppMyElementBrush" Color="#F8FAFC" />
  ```
- In `Themes/DarkTheme.axaml`:
  ```xml
  <SolidColorBrush x:Key="AppMyElementBrush" Color="#1E293B" />
  ```

### Step 2: Add Field to `PanelCustomizationRecord`
In `Services/DatabaseStorageService.cs`:
```csharp
public class PanelCustomizationRecord
{
    // ... existing fields ...
    public string MyElementColor { get; set; } = "Default";
}
```

### Step 3: Implement SQLite Serialization & Deserialization
In `Services/DatabaseStorageService.cs`:
- In `GetPanelCustomizationAsync()`:
  ```csharp
  record.MyElementColor = await GetSettingAsync("MyElementColor", "Default");
  ```
- In `SavePanelCustomizationAsync()`:
  ```csharp
  dict["MyElementColor"] = record.MyElementColor;
  ```

### Step 4: Map Runtime Override in `ThemeService`
In `Services/ThemeService.cs`:
- In `ApplyPanelCustomizationInternal()`:
  ```csharp
  SetOrRemoveBrushOverride("AppMyElementBrush", custom.MyElementColor);
  ```

### Step 5: Add Observable Property & Preview Brush in `SettingsModalViewModel`
In `ViewModels/SettingsModalViewModel.cs`:
```csharp
[ObservableProperty]
[NotifyPropertyChangedFor(nameof(PreviewMyElementBrush))]
private string _myElementColor = "Default";

public IBrush PreviewMyElementBrush
{
    get
    {
        if (!string.IsNullOrWhiteSpace(MyElementColor) && !MyElementColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(MyElementColor, out var color))
            return new SolidColorBrush(color);
        return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E293B")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
    }
}

[RelayCommand]
public void SelectMyElementColor(string hex)
{
    MyElementColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
    LivePreviewPanelCustomization();
}
```

### Step 6: Register in the Color Maker
In `ViewModels/SettingsModalViewModel.cs`:
- Add `"My Element Category: Target Name"` to `ColorMakerTargets`.
- In `ApplyColorMakerToTarget()`, add the switch case:
  ```csharp
  case "My Element Category: Target Name":
      MyElementColor = hex;
      break;
  ```
- Update `LivePreviewPanelCustomization()`, `OpenAsync()`, `SaveAsync()`, `ResetPanelCustomization()`, and `ApplyPanelBundle()`.

### Step 7: Add UI Controls to `PanelCustomizationComponent.axaml`
In `Components/Settings/PanelCustomizationComponent.axaml`:
```xml
<Grid ColumnDefinitions="160, *" Margin="0,4,0,0">
    <TextBlock Grid.Column="0" Text="My Element Color:" FontSize="11" FontWeight="SemiBold" Foreground="{DynamicResource AppTextPrimaryBrush}" VerticalAlignment="Center"/>
    <WrapPanel Grid.Column="1" Orientation="Horizontal" VerticalAlignment="Center">
        <Button Classes="color-swatch-btn" Command="{Binding SelectMyElementColorCommand}" CommandParameter="Default" Background="{DynamicResource AppMyElementBrush}" ToolTip.Tip="Default Theme" Margin="0,0,6,4"/>
        <Button Classes="color-swatch-btn" Command="{Binding SelectMyElementColorCommand}" CommandParameter="#3B82F6" Background="#3B82F6" ToolTip.Tip="Blue (#3B82F6)" Margin="0,0,6,4"/>
        <Button Classes="color-swatch-btn" Command="{Binding SelectMyElementColorCommand}" CommandParameter="#10B981" Background="#10B981" ToolTip.Tip="Green (#10B981)" Margin="0,0,6,4"/>
    </WrapPanel>
</Grid>
```

### Step 8: Update Verification Suite
In `Program.cs`:
Add an assertion in `VerifyLayoutAndPanelsAsync()` verifying that the property parses, saves to SQLite, resets, and applies correctly.

---

## 4. Complete Semantic Token Catalog

| Token Name | Light Theme Default | Dark Theme Default | Controlled Property | Scope |
| :--- | :--- | :--- | :--- | :--- |
| `AppAccentBrush` | `#2563EB` | `#3B82F6` | `ButtonAccentColor` | Primary action buttons, active tab indicator, status badges |
| `AppAccentHoverBrush` | `#1D4ED8` | `#60A5FA` | Derived from `ButtonAccentColor` | Pointer-over highlight on primary buttons |
| `AppButtonBackgroundBrush` | `#F8FAFC` | `#1E293B` | `ButtonBackgroundColor` | Face background of secondary and toolbar buttons |
| `AppButtonForegroundBrush` | `#0F172A` | `#F8FAFC` | `ButtonTextColor` | Label text of buttons |
| `AppButtonBorderBrush` | `#CBD5E1` | `#475569` | `ButtonBorderColor` | Outline border of buttons |
| `AppBorderBrush` | `#E2E8F0` | `#334155` | `BorderColor` | Global container frames, toolbars, modal dialogs, dividers |
| `AppSplitterBrush` | `#E2E8F0` | `#334155` | `SplitterColor` | GridSplitter resizing bars between panels |
| `AppSplitterHoverBrush` | `#3B82F6` | `#60A5FA` | Derived from `SplitterColor` | Active dragging indicator on splitters |
| `AppSidebarBackgroundBrush` | `#F8FAFC` | `#1E293B` | `SidebarBackground` | Object Explorer tree view canvas |
| `AppSidebarHeaderBackgroundBrush`| `#F8FAFC` | `#1E293B` | `SidebarHeaderBackground` | ToolWindowHeader and category tab strip |
| `AppSidebarHoverBrush` | `#F1F5F9` | `#334155` | `SidebarItemHoverBackground` | Mouse-over highlight on database and table tree nodes |
| `AppSidebarTextBrush` | `#0F172A` | `#F8FAFC` | `SidebarTextColor` | Database and table node labels |
| `AppSidebarBorderBrush` | `#E2E8F0` | `#334155` | `SidebarBorderColor` | Right border separating sidebar from editor |
| `AppEditorBackgroundBrush` | `#FFFFFF` | `#0F172A` | `EditorBackground` | SQL TextEditor canvas |
| `AppEditorForegroundBrush` | `#0F172A` | `#F8FAFC` | `EditorForeground` | SQL TextEditor code text |
| `AppKeywordBrush` | `#2563EB` | `#60A5FA` | `SelectedKeywordColor` | SQL syntax highlighting token |
| `AppTableWhiteColBackgroundBrush`| `#FFFFFF` | `#0F172A` | `TableBackground` | Query Results Table main row background |
| `AppTableAltColBackgroundBrush` | `#F8FAFC` | `#141E2E` | `AlternatingRowColor` | Query Results Table alternating row zebra striping |
| `AppTableCellForegroundBrush` | `#0F172A` | `#F8FAFC` | `CellTextColor` | Data cell text in results table |
| `AppTableHeaderBackgroundBrush`| `#F1F5F9` | `#1E293B` | `HeaderBackground` | Column header background in results table |
| `AppTableGridLinesHorizontalBrush`| `#CBD5E1`| `#334155` | `GridLinesColor` | Horizontal data grid lines |
| `AppTableGridLinesVerticalBrush` | `#E2E8F0` | `#334155` | `GridLinesColor` | Vertical data grid lines |
| `AppTableSelectedBackgroundBrush`| `#DBEAFE` | `#1E3A5F` | `SelectionColor` | Selected row / cell highlight |

---

## 5. Verification Commands

Always run these two commands before committing any customization or styling changes:

```powershell
# 1. Compile in Release mode (must be 0 warnings, 0 errors)
dotnet build Fdb.csproj --configuration Release

# 2. Run automated headless verification suite
dotnet run --configuration Release --no-build -- --verify
```
