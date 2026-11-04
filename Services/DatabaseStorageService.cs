using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Dapper;
using Microsoft.Data.Sqlite;
using Fdb.ViewModels;
using Fdb.Services.DatabaseProviders;

namespace Fdb.Services;

public class ConnectionRecord
{
    public int Id { get; set; }
    public string Server { get; set; } = "localhost";
    public int Port { get; set; } = 1433;
    public string DatabaseName { get; set; } = "master";
    public string AuthType { get; set; } = "SQL Server Authentication";
    public string Username { get; set; } = "sa";
    public string Password { get; set; } = string.Empty;
    public string DatabaseType { get; set; } = "MSSQL";
    public string LastConnectedAt { get; set; } = string.Empty;

    public string DisplayTitle => DatabaseType == "SQLite"
        ? (string.IsNullOrWhiteSpace(DatabaseName) || DatabaseName == "main" || DatabaseName == "master"
            ? (string.IsNullOrWhiteSpace(Path.GetFileName(Server)) || Server == "localhost" ? "SQLite DB" : Path.GetFileName(Server))
            : DatabaseName)
        : (!string.IsNullOrWhiteSpace(DatabaseName) ? DatabaseName : Server);

    public string BadgeColor => DatabaseType switch
    {
        "MSSQL" => "#2563EB",
        "PostgreSQL" => "#336791",
        "MySQL" => "#00758F",
        "SQLite" => "#003B57",
        _ => "#4B5563"
    };

    public bool IsActive { get; set; }
    public string StatusBadgeText => IsActive ? "ACTIVE" : "SAVED";
    public string StatusBadgeColor => IsActive ? "#10B981" : "#6B7280";

    public ConnectionRecord Clone() => new()
    {
        Id = Id,
        Server = Server,
        Port = Port,
        DatabaseName = DatabaseName,
        AuthType = AuthType,
        Username = Username,
        Password = Password,
        DatabaseType = DatabaseType,
        LastConnectedAt = LastConnectedAt,
        IsActive = IsActive
    };
}

public class QueryHistoryRecord
{
    public int Id { get; set; }
    public string QueryText { get; set; } = string.Empty;
    public string ExecutedAt { get; set; } = string.Empty;
    public string ExecutionTime { get; set; } = string.Empty;
    public int RowCount { get; set; }
    public string Status { get; set; } = "Success";
}

public class SavedQueryRecord
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string QueryText { get; set; } = string.Empty;
    public string UpdatedAt { get; set; } = string.Empty;
}

public class DialectKeywordRecord
{
    public int Id { get; set; }
    public string Dialect { get; set; } = "General";
    public string Keyword { get; set; } = string.Empty;
    public string Category { get; set; } = "Keyword";
}

