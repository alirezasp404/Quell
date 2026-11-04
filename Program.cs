using Avalonia;
using Avalonia.Controls;
using System;
using System.Linq;
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
            Console.OutputEncoding = System.Text.Encoding.UTF8;
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

            // 2b. Test Ctrl+B Keyboard Shortcut to Toggle / Close Sidebar
            Console.WriteLine("\n[Action] Verifying Ctrl+B Shortcut closes Sidebar...");
            vm.Sidebar.IsVisible = true;
            window.UpdateSidebarLayout();
            if (!vm.Sidebar.IsVisible || window.MainGrid.ColumnDefinitions[0].Width.Value == 0)
            {
                throw new Exception("Sidebar failed to initialize open before Ctrl+B test!");
            }
            // Fire Ctrl+B into Window tunneling handler
            window.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.B,
                KeyModifiers = Avalonia.Input.KeyModifiers.Control
            });
            Console.WriteLine($"After Ctrl+B KeyDown: Sidebar.IsVisible={vm.Sidebar.IsVisible}, ColWidth={window.MainGrid.ColumnDefinitions[0].Width}");
            if (vm.Sidebar.IsVisible || window.MainGrid.ColumnDefinitions[0].Width.Value != 0)
            {
                throw new Exception("Ctrl+B shortcut failed to close the Sidebar!");
            }

            // Fire Ctrl+B again to reopen Sidebar
            window.RaiseEvent(new Avalonia.Input.KeyEventArgs
            {
                RoutedEvent = Avalonia.Input.InputElement.KeyDownEvent,
                Key = Avalonia.Input.Key.B,
                KeyModifiers = Avalonia.Input.KeyModifiers.Control
            });
            Console.WriteLine($"After second Ctrl+B KeyDown: Sidebar.IsVisible={vm.Sidebar.IsVisible}, ColWidth={window.MainGrid.ColumnDefinitions[0].Width}");
            if (!vm.Sidebar.IsVisible || window.MainGrid.ColumnDefinitions[0].Width.Value == 0)
            {
                throw new Exception("Ctrl+B shortcut failed to reopen the Sidebar!");
            }
            Console.WriteLine("[Success] Ctrl+B shortcut verified to close and reopen Sidebar cleanly!");

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
            Console.WriteLine($"QueryResultTable Initial Rows: {vm.SampleQueryResult.Rows.Count} (Clean initial state, 0 default mock rows).");
            if (vm.SampleQueryResult.Rows.Count != 0)
            {
                throw new Exception($"Expected clean initial table (0 rows), but got {vm.SampleQueryResult.Rows.Count}");
            }
            if (vm.Footer.Tabs.Count != 0)
            {
                throw new Exception($"Expected clean initial footer tabs (0 tabs), but got {vm.Footer.Tabs.Count}");
            }
            Console.WriteLine($"SchemaPropertiesTable Columns: {vm.SampleSchema.Columns.Count} columns loaded.");
            var tableComp = new Fdb.Components.QueryResultTableComponent { DataContext = vm.SampleQueryResult };
            var dataGrid = tableComp.FindControl<Avalonia.Controls.DataGrid>("ResultsDataGrid");
            Console.WriteLine($"DataGrid Columns Count: {dataGrid?.Columns.Count ?? 0}");
            Console.WriteLine($"DataGrid RowHeight: {dataGrid?.RowHeight}px (Compact height)");
            if (dataGrid?.RowHeight != 25)
            {
                throw new Exception($"Expected compact DataGrid RowHeight=25, but got {dataGrid?.RowHeight}");
            }
            var testRow = new Fdb.ViewModels.QueryResultRow { RowIndex = 1, Id = 101, Name = "Alice Johnson" };
            var col0 = dataGrid?.Columns[0] as Avalonia.Controls.DataGridTemplateColumn;
            var cellContent = col0?.CellTemplate?.Build(testRow) as Avalonia.Controls.Grid;
            if (cellContent != null) cellContent.DataContext = testRow;
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

            // 5b. Test Panel Customization, Color Maker, Button/Border/Sidebar & SQLite Persistence
            Console.WriteLine("\n[Action] Verifying Color Maker ('رنگ‌ساز'), Buttons, Borders & Multi-Part Customization...");
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectCategory("Customization");
            Console.WriteLine($"Settings Category: Selected='{vm.SettingsModal.SelectedCategory}', IsCustomization={vm.SettingsModal.IsCustomizationCategory}");
            if (!vm.SettingsModal.IsCustomizationCategory) throw new Exception("Customization category selection failed!");

            // Test Color Maker Hex and RGB conversions
            vm.SettingsModal.SetColorMakerHex("#10B981");
            Console.WriteLine($"Color Maker parsed '#10B981': Hex={vm.SettingsModal.ColorMakerHex}, R={vm.SettingsModal.ColorMakerR}, G={vm.SettingsModal.ColorMakerG}, B={vm.SettingsModal.ColorMakerB}");
            if (vm.SettingsModal.ColorMakerR != 16 || vm.SettingsModal.ColorMakerG != 185 || vm.SettingsModal.ColorMakerB != 129)
            {
                throw new Exception("Color Maker hex to RGB parsing failed!");
            }

            // Test Color Maker Apply to Target
            vm.SettingsModal.ColorMakerSelectedTarget = "Buttons: Accent / Action";
            vm.SettingsModal.ApplyColorMakerToTarget();
            Console.WriteLine($"Applied Color Maker to Button Accent: ButtonAccentColor={vm.SettingsModal.ButtonAccentColor}");
            if (vm.SettingsModal.ButtonAccentColor != "#10B981") throw new Exception("Color Maker failed to apply to Button Accent!");

            // Customize Buttons, Borders, Sidebar parts, Editor
            vm.SettingsModal.SelectButtonAccentColor("#38BDF8");
            vm.SettingsModal.SelectButtonBackgroundColor("#1E293B");
            vm.SettingsModal.SelectButtonTextColor("#F8FAFC");
            vm.SettingsModal.SelectButtonBorderColor("#334155");

            vm.SettingsModal.SelectBorderColor("#334155");
            vm.SettingsModal.SelectSplitterColor("#38BDF8");

            vm.SettingsModal.SelectSidebarBackground("#141E2E");
            vm.SettingsModal.SelectSidebarHeaderBackground("#1E293B");
            vm.SettingsModal.SelectSidebarItemHoverBackground("#334155");
            vm.SettingsModal.SelectSidebarTextColor("#F8FAFC");
            vm.SettingsModal.SelectSidebarBorderColor("#334155");

            vm.SettingsModal.SelectEditorBackground("#1E293B");
            vm.SettingsModal.SelectEditorForeground("#34D399");
            vm.SettingsModal.SelectTableBackground("#1E293B");
            vm.SettingsModal.EditorFontSize = 15.0;
            vm.SettingsModal.EditorShowLineNumbers = true;

            await vm.SettingsModal.SaveAsync();

            var savedCustom = await Fdb.Services.DatabaseStorageService.Instance.GetPanelCustomizationAsync();
            Console.WriteLine($"SQLite Saved Panel Customization: ButtonAccent={savedCustom.ButtonAccentColor}, BorderColor={savedCustom.BorderColor}, SidebarHeader={savedCustom.SidebarHeaderBackground}, EditorBg={savedCustom.EditorBackground}, FontSize={savedCustom.EditorFontSize}");
            if (savedCustom.ButtonAccentColor != "#38BDF8" || savedCustom.BorderColor != "#334155" || savedCustom.SidebarHeaderBackground != "#1E293B" || savedCustom.EditorBackground != "#1E293B" || savedCustom.EditorFontSize != 15.0)
            {
                throw new Exception("Expanded panel customization failed to persist to SQLite!");
            }

            // Test Reset Panel Customization
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.ResetPanelCustomization();
            await vm.SettingsModal.SaveAsync();
            var resetCustom = await Fdb.Services.DatabaseStorageService.Instance.GetPanelCustomizationAsync();
            Console.WriteLine($"SQLite Restored Panel Customization: ButtonAccent={resetCustom.ButtonAccentColor}, BorderColor={resetCustom.BorderColor}, EditorBg={resetCustom.EditorBackground}, FontSize={resetCustom.EditorFontSize}");
            if (resetCustom.ButtonAccentColor != "Default" || resetCustom.BorderColor != "Default" || resetCustom.EditorBackground != "Default" || resetCustom.EditorFontSize != 13.0)
            {
                throw new Exception("Reset panel customization failed!");
            }
            Console.WriteLine("[Success] Color Maker, Button/Border/Sidebar customization, and SQLite persistence verified!");

            // 5c. Test Dynamic Shortcuts System (Read, Customize, and Reset from SQLite)
            Console.WriteLine("\n[Action] Verifying Dynamic Shortcuts System & SQLite Persistence...");
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectCategory("Shortcuts");
            Console.WriteLine($"Settings Category: Selected='{vm.SettingsModal.SelectedCategory}', IsShortcuts={vm.SettingsModal.IsShortcutsCategory}");
            if (!vm.SettingsModal.IsShortcutsCategory) throw new Exception("Shortcuts category selection failed!");

            Console.WriteLine($"Loaded Shortcuts Count: {vm.SettingsModal.Shortcuts.Count}");
            if (vm.SettingsModal.Shortcuts.Count == 0) throw new Exception("No shortcuts loaded from SQLite!");

            var runQueryShortcut = vm.SettingsModal.Shortcuts.FirstOrDefault(s => s.Id == Fdb.Services.ShortcutActions.RunQuery);
            if (runQueryShortcut == null) throw new Exception("RunQuery shortcut definition missing!");
            Console.WriteLine($"Initial RunQuery Shortcut: '{runQueryShortcut.CurrentGesture}' (Default: '{runQueryShortcut.DefaultGesture}')");

            // Customize RunQuery shortcut to 'F9'
            vm.SettingsModal.StartEditShortcut(runQueryShortcut);
            runQueryShortcut.EditingGesture = "F9";
            vm.SettingsModal.ApplyEditShortcut(runQueryShortcut);
            Console.WriteLine($"Customized RunQuery Shortcut: '{runQueryShortcut.CurrentGesture}', IsModified={runQueryShortcut.IsModified}");
            if (runQueryShortcut.CurrentGesture != "F9" || !runQueryShortcut.IsModified)
            {
                throw new Exception("Customizing shortcut in ViewModel failed!");
            }

            await vm.SettingsModal.SaveAsync();

            // Verify in SQLite
            var dbShortcuts = await Fdb.Services.DatabaseStorageService.Instance.GetShortcutsFromDbAsync();
            var dbRunQuery = dbShortcuts.FirstOrDefault(s => s.Id == Fdb.Services.ShortcutActions.RunQuery);
            Console.WriteLine($"SQLite Persisted RunQuery Shortcut: '{dbRunQuery?.KeyGesture}'");
            if (dbRunQuery?.KeyGesture != "F9")
            {
                throw new Exception("Customized shortcut failed to persist to SQLite!");
            }

            // Verify ShortcutService key matching engine
            bool isF9Match = Fdb.Services.ShortcutService.Instance.IsMatch(Fdb.Services.ShortcutActions.RunQuery,
                new Avalonia.Input.KeyEventArgs { Key = Avalonia.Input.Key.F9, KeyModifiers = Avalonia.Input.KeyModifiers.None });
            Console.WriteLine($"ShortcutService matches 'F9' for RunQuery: {isF9Match}");
            if (!isF9Match) throw new Exception("ShortcutService key matching engine failed for customized 'F9'!");

            // Test Reset Shortcut to Default
            await vm.SettingsModal.OpenAsync();
            var runQueryToReset = vm.SettingsModal.Shortcuts.FirstOrDefault(s => s.Id == Fdb.Services.ShortcutActions.RunQuery);
            vm.SettingsModal.ResetShortcut(runQueryToReset!);
            await vm.SettingsModal.SaveAsync();

            var dbResetShortcuts = await Fdb.Services.DatabaseStorageService.Instance.GetShortcutsFromDbAsync();
            var dbResetRunQuery = dbResetShortcuts.FirstOrDefault(s => s.Id == Fdb.Services.ShortcutActions.RunQuery);
            Console.WriteLine($"SQLite Restored RunQuery Shortcut: '{dbResetRunQuery?.KeyGesture}'");
            if (dbResetRunQuery?.KeyGesture != "F5")
            {
                throw new Exception("Reset shortcut failed to restore default 'F5' in SQLite!");
            }
            Console.WriteLine("[Success] Dynamic shortcuts system, live customization & SQLite persistence verified!");

            // 5d. Test Query Results Table Customization (RowHeight, HeaderHeight, Colors, Presets & SQLite Persistence)
            Console.WriteLine("\n[Action] Verifying Results Table Customization & SQLite Persistence...");
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectCategory("Table");
            Console.WriteLine($"Settings Category: Selected='{vm.SettingsModal.SelectedCategory}', IsTable={vm.SettingsModal.IsTableCategory}");
            if (!vm.SettingsModal.IsTableCategory) throw new Exception("Table category selection failed!");

            // Test applying custom row height, colors, font sizes
            vm.SettingsModal.SetTableRowHeight(28.0);
            vm.SettingsModal.SetTableHeaderHeight(30.0);
            vm.SettingsModal.SetTableGutterWidth(64.0);
            vm.SettingsModal.SetTableCellFontSize(12.0);
            vm.SettingsModal.SelectTableBackground("#1E293B");
            vm.SettingsModal.SelectTableAlternatingRowColor("#141E2E");
            vm.SettingsModal.SelectTableCellTextColor("#F8FAFC");
            vm.SettingsModal.SelectTableGridLinesColor("#334155");
            vm.SettingsModal.SelectTableSelectionColor("#1E3A5F");

            await vm.SettingsModal.SaveAsync();

            var savedTableCustom = await Fdb.Services.DatabaseStorageService.Instance.GetTableCustomizationAsync();
            Console.WriteLine($"SQLite Saved Table Customization: RowHeight={savedTableCustom.RowHeight}, HeaderHeight={savedTableCustom.HeaderHeight}, GutterWidth={savedTableCustom.GutterWidth}, FontSize={savedTableCustom.FontSize}, TableBg={savedTableCustom.TableBackground}, AltRowBg={savedTableCustom.AlternatingRowColor}");
            if (savedTableCustom.RowHeight != 28.0 || savedTableCustom.HeaderHeight != 30.0 || savedTableCustom.GutterWidth != 64.0 || savedTableCustom.FontSize != 12.0 || savedTableCustom.TableBackground != "#1E293B" || savedTableCustom.AlternatingRowColor != "#141E2E")
            {
                throw new Exception("Table customization failed to persist to SQLite!");
            }

            // Verify live dynamic update in ThemeService
            Console.WriteLine($"ThemeService CurrentTableCustomization: RowHeight={Fdb.Services.ThemeService.Instance.CurrentTableCustomization.RowHeight}, FontSize={Fdb.Services.ThemeService.Instance.CurrentTableCustomization.FontSize}");
            if (Fdb.Services.ThemeService.Instance.CurrentTableCustomization.RowHeight != 28.0 || Fdb.Services.ThemeService.Instance.CurrentTableCustomization.FontSize != 12.0)
            {
                throw new Exception("ThemeService failed to dynamically update CurrentTableCustomization!");
            }

            // Test Reset Table Customization
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.ResetTableCustomization();
            await vm.SettingsModal.SaveAsync();

            var resetTableCustom = await Fdb.Services.DatabaseStorageService.Instance.GetTableCustomizationAsync();
            Console.WriteLine($"SQLite Restored Table Customization: RowHeight={resetTableCustom.RowHeight}, FontSize={resetTableCustom.FontSize}, TableBg={resetTableCustom.TableBackground}");
            if (resetTableCustom.RowHeight != 25.0 || resetTableCustom.FontSize != 11.0 || resetTableCustom.TableBackground != "Default")
            {
                throw new Exception("Reset table customization failed to restore defaults!");
            }
            Console.WriteLine("[Success] Results table customization, sizing, presets and SQLite persistence verified!");

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

            Console.WriteLine($"SQLite DatabasePath: '{storageService.DatabasePath}'");
            if (storageService.DatabasePath.Contains("bin\\Debug") || storageService.DatabasePath.Contains("bin/Debug"))
            {
                throw new Exception("SQLite database path should not be inside binary output directory!");
            }

            // Verify DatabaseInitialized flag in AppSettings
            string isInit = await storageService.GetSettingAsync("DatabaseInitialized") ?? "";
            Console.WriteLine($"DatabaseInitialized setting in AppSettings: '{isInit}'");
            if (isInit != "true")
            {
                throw new Exception("DatabaseInitialized flag missing in AppSettings after init!");
            }

            // Test subsequent run idempotency: re-running InitializeAsync should NOT re-seed, duplicate or overwrite
            int kwCountBefore = (await storageService.GetKeywordsAsync("MSSQL")).Count;
            await storageService.InitializeAsync();
            int kwCountAfter = (await storageService.GetKeywordsAsync("MSSQL")).Count;
            if (kwCountBefore != kwCountAfter)
            {
                throw new Exception($"Re-running InitializeAsync altered keyword count! Before={kwCountBefore}, After={kwCountAfter}");
            }
            Console.WriteLine($"Subsequent launch idempotency verified: Init skipped, keywords preserved ({kwCountAfter})");

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

            // Verify Active/Highlighted state on clicked TableTreeNode
            if (!tableItem.IsActive || vm.Sidebar.ActiveTableNode != tableItem)
            {
                throw new Exception("Clicked table node is not marked as Active!");
            }
            Console.WriteLine($"[Success] TableTreeNode is highlighted and active: IsActive={tableItem.IsActive}, ActiveTableNode={vm.Sidebar.ActiveTableNode?.FullName}");

            // Verify Real-time Search & Filtering of Tables in Active Database
            Console.WriteLine("\n[Action] Verifying Sidebar Active Database Table Searching & Filtering...");
            Console.WriteLine($"Active Database: {testDbNode.DatabaseName}, Initial Tables Count: {testDbNode.TablesFolder.Children.Count}");

            // 1. Search matching table name 'user'
            vm.Sidebar.TableSearchFilter = "user";
            Console.WriteLine($"Filtered with 'user': Tables Count = {testDbNode.TablesFolder.Children.Count}, HasSearchFilter = {vm.Sidebar.HasSearchFilter}");
            if (testDbNode.TablesFolder.Children.Count != 1)
            {
                throw new Exception($"Expected 1 matching table for 'user', got {testDbNode.TablesFolder.Children.Count}!");
            }
            if (((Fdb.ViewModels.TableTreeNode)testDbNode.TablesFolder.Children[0]).TableName != "Users")
            {
                throw new Exception("Filtered table did not match 'Users'!");
            }

            // 2. Search non-existent table name 'nonexistent'
            vm.Sidebar.TableSearchFilter = "nonexistent";
            Console.WriteLine($"Filtered with 'nonexistent': Tables Count = {testDbNode.TablesFolder.Children.Count}");
            if (testDbNode.TablesFolder.Children.Count != 0)
            {
                throw new Exception($"Expected 0 matching tables for 'nonexistent', got {testDbNode.TablesFolder.Children.Count}!");
            }

            // 3. Test ClearSearchFilterCommand
            vm.Sidebar.ClearSearchFilterCommand.Execute(null);
            Console.WriteLine($"After clear: Filter='{vm.Sidebar.TableSearchFilter}', Tables Count = {testDbNode.TablesFolder.Children.Count}");
            if (testDbNode.TablesFolder.Children.Count != 1 || vm.Sidebar.HasSearchFilter)
            {
                throw new Exception("Clearing search filter failed to restore all tables!");
            }

            // 4. Verify tableItem still retains its Active state
            if (!tableItem.IsActive || vm.Sidebar.ActiveTableNode != tableItem)
            {
                throw new Exception("Table node did not stay active/highlighted after filter clear!");
            }
            Console.WriteLine("[Success] Real-time table searching, filtering, and active table highlighting verified!");

            // 13.5 Test Table Auto-Completion from Active Database (e.g. SELECT * FROM u -> Users)
            Console.WriteLine("\n[Action] Verifying Active Database Table Auto-Completion in Editor...");
            Console.WriteLine($"Active Database in KeywordsService: '{Fdb.Services.SqlKeywordsService.Instance.ActiveDatabaseName}', Tables Count: {Fdb.Services.SqlKeywordsService.Instance.ActiveTables.Count}");
            if (Fdb.Services.SqlKeywordsService.Instance.ActiveTables.Count != 1 || Fdb.Services.SqlKeywordsService.Instance.ActiveTables[0].TableName != "Users")
            {
                throw new Exception($"Expected active tables to contain 'Users', got {Fdb.Services.SqlKeywordsService.Instance.ActiveTables.Count} table(s)!");
            }
            bool isFromTableContext = Fdb.Components.SqlIntelliSenseOverlayComponent.IsTableContext("SELECT * FROM ");
            bool isJoinTableContext = Fdb.Components.SqlIntelliSenseOverlayComponent.IsTableContext("SELECT * FROM Orders JOIN ");
            bool isSelectContext = Fdb.Components.SqlIntelliSenseOverlayComponent.IsTableContext("SELECT ");
            Console.WriteLine($"Context Detection: 'FROM ' -> {isFromTableContext}, 'JOIN ' -> {isJoinTableContext}, 'SELECT ' -> {isSelectContext}");
            if (!isFromTableContext || !isJoinTableContext || isSelectContext)
            {
                throw new Exception("IsTableContext returned incorrect result for SQL clauses!");
            }
            Console.WriteLine("[Success] Active database table auto-completion verified!");

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

            // Verify Persian / Unicode Text UPDATE & Literal Encoding
            Console.WriteLine("\n[Action] Verifying Persian / Unicode Text UPDATE Query Generation & Execution...");
            // Verify FormatLiteral across dialects
            string mssqlPersian = Fdb.ViewModels.EditCellModalViewModel.FormatLiteral("احسان رضایی", "MSSQL");
            string sqlitePersian = Fdb.ViewModels.EditCellModalViewModel.FormatLiteral("احسان رضایی", "SQLITE");
            string mysqlPersian = Fdb.ViewModels.EditCellModalViewModel.FormatLiteral("احسان رضایی", "MYSQL");
            string pgPersian = Fdb.ViewModels.EditCellModalViewModel.FormatLiteral("احسان رضایی", "POSTGRESQL");
            Console.WriteLine($"Persian Formatted Literals:\nMSSQL: {mssqlPersian}\nSQLite: {sqlitePersian}\nMySQL: {mysqlPersian}\nPostgres: {pgPersian}");
            if (mssqlPersian != "N'احسان رضایی'") throw new Exception($"MSSQL must use N prefix for Persian text! Got '{mssqlPersian}'");
            if (sqlitePersian != "'احسان رضایی'") throw new Exception($"SQLite must use standard quotes without N prefix! Got '{sqlitePersian}'");
            if (mysqlPersian != "N'احسان رضایی'") throw new Exception($"MySQL must use N prefix for Unicode! Got '{mysqlPersian}'");
            if (pgPersian != "N'احسان رضایی'") throw new Exception($"Postgres must use N prefix for Unicode! Got '{pgPersian}'");

            // Verify MSSQL UPDATE query with N prefix for Persian text
            string mssqlUpdate = Fdb.ViewModels.EditCellModalViewModel.BuildUpdateQuery("Users", "Name", "احسان رضایی", firstRow, "MSSQL");
            Console.WriteLine($"Generated MSSQL Query for Persian Name:\n{mssqlUpdate}");
            if (!mssqlUpdate.Contains("N'احسان رضایی'") || !mssqlUpdate.Contains("SET [Name] = N'احسان رضایی'"))
            {
                throw new Exception($"MSSQL UPDATE query failed to contain 'SET [Name] = N\'احسان رضایی\'', got:\n{mssqlUpdate}");
            }

            // Test executing in the current active session (SQLite)
            vm.SampleQueryResult.OpenEditCell(firstRow, "Name");
            editModal.NewValue = "احسان رضایی";
            Console.WriteLine($"Generated Active DB Query ({editModal.DatabaseType}):\n{editModal.GeneratedQuery}");
            if (!editModal.GeneratedQuery.Contains("'احسان رضایی'"))
            {
                throw new Exception($"Expected active query to contain Persian text, got:\n{editModal.GeneratedQuery}");
            }
            await editModal.ExecuteAndSaveAsync();
            if (editModal.IsOpen || firstRow.Name != "احسان رضایی" || firstRow["Name"] != "احسان رضایی")
            {
                throw new Exception($"Row was not updated to Persian 'احسان رضایی'! Got Name='{firstRow.Name}'");
            }
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("احسان رضایی"))
            {
                throw new Exception("SQL Editor did not receive the executed Persian UPDATE statement!");
            }
            Console.WriteLine("[Success] Persian / Unicode text editing and UPDATE query generation verified!");

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

            // [Action] Verifying Multi-Item Selection & Batch Deletion (Deleting multiple selected rows & columns)
            Console.WriteLine("\n[Action] Verifying Multi-Item Deletion (Multiple Selected Rows & Columns)...");
            
            // 1. Reset to clean sample data (5 rows)
            await vm.SampleQueryResult.RefreshCommand.ExecuteAsync(null);
            Console.WriteLine($"Starting sample rows: {vm.SampleQueryResult.Rows.Count}");
            if (vm.SampleQueryResult.Rows.Count != 5) throw new Exception("Expected 5 sample rows!");

            // 2. Select 2 rows (Row 0 and Row 1)
            var row0 = vm.SampleQueryResult.Rows[0];
            var row1 = vm.SampleQueryResult.Rows[1];
            vm.SampleQueryResult.GetSelectedRowsCallback = () => new[] { row0, row1 };
            Console.WriteLine($"Simulated multi-row selection: 2 rows (Id={row0.Id}, Id={row1.Id})");

            // Test batch delete query generation
            vm.SampleQueryResult.GenerateDeleteQueryCommand.Execute(null);
            Console.WriteLine($"Batch DELETE query in editor: '{vm.Editor.SelectedTab?.Text}'");
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("101") || !vm.Editor.SelectedTab.Text.Contains("102"))
            {
                throw new Exception("Batch DELETE query failed to include both selected rows (101 and 102)!");
            }

            // Test deleting both rows with confirmation
            vm.SampleQueryResult.DeleteSelectedRowCommand.Execute(null);
            Console.WriteLine($"ConfirmModal open: {vm.ConfirmModal.IsOpen}, Target: '{vm.ConfirmModal.TargetName}'");
            if (!vm.ConfirmModal.IsOpen || !vm.ConfirmModal.TargetName!.Contains("101") || !vm.ConfirmModal.TargetName.Contains("102"))
            {
                throw new Exception("DeleteSelectedRow failed to prompt confirmation for both selected rows!");
            }
            vm.ConfirmModal.ConfirmCommand.Execute(null);
            Console.WriteLine($"After multi-row delete: Rows Count = {vm.SampleQueryResult.Rows.Count}, TotalRows = {vm.SampleQueryResult.TotalRows}");
            if (vm.SampleQueryResult.Rows.Count != 3 || vm.SampleQueryResult.TotalRows != 3)
            {
                throw new Exception($"Expected 3 rows remaining after deleting 2 rows, but got {vm.SampleQueryResult.Rows.Count}!");
            }
            if (System.Linq.Enumerable.Any(vm.SampleQueryResult.Rows, r => r.Id == 101 || r.Id == 102))
            {
                throw new Exception("Rows 101 or 102 still found in collection after multi-row delete!");
            }
            Console.WriteLine($"[Success] Multi-row batch deletion verified: '{vm.SampleQueryResult.LastQueryMessage}'");

            // 3. Test deleting multiple columns (e.g. 'Role' and 'Status')
            Console.WriteLine("[Action] Testing Multi-Column Deletion...");
            vm.SampleQueryResult.GetSelectedRowsCallback = null;
            vm.SampleQueryResult.GetSelectedColumnsCallback = () => new[] { "Role", "Status" };
            vm.SampleQueryResult.DeleteSelectedColumnCommand.Execute(null);
            Console.WriteLine($"ConfirmModal for columns: IsOpen={vm.ConfirmModal.IsOpen}, Target='{vm.ConfirmModal.TargetName}'");
            if (!vm.ConfirmModal.IsOpen || !vm.ConfirmModal.TargetName!.Contains("DROP COLUMN") || !vm.ConfirmModal.TargetName.Contains("Role"))
            {
                throw new Exception("DeleteSelectedColumn failed to open ConfirmModal with ALTER TABLE DROP COLUMN query!");
            }
            vm.ConfirmModal.ConfirmCommand.Execute(null);
            Console.WriteLine($"After column drop: Rows[0] contains Role={vm.SampleQueryResult.Rows[0].Values.ContainsKey("Role")}, Status={vm.SampleQueryResult.Rows[0].Values.ContainsKey("Status")}");
            if (vm.SampleQueryResult.Rows[0].Values.ContainsKey("Role") || vm.SampleQueryResult.Rows[0].Values.ContainsKey("Status"))
            {
                throw new Exception("Dropped columns still exist in row dictionary!");
            }
            Console.WriteLine($"[Success] Multi-column drop verified: '{vm.SampleQueryResult.LastQueryMessage}'");

            // 4. Verify PK has NO orange color in Light/Dark themes
            Console.WriteLine("\n[Action] Verifying PK has NO orange color...");
            var lightPkBrush = App.Current?.Resources["AppTablePkColumnForegroundBrush"] as Avalonia.Media.SolidColorBrush;
            Console.WriteLine($"Light PK Color: {lightPkBrush?.Color}");
            if (lightPkBrush?.Color.ToString() == "#FFD97706" || lightPkBrush?.Color.ToString() == "#FFF59E0B")
            {
                throw new Exception("PK brush still has orange color!");
            }
            Console.WriteLine("[Success] PK has neutral styling with zero orange coloring!");

            // 5. Test Batch Cell Editing (Selecting multiple names and applying a single value simultaneously)
            Console.WriteLine("\n[Action] Verifying Simultaneous Multi-Cell Editing (Batch Cell Edit)...");
            await vm.SampleQueryResult.RefreshCommand.ExecuteAsync(null);
            var bRow0 = vm.SampleQueryResult.Rows[0];
            var bRow1 = vm.SampleQueryResult.Rows[1];
            var bRow2 = vm.SampleQueryResult.Rows[2];
            Console.WriteLine($"Initial Names: Row0='{bRow0.Name}', Row1='{bRow1.Name}', Row2='{bRow2.Name}'");

            // Select 3 rows and column "Name"
            vm.SampleQueryResult.GetSelectedRowsCallback = () => new[] { bRow0, bRow1, bRow2 };
            vm.SampleQueryResult.GetSelectedColumnsCallback = () => new[] { "Name" };

            // Open edit modal for selected cells
            vm.SampleQueryResult.OpenEditSelectedCellCommand.Execute(null);
            var batchEditModal = vm.SampleQueryResult.EditCellModal;
            Console.WriteLine($"BatchEditModal State: IsOpen={batchEditModal.IsOpen}, IsBatchEdit={batchEditModal.IsBatchEdit}, Title='{batchEditModal.ModalTitle}', Rows={batchEditModal.BatchRowCount}");
            if (!batchEditModal.IsOpen || !batchEditModal.IsBatchEdit || batchEditModal.BatchRowCount != 3)
            {
                throw new Exception($"BatchEditModal failed to open in batch mode for 3 rows! IsOpen={batchEditModal.IsOpen}, IsBatch={batchEditModal.IsBatchEdit}");
            }

            // Enter new value "Antigravity"
            batchEditModal.NewValue = "Antigravity";
            Console.WriteLine($"Generated Batch UPDATE Query: " + batchEditModal.GeneratedQuery);
            if (!batchEditModal.GeneratedQuery.Contains("UPDATE") || !batchEditModal.GeneratedQuery.Contains("'Antigravity'") || !batchEditModal.GeneratedQuery.Contains("101") || !batchEditModal.GeneratedQuery.Contains("102") || !batchEditModal.GeneratedQuery.Contains("103"))
            {
                throw new Exception("Batch UPDATE query did not contain expected syntax or IDs: " + batchEditModal.GeneratedQuery);
            }

            // Save and Execute
            await batchEditModal.ExecuteAndSaveAsync();
            Console.WriteLine($"After Batch Save: Row0='{bRow0.Name}', Row1='{bRow1.Name}', Row2='{bRow2.Name}'");
            if (bRow0.Name != "Antigravity" || bRow1.Name != "Antigravity" || bRow2.Name != "Antigravity")
            {
                throw new Exception($"Batch cell edit failed! Expected all 3 names to be 'Antigravity', but got '{bRow0.Name}', '{bRow1.Name}', '{bRow2.Name}'");
            }
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("'Antigravity'"))
            {
                throw new Exception("SQL editor tab did not receive the executed batch UPDATE statement!");
            }
            Console.WriteLine($"[Success] Simultaneous multi-cell editing verified: '{vm.SampleQueryResult.LastQueryMessage}'");

            // 6. Test Foreign Key Indicators & Navigation Arrow to Referenced Table
            Console.WriteLine("\n[Action] Verifying Foreign Key (FK) Indicators & Navigation to Referenced Table...");
            
            // A. Reset to sample Users table
            await vm.SampleQueryResult.RefreshCommand.ExecuteAsync(null);
            Console.WriteLine($"Current Table: {vm.SampleQueryResult.CurrentTableName}");
            if (vm.SampleQueryResult.CurrentTableName != "Users") throw new Exception("Expected Users table!");

            // B. Verify FK detection on 'Role' column
            bool isRoleFk = vm.SampleQueryResult.IsColumnForeignKey("Role");
            var roleRel = vm.SampleQueryResult.GetForeignKeyRelation("Role");
            Console.WriteLine($"IsColumnForeignKey('Role'): {isRoleFk}, TargetTable: '{roleRel?.TargetTable}', TargetColumn: '{roleRel?.TargetColumn}'");
            if (!isRoleFk || roleRel == null || roleRel.TargetTable != "Roles" || roleRel.TargetColumn != "Title")
            {
                throw new Exception("Foreign Key relation detection failed for 'Role' column!");
            }

            // C. Verify heuristic FK detection on other common schema patterns
            var deptFk = Fdb.ViewModels.QueryResultTableViewModel.DetectProbableForeignKey("department_id", "Employees");
            Console.WriteLine($"Heuristic FK 'department_id': Table={deptFk?.TargetTable}, Col={deptFk?.TargetColumn}");
            if (deptFk == null || deptFk.TargetTable != "Departments" || deptFk.TargetColumn != "Id")
            {
                throw new Exception("Heuristic FK detection failed for 'department_id'!");
            }

            var custFk = Fdb.ViewModels.QueryResultTableViewModel.DetectProbableForeignKey("customer_id", "Orders");
            Console.WriteLine($"Heuristic FK 'customer_id': Table={custFk?.TargetTable}, Col={custFk?.TargetColumn}");
            if (custFk == null || custFk.TargetTable != "Customers" || custFk.TargetColumn != "Id")
            {
                throw new Exception("Heuristic FK detection failed for 'customer_id'!");
            }

            // D. Test Foreign Key Navigation to referenced 'Roles' table
            Console.WriteLine("[Action] Clicking FK navigation arrow on cell (Role = 'Developer')...");
            await vm.SampleQueryResult.NavigateToForeignKeyAsync("Role", "Developer");

            Console.WriteLine($"After FK Navigation: CurrentTableName='{vm.SampleQueryResult.CurrentTableName}', TotalRows={vm.SampleQueryResult.TotalRows}");
            Console.WriteLine($"Generated Navigation Query: '{vm.Editor.SelectedTab?.Text}'");
            Console.WriteLine($"Status Message: '{vm.SampleQueryResult.LastQueryMessage}'");

            if (vm.SampleQueryResult.CurrentTableName != "Roles")
            {
                throw new Exception($"Expected CurrentTableName to be 'Roles', got '{vm.SampleQueryResult.CurrentTableName}'!");
            }
            if (vm.SampleQueryResult.Rows.Count != 4)
            {
                throw new Exception($"Expected 4 role items in Roles table, got {vm.SampleQueryResult.Rows.Count}!");
            }
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("Roles") || !vm.Editor.SelectedTab.Text.Contains("Developer"))
            {
                throw new Exception("Editor failed to receive referenced table navigation query!");
            }
            if (!vm.SampleQueryResult.LastQueryMessage.Contains("Navigated to Roles via FK"))
            {
                throw new Exception($"Notification status message missing expected FK text: '{vm.SampleQueryResult.LastQueryMessage}'");
            }
            if (vm.SampleQueryResult.SelectedRow == null || vm.SampleQueryResult.SelectedRow["TITLE"] != "Developer")
            {
                throw new Exception($"Referenced row was not selected or title was unexpected: {vm.SampleQueryResult.SelectedRow?["TITLE"]}");
            }
            Console.WriteLine("[Success] Foreign Key visual indicators and arrow navigation to referenced table verified!");
