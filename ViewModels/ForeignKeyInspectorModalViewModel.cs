using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

public sealed record TableSchemaMetadata(
    IReadOnlyList<(string ColumnName, string DataType)> Columns,
    IReadOnlyCollection<string> PrimaryKeys,
    IReadOnlyCollection<ForeignKeyRelation> ForeignKeys);

public partial class ForeignKeyFieldItem : ObservableObject
{
    [ObservableProperty]
    private string _fieldName = string.Empty;

    [ObservableProperty]
    private string _fieldValue = string.Empty;

    [ObservableProperty]
    private bool _isPrimaryKey;

    [ObservableProperty]
    private bool _isForeignKey;

    [ObservableProperty]
    private string _dataType = "TEXT";

    public bool IsNull => string.IsNullOrWhiteSpace(FieldValue) || FieldValue.Equals("NULL", StringComparison.OrdinalIgnoreCase);

    public string DisplayValue => IsNull ? "NULL" : FieldValue;
}

public partial class ForeignKeyInspectorModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private bool _recordFound;

    [ObservableProperty]
    private string _sourceTable = string.Empty;

    [ObservableProperty]
    private string _sourceColumn = string.Empty;

    [ObservableProperty]
    private string _sourceValue = string.Empty;

    [ObservableProperty]
    private string _targetTable = string.Empty;

    [ObservableProperty]
    private string _targetColumn = "Id";

    [ObservableProperty]
    private string _constraintName = string.Empty;

    [ObservableProperty]
    private DatabaseType _databaseType = DatabaseType.MSSQL;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _rawJson = string.Empty;

    public ObservableCollection<ForeignKeyFieldItem> Fields { get; } = new();

    public QueryResultRow? ReferencedRow { get; private set; }

    public Func<string, string, string?, Task<QueryResultRow?>>? FetchRecordCallback { get; set; }
    public Func<string, Task<TableSchemaMetadata?>>? FetchTableSchemaCallback { get; set; }
    public Func<string, string, string?, string?, Task>? NavigateToTableCallback { get; set; }

    public async Task OpenAsync(
        string sourceTable,
        string sourceColumn,
        string? sourceValue,
        string targetTable,
        string targetColumn,
        DatabaseType dbType = DatabaseType.MSSQL,
        string? constraintName = null)
    {
        SourceTable = string.IsNullOrWhiteSpace(sourceTable) ? "Current Table" : sourceTable.Trim();
        SourceColumn = sourceColumn;
        SourceValue = sourceValue ?? string.Empty;
        TargetTable = string.IsNullOrWhiteSpace(targetTable) ? "Referenced Table" : targetTable.Trim();
        TargetColumn = string.IsNullOrWhiteSpace(targetColumn) ? "Id" : targetColumn.Trim();
        ConstraintName = constraintName ?? string.Empty;
        DatabaseType = dbType;

        Fields.Clear();
        ReferencedRow = null;
        StatusMessage = null;
        ErrorMessage = null;
        RawJson = string.Empty;
        RecordFound = false;
        IsLoading = true;
        IsOpen = true;

        if (string.IsNullOrWhiteSpace(sourceValue) || sourceValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            IsLoading = false;
            RecordFound = false;
            StatusMessage = "Foreign key value is NULL. No referenced record to display.";
            return;
        }

        try
        {
            if (FetchRecordCallback != null)
            {
                var rowTask = FetchRecordCallback.Invoke(TargetTable, TargetColumn, SourceValue);
                var schemaTask = FetchTableSchemaCallback != null
                    ? FetchTableSchemaCallback.Invoke(TargetTable)
                    : Task.FromResult<TableSchemaMetadata?>(null);

                var row = await rowTask;
                var schema = await schemaTask;

                if (row != null)
                {
                    ReferencedRow = row;
                    PopulateFields(row, schema);
                    RecordFound = true;
                }
                else
                {
                    RecordFound = false;
                    ErrorMessage = $"No matching record found in '{TargetTable}' where {TargetColumn} = '{SourceValue}'.";
                }
            }
            else
            {
                RecordFound = false;
                ErrorMessage = "Database fetch callback is not configured.";
            }
        }
        catch (Exception ex)
        {
            RecordFound = false;
            ErrorMessage = $"Failed to fetch referenced record: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    public void PopulateFields(QueryResultRow row, TableSchemaMetadata? schema = null)
    {
        Fields.Clear();
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        var columns = new List<string>();
        foreach (var key in row.Values.Keys)
        {
            if (!columns.Contains(key, StringComparer.OrdinalIgnoreCase) &&
                !key.Equals("ROWINDEX", StringComparison.OrdinalIgnoreCase) &&
                !key.Equals("ROW #", StringComparison.OrdinalIgnoreCase))
            {
                columns.Add(key);
            }
        }

        var pkSet = schema != null
            ? new HashSet<string>(schema.PrimaryKeys, StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var fkSet = schema != null
            ? new HashSet<string>(schema.ForeignKeys.Select(f => f.ColumnName), StringComparer.OrdinalIgnoreCase)
            : new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var typeMap = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (schema != null)
        {
            foreach (var (colName, dataType) in schema.Columns)
            {
                if (!string.IsNullOrWhiteSpace(colName))
                {
                    typeMap[colName] = string.IsNullOrWhiteSpace(dataType) ? "UNKNOWN" : dataType.ToUpperInvariant();
                }
            }
        }

        foreach (var col in columns)
        {
            string val = row[col];
            bool isPk = pkSet.Contains(col);
            bool isFk = !isPk && fkSet.Contains(col);
            string dataType = typeMap.TryGetValue(col, out var dbTypeStr) ? dbTypeStr : "UNKNOWN";

            Fields.Add(new ForeignKeyFieldItem
            {
                FieldName = col,
                FieldValue = val,
                IsPrimaryKey = isPk,
                IsForeignKey = isFk,
                DataType = dataType
            });

            dict[col] = val;
        }

        try
        {
            RawJson = JsonSerializer.Serialize(dict, new JsonSerializerOptions { WriteIndented = true });
        }
        catch
        {
            RawJson = string.Empty;
        }
    }

    [RelayCommand]
    public async Task OpenInTableViewAsync()
    {
        IsOpen = false;
        if (NavigateToTableCallback != null)
        {
            await NavigateToTableCallback.Invoke(TargetTable, TargetColumn, SourceValue, SourceColumn);
        }
    }

    [RelayCommand]
    public async Task CopyFieldValueAsync(ForeignKeyFieldItem? field)
    {
        if (field == null) return;
        await WriteClipboardAsync(field.FieldValue);
    }

    [RelayCommand]
    public async Task CopyRecordJsonAsync()
    {
        if (!string.IsNullOrEmpty(RawJson))
        {
            await WriteClipboardAsync(RawJson);
        }
    }

    [RelayCommand]
    public async Task CopySourceValueAsync()
    {
        await WriteClipboardAsync(SourceValue);
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    private static async Task WriteClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        await Quell.Services.ClipboardService.CopyTextAsync(text);
    }
}
