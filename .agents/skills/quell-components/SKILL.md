---
name: quell-components
description: >-
  Catalog, integration patterns, and usage rules for Quell's reusable Avalonia UI UserControl components.
  Use whenever building a new view or modal, composing UI from existing components (buttons, editor,
  tables, modals, settings panels, terminal, navigation, or tool windows), wiring DataContexts and
  StyledProperties, or adding a new reusable component to Quell.
---

# Quell UI Components Skill

This skill enables fast, consistent reuse of **Quell's** modular `UserControl` components located under `Components/`.
Always check whether an existing component already solves the UI need before writing new XAML markup.

For complete API details, directory layout, and copy-pasteable XAML/C# examples, read [COMPONENTS.md](../../../COMPONENTS.md).

---

## 1. Quick Integration Rules

1. **XML Namespace & Class Namespace:**
   - Every component `UserControl` lives in the CLR namespace `Quell.Components`, regardless of its subfolder (`Components/Buttons/`, `Components/Modals/`, `Components/Tables/`, etc.).
   - Import in any `.axaml` view or parent component with:
     ```xml
     xmlns:components="using:Quell.Components"
     ```
2. **Reuse Before Creating:**
   - Primary / Toolbar / Icon buttons -> `ActionButtonComponent`, `ToolButtonComponent`, `IconButtonComponent` (or style classes in [`quell-design-system`](../quell-design-system/SKILL.md)).
   - Custom modal dialogs -> wrap body inside `ModalOverlayComponent` instead of duplicating backdrop/header/footer chrome.
   - Panel title bars -> `ToolWindowHeaderComponent` (`Title`, `CloseCommand`).
   - Query result toolbars & footers -> `TableToolbarComponent`, `TablePaginationComponent`.
3. **Theme & GPU/CPU Compliance:**
   - All components must adhere to [`quell-design-system`](../quell-design-system/SKILL.md) (dynamic `{DynamicResource App*Brush}` tokens, zero emojis, GPU-gated motion, popup overlay flags) and [`quell-customization-system`](../quell-customization-system/SKILL.md) when introducing customizable color tokens.
4. **Top-Level Modals in `MainWindow.axaml`:**
   - Full-viewport modal overlays sit inside the root `Grid` of `Views/MainWindow.axaml` (and `Views/MainView.axaml` when applicable) with `Grid.Row="0" Grid.RowSpan="3"` and sequential `ZIndex` (`100`+).

---

## 2. Component Lookup Catalog

### 2.1 Buttons (`Components/Buttons/`)
| Component | Key Properties / DataContext | Use Case |
| :--- | :--- | :--- |
| `ActionButtonComponent` | `Text`, `Symbol` (`FASymbol`), `Command`, `CommandParameter` | Primary accent CTA button (`quell-btn-primary`) with icon + label |
| `ToolButtonComponent` | `Text`, `Symbol` (`FASymbol`), `Command`, `CommandParameter` | Compact secondary toolbar button (`classic-tool-btn`, `Height="24"`) |
| `IconButtonComponent` | `Symbol` (`FASymbol`), `Command`, `CommandParameter` | Compact `22x22` icon-only action button (`classic-menu-btn`) |

### 2.2 Editor & IntelliSense (`Components/Editor/`)
| Component | DataContext / API | Use Case |
| :--- | :--- | :--- |
| `EditorComponent` | `EditorViewModel` | Multi-tab SQL workspace with zoom (`ZoomIn`/`ZoomOut`/`ResetZoom`) and autocomplete |
| `EditorTabStripComponent` | `EditorViewModel`, event `TabSelected` | Uniform auto-expanding document tab strip with close & add actions |
| `SqlIntelliSenseOverlayComponent` | `UpdateFilter()`, `PositionAt()`, `SelectNext()`, `SelectPrevious()`, `CommitSelected()`, `Hide()`, events `ItemCommitted`, `Dismissed` | In-tree floating SQL keyword & snippet autocomplete overlay |
| `LiveSyntaxPreviewComponent` | `SettingsModalViewModel` | Mini live SQL syntax highlight preview card |

### 2.3 Tables & Grids (`Components/Tables/`)
| Component | DataContext | Use Case |
| :--- | :--- | :--- |
| `QueryResultTableComponent` | `QueryResultTableViewModel` | Excel-grade query results `DataGrid` with `ROW #` gutter, alternating columns, context menu, and shadow-free loading overlay |
| `TableToolbarComponent` | `QueryResultTableViewModel` | Search filter, refresh, execution stats, and export toolbar |
| `TablePaginationComponent` | `QueryResultTableViewModel` | Selection status and pagination summary bar |
| `SchemaPropertiesTableComponent` | `SchemaPropertiesTableViewModel` | Table column metadata, PK/FK, nullability, and defaults grid |

