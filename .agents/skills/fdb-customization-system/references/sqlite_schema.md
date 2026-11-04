# SQLite Customization Storage Schema

Fdb Studio persists all user appearance preferences, colors, shortcuts, and panel dimensions in SQLite using a lightweight, key-value `settings` table.

## 1. Table Schema

```sql
CREATE TABLE IF NOT EXISTS settings (
    key TEXT PRIMARY KEY,
    value TEXT NOT NULL
);
```

## 2. Key Taxonomy

### Theme & Syntax Keys
- `Theme`: Current active theme (`"Light"`, `"Dark"`, or `"System"`).
- `KeywordColor`: Selected SQL syntax keyword color preset (`"Blue"`, `"Green"`, `"Purple"`, `"Amber"`, `"Rose"`, `"Cyan"`).

### Panel & Workspace Customization Keys
- `ButtonAccentColor`: Hex string (e.g. `"#38BDF8"`) or `"Default"`.
- `ButtonBackgroundColor`: Hex string or `"Default"`.
- `ButtonTextColor`: Hex string or `"Default"`.
- `ButtonBorderColor`: Hex string or `"Default"`.
- `BorderColor`: Global border color hex or `"Default"`.
- `SplitterColor`: Splitter resizer hex or `"Default"`.
- `SidebarBackground`: Hex string or `"Default"`.
- `SidebarHeaderBackground`: Hex string or `"Default"`.
- `SidebarItemHoverBackground`: Hex string or `"Default"`.
- `SidebarTextColor`: Hex string or `"Default"`.
- `SidebarBorderColor`: Hex string or `"Default"`.
- `EditorBackground`: Hex string or `"Default"`.
- `EditorForeground`: Hex string or `"Default"`.
- `EditorFontSize`: Decimal string (e.g. `"13"`).
- `EditorShowLineNumbers`: `"true"` or `"false"`.

### Results Table Customization Keys
- `TableRowHeight`: Decimal string (e.g. `"25"`).
- `TableHeaderHeight`: Decimal string (e.g. `"26"`).
- `TableGutterWidth`: Decimal string (e.g. `"56"`).
- `TableCellFontSize`: Decimal string (e.g. `"11"`).
- `TableBackground`: Hex string or `"Default"`.
- `TableAlternatingRowColor`: Hex string or `"Default"`.
- `TableCellTextColor`: Hex string or `"Default"`.
- `TableHeaderBackground`: Hex string or `"Default"`.
- `TableHeaderTextColor`: Hex string or `"Default"`.
- `TableGutterBackground`: Hex string or `"Default"`.
- `TableGutterTextColor`: Hex string or `"Default"`.
- `TableGridLinesColor`: Hex string or `"Default"`.
- `TableSelectionColor`: Hex string or `"Default"`.
- `TableShowGridLines`: `"true"` or `"false"`.
- `TableShowAlternatingRowColors`: `"true"` or `"false"`.

## 3. Atomic Batch Persistence

To eliminate disk I/O bottlenecks and SQLite file lock contention, all settings are saved inside a single SQLite transaction via `SaveSettingsBatchAsync`:

```csharp
public async Task SaveSettingsBatchAsync(IReadOnlyDictionary<string, string> settings)
{
    await using var conn = new SqliteConnection(_connectionString);
    await conn.OpenAsync().ConfigureAwait(false);
    await using var trans = conn.BeginTransaction();
    
    foreach (var (k, v) in settings)
    {
        await using var cmd = conn.CreateCommand();
        cmd.Transaction = trans;
        cmd.CommandText = "INSERT OR REPLACE INTO settings (key, value) VALUES (@k, @v);";
        cmd.Parameters.AddWithValue("@k", k);
        cmd.Parameters.AddWithValue("@v", v);
        await cmd.ExecuteNonQueryAsync().ConfigureAwait(false);
    }
    await trans.CommitAsync().ConfigureAwait(false);
}
```

## 4. Splash Screen Pre-Caching

During application startup, `DatabaseStorageService.InitializeStartupCacheAsync` runs in the splash screen sequence:
1. Loads all settings into an in-memory dictionary.
2. Applies theme, keyword colors, panel customization, and table customization to `ThemeService`.
3. Pre-caches shortcuts and connection lists.
4. Transitions smoothly to the main window with zero UI layout recalculation or flickering.
