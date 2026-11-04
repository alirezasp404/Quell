# Fdb Studio Theme Tokens Reference

This document catalogs all XAML color and brush resources defined in the design system, their fallback defaults, and their relationships with `ThemeService`.

## 1. Palette Resources

Located in `Themes/LightTheme.axaml` and `Themes/DarkTheme.axaml`:

```xml
<!-- Light Theme Base Colors -->
<Color x:Key="AppControlFaceColor">#F8FAFC</Color>
<Color x:Key="AppWindowBackgroundColor">#FFFFFF</Color>
<Color x:Key="AppWorkspaceColor">#FFFFFF</Color>
<Color x:Key="AppBorderColor">#E2E8F0</Color>
<Color x:Key="AppTextPrimaryColor">#0F172A</Color>
<Color x:Key="AppTextSecondaryColor">#475569</Color>
<Color x:Key="AppAccentColor">#2563EB</Color>

<!-- Dark Theme Base Colors -->
<Color x:Key="AppControlFaceColor">#1E293B</Color>
<Color x:Key="AppWindowBackgroundColor">#0F172A</Color>
<Color x:Key="AppWorkspaceColor">#0F172A</Color>
<Color x:Key="AppBorderColor">#334155</Color>
<Color x:Key="AppTextPrimaryColor">#F8FAFC</Color>
<Color x:Key="AppTextSecondaryColor">#CBD5E1</Color>
<Color x:Key="AppAccentColor">#3B82F6</Color>
```

## 2. Dynamic Semantic Brushes

All Avalonia controls consume these brushes using `{DynamicResource Key}`.

| Resource Key | Usage | Dynamic Overriding Method |
| :--- | :--- | :--- |
| `AppAccentBrush` | Primary action buttons, tabs active border | `ThemeService.SetOrRemoveBrushOverride("AppAccentBrush", ButtonAccentColor)` |
| `AppAccentHoverBrush` | Hover state for accent buttons | Auto-computed: `R+25, G+25, B+25` |
| `AppButtonBackgroundBrush` | Secondary/Toolbar button face | `SetOrRemoveBrushOverride("AppButtonBackgroundBrush", ButtonBackgroundColor)` |
| `AppButtonForegroundBrush` | Button text label | `SetOrRemoveBrushOverride("AppButtonForegroundBrush", ButtonTextColor)` |
| `AppButtonBorderBrush` | Button border stroke | `SetOrRemoveBrushOverride("AppButtonBorderBrush", ButtonBorderColor)` |
| `AppBorderBrush` | Window and panel frames | `SetOrRemoveBrushOverride("AppBorderBrush", BorderColor)` |
| `AppSplitterBrush` | Resizing splitter bars | `SetOrRemoveBrushOverride("AppSplitterBrush", SplitterColor)` |
| `AppSplitterHoverBrush` | Active drag indicator on splitters | Auto-computed: `R+35, G+35, B+35` |
| `AppSidebarBackgroundBrush` | Object Explorer tree canvas | `SetOrRemoveBrushOverride("AppSidebarBackgroundBrush", SidebarBackground)` |
| `AppSidebarHeaderBackgroundBrush`| Object Explorer header & tabs | `SetOrRemoveBrushOverride("AppSidebarHeaderBackgroundBrush", SidebarHeaderBackground)` |
| `AppSidebarHoverBrush` | Tree node pointer-over highlight | `SetOrRemoveBrushOverride("AppSidebarHoverBrush", SidebarItemHoverBackground)` |
| `AppSidebarTextBrush` | Tree node labels | `SetOrRemoveBrushOverride("AppSidebarTextBrush", SidebarTextColor)` |
| `AppSidebarBorderBrush` | Sidebar right border divider | `SetOrRemoveBrushOverride("AppSidebarBorderBrush", SidebarBorderColor)` |
| `AppEditorBackgroundBrush` | SQL Code Editor background | `SetOrRemoveBrushOverride("AppEditorBackgroundBrush", EditorBackground)` |
| `AppEditorForegroundBrush` | SQL Code Editor text color | `SetOrRemoveBrushOverride("AppEditorForegroundBrush", EditorForeground)` |
| `AppKeywordBrush` | SQL Syntax Keyword Token | `ThemeService.SetKeywordColor(SelectedKeywordColor)` |
| `AppTableWhiteColBackgroundBrush`| Main row background in data table | `SetOrRemoveBrushOverride("AppTableWhiteColBackgroundBrush", TableBackground)` |
| `AppTableAltColBackgroundBrush` | Zebra striping alternating row | `SetOrRemoveBrushOverride("AppTableAltColBackgroundBrush", AlternatingRowColor)` |
| `AppTableCellForegroundBrush` | Cell data text | `SetOrRemoveBrushOverride("AppTableCellForegroundBrush", CellTextColor)` |
| `AppTableHeaderBackgroundBrush`| Column header bar in data table | `SetOrRemoveBrushOverride("AppTableHeaderBackgroundBrush", HeaderBackground)` |
| `AppTableHeaderForegroundBrush`| Column header text label | `SetOrRemoveBrushOverride("AppTableHeaderForegroundBrush", HeaderTextColor)` |
| `AppTableGridLinesHorizontalBrush`| Table horizontal grid lines | `SetOrRemoveBrushOverride("AppTableGridLinesHorizontalBrush", GridLinesColor)` |
| `AppTableGridLinesVerticalBrush` | Table vertical grid lines | `SetOrRemoveBrushOverride("AppTableGridLinesVerticalBrush", GridLinesColor)` |
| `AppTableSelectedBackgroundBrush`| Active cell and row selection | `SetOrRemoveBrushOverride("AppTableSelectedBackgroundBrush", SelectionColor)` |
