using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input;
using CommunityToolkit.Mvvm.ComponentModel;

namespace Fdb.Services;

public static class ShortcutActions
{
    public const string RunQuery = "RunQuery";
    public const string SaveQuery = "SaveQuery";
    public const string NewTab = "NewTab";
    public const string CloseTab = "CloseTab";
    public const string ConnectDatabase = "ConnectDatabase";
    public const string ToggleSidebar = "ToggleSidebar";
    public const string ToggleFooter = "ToggleFooter";
    public const string ToggleFullscreen = "ToggleFullscreen";
    public const string OpenSettings = "OpenSettings";
    public const string ExportData = "ExportData";
    public const string ZoomIn = "ZoomIn";
    public const string ZoomOut = "ZoomOut";
    public const string ResetZoom = "ResetZoom";
}

public partial class ShortcutItem : ObservableObject
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string DefaultGesture { get; set; } = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsModified))]
    [NotifyPropertyChangedFor(nameof(KeyTokens))]
    private string _currentGesture = string.Empty;

    [ObservableProperty]
    private bool _isEditing;

    [ObservableProperty]
    private string _editingGesture = string.Empty;

    public bool IsModified => !string.Equals(CurrentGesture, DefaultGesture, StringComparison.OrdinalIgnoreCase);

    public string[] KeyTokens => string.IsNullOrWhiteSpace(CurrentGesture) 
        ? Array.Empty<string>() 
        : CurrentGesture.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
}

public class ShortcutService
{
    private static ShortcutService? _instance;
    public static ShortcutService Instance => _instance ??= new ShortcutService();

    private readonly List<ShortcutItem> _shortcuts = new();
    private readonly object _lock = new();

    public event EventHandler? ShortcutsChanged;

    public static readonly IReadOnlyList<ShortcutItem> DefaultDefinitions = new List<ShortcutItem>
    {
        new() { Id = ShortcutActions.RunQuery, Name = "Execute Active Query", Category = "Query & Execution", DefaultGesture = "F5", CurrentGesture = "F5", Description = "Executes the active query or selected SQL text in the editor." },
        new() { Id = ShortcutActions.SaveQuery, Name = "Save Query", Category = "File & Editor", DefaultGesture = "Ctrl+S", CurrentGesture = "Ctrl+S", Description = "Saves active SQL query buffer to persistent SQLite storage." },
        new() { Id = ShortcutActions.NewTab, Name = "New Query Tab", Category = "File & Editor", DefaultGesture = "Ctrl+N", CurrentGesture = "Ctrl+N", Description = "Opens a new empty SQL editor tab in the main workspace." },
        new() { Id = ShortcutActions.CloseTab, Name = "Close Query Tab", Category = "File & Editor", DefaultGesture = "Ctrl+W", CurrentGesture = "Ctrl+W", Description = "Closes the currently active query tab." },
        new() { Id = ShortcutActions.ConnectDatabase, Name = "Connect Database", Category = "Database", DefaultGesture = "Ctrl+O", CurrentGesture = "Ctrl+O", Description = "Opens the database connection dialog (MSSQL, SQLite, Postgres, MySQL)." },
        new() { Id = ShortcutActions.ToggleSidebar, Name = "Toggle Explorer Sidebar", Category = "Panels & View", DefaultGesture = "Ctrl+B", CurrentGesture = "Ctrl+B", Description = "Shows or hides the database object explorer sidebar." },
        new() { Id = ShortcutActions.ToggleFooter, Name = "Toggle Results Panel", Category = "Panels & View", DefaultGesture = "Ctrl+J", CurrentGesture = "Ctrl+J", Description = "Collapses or expands the query results & schema footer panel." },
        new() { Id = ShortcutActions.ToggleFullscreen, Name = "Toggle Fullscreen Results", Category = "Panels & View", DefaultGesture = "F11", CurrentGesture = "F11", Description = "Maximizes query results to full workspace height or restores split layout." },
        new() { Id = ShortcutActions.OpenSettings, Name = "Preferences & Settings", Category = "General", DefaultGesture = "Ctrl+,", CurrentGesture = "Ctrl+,", Description = "Opens the application settings and preferences dialog." },
        new() { Id = ShortcutActions.ExportData, Name = "Export Query Results", Category = "Data", DefaultGesture = "Ctrl+E", CurrentGesture = "Ctrl+E", Description = "Opens data export modal (CSV, JSON, SQL, TSV formats)." },
        new() { Id = ShortcutActions.ZoomIn, Name = "Editor Zoom In", Category = "Editor", DefaultGesture = "Ctrl++", CurrentGesture = "Ctrl++", Description = "Increases SQL code editor font size." },
        new() { Id = ShortcutActions.ZoomOut, Name = "Editor Zoom Out", Category = "Editor", DefaultGesture = "Ctrl+-", CurrentGesture = "Ctrl+-", Description = "Decreases SQL code editor font size." },
        new() { Id = ShortcutActions.ResetZoom, Name = "Reset Editor Zoom", Category = "Editor", DefaultGesture = "Ctrl+0", CurrentGesture = "Ctrl+0", Description = "Resets SQL code editor font size to default 13px." }
    };