public class ShortcutDbRecord
{
    public string Id { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
    public string KeyGesture { get; set; } = string.Empty;
    public string DefaultGesture { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class PanelCustomizationRecord
{
    public string EditorBackground { get; set; } = "Default";
    public string EditorForeground { get; set; } = "Default";
    public string SidebarBackground { get; set; } = "Default";
    public string TableBackground { get; set; } = "Default";
    public double EditorFontSize { get; set; } = 13.0;
    public bool EditorShowLineNumbers { get; set; } = true;
}

public class TableCustomizationRecord
{
    public double RowHeight { get; set; } = 25.0;
    public double HeaderHeight { get; set; } = 26.0;
    public double GutterWidth { get; set; } = 56.0;
    public double FontSize { get; set; } = 11.0;
    public string TableBackground { get; set; } = "Default";
    public string AlternatingRowColor { get; set; } = "Default";
    public string CellTextColor { get; set; } = "Default";
    public string HeaderBackground { get; set; } = "Default";
    public string HeaderTextColor { get; set; } = "Default";
    public string GutterBackground { get; set; } = "Default";
    public string GutterTextColor { get; set; } = "Default";
    public string GridLinesColor { get; set; } = "Default";
    public string SelectionColor { get; set; } = "Default";
    public bool ShowGridLines { get; set; } = true;
    public bool ShowAlternatingRowColors { get; set; } = true;
}

public class DatabaseStorageService
{
    private static DatabaseStorageService? _instance;
    public static DatabaseStorageService Instance => _instance ??= new DatabaseStorageService();

    private readonly string _connectionString;
    private readonly System.Threading.SemaphoreSlim _initLock = new(1, 1);
    private bool _initialized;

    private readonly System.Collections.Concurrent.ConcurrentDictionary<string, string> _settingsCache = new(StringComparer.OrdinalIgnoreCase);
    private bool _settingsLoaded;

    private List<ConnectionRecord>? _cachedRecentConnections;
    private readonly object _connsLock = new();

    private List<QueryHistoryRecord>? _cachedQueryHistory;
    private readonly object _historyLock = new();

    private List<ShortcutDbRecord>? _cachedShortcuts;
    private readonly object _shortcutsLock = new();

    public string DatabasePath { get; }
    public bool IsFirstRun { get; private set; }

    public static string ResolveDatabasePath()
    {
        string? envPath = Environment.GetEnvironmentVariable("FDB_STUDIO_DB_PATH");
        if (!string.IsNullOrWhiteSpace(envPath))
        {
            var dir = Path.GetDirectoryName(envPath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            return envPath;
        }

        string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
        string appFolder = Path.Combine(appData, "FdbStudio");
        if (!Directory.Exists(appFolder))
        {
            Directory.CreateDirectory(appFolder);
        }
        return Path.Combine(appFolder, "fdb_studio.db");
    }

    public DatabaseStorageService()
    {
        DatabasePath = ResolveDatabasePath();
        _connectionString = new SqliteConnectionStringBuilder
        {
            DataSource = DatabasePath,
            Mode = SqliteOpenMode.ReadWriteCreate
        }.ToString();
    }

    public async Task InitializeAsync()
    {
        await InitializeWithProgressAsync(null);
    }

    public async Task InitializeWithProgressAsync(Action<double, string>? reportProgress)
    {
        if (_initialized)
        {
            reportProgress?.Invoke(100, "Studio Database Ready.");
            return;
        }

        await _initLock.WaitAsync();
        try
        {
            if (_initialized)
            {
                reportProgress?.Invoke(100, "Studio Database Ready.");
                return;
            }

            reportProgress?.Invoke(10, "Opening SQLite Database connection...");
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Check if database was already initialized on a previous run
            bool isAlreadyInitialized = false;
            try
            {
                var initVal = await connection.ExecuteScalarAsync<string>(
                    "SELECT Value FROM AppSettings WHERE Key = 'DatabaseInitialized';");
                isAlreadyInitialized = initVal == "true" || initVal == "1";
            }
            catch
            {
                // AppSettings table does not exist -> First run
                isAlreadyInitialized = false;
            }

            IsFirstRun = !isAlreadyInitialized;

            if (!isAlreadyInitialized)
            {
                // FIRST RUN INITIALIZATION: Run schema setup and seed keywords
                reportProgress?.Invoke(20, "First-time setup: Initializing SQLite database schema...");

                string sqlFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "init_database.sql");
                if (!File.Exists(sqlFilePath))
                {
                    sqlFilePath = Path.Combine(Directory.GetCurrentDirectory(), "Assets", "init_database.sql");
                }

                if (File.Exists(sqlFilePath))
                {
                    string fullSql = await File.ReadAllTextAsync(sqlFilePath);
                    await ExecuteInitScriptStepsAsync(connection, fullSql, reportProgress);
                }
                else
                {
                    reportProgress?.Invoke(30, "Running core SQLite schema initialization...");
                    await ExecuteFallbackSchemaAsync(connection);
                }

                // Mark database as initialized in AppSettings so it NEVER re-runs init script again
                await connection.ExecuteAsync(@"
                    INSERT OR REPLACE INTO AppSettings (Key, Value, UpdatedAt) 
                    VALUES ('DatabaseInitialized', 'true', datetime('now'));
                    INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) 
                    VALUES ('Theme', 'Light', datetime('now'));
                    INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) 
                    VALUES ('KeywordColor', 'Blue', datetime('now'));
                    INSERT OR REPLACE INTO AppSettings (Key, Value, UpdatedAt) 
                    VALUES ('InstalledAt', datetime('now'), datetime('now'));
                ");

                reportProgress?.Invoke(85, "Initial SQLite setup complete.");
            }
            else
            {
                // SUBSEQUENT RUNS: Skip all init scripts & preserve user data
                reportProgress?.Invoke(40, "SQLite database ready. Loading cached configurations...");
            }

            // Schema migrations / safety checks (safe for both first-run and existing installs)
            try
            {
                await connection.ExecuteAsync("ALTER TABLE Connections ADD COLUMN DatabaseType TEXT DEFAULT 'MSSQL';");
            }
            catch { }

            try
            {
                await connection.ExecuteAsync("ALTER TABLE Connections ADD COLUMN Password TEXT DEFAULT '';");
            }
            catch { }

            try
            {
                await connection.ExecuteAsync(@"
                    CREATE TABLE IF NOT EXISTS UserShortcuts (
                        Id TEXT PRIMARY KEY,
                        Name TEXT NOT NULL,
                        Category TEXT NOT NULL,
                        KeyGesture TEXT NOT NULL,
                        DefaultGesture TEXT NOT NULL,
                        Description TEXT,
                        UpdatedAt TEXT NOT NULL
                    );");
            }
            catch { }

            try
            {
                await LoadAllSettingsAsync(connection);
            }
            catch { }

            _initialized = true;

            reportProgress?.Invoke(90, "Pre-caching active SQL dialect keywords...");
            await SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync();

            reportProgress?.Invoke(100, "Studio Environment Ready — SQLite Engine Initialized.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] Initialization error: {ex.Message}");
            reportProgress?.Invoke(100, "Ready (with fallback mode).");
            _initialized = true;
        }
        finally
        {
            _initLock.Release();
        }
    }

    /// <summary>
    /// Loads all key-value pairs from AppSettings in a single round-trip into the in-memory cache.
    /// </summary>
    public async Task LoadAllSettingsAsync(SqliteConnection? existingConnection = null)
    {
        try
        {
            if (existingConnection != null)
            {
                var rows = await existingConnection.QueryAsync<(string Key, string Value)>("SELECT Key, Value FROM AppSettings;");
                foreach (var (k, v) in rows)
                {
                    if (k != null) _settingsCache[k] = v ?? string.Empty;
                }
            }
            else
            {
                await InitializeAsync();
                await using var connection = new SqliteConnection(_connectionString);
                await connection.OpenAsync();
                var rows = await connection.QueryAsync<(string Key, string Value)>("SELECT Key, Value FROM AppSettings;");
                foreach (var (k, v) in rows)
                {
                    if (k != null) _settingsCache[k] = v ?? string.Empty;
                }
            }
            _settingsLoaded = true;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] LoadAllSettingsAsync error: {ex.Message}");
        }
    }

    /// <summary>
    /// Unified high-performance startup routine executed during the splash screen sequence.
    /// Runs SQLite schema initialization/migrations, pre-caches all settings, applies theme,
    /// pre-caches SQL dialect keywords, shortcuts, saved connections, and query history.
    /// </summary>
    public async Task PreloadStartupDataAsync(Action<double, string>? reportProgress = null)
    {
        reportProgress?.Invoke(10, "Opening SQLite Storage Engine...");
        await InitializeWithProgressAsync((p, s) =>
        {
            // Map the schema init progress from 10 to 50
            double mapped = 10 + (p * 0.4);
            reportProgress?.Invoke(mapped, s);
        }).ConfigureAwait(false);

        reportProgress?.Invoke(55, "Pre-caching Application Settings & Preferences...");
        await LoadAllSettingsAsync().ConfigureAwait(false);

        reportProgress?.Invoke(68, "Applying Appearance, Themes & Customizations...");
        string theme = await GetThemeAsync().ConfigureAwait(false);
        ThemeService.Instance.ApplyTheme(theme);
        string keywordColor = await GetKeywordColorAsync().ConfigureAwait(false);
        ThemeService.Instance.SetKeywordColor(keywordColor);
        var panelCustom = await GetPanelCustomizationAsync().ConfigureAwait(false);
        ThemeService.Instance.ApplyPanelCustomization(panelCustom);
        var tableCustom = await GetTableCustomizationAsync().ConfigureAwait(false);
        ThemeService.Instance.ApplyTableCustomization(tableCustom);

        reportProgress?.Invoke(80, "Pre-caching Keyboard Shortcuts...");
        await ShortcutService.Instance.InitializeAsync().ConfigureAwait(false);

        reportProgress?.Invoke(90, "Pre-caching Saved Connections & Database Nodes...");
        await GetRecentConnectionsAsync(forceReload: true).ConfigureAwait(false);

        reportProgress?.Invoke(96, "Pre-caching Execution History & Dialect Metadata...");
        await GetQueryHistoryAsync(forceReload: true).ConfigureAwait(false);

        reportProgress?.Invoke(100, "Studio Environment Ready — SQLite Engine Initialized.");
    }

    private static async Task ExecuteInitScriptStepsAsync(SqliteConnection connection, string fullSql, Action<double, string>? reportProgress)
    {
        var stepSections = fullSql.Split(new[] { "-- [STEP " }, StringSplitOptions.RemoveEmptyEntries);

        double baseProgress = 15;
        double progressIncrement = 70.0 / Math.Max(1, stepSections.Length);

        for (int i = 0; i < stepSections.Length; i++)
        {
            string section = stepSections[i];
            int lineEndIndex = section.IndexOf('\n');
            string stepTitle = lineEndIndex > 0 ? section[..lineEndIndex].Trim() : $"Step {i + 1}";
            string stepSql = lineEndIndex > 0 ? section[lineEndIndex..] : section;

            double currentProgress = baseProgress + ((i + 1) * progressIncrement);
            reportProgress?.Invoke(Math.Min(88, currentProgress), $"Executing SQL {stepTitle}...");

            await connection.ExecuteAsync(stepSql);
        }
    }

    private static async Task ExecuteFallbackSchemaAsync(SqliteConnection connection)
    {
        const string fallbackSql = @"
            CREATE TABLE IF NOT EXISTS DialectKeywords (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Dialect TEXT NOT NULL,
                Keyword TEXT NOT NULL,
                Category TEXT NOT NULL DEFAULT 'Keyword',
                UNIQUE(Dialect, Keyword)
            );
            CREATE TABLE IF NOT EXISTS DialectSnippets (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Dialect TEXT NOT NULL,
                Keyword TEXT NOT NULL,
                Snippet TEXT NOT NULL,
                Description TEXT NOT NULL,
                UNIQUE(Dialect, Keyword)
            );
            CREATE TABLE IF NOT EXISTS Connections (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Server TEXT NOT NULL,
                Port INTEGER NOT NULL,
                DatabaseName TEXT NOT NULL,
                AuthType TEXT NOT NULL,
                Username TEXT,
                Password TEXT DEFAULT '',
                DatabaseType TEXT NOT NULL DEFAULT 'MSSQL',
                LastConnectedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS QueryHistory (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                QueryText TEXT NOT NULL,
                ExecutedAt TEXT NOT NULL,
                ExecutionTime TEXT NOT NULL,
                RowCount INTEGER NOT NULL,
                Status TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS SavedQueries (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                Title TEXT NOT NULL,
                QueryText TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS QueryResults (
                Id INTEGER PRIMARY KEY AUTOINCREMENT,
                RowIndex INTEGER NOT NULL,
                RecordId INTEGER NOT NULL,
                Name TEXT NOT NULL,
                Email TEXT NOT NULL,
                Role TEXT NOT NULL,
                Status TEXT NOT NULL,
                CreatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS AppSettings (
                Key TEXT PRIMARY KEY,
                Value TEXT NOT NULL,
                UpdatedAt TEXT NOT NULL
            );
            CREATE TABLE IF NOT EXISTS UserShortcuts (
                Id TEXT PRIMARY KEY,
                Name TEXT NOT NULL,
                Category TEXT NOT NULL,
                KeyGesture TEXT NOT NULL,
                DefaultGesture TEXT NOT NULL,
                Description TEXT,
                UpdatedAt TEXT NOT NULL
            );
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('Theme', 'Light', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('KeywordColor', 'Blue', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('EditorBackground', 'Default', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('EditorForeground', 'Default', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('SidebarBackground', 'Default', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('TableBackground', 'Default', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('EditorFontSize', '13', datetime('now'));
            INSERT OR IGNORE INTO AppSettings (Key, Value, UpdatedAt) VALUES ('EditorShowLineNumbers', 'true', datetime('now'));
        ";

        await connection.ExecuteAsync(fallbackSql);
    }

    public async Task<List<string>> GetKeywordsAsync(string dialect = "MSSQL")
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT DISTINCT Keyword FROM DialectKeywords 
                WHERE Dialect = @dialect OR Dialect = 'General' 
                ORDER BY length(Keyword) DESC, Keyword ASC;";

            var result = await connection.QueryAsync<string>(sql, new { dialect });
            return result.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetKeywordsAsync error: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<List<SqlSnippet>> GetSnippetsAsync(string dialect = "MSSQL")
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Keyword, Snippet, Description FROM DialectSnippets 
                WHERE Dialect = @dialect OR Dialect = 'General';";

            var result = await connection.QueryAsync<SqlSnippet>(sql, new { dialect });
            return result.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetSnippetsAsync error: {ex.Message}");
            return new List<SqlSnippet>();
        }
    }

    public async Task<List<string>> GetSupportedDialectsAsync()
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "SELECT DISTINCT Dialect FROM DialectKeywords WHERE Dialect != 'General' ORDER BY Dialect ASC;";
            var result = await connection.QueryAsync<string>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetSupportedDialectsAsync error: {ex.Message}");
            return new List<string>();
        }
    }

    public async Task<List<QueryResultRow>> GetQueryResultsAsync()
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT RowIndex, RecordId AS Id, Name, Email, Role, Status, CreatedAt 
                FROM QueryResults 
                ORDER BY RowIndex ASC;";

            var result = await connection.QueryAsync<QueryResultRow>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetQueryResultsAsync error: {ex.Message}");
            return new List<QueryResultRow>();
        }
    }

    public async Task<List<TableMetadata>> GetLocalTablesAsync()
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT 'main' AS SchemaName, name AS TableName, type AS TableType 
                FROM sqlite_master 
                WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%' 
                ORDER BY name;";

            var result = await connection.QueryAsync<TableMetadata>(sql);
            return result.ToList();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetLocalTablesAsync error: {ex.Message}");
            return new List<TableMetadata>();
        }
    }

    public async Task<QueryExecutionResult> ExecuteArbitraryQueryAsync(string sql)
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            var sw = System.Diagnostics.Stopwatch.StartNew();
            using var cmd = connection.CreateCommand();
            cmd.CommandText = sql;

            await using var reader = await cmd.ExecuteReaderAsync();
            var columns = new List<string>();
            for (int i = 0; i < reader.FieldCount; i++)
            {
                columns.Add(reader.GetName(i));
            }

            var rows = new List<QueryResultRow>();
            int rowIndex = 1;

            while (await reader.ReadAsync())
            {
                var row = new QueryResultRow { RowIndex = rowIndex++ };
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    string colName = reader.GetName(i);
                    object val = reader.GetValue(i);
                    string strVal = val == DBNull.Value || val == null ? "NULL" : val.ToString() ?? "";
                    row[colName] = strVal;
                }
                rows.Add(row);
            }

            sw.Stop();
            return new QueryExecutionResult
            {
                Success = true,
                Message = $"Query executed successfully ({rows.Count} rows).",
                ExecutionTime = $"{sw.ElapsedMilliseconds} ms",
                RowCount = rows.Count,
                ColumnNames = columns,
                Rows = rows
            };
        }
        catch (Exception ex)
        {
            return new QueryExecutionResult
            {
                Success = false,
                Message = $"SQLite error: {ex.Message}",
                Exception = ex
            };
        }
    }

    public async Task SaveQueryHistoryAsync(string queryText, string executionTime, int rowCount, string status = "Success")
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO QueryHistory (QueryText, ExecutedAt, ExecutionTime, RowCount, Status)
                VALUES (@queryText, datetime('now'), @executionTime, @rowCount, @status);";

            await connection.ExecuteAsync(sql, new
            {
                queryText,
                executionTime,
                rowCount,
                status
            });

            lock (_historyLock)
            {
                _cachedQueryHistory = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveQueryHistoryAsync error: {ex.Message}");
        }
    }