// E. Test Persian & English Course (دوره) -> Users (کاربران) Navigation
            Console.WriteLine("\n[Action] Testing Course (دوره) Foreign Key -> User Navigation...");
            var courseFk = Fdb.ViewModels.QueryResultTableViewModel.DetectProbableForeignKey("دوره");
            Console.WriteLine($"DetectProbableForeignKey('دوره'): TargetTable={courseFk?.TargetTable}, TargetColumn={courseFk?.TargetColumn}");
            if (courseFk == null || courseFk.TargetTable != "Users")
            {
                throw new Exception("Foreign Key detection failed for 'دوره' linking to Users!");
            }

            var studentFk = Fdb.ViewModels.QueryResultTableViewModel.DetectProbableForeignKey("نام_دانشجو");
            Console.WriteLine($"DetectProbableForeignKey('نام_دانشجو'): TargetTable={studentFk?.TargetTable}, TargetColumn={studentFk?.TargetColumn}");
            if (studentFk == null || studentFk.TargetTable != "Users")
            {
                throw new Exception("Foreign Key detection failed for 'نام_دانشجو' linking to Users!");
            }

            // Load Courses table
            vm.SampleQueryResult.LoadSampleReferencedTable("Courses");
            Console.WriteLine($"Loaded Courses table: Rows={vm.SampleQueryResult.Rows.Count}, CurrentTableName='{vm.SampleQueryResult.CurrentTableName}'");
            if (vm.SampleQueryResult.CurrentTableName != "Courses" || vm.SampleQueryResult.Rows.Count != 5)
            {
                throw new Exception("Failed to load Courses table!");
            }

            // Click navigation arrow on course 'C# & Avalonia UI'
            Console.WriteLine("[Action] Navigating from Course 'C# & Avalonia UI' to User via Foreign Key...");
            await vm.SampleQueryResult.NavigateToForeignKeyAsync("دوره", "C# & Avalonia UI");

            Console.WriteLine($"After Course->User Navigation: CurrentTable='{vm.SampleQueryResult.CurrentTableName}', SelectedUser='{vm.SampleQueryResult.SelectedRow?.Name}'");
            Console.WriteLine($"Generated Query: '{vm.Editor.SelectedTab?.Text}'");
            Console.WriteLine($"Status: '{vm.SampleQueryResult.LastQueryMessage}'");

            if (vm.SampleQueryResult.CurrentTableName != "Users")
            {
                throw new Exception("Navigation failed to navigate to Users table!");
            }
            if (vm.SampleQueryResult.SelectedRow == null || vm.SampleQueryResult.SelectedRow.Name != "Alice Johnson")
            {
                throw new Exception($"Expected selected user to be 'Alice Johnson', got '{vm.SampleQueryResult.SelectedRow?.Name}'!");
            }
            if (vm.Editor.SelectedTab == null || !vm.Editor.SelectedTab.Text.Contains("Users") || !vm.Editor.SelectedTab.Text.Contains("C# & Avalonia UI"))
            {
                throw new Exception("Editor failed to receive User selection query for Course!");
            }
            Console.WriteLine("[Success] Course (دوره) -> User navigation and item retrieval verified!");

            // [Action] Verifying Hardware Graphics Adapter Detection, GPU vs CPU Modes & SQLite Persistence
            Console.WriteLine("\n[Action] Verifying Hardware Graphics Adapter Detection, GPU/CPU Modes & SQLite Persistence...");
            var graphicsService = Fdb.Services.HardwareGraphicsService.Instance;
            graphicsService.DetectHardwareGpu();
            Console.WriteLine($"Detected GPU Adapter: '{graphicsService.GpuAdapterName}', IsGpuDetected={graphicsService.IsGpuDetected}");
            if (string.IsNullOrWhiteSpace(graphicsService.GpuAdapterName))
            {
                throw new Exception("Hardware graphics adapter detection returned empty adapter name!");
            }

            // Verify effective mode resolution
            string resolvedGpu = graphicsService.ResolveEffectiveMode("GPU");
            string resolvedCpu = graphicsService.ResolveEffectiveMode("CPU");
            string resolvedAuto = graphicsService.ResolveEffectiveMode("Auto");
            Console.WriteLine($"Resolved Modes: GPU='{resolvedGpu}', CPU='{resolvedCpu}', Auto='{resolvedAuto}'");
            if (resolvedGpu != "GPU" || resolvedCpu != "CPU")
            {
                throw new Exception("Effective mode resolution for GPU/CPU failed!");
            }
            if (resolvedAuto != (graphicsService.IsGpuDetected ? "GPU" : "CPU"))
            {
                throw new Exception("Auto mode resolution did not match hardware detection state!");
            }

            // Verify splash screen initialization routine
            await graphicsService.InitializeAsync();
            Console.WriteLine($"Initialized Summary: '{graphicsService.ActiveRendererSummary}', VRAM Cache={graphicsService.VramCacheMb}MB");
            if (graphicsService.VramCacheMb != 128 && graphicsService.VramCacheMb != 16)
            {
                throw new Exception($"Invalid VRAM cache limit: {graphicsService.VramCacheMb}MB");
            }

            // Verify Settings Modal Performance category & live selection
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectCategory("Performance");
            if (!vm.SettingsModal.IsPerformanceCategory)
            {
                throw new Exception("Failed to navigate to Performance & GPU settings category!");
            }

            // Test selecting CPU mode & persisting to SQLite
            vm.SettingsModal.SelectRenderingMode("CPU");
            if (!vm.SettingsModal.IsCpuRenderingMode || vm.SettingsModal.SelectedRenderingMode != "CPU")
            {
                throw new Exception("Failed to switch selected rendering mode to CPU!");
            }
            await vm.SettingsModal.SaveAsync();

            string dbModeAfterCpu = await Fdb.Services.DatabaseStorageService.Instance.GetGraphicsRenderingModeAsync();
            Console.WriteLine($"SQLite Saved GraphicsRenderingMode: '{dbModeAfterCpu}'");
            if (dbModeAfterCpu != "CPU")
            {
                throw new Exception($"Expected SQLite to persist 'CPU', got '{dbModeAfterCpu}'!");
            }

            // Test revert on Close()
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectRenderingMode("GPU");
            vm.SettingsModal.Close();
            if (vm.SettingsModal.SelectedRenderingMode != "CPU")
            {
                throw new Exception("Close() failed to revert uncommitted rendering mode change!");
            }

            // Reset back to Auto mode and verify SQLite
            await vm.SettingsModal.OpenAsync();
            vm.SettingsModal.SelectRenderingMode("Auto");
            await vm.SettingsModal.SaveAsync();
            string dbModeAfterAuto = await Fdb.Services.DatabaseStorageService.Instance.GetGraphicsRenderingModeAsync();
            Console.WriteLine($"SQLite Reset GraphicsRenderingMode: '{dbModeAfterAuto}'");
            if (dbModeAfterAuto != "Auto")
            {
                throw new Exception($"Expected SQLite to persist 'Auto', got '{dbModeAfterAuto}'!");
            }
            Console.WriteLine("[Success] Hardware graphics detection, GPU vs CPU mode switching, and SQLite persistence verified!");

            Console.WriteLine("\n>>> ALL TESTS (LAYOUT, TABLES, MODALS, TABS, SQL AUTOCOMPLETE, MULTI-DIALECT SQLITE, VECTOR ICONS, SPLASH SCREEN, STORAGE, DYNAMIC DATABASE PROVIDERS, ISOLATED STARTUP, HIERARCHICAL TREE VIEW, REAL QUERY, TARGET DATABASE ROUTING, DISCONNECT/DELETE, GRAPHICAL FOOTER CELL UPDATE, PRIMARY KEY NEUTRAL STYLING, MULTI-ROW BATCH DELETION, MULTI-COLUMN DROP, FOOTER REFRESH/FILTER, SIMULTANEOUS BATCH CELL EDITING, FOREIGN KEY NAVIGATION & HARDWARE GPU/CPU RENDERING PIPELINE) PASSED! <<<");
            return;
        }

        BuildAvaloniaApp()
            .StartWithClassicDesktopLifetime(args);
    }

    public static AppBuilder BuildAvaloniaApp()
    {
        var builder = AppBuilder.Configure<App>()
            .UsePlatformDetect()
#if DEBUG
            .WithDeveloperTools()
#endif
            .WithInterFont()
            .LogToTrace();

        // Hardware Graphics & Rendering Configuration (DirectX/ANGLE GPU vs CPU Software)
        Fdb.Services.HardwareGraphicsService.Instance.ConfigureAppBuilder(builder);

        return builder;
    }
}
