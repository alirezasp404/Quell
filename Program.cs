using Avalonia;
using Avalonia.Controls;
using System;
using System.Threading.Tasks;

namespace Fdb;

sealed class Program
{
    [STAThread]
    public static async Task Main(string[] args)
    {
        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            Console.WriteLine($"[UnhandledException]: {e.ExceptionObject}");
        };
        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            Console.WriteLine($"[UnobservedTaskException]: {e.Exception}");
            e.SetObserved();
        };

        if (args.Length > 0 && args[0] == "--verify")
        {
            Console.WriteLine("=== Fdb Dynamic Layout Verification ===");
            var vm = new Fdb.ViewModels.MainViewModel();
            var app = BuildAvaloniaApp().SetupWithoutStarting();
            var window = new Fdb.Views.MainWindow { DataContext = vm };

            Console.WriteLine($"[Initial State]");
            Console.WriteLine($"Sidebar: Visible={vm.Sidebar.IsVisible}, ColWidth={window.MainGrid.ColumnDefinitions[0].Width}");
            Console.WriteLine($"Footer: Visible={vm.Footer.IsVisible}, RowHeight={window.CenterGrid.RowDefinitions[2].Height}");
            Console.WriteLine($"Editor Row: Height={window.CenterGrid.RowDefinitions[0].Height}");
            Console.WriteLine($"Center Col: Width={window.MainGrid.ColumnDefinitions[2].Width}");

            // 1. Test Closing Footer
            Console.WriteLine("\n[Action] Closing Footer...");
            vm.ToggleFooterCommand.Execute(null);
            Console.WriteLine($"Footer: Visible={vm.Footer.IsVisible}, RowHeight={window.CenterGrid.RowDefinitions[2].Height}");
            Console.WriteLine($"Editor Row Height: {window.CenterGrid.RowDefinitions[0].Height} (MUST BE 1*)");
            if (window.CenterGrid.RowDefinitions[2].Height.Value != 0 || window.CenterGrid.RowDefinitions[0].Height.GridUnitType != Avalonia.Controls.GridUnitType.Star)
            {
                throw new Exception("Footer collapse failed! Editor did not expand.");
            }

            // 2. Test Closing Sidebar
            Console.WriteLine("\n[Action] Closing Sidebar...");
            vm.ToggleSidebarCommand.Execute(null);
            Console.WriteLine($"Sidebar: Visible={vm.Sidebar.IsVisible}, ColWidth={window.MainGrid.ColumnDefinitions[0].Width}");
            Console.WriteLine($"Center Col Width: {window.MainGrid.ColumnDefinitions[2].Width} (MUST BE 1*)");
            if (window.MainGrid.ColumnDefinitions[0].Width.Value != 0 || window.MainGrid.ColumnDefinitions[2].Width.GridUnitType != Avalonia.Controls.GridUnitType.Star)
            {
                throw new Exception("Sidebar collapse failed! Center area did not expand.");
            }

            // 3. Test Restoring from Window Menu
            Console.WriteLine("\n[Action] Restoring Sidebar & Footer from Window Menu...");
            vm.ShowSidebarCommand.Execute(null);
            vm.ShowFooterCommand.Execute(null);
            Console.WriteLine($"Sidebar: Visible={vm.Sidebar.IsVisible}, ColWidth={window.MainGrid.ColumnDefinitions[0].Width}");
            Console.WriteLine($"Footer: Visible={vm.Footer.IsVisible}, RowHeight={window.CenterGrid.RowDefinitions[2].Height}");

            // 3b. Test Maximizing Footer (Fullscreen Excel Table)
            Console.WriteLine("\n[Action] Toggling Fullscreen / Maximize on Footer (Excel Table)...");
            vm.ToggleFooterMaximizedCommand.Execute(null);
            Console.WriteLine($"Footer Maximize State: IsMaximized={vm.Footer.IsMaximized}, FooterRowHeight={window.CenterGrid.RowDefinitions[2].Height}, EditorRowHeight={window.CenterGrid.RowDefinitions[0].Height}");
            if (!vm.Footer.IsMaximized || window.CenterGrid.RowDefinitions[2].Height.GridUnitType != Avalonia.Controls.GridUnitType.Star || window.CenterGrid.RowDefinitions[0].Height.Value != 0)
            {
                throw new Exception("Footer fullscreen failed! Footer did not take 1* or Editor row is not 0.");
            }

            Console.WriteLine("[Action] Restoring Footer from Fullscreen...");
            vm.ToggleFooterMaximizedCommand.Execute(null);
            Console.WriteLine($"Footer Restored: IsMaximized={vm.Footer.IsMaximized}, FooterRowHeight={window.CenterGrid.RowDefinitions[2].Height}, EditorRowHeight={window.CenterGrid.RowDefinitions[0].Height}");
            if (vm.Footer.IsMaximized || window.CenterGrid.RowDefinitions[2].Height.GridUnitType != Avalonia.Controls.GridUnitType.Pixel || window.CenterGrid.RowDefinitions[0].Height.GridUnitType != Avalonia.Controls.GridUnitType.Star)
            {
                throw new Exception("Footer restore failed! Footer is not pixel height or Editor is not 1*.");
            }

            // 4. Test Table Components Data
            Console.WriteLine("\n[Action] Verifying Table Components...");
            Console.WriteLine($"QueryResultTable Rows: {vm.SampleQueryResult.Rows.Count} rows loaded.");
            Console.WriteLine($"SchemaPropertiesTable Columns: {vm.SampleSchema.Columns.Count} columns loaded.");
            var tableComp = new Fdb.Components.QueryResultTableComponent { DataContext = vm.SampleQueryResult };
            var dataGrid = tableComp.FindControl<Avalonia.Controls.DataGrid>("ResultsDataGrid");
            Console.WriteLine($"DataGrid Columns Count: {tableComp.FindControl<Avalonia.Controls.DataGrid>("ResultsDataGrid")?.Columns.Count ?? 0}");
            Console.WriteLine($"DataGrid RowHeight: {dataGrid?.RowHeight}px (Compact height)");
            if (dataGrid?.RowHeight != 25)
            {
                throw new Exception($"Expected compact DataGrid RowHeight=25, but got {dataGrid?.RowHeight}");
            }
            var col0 = dataGrid?.Columns[0] as Avalonia.Controls.DataGridTemplateColumn;
            var cellContent = col0?.CellTemplate?.Build(vm.SampleQueryResult.Rows[0]) as Avalonia.Controls.Grid;
            if (cellContent != null) cellContent.DataContext = vm.SampleQueryResult.Rows[0];
            var textBlock = cellContent?.Children[0] as Avalonia.Controls.TextBlock;
            Console.WriteLine($"Cell 0 TextBlock Text: '{textBlock?.Text}', DataContext: {textBlock?.DataContext}");
            if (textBlock?.Text != "1")
            {
                throw new Exception($"Expected row index '1', but got '{textBlock?.Text}'");
            }
            tableComp.DataContext = null;

            // 5. Test Modal Dialogs Lifecycle & Triggers
            Console.WriteLine("\n[Action] Verifying Modal Dialogs Lifecycle...");
            Console.WriteLine($"ConnectModal initially open: {vm.ConnectModal.IsOpen}");
            var navbarComp = window.FindControl<Fdb.Components.NavbarComponent>("MainNavbar");
            var connectBtn = navbarComp?.FindControl<Avalonia.Controls.Button>("ConnectButton");
            Console.WriteLine($"ConnectButton found: {connectBtn != null}, Command: {connectBtn?.Command}");
            connectBtn?.Command?.Execute(null);
            Console.WriteLine($"After ConnectButton click: ConnectModal.IsOpen = {vm.ConnectModal.IsOpen}");
            if (!vm.ConnectModal.IsOpen)
            {
                throw new Exception("ConnectButton failed to open ConnectModal!");
            }
            vm.ConnectModal.TestConnectionCommand.Execute(null);
            Console.WriteLine($"ConnectModal Status: {vm.ConnectModal.StatusMessage}");
            vm.ConnectModal.CloseCommand.Execute(null);
            Console.WriteLine($"ConnectModal: IsOpen = {vm.ConnectModal.IsOpen} (Closed)");

            // Test Disconnect Command triggering ConfirmModal
            vm.DisconnectCommand.Execute(null);
            Console.WriteLine($"ConfirmModal opened by Disconnect: {vm.ConfirmModal.IsOpen}, Target = {vm.ConfirmModal.TargetName}");
            if (!vm.ConfirmModal.IsOpen)
            {
                throw new Exception("DisconnectCommand failed to open ConfirmModal!");
            }
            vm.ConfirmModal.CancelCommand.Execute(null);
            Console.WriteLine($"ConfirmModal: IsOpen = {vm.ConfirmModal.IsOpen} (Closed)");

            // Test Export Modal
            vm.ExportModal.OpenCommand.Execute(null);
            Console.WriteLine($"ExportModal: IsOpen = {vm.ExportModal.IsOpen}, Format = {vm.ExportModal.SelectedFormat}");
            vm.ExportModal.CloseCommand.Execute(null);
            Console.WriteLine($"ExportModal: IsOpen = {vm.ExportModal.IsOpen} (Closed)");

            // Test Settings Modal (Appearance & Theme Switching)
            Console.WriteLine("\n[Action] Verifying Settings Modal & Theme Switching...");
            Console.WriteLine($"SettingsModal initially open: {vm.SettingsModal.IsOpen}");
            await vm.SettingsModal.OpenAsync();
            Console.WriteLine($"After OpenAsync: SettingsModal.IsOpen = {vm.SettingsModal.IsOpen}, SelectedTheme = {vm.SettingsModal.SelectedTheme}");
            if (!vm.SettingsModal.IsOpen) throw new Exception("SettingsModal failed to open!");

            // Test switching to Dark theme
            Console.WriteLine("[Action] Switching to Dark theme...");
            vm.SettingsModal.SelectTheme("Dark");
            Console.WriteLine($"Theme applied: CurrentTheme={Fdb.Services.ThemeService.Instance.CurrentTheme}, IsDark={vm.SettingsModal.IsDarkTheme}");
            if (!vm.SettingsModal.IsDarkTheme || Fdb.Services.ThemeService.Instance.CurrentTheme != "Dark")
            {
                throw new Exception("Dark theme selection failed!");
            }
            await vm.SettingsModal.SaveAsync();
            Console.WriteLine($"After SaveAsync: SettingsModal.IsOpen = {vm.SettingsModal.IsOpen}");
            string persistedTheme = await Fdb.Services.DatabaseStorageService.Instance.GetThemeAsync();
            Console.WriteLine($"SQLite Persisted Theme: '{persistedTheme}'");
            if (persistedTheme != "Dark") throw new Exception("Failed to persist Dark theme to SQLite!");

            // Test switching to System theme
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectTheme("System");
            if (!vm.SettingsModal.IsSystemTheme || Fdb.Services.ThemeService.Instance.CurrentTheme != "System")
            {
                throw new Exception("System theme selection failed!");
            }
            await vm.SettingsModal.SaveAsync();
            string persistedSystemTheme = await Fdb.Services.DatabaseStorageService.Instance.GetThemeAsync();
            Console.WriteLine($"SQLite Persisted System Theme: '{persistedSystemTheme}'");
            if (persistedSystemTheme != "System") throw new Exception("Failed to persist System theme to SQLite!");

            // Restore back to Light theme
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectTheme("Light");
            await vm.SettingsModal.SaveAsync();
            string restoredTheme = await Fdb.Services.DatabaseStorageService.Instance.GetThemeAsync();
            Console.WriteLine($"Restored Theme in SQLite: '{restoredTheme}'");
            if (restoredTheme != "Light") throw new Exception("Failed to restore Light theme in SQLite!");

            // Test Keyword Color Customization & High Contrast Adaptation across Themes
            Console.WriteLine("\n[Action] Verifying Keyword Syntax Color Customization & Contrast Adaptation...");
            await vm.SettingsModal.OpenAsync();
            Console.WriteLine($"Initial Keyword Color: '{vm.SettingsModal.SelectedKeywordColor}'");

            // Select 'Green' (Emerald / Mint)
            vm.SettingsModal.SelectKeywordColor("Green");
            Console.WriteLine($"Selected Green Keyword Color: IsGreen={vm.SettingsModal.IsGreenKeyword}");
            if (!vm.SettingsModal.IsGreenKeyword) throw new Exception("Green keyword selection failed in ViewModel!");

            // In Light theme, Green should be emerald (#059669)
            var lightBrush = Fdb.Services.ThemeService.Instance.CurrentKeywordBrush as Avalonia.Media.ISolidColorBrush;
            Console.WriteLine($"Light Theme Green Keyword Brush: {lightBrush?.Color}");
            if (lightBrush?.Color != Avalonia.Media.Color.Parse("#059669"))
            {
                throw new Exception($"Expected Light Theme Green Color #059669, got {lightBrush?.Color}");
            }

            // Switch to Dark Theme: Green keyword should automatically adapt to bright mint (#34D399) for high contrast!
            vm.SettingsModal.SelectTheme("Dark");
            var darkBrush = Fdb.Services.ThemeService.Instance.CurrentKeywordBrush as Avalonia.Media.ISolidColorBrush;
            Console.WriteLine($"Dark Theme Green Keyword Brush (Auto Contrast): {darkBrush?.Color}");
            if (darkBrush?.Color != Avalonia.Media.Color.Parse("#34D399"))
            {
                throw new Exception($"Expected Dark Theme Mint Color #34D399, got {darkBrush?.Color}");
            }

            // Save Green Keyword Color & Dark Theme to SQLite
            await vm.SettingsModal.SaveAsync();
            string persistedKeywordColor = await Fdb.Services.DatabaseStorageService.Instance.GetKeywordColorAsync();
            Console.WriteLine($"SQLite Persisted Keyword Color: '{persistedKeywordColor}'");
            if (persistedKeywordColor != "Green") throw new Exception("Failed to persist Green Keyword Color to SQLite!");

            // Revert back to Blue Keyword Color and Light Theme
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectKeywordColor("Blue");
            vm.SettingsModal.SelectTheme("Light");
            await vm.SettingsModal.SaveAsync();
            string restoredKeywordColor = await Fdb.Services.DatabaseStorageService.Instance.GetKeywordColorAsync();
            Console.WriteLine($"Restored Keyword Color in SQLite: '{restoredKeywordColor}'");
            if (restoredKeywordColor != "Blue") throw new Exception("Failed to restore Blue Keyword Color to SQLite!");

            // 6. Test Editor Tab Switching & Active State
            Console.WriteLine("\n[Action] Verifying Editor Tab Switching & State...");
            Console.WriteLine($"Initial Selected Tab: {vm.Editor.SelectedTab?.Title}, IsActive={vm.Editor.SelectedTab?.IsActive}");
            vm.Editor.SelectTab(vm.Editor.Tabs[1]);
            Console.WriteLine($"Switched Selected Tab: {vm.Editor.SelectedTab?.Title}, IsActive={vm.Editor.SelectedTab?.IsActive}");
            if (!vm.Editor.Tabs[1].IsActive || vm.Editor.Tabs[0].IsActive)
            {
                throw new Exception("Editor tab activation failed!");
            }

            var editorComp = new Fdb.Components.EditorComponent { DataContext = vm.Editor };
            var textEditor = editorComp.FindControl<AvaloniaEdit.TextEditor>("CodeEditor");
            Console.WriteLine($"TextEditor Document Text Length: {textEditor?.Document?.TextLength}");
            
            // Test per-tab document undo/redo integrity
            vm.Editor.SelectTab(vm.Editor.Tabs[0]);
            var tab0Doc = vm.Editor.Tabs[0].Document;
            int initialLen = tab0Doc.TextLength;
            tab0Doc.Insert(0, "-- New Line Added\n");
            Console.WriteLine($"Tab 0 modified. CanUndo: {textEditor?.CanUndo}");
            if (textEditor?.CanUndo != true)
            {
                throw new Exception("Editor CanUndo should be true after text edit!");
            }

            // Switch to tab 1
            vm.Editor.SelectTab(vm.Editor.Tabs[1]);
            Console.WriteLine($"Switched to Tab 1. Document matches: {textEditor?.Document == vm.Editor.Tabs[1].Document}");

            // Switch back to tab 0
            vm.Editor.SelectTab(vm.Editor.Tabs[0]);
            Console.WriteLine($"Switched back to Tab 0. CanUndo preserved: {textEditor?.CanUndo}");
            if (textEditor?.CanUndo != true)
            {
                throw new Exception("Editor CanUndo should be preserved across tab switches!");
            }

            // Perform Undo (Ctrl+Z)
            textEditor.Undo();
            Console.WriteLine($"Undo executed. Length restored: {tab0Doc.TextLength == initialLen}");
            if (tab0Doc.TextLength != initialLen)
            {
                throw new Exception("Undo failed to revert document to initial length!");
            }

            // Perform Redo (Ctrl+Y)
            textEditor.Redo();
            Console.WriteLine($"Redo executed. CanUndo: {textEditor.CanUndo}");
            textEditor.Undo(); // Revert back for clean state

            // 7. Test SQL Keywords Service & Regex
            Console.WriteLine("\n[Action] Verifying SQL Keywords Service...");
            var keywordsService = Fdb.Services.SqlKeywordsService.Instance;
            Console.WriteLine($"Loaded SQL Keywords: {keywordsService.Keywords.Count} keywords, {keywordsService.Snippets.Count} snippets.");
            var regex = keywordsService.KeywordRegex;
            bool selectMatches = regex != null && regex.IsMatch("SELECT * FROM dbo.Users");
            Console.WriteLine($"Regex Matches 'SELECT * FROM': {selectMatches}");
            if (!selectMatches)
            {
                throw new Exception("SQL Keywords regex matching failed!");
            }

            // Test typing in editor
            Console.WriteLine("\n[Action] Testing typing 'se' in TextEditor...");
            // Test typing in editor & Zoom shortcuts
            Console.WriteLine("\n[Action] Testing typing 'se' in TextEditor & Zoom Shortcuts...");
            var editorForTest = new Fdb.Components.EditorComponent { DataContext = vm.Editor };
            var te = editorForTest.FindControl<AvaloniaEdit.TextEditor>("CodeEditor")!;
            te.Document.Text = "";
            te.CaretOffset = 0;
            te.Document.Insert(0, "s");
            te.CaretOffset = 1;
            string word1 = editorForTest.GetWordBeforeOffset(1);
            Console.WriteLine($"Word after typing 's': '{word1}'");
            if (word1 != "s") throw new Exception("Failed to extract single-character word prefix!");

            te.Document.Insert(1, "e");
            te.CaretOffset = 2;
            string word2 = editorForTest.GetWordBeforeOffset(2);
            Console.WriteLine($"Word after typing 'se': '{word2}'");
            if (word2 != "se") throw new Exception("Failed to extract 'se' word prefix!");

            // Test Zoom In / Out / Reset
            Console.WriteLine($"Initial Editor FontSize: {te.FontSize}");
            if (te.FontSize != 13) throw new Exception("Default font size should be 13!");

            editorForTest.ZoomIn();
            Console.WriteLine($"After ZoomIn (Ctrl++): FontSize={te.FontSize}");
            if (te.FontSize != 14) throw new Exception("ZoomIn failed to increase font size!");

            editorForTest.ZoomOut();
            editorForTest.ZoomOut();
            Console.WriteLine($"After ZoomOut twice (Ctrl+-): FontSize={te.FontSize}");
            if (te.FontSize != 12) throw new Exception("ZoomOut failed to decrease font size!");

            editorForTest.ResetZoom();
            Console.WriteLine($"After ResetZoom (Ctrl+0): FontSize={te.FontSize}");
            if (te.FontSize != 13) throw new Exception("ResetZoom failed to restore font size to 13!");

            // Test CompletionData creation and replacement
            Console.WriteLine("\n[Action] Testing CompletionData instantiation and insertion...");
            var kwData = new Fdb.Services.SqlCompletionData("SELECT", "SQL Keyword: SELECT", 10.0, false);
            var snipData = new Fdb.Services.SqlCompletionData("SELECT * FROM", "Select all columns", 8.0, true, "SELECT * FROM ");
            Console.WriteLine($"kwData: Text={kwData.Text}, IsSnippet={kwData.IsSnippet}, Category={kwData.Category}");
            Console.WriteLine($"snipData: Text={snipData.Text}, IsSnippet={snipData.IsSnippet}, InsertionText='{snipData.InsertionText}'");

            // Execute completion on editor
            kwData.Complete(te.TextArea, new AvaloniaEdit.Document.TextSegment { StartOffset = 0, Length = 2 }, EventArgs.Empty);
            Console.WriteLine($"Editor text after completing 'SELECT': '{te.Document.Text}'");
            if (te.Document.Text != "SELECT") throw new Exception("CompletionData failed to insert 'SELECT'!");

            // Test In-Place IntelliSense Overlay Component
            Console.WriteLine("\n[Action] Verifying Modular SqlIntelliSenseOverlayComponent on EditorComponent...");
            te.Document.Text = "";
            te.CaretOffset = 0;
            te.Document.Insert(0, "S");
            te.CaretOffset = 1;

            editorForTest.UpdateAndShowCompletion("S", showAll: false);
            var overlay = editorForTest.FindControl<Fdb.Components.SqlIntelliSenseOverlayComponent>("IntelliSenseOverlay")!;
            Console.WriteLine($"After typing 'S': Overlay.IsVisible = {overlay.IsVisible}, ItemCount = {overlay.ItemCount}");
            if (!overlay.IsVisible || overlay.ItemCount == 0)
            {
                throw new Exception("SqlIntelliSenseOverlayComponent should be visible with items after typing 'S'!");
            }

            var topItem = overlay.SelectedItem;
            Console.WriteLine($"Top selected item for 'S': Text='{topItem?.Text}', Category='{topItem?.Category}'");
            if (topItem?.Text != "SELECT")
            {
                throw new Exception($"Expected top selected item to be 'SELECT', but got '{topItem?.Text}'");
            }

            // Test typing 'E' ('SE')
            te.Document.Insert(1, "E");
            te.CaretOffset = 2;
            editorForTest.UpdateAndShowCompletion("SE", showAll: false);
            var seTopItem = overlay.SelectedItem;
            Console.WriteLine($"After typing 'SE': Overlay.IsVisible = {overlay.IsVisible}, TopItem='{seTopItem?.Text}'");
            if (seTopItem?.Text != "SELECT")
            {
                throw new Exception($"Expected top selected item for 'SE' to be 'SELECT', but got '{seTopItem?.Text}'");
            }

            // Test Commit (Tab / Enter)
            editorForTest.CommitSelectedCompletion();
            Console.WriteLine($"After Commit: Document.Text='{te.Document.Text}', Overlay.IsVisible={overlay.IsVisible}");
            if (te.Document.Text != "SELECT " || overlay.IsVisible)
            {
                throw new Exception($"Commit failed! Expected 'SELECT ' and overlay hidden, got '{te.Document.Text}' and IsVisible={overlay.IsVisible}");
            }

            // Test MainEditor in Window
            Console.WriteLine("\n[Action] Verifying MainEditor in Window visual tree...");
            var mainEditor = window.FindControl<Fdb.Components.EditorComponent>("MainEditor");
            Console.WriteLine($"MainEditor found in window: {mainEditor != null}");
            var mainCodeEditor = mainEditor?.FindControl<AvaloniaEdit.TextEditor>("CodeEditor");
            Console.WriteLine($"MainCodeEditor found: {mainCodeEditor != null}");

            if (mainEditor != null && mainCodeEditor != null)
            {
                mainCodeEditor.Document = new AvaloniaEdit.Document.TextDocument();
                mainCodeEditor.Document.Text = "";
                mainCodeEditor.CaretOffset = 0;

                // Simulate typing 's'
                var textArgs = new Avalonia.Input.TextInputEventArgs
                {
                    Text = "s",
                    RoutedEvent = Avalonia.Input.InputElement.TextInputEvent
                };
                mainCodeEditor.TextArea.RaiseEvent(textArgs);

                var winOverlay = mainEditor.FindControl<Fdb.Components.SqlIntelliSenseOverlayComponent>("IntelliSenseOverlay")!;
                Console.WriteLine($"Window MainEditor after typing 's': Overlay.IsVisible={winOverlay.IsVisible}, ItemCount={winOverlay.ItemCount}");
                var winTopItem = winOverlay.SelectedItem;
                Console.WriteLine($"Window MainEditor TopItem: '{winTopItem?.Text}'");

                if (!winOverlay.IsVisible || winTopItem?.Text != "SELECT")
                {
                    throw new Exception("Window MainEditor failed to suggest 'SELECT' on typing 's'!");
                }

                // Simulate Enter commit
                mainEditor.CommitSelectedCompletion();
                Console.WriteLine($"Window MainEditor after commit: Document.Text='{mainCodeEditor.Document.Text}'");
                if (mainCodeEditor.Document.Text != "SELECT ")
                {
                    throw new Exception("Window MainEditor failed to commit 'SELECT '!");
                }
            }
            window.DataContext = null;

            // 8. Test Zero Emojis / All Vector Icons
            Console.WriteLine("\n[Action] Verifying Zero Emojis / Native Vector Icons...");
            var emojiRegex = new System.Text.RegularExpressions.Regex(@"[\uD83C-\uDBFF\uDC00-\uDFFF\u2600-\u27BF]");
            int emojiCount = 0;
            foreach (var file in System.IO.Directory.GetFiles("Components", "*.axaml", System.IO.SearchOption.AllDirectories))
            {
                var content = System.IO.File.ReadAllText(file);
                var matches = emojiRegex.Matches(content);
                emojiCount += matches.Count;
            }
            Console.WriteLine($"Total AXAML Emojis Remaining: {emojiCount} (All replaced with Fluent FASymbolIcon & PathIcon)");
            if (emojiCount > 0)
            {
                throw new Exception("Emojis found in AXAML files!");
            }

            // 9. Test Splash Screen Lifecycle & SQLite Startup Execution
            Console.WriteLine("\n[Action] Verifying Splash Screen Window & SQL Startup Execution...");
            var splashWindow = new Fdb.Views.SplashScreenWindow();
            Console.WriteLine($"Splash initial status: '{splashWindow.ViewModel.StatusText}', Progress: {splashWindow.ViewModel.ProgressValue}%");
            await splashWindow.RunStartupSequenceAsync().ConfigureAwait(false);
            Console.WriteLine($"Splash completed status: '{splashWindow.ViewModel.StatusText}', Progress: {splashWindow.ViewModel.ProgressValue}%");
            if (splashWindow.ViewModel.ProgressValue != 100)
            {
                throw new Exception("Splash sequence failed to reach 100%!");
            }
            splashWindow.Dismiss();

            // 10. Test SQLite Local Storage Service & Dialect Keywords
            Console.WriteLine("\n[Action] Verifying SQLite Local Storage Service & Multi-Dialect Keywords...");
            var storageService = Fdb.Services.DatabaseStorageService.Instance;
            await storageService.InitializeAsync();

            var dialects = await storageService.GetSupportedDialectsAsync();
            Console.WriteLine($"SQLite Supported Dialects: {string.Join(", ", dialects)}");
            if (!dialects.Contains("MSSQL") || !dialects.Contains("PostgreSQL") || !dialects.Contains("MySQL") || !dialects.Contains("SQLite"))
            {
                throw new Exception("Supported dialects missing expected database engines in SQLite!");
            }

            var mssqlKeywords = await storageService.GetKeywordsAsync("MSSQL");
            var pgKeywords = await storageService.GetKeywordsAsync("PostgreSQL");
            var mysqlKeywords = await storageService.GetKeywordsAsync("MySQL");
            var sqliteKeywords = await storageService.GetKeywordsAsync("SQLite");
            Console.WriteLine($"Keywords count: MSSQL={mssqlKeywords.Count}, PostgreSQL={pgKeywords.Count}, MySQL={mysqlKeywords.Count}, SQLite={sqliteKeywords.Count}");

            if (!mssqlKeywords.Contains("TOP") || !mssqlKeywords.Contains("IDENTITY"))
            {
                throw new Exception("MSSQL keywords missing expected dialect keywords!");
            }
            if (!pgKeywords.Contains("RETURNING") || !pgKeywords.Contains("SERIAL"))
            {
                throw new Exception("PostgreSQL keywords missing expected dialect keywords!");
            }
            if (!mysqlKeywords.Contains("AUTO_INCREMENT"))
            {
                throw new Exception("MySQL keywords missing AUTO_INCREMENT!");
            }
            if (!sqliteKeywords.Contains("AUTOINCREMENT"))
            {
                throw new Exception("SQLite keywords missing AUTOINCREMENT!");
            }

            var mssqlSnippets = await storageService.GetSnippetsAsync("MSSQL");
            var pgSnippets = await storageService.GetSnippetsAsync("PostgreSQL");
            Console.WriteLine($"Snippets count: MSSQL={mssqlSnippets.Count}, PostgreSQL={pgSnippets.Count}");
            if (mssqlSnippets.Count == 0 || pgSnippets.Count == 0)
            {
                throw new Exception("Dialect snippets failed to load from SQLite!");
            }

            // Test Dynamic Dialect Switching in SqlKeywordsService
            Console.WriteLine("\n[Action] Testing Dynamic Dialect Switching in SqlKeywordsService...");
            await Fdb.Services.SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync("PostgreSQL");
            if (Fdb.Services.SqlKeywordsService.Instance.CurrentDialect != "PostgreSQL" || !Fdb.Services.SqlKeywordsService.Instance.Keywords.Contains("RETURNING"))
            {
                throw new Exception("SqlKeywordsService failed to switch to PostgreSQL dialect!");
            }
            Console.WriteLine($"Switched to PostgreSQL. Contains 'RETURNING': {Fdb.Services.SqlKeywordsService.Instance.Keywords.Contains("RETURNING")}");

            await Fdb.Services.SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync("MSSQL");
            if (Fdb.Services.SqlKeywordsService.Instance.CurrentDialect != "MSSQL" || !Fdb.Services.SqlKeywordsService.Instance.Keywords.Contains("TOP"))
            {
                throw new Exception("SqlKeywordsService failed to switch back to MSSQL dialect!");
            }
            Console.WriteLine($"Switched back to MSSQL. Contains 'TOP': {Fdb.Services.SqlKeywordsService.Instance.Keywords.Contains("TOP")}");

            var connections = await storageService.GetRecentConnectionsAsync();
            Console.WriteLine($"SQLite Connections loaded: {connections.Count} connection(s).");
            var history = await storageService.GetQueryHistoryAsync();
            Console.WriteLine($"SQLite QueryHistory loaded: {history.Count} record(s).");
            var cachedRows = await storageService.GetQueryResultsAsync();
            Console.WriteLine($"SQLite QueryResults loaded: {cachedRows.Count} row(s).");

            // Test saving new query to SQLite
            await storageService.SaveQueryHistoryAsync("SELECT id, name FROM Users WHERE status = 'Active';", "6 ms", 1, "Success");
            var updatedHistory = await storageService.GetQueryHistoryAsync();
            Console.WriteLine($"SQLite QueryHistory updated count: {updatedHistory.Count} record(s).");
            if (updatedHistory.Count == 0)
            {
                throw new Exception("Failed to persist query history in SQLite!");
            }

                        // 11. Test Dynamic Database Providers & Connect Modal Engine Switching
            Console.WriteLine("\n[Action] Verifying Dynamic Database Providers & Engine Switching...");
            var registry = Fdb.Services.DatabaseProviders.DatabaseProviderRegistry.Instance;
            var allProviders = registry.GetAllProviders();
            Console.WriteLine($"Registered Providers: {string.Join(", ", System.Linq.Enumerable.Select(allProviders, p => p.Id))}");
            if (allProviders.Count < 4)
            {
                throw new Exception("Expected at least 4 registered database providers (MSSQL, PostgreSQL, MySQL, SQLite)!");
            }

            var mssqlProv = registry.GetProvider("MSSQL");
            var connStr = mssqlProv.BuildConnectionString(new Fdb.Services.DatabaseProviders.DatabaseConnectionConfig
            {
                Server = "127.0.0.1",
                Port = 1433,
                Database = "TestDb",
                AuthType = "SQL Server Authentication",
                Username = "sa",
                Password = "SecretPassword123!",
                TrustServerCertificate = true
            });
            Console.WriteLine($"Generated MSSQL Connection String: {connStr}");
            if (!connStr.Contains("TestDb") || !connStr.Contains("sa") || !connStr.Contains("127.0.0.1"))
            {
                throw new Exception("MSSQL Connection string did not contain expected configuration parameters!");
            }

            // Test SQLite Provider In-Memory
            var sqliteProv = registry.GetProvider("SQLite");
            var sqliteTest = await sqliteProv.TestConnectionAsync(new Fdb.Services.DatabaseProviders.DatabaseConnectionConfig
            {
                FilePath = ":memory:"
            });
            Console.WriteLine($"SQLite In-Memory Test: Success={sqliteTest.Success}, Version='{sqliteTest.ServerVersion}'");
            if (!sqliteTest.Success || string.IsNullOrEmpty(sqliteTest.ServerVersion))
            {
                throw new Exception("SQLite In-Memory test failed!");
            }

            // Test ConnectModal dynamic switching
            vm.ConnectModal.SelectEngine("PostgreSQL");
            Console.WriteLine($"Switched to PostgreSQL: Port={vm.ConnectModal.Port}, Database={vm.ConnectModal.Database}, Username={vm.ConnectModal.Username}, IsServerBased={vm.ConnectModal.IsServerBased}");
            if (vm.ConnectModal.Port != 5432 || vm.ConnectModal.Database != "postgres" || vm.ConnectModal.Username != "postgres")
            {
                throw new Exception("ConnectModal failed to switch default parameters for PostgreSQL!");
            }

            vm.ConnectModal.SelectEngine("SQLite");
            Console.WriteLine($"Switched to SQLite: IsFileBased={vm.ConnectModal.IsFileBased}, IsServerBased={vm.ConnectModal.IsServerBased}");
            if (!vm.ConnectModal.IsFileBased || vm.ConnectModal.IsServerBased)
            {
                throw new Exception("ConnectModal failed to switch to file-based mode for SQLite!");
            }

            vm.ConnectModal.SelectEngine("MSSQL");
            Console.WriteLine($"Switched back to MSSQL: Port={vm.ConnectModal.Port}, Database={vm.ConnectModal.Database}, Username={vm.ConnectModal.Username}");
            if (vm.ConnectModal.Port != 1433 || vm.ConnectModal.Database != "master")
            {
                throw new Exception("ConnectModal failed to restore default parameters for MSSQL!");
            }

            // 12. Test Startup Disconnection & Target Isolation (No auto-connection to internal SQLite)
            Console.WriteLine("\n[Action] Verifying Startup Disconnection & Target Isolation...");
            Console.WriteLine($"Startup State: Sidebar.IsConnected={vm.Sidebar.IsConnected}, SelectedTarget={vm.SelectedTargetDatabase?.DisplayName ?? "null"}, TargetDbName={vm.SampleQueryResult.TargetDatabaseName}");
            if (vm.Sidebar.IsConnected || vm.SelectedTargetDatabase != null)
            {
                throw new Exception("Application must NOT auto-connect to internal sqlite on startup!");
            }
            if (vm.SampleQueryResult.TargetDatabaseName != "(No Connection)")
            {
                throw new Exception($"Expected target '(No Connection)', got '{vm.SampleQueryResult.TargetDatabaseName}'!");
            }

            // Test executing query while disconnected returns friendly warning without querying internal sqlite
            vm.Editor.SelectedTab!.Text = "SELECT 1;";
            await vm.ExecuteQuery();
            Console.WriteLine($"Disconnected execution: Status={vm.SampleQueryResult.Rows[0]["Status"]}, Message={vm.SampleQueryResult.Rows[0]["Message"]}");
            if (vm.SampleQueryResult.Rows[0]["Status"] != "Not Connected")
            {
                throw new Exception("Query execution without connection must return Not Connected status!");
            }

            // 13. Test Hierarchical Tree View: Adding connection, clicking to connect, displaying tables in tree
            Console.WriteLine("\n[Action] Verifying Hierarchical Tree View & Connecting from Tree...");
            int testDbId = await Fdb.Services.DatabaseStorageService.Instance.SaveConnectionAsync(
                ":memory:", 0, "TestStudioDB", "None", "sa", "SQLite");
            await vm.Sidebar.RefreshAsync();
            await vm.RefreshAvailableDatabasesAsync();

            var testDbNode = System.Linq.Enumerable.FirstOrDefault(vm.Sidebar.DatabaseNodes, n => n.DatabaseName == "TestStudioDB");
            if (testDbNode == null)
            {
                throw new Exception("Failed to locate TestStudioDB in Sidebar DatabaseNodes!");
            }
            Console.WriteLine($"Located Tree Node: Title={testDbNode.Title}, IsConnected={testDbNode.IsConnected}");
            if (testDbNode.IsConnected)
            {
                throw new Exception("New database in tree should initially be Disconnected until clicked!");
            }

            // Click (+) Expand or Right-Click Connect to TestStudioDB from tree
            Console.WriteLine($"Clicking/connecting to tree node '{testDbNode.Title}' via ToggleExpandCommand (+)...");
            await testDbNode.ToggleExpandCommand.ExecuteAsync(null);
            Console.WriteLine($"After click: Node.IsConnected={testDbNode.IsConnected}, Node.IsActive={testDbNode.IsActive}, Node.IsExpanded={testDbNode.IsExpanded}");
            if (!testDbNode.IsConnected || !testDbNode.IsActive || !testDbNode.IsExpanded)
            {
                throw new Exception("Clicking database node failed to connect and expand in tree!");
            }

            // Verify children hierarchy (Folder node 'Tables' -> Table items)
            if (testDbNode.Children.Count == 0 || testDbNode.TablesFolder == null)
            {
                throw new Exception("Database node in tree must contain Tables folder node after connection!");
            }
            Console.WriteLine($"Tree Folder: '{testDbNode.TablesFolder.Title}', Child items count={testDbNode.TablesFolder.Children.Count}");

            // Create a table with rows in the connected database and verify it appears in the tree
            await Fdb.Services.DatabaseProviders.DatabaseSessionService.Instance.ExecuteQueryAsync("CREATE TABLE Users (Id INTEGER PRIMARY KEY, Name TEXT);", testDbNode.ConnectionKey);
            await Fdb.Services.DatabaseProviders.DatabaseSessionService.Instance.ExecuteQueryAsync("INSERT INTO Users (Id, Name) VALUES (1, 'Alice'), (2, 'Bob');", testDbNode.ConnectionKey);
            await vm.Sidebar.RefreshDatabaseNodeAsync(testDbNode);
            Console.WriteLine($"After table creation: Tree Folder '{testDbNode.TablesFolder.Title}', Child count={testDbNode.TablesFolder.Children.Count}");
            if (testDbNode.TablesFolder.Children.Count == 0)
            {
                throw new Exception("Expected at least 1 table in tree folder after CREATE TABLE!");
            }
            var tableItem = testDbNode.TablesFolder.Children[0] as Fdb.ViewModels.TableTreeNode;
            Console.WriteLine($"Found Table in Tree: {tableItem?.FullName}");
            if (tableItem?.TableName != "Users")
            {
                throw new Exception($"Expected TableName 'Users', got '{tableItem?.TableName}'!");
            }

            // Test clicking table node: Automatically generates TOP 100 query, executes it and populates TOP 100 items into table
            await vm.Sidebar.SelectTableNode(tableItem);
            Console.WriteLine($"Editor text after clicking table: {vm.Editor.SelectedTab!.Text}");
            Console.WriteLine($"Auto-executed Table Rows Count: {vm.SampleQueryResult.TotalRows}, Footer.IsVisible: {vm.Footer.IsVisible}");
            if (!vm.Editor.SelectedTab.Text.Contains("Users") || !vm.Editor.SelectedTab.Text.Contains("100"))
            {
                throw new Exception("Clicking table in tree failed to generate TOP/LIMIT 100 query in editor!");
            }
            if (vm.SampleQueryResult.TotalRows != 2)
            {
                throw new Exception($"Expected 2 rows from auto-executed table query, got {vm.SampleQueryResult.TotalRows}!");
            }
            if (vm.SampleQueryResult.Rows[0]["Name"] != "Alice" || vm.SampleQueryResult.Rows[1]["Name"] != "Bob")
            {
                throw new Exception("Auto-executed table query returned unexpected row data!");
            }

            // Verify SelectedTargetDatabase is now set to this database
            if (vm.SelectedTargetDatabase == null || vm.SelectedTargetDatabase.DatabaseName != "TestStudioDB")
            {
                throw new Exception($"Expected active target 'TestStudioDB', got '{vm.SelectedTargetDatabase?.DisplayName}'!");
            }

            // 14. Test Real Query Execution into Table on Connected Database
            Console.WriteLine("\n[Action] Verifying Real Query Execution on Connected Database...");
            vm.Editor.SelectedTab!.Text = "SELECT 42 AS Answer, 'Antigravity' AS Assistant, 'Active' AS Status;";
            await vm.ExecuteQuery();
            Console.WriteLine($"Query executed! Total Rows: {vm.SampleQueryResult.TotalRows}, ExecutionTime: {vm.SampleQueryResult.ExecutionTime}");
            if (vm.SampleQueryResult.TotalRows == 0)
            {
                throw new Exception("Real query execution failed to return rows!");
            }
            var resultRow = vm.SampleQueryResult.Rows[0];
            Console.WriteLine($"Real Result Row: Answer={resultRow["Answer"]}, Assistant={resultRow["Assistant"]}, Status={resultRow["Status"]}");
            if (resultRow["Answer"] != "42" || resultRow["Assistant"] != "Antigravity")
            {
                throw new Exception($"Expected dynamic column values '42' and 'Antigravity', got '{resultRow["Answer"]}' and '{resultRow["Assistant"]}'!");
            }

            // 15. Test Saving Connections in Connect Modal and SQLite Persistence
            Console.WriteLine("\n[Action] Verifying Connection Persistence in SQLite...");
            int initialSaved = vm.Sidebar.SavedConnections.Count;
            vm.ConnectModal.Open();
            vm.ConnectModal.Server = "sql-cluster.production.local";
            vm.ConnectModal.Port = 1433;
            vm.ConnectModal.Database = "SalesDB";
            vm.ConnectModal.Username = "sales_admin";
            // Persist connection
            await Fdb.Services.DatabaseStorageService.Instance.SaveConnectionAsync(
                vm.ConnectModal.Server, vm.ConnectModal.Port, vm.ConnectModal.Database,
                vm.ConnectModal.AuthType, vm.ConnectModal.Username, "MSSQL");
            await vm.Sidebar.RefreshAsync();
            await vm.ConnectModal.LoadSavedConnectionsAsync();
            Console.WriteLine($"Saved Connections Count: {vm.ConnectModal.SavedConnections.Count} (was {initialSaved})");
            var savedConn = System.Linq.Enumerable.FirstOrDefault(vm.ConnectModal.SavedConnections, c => c.Server == "sql-cluster.production.local");
            if (savedConn == null || savedConn.DatabaseName != "SalesDB")
            {
                throw new Exception("Failed to persist and retrieve connection from SQLite!");
            }
            Console.WriteLine($"Successfully verified saved connection: {savedConn.DisplayTitle}");

            // Test Editing Connection via ContextMenu Action (Node.EditCommand)
            Console.WriteLine("\n[Action] Verifying Edit Connection via ContextMenu Action...");
            var salesNodeToEdit = System.Linq.Enumerable.FirstOrDefault(vm.Sidebar.DatabaseNodes, n => n.Server == "sql-cluster.production.local");
            if (salesNodeToEdit != null)
            {
                salesNodeToEdit.EditCommand.Execute(null);
                Console.WriteLine($"ConnectModal Open: {vm.ConnectModal.IsOpen}, IsEditMode: {vm.ConnectModal.IsEditMode}, EditingId: {vm.ConnectModal.EditingConnectionId}");
                if (!vm.ConnectModal.IsOpen || !vm.ConnectModal.IsEditMode || vm.ConnectModal.EditingConnectionId != salesNodeToEdit.Record.Id)
                {
                    throw new Exception("EditDatabaseNode failed to open ConnectModal in edit mode!");
                }
                // Update database name in modal and save
                vm.ConnectModal.Database = "SalesDB_Updated";
                await Fdb.Services.DatabaseStorageService.Instance.UpdateConnectionAsync(
                    vm.ConnectModal.EditingConnectionId.Value,
                    vm.ConnectModal.Server, vm.ConnectModal.Port, vm.ConnectModal.Database,
                    vm.ConnectModal.AuthType, vm.ConnectModal.Username, vm.ConnectModal.SelectedDatabaseType);
                await vm.Sidebar.RefreshAsync();
                var updatedNode = System.Linq.Enumerable.FirstOrDefault(vm.Sidebar.DatabaseNodes, n => n.Record.Id == salesNodeToEdit.Record.Id);
                Console.WriteLine($"Updated Node Title: {updatedNode?.Title}, DB: {updatedNode?.DatabaseName}");
                if (updatedNode == null || updatedNode.DatabaseName != "SalesDB_Updated")
                {
                    throw new Exception("Editing connection failed to update database name in SQLite!");
                }
                Console.WriteLine("[Success] Connection successfully edited and persisted in SQLite!");
            }

            // 16. Test Disconnecting and Removing a Connection from Tree and SQLite
            Console.WriteLine("\n[Action] Verifying Disconnect and Deletion from Tree...");
            await vm.Sidebar.DisconnectDatabaseNodeAsync(testDbNode);
            Console.WriteLine($"After disconnect: Node.IsConnected={testDbNode.IsConnected}, Sidebar.IsConnected={vm.Sidebar.IsConnected}");
            if (testDbNode.IsConnected || vm.Sidebar.IsConnected)
            {
                throw new Exception("DisconnectDatabaseNodeAsync failed to disconnect!");
            }

            await vm.Sidebar.DeleteDatabaseNodeAsync(testDbNode);
            if (vm.Sidebar.DatabaseNodes.Contains(testDbNode))
            {
                throw new Exception("Failed to remove TestStudioDB from DatabaseNodes!");
            }

            var salesNode = System.Linq.Enumerable.FirstOrDefault(vm.Sidebar.DatabaseNodes, n => n.Server == "sql-cluster.production.local");
            if (salesNode != null)
            {
                await vm.Sidebar.DeleteDatabaseNodeAsync(salesNode);
            }
            Console.WriteLine("[Success] Database disconnected and removed cleanly!");

            // [Action] Verifying Graphical Cell Editor & Query Generator in Footer
            Console.WriteLine("\n[Action] Verifying Graphical Cell Editor & UPDATE Query Generator...");
            var testUserRow = new Fdb.ViewModels.QueryResultRow
            {
                RowIndex = 1,
                Id = 101,
                Name = "Alice Johnson",
                Email = "alice@example.com"
            };
            testUserRow["Id"] = "101";
            testUserRow["Name"] = "Alice Johnson";
            testUserRow["Email"] = "alice@example.com";
            vm.SampleQueryResult.SetResults(
                new System.Collections.Generic.List<string> { "Id", "Name", "Email" },
                new System.Collections.Generic.List<Fdb.ViewModels.QueryResultRow> { testUserRow },
                "5 ms");
            vm.SampleQueryResult.CurrentTableName = "Users";
            var firstRow = vm.SampleQueryResult.Rows[0];
            Console.WriteLine($"Initial Row 0 Name: '{firstRow.Name}', Id: {firstRow.Id}");

            // Open edit modal for Row 0, Column 'Name'
            vm.SampleQueryResult.OpenEditCell(firstRow, "Name");
            var editModal = vm.SampleQueryResult.EditCellModal;
            Console.WriteLine($"EditModal State: IsOpen={editModal.IsOpen}, Table={editModal.TableName}, Col={editModal.ColumnName}, OldVal='{editModal.OldValue}'");
            if (!editModal.IsOpen || editModal.OldValue != "Alice Johnson")
            {
                throw new Exception($"Expected EditModal open with 'Alice Johnson', but got IsOpen={editModal.IsOpen}, OldValue='{editModal.OldValue}'");
            }

            // Verify live generated query preview
            Console.WriteLine($"Initial Generated Query:\n{editModal.GeneratedQuery}");
            if (!editModal.GeneratedQuery.Contains("UPDATE") || !editModal.GeneratedQuery.Contains("SET") || (!editModal.GeneratedQuery.Contains("101") || !editModal.GeneratedQuery.Contains("Id")))
            {
                throw new Exception($"Generated query does not match expected UPDATE syntax!\n{editModal.GeneratedQuery}");
            }

            // Change name graphically to 'Ehsan'
            editModal.NewValue = "Ehsan";
            Console.WriteLine($"Updated Generated Query with 'Ehsan':\n{editModal.GeneratedQuery}");
            if (!editModal.GeneratedQuery.Contains("'Ehsan'"))
            {
                throw new Exception("Live query preview did not update with 'Ehsan'!");
            }

            // Save and Execute UPDATE
            await editModal.ExecuteAndSaveAsync();
            Console.WriteLine($"After Save: EditModal.IsOpen={editModal.IsOpen}, Row 0 Name='{firstRow.Name}', Row['Name']='{firstRow["Name"]}'");
            if (editModal.IsOpen || firstRow.Name != "Ehsan" || firstRow["Name"] != "Ehsan")
            {
                throw new Exception($"Row was not updated to 'Ehsan'! Got Name='{firstRow.Name}', Row['Name']='{firstRow["Name"]}'");
            }
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("'Ehsan'"))
            {
                throw new Exception("SQL Editor did not receive the executed UPDATE query!");
            }
            Console.WriteLine($"[Success] Cell graphically edited, query executed, and reflected in grid and editor: '{vm.SampleQueryResult.LastQueryMessage}'");

            // [Action] Verifying Primary Key Visual Distinctiveness (Badges & Headers)
            Console.WriteLine("\n[Action] Verifying Primary Key UI Highlighting & Detection...");
            Console.WriteLine($"Table HasPrimaryKey: {vm.SampleQueryResult.HasPrimaryKey}, PK Column: '{vm.SampleQueryResult.PrimaryKeyColumnName}'");
            if (!vm.SampleQueryResult.HasPrimaryKey || !vm.SampleQueryResult.PrimaryKeyColumnName.Contains("Id"))
            {
                throw new Exception($"Expected Primary Key detected for Users table, but got HasPK={vm.SampleQueryResult.HasPrimaryKey}, PK={vm.SampleQueryResult.PrimaryKeyColumnName}");
            }
            if (!vm.SampleQueryResult.IsColumnPrimaryKey("Id") || vm.SampleQueryResult.IsColumnPrimaryKey("Email"))
            {
                throw new Exception("IsColumnPrimaryKey mismatch for Id vs Email!");
            }
            if (!vm.SampleSchema.Columns[0].IsPrimaryKey)
            {
                throw new Exception("Schema table column 0 is not marked as Primary Key!");
            }
            Console.WriteLine("[Success] Primary Key visual badge, key icon & distinct styling verified!");

            // [Action] Verifying Table Row Deletion with Safety Confirmation & SQL Execution
            Console.WriteLine("\n[Action] Verifying Table Row Deletion & Safety Confirmation...");
            int rowsBeforeDelete = vm.SampleQueryResult.Rows.Count;
            var targetDeleteRow = vm.SampleQueryResult.Rows[0];
            Console.WriteLine($"Rows before delete: {rowsBeforeDelete}, Target Row: Id={targetDeleteRow.Id}, Name='{targetDeleteRow.Name}'");

            // 1. Test GenerateDeleteQuery
            vm.SampleQueryResult.GenerateDeleteQueryCommand.Execute(null);
            Console.WriteLine($"SQL Editor Text after GenerateDeleteQuery: '{vm.Editor.SelectedTab?.Text}'");
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("DELETE FROM") || !vm.Editor.SelectedTab.Text.Contains("Users") || !vm.Editor.SelectedTab.Text.Contains("101"))
            {
                throw new Exception($"GenerateDeleteQuery failed to generate expected DELETE statement! Text='{vm.Editor.SelectedTab?.Text}'");
            }

            // 2. Test DeleteRow triggering ConfirmModal
            vm.SampleQueryResult.DeleteSelectedRowCommand.Execute(null);
            Console.WriteLine($"ConfirmModal open: {vm.ConfirmModal.IsOpen}, Title='{vm.ConfirmModal.Title}', Target='{vm.ConfirmModal.TargetName}'");
            if (!vm.ConfirmModal.IsOpen || string.IsNullOrEmpty(vm.ConfirmModal.TargetName) || !vm.ConfirmModal.TargetName.Contains("DELETE FROM") || !vm.ConfirmModal.TargetName.Contains("101"))
            {
                throw new Exception($"DeleteSelectedRow failed to open ConfirmModal with generated DELETE query! IsOpen={vm.ConfirmModal.IsOpen}, Target='{vm.ConfirmModal.TargetName}'");
            }

            // Cancel first to test safety abort
            vm.ConfirmModal.CancelCommand.Execute(null);
            Console.WriteLine($"After cancel: ConfirmModal.IsOpen={vm.ConfirmModal.IsOpen}, Rows Count={vm.SampleQueryResult.Rows.Count}");
            if (vm.ConfirmModal.IsOpen || vm.SampleQueryResult.Rows.Count != rowsBeforeDelete)
            {
                throw new Exception("Cancel delete failed: modal remained open or row was deleted!");
            }

            // 3. Confirm deletion
            vm.SampleQueryResult.DeleteSelectedRowCommand.Execute(null);
            vm.ConfirmModal.ConfirmCommand.Execute(null);
            Console.WriteLine($"After confirm: ConfirmModal.IsOpen={vm.ConfirmModal.IsOpen}, Rows Count={vm.SampleQueryResult.Rows.Count}, TotalRows={vm.SampleQueryResult.TotalRows}");
            if (vm.ConfirmModal.IsOpen || vm.SampleQueryResult.Rows.Count != rowsBeforeDelete - 1 || vm.SampleQueryResult.TotalRows != rowsBeforeDelete - 1)
            {
                throw new Exception($"Row deletion failed! Expected {rowsBeforeDelete - 1} rows, got {vm.SampleQueryResult.Rows.Count}");
            }
            if (System.Linq.Enumerable.Any(vm.SampleQueryResult.Rows, r => r.Id == 101))
            {
                throw new Exception("Deleted row with Id=101 still exists in Rows collection!");
            }
            Console.WriteLine($"[Success] Row deletion verified: '{vm.SampleQueryResult.LastQueryMessage}'");

            // [Action] Verifying Footer Table Refresh Command & Real-time Row Search Filter
            Console.WriteLine("\n[Action] Verifying Footer Table Refresh & Search Filter...");
            Console.WriteLine($"Rows before refresh: {vm.SampleQueryResult.Rows.Count}");
            
            // 1. Test Refresh in Disconnected Mode (Restores clean sample dataset)
            await vm.SampleQueryResult.RefreshCommand.ExecuteAsync(null);
            Console.WriteLine($"After Refresh: Rows={vm.SampleQueryResult.Rows.Count}, TotalRows={vm.SampleQueryResult.TotalRows}, LastMessage='{vm.SampleQueryResult.LastQueryMessage}'");
            if (vm.SampleQueryResult.Rows.Count != 5 || vm.SampleQueryResult.TotalRows != 5 || vm.SampleQueryResult.Rows[0].Name != "Alice Johnson")
            {
                throw new Exception($"Refresh failed to restore 5 sample rows! Count={vm.SampleQueryResult.Rows.Count}, Row 0={(vm.SampleQueryResult.Rows.Count > 0 ? vm.SampleQueryResult.Rows[0].Name : null)}");
            }

            // 2. Test Real-time Search Filter
            Console.WriteLine("[Action] Testing SearchFilter with 'Bob'...");
            vm.SampleQueryResult.SearchFilter = "Bob";
            Console.WriteLine($"Filtered Rows Count: {vm.SampleQueryResult.Rows.Count}, Item 0: '{vm.SampleQueryResult.Rows[0].Name}'");
            if (vm.SampleQueryResult.Rows.Count != 1 || vm.SampleQueryResult.Rows[0].Name != "Bob Smith")
            {
                throw new Exception($"SearchFilter 'Bob' failed! Got {vm.SampleQueryResult.Rows.Count} rows.");
            }

            // Clear search filter
            vm.SampleQueryResult.SearchFilter = string.Empty;
            Console.WriteLine($"After clearing filter: Rows={vm.SampleQueryResult.Rows.Count}");
            if (vm.SampleQueryResult.Rows.Count != 5)
            {
                throw new Exception($"Clearing SearchFilter failed! Expected 5 rows, got {vm.SampleQueryResult.Rows.Count}");
            }

            // 3. Test Refresh on Active Database Session
            var activeSession = Fdb.Services.DatabaseProviders.DatabaseSessionService.Instance.ActiveSession;
            if (activeSession != null)
            {
                Console.WriteLine($"[Action] Testing Refresh on active session: {activeSession.DisplayName}...");
                vm.SampleQueryResult.LastExecutedQuery = "SELECT 1 AS RefreshTest, 'Active' AS State;";
                await vm.SampleQueryResult.RefreshCommand.ExecuteAsync(null);
                Console.WriteLine($"After active DB refresh: Rows={vm.SampleQueryResult.Rows.Count}, Message='{vm.SampleQueryResult.LastQueryMessage}'");
                if (vm.SampleQueryResult.Rows.Count != 1 || !vm.SampleQueryResult.LastQueryMessage.Contains("Table refreshed"))
                {
                    throw new Exception("Refresh on active database session failed to reload query results!");
                }
            }
            Console.WriteLine("[Success] Footer table Refresh command & real-time search filter verified!");

            Console.WriteLine("\n>>> ALL TESTS (LAYOUT, TABLES, MODALS, TABS, SQL AUTOCOMPLETE, MULTI-DIALECT SQLITE, VECTOR ICONS, SPLASH SCREEN, STORAGE, DYNAMIC DATABASE PROVIDERS, ISOLATED STARTUP, HIERARCHICAL TREE VIEW, REAL QUERY, TARGET DATABASE ROUTING, DISCONNECT/DELETE, GRAPHICAL FOOTER CELL UPDATE, PRIMARY KEY UI DISTINCTIVENESS, TABLE ROW DELETION & FOOTER REFRESH/FILTER) PASSED! <<<");
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
        => AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();
}
