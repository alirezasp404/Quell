using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace Quell.Services.DatabaseProviders;

public sealed class DatabaseMetadataService
{
    private static DatabaseMetadataService? _instance;
    public static DatabaseMetadataService Instance => _instance ??= new DatabaseMetadataService();

    private readonly ConcurrentDictionary<string, ConcurrentDictionary<string, TableSchemaDetail>> _tableCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, List<TableMetadata>> _tablesListCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, List<TableMetadata>> _routinesCache =
        new(StringComparer.OrdinalIgnoreCase);

    private readonly ConcurrentDictionary<string, List<string>> _databasesCache =
        new(StringComparer.OrdinalIgnoreCase);

    private DatabaseMetadataService()
    {
        DatabaseSessionService.Instance.ConnectionChanged += OnConnectionChanged;
    }

    private void OnConnectionChanged()
    {
        var openKeys = new HashSet<string>(
            DatabaseSessionService.Instance.OpenSessions.Select(s => s.Key),
            StringComparer.OrdinalIgnoreCase);

        foreach (var key in _tableCache.Keys)
        {
            if (!openKeys.Contains(key))
            {
                InvalidateSession(key);
            }
        }
    }

    public void InvalidateTable(string tableName, string? sessionKey = null)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return;

        string? resolvedKey = ResolveCacheSessionKey(sessionKey);
        if (string.IsNullOrEmpty(resolvedKey)) return;