    public async Task<List<QueryHistoryRecord>> GetQueryHistoryAsync(bool forceReload = false)
    {
        if (!forceReload && _cachedQueryHistory != null)
        {
            lock (_historyLock)
            {
                if (_cachedQueryHistory != null)
                {
                    return _cachedQueryHistory.Select(h => new QueryHistoryRecord
                    {
                        Id = h.Id,
                        QueryText = h.QueryText,
                        ExecutedAt = h.ExecutedAt,
                        ExecutionTime = h.ExecutionTime,
                        RowCount = h.RowCount,
                        Status = h.Status
                    }).ToList();
                }
            }
        }

        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, QueryText, ExecutedAt, ExecutionTime, RowCount, Status 
                FROM QueryHistory 
                ORDER BY Id DESC 
                LIMIT 50;";

            var result = (await connection.QueryAsync<QueryHistoryRecord>(sql)).ToList();
            lock (_historyLock)
            {
                _cachedQueryHistory = result.Select(h => new QueryHistoryRecord
                {
                    Id = h.Id,
                    QueryText = h.QueryText,
                    ExecutedAt = h.ExecutedAt,
                    ExecutionTime = h.ExecutionTime,
                    RowCount = h.RowCount,
                    Status = h.Status
                }).ToList();
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetQueryHistoryAsync error: {ex.Message}");
            return new List<QueryHistoryRecord>();
        }
    }

