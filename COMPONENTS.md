# Quell — Reusable UI Components & Integration Guide

## 1. Overview & Architecture

**Quell** is an enterprise-grade database management desktop IDE built with **.NET 10**, **Avalonia UI 12.1.1**, **FluentAvalonia 3.1.0**, **FluentIcons.Avalonia 2.1.345**, **AvaloniaEdit 12.0.0**, **Iciclecreek.Avalonia.Terminal 4.1.3**, **Xaml.Behaviors.Avalonia 12.0.7**, and **Dapper 2.1.89**.

The application is structured around a **strictly modular, reusable component architecture**. Visual elements are isolated into self-contained `UserControl` components residing in the `Quell.Components` namespace (organized by category under `Components/`), accompanied by dedicated MVVM ViewModels, GPU-gated motion behaviors (`Quell.Behaviors`), SQLite persistent services, and Microsoft Fluent vector iconography.

### Core Principles
1. **Zero Emojis / Native Vector Icons:** All graphical glyphs strictly utilize Microsoft Fluent vector symbols (`FluentAvalonia.UI.Controls.FASymbolIcon`, `FluentIcons.Avalonia.SymbolIcon`) and `PathIcon` vector geometries.
2. **Encapsulation & Reusability:** Every component owns its visual hierarchy, templates, and event routing, and exposes clean public APIs, `StyledProperty` bindings, or ViewModel contracts.
3. **Decoupled MVVM:** ViewModels never directly reference Avalonia UI controls. Data contexts and commands drive state changes.
4. **Theme-First & GPU/CPU Aware:** All surfaces and typography bind to `{DynamicResource App*Brush}` tokens defined in `Themes/LightTheme.axaml` and `Themes/DarkTheme.axaml`, while animations and shadows are gated behind `:is(TopLevel).gpu-mode` and `GpuMotionBehaviorBase<T>`.
5. **Resilient Autocomplete:** Avoids fragile third-party popup hosts; IntelliSense suggestions are delivered via dedicated in-tree overlay components.
6. **High-Performance Micro-ORM (Dapper):** All internal SQLite database interactions, object mappings, and query executions are powered by Dapper for maximum efficiency and clean parameterization.

---

## 2. Component Catalog Directory

All `UserControl` components share `namespace Quell.Components` (`xmlns:components="using:Quell.Components"`), grouped into category folders:

