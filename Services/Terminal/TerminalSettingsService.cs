using System;
using System.Globalization;
using System.Threading.Tasks;
using Quell.Models;

namespace Quell.Services.Terminal;

public class TerminalSettingsService
{
    private static TerminalSettingsService? _instance;
    public static TerminalSettingsService Instance => _instance ??= new TerminalSettingsService();

    private readonly DatabaseStorageService _storage = DatabaseStorageService.Instance;
    private TerminalSettingsModel _cachedSettings = new();

    public TerminalSettingsModel CurrentSettings => _cachedSettings;

    public event EventHandler<TerminalSettingsModel>? SettingsChanged;

    public async Task<TerminalSettingsModel> LoadSettingsAsync()
    {
        var model = new TerminalSettingsModel();

        model.DefaultShellId = await _storage.GetSettingAsync("Terminal_DefaultShell", "default");
        model.FontFamily = await _storage.GetSettingAsync("Terminal_FontFamily", "Cascadia Mono, Consolas, Courier New, monospace");

        var fontSizeStr = await _storage.GetSettingAsync("Terminal_FontSize", "13");
        if (double.TryParse(fontSizeStr, NumberStyles.Any, CultureInfo.InvariantCulture, out var fs))
        {
            model.FontSize = fs;
        }

        model.CursorStyle = await _storage.GetSettingAsync("Terminal_CursorStyle", "Block");
        var blinkStr = await _storage.GetSettingAsync("Terminal_CursorBlink", "true");
        model.CursorBlink = blinkStr == "true" || blinkStr == "1";

        var scrollStr = await _storage.GetSettingAsync("Terminal_MaxScrollback", "5000");
        if (int.TryParse(scrollStr, out var scroll))
        {
            model.MaxScrollback = scroll;
        }

        model.PsqlPath = await _storage.GetSettingAsync("Terminal_PsqlPath", "");
        model.MySqlPath = await _storage.GetSettingAsync("Terminal_MySqlPath", "");
        model.SqlitePath = await _storage.GetSettingAsync("Terminal_SqlitePath", "");
        model.SqlCmdPath = await _storage.GetSettingAsync("Terminal_SqlCmdPath", "");

        _cachedSettings = model;
        return model;
    }

    public async Task SaveSettingsAsync(TerminalSettingsModel settings)
    {
        _cachedSettings = settings;

        await _storage.SaveSettingAsync("Terminal_DefaultShell", settings.DefaultShellId);
        await _storage.SaveSettingAsync("Terminal_FontFamily", settings.FontFamily);
        await _storage.SaveSettingAsync("Terminal_FontSize", settings.FontSize.ToString(CultureInfo.InvariantCulture));
        await _storage.SaveSettingAsync("Terminal_CursorStyle", settings.CursorStyle);
        await _storage.SaveSettingAsync("Terminal_CursorBlink", settings.CursorBlink ? "true" : "false");
        await _storage.SaveSettingAsync("Terminal_MaxScrollback", settings.MaxScrollback.ToString());
        await _storage.SaveSettingAsync("Terminal_PsqlPath", settings.PsqlPath);
        await _storage.SaveSettingAsync("Terminal_MySqlPath", settings.MySqlPath);
        await _storage.SaveSettingAsync("Terminal_SqlitePath", settings.SqlitePath);
        await _storage.SaveSettingAsync("Terminal_SqlCmdPath", settings.SqlCmdPath);

        SettingsChanged?.Invoke(this, settings);
    }
}