    public IReadOnlyList<ShortcutItem> Shortcuts
    {
        get
        {
            lock (_lock)
            {
                return _shortcuts.ToList();
            }
        }
    }

    public async Task InitializeAsync()
    {
        try
        {
            var dbRecords = await DatabaseStorageService.Instance.GetShortcutsFromDbAsync().ConfigureAwait(false);
            var recordDict = dbRecords.ToDictionary(r => r.Id, StringComparer.OrdinalIgnoreCase);

            var toSeed = new List<ShortcutDbRecord>();

            lock (_lock)
            {
                _shortcuts.Clear();
                foreach (var def in DefaultDefinitions)
                {
                    var item = new ShortcutItem
                    {
                        Id = def.Id,
                        Name = def.Name,
                        Category = def.Category,
                        Description = def.Description,
                        DefaultGesture = def.DefaultGesture,
                        CurrentGesture = def.DefaultGesture
                    };

                    if (recordDict.TryGetValue(def.Id, out var saved))
                    {
                        item.CurrentGesture = !string.IsNullOrWhiteSpace(saved.KeyGesture) ? saved.KeyGesture : def.DefaultGesture;
                    }
                    else
                    {
                        toSeed.Add(new ShortcutDbRecord
                        {
                            Id = def.Id,
                            Name = def.Name,
                            Category = def.Category,
                            KeyGesture = def.DefaultGesture,
                            DefaultGesture = def.DefaultGesture,
                            Description = def.Description
                        });
                    }

                    _shortcuts.Add(item);
                }
            }

            if (toSeed.Count > 0)
            {
                await DatabaseStorageService.Instance.SaveShortcutsBatchAsync(toSeed).ConfigureAwait(false);
            }

            ShortcutsChanged?.Invoke(this, EventArgs.Empty);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ShortcutService] InitializeAsync error: {ex.Message}");
            lock (_lock)
            {
                if (_shortcuts.Count == 0)
                {
                    _shortcuts.AddRange(DefaultDefinitions);
                }
            }
        }
    }

    public string GetGesture(string actionId)
    {
        lock (_lock)
        {
            var item = _shortcuts.FirstOrDefault(s => string.Equals(s.Id, actionId, StringComparison.OrdinalIgnoreCase));
            return item?.CurrentGesture ?? DefaultDefinitions.FirstOrDefault(d => string.Equals(d.Id, actionId, StringComparison.OrdinalIgnoreCase))?.DefaultGesture ?? string.Empty;
        }
    }

    public bool IsMatch(string actionId, KeyEventArgs e)
    {
        string gesture = GetGesture(actionId);
        if (string.IsNullOrWhiteSpace(gesture)) return false;
        return MatchesGesture(gesture, e);
    }

    public static bool MatchesGesture(string gestureStr, KeyEventArgs e)
    {
        if (string.IsNullOrWhiteSpace(gestureStr)) return false;

        string[] parts = gestureStr.Split('+', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        if (parts.Length == 0) return false;

        bool requireCtrl = false;
        bool requireShift = false;
        bool requireAlt = false;
        string keyPart = string.Empty;

        for (int i = 0; i < parts.Length; i++)
        {
            string p = parts[i].Trim();
            if (p.Equals("Ctrl", StringComparison.OrdinalIgnoreCase) || p.Equals("Control", StringComparison.OrdinalIgnoreCase))
            {
                requireCtrl = true;
            }
            else if (p.Equals("Shift", StringComparison.OrdinalIgnoreCase))
            {
                requireShift = true;
            }
            else if (p.Equals("Alt", StringComparison.OrdinalIgnoreCase))
            {
                requireAlt = true;
            }
            else
            {
                keyPart = p;
            }
        }

        // Modifiers check
        bool hasCtrl = e.KeyModifiers.HasFlag(KeyModifiers.Control);
        bool hasShift = e.KeyModifiers.HasFlag(KeyModifiers.Shift);
        bool hasAlt = e.KeyModifiers.HasFlag(KeyModifiers.Alt);

        if (requireCtrl != hasCtrl || requireShift != hasShift || requireAlt != hasAlt)
        {
            return false;
        }

        if (string.IsNullOrWhiteSpace(keyPart))
        {
            return false;
        }

        return MatchesKey(keyPart, e.Key);
    }

    public static bool MatchesKey(string keyString, Key key)
    {
        // Special symbol aliases
        if (keyString == "," || keyString.Equals("OemComma", StringComparison.OrdinalIgnoreCase))
        {
            return key == Key.OemComma;
        }

        if (keyString == "." || keyString.Equals("OemPeriod", StringComparison.OrdinalIgnoreCase))
        {
            return key == Key.OemPeriod;
        }

        if (keyString == "+" || keyString.Equals("Add", StringComparison.OrdinalIgnoreCase) || keyString.Equals("OemPlus", StringComparison.OrdinalIgnoreCase))
        {
            return key is Key.OemPlus or Key.Add;
        }

        if (keyString == "-" || keyString.Equals("Subtract", StringComparison.OrdinalIgnoreCase) || keyString.Equals("OemMinus", StringComparison.OrdinalIgnoreCase))
        {
            return key is Key.OemMinus or Key.Subtract;
        }

        if (keyString == "0" || keyString.Equals("D0", StringComparison.OrdinalIgnoreCase))
        {
            return key is Key.D0 or Key.NumPad0;
        }

        if (keyString.Length == 1 && char.IsDigit(keyString[0]))
        {
            int digit = keyString[0] - '0';
            return key == (Key)((int)Key.D0 + digit) || key == (Key)((int)Key.NumPad0 + digit);
        }

        if (Enum.TryParse<Key>(keyString, ignoreCase: true, out var parsedKey))
        {
            return key == parsedKey;
        }

        return false;
    }

    public static string KeyEventArgsToGesture(KeyEventArgs e)
    {
        if (e.Key is Key.LeftCtrl or Key.RightCtrl or Key.LeftShift or Key.RightShift or Key.LeftAlt or Key.RightAlt)
        {
            return string.Empty;
        }

        var parts = new List<string>();
        if (e.KeyModifiers.HasFlag(KeyModifiers.Control)) parts.Add("Ctrl");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Shift)) parts.Add("Shift");
        if (e.KeyModifiers.HasFlag(KeyModifiers.Alt)) parts.Add("Alt");

        string keyName = e.Key switch
        {
            Key.OemComma => ",",
            Key.OemPeriod => ".",
            Key.OemPlus or Key.Add => "+",
            Key.OemMinus or Key.Subtract => "-",
            Key.D0 or Key.NumPad0 => "0",
            Key.D1 or Key.NumPad1 => "1",
            Key.D2 or Key.NumPad2 => "2",
            Key.D3 or Key.NumPad3 => "3",
            Key.D4 or Key.NumPad4 => "4",
            Key.D5 or Key.NumPad5 => "5",
            Key.D6 or Key.NumPad6 => "6",
            Key.D7 or Key.NumPad7 => "7",
            Key.D8 or Key.NumPad8 => "8",
            Key.D9 or Key.NumPad9 => "9",
            _ => e.Key.ToString()
        };

        parts.Add(keyName);
        return string.Join("+", parts);
    }

    public async Task SaveShortcutAsync(string actionId, string newGesture)
    {
        if (string.IsNullOrWhiteSpace(actionId) || string.IsNullOrWhiteSpace(newGesture)) return;

        lock (_lock)
        {
            var item = _shortcuts.FirstOrDefault(s => string.Equals(s.Id, actionId, StringComparison.OrdinalIgnoreCase));
            if (item != null)
            {
                item.CurrentGesture = newGesture.Trim();
            }
        }

        await DatabaseStorageService.Instance.SaveShortcutToDbAsync(actionId, newGesture.Trim()).ConfigureAwait(false);
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task SaveAllAsync(IEnumerable<ShortcutItem> items)
    {
        var dbRecords = new List<ShortcutDbRecord>();
        lock (_lock)
        {
            foreach (var item in items)
            {
                var existing = _shortcuts.FirstOrDefault(s => string.Equals(s.Id, item.Id, StringComparison.OrdinalIgnoreCase));
                if (existing != null)
                {
                    existing.CurrentGesture = item.CurrentGesture;
                }

                dbRecords.Add(new ShortcutDbRecord
                {
                    Id = item.Id,
                    Name = item.Name,
                    Category = item.Category,
                    KeyGesture = item.CurrentGesture,
                    DefaultGesture = item.DefaultGesture,
                    Description = item.Description
                });
            }
        }

        await DatabaseStorageService.Instance.SaveShortcutsBatchAsync(dbRecords).ConfigureAwait(false);
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }

    public async Task ResetAllToDefaultsAsync()
    {
        lock (_lock)
        {
            foreach (var item in _shortcuts)
            {
                item.CurrentGesture = item.DefaultGesture;
            }
        }

        await DatabaseStorageService.Instance.ResetAllShortcutsInDbAsync().ConfigureAwait(false);
        ShortcutsChanged?.Invoke(this, EventArgs.Empty);
    }
}
