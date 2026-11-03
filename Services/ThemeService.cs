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

    private string _currentTheme = "Light";
    public string CurrentTheme => _currentTheme;

    private string _currentKeywordColor = "Blue";
    public string CurrentKeywordColor => _currentKeywordColor;

    public PanelCustomizationRecord CurrentPanelCustomization { get; private set; } = new();

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

    private void ApplyPanelCustomizationInternal()
    {
        if (Application.Current == null) return;

        var custom = CurrentPanelCustomization;

        // 1. Editor Background
        if (!string.IsNullOrWhiteSpace(custom.EditorBackground) && !custom.EditorBackground.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(custom.EditorBackground, out var edBg))
            {
                Application.Current.Resources["AppEditorBackgroundBrush"] = new SolidColorBrush(edBg);
            }
        }
        else
        {
            // Restore default theme editor background
            string defaultHex = IsDarkTheme() ? "#0F172A" : "#FFFFFF";
            Application.Current.Resources["AppEditorBackgroundBrush"] = new SolidColorBrush(Color.Parse(defaultHex));
        }

        // 2. Editor Foreground
        if (!string.IsNullOrWhiteSpace(custom.EditorForeground) && !custom.EditorForeground.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(custom.EditorForeground, out var edFg))
            {
                Application.Current.Resources["AppEditorForegroundBrush"] = new SolidColorBrush(edFg);
            }
        }
        else
        {
            string defaultHex = IsDarkTheme() ? "#F8FAFC" : "#0F172A";
            Application.Current.Resources["AppEditorForegroundBrush"] = new SolidColorBrush(Color.Parse(defaultHex));
        }

        // 3. Sidebar Background
        if (!string.IsNullOrWhiteSpace(custom.SidebarBackground) && !custom.SidebarBackground.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(custom.SidebarBackground, out var sbBg))
            {
                var brush = new SolidColorBrush(sbBg);
                Application.Current.Resources["AppSidebarBackgroundBrush"] = brush;
            }
        }
        else
        {
            string defaultHex = IsDarkTheme() ? "#1E293B" : "#F8FAFC";
            Application.Current.Resources["AppSidebarBackgroundBrush"] = new SolidColorBrush(Color.Parse(defaultHex));
        }

        // 4. Table Alternating Background
        if (!string.IsNullOrWhiteSpace(custom.TableBackground) && !custom.TableBackground.Equals("Default", StringComparison.OrdinalIgnoreCase))
        {
            if (Color.TryParse(custom.TableBackground, out var tbBg))
            {
                Application.Current.Resources["AppTableAltColBackgroundBrush"] = new SolidColorBrush(tbBg);
            }
        }
        else
        {
            string defaultHex = IsDarkTheme() ? "#141E2E" : "#F8FAFC";
            Application.Current.Resources["AppTableAltColBackgroundBrush"] = new SolidColorBrush(Color.Parse(defaultHex));
        }
    }
}
