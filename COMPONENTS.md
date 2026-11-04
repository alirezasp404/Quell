# Fdb Studio — Reusable UI Components & Integration Guide

## 1. Overview & Architecture

**Fdb Studio** is an enterprise-grade database management desktop IDE built with **.NET 10**, **Avalonia UI 12.1.1**, **FluentAvalonia 3.1.0**, **AvaloniaEdit 12.0.0**, and **Dapper 2.1.89**.

The application is structured around a **strictly modular, reusable component architecture**. Visual elements are isolated into self-contained `UserControl` components residing in the `Fdb.Components` namespace, accompanied by dedicated MVVM ViewModels, SQLite persistent services, and Microsoft Fluent vector iconography.

### Core Principles
1. **Zero Emojis / Native Vector Icons:** All graphical glyphs strictly utilize Microsoft Fluent vector symbols (`FluentAvalonia.UI.Controls.FASymbolIcon`) and `PathIcon` vector geometries.
2. **Encapsulation & Reusability:** Every component owns its visual hierarchy, templates, and event routing, and exposes clean public APIs, styled properties, and routed events.
3. **Decoupled MVVM:** ViewModels never directly reference Avalonia UI controls. Data contexts and commands drive state changes.
4. **Resilient Autocomplete:** Avoids fragile third-party popup hosts; IntelliSense suggestions are delivered via dedicated in-tree overlay components.
5. **High-Performance Micro-ORM (Dapper):** All internal SQLite database interactions, object mappings, and query executions are powered by Dapper for maximum efficiency and clean parameterization.

---

## 2. Component Catalog Directory

```
Components/
├── Editor & IntelliSense:
│   ├── EditorComponent.axaml                   # Composed multi-tab SQL query editor workspace
│   ├── EditorTabStripComponent.axaml           # Reusable auto-expanding uniform tab strip with close & add actions
│   └── SqlIntelliSenseOverlayComponent.axaml   # Reusable floating SQL autocomplete & snippet card
│
├── Data Tables & Grids:
│   ├── QueryResultTableComponent.axaml         # Composed Excel-grade query results grid with alternating columns
│   ├── TableToolbarComponent.axaml             # Reusable table search filter, refresh, stats badge & export toolbar
│   ├── TablePaginationComponent.axaml          # Reusable selection status & pagination navigation bar
│   └── SchemaPropertiesTableComponent.axaml     # Database schema, table columns, and metadata viewer
│
├── Modals & Dialogs:
│   ├── ModalOverlayComponent.axaml             # Universal base modal backdrop & dialog card
│   ├── ConnectDatabaseModalComponent.axaml      # SQL Server connection configuration dialog
│   ├── ConfirmActionModalComponent.axaml       # Action confirmation & danger alert dialog
│   ├── ExportDataModalComponent.axaml          # Data export format (CSV/JSON/SQL/TSV) modal
│   └── SettingsModalComponent.axaml            # Composed preferences dialog (Theme + Syntax Colors)
│
├── Settings Sub-Components:
│   ├── SettingsCategoriesNavComponent.axaml    # Reusable categories navigation sidebar for settings (Appearance, Results Table, Customization, Shortcuts)
│   ├── ThemeSelectionCardsComponent.axaml      # Reusable Light, Dark, and System theme selection cards
│   ├── SyntaxColorSelectorComponent.axaml      # Reusable 6-preset SQL syntax keyword color palette
│   ├── LiveSyntaxPreviewComponent.axaml        # Reusable live code snippet preview with dynamic syntax highlighting
│   ├── TableCustomizationComponent.axaml       # Dedicated Query Results Table row height, font size, gutter, colors, and live preview
│   ├── PanelCustomizationComponent.axaml       # Workspace, Editor, Sidebar, and Table panel color customization & presets
│   └── ShortcutsSettingsComponent.axaml        # Dynamic keyboard shortcuts manager with SQLite persistence and key capture
│
├── Themes & Design System Components:
│   ├── Themes/ThemeTokens.axaml                # Componentized shared design metrics & typography
│   ├── Themes/LightTheme.axaml                 # Light theme semantic color tokens & table brushes
│   └── Themes/DarkTheme.axaml                  # Dark theme semantic color tokens & table brushes
│
├── Layout & Tool Windows:
│   ├── NavbarComponent.axaml                   # Composed top navigation bar
│   ├── MainMenuBarComponent.axaml              # Reusable native desktop menu bar, brand icon & connection badge
│   ├── MainToolbarComponent.axaml              # Reusable execution & document action toolbar
│   ├── SidebarComponent.axaml                  # Composed collapsible left panel (Object Explorer)
│   ├── ObjectExplorerEmptyStateComponent.axaml # Reusable empty state connection card for Object Explorer
│   ├── FooterComponent.axaml                   # Collapsible & Fullscreen bottom results panel
│   ├── StatusBarComponent.axaml                # Bottom status bar with connection badges
│   └── ToolWindowHeaderComponent.axaml         # Reusable header with title, close, and maximize actions
│
└── Splash & Startup:
    └── SplashScreenCardComponent.axaml         # Reusable frameless splash card with SQLite loading progress
```