    public async Task<int> SaveConnectionAsync(string server, int port, string databaseName, string authType, string username, string databaseType = "MSSQL", string password = "")
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            // Check if connection with same server, port, database and type already exists
            const string findSql = @"
                SELECT Id FROM Connections 
                WHERE Server = @server AND Port = @port AND DatabaseName = @databaseName AND DatabaseType = @databaseType 
                LIMIT 1;";

            int existingId = await connection.ExecuteScalarAsync<int>(findSql, new
            {
                server = server ?? "localhost",
                port,
                databaseName = databaseName ?? "master",
                databaseType = databaseType ?? "MSSQL"
            });

            if (existingId > 0)
            {
                await UpdateConnectionAsync(existingId, server ?? string.Empty, port, databaseName ?? string.Empty, authType ?? string.Empty, username ?? string.Empty, databaseType ?? string.Empty, password ?? string.Empty);
                lock (_connsLock)
                {
                    _cachedRecentConnections = null;
                }
                return existingId;
            }

            const string sql = @"
                INSERT INTO Connections (Server, Port, DatabaseName, AuthType, Username, Password, DatabaseType, LastConnectedAt)
                VALUES (@server, @port, @databaseName, @authType, @username, @password, @databaseType, datetime('now'));
                SELECT last_insert_rowid();";

