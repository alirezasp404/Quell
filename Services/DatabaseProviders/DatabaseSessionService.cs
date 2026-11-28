using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Quell.ViewModels;

namespace Quell.Services.DatabaseProviders;

public record QueryExecutionResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public string ExecutionTime { get; init; } = "0 ms";
    public int RowCount { get; init; }
    public List<string> ColumnNames { get; init; } = new();
    public List<QueryResultRow> Rows { get; init; } = new();
    public Exception? Exception { get; init; }
}

public record ForeignKeyRelation
{
    public string ColumnName { get; init; } = string.Empty;
    public string TargetTable { get; init; } = string.Empty;
    public string TargetColumn { get; init; } = "Id";
    public string ConstraintName { get; init; } = string.Empty;
}

public class OpenDatabaseSession
{
    public string Key { get; init; } = string.Empty;
    public IDbConnection Connection { get; init; } = null!;
    public DatabaseConnectionConfig Config { get; set; } = null!;
    public IDatabaseProvider Provider { get; init; } = null!;
    public string? ServerVersion { get; init; }
    public DateTime ConnectedAt { get; init; } = DateTime.Now;

    public string DisplayName => Config.DisplayName;
    public DatabaseType DatabaseType => Config.DatabaseType;
}

public class DatabaseSessionService
{
    private static DatabaseSessionService? _instance;
    public static DatabaseSessionService Instance => _instance ??= new DatabaseSessionService();

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Dictionary<string, OpenDatabaseSession> _openSessions = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TableMetadata>> _cachedTables = new(StringComparer.OrdinalIgnoreCase);
    private readonly Dictionary<string, List<TableMetadata>> _cachedRoutines = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<TableMetadata> GetActiveCachedTables()
    {
        if (ActiveSession == null) return Array.Empty<TableMetadata>();
        lock (_cachedTables)
        {
            if (_cachedTables.TryGetValue(ActiveSession.Key, out var list))
            {
                return list;
            }
        }
        return Array.Empty<TableMetadata>();
    }

    public IReadOnlyList<TableMetadata> GetActiveCachedRoutines()
    {
        if (ActiveSession == null) return Array.Empty<TableMetadata>();
        lock (_cachedRoutines)
        {
            if (_cachedRoutines.TryGetValue(ActiveSession.Key, out var list))
            {
                return list;
            }
        }
        return Array.Empty<TableMetadata>();
    }

    public OpenDatabaseSession? ActiveSession { get; private set; }

    public bool IsConnected => ActiveSession != null && ActiveSession.Connection.State == ConnectionState.Open;
    public DatabaseConnectionConfig? ActiveConfig => ActiveSession?.Config;
    public IDatabaseProvider? ActiveProvider => ActiveSession?.Provider;
    public string? ServerVersion => ActiveSession?.ServerVersion;

    public IReadOnlyList<OpenDatabaseSession> OpenSessions
    {
        get
        {
            lock (_openSessions)
            {
                return _openSessions.Values.ToList();
            }
        }
    }

    public string ConnectionDisplayText
    {
        get
        {
            if (!IsConnected || ActiveConfig == null)
            {
                return "Disconnected";
            }

            if (ActiveProvider?.IsFileBased == true)
            {
                string name = System.IO.Path.GetFileName(ActiveConfig.FilePath);
                if (string.IsNullOrEmpty(name)) name = ":memory:";
                return name;
            }

            return !string.IsNullOrWhiteSpace(ActiveConfig.Database) ? ActiveConfig.Database : ActiveConfig.Server;
        }
    }

    public string TargetDisplayName
    {
        get
        {
            if (ActiveConfig == null)
            {
                return "(No Connection)";
            }

            if (ActiveProvider?.IsFileBased == true)
            {
                string name = System.IO.Path.GetFileName(ActiveConfig.FilePath);
                return string.IsNullOrEmpty(name) ? ":memory:" : name;
            }

            return !string.IsNullOrWhiteSpace(ActiveConfig.Database) ? ActiveConfig.Database : ActiveConfig.Server;
        }
    }

    public event Action? ConnectionChanged;

    private void NotifyConnectionChanged()
    {
        void InvokeHandlers()
        {
            try
            {
                ConnectionChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseSessionService] ConnectionChanged error: {ex.Message}");
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            InvokeHandlers();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(InvokeHandlers);
        }
    }