```text
Components/
├── Buttons/
│   ├── ActionButtonComponent.axaml               # Primary accent button (quell-btn-primary) with FASymbol + Text
│   ├── ToolButtonComponent.axaml                 # Compact secondary toolbar button (classic-tool-btn)
│   └── IconButtonComponent.axaml                 # Compact 22x22 icon-only menu/action button (classic-menu-btn)
│
├── Editor/
│   ├── EditorComponent.axaml                     # Composed multi-tab SQL query editor workspace
│   ├── EditorTabStripComponent.axaml             # Reusable auto-expanding uniform tab strip with close & add actions
│   ├── LiveSyntaxPreviewComponent.axaml          # Live SQL syntax snippet preview card for Settings
│   └── SqlIntelliSenseOverlayComponent.axaml     # Floating in-tree SQL autocomplete & snippet suggestion card
│
├── Tables/
│   ├── QueryResultTableComponent.axaml           # Excel-grade query results grid with ROW # gutter & alternating columns
│   ├── TableToolbarComponent.axaml               # Table search filter, refresh, execution stats & export toolbar
│   ├── TablePaginationComponent.axaml            # Selection status & pagination summary bar
│   └── SchemaPropertiesTableComponent.axaml      # Database schema, table columns, PK/FK, and metadata viewer
│
├── Modals/
│   ├── ModalOverlayComponent.axaml               # Universal base modal backdrop & dialog card
│   ├── ConnectDatabaseModalComponent.axaml       # Multi-engine database connection configuration dialog
│   ├── ConfirmActionModalComponent.axaml         # Action confirmation & danger alert dialog
│   ├── ExportDataModalComponent.axaml            # Data export format (CSV/JSON/SQL/TSV) modal
│   ├── ImportDataModalComponent.axaml            # Visual Excel/CSV import & column mapping modal (+ MappingSurfaceControl.cs)
│   ├── EditCellModalComponent.axaml              # Graphical cell editor & UPDATE SQL generator modal
│   ├── ForeignKeyInspectorModalComponent.axaml   # Quick foreign-key referenced record inspector modal
│   ├── DatabaseDiagramModalComponent.axaml       # Interactive pan/zoom ER diagram modal (+ DiagramSurfaceControl.cs)
│   ├── SettingsModalComponent.axaml              # Composed preferences & customization dialog
│   └── UpdateModalComponent.axaml                # 3-step software update modal (Available -> Downloading -> Ready)
│
├── Settings/
│   ├── SettingsCategoriesNavComponent.axaml      # Categories navigation sidebar for Settings modal
│   ├── ThemeSelectionCardsComponent.axaml        # Light, Dark, and System theme selection cards
│   ├── TableAppearanceModelsComponent.axaml      # Excel/Grid UI models and formula bar / striping / gridline toggles
│   ├── SyntaxColorSelectorComponent.axaml        # 6-preset SQL syntax keyword color palette
│   ├── TableCustomizationComponent.axaml         # Query Results Table row height, font size, gutter & color presets
│   ├── PanelCustomizationComponent.axaml         # Color Maker (RGB/Hex), buttons, borders, sidebar, editor & bundles
│   ├── ShortcutsSettingsComponent.axaml          # Dynamic keyboard shortcuts manager with key capture
│   ├── TerminalSettingsComponent.axaml           # Terminal shell, typography, and CLI binary overrides
│   └── PerformanceSettingsComponent.axaml        # Hardware adapter diagnostics & Auto/GPU/CPU graphics mode selector
│
├── Terminal/
│   ├── TerminalComponent.axaml                   # Multi-session tabbed PTY terminal with shell dropdown & CLI launcher
│   └── TerminalSessionView.axaml                 # Interactive terminal control host with missing-CLI fallback banner
│
├── Navigation/
│   ├── NavbarComponent.axaml                     # Composed top navigation bar (MenuBar + MainToolbar)
│   ├── MainMenuBarComponent.axaml                # Native desktop menu bar, brand icon & connection badge
│   ├── MainToolbarComponent.axaml                # Execution & document action toolbar (Run F5, New Tab, Save, Connect)
│   ├── StatusBarComponent.axaml                  # Bottom status bar with connection & update badges
│   └── ToolWindowHeaderComponent.axaml           # Reusable tool window header with title & close command
│
├── Panels/
│   ├── SidebarComponent.axaml                    # Collapsible left activity rail & Object Explorer / Queries / History drawer
│   ├── ObjectExplorerEmptyStateComponent.axaml   # Empty state connection card for Object Explorer
│   └── FooterComponent.axaml                     # Collapsible & maximizable bottom Results / Schema / Terminal dock
│
└── Splash/
    └── SplashScreenCardComponent.axaml           # Frameless splash card with SQLite startup preload progress

Themes/
├── ThemeTokens.axaml                             # Shared corner radii, table dimensions & typography tokens
├── LightTheme.axaml                              # Light theme semantic color tokens & table brushes
└── DarkTheme.axaml                               # Dark theme semantic color tokens & table brushes

Behaviors/
├── GpuButtonPressBehavior.cs                     # GPU-only button press spring scale
├── GpuCardHoverBehavior.cs                       # GPU-only card hover lift
├── GpuModalEntranceBehavior.cs                   # GPU-only modal entrance pop
├── GpuSlideEntranceBehavior.cs                   # GPU-only slide-and-fade entrance
├── GpuPulseAttentionBehavior.cs                  # GPU-only repeating status dot pulse
└── ContextMenuBehavior.cs                        # Dynamic ContextMenu builder attachment
```

