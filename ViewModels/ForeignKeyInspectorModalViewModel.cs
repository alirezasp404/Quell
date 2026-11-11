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

namespace Quell.ViewModels;

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
    private string _databaseType = "MSSQL";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _rawJson = string.Empty;

    public ObservableCollection<ForeignKeyFieldItem> Fields { get; } = new();

    public QueryResultRow? ReferencedRow { get; private set; }

    public Func<string, string, string?, Task<QueryResultRow?>>? FetchRecordCallback { get; set; }
    public Func<string, string, string?, string?, Task>? NavigateToTableCallback { get; set; }

    public async Task OpenAsync(
        string sourceTable,
        string sourceColumn,
        string? sourceValue,
        string targetTable,
        string targetColumn,
        string dbType = "MSSQL",
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
                var row = await FetchRecordCallback.Invoke(TargetTable, TargetColumn, SourceValue);
                if (row != null)
                {
                    ReferencedRow = row;
                    PopulateFields(row);
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

    public void PopulateFields(QueryResultRow row)
    {
        Fields.Clear();
        var dict = new Dictionary<string, object?>(StringComparer.OrdinalIgnoreCase);

        // Collect all available column values
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

        // If row has standard properties that aren't in Values yet, add them
        if (row.Id > 0 && !columns.Contains("Id", StringComparer.OrdinalIgnoreCase)) columns.Insert(0, "Id");
        if (!string.IsNullOrEmpty(row.Name) && !columns.Contains("Name", StringComparer.OrdinalIgnoreCase)) columns.Add("Name");
        if (!string.IsNullOrEmpty(row.Email) && !columns.Contains("Email", StringComparer.OrdinalIgnoreCase)) columns.Add("Email");
        if (!string.IsNullOrEmpty(row.Role) && !columns.Contains("Role", StringComparer.OrdinalIgnoreCase)) columns.Add("Role");
        if (!string.IsNullOrEmpty(row.Course) && !columns.Contains("Course", StringComparer.OrdinalIgnoreCase)) columns.Add("Course");
        if (!string.IsNullOrEmpty(row.Status) && !columns.Contains("Status", StringComparer.OrdinalIgnoreCase)) columns.Add("Status");
        if (!string.IsNullOrEmpty(row.CreatedAt) && !columns.Contains("CreatedAt", StringComparer.OrdinalIgnoreCase)) columns.Add("CreatedAt");

        foreach (var col in columns)
        {
            string val = row[col];
            bool isPk = col.Equals(TargetColumn, StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("Id", StringComparison.OrdinalIgnoreCase) ||
                        col.Equals("ID", StringComparison.OrdinalIgnoreCase);

            bool isFk = !isPk && (col.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
                                  col.EndsWith("id", StringComparison.OrdinalIgnoreCase) ||
                                  col.Equals("Role", StringComparison.OrdinalIgnoreCase) ||
                                  col.Equals("Course", StringComparison.OrdinalIgnoreCase));

            string guessedType = isPk ? "INT (PK)" : (isFk ? "INT (FK)" : (long.TryParse(val, out _) ? "BIGINT" : "VARCHAR"));

            Fields.Add(new ForeignKeyFieldItem
            {
                FieldName = col,
                FieldValue = val,
                IsPrimaryKey = isPk,
                IsForeignKey = isFk,
                DataType = guessedType
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