### 2.4 Modals & Dialogs (`Components/Modals/`)
| Component | DataContext / API | Use Case |
| :--- | :--- | :--- |
| `ModalOverlayComponent` | StyledProperties: `IsOpen`, `Title`, `Subtitle`, `Icon`, `DialogWidth`, `ModalContent`, `CloseCommand`, `PrimaryButtonText`, `PrimaryCommand`, `SecondaryButtonText`, `SecondaryCommand`, `CancelButtonText` | Universal base modal card & backdrop for custom dialogs |
| `ConnectDatabaseModalComponent` | `ConnectDatabaseModalViewModel` | Database connection configuration & live test dialog (`ZIndex="100"`) |
| `ConfirmActionModalComponent` | `ConfirmActionModalViewModel` | Destructive action confirmation dialog (`ZIndex="101"`) |
| `ExportDataModalComponent` | `ExportDataModalViewModel` | CSV / JSON / SQL / TSV data export dialog (`ZIndex="102"`) |
| `SettingsModalComponent` | `SettingsModalViewModel` | Composed preferences & customization modal (`ZIndex="103"`) |
| `EditCellModalComponent` | `EditCellModalViewModel` (`SampleQueryResult.EditCellModal`) | Graphical cell editor & `UPDATE` SQL generator (`ZIndex="104"`) |
| `DatabaseDiagramModalComponent` | `DatabaseDiagramModalViewModel` | Interactive ER diagram canvas with pan/zoom/drag & `DiagramSurfaceControl` (`ZIndex="105"`) |
| `UpdateModalComponent` | `UpdateModalViewModel` | 3-step update flow: Available -> Downloading -> Ready (`ZIndex="106"`) |
| `ImportDataModalComponent` | `ImportDataModalViewModel` | Visual Excel/CSV import & column mapping via `MappingSurfaceControl` (`ZIndex="107"`) |
| `ForeignKeyInspectorModalComponent` | `ForeignKeyInspectorModalViewModel` (`SampleQueryResult.ForeignKeyInspectorModal`) | Quick foreign-key referenced record inspector (`ZIndex="108"`) |

### 2.5 Settings Sub-Components (`Components/Settings/`)
All bind to `SettingsModalViewModel` inside `SettingsModalComponent`:
- `SettingsCategoriesNavComponent` — Left category navigation list
- `ThemeSelectionCardsComponent` — Light / Dark / System theme selector cards
- `SyntaxColorSelectorComponent` — 6 SQL keyword color presets
- `TableCustomizationComponent` — Row height, font size, gutter, and color presets
- `PanelCustomizationComponent` — Color Maker (RGB/Hex), borders, sidebar, editor, and quick bundles
- `ShortcutsSettingsComponent` — Keyboard shortcut viewer & key capture
- `TerminalSettingsComponent` — Default shell, emulator typography, and CLI binary paths
- `PerformanceSettingsComponent` — Hardware graphics adapter info & `Auto` / `GPU` / `CPU` rendering mode cards

### 2.6 Navigation, Panels, Terminal & Splash
| Component | Folder | DataContext / API | Use Case |
| :--- | :--- | :--- | :--- |
| `NavbarComponent` | `Navigation/` | `NavbarViewModel` | Top window chrome, menu bar, and action toolbar |
| `MainMenuBarComponent` | `Navigation/` | `NavbarViewModel` | Native desktop menu bar (`File`, `Edit`, `View`, `Window`, `Help`) + brand & connection badge |
| `MainToolbarComponent` | `Navigation/` | `NavbarViewModel` | Run (`F5`), New Tab, Save, Connect, and Disconnect toolbar |
| `StatusBarComponent` | `Navigation/` | `MainViewModel` | Bottom status bar with ready state, update pill, and connection badges |
| `ToolWindowHeaderComponent` | `Navigation/` | `Title`, `CloseCommand` | Reusable dockable tool-window header bar |
| `SidebarComponent` | `Panels/` | `SidebarViewModel` | Collapsible left activity rail & Object Explorer / Saved Queries / History drawer |
| `ObjectExplorerEmptyStateComponent` | `Panels/` | Inherits `Window` `MainViewModel` | Disconnected Object Explorer empty state card with Connect CTA |
| `FooterComponent` | `Panels/` | `FooterViewModel` | Collapsible/maximizable bottom dock hosting Results, Schema, and Terminal |
| `TerminalComponent` | `Terminal/` | `TerminalViewModel` | Multi-session tabbed PTY terminal host & CLI launcher |
| `TerminalSessionView` | `Terminal/` | `TerminalSessionViewModel` | Individual `Iciclecreek.Terminal` control host + missing-CLI fallback banner |
| `SplashScreenCardComponent` | `Splash/` | `SplashScreenViewModel` | Frameless startup splash card with SQLite preload progress |

---

## 3. Workflow: Using or Adding a Component

### Reusing an Existing Component
1. Locate the component in Section 2 above and inspect its section in [COMPONENTS.md](../../../COMPONENTS.md) (plus its `.axaml` / `.axaml.cs` if you need exact bindings).
2. Add `xmlns:components="using:Quell.Components"` to the host AXAML file.
3. Instantiate `<components:ComponentName ... />` and bind its expected `DataContext` or `StyledProperty` values.

### Creating a New Reusable Component
1. Place `<Name>Component.axaml` and `<Name>Component.axaml.cs` under the matching `Components/<Category>/` subfolder, using `x:Class="Quell.Components.<Name>Component"` and `namespace Quell.Components;`.
2. Follow [`quell-design-system`](../quell-design-system/SKILL.md) (dynamic `{DynamicResource App*Brush}` tokens, Fluent vector icons only, GPU-scoped motion).
3. Expose clean `StyledProperty` parameters (for generic UI primitives) or a dedicated ViewModel `x:DataType` (for feature panels/modals).
4. Document the new component in [COMPONENTS.md](../../../COMPONENTS.md) (directory tree + Section 3 usage guide + Section 4 MVVM hierarchy if applicable) and add it to the lookup tables in this `SKILL.md`.

---

## 4. Verification Checklist

After wiring or modifying any component:

```powershell
dotnet build Quell.csproj --configuration Release
dotnet run --configuration Release --no-build -- --verify
```