    public bool IsSessionOpen(string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey)) return false;
        lock (_openSessions)
        {
            return _openSessions.TryGetValue(sessionKey, out var s) && s.Connection.State == ConnectionState.Open;
        }
    }

    public bool SetActiveSession(string sessionKey)
    {
        if (string.IsNullOrEmpty(sessionKey)) return false;
        bool changed = false;
        lock (_openSessions)
        {
            if (_openSessions.TryGetValue(sessionKey, out var session) && session.Connection.State == ConnectionState.Open)
            {
                if (ActiveSession == session) return true;
                ActiveSession = session;
                changed = true;
            }
        }

        if (changed)
        {
            lock (_cachedTables)
            {
                if (ActiveSession != null && _cachedTables.TryGetValue(ActiveSession.Key, out var cached))
                {
                    SqlKeywordsService.Instance.UpdateActiveTables(ActiveSession.Config.Database, cached);
                }
            }
            NotifyConnectionChanged();
            return true;
        }
        return false;
    }

    public async Task<ConnectionTestResult> ConnectAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        bool shouldNotify = false;
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            string key = config.ConnectionKey;

            // Check if already open and active
            lock (_openSessions)
            {
                if (_openSessions.TryGetValue(key, out var existing) && existing.Connection.State == ConnectionState.Open)
                {
                    if (ActiveSession != existing)
                    {
                        ActiveSession = existing;
                        shouldNotify = true;
                    }
                    return new ConnectionTestResult
                    {
                        Success = true,
                        ServerVersion = existing.ServerVersion,
                        Message = $"Session '{existing.DisplayName}' is already active."
                    };
                }
            }

            var provider = DatabaseProviderRegistry.Instance.GetProvider(config.DatabaseType);
            var testResult = await provider.TestConnectionAsync(config, cancellationToken).ConfigureAwait(false);

            if (!testResult.Success)
            {
                return testResult;
            }

            var conn = await provider.CreateOpenConnectionAsync(config, cancellationToken).ConfigureAwait(false);
            var session = new OpenDatabaseSession
            {
                Key = key,
                Connection = conn,
                Config = config,
                Provider = provider,
                ServerVersion = testResult.ServerVersion
            };

            lock (_openSessions)
            {
                _openSessions[key] = session;
            }

            ActiveSession = session;
            shouldNotify = true;
            return testResult;
        }
        catch (Exception ex)
        {
            return new ConnectionTestResult
            {
                Success = false,
                Message = $"Failed to establish connection: {ex.Message}",
                Exception = ex
            };
        }
        finally
        {
            _connectionLock.Release();
            if (shouldNotify)
            {
                NotifyConnectionChanged();
            }
        }
    }

    public async Task DisconnectAsync(string? sessionKey = null)
    {
        bool shouldNotify = false;
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (string.IsNullOrEmpty(sessionKey))
            {
                sessionKey = ActiveSession?.Key;
            }

            if (string.IsNullOrEmpty(sessionKey))
            {
                return;
            }

            OpenDatabaseSession? sessionToClose = null;
            lock (_openSessions)
            {
                if (_openSessions.TryGetValue(sessionKey, out sessionToClose))
                {
                    _openSessions.Remove(sessionKey);
                    shouldNotify = true;
                }
            }

            if (sessionToClose != null)
            {
                try
                {
                    if (sessionToClose.Connection.State != ConnectionState.Closed)
                    {
                        sessionToClose.Connection.Close();
                    }
                    sessionToClose.Connection.Dispose();
                }
                catch
                {
                    // Ignore disposal errors
                }
            }

            if (ActiveSession?.Key == sessionKey)
            {
                lock (_openSessions)
                {
                    ActiveSession = _openSessions.Values.FirstOrDefault();
                }
                shouldNotify = true;
            }
        }
        finally
        {
            _connectionLock.Release();
            if (shouldNotify)
            {
                NotifyConnectionChanged();
            }
        }
    }

    public async Task DisconnectAllAsync()
    {
        await _connectionLock.WaitAsync().ConfigureAwait(false);
        try
        {
            List<OpenDatabaseSession> sessions;
            lock (_openSessions)
            {
                sessions = _openSessions.Values.ToList();
                _openSessions.Clear();
                ActiveSession = null;
            }

            foreach (var s in sessions)
            {
                try
                {
                    if (s.Connection.State != ConnectionState.Closed)
                    {
                        s.Connection.Close();
                    }
                    s.Connection.Dispose();
                }
                catch { }
            }
        }
        finally
        {
            _connectionLock.Release();
            NotifyConnectionChanged();
        }
    }

    private OpenDatabaseSession? ResolveSession(string? sessionKey)
    {
        if (!string.IsNullOrEmpty(sessionKey))
        {
            lock (_openSessions)
            {
                if (_openSessions.TryGetValue(sessionKey, out var s))
                    return s;
            }
        }
        lock (_openSessions)
        {
            return ActiveSession ?? _openSessions.Values.FirstOrDefault(s => s.Connection.State == ConnectionState.Open);
        }
    }

    internal async Task<T> ExecuteWithSessionAsync<T>(
        string? sessionKey,
        Func<OpenDatabaseSession, CancellationToken, Task<T>> action,
        T fallback,
        CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return fallback;
            }
            return await action(session, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<List<TableMetadata>> GetTablesAsync(string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<TableMetadata>();
            }

            string sql = session.Config.DatabaseType switch
            {
                DatabaseType.MSSQL => @"
                    SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE 
                    FROM INFORMATION_SCHEMA.TABLES 
                    ORDER BY TABLE_SCHEMA, TABLE_NAME;",
                DatabaseType.PostgreSQL => @"
                    SELECT table_schema, table_name, table_type 
                    FROM information_schema.tables 
                    WHERE table_schema NOT IN ('pg_catalog', 'information_schema') 
                    ORDER BY table_schema, table_name;",
                DatabaseType.MySQL => @"
                    SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE 
                    FROM information_schema.tables 
                    WHERE TABLE_SCHEMA = DATABASE() 
                    ORDER BY TABLE_NAME;",
                _ => @"
                    SELECT 'main' AS SchemaName, name AS TableName, type AS TableType 
                    FROM sqlite_master 
                    WHERE type IN ('table', 'view') AND name NOT LIKE 'sqlite_%' 
                    ORDER BY name;"
            };

            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = sql;

            var list = new List<TableMetadata>();
            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(new TableMetadata
                    {
                        SchemaName = reader[0]?.ToString() ?? "dbo",
                        TableName = reader[1]?.ToString() ?? string.Empty,
                        TableType = reader[2]?.ToString() ?? "TABLE"
                    });
                }
            }
            else
            {
                using var reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    list.Add(new TableMetadata
                    {
                        SchemaName = reader[0]?.ToString() ?? "dbo",
                        TableName = reader[1]?.ToString() ?? string.Empty,
                        TableType = reader[2]?.ToString() ?? "TABLE"
                    });
                }
            }

            // Try fast metadata-only approximate row counts (best-effort, zero full table scans)
            await TryPopulateRowCountsAsync(session, list, cancellationToken).ConfigureAwait(false);

            lock (_cachedTables)
            {
                _cachedTables[session.Key] = list;
            }

            if (ActiveSession?.Key == session.Key)
            {
                SqlKeywordsService.Instance.UpdateActiveTables(session.Config.Database, list);
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseSessionService] GetTablesAsync error: {ex.Message}");
            return new List<TableMetadata>();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static async Task TryPopulateRowCountsAsync(OpenDatabaseSession session, List<TableMetadata> tables, CancellationToken cancellationToken)
    {
        if (tables.Count == 0 || session.Config.DatabaseType == DatabaseType.SQLite) return;

        try
        {
            string? countSql = session.Config.DatabaseType switch
            {
                DatabaseType.MSSQL => @"
                    SELECT s.name AS SchemaName, t.name AS TableName, SUM(p.rows) AS ApproxRows
                    FROM sys.tables t
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    INNER JOIN sys.partitions p ON t.object_id = p.object_id AND p.index_id IN (0, 1)
                    GROUP BY s.name, t.name;",
                DatabaseType.PostgreSQL => @"
                    SELECT n.nspname AS SchemaName, c.relname AS TableName, GREATEST(c.reltuples::bigint, 0) AS ApproxRows
                    FROM pg_class c
                    JOIN pg_namespace n ON n.oid = c.relnamespace
                    WHERE c.relkind IN ('r', 'p') AND n.nspname NOT IN ('pg_catalog', 'information_schema');",
                DatabaseType.MySQL => @"
                    SELECT TABLE_SCHEMA, TABLE_NAME, COALESCE(TABLE_ROWS, 0)
                    FROM INFORMATION_SCHEMA.TABLES
                    WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE';",
                _ => null
            };

            if (string.IsNullOrEmpty(countSql)) return;

            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = countSql;
            cmd.CommandTimeout = 5;

            var counts = new Dictionary<string, long>(StringComparer.OrdinalIgnoreCase);
            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string sch = reader[0]?.ToString() ?? "dbo";
                    string tbl = reader[1]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrEmpty(tbl) && long.TryParse(reader[2]?.ToString(), out long rc))
                    {
                        counts[$"{sch}.{tbl}"] = rc;
                        counts[tbl] = rc;
                    }
                }
            }

            foreach (var t in tables)
            {
                if (!t.IsTable) continue;
                if (counts.TryGetValue($"{t.SchemaName}.{t.TableName}", out long c) ||
                    counts.TryGetValue(t.TableName, out c))
                {
                    t.RowCount = c;
                }
            }
        }
        catch
        {
            // Permissions or dialect differences are non-fatal for row counts
        }
    }

    public async Task<List<TableMetadata>> GetRoutinesAsync(string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<TableMetadata>();
            }

            if (session.Config.DatabaseType == DatabaseType.SQLite)
            {
                return new List<TableMetadata>();
            }

            string sql = session.Config.DatabaseType switch
            {
                DatabaseType.MSSQL => @"
                    SELECT ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_TYPE
                    FROM INFORMATION_SCHEMA.ROUTINES
                    WHERE ROUTINE_TYPE IN ('PROCEDURE', 'FUNCTION')
                    ORDER BY ROUTINE_SCHEMA, ROUTINE_NAME;",
                DatabaseType.PostgreSQL => @"
                    SELECT routine_schema, routine_name, routine_type
                    FROM information_schema.routines
                    WHERE routine_schema NOT IN ('pg_catalog', 'information_schema')
                    ORDER BY routine_schema, routine_name;",
                DatabaseType.MySQL => @"
                    SELECT ROUTINE_SCHEMA, ROUTINE_NAME, ROUTINE_TYPE
                    FROM INFORMATION_SCHEMA.ROUTINES
                    WHERE ROUTINE_SCHEMA = DATABASE()
                    ORDER BY ROUTINE_NAME;",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(sql)) return new List<TableMetadata>();

            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = sql;
            var list = new List<TableMetadata>();
            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(new TableMetadata
                    {
                        SchemaName = reader[0]?.ToString() ?? "dbo",
                        TableName = reader[1]?.ToString() ?? string.Empty,
                        TableType = reader[2]?.ToString() ?? "PROCEDURE"
                    });
                }
            }

            lock (_cachedRoutines)
            {
                _cachedRoutines[session.Key] = list;
            }
            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseSessionService] GetRoutinesAsync error: {ex.Message}");
            return new List<TableMetadata>();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<bool> SwitchActiveDatabaseCatalogAsync(string databaseName, string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(databaseName)) return false;

        var session = ResolveSession(sessionKey);
        if (session == null || session.Connection.State != ConnectionState.Open) return false;

        if (session.Config.DatabaseType == DatabaseType.MSSQL || session.Config.DatabaseType == DatabaseType.MySQL)
        {
            string formattedDb = QueryResultTableViewModel.FormatIdentifier(databaseName, session.Config.DatabaseType);
            var res = await ExecuteQueryAsync($"USE {formattedDb};", session.Key, cancellationToken).ConfigureAwait(false);
            if (res.Success)
            {
                session.Config = session.Config with { Database = databaseName };
                NotifyConnectionChanged();
                return true;
            }
            return false;
        }

        if (session.Config.DatabaseType == DatabaseType.PostgreSQL)
        {
            var newConfig = session.Config with { Id = 0, Database = databaseName };
            var connRes = await ConnectAsync(newConfig, cancellationToken).ConfigureAwait(false);
            return connRes.Success;
        }

        return false;
    }

    public async Task<List<string>> GetServerDatabasesAsync(string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<string>();
            }

            if (session.Config.DatabaseType == DatabaseType.SQLite)
            {
                return new List<string> { session.DisplayName };
            }

            string sql = session.Config.DatabaseType switch
            {
                DatabaseType.MSSQL => "SELECT name FROM sys.databases WHERE state = 0 ORDER BY CASE WHEN database_id > 4 THEN 0 ELSE 1 END, name;",
                DatabaseType.PostgreSQL => "SELECT datname FROM pg_database WHERE datistemplate = false ORDER BY datname;",
                DatabaseType.MySQL => "SHOW DATABASES;",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(sql)) return new List<string>();

            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 5;
            var dbs = new List<string>();
            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string name = reader[0]?.ToString() ?? string.Empty;
                    if (!string.IsNullOrWhiteSpace(name))
                        dbs.Add(name);
                }
            }
            return dbs;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseSessionService] GetServerDatabasesAsync error: {ex.Message}");
            return new List<string>();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private static (string? Schema, string Table) ParseTableName(string tableName)
    {
        string clean = tableName.Trim().Trim('[', ']', '"', '`');
        int dotIdx = clean.IndexOf('.');
        if (dotIdx > 0 && dotIdx < clean.Length - 1)
        {
            string schema = clean.Substring(0, dotIdx).Trim('[', ']', '"', '`');
            string table = clean.Substring(dotIdx + 1).Trim('[', ']', '"', '`');
            return (string.IsNullOrWhiteSpace(schema) ? null : schema, table);
        }
        return (null, clean);
    }

    private static string EscapeSqlLiteral(string value) => value.Replace("'", "''");

    public async Task<List<string>> GetPrimaryKeyColumnsAsync(string tableName, string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return new List<string>();

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<string>();
            }

            var (schema, cleanTable) = ParseTableName(tableName);
            bool hasSchema = !string.IsNullOrWhiteSpace(schema);

            string sql = session.Config.DatabaseType switch
            {
                DatabaseType.MSSQL => $@"
                    SELECT kcu.COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE kcu
                    WHERE OBJECTPROPERTY(OBJECT_ID(kcu.CONSTRAINT_SCHEMA + '.' + kcu.CONSTRAINT_NAME), 'IsPrimaryKey') = 1 
                      AND kcu.TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                      {(hasSchema ? $"AND kcu.TABLE_SCHEMA = '{EscapeSqlLiteral(schema!)}'" : "")}
                    ORDER BY kcu.ORDINAL_POSITION;",
                DatabaseType.PostgreSQL => $@"
                    SELECT kcu.column_name 
                    FROM information_schema.table_constraints tc 
                    JOIN information_schema.key_column_usage kcu 
                      ON tc.constraint_name = kcu.constraint_name 
                     AND tc.table_schema = kcu.table_schema
                    WHERE tc.constraint_type = 'PRIMARY KEY' 
                      AND LOWER(tc.table_name) = '{EscapeSqlLiteral(cleanTable.ToLowerInvariant())}'
                      {(hasSchema ? $"AND LOWER(tc.table_schema) = '{EscapeSqlLiteral(schema!.ToLowerInvariant())}'" : "AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')")}
                    ORDER BY kcu.ordinal_position;",
                DatabaseType.MySQL => $@"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE CONSTRAINT_NAME = 'PRIMARY' 
                      AND TABLE_SCHEMA = {(hasSchema ? $"'{EscapeSqlLiteral(schema!)}'" : "DATABASE()")}
                      AND TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                    ORDER BY ORDINAL_POSITION;",
                _ => hasSchema && !schema!.Equals("main", StringComparison.OrdinalIgnoreCase)
                    ? $@"PRAGMA ""{schema.Replace("\"", "\"\"")}"".table_info('{EscapeSqlLiteral(cleanTable)}');"
                    : $@"PRAGMA table_info('{EscapeSqlLiteral(cleanTable)}');"
            };

            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = sql;
            var pks = new List<string>();

            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                if (session.Config.DatabaseType == DatabaseType.SQLite)
                {
                    var pkEntries = new List<(int Order, string Name)>();
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        int pkIndex = reader.GetInt32(5);
                        if (pkIndex > 0)
                        {
                            pkEntries.Add((pkIndex, reader.GetString(1)));
                        }
                    }
                    pks.AddRange(pkEntries.OrderBy(e => e.Order).Select(e => e.Name));
                }
                else
                {
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        pks.Add(reader.GetString(0));
                    }
                }
            }
            else
            {
                using var reader = cmd.ExecuteReader();
                if (session.Config.DatabaseType == DatabaseType.SQLite)
                {
                    var pkEntries = new List<(int Order, string Name)>();
                    while (reader.Read())
                    {
                        int pkIndex = reader.GetInt32(5);
                        if (pkIndex > 0)
                        {
                            pkEntries.Add((pkIndex, reader.GetString(1)));
                        }
                    }
                    pks.AddRange(pkEntries.OrderBy(e => e.Order).Select(e => e.Name));
                }
                else
                {
                    while (reader.Read())
                    {
                        pks.Add(reader.GetString(0));
                    }
                }
            }

            return pks;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseSessionService] GetPrimaryKeyColumnsAsync error: {ex.Message}");
            return new List<string>();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<List<(string ColumnName, string DataType)>> GetColumnsAsync(string tableName, string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return new List<(string, string)>();

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<(string, string)>();
            }

            var (schema, cleanTable) = ParseTableName(tableName);
            bool hasSchema = !string.IsNullOrWhiteSpace(schema);

            var list = new List<(string, string)>();

            if (session.Config.DatabaseType == DatabaseType.SQLite)
            {
                using var cmd = session.Connection.CreateCommand();
                cmd.CommandText = hasSchema && !schema!.Equals("main", StringComparison.OrdinalIgnoreCase)
                    ? $"PRAGMA \"{schema.Replace("\"", "\"\"")}\".table_info('{EscapeSqlLiteral(cleanTable)}');"
                    : $"PRAGMA table_info('{EscapeSqlLiteral(cleanTable)}');";
                if (cmd is DbCommand dbCmd)
                {
                    await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        string colName = reader.GetString(1);
                        string colType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2);
                        list.Add((colName, string.IsNullOrWhiteSpace(colType) ? "TEXT" : colType));
                    }
                }
            }
            else
            {
                string sql = session.Config.DatabaseType switch
                {
                    DatabaseType.PostgreSQL => $@"
                        SELECT column_name, data_type 
                        FROM information_schema.columns 
                        WHERE LOWER(table_name) = '{EscapeSqlLiteral(cleanTable.ToLowerInvariant())}'
                          {(hasSchema ? $"AND LOWER(table_schema) = '{EscapeSqlLiteral(schema!.ToLowerInvariant())}'" : "AND table_schema NOT IN ('pg_catalog', 'information_schema')")}
                        ORDER BY ordinal_position;",
                    DatabaseType.MySQL => $@"
                        SELECT COLUMN_NAME, DATA_TYPE 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_SCHEMA = {(hasSchema ? $"'{EscapeSqlLiteral(schema!)}'" : "DATABASE()")}
                          AND TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                        ORDER BY ORDINAL_POSITION;",
                    _ => $@"
                        SELECT COLUMN_NAME, DATA_TYPE 
                        FROM INFORMATION_SCHEMA.COLUMNS 
                        WHERE TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                          {(hasSchema ? $"AND TABLE_SCHEMA = '{EscapeSqlLiteral(schema!)}'" : "")}
                        ORDER BY ORDINAL_POSITION;"
                };

                using var cmd = session.Connection.CreateCommand();
                cmd.CommandText = sql;
                if (cmd is DbCommand dbCmd)
                {
                    await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        string colName = reader.GetString(0);
                        string colType = reader.IsDBNull(1) ? string.Empty : reader.GetString(1);
                        list.Add((colName, colType));
                    }
                }
            }

            return list;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseSessionService] GetColumnsAsync error: {ex.Message}");
            return new List<(string, string)>();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<List<ForeignKeyRelation>> GetForeignKeyRelationsAsync(string tableName, string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return new List<ForeignKeyRelation>();

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<ForeignKeyRelation>();
            }

            var (schema, cleanTable) = ParseTableName(tableName);
            bool hasSchema = !string.IsNullOrWhiteSpace(schema);

            var results = new List<ForeignKeyRelation>();

            if (session.Config.DatabaseType == DatabaseType.SQLite)
            {
                var rawEntries = new List<(string ColumnName, string TargetTable, string? TargetColumn)>();
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = hasSchema && !schema!.Equals("main", StringComparison.OrdinalIgnoreCase)
                        ? $"PRAGMA \"{schema.Replace("\"", "\"\"")}\".foreign_key_list('{EscapeSqlLiteral(cleanTable)}');"
                        : $"PRAGMA foreign_key_list('{EscapeSqlLiteral(cleanTable)}');";
                    if (cmd is DbCommand dbCmd)
                    {
                        await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            string targetTable = reader.GetString(2);
                            string fromCol = reader.GetString(3);
                            string? toCol = reader.IsDBNull(4) ? null : reader.GetString(4);
                            rawEntries.Add((fromCol, targetTable, toCol));
                        }
                    }
                }

                foreach (var entry in rawEntries)
                {
                    string resolvedTargetCol = entry.TargetColumn ?? string.Empty;
                    if (string.IsNullOrWhiteSpace(resolvedTargetCol))
                    {
                        // SQLite omits 'to' column when FK uses shorthand REFERENCES TargetTable; resolve its actual PK column from metadata
                        using var pkCmd = session.Connection.CreateCommand();
                        pkCmd.CommandText = $"PRAGMA table_info('{EscapeSqlLiteral(entry.TargetTable)}');";
                        if (pkCmd is DbCommand dbPkCmd)
                        {
                            await using var pkReader = await dbPkCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                            while (await pkReader.ReadAsync(cancellationToken).ConfigureAwait(false))
                            {
                                if (pkReader.GetInt32(5) > 0)
                                {
                                    resolvedTargetCol = pkReader.GetString(1);
                                    break;
                                }
                            }
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(resolvedTargetCol))
                    {
                        results.Add(new ForeignKeyRelation
                        {
                            ColumnName = entry.ColumnName,
                            TargetTable = entry.TargetTable,
                            TargetColumn = resolvedTargetCol
                        });
                    }
                }
            }
            else if (session.Config.DatabaseType == DatabaseType.MSSQL)
            {
                using var cmd = session.Connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT 
                        col.name AS ColumnName,
                        CASE WHEN ref_sch.name = 'dbo' THEN ref_tab.name ELSE ref_sch.name + '.' + ref_tab.name END AS TargetTable,
                        ref_col.name AS TargetColumn,
                        fk.name AS ConstraintName
                    FROM sys.foreign_keys fk
                    INNER JOIN sys.foreign_key_columns fkc ON fk.object_id = fkc.constraint_object_id
                    INNER JOIN sys.tables tab ON tab.object_id = fk.parent_object_id
                    INNER JOIN sys.schemas sch ON sch.schema_id = tab.schema_id
                    INNER JOIN sys.columns col ON col.object_id = fkc.parent_object_id AND col.column_id = fkc.parent_column_id
                    INNER JOIN sys.tables ref_tab ON ref_tab.object_id = fk.referenced_object_id
                    INNER JOIN sys.schemas ref_sch ON ref_sch.schema_id = ref_tab.schema_id
                    INNER JOIN sys.columns ref_col ON ref_col.object_id = fkc.referenced_object_id AND ref_col.column_id = fkc.referenced_column_id
                    WHERE tab.name = '{EscapeSqlLiteral(cleanTable)}'
                      {(hasSchema ? $"AND sch.name = '{EscapeSqlLiteral(schema!)}'" : "")};";
                if (cmd is DbCommand dbCmd)
                {
                    await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        results.Add(new ForeignKeyRelation
                        {
                            ColumnName = reader.GetString(0),
                            TargetTable = reader.GetString(1),
                            TargetColumn = reader.GetString(2),
                            ConstraintName = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }
            else if (session.Config.DatabaseType == DatabaseType.PostgreSQL)
            {
                using var cmd = session.Connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT
                        kcu.column_name AS ColumnName,
                        CASE WHEN ccu.table_schema = 'public' THEN ccu.table_name ELSE ccu.table_schema || '.' || ccu.table_name END AS TargetTable,
                        ccu.column_name AS TargetColumn,
                        tc.constraint_name AS ConstraintName
                    FROM information_schema.table_constraints AS tc
                    JOIN information_schema.key_column_usage AS kcu
                      ON tc.constraint_name = kcu.constraint_name
                      AND tc.table_schema = kcu.table_schema
                    JOIN information_schema.constraint_column_usage AS ccu
                      ON ccu.constraint_name = tc.constraint_name
                      AND ccu.constraint_schema = tc.constraint_schema
                    WHERE tc.constraint_type = 'FOREIGN KEY'
                      AND LOWER(tc.table_name) = '{EscapeSqlLiteral(cleanTable.ToLowerInvariant())}'
                      {(hasSchema ? $"AND LOWER(tc.table_schema) = '{EscapeSqlLiteral(schema!.ToLowerInvariant())}'" : "AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')")};";
                if (cmd is DbCommand dbCmd)
                {
                    await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        results.Add(new ForeignKeyRelation
                        {
                            ColumnName = reader.GetString(0),
                            TargetTable = reader.GetString(1),
                            TargetColumn = reader.GetString(2),
                            ConstraintName = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }
            else if (session.Config.DatabaseType == DatabaseType.MySQL)
            {
                using var cmd = session.Connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT 
                        COLUMN_NAME AS ColumnName,
                        REFERENCED_TABLE_NAME AS TargetTable,
                        REFERENCED_COLUMN_NAME AS TargetColumn,
                        CONSTRAINT_NAME AS ConstraintName
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                    WHERE TABLE_SCHEMA = {(hasSchema ? $"'{EscapeSqlLiteral(schema!)}'" : "DATABASE()")}
                      AND REFERENCED_TABLE_NAME IS NOT NULL
                      AND TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}';";
                if (cmd is DbCommand dbCmd)
                {
                    await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        results.Add(new ForeignKeyRelation
                        {
                            ColumnName = reader.GetString(0),
                            TargetTable = reader.GetString(1),
                            TargetColumn = reader.GetString(2),
                            ConstraintName = reader.IsDBNull(3) ? "" : reader.GetString(3)
                        });
                    }
                }
            }

            return results;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseSessionService] GetForeignKeyRelationsAsync error: {ex.Message}");
            return new List<ForeignKeyRelation>();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    public async Task<QueryExecutionResult> ExecuteQueryAsync(string sql, string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(sql))
        {
            return new QueryExecutionResult
            {
                Success = false,
                Message = "Query text is empty."
            };
        }

        await _connectionLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new QueryExecutionResult
                {
                    Success = false,
                    Message = "No active database connection. Please connect to a database first."
                };
            }

            var sw = Stopwatch.StartNew();
            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = sql;
            cmd.CommandTimeout = 60;

            using var cancelReg = cancellationToken.Register(() =>
            {
                try { cmd.Cancel(); } catch { }
            });

            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                var rows = new List<QueryResultRow>();
                int rowIndex = 1;

                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    var row = new QueryResultRow { RowIndex = rowIndex++ };
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetName(i);
                        object val = reader.GetValue(i);
                        string strVal = val == DBNull.Value || val == null ? "NULL" : val.ToString() ?? "";
                        row.SetRawValue(colName, strVal);
                    }
                    rows.Add(row);
                }

                sw.Stop();
                string execMsg = reader.FieldCount == 0
                    ? (reader.RecordsAffected >= 0 ? $"Command executed successfully ({reader.RecordsAffected} row(s) affected)." : "Command executed successfully.")
                    : $"Query executed successfully ({rows.Count} rows).";

                return new QueryExecutionResult
                {
                    Success = true,
                    Message = execMsg,
                    ExecutionTime = $"{sw.ElapsedMilliseconds} ms",
                    RowCount = reader.FieldCount == 0 ? Math.Max(0, reader.RecordsAffected) : rows.Count,
                    ColumnNames = columns,
                    Rows = rows
                };
            }
            else
            {
                using var reader = cmd.ExecuteReader();
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                var rows = new List<QueryResultRow>();
                int rowIndex = 1;
                while (reader.Read())
                {
                    var row = new QueryResultRow { RowIndex = rowIndex++ };
                    for (int i = 0; i < reader.FieldCount; i++)
                    {
                        string colName = reader.GetName(i);
                        object val = reader.GetValue(i);
                        string strVal = val == DBNull.Value || val == null ? "NULL" : val.ToString() ?? "";
                        row.SetRawValue(colName, strVal);
                    }
                    rows.Add(row);
                }

                sw.Stop();
                string execMsg = reader.FieldCount == 0
                    ? (reader.RecordsAffected >= 0 ? $"Command executed successfully ({reader.RecordsAffected} row(s) affected)." : "Command executed successfully.")
                    : $"Query executed successfully ({rows.Count} rows).";

                return new QueryExecutionResult
                {
                    Success = true,
                    Message = execMsg,
                    ExecutionTime = $"{sw.ElapsedMilliseconds} ms",
                    RowCount = reader.FieldCount == 0 ? Math.Max(0, reader.RecordsAffected) : rows.Count,
                    ColumnNames = columns,
                    Rows = rows
                };
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            if (cancellationToken.IsCancellationRequested)
            {
                throw new OperationCanceledException(cancellationToken);
            }
            return new QueryExecutionResult
            {
                Success = false,
                Message = $"Query execution failed: {ex.Message}",
                Exception = ex
            };
        }
        finally
        {
            _connectionLock.Release();
        }
    }
}
