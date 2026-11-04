using System;
using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Media;
using Avalonia.Styling;
using Avalonia.Threading;

namespace Fdb.Services;

public class KeywordColorPreset
{
    public string Key { get; set; } = "Blue";
    public string Name { get; set; } = "Royal Blue";
    public string LightHex { get; set; } = "#2563EB";
    public string DarkHex { get; set; } = "#60A5FA";
    public string Description { get; set; } = "Classic Studio Blue (Default)";
}

public class ThemeService
{
    private static ThemeService? _instance;
    public static ThemeService Instance => _instance ??= new ThemeService();

    public static readonly IReadOnlyList<KeywordColorPreset> Presets = new List<KeywordColorPreset>
    {
        new() { Key = "Blue", Name = "Royal Blue", LightHex = "#2563EB", DarkHex = "#60A5FA", Description = "Classic Studio Blue (Default)" },
        new() { Key = "Green", Name = "Emerald Green", LightHex = "#059669", DarkHex = "#34D399", Description = "High-contrast emerald & vibrant mint" },
        new() { Key = "Purple", Name = "Violet Purple", LightHex = "#7C3AED", DarkHex = "#A78BFA", Description = "Modern deep violet & soft lavender" },
        new() { Key = "Amber", Name = "Amber Gold", LightHex = "#D97706", DarkHex = "#FBBF24", Description = "Warm golden amber & honey yellow" },
        new() { Key = "Rose", Name = "Crimson Rose", LightHex = "#E11D48", DarkHex = "#FB7185", Description = "Rich crimson & radiant rose" },
        new() { Key = "Cyan", Name = "Teal Cyan", LightHex = "#0891B2", DarkHex = "#38BDF8", Description = "Oceanic teal & electric cyan" }
    };

    public event EventHandler<string>? ThemeChanged;
    public event EventHandler<string>? KeywordColorChanged;
    public event EventHandler<PanelCustomizationRecord>? PanelCustomizationChanged;
    public event EventHandler<TableCustomizationRecord>? TableCustomizationChanged;

    private string _currentTheme = "Light";
    public string CurrentTheme => _currentTheme;

    private string _currentKeywordColor = "Blue";
    public string CurrentKeywordColor => _currentKeywordColor;

    public PanelCustomizationRecord CurrentPanelCustomization { get; private set; } = new();
    public TableCustomizationRecord CurrentTableCustomization { get; private set; } = new();

    public IBrush CurrentKeywordBrush { get; private set; } = new SolidColorBrush(Color.Parse("#2563EB"));

    public KeywordColorPreset GetPreset(string? key)
    {
        if (string.IsNullOrWhiteSpace(key)) key = "Blue";
        return Presets.FirstOrDefault(p => p.Key.Equals(key, StringComparison.OrdinalIgnoreCase)) ?? Presets[0];
    }

    public void ApplyTheme(string themeMode)
    {
        if (string.IsNullOrWhiteSpace(themeMode)) themeMode = "Light";
        _currentTheme = themeMode;

        void Update()
        {
            if (Application.Current != null)
            {
                Application.Current.RequestedThemeVariant = themeMode.ToLowerInvariant() switch
                {
                    "dark" => ThemeVariant.Dark,
                    "system" or "default" => ThemeVariant.Default,
                    _ => ThemeVariant.Light
                };
            }

            UpdateKeywordBrushInternal();
            ApplyPanelCustomizationInternal();
            ApplyTableCustomizationInternal();
            ThemeChanged?.Invoke(this, _currentTheme);
            KeywordColorChanged?.Invoke(this, _currentKeywordColor);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Dispatcher.UIThread.Post(Update);
        }
    }

    public void SetKeywordColor(string colorKey)
    {
        if (string.IsNullOrWhiteSpace(colorKey)) colorKey = "Blue";
        _currentKeywordColor = colorKey;

        void Update()
        {
            UpdateKeywordBrushInternal();
            KeywordColorChanged?.Invoke(this, _currentKeywordColor);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Dispatcher.UIThread.Post(Update);
        }
    }

    private void UpdateKeywordBrushInternal()
    {
        var preset = GetPreset(_currentKeywordColor);
        bool isDark = IsDarkTheme();
        string hex = isDark ? preset.DarkHex : preset.LightHex;

        if (Color.TryParse(hex, out var color))
        {
            var brush = new SolidColorBrush(color);
            CurrentKeywordBrush = brush;

            if (Application.Current != null)
            {
                Application.Current.Resources["AppKeywordBrush"] = brush;
            }
        }
    }

