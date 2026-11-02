using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Diagnostics;
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

public class DatabaseSessionService
{
    private static DatabaseSessionService? _instance;
    public static DatabaseSessionService Instance => _instance ??= new DatabaseSessionService();

    private readonly SemaphoreSlim _connectionLock = new(1, 1);
    private IDbConnection? _activeConnection;

    public bool IsConnected => _activeConnection != null && _activeConnection.State == ConnectionState.Open;
    public DatabaseConnectionConfig? ActiveConfig { get; private set; }
    public IDatabaseProvider? ActiveProvider { get; private set; }
    public string? ServerVersion { get; private set; }

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
                return $"[SQLite] {name}";
            }

            return $"{ActiveConfig.Database} [{ActiveConfig.Server}:{ActiveConfig.Port}]";
        }
    }

    public string TargetDisplayName
    {
        get
        {
            if (ActiveConfig == null)
            {
                return "localhost:1433 (master)";
            }

            if (ActiveProvider?.IsFileBased == true)
            {
                return System.IO.Path.GetFileName(ActiveConfig.FilePath);
            }

            return $"{ActiveConfig.Server}:{ActiveConfig.Port} ({ActiveConfig.Database})";
        }
    }

    public event Action? ConnectionChanged;

    public async Task<ConnectionTestResult> ConnectAsync(DatabaseConnectionConfig config, CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            await DisconnectInternalAsync();

            var provider = DatabaseProviderRegistry.Instance.GetProvider(config.DatabaseType);
            var testResult = await provider.TestConnectionAsync(config, cancellationToken);

            if (!testResult.Success)
            {
                return testResult;
            }

            _activeConnection = await provider.CreateOpenConnectionAsync(config, cancellationToken);
            ActiveConfig = config;
            ActiveProvider = provider;
            ServerVersion = testResult.ServerVersion;

            ConnectionChanged?.Invoke();
            return testResult;
        }
        catch (Exception ex)
        {
            await DisconnectInternalAsync();
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
        }
    }

    public async Task DisconnectAsync()
    {
        await _connectionLock.WaitAsync();
        try
        {
            await DisconnectInternalAsync();
            ConnectionChanged?.Invoke();
        }
        finally
        {
            _connectionLock.Release();
        }
    }

    private Task DisconnectInternalAsync()
    {
        if (_activeConnection != null)
        {
            try
            {
                if (_activeConnection.State != ConnectionState.Closed)
                {
                    _activeConnection.Close();
                }
                _activeConnection.Dispose();
            }
            catch
            {
                // Ignore disposal errors
            }
            _activeConnection = null;
        }

        ActiveConfig = null;
        ActiveProvider = null;
        ServerVersion = null;
        return Task.CompletedTask;
    }

    public async Task<List<TableMetadata>> GetTablesAsync(CancellationToken cancellationToken = default)
    {
        await _connectionLock.WaitAsync(cancellationToken);
        try
        {
            if (_activeConnection == null || _activeConnection.State != ConnectionState.Open)
            {
                return new List<TableMetadata>();
            }

            string sql = ActiveConfig?.DatabaseType switch
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

            using var cmd = _activeConnection.CreateCommand();
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

    public async Task<QueryExecutionResult> ExecuteQueryAsync(string sql, CancellationToken cancellationToken = default)
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
            if (_activeConnection == null || _activeConnection.State != ConnectionState.Open)
            {
                return new QueryExecutionResult
                {
                    Success = false,
                    Message = "No active database connection. Please connect to a database first."
                };
            }

            var sw = Stopwatch.StartNew();
            using var cmd = _activeConnection.CreateCommand();
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
