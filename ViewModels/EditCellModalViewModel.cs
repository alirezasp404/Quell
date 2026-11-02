using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fdb.ViewModels;

public partial class EditCellModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _modalTitle = "Edit Table Cell";

    [ObservableProperty]
    private string _modalSubtitle = "Modify cell value graphically and generate UPDATE query";

    [ObservableProperty]
    private string _tableName = "Users";

    [ObservableProperty]
    private string _columnName = "Name";

    [ObservableProperty]
    private string _rowIdText = "ID = 101";

    [ObservableProperty]
    private string _oldValue = string.Empty;

    [ObservableProperty]
    private string _newValue = string.Empty;

    [ObservableProperty]
    private string _generatedQuery = string.Empty;

    [ObservableProperty]
    private string _databaseType = "MSSQL";

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool? _isSuccess;

    [ObservableProperty]
    private bool _isExecuting;

    [ObservableProperty]
    private bool _isBatchEdit;

    [ObservableProperty]
    private int _batchRowCount = 1;

    [ObservableProperty]
    private int _batchCellCount = 1;

    public QueryResultRow? TargetRow { get; set; }

    public List<QueryResultRow> TargetRows { get; } = new();

    public List<string> TargetColumns { get; } = new();

    public Func<string, Task<bool>>? ExecuteQueryCallback { get; set; }
    public Action<string>? InsertQueryToEditorCallback { get; set; }
    public Action<string>? OnUpdatedCallback { get; set; }

    public void Open(QueryResultRow row, string? columnName, string tableName, string dbType)
    {
        OpenBatch(new[] { row }, new[] { string.IsNullOrWhiteSpace(columnName) ? "Name" : columnName }, tableName, dbType);
    }

    public void OpenBatch(IEnumerable<QueryResultRow> rows, IEnumerable<string> columnNames, string tableName, string dbType)
    {
        TargetRows.Clear();
        TargetRows.AddRange(rows);

        TargetColumns.Clear();
        var cleanCols = columnNames.Where(c => !string.IsNullOrWhiteSpace(c) && c != "ROW #").ToList();
        if (cleanCols.Count == 0) cleanCols.Add("Name");
        TargetColumns.AddRange(cleanCols);

        if (TargetRows.Count == 0) return;

        TargetRow = TargetRows[0];
        TableName = string.IsNullOrWhiteSpace(tableName) ? "Users" : tableName.Trim();
        ColumnName = string.Join(", ", TargetColumns);
        DatabaseType = string.IsNullOrWhiteSpace(dbType) ? "MSSQL" : dbType.Trim();

        if (TargetRows.Count > 1 || TargetColumns.Count > 1)
        {
            IsBatchEdit = true;
            BatchRowCount = TargetRows.Count;
            BatchCellCount = TargetRows.Count * TargetColumns.Count;
            ModalTitle = $"Batch Edit {BatchCellCount} Selected Cells";
            ModalSubtitle = $"Apply new value to {TargetRows.Count} rows in column '{ColumnName}' and generate UPDATE query";

            var idList = TargetRows.Select(r =>
            {
                string idCol = FindIdColumn(r);
                return !string.IsNullOrEmpty(idCol) && !string.IsNullOrEmpty(r[idCol]) ? r[idCol] : (r.Id > 0 ? r.Id.ToString() : $"#{r.RowIndex}");
            }).ToList();

            RowIdText = $"{TargetRows.Count} rows ({string.Join(", ", idList.Take(3))}{(idList.Count > 3 ? "..." : "")})";
            OldValue = GetBatchOldValueDescription(TargetRows, TargetColumns[0]);
            NewValue = string.Empty;
        }
        else
        {
            IsBatchEdit = false;
            BatchRowCount = 1;
            BatchCellCount = 1;
            ModalTitle = "Edit Table Cell";
            ModalSubtitle = "Modify cell value graphically and generate UPDATE query";

            string idCol = FindIdColumn(TargetRow);
            string idVal = !string.IsNullOrEmpty(idCol) ? TargetRow[idCol] : (TargetRow.Id > 0 ? TargetRow.Id.ToString() : "");
            RowIdText = !string.IsNullOrEmpty(idVal) ? $"{idCol} = {idVal}" : $"Row #{TargetRow.RowIndex}";

            OldValue = TargetRow[TargetColumns[0]];
            NewValue = OldValue;
        }

        StatusMessage = null;
        IsSuccess = null;
        IsExecuting = false;

        UpdateGeneratedQuery();
        IsOpen = true;
    }

    private static string GetBatchOldValueDescription(IReadOnlyList<QueryResultRow> rows, string column)
    {
        var distinctVals = rows.Select(r => r[column]).Distinct().ToList();
        if (distinctVals.Count == 1)
        {
            return distinctVals[0];
        }
        return $"(Multiple values: {string.Join(", ", distinctVals.Take(2))}{(distinctVals.Count > 2 ? "..." : "")})";
    }

    partial void OnNewValueChanged(string value)
    {
        UpdateGeneratedQuery();
    }

    partial void OnTableNameChanged(string value)
    {
        UpdateGeneratedQuery();
    }

    public void UpdateGeneratedQuery()
    {
        if (TargetRows.Count == 0 && TargetRow == null)
        {
            GeneratedQuery = string.Empty;
            return;
        }

        if (IsBatchEdit && TargetRows.Count > 0)
        {
            GeneratedQuery = BuildBatchUpdateQuery(TableName, TargetColumns, NewValue, TargetRows, DatabaseType);
        }
        else if (TargetRow != null)
        {
            GeneratedQuery = BuildUpdateQuery(TableName, TargetColumns.FirstOrDefault() ?? ColumnName, NewValue, TargetRow, DatabaseType);
        }
    }

    public static string BuildBatchUpdateQuery(string tableName, IReadOnlyList<string> columns, string newValue, IReadOnlyList<QueryResultRow> rows, string dbType)
    {
        string escapedTable = FormatIdentifier(tableName, dbType);
        string valueLiteral = FormatLiteral(newValue);
        var setClauses = columns.Select(col => $"{FormatIdentifier(col, dbType)} = {valueLiteral}");

        // Try single PK IN (...)
        string idCol = rows.Count > 0 ? FindIdColumn(rows[0]) : "Id";
        if (!string.IsNullOrEmpty(idCol) && rows.All(r => !string.IsNullOrEmpty(r[idCol])))
        {
            string escapedId = FormatIdentifier(idCol, dbType);
            var idList = rows.Select(r => FormatLiteralOrNumber(r[idCol]));
            return $"UPDATE {escapedTable}\nSET {string.Join(", ", setClauses)}\nWHERE {escapedId} IN ({string.Join(", ", idList)});";
        }

        var whereClauses = rows.Select(r => $"({BuildWhereClause(r, columns[0], dbType)})");
        return $"UPDATE {escapedTable}\nSET {string.Join(", ", setClauses)}\nWHERE {string.Join(" OR\n      ", whereClauses)};";
    }

    private static string FormatLiteralOrNumber(string val)
    {
        if (long.TryParse(val, out _) || double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return val;
        }
        return $"'{val.Replace("'", "''")}'";
    }

    public static string BuildUpdateQuery(string tableName, string columnName, string newValue, QueryResultRow row, string dbType)
    {
        string escapedTable = FormatIdentifier(tableName, dbType);
        string escapedCol = FormatIdentifier(columnName, dbType);
        string valueLiteral = FormatLiteral(newValue);

        string whereClause = BuildWhereClause(row, columnName, dbType);

        return $"UPDATE {escapedTable}\nSET {escapedCol} = {valueLiteral}\nWHERE {whereClause};";
    }

    private static string BuildWhereClause(QueryResultRow row, string excludeColumn, string dbType)
    {
        string idCol = FindIdColumn(row);
        if (!string.IsNullOrEmpty(idCol) && !string.IsNullOrEmpty(row[idCol]))
        {
            string idVal = row[idCol];
            if (long.TryParse(idVal, out _))
            {
                return $"{FormatIdentifier(idCol, dbType)} = {idVal}";
            }
            return $"{FormatIdentifier(idCol, dbType)} = '{idVal.Replace("'", "''")}'";
        }

        if (row.Id > 0)
        {
            return $"{FormatIdentifier("Id", dbType)} = {row.Id}";
        }

        // Fallback: Use other non-empty columns
        var conditions = new List<string>();
        foreach (var kvp in row.Values)
        {
            if (string.Equals(kvp.Key, excludeColumn, StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(kvp.Key, "Row #", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(kvp.Value)) continue;

            string escaped = kvp.Value.Replace("'", "''");
            conditions.Add($"{FormatIdentifier(kvp.Key, dbType)} = '{escaped}'");
            if (conditions.Count >= 3) break;
        }

        if (conditions.Count > 0)
        {
            return string.Join(" AND ", conditions);
        }

        string oldVal = row[excludeColumn].Replace("'", "''");
        return $"{FormatIdentifier(excludeColumn, dbType)} = '{oldVal}'";
    }

    private static string FindIdColumn(QueryResultRow row)
    {
        foreach (var key in row.Values.Keys)
        {
            if (string.Equals(key, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "user_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "userid", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith("id", StringComparison.OrdinalIgnoreCase))
            {
                return key;
            }
        }
        return row.Id > 0 ? "Id" : string.Empty;
    }

    private static string FormatIdentifier(string identifier, string dbType)
    {
        if (string.IsNullOrWhiteSpace(identifier)) return identifier;

        if (identifier.Contains('.'))
        {
            var parts = identifier.Split('.');
            for (int i = 0; i < parts.Length; i++)
            {
                parts[i] = FormatIdentifier(parts[i], dbType);
            }
            return string.Join(".", parts);
        }

        string clean = identifier.Trim('[', ']', '"', '`');

        return dbType.ToUpperInvariant() switch
        {
            "MSSQL" => $"[{clean}]",
            "MYSQL" => $"`{clean}`",
            "POSTGRESQL" or "SQLITE" => $"\"{clean}\"",
            _ => clean
        };
    }

    private static string FormatLiteral(string val)
    {
        if (val == null) return "NULL";
        if (string.Equals(val, "NULL", StringComparison.OrdinalIgnoreCase)) return "NULL";
        return $"'{val.Replace("'", "''")}'";
    }

    [RelayCommand]
    public async Task ExecuteAndSaveAsync()
    {
        if (TargetRow == null) return;

        IsExecuting = true;
        StatusMessage = "Executing UPDATE query...";
        IsSuccess = null;

        string query = GeneratedQuery;

        try
        {
            bool success = true;
            if (ExecuteQueryCallback != null)
            {
                success = await ExecuteQueryCallback.Invoke(query);
            }

            if (success)
            {
                if (IsBatchEdit)
                {
                    foreach (var r in TargetRows)
                    {
                        foreach (var col in TargetColumns)
                        {
                            r[col] = NewValue;
                        }
                    }

                    InsertQueryToEditorCallback?.Invoke(query);
                    string singleLineQuery = query.Replace("\n", " ");
                    OnUpdatedCallback?.Invoke($"Updated {BatchCellCount} cells: {singleLineQuery}");
                }
                else
                {
                    TargetRow[TargetColumns.FirstOrDefault() ?? ColumnName] = NewValue;
                    InsertQueryToEditorCallback?.Invoke(query);
                    string singleLineQuery = query.Replace("\n", " ");
                    OnUpdatedCallback?.Invoke($"Updated 1 row: {singleLineQuery}");
                }

                IsOpen = false;
            }
            else
            {
                StatusMessage = "Query execution failed on database.";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsExecuting = false;
        }
    }

    [RelayCommand]
    public void CopyQueryToEditor()
    {
        if (!string.IsNullOrEmpty(GeneratedQuery))
        {
            InsertQueryToEditorCallback?.Invoke(GeneratedQuery);
            OnUpdatedCallback?.Invoke($"Query copied to SQL editor: {GeneratedQuery.Replace("\n", " ")}");
        }
        IsOpen = false;
    }

    [RelayCommand]
    public void Cancel()
    {
        IsOpen = false;
    }
}
