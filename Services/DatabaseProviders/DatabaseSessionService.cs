using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Fdb.ViewModels;

namespace Fdb.Services.DatabaseProviders;

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

public class OpenDatabaseSession
{
    public string Key { get; init; } = string.Empty;
    public IDbConnection Connection { get; init; } = null!;
    public DatabaseConnectionConfig Config { get; init; } = null!;
    public IDatabaseProvider Provider { get; init; } = null!;
    public string? ServerVersion { get; init; }
    public DateTime ConnectedAt { get; init; } = DateTime.Now;

    public string DisplayName => Config.DisplayName;
    public string DatabaseType => Config.DatabaseType;
}

public class DatabaseSessionService
{
    private static DatabaseSessionService? _instance;
    public static DatabaseSessionService Instance => _instance ??= new DatabaseSessionService();

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private readonly Dictionary<string, OpenDatabaseSession> _openSessions = new(StringComparer.OrdinalIgnoreCase);

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
        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
        {
            try
            {
                ConnectionChanged?.Invoke();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[DatabaseSessionService] ConnectionChanged error: {ex.Message}");
            }
        });
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
            NotifyConnectionChanged();
            return true;
        }
        return false;
    }

    public async Task<ConnectionTestResult> ConnectAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        bool shouldNotify = false;
        await _connectionLock.WaitAsync(cancellationToken);
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
            var testResult = await provider.TestConnectionAsync(config, cancellationToken);

            if (!testResult.Success)
            {
                return testResult;
            }

            var conn = await provider.CreateOpenConnectionAsync(config, cancellationToken);
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
        await _connectionLock.WaitAsync();
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
        await _connectionLock.WaitAsync();
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
        return ActiveSession;
    }

    public async Task<List<TableMetadata>> GetTablesAsync(string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<TableMetadata>();
            }

            string sql = session.Config.DatabaseType switch
            {
                "MSSQL" => @"
                    SELECT TABLE_SCHEMA, TABLE_NAME, TABLE_TYPE 
                    FROM INFORMATION_SCHEMA.TABLES 
                    ORDER BY TABLE_SCHEMA, TABLE_NAME;",
                "PostgreSQL" => @"
                    SELECT table_schema, table_name, table_type 
                    FROM information_schema.tables 
                    WHERE table_schema NOT IN ('pg_catalog', 'information_schema') 
                    ORDER BY table_schema, table_name;",
                "MySQL" => @"
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
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken);
                while (await reader.ReadAsync(cancellationToken))
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

    public async Task<List<string>> GetPrimaryKeyColumnsAsync(string tableName, string? sessionKey = null, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return new List<string>();

        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            var session = ResolveSession(sessionKey);
            if (session == null || session.Connection.State != ConnectionState.Open)
            {
                return new List<string>();
            }

            string cleanTable = tableName.Trim('[', ']', '"', '`');
            string schema = "dbo";
            if (cleanTable.Contains('.'))
            {
                var parts = cleanTable.Split('.');
                schema = parts[0];
                cleanTable = parts[1];
            }

            string sql = session.Config.DatabaseType switch
            {
                "MSSQL" => $@"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE OBJECTPROPERTY(OBJECT_ID(CONSTRAINT_SCHEMA + '.' + CONSTRAINT_NAME), 'IsPrimaryKey') = 1 
                      AND TABLE_NAME = '{cleanTable.Replace("'", "''")}';",
                "PostgreSQL" => $@"
                    SELECT kcu.column_name 
                    FROM information_schema.table_constraints tc 
                    JOIN information_schema.key_column_usage kcu 
                      ON tc.constraint_name = kcu.constraint_name 
                    WHERE tc.constraint_type = 'PRIMARY KEY' 
                      AND tc.table_name = '{cleanTable.ToLowerInvariant().Replace("'", "''")}';",
                "MySQL" => $@"
                    SELECT COLUMN_NAME 
                    FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE 
                    WHERE CONSTRAINT_NAME = 'PRIMARY' 
                      AND TABLE_NAME = '{cleanTable.Replace("'", "''")}';",
                _ => $@"PRAGMA table_info('{cleanTable.Replace("'", "''")}');"
            };

            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = sql;
            using var reader = cmd.ExecuteReader();
            var pks = new List<string>();

            if (session.Config.DatabaseType == "SQLite")
            {
                while (reader.Read())
                {
                    int pkIndex = reader.GetInt32(5);
                    if (pkIndex > 0)
                    {
                        pks.Add(reader.GetString(1));
                    }
                }
            }
            else
            {
                while (reader.Read())
                {
                    pks.Add(reader.GetString(0));
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

        await _connectionLock.WaitAsync(cancellationToken);
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
            cmd.CommandTimeout = 30;

            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken);
                var columns = new List<string>();
                for (int i = 0; i < reader.FieldCount; i++)
                {
                    columns.Add(reader.GetName(i));
                }

                var rows = new List<QueryResultRow>();
                int rowIndex = 1;

                while (await reader.ReadAsync(cancellationToken))
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
        }
        catch (Exception ex)
        {
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