---

## 3. How to Use Each Component (Usage Guides & Examples)

Add the XML namespace to your XAML root:

```xml
xmlns:components="using:Quell.Components"
```

---

### 3.1 Button Primitives (`ActionButtonComponent`, `ToolButtonComponent`, `IconButtonComponent`)
* **Files:**
  * [ActionButtonComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Buttons/ActionButtonComponent.axaml) | [ActionButtonComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Buttons/ActionButtonComponent.axaml.cs)
  * [ToolButtonComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Buttons/ToolButtonComponent.axaml) | [ToolButtonComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Buttons/ToolButtonComponent.axaml.cs)
  * [IconButtonComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Buttons/IconButtonComponent.axaml) | [IconButtonComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Buttons/IconButtonComponent.axaml.cs)
* **Description:** Reusable button `UserControl` wrappers exposing `StyledProperty` bindings over Quell's `quell-btn-primary`, `classic-tool-btn`, and `classic-menu-btn` styles with `FluentAvalonia` symbols (`FASymbol`).

#### Styled Properties:
* `ActionButtonComponent`: `Text` (`string`), `Symbol` (`FASymbol`, default `Play`), `Command` (`ICommand?`), `CommandParameter` (`object?`)
* `ToolButtonComponent`: `Text` (`string`), `Symbol` (`FASymbol`, default `Add`), `Command` (`ICommand?`), `CommandParameter` (`object?`)
* `IconButtonComponent`: `Symbol` (`FASymbol`, default `Refresh`), `Command` (`ICommand?`), `CommandParameter` (`object?`)

#### XAML Usage Example:
```xml
<StackPanel Orientation="Horizontal" Spacing="6">
    <components:ActionButtonComponent Text="Run Query"
                                      Symbol="Play"
                                      Command="{Binding ExecuteQueryCommand}" />
    <components:ToolButtonComponent Text="New Tab"
                                    Symbol="Add"
                                    Command="{Binding Editor.AddTabCommand}" />
    <components:IconButtonComponent Symbol="Refresh"
                                    Command="{Binding RefreshCommand}" />
</StackPanel>
```

---