---

## 3. How to Use Each Component (Usage Guides & Examples)

This section provides comprehensive documentation and copy-pasteable code examples for using every component in XAML and C#.

```xml
<!-- Add the XML namespace to your XAML root: -->
xmlns:components="using:Fdb.Components"
```

---

### 3.1 `SqlIntelliSenseOverlayComponent`
* **File:** [SqlIntelliSenseOverlayComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/SqlIntelliSenseOverlayComponent.axaml) | [SqlIntelliSenseOverlayComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/SqlIntelliSenseOverlayComponent.axaml.cs)
* **Description:** A floating IntelliSense suggestion card featuring SQL keyword prioritization (e.g., `SELECT` ranking #1 on typing 'S'), code snippet templates, vector icons, keyboard navigation, and live documentation previews.

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
  * `void SelectNext()`: Highlights the next item down the list.
  * `void SelectPrevious()`: Highlights the previous item up the list.
  * `void CommitSelected()`: Commits the active selection and fires `ItemCommitted`.
  * `void Hide()`: Dismisses the card and clears the current match set.

#### XAML Usage Example:
```xml
<Grid>
    <!-- Code Editor Canvas -->
    <ae:TextEditor Name="MyTextEditor" />

    <!-- Overlay Canvas hosting the Reusable IntelliSense Component -->
    <Canvas Name="OverlayCanvas" ClipToBounds="True">
        <components:SqlIntelliSenseOverlayComponent x:Name="IntelliSenseOverlay"
                                                    IsVisible="False"
                                                    ItemCommitted="OnIntelliSenseItemCommitted" />
    </Canvas>
</Grid>
```

#### C# Integration Example:
```csharp
// 1. Show suggestions when typing
private void OnTextEntered(object? sender, TextInputEventArgs e)
{
    string word = GetCurrentWord();
    if (word.Length >= 1)
    {
        if (IntelliSenseOverlay.UpdateFilter(word))
        {
            Point caretPos = GetCaretPoint();
            IntelliSenseOverlay.PositionAt(caretPos, 18.0, OverlayCanvas.Bounds.Size);
        }
    }
    else
    {
        IntelliSenseOverlay.Hide();
    }
}

// 2. Intercept keyboard navigation
private void OnKeyDown(object? sender, KeyEventArgs e)
{
    if (IntelliSenseOverlay.IsVisible)
    {
        if (e.Key == Key.Down) { IntelliSenseOverlay.SelectNext(); e.Handled = true; }
        else if (e.Key == Key.Up) { IntelliSenseOverlay.SelectPrevious(); e.Handled = true; }
        else if (e.Key is Key.Tab or Key.Enter) { IntelliSenseOverlay.CommitSelected(); e.Handled = true; }
        else if (e.Key == Key.Escape) { IntelliSenseOverlay.Hide(); e.Handled = true; }
    }
}

// 3. Handle item insertion
private void OnIntelliSenseItemCommitted(object? sender, SqlCompletionData item)
{
    ReplaceCurrentWordWith(item.InsertionText);
    MyTextEditor.TextArea.Focus();
}
```

---

### 3.2 `EditorComponent`
* **File:** [EditorComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/EditorComponent.axaml) | [EditorComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/EditorComponent.axaml.cs)
* **DataContext:** `EditorViewModel`
* **Description:** A multi-tab SQL document editor featuring isolated per-tab undo/redo history, auto-expanding uniform tabs, blue syntax highlighting (`#2563EB`), zooming shortcuts, and built-in IntelliSense autocomplete.

#### Features & Shortcuts:
* `Ctrl + +` / `Ctrl + =`: Zoom in (increases font size by 1px up to 40px).
* `Ctrl + -`: Zoom out (decreases font size by 1px down to 8px).
* `Ctrl + 0`: Reset zoom (restores font size to 13px).
* `Ctrl + MouseWheel`: Smooth real-time font size zooming.
* `Ctrl + Space`: Manually opens IntelliSense suggestions.
* Auto-triggering autocomplete when typing any word character (`S` $\rightarrow$ suggests `SELECT`).

#### XAML Usage Example:
```xml
<components:EditorComponent x:Name="MainEditor" 
                            DataContext="{Binding Editor}" />
```

#### C# Usage Example:
```csharp
var editorVm = new EditorViewModel();
editorVm.AddTab("Query 1.sql", "SELECT * FROM Users WHERE status = 'Active';");
editorVm.AddTab("Query 2.sql", "SELECT id, email, created_at FROM Orders;");

var editorComponent = new EditorComponent { DataContext = editorVm };

// Access zoom methods programmatically:
editorComponent.ZoomIn();
editorComponent.ZoomOut();
editorComponent.ResetZoom();
```

---

### 3.3 `QueryResultTableComponent`
* **File:** [QueryResultTableComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/QueryResultTableComponent.axaml) | [QueryResultTableComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/QueryResultTableComponent.axaml.cs)
* **DataContext:** `QueryResultTableViewModel`
* **Description:** An Excel-grade, high-performance database query results grid with compact 25px row heights, dynamic alternating column backgrounds, an explicit `ROW #` gutter column, quick search filter, execution metrics, and full data export capabilities. Fully adapted to both Light and Dark themes via semantic brush bindings without visual bugs or contrast issues.

#### Features:
* **Compact Row Height (`25px`):** Dense display maximizing vertical data visibility.
* **Theme-Aware Alternating Columns:** Odd and even columns adapt seamlessly (`AppTableWhiteColBackgroundBrush` & `AppTableAltColBackgroundBrush`), maintaining optimal reading contrast in both Light and Dark themes.
* **Theme-Aware Selection & Active Border:** Selected cells and active focused cells bind to dynamic theme resources (`AppTableSelectedBackgroundBrush`, `AppTableActiveBorderBrush`), completely eliminating hardcoded color artifacts.
* **Row Number Column (`ROW #`):** High-contrast centered row numbering with a dedicated vertical boundary line (`AppTableDividerBrush`).
* **Interactive Context Menu:** Copy Cell (`Ctrl+C`), Copy Entire Row, Copy as JSON, Copy as CSV.
* **Fullscreen Mode:** Includes a dedicated Fullscreen/Maximize button that expands the table across the entire workspace.

#### XAML Usage Example:
```xml
<components:QueryResultTableComponent x:Name="ResultsGrid" 
                                      DataContext="{Binding SampleQueryResult}" />
```

#### C# Usage Example:
```csharp
var tableVm = new QueryResultTableViewModel();

// Configure column definitions
tableVm.Columns.Add(new QueryResultColumn { Header = "ID", DataType = "INT" });
tableVm.Columns.Add(new QueryResultColumn { Header = "NAME", DataType = "NVARCHAR" });
tableVm.Columns.Add(new QueryResultColumn { Header = "EMAIL", DataType = "NVARCHAR" });

// Populate row data
tableVm.Rows.Add(new QueryResultRow(1, ["101", "Alice Johnson", "alice@example.com"]));
tableVm.Rows.Add(new QueryResultRow(2, ["102", "Bob Smith", "bob@example.com"]));

// Bind to component
var tableControl = new QueryResultTableComponent { DataContext = tableVm };
```

---

### 3.4 `SchemaPropertiesTableComponent`
* **File:** [SchemaPropertiesTableComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/SchemaPropertiesTableComponent.axaml) | [SchemaPropertiesTableComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/SchemaPropertiesTableComponent.axaml.cs)
* **DataContext:** `SchemaPropertiesTableViewModel`
* **Description:** Interactive table schema metadata viewer showing primary keys, foreign keys, data types, nullability, default values, and column descriptions with compact 25px alternating rows.

#### XAML Usage Example:
```xml
<components:SchemaPropertiesTableComponent DataContext="{Binding SampleSchema}" />
```

#### C# Usage Example:
```csharp
var schemaVm = new SchemaPropertiesTableViewModel
{
    TableName = "dbo.Customers",
    RowCount = 12450
};

schemaVm.Columns.Add(new SchemaColumnItem
{
    Ordinal = 1,
    Name = "CustomerID",
    DataType = "INT",
    IsPrimaryKey = true,
    IsNullable = false
});

var schemaControl = new SchemaPropertiesTableComponent { DataContext = schemaVm };
```

---

### 3.5 `ModalOverlayComponent` (Universal Modal Base Card)
* **File:** [ModalOverlayComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/ModalOverlayComponent.axaml) | [ModalOverlayComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/ModalOverlayComponent.axaml.cs)
* **Description:** The foundational reusable modal card supporting arbitrary custom content via `ModalContent`, an interactive backdrop dismissal, keyboard `Escape` handling, and elevated `ZIndex`.

#### Styled Properties:
* `IsOpen` (`bool`): Controls modal visibility and backdrop.
* `Title` (`string`): Header title text.
* `Subtitle` (`string?`): Secondary guidance text below the title.
* `DialogWidth` (`double`, default: `520`): Width of the modal card.
* `CloseCommand` (`ICommand?`): Invoked on backdrop click or close button.
* `PrimaryButtonText` (`string?`): Text for primary action button.
* `PrimaryCommand` (`ICommand?`): Bound to primary action.
* `SecondaryButtonText` / `SecondaryCommand`: Optional left action button.
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
    <components:ModalOverlayComponent.ModalContent>
        <StackPanel Spacing="12" Margin="4">
            <TextBlock Text="Sync Interval (Minutes):" FontWeight="SemiBold"/>
            <NumericUpDown Value="15" Minimum="1" Maximum="1440"/>
            <CheckBox Content="Enable Automatic Schema Reload" IsChecked="True"/>
        </StackPanel>
    </components:ModalOverlayComponent.ModalContent>
</components:ModalOverlayComponent>
```

---

### 3.6 `ConnectDatabaseModalComponent`
* **File:** [ConnectDatabaseModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/ConnectDatabaseModalComponent.axaml) | [ConnectDatabaseModalComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/ConnectDatabaseModalComponent.axaml.cs)
* **DataContext:** `ConnectDatabaseModalViewModel`
* **Description:** Dedicated SQL Server connection dialog supporting host, port, database, authentication modes, encryption options, and live connection testing with status feedback.

#### XAML Usage Example:
```xml
<components:ConnectDatabaseModalComponent DataContext="{Binding ConnectModal}" />
```

#### Triggering Connection:
```csharp
// Open modal
mainVm.ConnectModal.IsOpen = true;

// Programmatically test connection
await mainVm.ConnectModal.TestConnectionCommand.ExecuteAsync(null);
Console.WriteLine(mainVm.ConnectModal.StatusMessage);
```

---

### 3.7 `ConfirmActionModalComponent`
* **File:** [ConfirmActionModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/ConfirmActionModalComponent.axaml) | [ConfirmActionModalComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/ConfirmActionModalComponent.axaml.cs)
* **DataContext:** `ConfirmActionModalViewModel`
* **Description:** Danger alert dialog for destructive actions (`Disconnect`, `DROP`, `TRUNCATE`) featuring red warning accents, target resource tags, and confirmation commands.

#### XAML Usage Example:
```xml
<components:ConfirmActionModalComponent DataContext="{Binding ConfirmModal}" />
```

#### Triggering Confirmation:
```csharp
mainVm.ConfirmModal.Open(
    actionName: "Disconnect Session",
    targetName: "localhost:1433 (master)",
    warningMessage: "Are you sure you want to terminate the active connection? Any uncommitted transactions will be rolled back.",
    confirmCommand: new RelayCommand(() => DisconnectActiveDatabase())
);
```

---

### 3.8 `ExportDataModalComponent`
* **File:** [ExportDataModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/ExportDataModalComponent.axaml) | [ExportDataModalComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/ExportDataModalComponent.axaml.cs)
* **DataContext:** `ExportDataModalViewModel`
* **Description:** Data export configuration dialog supporting CSV, JSON, SQL INSERT, and TSV formats with native OS file dialog integration.

#### XAML Usage Example:
```xml
<components:ExportDataModalComponent DataContext="{Binding ExportModal}" />
```

---

### 3.9 `SettingsModalComponent` (Appearance, Theme & Syntax Colors)
* **File:** [SettingsModalComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/SettingsModalComponent.axaml) | [SettingsModalComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/SettingsModalComponent.axaml.cs)
* **DataContext:** `SettingsModalViewModel`
* **Description:** Application preferences dialog featuring real-time color theme switching (`Light`, `Dark`, `System Default`) and SQL syntax keyword color customization, backed by SQLite persistent storage (`AppSettings` table via Dapper) and automatic loading during the startup splash screen.

#### Features:
1. **Interactive Workspace Theme Selection:**
   * **Light Theme:** Clean, bright desktop workspace with high-contrast text.
   * **Dark Theme:** Modern dark mode workspace with deep slate backgrounds (`#0F172A`, `#1E293B`) and blue accents.
   * **System Default:** Synchronizes dynamically with the host operating system's dark/light preference.
2. **SQL Syntax & Keyword Highlighting Color Customizer:**
   * Allows users to customize the highlight color of SQL keywords (`SELECT`, `FROM`, `WHERE`) and IntelliSense suggestion badges.
   * **6 Curated High-Contrast Presets:**
     * **Royal Blue:** Light `#2563EB` | Dark `#60A5FA` (Default Studio Blue)
     * **Emerald Green:** Light `#059669` | Dark `#34D399` (Mint & Emerald)
     * **Violet Purple:** Light `#7C3AED` | Dark `#A78BFA` (Vibrant Lavender & Violet)
     * **Amber Gold:** Light `#D97706` | Dark `#FBBF24` (Golden Amber & Honey)
     * **Crimson Rose:** Light `#E11D48` | Dark `#FB7185` (Ruby Crimson & Rose)
     * **Cyan Teal:** Light `#0891B2` | Dark `#38BDF8` (Oceanic Teal & Cyan)
   * **Automatic Tone & Contrast Preservation:** Switching between Light and Dark themes dynamically re-tints the chosen keyword color to guarantee maximum legibility against the active background without contrast degradation.
   * **Live Code Preview:** Shows a real-time mini code snippet in the settings dialog demonstrating the chosen keyword color.
3. **Instant Live Preview:** Changes apply immediately across the text editor, IntelliSense overlay, and UI components without requiring a restart.
4. **SQLite Database Persistence (Dapper):** Both `Theme` and `KeywordColor` are persisted to the `AppSettings` table via Dapper SQL commands.
5. **Splash Screen Auto-Load:** The saved theme and syntax color are pre-loaded from SQLite during the splash screen startup sequence.
6. **Access Shortcuts:**
   * Top Navbar: `File` $\rightarrow$ `Settings...`
   * Keyboard Shortcut: `Ctrl+,` (Ctrl + Comma)
   * Dismissal: `Escape` key or backdrop click cancels any unsaved preview and restores the saved theme.

#### XAML Usage Example:
```xml
<components:SettingsModalComponent Grid.Row="0" Grid.RowSpan="3" ZIndex="103" 
                                  DataContext="{Binding SettingsModal}" />
```

#### C# Usage Example:
```csharp
// Open settings dialog programmatically
await mainVm.SettingsModal.OpenAsync();

// Switch to Dark theme and Emerald Green syntax color
mainVm.SettingsModal.SelectTheme("Dark");
mainVm.SettingsModal.SelectKeywordColor("Green");

// Persist both settings to SQLite and close dialog
await mainVm.SettingsModal.SaveAsync();
```

---

### 3.10 Theme Components & Color Tokens
* **Files:**
  * [ThemeTokens.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Themes/ThemeTokens.axaml): Shared corner radii, table row heights (`25px`), and typography.
  * [LightTheme.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Themes/LightTheme.axaml): Light mode semantic color tokens and table brushes.
  * [DarkTheme.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Themes/DarkTheme.axaml): Dark mode semantic color tokens and table brushes.
* **Description:** Modular AXAML resource dictionaries componentizing the application design system. Each theme is an independent component loaded into `Application.Resources.ThemeDictionaries`.

#### Usage in App.axaml:
```xml
<Application.Resources>
    <ResourceDictionary>
        <ResourceDictionary.MergedDictionaries>
            <ResourceInclude Source="avares://Fdb/Themes/ThemeTokens.axaml" />
        </ResourceDictionary.MergedDictionaries>
        <ResourceDictionary.ThemeDictionaries>
            <ResourceInclude x:Key="Light" Source="avares://Fdb/Themes/LightTheme.axaml" />
            <ResourceInclude x:Key="Dark" Source="avares://Fdb/Themes/DarkTheme.axaml" />
        </ResourceDictionary.ThemeDictionaries>
    </ResourceDictionary>
</Application.Resources>
```

---

### 3.10 `ToolWindowHeaderComponent`
* **File:** [ToolWindowHeaderComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/ToolWindowHeaderComponent.axaml) | [ToolWindowHeaderComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/ToolWindowHeaderComponent.axaml.cs)
* **Description:** Reusable title header for dockable panels and tool windows with built-in Fullscreen/Maximize and Close buttons.

#### Styled Properties:
* `Title` (`string`): Panel title (e.g. `"OBJECT EXPLORER"`, `"QUERY RESULTS"`).
* `IsMaximized` (`bool`): Toggles between Fullscreen (`FullScreen`) and Restore (`BackToWindow`) vector icons.
* `IsMaximizeVisible` (`bool`, default: `true`): Controls visibility of the Maximize button.
* `CloseCommand` (`ICommand?`): Invoked when clicking the close (`✕`) button.
* `MaximizeCommand` (`ICommand?`): Invoked when clicking the maximize button.

#### XAML Usage Example:
```xml
<components:ToolWindowHeaderComponent Title="QUERY RESULTS"
                                      IsMaximized="{Binding Footer.IsMaximized}"
                                      MaximizeCommand="{Binding ToggleFooterMaximizedCommand}"
                                      CloseCommand="{Binding ToggleFooterCommand}" />
```

---

### 3.11 `NavbarComponent`
* **File:** [NavbarComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/NavbarComponent.axaml) | [NavbarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/NavbarComponent.axaml.cs)
* **DataContext:** `NavbarViewModel`
* **Description:** The top navigation bar containing native desktop menus (`File`, `Edit`, `View`, `Window`, `Help`) and action shortcuts.

#### XAML Usage Example:
```xml
<components:NavbarComponent Grid.Row="0" 
                            DataContext="{Binding Navbar}" />
```

---

### 3.12 `SidebarComponent`
* **File:** [SidebarComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/SidebarComponent.axaml) | [SidebarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/SidebarComponent.axaml.cs)
* **DataContext:** `SidebarViewModel`
* **Description:** The left-hand tool panel hosting the Database Object Explorer and schema tree views.

#### XAML Usage Example:
```xml
<components:SidebarComponent Grid.Column="0" 
                             DataContext="{Binding Sidebar}" />
```

---

### 3.13 `FooterComponent`
* **File:** [FooterComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/FooterComponent.axaml) | [FooterComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/FooterComponent.axaml.cs)
* **DataContext:** `FooterViewModel`
* **Description:** The collapsible and maximizable bottom dock housing query output tables, execution statistics, and logs.

#### XAML Usage Example:
```xml
<components:FooterComponent Grid.Row="2" 
                            DataContext="{Binding Footer}" />
```

---

### 3.14 `StatusBarComponent`
* **File:** [StatusBarComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/StatusBarComponent.axaml) | [StatusBarComponent.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Components/StatusBarComponent.axaml.cs)
* **Description:** Anchored application footer bar displaying active database connection, server version, execution time, and keyboard layout indicators.

#### XAML Usage Example:
```xml
<components:StatusBarComponent Grid.Row="2" />
```

---

### 3.15 `SplashScreenCardComponent` & Startup Pre-load Sequence
* **File:** [SplashScreenCardComponent.axaml](file:///c:/Users/ehsan/Desktop/Fdb/Components/Splash/SplashScreenCardComponent.axaml) | [SplashScreenWindow.axaml.cs](file:///c:/Users/ehsan/Desktop/Fdb/Views/SplashScreenWindow.axaml.cs)
* **DataContext:** `SplashScreenViewModel`
* **Description:** Reusable frameless splash card with high-resolution artwork and live real-time SQLite initialization diagnostics. Pre-loads all core app configurations (theme, keyword syntax color, panel customizations, table dimensions, keyboard shortcuts, dialect keywords, saved connections, and query history) into in-memory caches before displaying `MainWindow`, ensuring instant rendering with zero startup query lag.

#### Features:
1. **Unified Startup Pre-cache (`PreloadStartupDataAsync`):** Executes all initialization queries inside the splash screen sequence in `DatabaseStorageService.cs`.
2. **In-Memory Settings Caching:** `AppSettings` are read in a single batch query and served from an in-memory cache, eliminating multiple SQLite connection round-trips.
3. **Instant Object Explorer & Connections:** Saved connections and query history are pre-cached, populating the database target dropdown and sidebar tree nodes instantly upon startup.

#### XAML Usage Example:
```xml
<components:SplashScreenCardComponent DataContext="{Binding ViewModel}" />
```

---

## 4. MVVM Hierarchy & Data Flow

```
MainViewModel
├── Navbar: NavbarViewModel
├── Sidebar: SidebarViewModel
├── Editor: EditorViewModel
│     ├── SelectedTab: EditorTabItem
│     └── Tabs: ObservableCollection<EditorTabItem>
├── Footer: FooterViewModel (Manages IsMaximized & Fullscreen layout)
│
├── Modals:
│   ├── ConnectModal: ConnectDatabaseModalViewModel
│   ├── ConfirmModal: ConfirmActionModalViewModel
│   ├── ExportModal: ExportDataModalViewModel
│   └── SettingsModal: SettingsModalViewModel (Appearance & Theme preferences)
│
└── Tables:
    ├── SampleQueryResult: QueryResultTableViewModel
    └── SampleSchema: SchemaPropertiesTableViewModel
```

---

## 5. Verification & Test Suite

Fdb Studio includes an automated layout, component, and autocomplete verification suite in `Program.cs`. To verify all components and behaviors:

```powershell
dotnet run -- --verify
```

### Verified Checks:
1. **Dynamic Layout & Fullscreen Transitions:** Verifies sidebar collapse, footer collapse, and footer fullscreen maximize (`1*` Star takeover).
2. **Table Component Rendering:** Validates compact row heights (`25px`), alternating column striping, and `ROW #` numbering.
3. **Modal Dialog Lifecycles:** Tests trigger, backdrop dismissal, test connection execution, and confirm modals.
4. **Editor Tab State & Undo Isolation:** Verifies per-tab persistent undo history across tab switches.
5. **IntelliSense Autocomplete Overlay:** Validates that typing `S` displays `SELECT` at the top of the suggestion card, and pressing `Enter`/`Tab` cleanly commits the selection.
6. **Zero Emojis Enforced:** Scans all AXAML visual templates ensuring 100% compliance with Microsoft Fluent vector icons (`FASymbolIcon` / `PathIcon`).
7. **SQLite Engine & Dialects:** Queries SQLite database verifying multi-dialect keyword caches (`MSSQL`, `PostgreSQL`, `MySQL`, `SQLite`).
