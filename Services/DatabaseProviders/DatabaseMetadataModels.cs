using System;
using System.Collections.Generic;
using System.Linq;

namespace Quell.Services.DatabaseProviders;

public sealed class ColumnMetadata
{
    public int Ordinal { get; init; }
    public string Name { get; init; } = string.Empty;
    public string DataType { get; init; } = string.Empty;
    public int? MaxLength { get; init; }
    public int? NumericPrecision { get; init; }
    public int? NumericScale { get; init; }
    public bool IsNullable { get; init; } = true;
    public bool IsPrimaryKey { get; set; }
    public int? PrimaryKeyOrdinal { get; set; }
    public bool IsForeignKey { get; set; }
    public bool IsUnique { get; set; }
    public bool IsIdentity { get; init; }
    public string? DefaultValue { get; init; }
    public string? Description { get; init; }
    public ForeignKeyRelation? ForeignKey { get; set; }

    public string FormattedDataType
    {
        get
        {
            if (string.IsNullOrWhiteSpace(DataType)) return "TEXT";
            if (DataType.Contains('(')) return DataType;
            if (MaxLength.HasValue)
            {
                return MaxLength.Value < 0
                    ? $"{DataType}(MAX)"
                    : $"{DataType}({MaxLength.Value})";
            }
            if (NumericPrecision.HasValue && NumericScale.HasValue && NumericScale.Value > 0)
            {
                return $"{DataType}({NumericPrecision.Value},{NumericScale.Value})";
            }
            return DataType;
        }
    }
}

public sealed class IndexMetadata
{
    public string Name { get; init; } = string.Empty;
    public bool IsUnique { get; init; }
    public bool IsPrimaryKey { get; init; }
    public List<string> Columns { get; init; } = new();
}

public sealed class TableSchemaDetail
{
    private readonly Dictionary<string, ColumnMetadata> _columnLookup;
    private readonly Dictionary<string, ForeignKeyRelation> _fkLookup;
    private readonly HashSet<string> _pkSet;

    public string SchemaName { get; }
    public string TableName { get; }
    public string FullName => string.IsNullOrEmpty(SchemaName) || SchemaName.Equals("main", StringComparison.OrdinalIgnoreCase)
        ? TableName
        : $"{SchemaName}.{TableName}";
    public string TableType { get; }
    public long? ApproximateRowCount { get; }
    public DatabaseType DatabaseType { get; }
    public DateTime ExtractedAtUtc { get; }

    public IReadOnlyList<ColumnMetadata> Columns { get; }
    public IReadOnlyList<string> PrimaryKeyColumns { get; }
    public IReadOnlyList<ForeignKeyRelation> ForeignKeys { get; }
    public IReadOnlyList<IndexMetadata> Indexes { get; }

    public TableSchemaDetail(
        string schemaName,
        string tableName,
        string tableType,
        DatabaseType databaseType,
        IReadOnlyList<ColumnMetadata> columns,
        IReadOnlyList<string> primaryKeyColumns,
        IReadOnlyList<ForeignKeyRelation> foreignKeys,
        IReadOnlyList<IndexMetadata>? indexes = null,
        long? approximateRowCount = null)
    {
        SchemaName = schemaName;
        TableName = tableName;
        TableType = tableType;
        DatabaseType = databaseType;
        ApproximateRowCount = approximateRowCount;
        ExtractedAtUtc = DateTime.UtcNow;

        Columns = columns;
        PrimaryKeyColumns = primaryKeyColumns;
        ForeignKeys = foreignKeys;
        Indexes = indexes ?? Array.Empty<IndexMetadata>();

        _pkSet = new HashSet<string>(primaryKeyColumns, StringComparer.OrdinalIgnoreCase);
        _fkLookup = new Dictionary<string, ForeignKeyRelation>(StringComparer.OrdinalIgnoreCase);
        foreach (var fk in foreignKeys)
        {
            if (!string.IsNullOrWhiteSpace(fk.ColumnName) && !_fkLookup.ContainsKey(fk.ColumnName))
            {
                _fkLookup[fk.ColumnName] = fk;
            }
        }

        _columnLookup = new Dictionary<string, ColumnMetadata>(StringComparer.OrdinalIgnoreCase);
        foreach (var col in columns)
        {
            if (!string.IsNullOrWhiteSpace(col.Name) && !_columnLookup.ContainsKey(col.Name))
            {
                _columnLookup[col.Name] = col;
            }
        }
    }

    public ColumnMetadata? GetColumn(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return null;
        return _columnLookup.TryGetValue(columnName, out var col) ? col : null;
    }

    public bool IsColumnPrimaryKey(string? columnName)
        => !string.IsNullOrWhiteSpace(columnName) && _pkSet.Contains(columnName);

    public bool IsColumnForeignKey(string? columnName)
        => !string.IsNullOrWhiteSpace(columnName) && _fkLookup.ContainsKey(columnName);

    public ForeignKeyRelation? GetForeignKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return null;
        return _fkLookup.TryGetValue(columnName, out var fk) ? fk : null;
    }

    public string? GetColumnDataType(string? columnName)
        => GetColumn(columnName)?.FormattedDataType;

    public bool IsColumnNullable(string? columnName)
        => GetColumn(columnName)?.IsNullable ?? true;

    public bool IsColumnIdentity(string? columnName)
        => GetColumn(columnName)?.IsIdentity ?? false;

    public List<(string ColumnName, string DataType)> ToColumnTuples()
        => Columns.Select(c => (c.Name, c.FormattedDataType)).ToList();
}

public sealed class DatabaseSchemaSnapshot
{
    public string SessionKey { get; init; } = string.Empty;
    public string DatabaseName { get; init; } = string.Empty;
    public DatabaseType DatabaseType { get; init; }
    public string? ServerVersion { get; init; }
    public DateTime ExtractedAtUtc { get; init; } = DateTime.UtcNow;
    public IReadOnlyList<TableMetadata> Tables { get; init; } = Array.Empty<TableMetadata>();
    public IReadOnlyList<TableMetadata> Routines { get; init; } = Array.Empty<TableMetadata>();
    public IReadOnlyDictionary<string, TableSchemaDetail> TableSchemas { get; init; }
        = new Dictionary<string, TableSchemaDetail>(StringComparer.OrdinalIgnoreCase);
}