### 3.2 `SqlIntelliSenseOverlayComponent`
* **File:** [SqlIntelliSenseOverlayComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/SqlIntelliSenseOverlayComponent.axaml) | [SqlIntelliSenseOverlayComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/SqlIntelliSenseOverlayComponent.axaml.cs)
* **Description:** A floating in-tree IntelliSense suggestion card featuring SQL keyword prioritization (e.g., `SELECT` ranking #1 on typing `'S'`), code snippet templates, vector icons, keyboard navigation, and live documentation previews.

#### Public API:
* **Events:**
  * `event EventHandler<SqlCompletionData>? ItemCommitted`: Fired when the user selects and commits an item (via `Enter`, `Tab`, or mouse click).
  * `event EventHandler? Dismissed`: Fired when the overlay closes.
* **Properties:**
  * `int ItemCount`: Total number of matching suggestions.
  * `SqlCompletionData? SelectedItem`: Currently highlighted suggestion item.
  * `string ActiveFilterWord`: The prefix string used to filter the list.
* **Methods:**
  * `bool UpdateFilter(string currentWord, bool showAll = false)`: Filters suggestions based on `currentWord`. Returns `true` if matches were found.
  * `void PositionAt(Point canvasPoint, double lineHeight, Size canvasSize)`: Positions the card relative to a caret coordinate, automatically flipping above the caret when near the bottom of the canvas.
  * `void SelectNext()` / `void SelectPrevious()`: Navigates suggestions.
  * `void CommitSelected()`: Commits the active selection and fires `ItemCommitted`.
  * `void Hide()`: Dismisses the card and clears the current match set.

#### XAML Usage Example:
```xml
<Grid>
    <ae:TextEditor Name="MyTextEditor" />
    <Canvas Name="OverlayCanvas" ClipToBounds="True">
        <components:SqlIntelliSenseOverlayComponent x:Name="IntelliSenseOverlay"
                                                    IsVisible="False"
                                                    ItemCommitted="OnIntelliSenseItemCommitted" />
    </Canvas>
</Grid>
```

---

### 3.3 `EditorComponent` & `EditorTabStripComponent`
* **Files:**
  * [EditorComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/EditorComponent.axaml) | [EditorComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/EditorComponent.axaml.cs)
  * [EditorTabStripComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/EditorTabStripComponent.axaml) | [EditorTabStripComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/EditorTabStripComponent.axaml.cs)
* **DataContext:** `EditorViewModel`
* **Description:** A multi-tab SQL document editor featuring isolated per-tab undo/redo history, uniform auto-expanding tabs (`EditorTabStripComponent`, exposing `event EventHandler<EditorTabItem>? TabSelected`), theme-aware syntax highlighting (`AppKeywordBrush`), zooming shortcuts (`Ctrl++`, `Ctrl+-`, `Ctrl+0`, `Ctrl+MouseWheel`), and built-in IntelliSense (`Ctrl+Space` or typing word characters).

#### XAML Usage Example:
```xml
<components:EditorComponent x:Name="MainEditor"
                            DataContext="{Binding Editor}" />

<!-- Standalone tab strip inside EditorComponent or custom host: -->
<components:EditorTabStripComponent DataContext="{Binding}" />
```

#### C# Usage Example:
```csharp
var editorVm = new EditorViewModel();
editorVm.AddTab("Query 1.sql", "SELECT * FROM Users WHERE status = 'Active';");

var editorComponent = new EditorComponent { DataContext = editorVm };
editorComponent.ZoomIn();
editorComponent.ZoomOut();
editorComponent.ResetZoom();
```

---

### 3.4 `QueryResultTableComponent`, `TableToolbarComponent` & `TablePaginationComponent`
* **Files:**
  * [QueryResultTableComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/QueryResultTableComponent.axaml) | [QueryResultTableComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/QueryResultTableComponent.axaml.cs)
  * [TableToolbarComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/TableToolbarComponent.axaml) | [TableToolbarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/TableToolbarComponent.axaml.cs)
  * [TablePaginationComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/TablePaginationComponent.axaml) | [TablePaginationComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/TablePaginationComponent.axaml.cs)
* **DataContext:** `QueryResultTableViewModel`
* **Description:** An Excel-grade, high-performance database query results grid composed with `TableToolbarComponent` (search filter, refresh, execution stats, export) and `TablePaginationComponent` (selection & pagination status). Supports customizable row heights, theme-aware alternating columns (`AppTableWhiteColBackgroundBrush` & `AppTableAltColBackgroundBrush`), `ROW #` gutter, interactive context menu (Copy Cell/Row/JSON/CSV, Edit Cell, FK Inspector), and a shadow-free loading overlay (`QueryLoadingCardBorder`).

#### XAML Usage Example:
```xml
<components:QueryResultTableComponent x:Name="ResultsGrid"
                                      DataContext="{Binding SampleQueryResult}" />
```

#### C# Usage Example:
```csharp
var tableVm = new QueryResultTableViewModel();
tableVm.Columns.Add(new QueryResultColumn { Header = "ID", DataType = "INT" });
tableVm.Columns.Add(new QueryResultColumn { Header = "NAME", DataType = "NVARCHAR" });
tableVm.Rows.Add(new QueryResultRow(1, ["101", "Alice Johnson"]));

var tableControl = new QueryResultTableComponent { DataContext = tableVm };
```

---

### 3.5 `SchemaPropertiesTableComponent`
* **File:** [SchemaPropertiesTableComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/SchemaPropertiesTableComponent.axaml) | [SchemaPropertiesTableComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Tables/SchemaPropertiesTableComponent.axaml.cs)
* **DataContext:** `SchemaPropertiesTableViewModel`
* **Description:** Interactive table schema metadata viewer showing primary keys, foreign keys, data types, nullability, default values, and column descriptions with compact alternating rows.

#### XAML Usage Example:
```xml
<components:SchemaPropertiesTableComponent DataContext="{Binding SampleSchema}" />
```

---

### 3.6 `ModalOverlayComponent` (Universal Modal Base Card)
* **File:** [ModalOverlayComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ModalOverlayComponent.axaml) | [ModalOverlayComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ModalOverlayComponent.axaml.cs)
* **Description:** Foundational reusable modal card supporting arbitrary custom content via `ModalContent` (`[Content]`), interactive backdrop dismissal, and GPU-gated entrance motion.

#### Styled Properties:
* `IsOpen` (`bool`): Controls modal visibility and backdrop.
* `Title` (`string`, default: `"Dialog"`): Header title text.
* `Subtitle` (`string?`): Secondary guidance text below the title.
* `Icon` (`string`, default: `"Important"`): Header `FASymbolIcon` name.
* `DialogWidth` (`double`, default: `520.0`): Width of the modal card.
* `CloseCommand` (`ICommand?`): Invoked on backdrop click or close button.
* `PrimaryButtonText` (`string?`, default: `"OK"`) & `PrimaryCommand` (`ICommand?`).
* `SecondaryButtonText` (`string?`) & `SecondaryCommand` (`ICommand?`).
* `CancelButtonText` (`string`, default: `"Cancel"`).
* `ModalContent` (`object?`): Content slot for custom body controls.

#### XAML Usage Example:
```xml
<components:ModalOverlayComponent IsOpen="{Binding IsModalOpen}"
                                  Title="Custom Configuration"
                                  Subtitle="Configure your database sync options"
                                  DialogWidth="480"
                                  PrimaryButtonText="Apply Changes"
                                  PrimaryCommand="{Binding SaveSettingsCommand}"
                                  CloseCommand="{Binding CloseModalCommand}">
    <StackPanel Spacing="12" Margin="4">
        <TextBlock Text="Sync Interval (Minutes):" FontWeight="SemiBold"/>
        <NumericUpDown Value="15" Minimum="1" Maximum="1440"/>
    </StackPanel>
</components:ModalOverlayComponent>
```

---

### 3.7 Feature Modals (`Connect`, `Confirm`, `Export`, `Import`, `EditCell`, `FKInspector`, `Diagram`, `Update`)

All feature modals bind `IsOpen`, render with `Classes="modal-backdrop"` + `Classes="modal-dialog-card"` + `GpuModalEntranceBehavior`, and are hosted in `Views/MainWindow.axaml` with `Grid.Row="0" Grid.RowSpan="3"`:

| Component | File | DataContext | Purpose / ZIndex |
| :--- | :--- | :--- | :--- |
| `ConnectDatabaseModalComponent` | [ConnectDatabaseModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ConnectDatabaseModalComponent.axaml) | `ConnectDatabaseModalViewModel` | Multi-engine connection & live test (`ZIndex="100"`) |
| `ConfirmActionModalComponent` | [ConfirmActionModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ConfirmActionModalComponent.axaml) | `ConfirmActionModalViewModel` | Destructive action warning & confirm (`ZIndex="101"`) |
| `ExportDataModalComponent` | [ExportDataModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ExportDataModalComponent.axaml) | `ExportDataModalViewModel` | CSV/JSON/SQL/TSV export dialog (`ZIndex="102"`) |
| `SettingsModalComponent` | [SettingsModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/SettingsModalComponent.axaml) | `SettingsModalViewModel` | Composed settings & Color Maker modal (`ZIndex="103"`) |
| `EditCellModalComponent` | [EditCellModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/EditCellModalComponent.axaml) | `EditCellModalViewModel` | Graphical cell editor & `UPDATE` preview (`ZIndex="104"`) |
| `DatabaseDiagramModalComponent` | [DatabaseDiagramModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/DatabaseDiagramModalComponent.axaml) | `DatabaseDiagramModalViewModel` | Pan/zoom/drag ER diagram + `DiagramSurfaceControl` (`ZIndex="105"`) |
| `UpdateModalComponent` | [UpdateModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/UpdateModalComponent.axaml) | `UpdateModalViewModel` | 3-step update flow (`ZIndex="106"`) |
| `ImportDataModalComponent` | [ImportDataModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ImportDataModalComponent.axaml) | `ImportDataModalViewModel` | Visual CSV/Excel mapping + `MappingSurfaceControl` (`ZIndex="107"`) |
| `ForeignKeyInspectorModalComponent` | [ForeignKeyInspectorModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/ForeignKeyInspectorModalComponent.axaml) | `ForeignKeyInspectorModalViewModel` | Foreign-key target record quick inspector (`ZIndex="108"`) |

#### XAML Usage Example (`MainWindow.axaml`):
```xml
<components:ConnectDatabaseModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="100" DataContext="{Binding ConnectModal}" />
<components:ConfirmActionModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="101" DataContext="{Binding ConfirmModal}" />
<components:ExportDataModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="102" DataContext="{Binding ExportModal}" />
<components:SettingsModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="103" DataContext="{Binding SettingsModal}" />
<components:EditCellModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="104" DataContext="{Binding SampleQueryResult.EditCellModal}" />
<components:DatabaseDiagramModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="105" DataContext="{Binding DiagramModal}" />
<components:UpdateModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="106" DataContext="{Binding UpdateModal}" />
<components:ImportDataModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="107" DataContext="{Binding ImportModal}" />
<components:ForeignKeyInspectorModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="108" DataContext="{Binding SampleQueryResult.ForeignKeyInspectorModal}" />
```

---

### 3.8 `SettingsModalComponent` & Settings Sub-Components
* **Modal File:** [SettingsModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/SettingsModalComponent.axaml) | [SettingsModalComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Modals/SettingsModalComponent.axaml.cs)
* **DataContext:** `SettingsModalViewModel`
* **Description:** Composed application preferences dialog backed by SQLite persistent storage (`AppSettings` table via Dapper) and startup pre-caching. It assembles these modular sub-components (all `x:DataType="vm:SettingsModalViewModel"`):
  * [SettingsCategoriesNavComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/SettingsCategoriesNavComponent.axaml) — Category navigation rail
  * [ThemeSelectionCardsComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/ThemeSelectionCardsComponent.axaml) — Light / Dark / System theme selector cards
  * [TableAppearanceModelsComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/TableAppearanceModelsComponent.axaml) — Excel / Flat / Compact / High-Contrast table UI models and grid toggles
  * [SyntaxColorSelectorComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/SyntaxColorSelectorComponent.axaml) — 6 SQL keyword color presets
  * [LiveSyntaxPreviewComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Editor/LiveSyntaxPreviewComponent.axaml) — Live SQL keyword highlight preview
  * [TableCustomizationComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/TableCustomizationComponent.axaml) — Table row height, font size, gutter, and color presets
  * [PanelCustomizationComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/PanelCustomizationComponent.axaml) — Color Maker (RGB/Hex sync), button/border/sidebar/editor colors, and quick bundles
  * [ShortcutsSettingsComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/ShortcutsSettingsComponent.axaml) — Dynamic keyboard shortcut capture & filtering
  * [TerminalSettingsComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/TerminalSettingsComponent.axaml) — Shell profile, font settings, and CLI binary overrides
  * [PerformanceSettingsComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Settings/PerformanceSettingsComponent.axaml) — Hardware adapter card & `Auto` / `GPU` / `CPU` rendering mode switcher

---

### 3.9 Theme Components & Color Tokens
* **Files:**
  * [ThemeTokens.axaml](file:///c:/Users/ehsan/Desktop/Quell/Themes/ThemeTokens.axaml): Shared corner radii, table dimensions, and typography.
  * [LightTheme.axaml](file:///c:/Users/ehsan/Desktop/Quell/Themes/LightTheme.axaml): Light mode semantic color tokens and table brushes.
  * [DarkTheme.axaml](file:///c:/Users/ehsan/Desktop/Quell/Themes/DarkTheme.axaml): Dark mode semantic color tokens and table brushes.
* **Usage in `App.axaml`:**
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceInclude Source="avares://Quell/Themes/ThemeTokens.axaml" />
        </ResourceDictionary.MergedDictionaries>
        <ResourceDictionary.ThemeDictionaries>
            <ResourceInclude x:Key="Light" Source="avares://Quell/Themes/LightTheme.axaml" />
            <ResourceInclude x:Key="Dark" Source="avares://Quell/Themes/DarkTheme.axaml" />
        </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

---

### 3.10 `ToolWindowHeaderComponent`
* **File:** [ToolWindowHeaderComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/ToolWindowHeaderComponent.axaml) | [ToolWindowHeaderComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/ToolWindowHeaderComponent.axaml.cs)
* **Description:** Reusable title header for dockable panels and tool windows with a close action button.

#### Styled Properties:
* `Title` (`string`, default: `"Tool Window"`): Panel title (e.g., `"OBJECT EXPLORER"`).
* `CloseCommand` (`ICommand?`): Invoked when clicking the close button.

#### XAML Usage Example:
```xml
<components:ToolWindowHeaderComponent Title="OBJECT EXPLORER"
                                      CloseCommand="{Binding ToggleSidebarCommand}" />
```

---

### 3.11 Navigation Components (`NavbarComponent`, `MainMenuBarComponent`, `MainToolbarComponent`, `StatusBarComponent`)
* **Files:**
  * [NavbarComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/NavbarComponent.axaml) | [NavbarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/NavbarComponent.axaml.cs) (`NavbarViewModel`)
  * [MainMenuBarComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/MainMenuBarComponent.axaml) | [MainMenuBarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/MainMenuBarComponent.axaml.cs) (`NavbarViewModel`)
  * [MainToolbarComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/MainToolbarComponent.axaml) | [MainToolbarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/MainToolbarComponent.axaml.cs) (`NavbarViewModel`)
  * [StatusBarComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/StatusBarComponent.axaml) | [StatusBarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Navigation/StatusBarComponent.axaml.cs) (`MainViewModel`)
* **Description:** Top window chrome, native menus (`File`, `Edit`, `View`, `Window`, `Help`), execution toolbar (`Run (F5)`, `New Tab`, `Connect`), and anchored bottom status bar with connection and update indicators.

#### XAML Usage Example:
```xml
<components:NavbarComponent x:Name="MainNavbar" Grid.Row="0" DataContext="{Binding Navbar}" />
<components:StatusBarComponent x:Name="MainStatusBar" Grid.Row="2" />
```

---

### 3.12 Panel Components (`SidebarComponent`, `ObjectExplorerEmptyStateComponent`, `FooterComponent`)
* **Files:**
  * [SidebarComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Panels/SidebarComponent.axaml) | [SidebarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Panels/SidebarComponent.axaml.cs) (`SidebarViewModel`)
  * [ObjectExplorerEmptyStateComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Panels/ObjectExplorerEmptyStateComponent.axaml) | [ObjectExplorerEmptyStateComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Panels/ObjectExplorerEmptyStateComponent.axaml.cs)
  * [FooterComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Panels/FooterComponent.axaml) | [FooterComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Panels/FooterComponent.axaml.cs) (`FooterViewModel`)
* **Description:** Left collapsible activity rail + drawer (`Object Explorer`, `Saved Queries`, `History`), disconnected empty state card (`ObjectExplorerEmptyStateComponent`), and collapsible/maximizable bottom dock (`FooterComponent`) hosting Results, Schema, and Terminal tabs.

#### XAML Usage Example:
```xml
<components:SidebarComponent Grid.Column="0" DataContext="{Binding Sidebar}" />
<components:FooterComponent Grid.Row="2" DataContext="{Binding Footer}" />
```

---

### 3.13 Terminal Components (`TerminalComponent`, `TerminalSessionView`)
* **Files:**
  * [TerminalComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Terminal/TerminalComponent.axaml) | [TerminalComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Terminal/TerminalComponent.axaml.cs) (`TerminalViewModel`)
  * [TerminalSessionView.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Terminal/TerminalSessionView.axaml) | [TerminalSessionView.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Components/Terminal/TerminalSessionView.axaml.cs) (`TerminalSessionViewModel`)
* **Description:** Multi-session tabbed PTY terminal (`Iciclecreek.Terminal`) with shell selector, database CLI launcher, live theme color synchronization, and missing-CLI error banner with fallback actions.

#### XAML Usage Example:
```xml
<components:TerminalComponent DataContext="{Binding Terminal}" />
```

---

### 3.14 `SplashScreenCardComponent` & Startup Pre-load Sequence
* **File:** [SplashScreenCardComponent.axaml](file:///c:/Users/ehsan/Desktop/Quell/Components/Splash/SplashScreenCardComponent.axaml) | [SplashScreenWindow.axaml.cs](file:///c:/Users/ehsan/Desktop/Quell/Views/SplashScreenWindow.axaml.cs)
* **DataContext:** `SplashScreenViewModel`
* **Description:** Reusable frameless splash card with startup artwork and live SQLite initialization diagnostics. Pre-loads all core app configurations (theme, keyword syntax color, panel customizations, table dimensions, graphics mode, keyboard shortcuts, dialect keywords, saved connections, and query history) before displaying `MainWindow`.

#### XAML Usage Example:
```xml
<components:SplashScreenCardComponent DataContext="{Binding ViewModel}" />
```

---

## 4. MVVM Hierarchy & Data Flow

```text
MainViewModel
├── Navbar: NavbarViewModel
├── Sidebar: SidebarViewModel
├── Editor: EditorViewModel
│     ├── SelectedTab: EditorTabItem
│     └── Tabs: ObservableCollection<EditorTabItem>
├── Footer: FooterViewModel (Manages IsMaximized, QueryResult, Schema & Terminal tabs)
├── Terminal: TerminalViewModel
│     └── Sessions: ObservableCollection<TerminalSessionViewModel>
│
├── Modals:
│   ├── ConnectModal: ConnectDatabaseModalViewModel
│   ├── ConfirmModal: ConfirmActionModalViewModel
│   ├── ExportModal: ExportDataModalViewModel
│   ├── ImportModal: ImportDataModalViewModel
│   ├── DiagramModal: DatabaseDiagramModalViewModel
│   ├── SettingsModal: SettingsModalViewModel
│   └── UpdateModal: UpdateModalViewModel
│
└── Tables:
    ├── SampleQueryResult: QueryResultTableViewModel
    │     ├── EditCellModal: EditCellModalViewModel
    │     └── ForeignKeyInspectorModal: ForeignKeyInspectorModalViewModel
    └── SampleSchema: SchemaPropertiesTableViewModel
```

---

## 5. Verification & Test Suite

Quell includes an automated headless layout, component, theme, and database verification suite in [Program.cs](file:///c:/Users/ehsan/Desktop/Quell/Program.cs). Run after any component or styling change:

```powershell
dotnet build Quell.csproj --configuration Release
dotnet run --configuration Release --no-build -- --verify
```