            int newId = await connection.ExecuteScalarAsync<int>(sql, new
            {
                server = server ?? "localhost",
                port,
                databaseName = databaseName ?? "master",
                authType = authType ?? "SQL Server Authentication",
                username = username ?? string.Empty,
                password = password ?? string.Empty,
                databaseType = databaseType ?? "MSSQL"
            });

            lock (_connsLock)
            {
                _cachedRecentConnections = null;
            }
            return newId;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveConnectionAsync error: {ex.Message}");
            return 0;
        }
    }

    public async Task<bool> UpdateConnectionAsync(int id, string server, int port, string databaseName, string authType, string username, string databaseType = "MSSQL", string password = "")
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                UPDATE Connections 
                SET Server = @server, Port = @port, DatabaseName = @databaseName, 
                    AuthType = @authType, Username = @username,
                    Password = CASE WHEN @password != '' THEN @password ELSE Password END,
                    DatabaseType = @databaseType, 
                    LastConnectedAt = datetime('now')
                WHERE Id = @id;";

            int affected = await connection.ExecuteAsync(sql, new
            {
                id,
                server = server ?? "localhost",
                port,
                databaseName = databaseName ?? "master",
                authType = authType ?? "SQL Server Authentication",
                username = username ?? string.Empty,
                password = password ?? string.Empty,
                databaseType = databaseType ?? "MSSQL"
            });

            lock (_connsLock)
            {
                _cachedRecentConnections = null;
            }
            return affected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] UpdateConnectionAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteConnectionAsync(int id)
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "DELETE FROM Connections WHERE Id = @id;";
            int affected = await connection.ExecuteAsync(sql, new { id });

            lock (_connsLock)
            {
                _cachedRecentConnections = null;
            }
            return affected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] DeleteConnectionAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<bool> DeleteConnectionAsync(string server, string databaseName)
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "DELETE FROM Connections WHERE Server = @server AND DatabaseName = @databaseName;";
            int affected = await connection.ExecuteAsync(sql, new { server, databaseName });

            lock (_connsLock)
            {
                _cachedRecentConnections = null;
            }
            return affected > 0;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] DeleteConnectionAsync error: {ex.Message}");
            return false;
        }
    }

    public async Task<List<ConnectionRecord>> GetRecentConnectionsAsync(bool forceReload = false)
    {
        if (!forceReload && _cachedRecentConnections != null)
        {
            lock (_connsLock)
            {
                if (_cachedRecentConnections != null)
                {
                    return _cachedRecentConnections.Select(c => c.Clone()).ToList();
                }
            }
        }

        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, Server, Port, DatabaseName, AuthType, COALESCE(Username, '') AS Username, 
                       COALESCE(Password, '') AS Password,
                       COALESCE(DatabaseType, 'MSSQL') AS DatabaseType, LastConnectedAt 
                FROM Connections 
                WHERE Server NOT LIKE '%fdb_studio%' AND DatabaseName NOT LIKE '%fdb_studio%'
                ORDER BY Id DESC 
                LIMIT 20;";

            var result = (await connection.QueryAsync<ConnectionRecord>(sql)).ToList();
            lock (_connsLock)
            {
                _cachedRecentConnections = result.Select(c => c.Clone()).ToList();
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetRecentConnectionsAsync error: {ex.Message}");
            return new List<ConnectionRecord>();
        }
    }

    public async Task<ConnectionRecord?> GetConnectionByIdAsync(int id)
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                SELECT Id, Server, Port, DatabaseName, AuthType, COALESCE(Username, '') AS Username, 
                       COALESCE(Password, '') AS Password,
                       COALESCE(DatabaseType, 'MSSQL') AS DatabaseType, LastConnectedAt 
                FROM Connections 
                WHERE Id = @id AND Server NOT LIKE '%fdb_studio%' AND DatabaseName NOT LIKE '%fdb_studio%';";

            return await connection.QueryFirstOrDefaultAsync<ConnectionRecord>(sql, new { id });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetConnectionByIdAsync error: {ex.Message}");
            return null;
        }
    }

    public async Task<string> GetSettingAsync(string key, string defaultValue = "")
    {
        if (_settingsLoaded && _settingsCache.TryGetValue(key, out var cachedVal))
        {
            return cachedVal;
        }

        try
        {
            await InitializeAsync();
            if (_settingsLoaded && _settingsCache.TryGetValue(key, out var cachedVal2))
            {
                return cachedVal2;
            }

            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "SELECT Value FROM AppSettings WHERE Key = @key LIMIT 1;";
            var result = await connection.ExecuteScalarAsync<string>(sql, new { key });
            string val = result ?? defaultValue;
            _settingsCache[key] = val;
            return val;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetSettingAsync error: {ex.Message}");
            return defaultValue;
        }
    }

    public async Task SaveSettingAsync(string key, string value)
    {
        _settingsCache[key] = value;
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO AppSettings (Key, Value, UpdatedAt) 
                VALUES (@key, @value, datetime('now'))
                ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = datetime('now');";

            await connection.ExecuteAsync(sql, new { key, value });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveSettingAsync error: {ex.Message}");
        }
    }

    public async Task SaveSettingsBatchAsync(IDictionary<string, string> settings)
    {
        foreach (var kvp in settings)
        {
            _settingsCache[kvp.Key] = kvp.Value;
        }

        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();
            await using var tx = await connection.BeginTransactionAsync();

            const string sql = @"
                INSERT INTO AppSettings (Key, Value, UpdatedAt) 
                VALUES (@key, @value, datetime('now'))
                ON CONFLICT(Key) DO UPDATE SET Value = @value, UpdatedAt = datetime('now');";

            foreach (var kvp in settings)
            {
                await connection.ExecuteAsync(sql, new { key = kvp.Key, value = kvp.Value }, transaction: tx);
            }
            await tx.CommitAsync();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveSettingsBatchAsync error: {ex.Message}");
        }
    }

    public async Task<string> GetThemeAsync() => await GetSettingAsync("Theme", "Light");
    public async Task SaveThemeAsync(string theme) => await SaveSettingAsync("Theme", theme);

    public async Task<string> GetKeywordColorAsync() => await GetSettingAsync("KeywordColor", "Blue");
    public async Task SaveKeywordColorAsync(string color) => await SaveSettingAsync("KeywordColor", color);

    public async Task<List<ShortcutDbRecord>> GetShortcutsFromDbAsync(bool forceReload = false)
    {
        if (!forceReload && _cachedShortcuts != null)
        {
            lock (_shortcutsLock)
            {
                if (_cachedShortcuts != null)
                {
                    return _cachedShortcuts.Select(s => new ShortcutDbRecord
                    {
                        Id = s.Id,
                        Name = s.Name,
                        Category = s.Category,
                        KeyGesture = s.KeyGesture,
                        DefaultGesture = s.DefaultGesture,
                        Description = s.Description
                    }).ToList();
                }
            }
        }

        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "SELECT Id, Name, Category, KeyGesture, DefaultGesture, Description FROM UserShortcuts ORDER BY Category, Name;";
            var result = (await connection.QueryAsync<ShortcutDbRecord>(sql)).ToList();
            lock (_shortcutsLock)
            {
                _cachedShortcuts = result.Select(s => new ShortcutDbRecord
                {
                    Id = s.Id,
                    Name = s.Name,
                    Category = s.Category,
                    KeyGesture = s.KeyGesture,
                    DefaultGesture = s.DefaultGesture,
                    Description = s.Description
                }).ToList();
            }
            return result;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetShortcutsFromDbAsync error: {ex.Message}");
            return new List<ShortcutDbRecord>();
        }
    }

    public async Task SaveShortcutToDbAsync(string id, string keyGesture)
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                UPDATE UserShortcuts 
                SET KeyGesture = @keyGesture, UpdatedAt = datetime('now') 
                WHERE Id = @id;";
            await connection.ExecuteAsync(sql, new { id, keyGesture });

            lock (_shortcutsLock)
            {
                _cachedShortcuts = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveShortcutToDbAsync error: {ex.Message}");
        }
    }

    public async Task SaveShortcutsBatchAsync(IEnumerable<ShortcutDbRecord> records)
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = @"
                INSERT INTO UserShortcuts (Id, Name, Category, KeyGesture, DefaultGesture, Description, UpdatedAt)
                VALUES (@Id, @Name, @Category, @KeyGesture, @DefaultGesture, @Description, datetime('now'))
                ON CONFLICT(Id) DO UPDATE SET KeyGesture = @KeyGesture, UpdatedAt = datetime('now');";
            await connection.ExecuteAsync(sql, records);

            lock (_shortcutsLock)
            {
                _cachedShortcuts = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveShortcutsBatchAsync error: {ex.Message}");
        }
    }

    public async Task ResetAllShortcutsInDbAsync()
    {
        try
        {
            await InitializeAsync();
            await using var connection = new SqliteConnection(_connectionString);
            await connection.OpenAsync();

            const string sql = "UPDATE UserShortcuts SET KeyGesture = DefaultGesture, UpdatedAt = datetime('now');";
            await connection.ExecuteAsync(sql);

            lock (_shortcutsLock)
            {
                _cachedShortcuts = null;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] ResetAllShortcutsInDbAsync error: {ex.Message}");
        }
    }

    public async Task<PanelCustomizationRecord> GetPanelCustomizationAsync()
    {
        var record = new PanelCustomizationRecord();
        try
        {
            record.EditorBackground = await GetSettingAsync("EditorBackground", "Default");
            record.EditorForeground = await GetSettingAsync("EditorForeground", "Default");
            record.SidebarBackground = await GetSettingAsync("SidebarBackground", "Default");
            record.TableBackground = await GetSettingAsync("TableBackground", "Default");

            string fontSizeStr = await GetSettingAsync("EditorFontSize", "13");
            if (double.TryParse(fontSizeStr, System.Globalization.CultureInfo.InvariantCulture, out double fs))
            {
                record.EditorFontSize = fs;
            }

            string lineNumsStr = await GetSettingAsync("EditorShowLineNumbers", "true");
            record.EditorShowLineNumbers = lineNumsStr != "false" && lineNumsStr != "0";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetPanelCustomizationAsync error: {ex.Message}");
        }
        return record;
    }

    public async Task SavePanelCustomizationAsync(PanelCustomizationRecord record)
    {
        try
        {
            var dict = new Dictionary<string, string>
            {
                ["EditorBackground"] = record.EditorBackground,
                ["EditorForeground"] = record.EditorForeground,
                ["SidebarBackground"] = record.SidebarBackground,
                ["TableBackground"] = record.TableBackground,
                ["EditorFontSize"] = record.EditorFontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["EditorShowLineNumbers"] = record.EditorShowLineNumbers ? "true" : "false"
            };
            await SaveSettingsBatchAsync(dict);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SavePanelCustomizationAsync error: {ex.Message}");
        }
    }

    public async Task<TableCustomizationRecord> GetTableCustomizationAsync()
    {
        var record = new TableCustomizationRecord();
        try
        {
            string rhStr = await GetSettingAsync("TableRowHeight", "25");
            if (double.TryParse(rhStr, System.Globalization.CultureInfo.InvariantCulture, out double rh))
            {
                record.RowHeight = rh;
            }

            string hhStr = await GetSettingAsync("TableHeaderHeight", "26");
            if (double.TryParse(hhStr, System.Globalization.CultureInfo.InvariantCulture, out double hh))
            {
                record.HeaderHeight = hh;
            }

            string gwStr = await GetSettingAsync("TableGutterWidth", "56");
            if (double.TryParse(gwStr, System.Globalization.CultureInfo.InvariantCulture, out double gw))
            {
                record.GutterWidth = gw;
            }

            string fsStr = await GetSettingAsync("TableCellFontSize", "11");
            if (double.TryParse(fsStr, System.Globalization.CultureInfo.InvariantCulture, out double fs))
            {
                record.FontSize = fs;
            }

            record.TableBackground = await GetSettingAsync("TableBackground", "Default");
            record.AlternatingRowColor = await GetSettingAsync("TableAlternatingRowColor", "Default");
            record.CellTextColor = await GetSettingAsync("TableCellTextColor", "Default");
            record.HeaderBackground = await GetSettingAsync("TableHeaderBackground", "Default");
            record.HeaderTextColor = await GetSettingAsync("TableHeaderTextColor", "Default");
            record.GutterBackground = await GetSettingAsync("TableGutterBackground", "Default");
            record.GutterTextColor = await GetSettingAsync("TableGutterTextColor", "Default");
            record.GridLinesColor = await GetSettingAsync("TableGridLinesColor", "Default");
            record.SelectionColor = await GetSettingAsync("TableSelectionColor", "Default");

            string gridLinesStr = await GetSettingAsync("TableShowGridLines", "true");
            record.ShowGridLines = gridLinesStr != "false" && gridLinesStr != "0";

            string altRowsStr = await GetSettingAsync("TableShowAlternatingRowColors", "true");
            record.ShowAlternatingRowColors = altRowsStr != "false" && altRowsStr != "0";
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] GetTableCustomizationAsync error: {ex.Message}");
        }
        return record;
    }

    public async Task SaveTableCustomizationAsync(TableCustomizationRecord record)
    {
        try
        {
            var dict = new Dictionary<string, string>
            {
                ["TableRowHeight"] = record.RowHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["TableHeaderHeight"] = record.HeaderHeight.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["TableGutterWidth"] = record.GutterWidth.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["TableCellFontSize"] = record.FontSize.ToString(System.Globalization.CultureInfo.InvariantCulture),
                ["TableBackground"] = record.TableBackground,
                ["TableAlternatingRowColor"] = record.AlternatingRowColor,
                ["TableCellTextColor"] = record.CellTextColor,
                ["TableHeaderBackground"] = record.HeaderBackground,
                ["TableHeaderTextColor"] = record.HeaderTextColor,
                ["TableGutterBackground"] = record.GutterBackground,
                ["TableGutterTextColor"] = record.GutterTextColor,
                ["TableGridLinesColor"] = record.GridLinesColor,
                ["TableSelectionColor"] = record.SelectionColor,
                ["TableShowGridLines"] = record.ShowGridLines ? "true" : "false",
                ["TableShowAlternatingRowColors"] = record.ShowAlternatingRowColors ? "true" : "false"
            };
            await SaveSettingsBatchAsync(dict);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseStorageService] SaveTableCustomizationAsync error: {ex.Message}");
        }
    }
}