    public bool IsDarkTheme()
    {
        if (_currentTheme.Equals("Dark", StringComparison.OrdinalIgnoreCase)) return true;
        if (_currentTheme.Equals("Light", StringComparison.OrdinalIgnoreCase)) return false;

        // System theme check
        if (Application.Current != null)
        {
            return Application.Current.ActualThemeVariant == ThemeVariant.Dark;
        }
        return false;
    }

    public void ApplyPanelCustomization(PanelCustomizationRecord custom)
    {
        CurrentPanelCustomization = custom ?? new PanelCustomizationRecord();

        void Update()
        {
            ApplyPanelCustomizationInternal();
            PanelCustomizationChanged?.Invoke(this, CurrentPanelCustomization);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Dispatcher.UIThread.Post(Update);
        }
    }

    private static void SetOrRemoveBrushOverride(string key, string? customHex)
    {
        if (Application.Current == null) return;

        if (!string.IsNullOrWhiteSpace(customHex) && !customHex.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(customHex, out var color))
            {
                Application.Current.Resources[key] = new SolidColorBrush(color);
                return;
            }
        }

        // When "Default", removing the key ensures Avalonia's ThemeDictionaries (LightTheme/DarkTheme)
        // dynamically resolve the resource on theme changes without static override pollution.
        Application.Current.Resources.Remove(key);
    }

    private void ApplyPanelCustomizationInternal()
    {
        if (Application.Current == null) return;

        var custom = CurrentPanelCustomization;

        // 1. Text Editor Background & Foreground
        SetOrRemoveBrushOverride("AppEditorBackgroundBrush", custom.EditorBackground);
        SetOrRemoveBrushOverride("AppEditorForegroundBrush", custom.EditorForeground);

        // 2. Sidebar Multi-part Customization
        SetOrRemoveBrushOverride("AppSidebarBackgroundBrush", custom.SidebarBackground);
        SetOrRemoveBrushOverride("AppSidebarHeaderBackgroundBrush", custom.SidebarHeaderBackground);
        SetOrRemoveBrushOverride("AppSidebarHoverBrush", custom.SidebarItemHoverBackground);
        SetOrRemoveBrushOverride("AppSidebarTextBrush", custom.SidebarTextColor);
        SetOrRemoveBrushOverride("AppSidebarBorderBrush", custom.SidebarBorderColor);

        // 3. Buttons & Action Customization
        SetOrRemoveBrushOverride("AppAccentBrush", custom.ButtonAccentColor);
        if (!string.IsNullOrWhiteSpace(custom.ButtonAccentColor) && !custom.ButtonAccentColor.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(custom.ButtonAccentColor, out var accentCol))
            {
                byte r = (byte)Math.Clamp(accentCol.R + 25, 0, 255);
                byte g = (byte)Math.Clamp(accentCol.G + 25, 0, 255);
                byte b = (byte)Math.Clamp(accentCol.B + 25, 0, 255);
                Application.Current.Resources["AppAccentHoverBrush"] = new SolidColorBrush(Color.FromArgb(accentCol.A, r, g, b));
                Application.Current.Resources["AppTabActiveBorderBrush"] = new SolidColorBrush(accentCol);
                Application.Current.Resources["AppTableActiveBorderBrush"] = new SolidColorBrush(accentCol);
            }
        }
        else
        {
            Application.Current.Resources.Remove("AppAccentHoverBrush");
            Application.Current.Resources.Remove("AppTabActiveBorderBrush");
            Application.Current.Resources.Remove("AppTableActiveBorderBrush");
        }

        SetOrRemoveBrushOverride("AppControlFaceBrush", custom.ButtonBackgroundColor);
        SetOrRemoveBrushOverride("AppButtonBackgroundBrush", custom.ButtonBackgroundColor);
        SetOrRemoveBrushOverride("AppButtonForegroundBrush", custom.ButtonTextColor);
        SetOrRemoveBrushOverride("AppButtonBorderBrush", custom.ButtonBorderColor);

        // 4. Global Borders & Dividers
        SetOrRemoveBrushOverride("AppBorderBrush", custom.BorderColor);
        SetOrRemoveBrushOverride("AppBorderLightBrush", custom.BorderColor);
        SetOrRemoveBrushOverride("AppBorderDarkBrush", custom.BorderColor);

        // 5. Splitters
        SetOrRemoveBrushOverride("AppSplitterBrush", custom.SplitterColor);
        if (!string.IsNullOrWhiteSpace(custom.SplitterColor) && !custom.SplitterColor.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(custom.SplitterColor, out var splitCol))
            {
                byte r = (byte)Math.Clamp(splitCol.R + 35, 0, 255);
                byte g = (byte)Math.Clamp(splitCol.G + 35, 0, 255);
                byte b = (byte)Math.Clamp(splitCol.B + 35, 0, 255);
                Application.Current.Resources["AppSplitterHoverBrush"] = new SolidColorBrush(Color.FromArgb(splitCol.A, r, g, b));
            }
        }
        else
        {
            Application.Current.Resources.Remove("AppSplitterHoverBrush");
        }

        // 4. Table Alternating Background
        SetOrRemoveBrushOverride("AppTableAltColBackgroundBrush", custom.TableBackground);

        // 5. Terminal Multi-part Customization
        SetOrRemoveBrushOverride("AppTerminalBackgroundBrush", custom.TerminalBackground);
        SetOrRemoveBrushOverride("AppTerminalForegroundBrush", custom.TerminalForeground);
    }

    public void ApplyTableCustomization(TableCustomizationRecord custom)
    {
        CurrentTableCustomization = custom ?? new TableCustomizationRecord();

        void Update()
        {
            ApplyTableCustomizationInternal();
            TableCustomizationChanged?.Invoke(this, CurrentTableCustomization);
        }

        if (Dispatcher.UIThread.CheckAccess())
        {
            Update();
        }
        else
        {
            Dispatcher.UIThread.Post(Update);
        }
    }

    public void ResetTableCustomization()
    {
        ApplyTableCustomization(new TableCustomizationRecord());
    }

    private void ApplyTableCustomizationInternal()
    {
        if (Application.Current == null) return;

        var custom = CurrentTableCustomization;

        // 1. Dimensions
        if (custom.RowHeight >= 18.0)
        {
            Application.Current.Resources["AppTableRowHeight"] = custom.RowHeight;
        }

        if (custom.HeaderHeight >= 18.0)
        {
            Application.Current.Resources["AppTableHeaderHeight"] = custom.HeaderHeight;
        }

        if (custom.GutterWidth >= 30.0)
        {
            Application.Current.Resources["AppTableGutterWidth"] = custom.GutterWidth;
        }

        if (custom.FontSize >= 8.0)
        {
            Application.Current.Resources["AppTableCellFontSize"] = custom.FontSize;
        }

        // 2. Main Row Background & Workspace
        SetOrRemoveBrushOverride("AppTableWhiteColBackgroundBrush", custom.TableBackground);
        SetOrRemoveBrushOverride("AppWorkspaceBrush", custom.TableBackground);

        // 3. Alternating Row Color
        if (custom.ShowAlternatingRowColors)
        {
            SetOrRemoveBrushOverride("AppTableAltColBackgroundBrush", custom.AlternatingRowColor);
        }
        else
        {
            if (Application.Current.Resources.TryGetValue("AppTableWhiteColBackgroundBrush", out var mainBrush) && mainBrush is IBrush mb)
            {
                Application.Current.Resources["AppTableAltColBackgroundBrush"] = mb;
            }
            else
            {
                Application.Current.Resources.Remove("AppTableAltColBackgroundBrush");
            }
        }

        // 4. Cell Text Color
        SetOrRemoveBrushOverride("AppTableCellForegroundBrush", custom.CellTextColor);

        // 5. Header Background & Text
        SetOrRemoveBrushOverride("AppTableHeaderBackgroundBrush", custom.HeaderBackground);
        SetOrRemoveBrushOverride("AppTableHeaderForegroundBrush", custom.HeaderTextColor);

        // 6. Gutter (Row number column) Background & Text
        SetOrRemoveBrushOverride("AppTableGutterBackgroundBrush", custom.GutterBackground);
        SetOrRemoveBrushOverride("AppTableGutterForegroundBrush", custom.GutterTextColor);

        // 7. Grid Lines
        if (custom.ShowGridLines)
        {
            SetOrRemoveBrushOverride("AppTableGridLinesHorizontalBrush", custom.GridLinesColor);
            SetOrRemoveBrushOverride("AppTableGridLinesVerticalBrush", custom.GridLinesColor);
            SetOrRemoveBrushOverride("AppTableCellBorderBrush", custom.GridLinesColor);
        }
        else
        {
            Application.Current.Resources["AppTableGridLinesHorizontalBrush"] = Brushes.Transparent;
            Application.Current.Resources["AppTableGridLinesVerticalBrush"] = Brushes.Transparent;
            Application.Current.Resources["AppTableCellBorderBrush"] = Brushes.Transparent;
        }

        // 8. Selection / Active Cell Highlight
        SetOrRemoveBrushOverride("AppTableSelectedBackgroundBrush", custom.SelectionColor);
        SetOrRemoveBrushOverride("AppTableActiveBackgroundBrush", custom.SelectionColor);
    }
}