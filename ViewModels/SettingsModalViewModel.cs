using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Media;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fdb.Services;

namespace Fdb.ViewModels;

public partial class SettingsModalViewModel : ObservableObject
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAppearanceCategory))]
    [NotifyPropertyChangedFor(nameof(IsTableCategory))]
    [NotifyPropertyChangedFor(nameof(IsCustomizationCategory))]
    [NotifyPropertyChangedFor(nameof(IsShortcutsCategory))]
    [NotifyPropertyChangedFor(nameof(IsPerformanceCategory))]
    [NotifyPropertyChangedFor(nameof(IsTerminalCategory))]
    private string _selectedCategory = "Appearance";

    public bool IsAppearanceCategory => string.Equals(SelectedCategory, "Appearance", StringComparison.OrdinalIgnoreCase);
    public bool IsTableCategory => string.Equals(SelectedCategory, "Table", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomizationCategory => string.Equals(SelectedCategory, "Customization", StringComparison.OrdinalIgnoreCase);
    public bool IsShortcutsCategory => string.Equals(SelectedCategory, "Shortcuts", StringComparison.OrdinalIgnoreCase);
    public bool IsPerformanceCategory => string.Equals(SelectedCategory, "Performance", StringComparison.OrdinalIgnoreCase);
    public bool IsTerminalCategory => string.Equals(SelectedCategory, "Terminal", StringComparison.OrdinalIgnoreCase);

    #region Appearance & Theme Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsLightTheme))]
    [NotifyPropertyChangedFor(nameof(IsDarkTheme))]
    [NotifyPropertyChangedFor(nameof(IsSystemTheme))]
    private string _selectedTheme = "Light";

    public bool IsLightTheme => string.Equals(SelectedTheme, "Light", StringComparison.OrdinalIgnoreCase);
    public bool IsDarkTheme => string.Equals(SelectedTheme, "Dark", StringComparison.OrdinalIgnoreCase);
    public bool IsSystemTheme => string.Equals(SelectedTheme, "System", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsBlueKeyword))]
    [NotifyPropertyChangedFor(nameof(IsGreenKeyword))]
    [NotifyPropertyChangedFor(nameof(IsPurpleKeyword))]
    [NotifyPropertyChangedFor(nameof(IsAmberKeyword))]
    [NotifyPropertyChangedFor(nameof(IsRoseKeyword))]
    [NotifyPropertyChangedFor(nameof(IsCyanKeyword))]
    [NotifyPropertyChangedFor(nameof(CurrentKeywordBrush))]
    private string _selectedKeywordColor = "Blue";

    public bool IsBlueKeyword => string.Equals(SelectedKeywordColor, "Blue", StringComparison.OrdinalIgnoreCase);
    public bool IsGreenKeyword => string.Equals(SelectedKeywordColor, "Green", StringComparison.OrdinalIgnoreCase);
    public bool IsPurpleKeyword => string.Equals(SelectedKeywordColor, "Purple", StringComparison.OrdinalIgnoreCase);
    public bool IsAmberKeyword => string.Equals(SelectedKeywordColor, "Amber", StringComparison.OrdinalIgnoreCase);
    public bool IsRoseKeyword => string.Equals(SelectedKeywordColor, "Rose", StringComparison.OrdinalIgnoreCase);
    public bool IsCyanKeyword => string.Equals(SelectedKeywordColor, "Cyan", StringComparison.OrdinalIgnoreCase);

    public IBrush CurrentKeywordBrush => ThemeService.Instance.CurrentKeywordBrush;

    private string _initialTheme = "Light";
    private string _initialKeywordColor = "Blue";

    #endregion

    #region Workspace & Panels Customization Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEditorBackgroundBrush))]
    private string _editorBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewEditorForegroundBrush))]
    private string _editorForeground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSidebarBackgroundBrush))]
    private string _sidebarBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSidebarHeaderBrush))]
    private string _sidebarHeaderBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSidebarHoverBrush))]
    private string _sidebarItemHoverBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSidebarTextBrush))]
    private string _sidebarTextColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSidebarBorderBrush))]
    private string _sidebarBorderColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonAccentBrush))]
    private string _buttonAccentColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonBackgroundBrush))]
    private string _buttonBackgroundColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonTextBrush))]
    private string _buttonTextColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewButtonBorderBrush))]
    private string _buttonBorderColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewBorderBrush))]
    [NotifyPropertyChangedFor(nameof(PreviewSidebarBorderBrush))]
    [NotifyPropertyChangedFor(nameof(PreviewButtonBorderBrush))]
    [NotifyPropertyChangedFor(nameof(PreviewSplitterBrush))]
    private string _borderColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewSplitterBrush))]
    private string _splitterColor = "Default";

    [ObservableProperty]
    private double _editorFontSize = 13.0;

    [ObservableProperty]
    private bool _editorShowLineNumbers = true;

    #region Color Maker Properties

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorMakerPreviewBrush))]
    [NotifyPropertyChangedFor(nameof(ColorMakerRgbText))]
    [NotifyPropertyChangedFor(nameof(ColorMakerIsDarkText))]
    private string _colorMakerHex = "#3B82F6";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorMakerPreviewBrush))]
    [NotifyPropertyChangedFor(nameof(ColorMakerRgbText))]
    [NotifyPropertyChangedFor(nameof(ColorMakerIsDarkText))]
    private double _colorMakerR = 59;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorMakerPreviewBrush))]
    [NotifyPropertyChangedFor(nameof(ColorMakerRgbText))]
    [NotifyPropertyChangedFor(nameof(ColorMakerIsDarkText))]
    private double _colorMakerG = 130;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ColorMakerPreviewBrush))]
    [NotifyPropertyChangedFor(nameof(ColorMakerRgbText))]
    [NotifyPropertyChangedFor(nameof(ColorMakerIsDarkText))]
    private double _colorMakerB = 246;

    [ObservableProperty]
    private string _colorMakerSelectedTarget = "Buttons: Accent / Action";

    public ObservableCollection<string> ColorMakerTargets { get; } = new()
    {
        "Buttons: Accent / Action",
        "Buttons: Background",
        "Buttons: Text",
        "Buttons: Border",
        "Borders: Global Panels & Dividers",
        "Borders: Splitters & Separators",
        "Sidebar: Main Background",
        "Sidebar: Header & Toolbar",
        "Sidebar: Item Hover / Selection",
        "Sidebar: Text",
        "Sidebar: Border",
        "Text Editor: Background",
        "Text Editor: Text",
        "Table: Background",
        "Table: Alternating Row",
        "Table: Header Background",
        "Table: Grid Lines",
        "Table: Cell Text"
    };

    public IBrush ColorMakerPreviewBrush
    {
        get
        {
            if (Color.TryParse(ColorMakerHex, out var col))
                return new SolidColorBrush(col);
            return new SolidColorBrush(Color.FromRgb((byte)Math.Clamp(ColorMakerR, 0, 255), (byte)Math.Clamp(ColorMakerG, 0, 255), (byte)Math.Clamp(ColorMakerB, 0, 255)));
        }
    }

    public string ColorMakerRgbText => $"RGB({(int)ColorMakerR}, {(int)ColorMakerG}, {(int)ColorMakerB})";

    public bool ColorMakerIsDarkText
    {
        get
        {
            double yiq = ((ColorMakerR * 299) + (ColorMakerG * 587) + (ColorMakerB * 114)) / 1000.0;
            return yiq >= 128;
        }
    }

    #endregion

    private PanelCustomizationRecord _initialPanelCustomization = new();

    public IBrush PreviewEditorBackgroundBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(EditorBackground) && !EditorBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(EditorBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#0F172A")) : new SolidColorBrush(Color.Parse("#FFFFFF"));
        }
    }

    public IBrush PreviewEditorForegroundBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(EditorForeground) && !EditorForeground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(EditorForeground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
    }

    public IBrush PreviewSidebarBackgroundBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SidebarBackground) && !SidebarBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(SidebarBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E293B")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
        }
    }

    public IBrush PreviewSidebarHeaderBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SidebarHeaderBackground) && !SidebarHeaderBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(SidebarHeaderBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E293B")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
        }
    }

    public IBrush PreviewSidebarHoverBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SidebarItemHoverBackground) && !SidebarItemHoverBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(SidebarItemHoverBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#334155")) : new SolidColorBrush(Color.Parse("#F1F5F9"));
        }
    }

    public IBrush PreviewSidebarTextBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SidebarTextColor) && !SidebarTextColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(SidebarTextColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
    }

    public IBrush PreviewSidebarBorderBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SidebarBorderColor) && !SidebarBorderColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(SidebarBorderColor, out var color))
                return new SolidColorBrush(color);
            return PreviewBorderBrush;
        }
    }

    public IBrush PreviewButtonAccentBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ButtonAccentColor) && !ButtonAccentColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(ButtonAccentColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#3B82F6")) : new SolidColorBrush(Color.Parse("#2563EB"));
        }
    }

    public IBrush PreviewButtonBackgroundBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ButtonBackgroundColor) && !ButtonBackgroundColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(ButtonBackgroundColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E293B")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
        }
    }

    public IBrush PreviewButtonTextBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ButtonTextColor) && !ButtonTextColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(ButtonTextColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
    }

    public IBrush PreviewButtonBorderBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(ButtonBorderColor) && !ButtonBorderColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(ButtonBorderColor, out var color))
                return new SolidColorBrush(color);
            return PreviewBorderBrush;
        }
    }

    public IBrush PreviewBorderBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(BorderColor) && !BorderColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(BorderColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#334155")) : new SolidColorBrush(Color.Parse("#E2E8F0"));
        }
    }

    public IBrush PreviewSplitterBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(SplitterColor) && !SplitterColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(SplitterColor, out var color))
                return new SolidColorBrush(color);
            return PreviewBorderBrush;
        }
    }

    #endregion

    #region Query Results Table Customization Properties

    [ObservableProperty]
    private double _tableRowHeight = 25.0;

    [ObservableProperty]
    private double _tableHeaderHeight = 26.0;

    [ObservableProperty]
    private double _tableGutterWidth = 56.0;

    [ObservableProperty]
    private double _tableCellFontSize = 11.0;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableMainRowBrush))]
    private string _tableBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableAltRowBrush))]
    private string _tableAlternatingRowColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableCellTextBrush))]
    private string _tableCellTextColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableHeaderBgBrush))]
    private string _tableHeaderBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableHeaderTextBrush))]
    private string _tableHeaderTextColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableGutterBgBrush))]
    private string _tableGutterBackground = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableGutterTextBrush))]
    private string _tableGutterTextColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableGridLinesBrush))]
    private string _tableGridLinesColor = "Default";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(PreviewTableSelectionBrush))]
    private string _tableSelectionColor = "Default";

    [ObservableProperty]
    private bool _tableShowGridLines = true;

    [ObservableProperty]
    private bool _tableShowAlternatingRowColors = true;

    private TableCustomizationRecord _initialTableCustomization = new();

    public IBrush PreviewTableMainRowBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableBackground) && !TableBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#0F172A")) : new SolidColorBrush(Color.Parse("#FFFFFF"));
        }
    }

    public IBrush PreviewTableBackgroundBrush => PreviewTableMainRowBrush;

    public IBrush PreviewTableAltRowBrush
    {
        get
        {
            if (!TableShowAlternatingRowColors) return PreviewTableMainRowBrush;
            if (!string.IsNullOrWhiteSpace(TableAlternatingRowColor) && !TableAlternatingRowColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableAlternatingRowColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#141E2E")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
        }
    }

    public IBrush PreviewTableCellTextBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableCellTextColor) && !TableCellTextColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableCellTextColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
    }

    public IBrush PreviewTableHeaderBgBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableHeaderBackground) && !TableHeaderBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableHeaderBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E293B")) : new SolidColorBrush(Color.Parse("#F1F5F9"));
        }
    }

    public IBrush PreviewTableHeaderTextBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableHeaderTextColor) && !TableHeaderTextColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableHeaderTextColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
    }

    public IBrush PreviewTableGutterBgBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableGutterBackground) && !TableGutterBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableGutterBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E293B")) : new SolidColorBrush(Color.Parse("#F1F5F9"));
        }
    }

    public IBrush PreviewTableGutterTextBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableGutterTextColor) && !TableGutterTextColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableGutterTextColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#F8FAFC")) : new SolidColorBrush(Color.Parse("#0F172A"));
        }
    }

    public IBrush PreviewTableGridLinesBrush
    {
        get
        {
            if (!TableShowGridLines) return Brushes.Transparent;
            if (!string.IsNullOrWhiteSpace(TableGridLinesColor) && !TableGridLinesColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableGridLinesColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#334155")) : new SolidColorBrush(Color.Parse("#E2E8F0"));
        }
    }

    public IBrush PreviewTableSelectionBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableSelectionColor) && !TableSelectionColor.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableSelectionColor, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#1E3A5F")) : new SolidColorBrush(Color.Parse("#DBEAFE"));
        }
    }

    #endregion

    #region Shortcuts Properties

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();
    public ObservableCollection<ShortcutItem> FilteredShortcuts { get; } = new();

    [ObservableProperty]
    private string _shortcutSearchFilter = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAllShortcutsCategory))]
    [NotifyPropertyChangedFor(nameof(IsQueryShortcutsCategory))]
    [NotifyPropertyChangedFor(nameof(IsEditorShortcutsCategory))]
    [NotifyPropertyChangedFor(nameof(IsPanelsShortcutsCategory))]
    private string _selectedShortcutCategory = "All";

    public bool IsAllShortcutsCategory => string.Equals(SelectedShortcutCategory, "All", StringComparison.OrdinalIgnoreCase);
    public bool IsQueryShortcutsCategory => string.Equals(SelectedShortcutCategory, "Query & Execution", StringComparison.OrdinalIgnoreCase);
    public bool IsEditorShortcutsCategory => string.Equals(SelectedShortcutCategory, "File & Editor", StringComparison.OrdinalIgnoreCase);
    public bool IsPanelsShortcutsCategory => string.Equals(SelectedShortcutCategory, "Panels & View", StringComparison.OrdinalIgnoreCase);

    #endregion

    #region Terminal Settings Properties

    [ObservableProperty]
    private string _terminalDefaultShell = "default";

    [ObservableProperty]
    private string _terminalFontFamily = "Cascadia Code, Consolas, Courier New, monospace";

    [ObservableProperty]
    private double _terminalFontSize = 13.0;

    [ObservableProperty]
    private string _terminalCursorStyle = "Block";

    [ObservableProperty]
    private bool _terminalCursorBlink = true;

    [ObservableProperty]
    private int _terminalMaxScrollback = 5000;

    [ObservableProperty]
    private string _terminalPsqlPath = string.Empty;

    [ObservableProperty]
    private string _terminalMySqlPath = string.Empty;

    [ObservableProperty]
    private string _terminalSqlitePath = string.Empty;

    [ObservableProperty]
    private string _terminalSqlCmdPath = string.Empty;

    public System.Collections.ObjectModel.ObservableCollection<Fdb.Models.ShellProfile> AvailableTerminalShells { get; } = new();

    #endregion

    public SettingsModalViewModel()
    {
        _selectedTheme = ThemeService.Instance.CurrentTheme;
        _selectedKeywordColor = ThemeService.Instance.CurrentKeywordColor;
    }

    [RelayCommand]
    public void SelectCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        SelectedCategory = category;
    }

    [RelayCommand]
    public async Task OpenAsync()
    {
        try
        {
            // 1. Load Theme & Keywords from SQLite
            _initialTheme = await DatabaseStorageService.Instance.GetThemeAsync().ConfigureAwait(false);
            SelectedTheme = _initialTheme;

            _initialKeywordColor = await DatabaseStorageService.Instance.GetKeywordColorAsync().ConfigureAwait(false);
            SelectedKeywordColor = _initialKeywordColor;

            // 2. Load Panel Customizations from SQLite
            _initialPanelCustomization = await DatabaseStorageService.Instance.GetPanelCustomizationAsync().ConfigureAwait(false);
            EditorBackground = _initialPanelCustomization.EditorBackground;
            EditorForeground = _initialPanelCustomization.EditorForeground;
            SidebarBackground = _initialPanelCustomization.SidebarBackground;
            SidebarHeaderBackground = _initialPanelCustomization.SidebarHeaderBackground;
            SidebarItemHoverBackground = _initialPanelCustomization.SidebarItemHoverBackground;
            SidebarTextColor = _initialPanelCustomization.SidebarTextColor;
            SidebarBorderColor = _initialPanelCustomization.SidebarBorderColor;
            ButtonAccentColor = _initialPanelCustomization.ButtonAccentColor;
            ButtonBackgroundColor = _initialPanelCustomization.ButtonBackgroundColor;
            ButtonTextColor = _initialPanelCustomization.ButtonTextColor;
            ButtonBorderColor = _initialPanelCustomization.ButtonBorderColor;
            BorderColor = _initialPanelCustomization.BorderColor;
            SplitterColor = _initialPanelCustomization.SplitterColor;
            EditorFontSize = _initialPanelCustomization.EditorFontSize;
            EditorShowLineNumbers = _initialPanelCustomization.EditorShowLineNumbers;

            // 3. Load Table Customizations from SQLite
            _initialTableCustomization = await DatabaseStorageService.Instance.GetTableCustomizationAsync().ConfigureAwait(false);
            TableRowHeight = _initialTableCustomization.RowHeight;
            TableHeaderHeight = _initialTableCustomization.HeaderHeight;
            TableGutterWidth = _initialTableCustomization.GutterWidth;
            TableCellFontSize = _initialTableCustomization.FontSize;
            TableBackground = _initialTableCustomization.TableBackground;
            TableAlternatingRowColor = _initialTableCustomization.AlternatingRowColor;
            TableCellTextColor = _initialTableCustomization.CellTextColor;
            TableHeaderBackground = _initialTableCustomization.HeaderBackground;
            TableHeaderTextColor = _initialTableCustomization.HeaderTextColor;
            TableGutterBackground = _initialTableCustomization.GutterBackground;
            TableGutterTextColor = _initialTableCustomization.GutterTextColor;
            TableGridLinesColor = _initialTableCustomization.GridLinesColor;
            TableSelectionColor = _initialTableCustomization.SelectionColor;
            TableShowGridLines = _initialTableCustomization.ShowGridLines;
            TableShowAlternatingRowColors = _initialTableCustomization.ShowAlternatingRowColors;

            // 4. Load Dynamic Shortcuts from SQLite
            await ShortcutService.Instance.InitializeAsync().ConfigureAwait(false);
            LoadShortcutsList();

            // 5. Load Performance & Graphics Configuration from SQLite
            _initialRenderingMode = await DatabaseStorageService.Instance.GetGraphicsRenderingModeAsync().ConfigureAwait(false);
            SelectedRenderingMode = _initialRenderingMode;
            GpuAdapterName = HardwareGraphicsService.Instance.GpuAdapterName;
            IsGpuDetected = HardwareGraphicsService.Instance.IsGpuDetected;
            ActiveRendererSummary = HardwareGraphicsService.Instance.ActiveRendererSummary;
            VramCacheMb = HardwareGraphicsService.Instance.VramCacheMb;
            OnPropertyChanged(nameof(EffectiveModeDescription));
            OnPropertyChanged(nameof(RequiresGraphicsRestart));
            // 5. Load Terminal Settings from SQLite
            var termSettings = await Fdb.Services.Terminal.TerminalSettingsService.Instance.LoadSettingsAsync().ConfigureAwait(false);
            TerminalDefaultShell = termSettings.DefaultShellId;
            TerminalFontFamily = termSettings.FontFamily;
            TerminalFontSize = termSettings.FontSize;
            TerminalCursorStyle = termSettings.CursorStyle;
            TerminalCursorBlink = termSettings.CursorBlink;
            TerminalMaxScrollback = termSettings.MaxScrollback;
            TerminalPsqlPath = termSettings.PsqlPath;
            TerminalMySqlPath = termSettings.MySqlPath;
            TerminalSqlitePath = termSettings.SqlitePath;
            TerminalSqlCmdPath = termSettings.SqlCmdPath;

            AvailableTerminalShells.Clear();
            foreach (var sh in Fdb.Services.Terminal.ShellDetectorService.Instance.GetAvailableShells())
            {
                AvailableTerminalShells.Add(sh);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsModalViewModel] OpenAsync error: {ex.Message}");
            SelectedTheme = ThemeService.Instance.CurrentTheme;
            _initialTheme = SelectedTheme;
            SelectedKeywordColor = ThemeService.Instance.CurrentKeywordColor;
            _initialKeywordColor = SelectedKeywordColor;
            SelectedRenderingMode = HardwareGraphicsService.Instance.RenderingMode;
            _initialRenderingMode = SelectedRenderingMode;
        }

        IsOpen = true;
    }

    private void LoadShortcutsList()
    {
        Shortcuts.Clear();
        foreach (var s in ShortcutService.Instance.Shortcuts)
        {
            Shortcuts.Add(new ShortcutItem
            {
                Id = s.Id,
                Name = s.Name,
                Category = s.Category,
                Description = s.Description,
                DefaultGesture = s.DefaultGesture,
                CurrentGesture = s.CurrentGesture
            });
        }
        ApplyShortcutsFilter();
    }

    partial void OnShortcutSearchFilterChanged(string value)
    {
        ApplyShortcutsFilter();
    }

    partial void OnSelectedShortcutCategoryChanged(string value)
    {
        ApplyShortcutsFilter();
    }

    public void ApplyShortcutsFilter()
    {
        FilteredShortcuts.Clear();
        string query = ShortcutSearchFilter?.Trim().ToLowerInvariant() ?? string.Empty;
        string cat = SelectedShortcutCategory ?? "All";

        foreach (var s in Shortcuts)
        {
            bool matchCategory = cat == "All" || string.Equals(s.Category, cat, StringComparison.OrdinalIgnoreCase);
            if (!matchCategory) continue;

            if (string.IsNullOrEmpty(query))
            {
                FilteredShortcuts.Add(s);
            }
            else
            {
                bool matchText = s.Name.ToLowerInvariant().Contains(query) ||
                                 s.Description.ToLowerInvariant().Contains(query) ||
                                 s.CurrentGesture.ToLowerInvariant().Contains(query) ||
                                 s.Category.ToLowerInvariant().Contains(query);
                if (matchText)
                {
                    FilteredShortcuts.Add(s);
                }
            }
        }
    }

    [RelayCommand]
    public void FilterShortcutsByCategory(string category)
    {
        SelectedShortcutCategory = string.IsNullOrWhiteSpace(category) ? "All" : category;
    }

    #region Workspace & Panels Customization Commands

    [RelayCommand]
    public void SelectEditorBackground(string hex)
    {
        EditorBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectEditorForeground(string hex)
    {
        EditorForeground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectSidebarBackground(string hex)
    {
        SidebarBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectSidebarHeaderBackground(string hex)
    {
        SidebarHeaderBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectSidebarItemHoverBackground(string hex)
    {
        SidebarItemHoverBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectSidebarTextColor(string hex)
    {
        SidebarTextColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectSidebarBorderColor(string hex)
    {
        SidebarBorderColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectButtonAccentColor(string hex)
    {
        ButtonAccentColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectButtonBackgroundColor(string hex)
    {
        ButtonBackgroundColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectButtonTextColor(string hex)
    {
        ButtonTextColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectButtonBorderColor(string hex)
    {
        ButtonBorderColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectBorderColor(string hex)
    {
        BorderColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void SelectSplitterColor(string hex)
    {
        SplitterColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

    #region Color Maker Methods & Commands

    partial void OnColorMakerRChanged(double value) => UpdateHexFromRgb();
    partial void OnColorMakerGChanged(double value) => UpdateHexFromRgb();
    partial void OnColorMakerBChanged(double value) => UpdateHexFromRgb();

    private bool _isSyncingColorMaker;

    private void UpdateHexFromRgb()
    {
        if (_isSyncingColorMaker) return;
        _isSyncingColorMaker = true;
        try
        {
            byte r = (byte)Math.Clamp(ColorMakerR, 0, 255);
            byte g = (byte)Math.Clamp(ColorMakerG, 0, 255);
            byte b = (byte)Math.Clamp(ColorMakerB, 0, 255);
            ColorMakerHex = $"#{r:X2}{g:X2}{b:X2}";
        }
        finally
        {
            _isSyncingColorMaker = false;
        }
    }

    [RelayCommand]
    public void SetColorMakerHex(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex)) return;
        string clean = hex.Trim();
        if (!clean.StartsWith("#")) clean = "#" + clean;

        if (Color.TryParse(clean, out var color))
        {
            _isSyncingColorMaker = true;
            try
            {
                ColorMakerHex = clean.ToUpperInvariant();
                ColorMakerR = color.R;
                ColorMakerG = color.G;
                ColorMakerB = color.B;
            }
            finally
            {
                _isSyncingColorMaker = false;
            }
        }
    }

    [RelayCommand]
    public void LoadIntoColorMaker(string? hex)
    {
        if (string.IsNullOrWhiteSpace(hex) || hex.Equals("Default", StringComparison.OrdinalIgnoreCase))
            hex = "#3B82F6";
        SetColorMakerHex(hex);
    }

    [RelayCommand]
    public void ApplyColorMakerToTarget()
    {
        string hex = ColorMakerHex?.Trim().ToUpperInvariant() ?? "#3B82F6";
        if (!hex.StartsWith("#")) hex = "#" + hex;
        if (!Color.TryParse(hex, out _)) return;

        string target = ColorMakerSelectedTarget ?? "Buttons: Accent / Action";

        switch (target)
        {
            case "Buttons: Accent / Action":
                ButtonAccentColor = hex;
                break;
            case "Buttons: Background":
                ButtonBackgroundColor = hex;
                break;
            case "Buttons: Text":
                ButtonTextColor = hex;
                break;
            case "Buttons: Border":
                ButtonBorderColor = hex;
                break;
            case "Borders: Global Panels & Dividers":
                BorderColor = hex;
                break;
            case "Borders: Splitters & Separators":
                SplitterColor = hex;
                break;
            case "Sidebar: Main Background":
                SidebarBackground = hex;
                break;
            case "Sidebar: Header & Toolbar":
                SidebarHeaderBackground = hex;
                break;
            case "Sidebar: Item Hover / Selection":
                SidebarItemHoverBackground = hex;
                break;
            case "Sidebar: Text":
                SidebarTextColor = hex;
                break;
            case "Sidebar: Border":
                SidebarBorderColor = hex;
                break;
            case "Text Editor: Background":
                EditorBackground = hex;
                break;
            case "Text Editor: Text":
                EditorForeground = hex;
                break;
            case "Table: Background":
                TableBackground = hex;
                LivePreviewTableCustomization();
                break;
            case "Table: Alternating Row":
                TableAlternatingRowColor = hex;
                LivePreviewTableCustomization();
                break;
            case "Table: Header Background":
                TableHeaderBackground = hex;
                LivePreviewTableCustomization();
                break;
            case "Table: Grid Lines":
                TableGridLinesColor = hex;
                LivePreviewTableCustomization();
                break;
            case "Table: Cell Text":
                TableCellTextColor = hex;
                LivePreviewTableCustomization();
                break;
        }

        LivePreviewPanelCustomization();
    }

    #endregion

    [RelayCommand]
    public void IncreaseFontSize()
    {
        EditorFontSize = Math.Min(24.0, EditorFontSize + 1.0);
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void DecreaseFontSize()
    {
        EditorFontSize = Math.Max(9.0, EditorFontSize - 1.0);
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void ToggleLineNumbers()
    {
        EditorShowLineNumbers = !EditorShowLineNumbers;
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void ApplyPanelBundle(string bundleName)
    {
        switch (bundleName?.ToLowerInvariant())
        {
            case "slate":
                EditorBackground = "#1E293B";
                EditorForeground = "#F8FAFC";
                SidebarBackground = "#141E2E";
                SidebarHeaderBackground = "#1E293B";
                SidebarItemHoverBackground = "#334155";
                SidebarTextColor = "#F8FAFC";
                SidebarBorderColor = "#334155";
                ButtonAccentColor = "#38BDF8";
                ButtonBackgroundColor = "#1E293B";
                ButtonTextColor = "#F8FAFC";
                ButtonBorderColor = "#334155";
                BorderColor = "#334155";
                SplitterColor = "#38BDF8";
                break;
            case "midnight":
                EditorBackground = "#0F1117";
                EditorForeground = "#E2E8F0";
                SidebarBackground = "#090D16";
                SidebarHeaderBackground = "#0F1117";
                SidebarItemHoverBackground = "#1E293B";
                SidebarTextColor = "#E2E8F0";
                SidebarBorderColor = "#1F2937";
                ButtonAccentColor = "#818CF8";
                ButtonBackgroundColor = "#1E293B";
                ButtonTextColor = "#F8FAFC";
                ButtonBorderColor = "#374151";
                BorderColor = "#1F2937";
                SplitterColor = "#818CF8";
                break;
            case "monokai":
                EditorBackground = "#272822";
                EditorForeground = "#F8F8F2";
                SidebarBackground = "#1E1F1C";
                SidebarHeaderBackground = "#272822";
                SidebarItemHoverBackground = "#3E3D32";
                SidebarTextColor = "#F8F8F2";
                SidebarBorderColor = "#3E3D32";
                ButtonAccentColor = "#A6E22E";
                ButtonBackgroundColor = "#272822";
                ButtonTextColor = "#F8F8F2";
                ButtonBorderColor = "#49483E";
                BorderColor = "#3E3D32";
                SplitterColor = "#A6E22E";
                break;
            case "solarized":
                EditorBackground = "#002B36";
                EditorForeground = "#839496";
                SidebarBackground = "#073642";
                SidebarHeaderBackground = "#002B36";
                SidebarItemHoverBackground = "#073642";
                SidebarTextColor = "#839496";
                SidebarBorderColor = "#073642";
                ButtonAccentColor = "#268BD2";
                ButtonBackgroundColor = "#073642";
                ButtonTextColor = "#EEE8D5";
                ButtonBorderColor = "#586E75";
                BorderColor = "#073642";
                SplitterColor = "#268BD2";
                break;
            case "emerald":
                EditorBackground = "#064E3B";
                EditorForeground = "#ECFDF5";
                SidebarBackground = "#022C22";
                SidebarHeaderBackground = "#064E3B";
                SidebarItemHoverBackground = "#065F46";
                SidebarTextColor = "#D1FAE5";
                SidebarBorderColor = "#065F46";
                ButtonAccentColor = "#10B981";
                ButtonBackgroundColor = "#064E3B";
                ButtonTextColor = "#ECFDF5";
                ButtonBorderColor = "#047857";
                BorderColor = "#065F46";
                SplitterColor = "#10B981";
                break;
            case "violet":
                EditorBackground = "#1A102F";
                EditorForeground = "#FAF5FF";
                SidebarBackground = "#0F0C29";
                SidebarHeaderBackground = "#1E1B4B";
                SidebarItemHoverBackground = "#312E81";
                SidebarTextColor = "#F3E8FF";
                SidebarBorderColor = "#312E81";
                ButtonAccentColor = "#A855F7";
                ButtonBackgroundColor = "#2E1065";
                ButtonTextColor = "#FAF5FF";
                ButtonBorderColor = "#4C1D95";
                BorderColor = "#312E81";
                SplitterColor = "#A855F7";
                break;
            case "paper":
                EditorBackground = "#FFFFFF";
                EditorForeground = "#0F172A";
                SidebarBackground = "#F8FAFC";
                SidebarHeaderBackground = "#F1F5F9";
                SidebarItemHoverBackground = "#E2E8F0";
                SidebarTextColor = "#0F172A";
                SidebarBorderColor = "#E2E8F0";
                ButtonAccentColor = "#2563EB";
                ButtonBackgroundColor = "#F8FAFC";
                ButtonTextColor = "#0F172A";
                ButtonBorderColor = "#CBD5E1";
                BorderColor = "#E2E8F0";
                SplitterColor = "#2563EB";
                break;
            default:
                ResetPanelCustomization();
                return;
        }
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void ResetPanelCustomization()
    {
        EditorBackground = "Default";
        EditorForeground = "Default";
        SidebarBackground = "Default";
        SidebarHeaderBackground = "Default";
        SidebarItemHoverBackground = "Default";
        SidebarTextColor = "Default";
        SidebarBorderColor = "Default";
        ButtonAccentColor = "Default";
        ButtonBackgroundColor = "Default";
        ButtonTextColor = "Default";
        ButtonBorderColor = "Default";
        BorderColor = "Default";
        SplitterColor = "Default";
        EditorFontSize = 13.0;
        EditorShowLineNumbers = true;
        LivePreviewPanelCustomization();
    }

    private void LivePreviewPanelCustomization()
    {
        var record = new PanelCustomizationRecord
        {
            EditorBackground = EditorBackground,
            EditorForeground = EditorForeground,
            SidebarBackground = SidebarBackground,
            SidebarHeaderBackground = SidebarHeaderBackground,
            SidebarItemHoverBackground = SidebarItemHoverBackground,
            SidebarTextColor = SidebarTextColor,
            SidebarBorderColor = SidebarBorderColor,
            ButtonAccentColor = ButtonAccentColor,
            ButtonBackgroundColor = ButtonBackgroundColor,
            ButtonTextColor = ButtonTextColor,
            ButtonBorderColor = ButtonBorderColor,
            BorderColor = BorderColor,
            SplitterColor = SplitterColor,
            TableBackground = TableBackground,
            EditorFontSize = EditorFontSize,
            EditorShowLineNumbers = EditorShowLineNumbers
        };
        ThemeService.Instance.ApplyPanelCustomization(record);

        OnPropertyChanged(nameof(PreviewEditorBackgroundBrush));
        OnPropertyChanged(nameof(PreviewEditorForegroundBrush));
        OnPropertyChanged(nameof(PreviewSidebarBackgroundBrush));
        OnPropertyChanged(nameof(PreviewSidebarHeaderBrush));
        OnPropertyChanged(nameof(PreviewSidebarHoverBrush));
        OnPropertyChanged(nameof(PreviewSidebarTextBrush));
        OnPropertyChanged(nameof(PreviewSidebarBorderBrush));
        OnPropertyChanged(nameof(PreviewButtonAccentBrush));
        OnPropertyChanged(nameof(PreviewButtonBackgroundBrush));
        OnPropertyChanged(nameof(PreviewButtonTextBrush));
        OnPropertyChanged(nameof(PreviewButtonBorderBrush));
        OnPropertyChanged(nameof(PreviewBorderBrush));
        OnPropertyChanged(nameof(PreviewSplitterBrush));
        OnPropertyChanged(nameof(PreviewTableBackgroundBrush));
        OnPropertyChanged(nameof(PreviewTableMainRowBrush));
    }

    #endregion

    #region Query Results Table Customization Commands

    [RelayCommand]
    public void SetTableRowHeight(double height)
    {
        TableRowHeight = Math.Clamp(height, 18.0, 50.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void IncreaseTableRowHeight()
    {
        TableRowHeight = Math.Min(50.0, TableRowHeight + 2.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void DecreaseTableRowHeight()
    {
        TableRowHeight = Math.Max(18.0, TableRowHeight - 2.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SetTableHeaderHeight(double height)
    {
        TableHeaderHeight = Math.Clamp(height, 18.0, 45.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SetTableGutterWidth(double width)
    {
        TableGutterWidth = Math.Clamp(width, 36.0, 90.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SetTableCellFontSize(double size)
    {
        TableCellFontSize = Math.Clamp(size, 9.0, 18.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void IncreaseTableCellFontSize()
    {
        TableCellFontSize = Math.Min(18.0, TableCellFontSize + 1.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void DecreaseTableCellFontSize()
    {
        TableCellFontSize = Math.Max(9.0, TableCellFontSize - 1.0);
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableBackground(string hex)
    {
        TableBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableAlternatingRowColor(string hex)
    {
        TableAlternatingRowColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableCellTextColor(string hex)
    {
        TableCellTextColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableHeaderBackground(string hex)
    {
        TableHeaderBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableHeaderTextColor(string hex)
    {
        TableHeaderTextColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableGutterBackground(string hex)
    {
        TableGutterBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableGridLinesColor(string hex)
    {
        TableGridLinesColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void SelectTableSelectionColor(string hex)
    {
        TableSelectionColor = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void ToggleTableGridLines()
    {
        TableShowGridLines = !TableShowGridLines;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void ToggleTableAlternatingRows()
    {
        TableShowAlternatingRowColors = !TableShowAlternatingRowColors;
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void ApplyTablePreset(string preset)
    {
        switch (preset?.ToLowerInvariant())
        {
            case "excel":
                TableRowHeight = 25.0;
                TableHeaderHeight = 26.0;
                TableGutterWidth = 56.0;
                TableCellFontSize = 11.0;
                TableBackground = "#FFFFFF";
                TableAlternatingRowColor = "#F8FAFC";
                TableCellTextColor = "#0F172A";
                TableHeaderBackground = "#F1F5F9";
                TableHeaderTextColor = "#0F172A";
                TableGutterBackground = "#F1F5F9";
                TableGutterTextColor = "#0F172A";
                TableGridLinesColor = "#CBD5E1";
                TableSelectionColor = "#DBEAFE";
                TableShowGridLines = true;
                TableShowAlternatingRowColors = true;
                break;
            case "darkpro":
                TableRowHeight = 26.0;
                TableHeaderHeight = 28.0;
                TableGutterWidth = 56.0;
                TableCellFontSize = 11.0;
                TableBackground = "#1E293B";
                TableAlternatingRowColor = "#141E2E";
                TableCellTextColor = "#F8FAFC";
                TableHeaderBackground = "#1E293B";
                TableHeaderTextColor = "#F8FAFC";
                TableGutterBackground = "#1E293B";
                TableGutterTextColor = "#94A3B8";
                TableGridLinesColor = "#334155";
                TableSelectionColor = "#1E3A5F";
                TableShowGridLines = true;
                TableShowAlternatingRowColors = true;
                break;
            case "compact":
                TableRowHeight = 22.0;
                TableHeaderHeight = 24.0;
                TableGutterWidth = 44.0;
                TableCellFontSize = 10.0;
                TableShowGridLines = true;
                TableShowAlternatingRowColors = true;
                break;
            case "comfortable":
                TableRowHeight = 32.0;
                TableHeaderHeight = 32.0;
                TableGutterWidth = 60.0;
                TableCellFontSize = 12.0;
                TableShowGridLines = true;
                TableShowAlternatingRowColors = true;
                break;
            case "highcontrast":
                TableRowHeight = 26.0;
                TableHeaderHeight = 28.0;
                TableCellFontSize = 11.0;
                TableBackground = "#000000";
                TableAlternatingRowColor = "#111827";
                TableCellTextColor = "#FFFFFF";
                TableHeaderBackground = "#000000";
                TableHeaderTextColor = "#FFFFFF";
                TableGutterBackground = "#000000";
                TableGutterTextColor = "#FFFFFF";
                TableGridLinesColor = "#FFFFFF";
                TableSelectionColor = "#2563EB";
                TableShowGridLines = true;
                TableShowAlternatingRowColors = true;
                break;
            default:
                ResetTableCustomization();
                return;
        }
        LivePreviewTableCustomization();
    }

    [RelayCommand]
    public void ResetTableCustomization()
    {
        TableRowHeight = 25.0;
        TableHeaderHeight = 26.0;
        TableGutterWidth = 56.0;
        TableCellFontSize = 11.0;
        TableBackground = "Default";
        TableAlternatingRowColor = "Default";
        TableCellTextColor = "Default";
        TableHeaderBackground = "Default";
        TableHeaderTextColor = "Default";
        TableGutterBackground = "Default";
        TableGutterTextColor = "Default";
        TableGridLinesColor = "Default";
        TableSelectionColor = "Default";
        TableShowGridLines = true;
        TableShowAlternatingRowColors = true;
        LivePreviewTableCustomization();
    }

    private void LivePreviewTableCustomization()
    {
        var record = new TableCustomizationRecord
        {
            RowHeight = TableRowHeight,
            HeaderHeight = TableHeaderHeight,
            GutterWidth = TableGutterWidth,
            FontSize = TableCellFontSize,
            TableBackground = TableBackground,
            AlternatingRowColor = TableAlternatingRowColor,
            CellTextColor = TableCellTextColor,
            HeaderBackground = TableHeaderBackground,
            HeaderTextColor = TableHeaderTextColor,
            GutterBackground = TableGutterBackground,
            GutterTextColor = TableGutterTextColor,
            GridLinesColor = TableGridLinesColor,
            SelectionColor = TableSelectionColor,
            ShowGridLines = TableShowGridLines,
            ShowAlternatingRowColors = TableShowAlternatingRowColors
        };
        ThemeService.Instance.ApplyTableCustomization(record);

        OnPropertyChanged(nameof(PreviewTableMainRowBrush));
        OnPropertyChanged(nameof(PreviewTableAltRowBrush));
        OnPropertyChanged(nameof(PreviewTableCellTextBrush));
        OnPropertyChanged(nameof(PreviewTableHeaderBgBrush));
        OnPropertyChanged(nameof(PreviewTableHeaderTextBrush));
        OnPropertyChanged(nameof(PreviewTableGutterBgBrush));
        OnPropertyChanged(nameof(PreviewTableGutterTextBrush));
        OnPropertyChanged(nameof(PreviewTableGridLinesBrush));
        OnPropertyChanged(nameof(PreviewTableSelectionBrush));
    }

    #endregion

    #region Shortcut Customization Commands

    [RelayCommand]
    public void StartEditShortcut(ShortcutItem item)
    {
        if (item == null) return;
        foreach (var s in Shortcuts)
        {
            if (s != item && s.IsEditing) s.IsEditing = false;
        }
        item.EditingGesture = item.CurrentGesture;
        item.IsEditing = true;
    }

    [RelayCommand]
    public void ApplyEditShortcut(ShortcutItem item)
    {
        if (item == null) return;
        if (!string.IsNullOrWhiteSpace(item.EditingGesture))
        {
            item.CurrentGesture = item.EditingGesture.Trim();
        }
        item.IsEditing = false;
        OnPropertyChanged(nameof(Shortcuts));
        ApplyShortcutsFilter();
    }

    [RelayCommand]
    public void CancelEditShortcut(ShortcutItem item)
    {
        if (item == null) return;
        item.IsEditing = false;
    }

    [RelayCommand]
    public void ResetShortcut(ShortcutItem item)
    {
        if (item == null) return;
        item.CurrentGesture = item.DefaultGesture;
        item.IsEditing = false;
        ApplyShortcutsFilter();
    }

    [RelayCommand]
    public void ResetAllShortcuts()
    {
        foreach (var s in Shortcuts)
        {
            s.CurrentGesture = s.DefaultGesture;
            s.IsEditing = false;
        }
        ApplyShortcutsFilter();
    }

    #endregion

    #region Performance & Graphics Properties & Commands

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsAutoRenderingMode))]
    [NotifyPropertyChangedFor(nameof(IsGpuRenderingMode))]
    [NotifyPropertyChangedFor(nameof(IsCpuRenderingMode))]
    [NotifyPropertyChangedFor(nameof(EffectiveModeDescription))]
    [NotifyPropertyChangedFor(nameof(RequiresGraphicsRestart))]
    private string _selectedRenderingMode = "Auto"; // "Auto", "GPU", "CPU"

    public bool IsAutoRenderingMode => string.Equals(SelectedRenderingMode, "Auto", StringComparison.OrdinalIgnoreCase);
    public bool IsGpuRenderingMode => string.Equals(SelectedRenderingMode, "GPU", StringComparison.OrdinalIgnoreCase);
    public bool IsCpuRenderingMode => string.Equals(SelectedRenderingMode, "CPU", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private string _gpuAdapterName = "Detecting Graphics Adapter...";

    [ObservableProperty]
    private bool _isGpuDetected;

    [ObservableProperty]
    private string _activeRendererSummary = "Skia Hardware (DirectX 11 / ANGLE)";

    [ObservableProperty]
    private int _vramCacheMb = 128;

    private string _initialRenderingMode = "Auto";

    public bool RequiresGraphicsRestart => !string.Equals(SelectedRenderingMode, _initialRenderingMode, StringComparison.OrdinalIgnoreCase);

    public string EffectiveModeDescription
    {
        get
        {
            string eff = HardwareGraphicsService.Instance.ResolveEffectiveMode(SelectedRenderingMode);
            if (string.Equals(SelectedRenderingMode, "Auto", StringComparison.OrdinalIgnoreCase))
            {
                return IsGpuDetected
                    ? $"Auto-detect: Dedicated/Integrated GPU detected -> Active Engine: GPU Hardware Acceleration ({GpuAdapterName})"
                    : "Auto-detect: No discrete GPU detected -> Active Engine: CPU Software Rasterizer (Lightweight)";
            }
            if (string.Equals(SelectedRenderingMode, "GPU", StringComparison.OrdinalIgnoreCase))
            {
                return $"Forced GPU Mode: Hardware-accelerated DirectX 11 / ANGLE pipeline with {VramCacheMb}MB VRAM cache";
            }
            return $"Forced CPU Mode: Minimal RAM footprint with CPU Software Rasterizer ({VramCacheMb}MB buffer)";
        }
    }

    [RelayCommand]
    public void SelectRenderingMode(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        SelectedRenderingMode = mode;
        OnPropertyChanged(nameof(EffectiveModeDescription));
        OnPropertyChanged(nameof(RequiresGraphicsRestart));
    }

    #endregion

    #region Appearance Commands

    [RelayCommand]
    public void SelectTheme(string theme)
    {
        if (string.IsNullOrWhiteSpace(theme)) return;
        SelectedTheme = theme;
        ThemeService.Instance.ApplyTheme(theme);
        OnPropertyChanged(nameof(CurrentKeywordBrush));
        OnPropertyChanged(nameof(PreviewEditorBackgroundBrush));
        OnPropertyChanged(nameof(PreviewEditorForegroundBrush));
        OnPropertyChanged(nameof(PreviewSidebarBackgroundBrush));
        OnPropertyChanged(nameof(PreviewSidebarHeaderBrush));
        OnPropertyChanged(nameof(PreviewSidebarHoverBrush));
        OnPropertyChanged(nameof(PreviewSidebarTextBrush));
        OnPropertyChanged(nameof(PreviewSidebarBorderBrush));
        OnPropertyChanged(nameof(PreviewButtonAccentBrush));
        OnPropertyChanged(nameof(PreviewButtonBackgroundBrush));
        OnPropertyChanged(nameof(PreviewButtonTextBrush));
        OnPropertyChanged(nameof(PreviewButtonBorderBrush));
        OnPropertyChanged(nameof(PreviewBorderBrush));
        OnPropertyChanged(nameof(PreviewSplitterBrush));
        OnPropertyChanged(nameof(PreviewTableBackgroundBrush));
        OnPropertyChanged(nameof(PreviewTableMainRowBrush));
        OnPropertyChanged(nameof(PreviewTableAltRowBrush));
        OnPropertyChanged(nameof(PreviewTableCellTextBrush));
        OnPropertyChanged(nameof(PreviewTableHeaderBgBrush));
        OnPropertyChanged(nameof(PreviewTableHeaderTextBrush));
        OnPropertyChanged(nameof(PreviewTableGutterBgBrush));
        OnPropertyChanged(nameof(PreviewTableGutterTextBrush));
        OnPropertyChanged(nameof(PreviewTableGridLinesBrush));
        OnPropertyChanged(nameof(PreviewTableSelectionBrush));
    }

    [RelayCommand]
    public void SelectKeywordColor(string colorKey)
    {
        if (string.IsNullOrWhiteSpace(colorKey)) return;
        SelectedKeywordColor = colorKey;
        ThemeService.Instance.SetKeywordColor(colorKey);
        OnPropertyChanged(nameof(CurrentKeywordBrush));
    }

    #endregion

    [RelayCommand]
    public async Task SaveAsync()
    {
        try
        {
            // 1. Save Theme & Keyword Colors
            await DatabaseStorageService.Instance.SaveThemeAsync(SelectedTheme).ConfigureAwait(false);
            await DatabaseStorageService.Instance.SaveKeywordColorAsync(SelectedKeywordColor).ConfigureAwait(false);
            _initialTheme = SelectedTheme;
            _initialKeywordColor = SelectedKeywordColor;

            // 2. Save Panel Customization
            var panelRecord = new PanelCustomizationRecord
            {
                EditorBackground = EditorBackground,
                EditorForeground = EditorForeground,
                SidebarBackground = SidebarBackground,
                SidebarHeaderBackground = SidebarHeaderBackground,
                SidebarItemHoverBackground = SidebarItemHoverBackground,
                SidebarTextColor = SidebarTextColor,
                SidebarBorderColor = SidebarBorderColor,
                ButtonAccentColor = ButtonAccentColor,
                ButtonBackgroundColor = ButtonBackgroundColor,
                ButtonTextColor = ButtonTextColor,
                ButtonBorderColor = ButtonBorderColor,
                BorderColor = BorderColor,
                SplitterColor = SplitterColor,
                TableBackground = TableBackground,
                EditorFontSize = EditorFontSize,
                EditorShowLineNumbers = EditorShowLineNumbers
            };
            await DatabaseStorageService.Instance.SavePanelCustomizationAsync(panelRecord).ConfigureAwait(false);
            ThemeService.Instance.ApplyPanelCustomization(panelRecord);
            _initialPanelCustomization = panelRecord;

            // 3. Save Table Customization
            var tableRecord = new TableCustomizationRecord
            {
                RowHeight = TableRowHeight,
                HeaderHeight = TableHeaderHeight,
                GutterWidth = TableGutterWidth,
                FontSize = TableCellFontSize,
                TableBackground = TableBackground,
                AlternatingRowColor = TableAlternatingRowColor,
                CellTextColor = TableCellTextColor,
                HeaderBackground = TableHeaderBackground,
                HeaderTextColor = TableHeaderTextColor,
                GutterBackground = TableGutterBackground,
                GutterTextColor = TableGutterTextColor,
                GridLinesColor = TableGridLinesColor,
                SelectionColor = TableSelectionColor,
                ShowGridLines = TableShowGridLines,
                ShowAlternatingRowColors = TableShowAlternatingRowColors
            };
            await DatabaseStorageService.Instance.SaveTableCustomizationAsync(tableRecord).ConfigureAwait(false);
            ThemeService.Instance.ApplyTableCustomization(tableRecord);
            _initialTableCustomization = tableRecord;

            // 4. Save Shortcuts to SQLite
            await ShortcutService.Instance.SaveAllAsync(Shortcuts).ConfigureAwait(false);

            // 5. Save Graphics Rendering Mode to SQLite
            await DatabaseStorageService.Instance.SaveGraphicsRenderingModeAsync(SelectedRenderingMode).ConfigureAwait(false);
            HardwareGraphicsService.Instance.RenderingMode = SelectedRenderingMode;
            _initialRenderingMode = SelectedRenderingMode;
            OnPropertyChanged(nameof(RequiresGraphicsRestart));
            // 5. Save Terminal Settings to SQLite
            var termSettingsToSave = new Fdb.Models.TerminalSettingsModel
            {
                DefaultShellId = TerminalDefaultShell,
                FontFamily = TerminalFontFamily,
                FontSize = TerminalFontSize,
                CursorStyle = TerminalCursorStyle,
                CursorBlink = TerminalCursorBlink,
                MaxScrollback = TerminalMaxScrollback,
                PsqlPath = TerminalPsqlPath,
                MySqlPath = TerminalMySqlPath,
                SqlitePath = TerminalSqlitePath,
                SqlCmdPath = TerminalSqlCmdPath
            };
            await Fdb.Services.Terminal.TerminalSettingsService.Instance.SaveSettingsAsync(termSettingsToSave).ConfigureAwait(false);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsModalViewModel] Failed to persist settings: {ex.Message}");
        }

        IsOpen = false;
    }

    [RelayCommand]
    public void Close()
    {
        // Revert Theme if changed
        if (!string.Equals(SelectedTheme, _initialTheme, StringComparison.OrdinalIgnoreCase))
        {
            SelectedTheme = _initialTheme;
            ThemeService.Instance.ApplyTheme(_initialTheme);
        }

        // Revert Keyword Color if changed
        if (!string.Equals(SelectedKeywordColor, _initialKeywordColor, StringComparison.OrdinalIgnoreCase))
        {
            SelectedKeywordColor = _initialKeywordColor;
            ThemeService.Instance.SetKeywordColor(_initialKeywordColor);
        }

        // Revert Panel Customization
        ThemeService.Instance.ApplyPanelCustomization(_initialPanelCustomization);

        // Revert Table Customization
        ThemeService.Instance.ApplyTableCustomization(_initialTableCustomization);

        // Revert Rendering Mode
        if (!string.Equals(SelectedRenderingMode, _initialRenderingMode, StringComparison.OrdinalIgnoreCase))
        {
            SelectedRenderingMode = _initialRenderingMode;
        }

        IsOpen = false;
    }
}