        if (_tableCache.TryGetValue(resolvedKey, out var sessionTables))
        {
            var (schema, cleanTable) = ParseTableName(tableName);
            sessionTables.TryRemove(NormalizeCacheKey(schema, cleanTable), out _);
            sessionTables.TryRemove(cleanTable, out _);
        }
    }

    public void InvalidateSession(string? sessionKey = null)
    {
        string? resolvedKey = ResolveCacheSessionKey(sessionKey);
        if (string.IsNullOrEmpty(resolvedKey)) return;

        _tableCache.TryRemove(resolvedKey, out _);
        _tablesListCache.TryRemove(resolvedKey, out _);
        _routinesCache.TryRemove(resolvedKey, out _);
        _databasesCache.TryRemove(resolvedKey, out _);
    }

    public void ClearAllCaches()
    {
        _tableCache.Clear();
        _tablesListCache.Clear();
        _routinesCache.Clear();
        _databasesCache.Clear();
    }

    public async Task<List<TableMetadata>> GetTablesAsync(
        string? sessionKey = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        string? cacheKey = ResolveCacheSessionKey(sessionKey);
        if (!forceRefresh && !string.IsNullOrEmpty(cacheKey) &&
            _tablesListCache.TryGetValue(cacheKey, out var cached))
        {
            return cached.ToList();
        }

        var tables = await DatabaseSessionService.Instance
            .GetTablesAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(cacheKey) && tables.Count > 0)
        {
            _tablesListCache[cacheKey] = tables;
        }

        return tables;
    }

    public async Task<List<TableMetadata>> GetRoutinesAsync(
        string? sessionKey = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        string? cacheKey = ResolveCacheSessionKey(sessionKey);
        if (!forceRefresh && !string.IsNullOrEmpty(cacheKey) &&
            _routinesCache.TryGetValue(cacheKey, out var cached))
        {
            return cached.ToList();
        }

        var routines = await DatabaseSessionService.Instance
            .GetRoutinesAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(cacheKey) && routines.Count > 0)
        {
            _routinesCache[cacheKey] = routines;
        }

        return routines;
    }

    public async Task<List<string>> GetServerDatabasesAsync(
        string? sessionKey = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        string? cacheKey = ResolveCacheSessionKey(sessionKey);
        if (!forceRefresh && !string.IsNullOrEmpty(cacheKey) &&
            _databasesCache.TryGetValue(cacheKey, out var cached))
        {
            return cached.ToList();
        }

        var dbs = await DatabaseSessionService.Instance
            .GetServerDatabasesAsync(sessionKey, cancellationToken)
            .ConfigureAwait(false);

        if (!string.IsNullOrEmpty(cacheKey) && dbs.Count > 0)
        {
            _databasesCache[cacheKey] = dbs;
        }

        return dbs;
    }

    public async Task<TableSchemaDetail?> GetTableSchemaAsync(
        string tableName,
        string? sessionKey = null,
        bool forceRefresh = false,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return null;

        var (schema, cleanTable) = ParseTableName(tableName);
        if (string.IsNullOrWhiteSpace(cleanTable)) return null;

        string? cacheKey = ResolveCacheSessionKey(sessionKey);
        string tableCacheKey = NormalizeCacheKey(schema, cleanTable);

        if (!forceRefresh && !string.IsNullOrEmpty(cacheKey) &&
            _tableCache.TryGetValue(cacheKey, out var sessionTables))
        {
            if (sessionTables.TryGetValue(tableCacheKey, out var cached) ||
                sessionTables.TryGetValue(cleanTable, out cached))
            {
                return cached;
            }
        }

        var detail = await DatabaseSessionService.Instance.ExecuteWithSessionAsync<TableSchemaDetail?>(
            sessionKey,
            async (session, ct) => await ExtractTableSchemaInternalAsync(session, schema, cleanTable, ct).ConfigureAwait(false),
            fallback: null,
            cancellationToken).ConfigureAwait(false);

        if (detail != null && !string.IsNullOrEmpty(cacheKey))
        {
            var cachedMap = _tableCache.GetOrAdd(cacheKey, _ => new ConcurrentDictionary<string, TableSchemaDetail>(StringComparer.OrdinalIgnoreCase));
            cachedMap[tableCacheKey] = detail;
            cachedMap[detail.FullName] = detail;
            if (string.IsNullOrWhiteSpace(schema))
            {
                cachedMap[cleanTable] = detail;
            }
        }

        return detail;
    }

    public async Task<DatabaseSchemaSnapshot?> GetDatabaseSchemaSnapshotAsync(
        string? sessionKey = null,
        bool forceRefresh = false,
        IProgress<(int Processed, int Total, string CurrentTable)>? progress = null,
        CancellationToken cancellationToken = default)
    {
        var session = !string.IsNullOrEmpty(sessionKey)
            ? DatabaseSessionService.Instance.OpenSessions.FirstOrDefault(s => s.Key.Equals(sessionKey, StringComparison.OrdinalIgnoreCase))
            : DatabaseSessionService.Instance.ActiveSession;

        if (session == null || session.Connection.State != ConnectionState.Open)
        {
            return null;
        }

        var tables = await GetTablesAsync(session.Key, forceRefresh, cancellationToken).ConfigureAwait(false);
        var routines = await GetRoutinesAsync(session.Key, forceRefresh, cancellationToken).ConfigureAwait(false);

        var schemas = new Dictionary<string, TableSchemaDetail>(StringComparer.OrdinalIgnoreCase);
        int total = tables.Count;
        int processed = 0;

        foreach (var table in tables)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var detail = await GetTableSchemaAsync(table.FullName, session.Key, forceRefresh, cancellationToken).ConfigureAwait(false);
            if (detail != null)
            {
                schemas[table.FullName] = detail;
            }
            processed++;
            progress?.Report((processed, total, table.FullName));
        }

        return new DatabaseSchemaSnapshot
        {
            SessionKey = session.Key,
            DatabaseName = session.Config.Database,
            DatabaseType = session.DatabaseType,
            ServerVersion = session.ServerVersion,
            ExtractedAtUtc = DateTime.UtcNow,
            Tables = tables,
            Routines = routines,
            TableSchemas = schemas
        };
    }

    private async Task<TableSchemaDetail?> ExtractTableSchemaInternalAsync(
        OpenDatabaseSession session,
        string? schema,
        string cleanTable,
        CancellationToken cancellationToken)
    {
        try
        {
            var dbType = session.Config.DatabaseType;
            string effectiveSchema = schema ?? dbType switch
            {
                DatabaseType.MSSQL => "dbo",
                DatabaseType.PostgreSQL => "public",
                DatabaseType.MySQL => session.Config.Database,
                _ => "main"
            };

            var columns = await ExtractColumnsAsync(session, schema, cleanTable, cancellationToken).ConfigureAwait(false);
            var pks = await ExtractPrimaryKeysAsync(session, schema, cleanTable, cancellationToken).ConfigureAwait(false);
            var fks = await ExtractForeignKeysAsync(session, schema, cleanTable, cancellationToken).ConfigureAwait(false);
            var indexes = await ExtractIndexesAsync(session, schema, cleanTable, cancellationToken).ConfigureAwait(false);

            var uniqueColumns = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach (var idx in indexes)
            {
                if (idx.IsUnique && idx.Columns.Count == 1)
                {
                    uniqueColumns.Add(idx.Columns[0]);
                }
            }

            var fkMap = new Dictionary<string, ForeignKeyRelation>(StringComparer.OrdinalIgnoreCase);
            foreach (var fk in fks)
            {
                if (!string.IsNullOrWhiteSpace(fk.ColumnName) && !fkMap.ContainsKey(fk.ColumnName))
                {
                    fkMap[fk.ColumnName] = fk;
                }
            }

            for (int i = 0; i < columns.Count; i++)
            {
                var col = columns[i];
                int pkIndex = pks.FindIndex(p => p.Equals(col.Name, StringComparison.OrdinalIgnoreCase));
                if (pkIndex >= 0)
                {
                    col.IsPrimaryKey = true;
                    col.PrimaryKeyOrdinal = pkIndex + 1;
                }

                if (fkMap.TryGetValue(col.Name, out var fk))
                {
                    col.IsForeignKey = true;
                    col.ForeignKey = fk;
                }

                if (uniqueColumns.Contains(col.Name))
                {
                    col.IsUnique = true;
                }
            }

            long? approxRows = null;
            var cachedTables = DatabaseSessionService.Instance.GetActiveCachedTables();
            var matchedTable = cachedTables.FirstOrDefault(t =>
                t.TableName.Equals(cleanTable, StringComparison.OrdinalIgnoreCase) &&
                (string.IsNullOrWhiteSpace(schema) || t.SchemaName.Equals(schema, StringComparison.OrdinalIgnoreCase)));

            if (matchedTable != null)
            {
                effectiveSchema = matchedTable.SchemaName;
                approxRows = matchedTable.RowCount;
            }

            string tableType = matchedTable?.TableType ?? "TABLE";

            return new TableSchemaDetail(
                effectiveSchema,
                cleanTable,
                tableType,
                dbType,
                columns,
                pks,
                fks,
                indexes,
                approxRows);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[DatabaseMetadataService] ExtractTableSchemaInternalAsync error for '{cleanTable}': {ex.Message}");
            return null;
        }
    }

    private static async Task<List<ColumnMetadata>> ExtractColumnsAsync(
        OpenDatabaseSession session,
        string? schema,
        string cleanTable,
        CancellationToken cancellationToken)
    {
        var list = new List<ColumnMetadata>();
        bool hasSchema = !string.IsNullOrWhiteSpace(schema);
        var dbType = session.Config.DatabaseType;

        if (dbType == DatabaseType.SQLite)
        {
            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = hasSchema && !schema!.Equals("main", StringComparison.OrdinalIgnoreCase)
                ? $"PRAGMA \"{schema.Replace("\"", "\"\"")}\".table_info('{EscapeSqlLiteral(cleanTable)}');"
                : $"PRAGMA table_info('{EscapeSqlLiteral(cleanTable)}');";

            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                int ord = 1;
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string colName = reader.GetString(1);
                    string colType = reader.IsDBNull(2) ? "TEXT" : reader.GetString(2);
                    if (string.IsNullOrWhiteSpace(colType)) colType = "TEXT";
                    bool notNull = !reader.IsDBNull(3) && reader.GetInt32(3) != 0;
                    string? dflt = reader.IsDBNull(4) ? null : reader.GetValue(4)?.ToString();
                    bool isPk = !reader.IsDBNull(5) && reader.GetInt32(5) > 0;
                    bool isIdentity = isPk && colType.Equals("INTEGER", StringComparison.OrdinalIgnoreCase);

                    list.Add(new ColumnMetadata
                    {
                        Ordinal = ord++,
                        Name = colName,
                        DataType = colType,
                        IsNullable = !notNull && !isPk,
                        IsPrimaryKey = isPk,
                        IsIdentity = isIdentity,
                        DefaultValue = dflt
                    });
                }
            }
            return list;
        }

        if (dbType == DatabaseType.MSSQL)
        {
            try
            {
                using var cmd = session.Connection.CreateCommand();
                cmd.CommandText = $@"
                    SELECT
                        c.column_id,
                        c.name,
                        t.name,
                        CASE
                            WHEN t.name IN ('nvarchar', 'nchar') AND c.max_length > 0 THEN c.max_length / 2
                            WHEN t.name IN ('varchar', 'char', 'varbinary', 'binary', 'nvarchar', 'nchar') THEN c.max_length
                            ELSE NULL
                        END AS MaxLength,
                        CASE WHEN t.name IN ('decimal', 'numeric') THEN CAST(c.precision AS INT) ELSE NULL END AS NumPrecision,
                        CASE WHEN t.name IN ('decimal', 'numeric') THEN CAST(c.scale AS INT) ELSE NULL END AS NumScale,
                        c.is_nullable,
                        c.is_identity,
                        dc.definition,
                        CAST(ep.value AS NVARCHAR(500))
                    FROM sys.columns c
                    INNER JOIN sys.types t ON c.user_type_id = t.user_type_id
                    INNER JOIN sys.objects o ON c.object_id = o.object_id
                    INNER JOIN sys.schemas s ON o.schema_id = s.schema_id
                    LEFT JOIN sys.default_constraints dc ON c.default_object_id = dc.object_id
                    LEFT JOIN sys.extended_properties ep ON ep.major_id = c.object_id AND ep.minor_id = c.column_id AND ep.name = 'MS_Description'
                    WHERE o.name = '{EscapeSqlLiteral(cleanTable)}'
                      AND o.type IN ('U', 'V')
                      {(hasSchema ? $"AND s.name = '{EscapeSqlLiteral(schema!)}'" : "")}
                    ORDER BY c.column_id;";

                if (cmd is DbCommand dbCmd)
                {
                    await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                    while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                    {
                        list.Add(new ColumnMetadata
                        {
                            Ordinal = reader.IsDBNull(0) ? list.Count + 1 : Convert.ToInt32(reader.GetValue(0)),
                            Name = reader.GetString(1),
                            DataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                            MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                            NumericPrecision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                            NumericScale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                            IsNullable = reader.IsDBNull(6) || Convert.ToBoolean(reader.GetValue(6)),
                            IsIdentity = !reader.IsDBNull(7) && Convert.ToBoolean(reader.GetValue(7)),
                            DefaultValue = reader.IsDBNull(8) ? null : reader.GetString(8),
                            Description = reader.IsDBNull(9) ? null : reader.GetString(9)
                        });
                    }
                    if (list.Count > 0) return list;
                }
            }
            catch
            {
                list.Clear();
            }

            // Fallback to INFORMATION_SCHEMA.COLUMNS
            using var fbCmd = session.Connection.CreateCommand();
            fbCmd.CommandText = $@"
                SELECT ORDINAL_POSITION, COLUMN_NAME, DATA_TYPE, CHARACTER_MAXIMUM_LENGTH,
                       NUMERIC_PRECISION, NUMERIC_SCALE, IS_NULLABLE, COLUMN_DEFAULT
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                  {(hasSchema ? $"AND TABLE_SCHEMA = '{EscapeSqlLiteral(schema!)}'" : "")}
                ORDER BY ORDINAL_POSITION;";

            if (fbCmd is DbCommand dbFbCmd)
            {
                await using var reader = await dbFbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    list.Add(new ColumnMetadata
                    {
                        Ordinal = reader.IsDBNull(0) ? list.Count + 1 : Convert.ToInt32(reader.GetValue(0)),
                        Name = reader.GetString(1),
                        DataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                        NumericPrecision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                        NumericScale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                        IsNullable = reader.IsDBNull(6) || reader.GetString(6).Equals("YES", StringComparison.OrdinalIgnoreCase),
                        DefaultValue = reader.IsDBNull(7) ? null : reader.GetValue(7)?.ToString()
                    });
                }
            }
            return list;
        }

        if (dbType == DatabaseType.PostgreSQL)
        {
            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    ordinal_position,
                    column_name,
                    data_type,
                    character_maximum_length,
                    numeric_precision,
                    numeric_scale,
                    is_nullable,
                    column_default,
                    is_identity
                FROM information_schema.columns
                WHERE LOWER(table_name) = '{EscapeSqlLiteral(cleanTable.ToLowerInvariant())}'
                  {(hasSchema ? $"AND LOWER(table_schema) = '{EscapeSqlLiteral(schema!.ToLowerInvariant())}'" : "AND table_schema NOT IN ('pg_catalog', 'information_schema')")}
                ORDER BY ordinal_position;";

            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string? dflt = reader.IsDBNull(7) ? null : reader.GetValue(7)?.ToString();
                    bool isIdentity = (!reader.IsDBNull(8) && reader.GetString(8).Equals("YES", StringComparison.OrdinalIgnoreCase))
                        || (!string.IsNullOrEmpty(dflt) && dflt.StartsWith("nextval(", StringComparison.OrdinalIgnoreCase));

                    list.Add(new ColumnMetadata
                    {
                        Ordinal = reader.IsDBNull(0) ? list.Count + 1 : Convert.ToInt32(reader.GetValue(0)),
                        Name = reader.GetString(1),
                        DataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                        NumericPrecision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                        NumericScale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                        IsNullable = reader.IsDBNull(6) || reader.GetString(6).Equals("YES", StringComparison.OrdinalIgnoreCase),
                        DefaultValue = dflt,
                        IsIdentity = isIdentity
                    });
                }
            }
            return list;
        }

        if (dbType == DatabaseType.MySQL)
        {
            using var cmd = session.Connection.CreateCommand();
            cmd.CommandText = $@"
                SELECT
                    ORDINAL_POSITION,
                    COLUMN_NAME,
                    DATA_TYPE,
                    CHARACTER_MAXIMUM_LENGTH,
                    NUMERIC_PRECISION,
                    NUMERIC_SCALE,
                    IS_NULLABLE,
                    COLUMN_DEFAULT,
                    EXTRA,
                    COLUMN_COMMENT
                FROM INFORMATION_SCHEMA.COLUMNS
                WHERE TABLE_SCHEMA = {(hasSchema ? $"'{EscapeSqlLiteral(schema!)}'" : "DATABASE()")}
                  AND TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                ORDER BY ORDINAL_POSITION;";

            if (cmd is DbCommand dbCmd)
            {
                await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string extra = reader.IsDBNull(8) ? string.Empty : reader.GetString(8);
                    list.Add(new ColumnMetadata
                    {
                        Ordinal = reader.IsDBNull(0) ? list.Count + 1 : Convert.ToInt32(reader.GetValue(0)),
                        Name = reader.GetString(1),
                        DataType = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        MaxLength = reader.IsDBNull(3) ? null : Convert.ToInt32(reader.GetValue(3)),
                        NumericPrecision = reader.IsDBNull(4) ? null : Convert.ToInt32(reader.GetValue(4)),
                        NumericScale = reader.IsDBNull(5) ? null : Convert.ToInt32(reader.GetValue(5)),
                        IsNullable = reader.IsDBNull(6) || reader.GetString(6).Equals("YES", StringComparison.OrdinalIgnoreCase),
                        DefaultValue = reader.IsDBNull(7) ? null : reader.GetValue(7)?.ToString(),
                        IsIdentity = extra.Contains("auto_increment", StringComparison.OrdinalIgnoreCase),
                        Description = reader.IsDBNull(9) ? null : reader.GetString(9)
                    });
                }
            }
        }

        return list;
    }

    private static async Task<List<string>> ExtractPrimaryKeysAsync(
        OpenDatabaseSession session,
        string? schema,
        string cleanTable,
        CancellationToken cancellationToken)
    {
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
                ? $"PRAGMA \"{schema.Replace("\"", "\"\"")}\".table_info('{EscapeSqlLiteral(cleanTable)}');"
                : $"PRAGMA table_info('{EscapeSqlLiteral(cleanTable)}');"
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

        return pks;
    }

    private static async Task<List<ForeignKeyRelation>> ExtractForeignKeysAsync(
        OpenDatabaseSession session,
        string? schema,
        string cleanTable,
        CancellationToken cancellationToken)
    {
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
            return results;
        }

        string sql = session.Config.DatabaseType switch
        {
            DatabaseType.MSSQL => $@"
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
                  {(hasSchema ? $"AND sch.name = '{EscapeSqlLiteral(schema!)}'" : "")};",
            DatabaseType.PostgreSQL => $@"
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
                  {(hasSchema ? $"AND LOWER(tc.table_schema) = '{EscapeSqlLiteral(schema!.ToLowerInvariant())}'" : "AND tc.table_schema NOT IN ('pg_catalog', 'information_schema')")};",
            DatabaseType.MySQL => $@"
                SELECT
                    COLUMN_NAME AS ColumnName,
                    REFERENCED_TABLE_NAME AS TargetTable,
                    REFERENCED_COLUMN_NAME AS TargetColumn,
                    CONSTRAINT_NAME AS ConstraintName
                FROM INFORMATION_SCHEMA.KEY_COLUMN_USAGE
                WHERE TABLE_SCHEMA = {(hasSchema ? $"'{EscapeSqlLiteral(schema!)}'" : "DATABASE()")}
                  AND REFERENCED_TABLE_NAME IS NOT NULL
                  AND TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}';",
            _ => string.Empty
        };

        if (string.IsNullOrEmpty(sql)) return results;

        using var fkCmd = session.Connection.CreateCommand();
        fkCmd.CommandText = sql;
        if (fkCmd is DbCommand dbFkCmd)
        {
            await using var reader = await dbFkCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
            while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
            {
                results.Add(new ForeignKeyRelation
                {
                    ColumnName = reader.GetString(0),
                    TargetTable = reader.GetString(1),
                    TargetColumn = reader.GetString(2),
                    ConstraintName = reader.IsDBNull(3) ? string.Empty : reader.GetString(3)
                });
            }
        }

        return results;
    }

    private static async Task<List<IndexMetadata>> ExtractIndexesAsync(
        OpenDatabaseSession session,
        string? schema,
        string cleanTable,
        CancellationToken cancellationToken)
    {
        var indexes = new List<IndexMetadata>();
        bool hasSchema = !string.IsNullOrWhiteSpace(schema);

        try
        {
            if (session.Config.DatabaseType == DatabaseType.SQLite)
            {
                var rawIdx = new List<(string Name, bool IsUnique, bool IsPk)>();
                using (var cmd = session.Connection.CreateCommand())
                {
                    cmd.CommandText = hasSchema && !schema!.Equals("main", StringComparison.OrdinalIgnoreCase)
                        ? $"PRAGMA \"{schema.Replace("\"", "\"\"")}\".index_list('{EscapeSqlLiteral(cleanTable)}');"
                        : $"PRAGMA index_list('{EscapeSqlLiteral(cleanTable)}');";
                    if (cmd is DbCommand dbCmd)
                    {
                        await using var reader = await dbCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            string name = reader.GetString(1);
                            bool isUnique = !reader.IsDBNull(2) && reader.GetInt32(2) != 0;
                            string origin = reader.FieldCount > 3 && !reader.IsDBNull(3) ? reader.GetString(3) : string.Empty;
                            rawIdx.Add((name, isUnique, origin.Equals("pk", StringComparison.OrdinalIgnoreCase)));
                        }
                    }
                }

                foreach (var idx in rawIdx)
                {
                    var cols = new List<string>();
                    using var colCmd = session.Connection.CreateCommand();
                    colCmd.CommandText = hasSchema && !schema!.Equals("main", StringComparison.OrdinalIgnoreCase)
                        ? $"PRAGMA \"{schema.Replace("\"", "\"\"")}\".index_info('{EscapeSqlLiteral(idx.Name)}');"
                        : $"PRAGMA index_info('{EscapeSqlLiteral(idx.Name)}');";
                    if (colCmd is DbCommand dbColCmd)
                    {
                        await using var reader = await dbColCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                        while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                        {
                            if (!reader.IsDBNull(2))
                            {
                                cols.Add(reader.GetString(2));
                            }
                        }
                    }

                    indexes.Add(new IndexMetadata
                    {
                        Name = idx.Name,
                        IsUnique = idx.IsUnique,
                        IsPrimaryKey = idx.IsPk,
                        Columns = cols
                    });
                }
                return indexes;
            }

            string sql = session.Config.DatabaseType switch
            {
                DatabaseType.MSSQL => $@"
                    SELECT i.name, i.is_unique, i.is_primary_key, c.name
                    FROM sys.indexes i
                    INNER JOIN sys.index_columns ic ON i.object_id = ic.object_id AND i.index_id = ic.index_id
                    INNER JOIN sys.columns c ON ic.object_id = c.object_id AND ic.column_id = c.column_id
                    INNER JOIN sys.tables t ON i.object_id = t.object_id
                    INNER JOIN sys.schemas s ON t.schema_id = s.schema_id
                    WHERE t.name = '{EscapeSqlLiteral(cleanTable)}'
                      {(hasSchema ? $"AND s.name = '{EscapeSqlLiteral(schema!)}'" : "")}
                      AND i.name IS NOT NULL
                      AND ic.is_included_column = 0
                    ORDER BY i.name, ic.key_ordinal;",
                DatabaseType.PostgreSQL => $@"
                    SELECT i.relname, ix.indisunique, ix.indisprimary, a.attname
                    FROM pg_class t
                    JOIN pg_namespace n ON n.oid = t.relnamespace
                    JOIN pg_index ix ON t.oid = ix.indrelid
                    JOIN pg_class i ON i.oid = ix.indexrelid
                    JOIN pg_attribute a ON a.attrelid = t.oid AND a.attnum = ANY(ix.indkey)
                    WHERE LOWER(t.relname) = '{EscapeSqlLiteral(cleanTable.ToLowerInvariant())}'
                      {(hasSchema ? $"AND LOWER(n.nspname) = '{EscapeSqlLiteral(schema!.ToLowerInvariant())}'" : "AND n.nspname NOT IN ('pg_catalog', 'information_schema')")}
                    ORDER BY i.relname;",
                DatabaseType.MySQL => $@"
                    SELECT INDEX_NAME, (NON_UNIQUE = 0) AS IsUnique, (INDEX_NAME = 'PRIMARY') AS IsPk, COLUMN_NAME
                    FROM INFORMATION_SCHEMA.STATISTICS
                    WHERE TABLE_SCHEMA = {(hasSchema ? $"'{EscapeSqlLiteral(schema!)}'" : "DATABASE()")}
                      AND TABLE_NAME = '{EscapeSqlLiteral(cleanTable)}'
                    ORDER BY INDEX_NAME, SEQ_IN_INDEX;",
                _ => string.Empty
            };

            if (string.IsNullOrEmpty(sql)) return indexes;

            using var idxCmd = session.Connection.CreateCommand();
            idxCmd.CommandText = sql;
            if (idxCmd is DbCommand dbIdxCmd)
            {
                var grouped = new Dictionary<string, IndexMetadata>(StringComparer.OrdinalIgnoreCase);
                await using var reader = await dbIdxCmd.ExecuteReaderAsync(cancellationToken).ConfigureAwait(false);
                while (await reader.ReadAsync(cancellationToken).ConfigureAwait(false))
                {
                    string idxName = reader.GetString(0);
                    bool isUnique = !reader.IsDBNull(1) && Convert.ToBoolean(reader.GetValue(1));
                    bool isPk = !reader.IsDBNull(2) && Convert.ToBoolean(reader.GetValue(2));
                    string colName = reader.GetString(3);

                    if (!grouped.TryGetValue(idxName, out var meta))
                    {
                        meta = new IndexMetadata
                        {
                            Name = idxName,
                            IsUnique = isUnique,
                            IsPrimaryKey = isPk
                        };
                        grouped[idxName] = meta;
                        indexes.Add(meta);
                    }
                    meta.Columns.Add(colName);
                }
            }
        }
        catch
        {
            // Non-fatal: some restricted accounts cannot read index metadata
        }

        return indexes;
    }

    private static string? ResolveCacheSessionKey(string? sessionKey)
    {
        if (!string.IsNullOrWhiteSpace(sessionKey)) return sessionKey;
        return DatabaseSessionService.Instance.ActiveSession?.Key;
    }

    private static string NormalizeCacheKey(string? schema, string table)
        => string.IsNullOrWhiteSpace(schema) ? table : $"{schema}.{table}";

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
}
