using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fdb.ViewModels;

public partial class EditCellModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

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

    public QueryResultRow? TargetRow { get; set; }

    public Func<string, Task<bool>>? ExecuteQueryCallback { get; set; }
    public Action<string>? InsertQueryToEditorCallback { get; set; }
    public Action<string>? OnUpdatedCallback { get; set; }

    public void Open(QueryResultRow row, string? columnName, string tableName, string dbType)
    {
        TargetRow = row;
        TableName = string.IsNullOrWhiteSpace(tableName) ? "Users" : tableName.Trim();
        ColumnName = string.IsNullOrWhiteSpace(columnName) ? "Name" : columnName.Trim();
        DatabaseType = string.IsNullOrWhiteSpace(dbType) ? "MSSQL" : dbType.Trim();

        OldValue = row[ColumnName];
        NewValue = OldValue;

        // Determine Row ID representation
        string idCol = FindIdColumn(row);
        string idVal = !string.IsNullOrEmpty(idCol) ? row[idCol] : (row.Id > 0 ? row.Id.ToString() : "");
        if (!string.IsNullOrEmpty(idVal))
        {
            RowIdText = $"{idCol} = {idVal}";
        }
        else
        {
            RowIdText = $"Row #{row.RowIndex}";
        }

        StatusMessage = null;
        IsSuccess = null;
        IsExecuting = false;

        UpdateGeneratedQuery();
        IsOpen = true;
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
        if (TargetRow == null)
        {
            GeneratedQuery = string.Empty;
            return;
        }

        GeneratedQuery = BuildUpdateQuery(TableName, ColumnName, NewValue, TargetRow, DatabaseType);
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
                // Update in-memory row
                TargetRow[ColumnName] = NewValue;

                // Insert into SQL editor so user sees the executed query
                InsertQueryToEditorCallback?.Invoke(query);

                string singleLineQuery = query.Replace("\n", " ");
                OnUpdatedCallback?.Invoke($"Updated 1 row: {singleLineQuery}");

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
