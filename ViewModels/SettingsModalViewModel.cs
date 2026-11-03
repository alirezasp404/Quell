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
    [NotifyPropertyChangedFor(nameof(IsCustomizationCategory))]
    [NotifyPropertyChangedFor(nameof(IsShortcutsCategory))]
    private string _selectedCategory = "Appearance";

    public bool IsAppearanceCategory => string.Equals(SelectedCategory, "Appearance", StringComparison.OrdinalIgnoreCase);
    public bool IsCustomizationCategory => string.Equals(SelectedCategory, "Customization", StringComparison.OrdinalIgnoreCase);
    public bool IsShortcutsCategory => string.Equals(SelectedCategory, "Shortcuts", StringComparison.OrdinalIgnoreCase);

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

    #region Panel Customization Properties

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
    [NotifyPropertyChangedFor(nameof(PreviewTableBackgroundBrush))]
    private string _tableBackground = "Default";

    [ObservableProperty]
    private double _editorFontSize = 13.0;

    [ObservableProperty]
    private bool _editorShowLineNumbers = true;

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

    public IBrush PreviewTableBackgroundBrush
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(TableBackground) && !TableBackground.Equals("Default", StringComparison.OrdinalIgnoreCase) && Color.TryParse(TableBackground, out var color))
                return new SolidColorBrush(color);
            return IsDarkTheme ? new SolidColorBrush(Color.Parse("#141E2E")) : new SolidColorBrush(Color.Parse("#F8FAFC"));
        }
    }

    #endregion

    #region Shortcuts Properties

    public ObservableCollection<ShortcutItem> Shortcuts { get; } = new();
    public ObservableCollection<ShortcutItem> FilteredShortcuts { get; } = new();

    [ObservableProperty]
    private string _shortcutSearchFilter = string.Empty;

    [ObservableProperty]
    private string _selectedShortcutCategory = "All";

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
            TableBackground = _initialPanelCustomization.TableBackground;
            EditorFontSize = _initialPanelCustomization.EditorFontSize;
            EditorShowLineNumbers = _initialPanelCustomization.EditorShowLineNumbers;

            // 3. Load Dynamic Shortcuts from SQLite
            await ShortcutService.Instance.InitializeAsync().ConfigureAwait(false);
            LoadShortcutsList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SettingsModalViewModel] OpenAsync error: {ex.Message}");
            SelectedTheme = ThemeService.Instance.CurrentTheme;
            _initialTheme = SelectedTheme;
            SelectedKeywordColor = ThemeService.Instance.CurrentKeywordColor;
            _initialKeywordColor = SelectedKeywordColor;
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

    #region Panel Customization Commands

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
    public void SelectTableBackground(string hex)
    {
        TableBackground = string.IsNullOrWhiteSpace(hex) ? "Default" : hex;
        LivePreviewPanelCustomization();
    }

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
                TableBackground = "#1E293B";
                break;
            case "midnight":
                EditorBackground = "#0F1117";
                EditorForeground = "#E2E8F0";
                SidebarBackground = "#090D16";
                TableBackground = "#0F1117";
                break;
            case "monokai":
                EditorBackground = "#272822";
                EditorForeground = "#F8F8F2";
                SidebarBackground = "#1E1F1C";
                TableBackground = "#272822";
                break;
            case "solarized":
                EditorBackground = "#002B36";
                EditorForeground = "#839496";
                SidebarBackground = "#073642";
                TableBackground = "#002B36";
                break;
            case "paper":
                EditorBackground = "#FFFFFF";
                EditorForeground = "#0F172A";
                SidebarBackground = "#F1F5F9";
                TableBackground = "#FFFFFF";
                break;
            default: // Default
                EditorBackground = "Default";
                EditorForeground = "Default";
                SidebarBackground = "Default";
                TableBackground = "Default";
                break;
        }
        LivePreviewPanelCustomization();
    }

    [RelayCommand]
    public void ResetPanelCustomization()
    {
        EditorBackground = "Default";
        EditorForeground = "Default";
        SidebarBackground = "Default";
        TableBackground = "Default";
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
            TableBackground = TableBackground,
            EditorFontSize = EditorFontSize,
            EditorShowLineNumbers = EditorShowLineNumbers
        };
        ThemeService.Instance.ApplyPanelCustomization(record);

        OnPropertyChanged(nameof(PreviewEditorBackgroundBrush));
        OnPropertyChanged(nameof(PreviewEditorForegroundBrush));
        OnPropertyChanged(nameof(PreviewSidebarBackgroundBrush));
        OnPropertyChanged(nameof(PreviewTableBackgroundBrush));
    }

    #endregion

    #region Shortcut Customization Commands

    [RelayCommand]
    public void StartEditShortcut(ShortcutItem item)
    {
        if (item == null) return;
        // Close editing for other items
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
        OnPropertyChanged(nameof(PreviewTableBackgroundBrush));
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
                TableBackground = TableBackground,
                EditorFontSize = EditorFontSize,
                EditorShowLineNumbers = EditorShowLineNumbers
            };
            await DatabaseStorageService.Instance.SavePanelCustomizationAsync(panelRecord).ConfigureAwait(false);
            ThemeService.Instance.ApplyPanelCustomization(panelRecord);
            _initialPanelCustomization = panelRecord;

            // 3. Save Shortcuts to SQLite
            await ShortcutService.Instance.SaveAllAsync(Shortcuts).ConfigureAwait(false);
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

        IsOpen = false;
    }
}
