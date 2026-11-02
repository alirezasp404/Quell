using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Controls.ApplicationLifetimes;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Fdb.ViewModels;

public partial class QueryResultRow : ObservableObject
{
    [ObservableProperty]
    private int _rowIndex;

    [ObservableProperty]
    private int _id;

    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private string _email = string.Empty;

    [ObservableProperty]
    private string _role = string.Empty;

    [ObservableProperty]
    private string _status = "Active";

    [ObservableProperty]
    private string _createdAt = string.Empty;

    public Dictionary<string, string> Values { get; } = new(StringComparer.OrdinalIgnoreCase);
    public Dictionary<string, string> OriginalValues { get; } = new(StringComparer.OrdinalIgnoreCase);

    public string this[string key]
    {
        get
        {
            if (Values.TryGetValue(key, out var val))
                return val;

            return key.ToUpperInvariant() switch
            {
                "ROW #" or "ROWINDEX" => RowIndex.ToString(),
                "ID" => Id.ToString(),
                "NAME" => Name,
                "EMAIL" => Email,
                "ROLE" => Role,
                "STATUS" => Status,
                "CREATED AT" or "CREATEDAT" => CreatedAt,
                _ => string.Empty
            };
        }
        set
        {
            Values[key] = value;
            switch (key.ToUpperInvariant())
            {
                case "ID":
                    if (int.TryParse(value, out int idVal)) Id = idVal;
                    break;
                case "NAME":
                    Name = value;
                    break;
                case "EMAIL":
                    Email = value;
                    break;
                case "ROLE":
                    Role = value;
                    break;
                case "STATUS":
                    Status = value;
                    break;
                case "CREATED AT" or "CREATEDAT":
                    CreatedAt = value;
                    break;
            }
            OnPropertyChanged("Item[]");
            OnPropertyChanged(nameof(Name));
            OnPropertyChanged(nameof(Email));
            OnPropertyChanged(nameof(Role));
            OnPropertyChanged(nameof(Status));
            OnPropertyChanged(nameof(CreatedAt));
        }
    }
}

public partial class QueryResultTableViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _lastExecutedQuery = string.Empty;

    [ObservableProperty]
    private bool _isRefreshing;

    public Func<Task>? RefreshCallback { get; set; }

    private readonly List<QueryResultRow> _allRows = new();

    [ObservableProperty]
    private int _totalRows = 5;

    [ObservableProperty]
    private string _executionTime = "14 ms";

    [ObservableProperty]
    private string _targetDatabaseName = "(No Connection)";

    [ObservableProperty]
    private string _currentTableName = "Users";

    [ObservableProperty]
    private string _currentDatabaseType = "MSSQL";

    [ObservableProperty]
    private string _primaryKeyColumnName = "Id";

    [ObservableProperty]
    private bool _hasPrimaryKey = true;

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter(value);
    }

    public void ApplyFilter(string? filter)
    {
        if (_allRows.Count == 0 && Rows.Count > 0)
        {
            _allRows.AddRange(Rows);
        }

        if (string.IsNullOrWhiteSpace(filter))
        {
            if (Rows.Count != _allRows.Count)
            {
                Rows.Clear();
                foreach (var r in _allRows) Rows.Add(r);
                TotalRows = Rows.Count;
            }
            return;
        }

        string term = filter.Trim();
        var filtered = _allRows.Where(r => MatchesFilter(r, term)).ToList();

        Rows.Clear();
        foreach (var r in filtered) Rows.Add(r);
        TotalRows = Rows.Count;
    }

    private static bool MatchesFilter(QueryResultRow row, string term)
    {
        if (row.Name.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            row.Email.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            row.Role.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            row.Status.Contains(term, StringComparison.OrdinalIgnoreCase) ||
            row.Id.ToString().Contains(term, StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        foreach (var val in row.Values.Values)
        {
            if (val != null && val.Contains(term, StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }
        return false;
    }

    public HashSet<string> PrimaryKeyColumns { get; } = new(StringComparer.OrdinalIgnoreCase) { "Id", "ID", "user_id" };

    public bool IsColumnPrimaryKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        return PrimaryKeyColumns.Contains(columnName) || IsProbablePrimaryKey(columnName, CurrentTableName);
    }

    public static bool IsProbablePrimaryKey(string? columnName, string? tableName = null)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        string c = columnName.Trim().ToLowerInvariant();
        if (c == "id" || c == "rowid" || c == "user_id" || c == "userid" || c == "uuid" || c == "guid" || c == "pk")
            return true;

        if (!string.IsNullOrEmpty(tableName))
        {
            string t = tableName.Trim('[', ']', '"', '`').ToLowerInvariant();
            if (t.Contains('.')) t = t.Split('.')[1];
            if (c == $"{t}_id" || c == $"{t}id" || c == $"{t}_pk")
                return true;
            if (t.EndsWith("s") && (c == $"{t.TrimEnd('s')}_id" || c == $"{t.TrimEnd('s')}id"))
                return true;
        }

        return c.EndsWith("_id") || (c.EndsWith("id") && c.Length > 2);
    }

    public void SetPrimaryKeys(IEnumerable<string> pks)
    {
        PrimaryKeyColumns.Clear();
        foreach (var pk in pks)
        {
            PrimaryKeyColumns.Add(pk);
        }
        if (PrimaryKeyColumns.Count > 0)
        {
            PrimaryKeyColumnName = string.Join(", ", PrimaryKeyColumns);
            HasPrimaryKey = true;
        }
        else
        {
            PrimaryKeyColumnName = string.Empty;
            HasPrimaryKey = false;
        }

        if (_lastColumns != null)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    private IReadOnlyList<string>? _lastColumns;

    [ObservableProperty]
    private string _lastQueryMessage = string.Empty;

    [ObservableProperty]
    private bool _hasLastQueryMessage;

    [ObservableProperty]
    private int _currentPage = 1;

    [ObservableProperty]
    private int _totalPages = 1;

    [ObservableProperty]
    private QueryResultRow? _selectedRow;

    [ObservableProperty]
    private string _selectionStatus = "Click a cell to select it · drag or Shift-click for a range";

    public Func<string?>? CopySelection { get; set; }

    public Func<string?>? CopyFocusedRow { get; set; }

    public EditCellModalViewModel EditCellModal { get; } = new();

    public Action<string, string, string?, Action>? RequestConfirmation { get; set; }

    public Func<string, Task<bool>>? ExecuteQueryCallback { get; set; }

    public Action<string>? InsertQueryToEditorCallback { get; set; }

    public Func<IReadOnlyList<QueryResultRow>>? GetSelectedRowsCallback { get; set; }

    public Func<IReadOnlyList<string>>? GetSelectedColumnsCallback { get; set; }

    public event Action<IReadOnlyList<string>>? ColumnsChanged;

    public ObservableCollection<QueryResultRow> Rows { get; } = new()
    {
        new QueryResultRow { RowIndex = 1, Id = 101, Name = "Alice Johnson", Email = "alice@example.com", Role = "DBA", Status = "Active", CreatedAt = "2026-01-15 08:30:00" },
        new QueryResultRow { RowIndex = 2, Id = 102, Name = "Bob Smith", Email = "bob@example.com", Role = "Developer", Status = "Active", CreatedAt = "2026-02-10 11:15:22" },
        new QueryResultRow { RowIndex = 3, Id = 103, Name = "Charlie Davis", Email = "charlie@example.com", Role = "Analyst", Status = "Pending", CreatedAt = "2026-03-01 14:45:00" },
        new QueryResultRow { RowIndex = 4, Id = 104, Name = "Dana White", Email = "dana@example.com", Role = "Admin", Status = "Active", CreatedAt = "2026-03-12 09:20:11" },
        new QueryResultRow { RowIndex = 5, Id = 105, Name = "Evan Wright", Email = "evan@example.com", Role = "Developer", Status = "Inactive", CreatedAt = "2026-04-05 16:00:45" }
    };

    public QueryResultTableViewModel()
    {
        EditCellModal.OnUpdatedCallback = msg =>
        {
            LastQueryMessage = msg;
            HasLastQueryMessage = true;
        };

        // Cache initial row values into OriginalValues
        foreach (var r in Rows)
        {
            r.OriginalValues["Id"] = r.Id.ToString();
            r.OriginalValues["Name"] = r.Name;
            r.OriginalValues["Email"] = r.Email;
            r.OriginalValues["Role"] = r.Role;
            r.OriginalValues["Status"] = r.Status;
            r.OriginalValues["CreatedAt"] = r.CreatedAt;
        }
        _allRows.Clear();
        _allRows.AddRange(Rows);
    }

    public void SetResults(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> newRows, string execTime)
    {
        void Apply()
        {
            _lastColumns = columns;
            _allRows.Clear();
            _allRows.AddRange(newRows);

            Rows.Clear();
            foreach (var r in newRows)
            {
                foreach (var kvp in r.Values)
                {
                    r.OriginalValues[kvp.Key] = kvp.Value;
                }
                Rows.Add(r);
            }

            if (!string.IsNullOrWhiteSpace(SearchFilter))
            {
                ApplyFilter(SearchFilter);
            }
            else
            {
                TotalRows = newRows.Count;
            }
            ExecutionTime = execTime;

            if (PrimaryKeyColumns.Count == 0 || !columns.Any(c => PrimaryKeyColumns.Contains(c)))
            {
                var detected = columns.FirstOrDefault(c => IsProbablePrimaryKey(c, CurrentTableName));
                if (!string.IsNullOrEmpty(detected))
                {
                    PrimaryKeyColumns.Clear();
                    PrimaryKeyColumns.Add(detected);
                    PrimaryKeyColumnName = detected;
                    HasPrimaryKey = true;
                }
                else
                {
                    HasPrimaryKey = false;
                    PrimaryKeyColumnName = string.Empty;
                }
            }
            else
            {
                HasPrimaryKey = true;
                PrimaryKeyColumnName = string.Join(", ", PrimaryKeyColumns.Where(c => columns.Contains(c, StringComparer.OrdinalIgnoreCase)));
            }

            ColumnsChanged?.Invoke(columns);
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else
        {
            try
            {
                Apply();
            }
            catch (InvalidOperationException)
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
            }
        }
    }

    public void OpenEditCell(QueryResultRow? row, string? columnName = null)
    {
        var selectedRows = GetSelectedRowsCallback?.Invoke();
        var selectedCols = GetSelectedColumnsCallback?.Invoke();

        if (selectedRows != null && selectedRows.Count > 1)
        {
            var cols = !string.IsNullOrWhiteSpace(columnName) && columnName != "ROW #" 
                ? new[] { columnName } 
                : (selectedCols != null && selectedCols.Count > 0 ? selectedCols : new[] { "Name" });
            EditCellModal.OpenBatch(selectedRows, cols, CurrentTableName, CurrentDatabaseType);
            return;
        }

        var targetRow = row ?? selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        if (targetRow == null) return;

        string col = string.IsNullOrWhiteSpace(columnName) || columnName == "ROW #" 
            ? (selectedCols?.FirstOrDefault() ?? "Name") 
            : columnName;
        EditCellModal.Open(targetRow, col, CurrentTableName, CurrentDatabaseType);
    }

    [RelayCommand]
    public void OpenEditSelectedCell()
    {
        var selectedRows = GetSelectedRowsCallback?.Invoke();
        var selectedCols = GetSelectedColumnsCallback?.Invoke();

        if (selectedRows != null && selectedRows.Count > 1)
        {
            var cols = selectedCols != null && selectedCols.Count > 0 ? selectedCols : new[] { "Name" };
            EditCellModal.OpenBatch(selectedRows, cols, CurrentTableName, CurrentDatabaseType);
            return;
        }

        var row = selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        string? col = selectedCols?.FirstOrDefault();
        OpenEditCell(row, col);
    }

    [RelayCommand]
    public void GenerateUpdateQuery()
    {
        OpenEditSelectedCell();
    }

    [RelayCommand]
    public void DeleteSelectedRow()
    {
        var selectedRows = GetSelectedRowsCallback?.Invoke();
        if (selectedRows != null && selectedRows.Count > 1)
        {
            DeleteRows(selectedRows);
            return;
        }

        var selectedCols = GetSelectedColumnsCallback?.Invoke();
        if (selectedCols != null && selectedCols.Count > 1 && (selectedRows == null || selectedRows.Count <= 1))
        {
            DeleteColumns(selectedCols);
            return;
        }

        DeleteRow(selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault());
    }

    [RelayCommand]
    public void DeleteSelectedColumn()
    {
        var selectedCols = GetSelectedColumnsCallback?.Invoke();
        if (selectedCols != null && selectedCols.Count > 0)
        {
            DeleteColumns(selectedCols);
        }
    }

    [RelayCommand]
    public void GenerateDeleteQuery()
    {
        var selectedRows = GetSelectedRowsCallback?.Invoke();
        if (selectedRows != null && selectedRows.Count > 1)
        {
            string batchQuery = BuildBatchDeleteQuery(selectedRows);
            InsertQueryToEditorCallback?.Invoke(batchQuery);
            LastQueryMessage = $"Generated DELETE query for {selectedRows.Count} rows in SQL editor";
            HasLastQueryMessage = true;
            return;
        }

        var selectedCols = GetSelectedColumnsCallback?.Invoke();
        if (selectedCols != null && selectedCols.Count > 1 && (selectedRows == null || selectedRows.Count <= 1))
        {
            string dropQuery = BuildDropColumnsQuery(selectedCols);
            InsertQueryToEditorCallback?.Invoke(dropQuery);
            LastQueryMessage = $"Generated ALTER TABLE DROP COLUMN query for {selectedCols.Count} columns";
            HasLastQueryMessage = true;
            return;
        }

        var targetRow = selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        if (targetRow == null) return;

        string query = BuildDeleteQuery(targetRow);
        InsertQueryToEditorCallback?.Invoke(query);
        LastQueryMessage = "Generated DELETE query in SQL editor";
        HasLastQueryMessage = true;
    }

    public void DeleteRows(IReadOnlyList<QueryResultRow> rows)
    {
        if (rows == null || rows.Count == 0) return;

        if (rows.Count == 1)
        {
            DeleteRow(rows[0]);
            return;
        }

        string query = BuildBatchDeleteQuery(rows);

        if (RequestConfirmation != null)
        {
            RequestConfirmation(
                "Delete Rows Confirmation",
                $"Are you sure you want to permanently delete {rows.Count} rows from table '{CurrentTableName}'? This will execute a DELETE statement against the database.",
                query,
                () => _ = ExecuteBatchDeleteAsync(rows, query)
            );
        }
        else
        {
            _ = ExecuteBatchDeleteAsync(rows, query);
        }
    }

    public async Task<bool> ExecuteBatchDeleteAsync(IReadOnlyList<QueryResultRow> rows, string query)
    {
        bool success = true;
        if (ExecuteQueryCallback != null)
        {
            success = await ExecuteQueryCallback(query);
        }

        if (success)
        {
            foreach (var r in rows)
            {
                Rows.Remove(r);
                _allRows.Remove(r);
            }
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].RowIndex = i + 1;
            }
            TotalRows = Rows.Count;
            InsertQueryToEditorCallback?.Invoke(query);
            LastQueryMessage = $"Deleted {rows.Count} rows: {query}";
            HasLastQueryMessage = true;
            return true;
        }
        else
        {
            LastQueryMessage = "Failed to execute DELETE query against database.";
            HasLastQueryMessage = true;
            return false;
        }
    }

    public string BuildBatchDeleteQuery(IReadOnlyList<QueryResultRow> rows)
    {
        string escapedTable = FormatIdentifier(CurrentTableName, CurrentDatabaseType);
        string pkCol = PrimaryKeyColumns.FirstOrDefault() ?? FindIdColumn(rows[0]);

        if (!string.IsNullOrEmpty(pkCol) && rows.All(r => !string.IsNullOrEmpty(r[pkCol])))
        {
            string escapedPk = FormatIdentifier(pkCol, CurrentDatabaseType);
            var idList = rows.Select(r => FormatLiteralOrNumber(r[pkCol]));
            return $"DELETE FROM {escapedTable} WHERE {escapedPk} IN ({string.Join(", ", idList)});";
        }

        var whereClauses = rows.Select(r => $"({BuildWhereClause(r, CurrentDatabaseType)})");
        return $"DELETE FROM {escapedTable} WHERE {string.Join(" OR ", whereClauses)};";
    }

    private static string FormatLiteralOrNumber(string val)
    {
        if (long.TryParse(val, out _) || double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return val;
        }
        return $"'{val.Replace("'", "''")}'";
    }

    public void DeleteColumns(IReadOnlyList<string> columns)
    {
        if (columns == null || columns.Count == 0) return;

        string query = BuildDropColumnsQuery(columns);

        if (RequestConfirmation != null)
        {
            RequestConfirmation(
                "Delete Columns Confirmation",
                $"Are you sure you want to permanently drop {columns.Count} column(s) ({string.Join(", ", columns)}) from table '{CurrentTableName}'? This will execute an ALTER TABLE statement against the database.",
                query,
                () => _ = ExecuteDropColumnsAsync(columns, query)
            );
        }
        else
        {
            _ = ExecuteDropColumnsAsync(columns, query);
        }
    }

    public async Task<bool> ExecuteDropColumnsAsync(IReadOnlyList<string> columns, string query)
    {
        bool success = true;
        if (ExecuteQueryCallback != null)
        {
            success = await ExecuteQueryCallback(query);
        }

        if (success)
        {
            var colSet = new HashSet<string>(columns, StringComparer.OrdinalIgnoreCase);
            var remaining = (_lastColumns ?? Rows.FirstOrDefault()?.Values.Keys.ToList() ?? new List<string>())
                .Where(c => !colSet.Contains(c))
                .ToList();

            foreach (var r in Rows)
            {
                foreach (var col in columns)
                {
                    r.Values.Remove(col);
                    r.OriginalValues.Remove(col);
                }
            }

            foreach (var r in _allRows)
            {
                foreach (var col in columns)
                {
                    r.Values.Remove(col);
                    r.OriginalValues.Remove(col);
                }
            }

            SetResults(remaining, Rows.ToList(), ExecutionTime);
            InsertQueryToEditorCallback?.Invoke(query);
            LastQueryMessage = $"Dropped {columns.Count} column(s): {string.Join(", ", columns)}";
            HasLastQueryMessage = true;
            return true;
        }
        else
        {
            LastQueryMessage = "Failed to drop column(s) from database.";
            HasLastQueryMessage = true;
            return false;
        }
    }

    public string BuildDropColumnsQuery(IReadOnlyList<string> columns)
    {
        string escapedTable = FormatIdentifier(CurrentTableName, CurrentDatabaseType);
        var escapedCols = columns.Select(c => FormatIdentifier(c, CurrentDatabaseType)).ToList();

        return CurrentDatabaseType.ToUpperInvariant() switch
        {
            "MSSQL" => $"ALTER TABLE {escapedTable} DROP COLUMN {string.Join(", ", escapedCols)};",
            "POSTGRESQL" or "MYSQL" => $"ALTER TABLE {escapedTable} {string.Join(", ", escapedCols.Select(c => $"DROP COLUMN {c}"))};",
            "SQLITE" => string.Join("\n", escapedCols.Select(c => $"ALTER TABLE {escapedTable} DROP COLUMN {c};")),
            _ => $"ALTER TABLE {escapedTable} DROP COLUMN {string.Join(", ", escapedCols)};"
        };
    }

    public void DeleteRow(QueryResultRow? row)
    {
        var targetRow = row ?? SelectedRow ?? Rows.FirstOrDefault();
        if (targetRow == null) return;

        string query = BuildDeleteQuery(targetRow);

        if (RequestConfirmation != null)
        {
            RequestConfirmation(
                "Delete Row Confirmation",
                $"Are you sure you want to permanently delete this row from table '{CurrentTableName}'? This will execute a DELETE statement against the database.",
                query,
                () => _ = ExecuteDeleteAsync(targetRow, query)
            );
        }
        else
        {
            _ = ExecuteDeleteAsync(targetRow, query);
        }
    }

    public async Task<bool> ExecuteDeleteAsync(QueryResultRow row, string query)
    {
        bool success = true;
        if (ExecuteQueryCallback != null)
        {
            success = await ExecuteQueryCallback(query);
        }

        if (success)
        {
            Rows.Remove(row);
            for (int i = 0; i < Rows.Count; i++)
            {
                Rows[i].RowIndex = i + 1;
            }
            TotalRows = Rows.Count;
            InsertQueryToEditorCallback?.Invoke(query);
            LastQueryMessage = $"Row deleted: {query}";
            HasLastQueryMessage = true;
            return true;
        }
        else
        {
            LastQueryMessage = "Failed to execute DELETE query against database.";
            HasLastQueryMessage = true;
            return false;
        }
    }

    public string BuildDeleteQuery(QueryResultRow row)
    {
        string escapedTable = FormatIdentifier(CurrentTableName, CurrentDatabaseType);
        string whereClause = BuildWhereClause(row, CurrentDatabaseType);
        return $"DELETE FROM {escapedTable} WHERE {whereClause};";
    }

    public string BuildWhereClause(QueryResultRow row, string dbType)
    {
        if (PrimaryKeyColumns.Count > 0)
        {
            var pkConditions = new List<string>();
            foreach (var pk in PrimaryKeyColumns)
            {
                string val = row[pk];
                if (!string.IsNullOrEmpty(val))
                {
                    pkConditions.Add(FormatCondition(pk, val, dbType));
                }
            }
            if (pkConditions.Count > 0)
            {
                return string.Join(" AND ", pkConditions);
            }
        }

        string idCol = FindIdColumn(row);
        if (!string.IsNullOrEmpty(idCol) && !string.IsNullOrEmpty(row[idCol]))
        {
            return FormatCondition(idCol, row[idCol], dbType);
        }

        if (row.Id > 0)
        {
            return $"{FormatIdentifier("Id", dbType)} = {row.Id}";
        }

        var conditions = new List<string>();
        foreach (var kvp in row.Values)
        {
            if (string.Equals(kvp.Key, "Row #", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.Equals(kvp.Key, "RowIndex", StringComparison.OrdinalIgnoreCase)) continue;
            if (string.IsNullOrEmpty(kvp.Value)) continue;

            conditions.Add(FormatCondition(kvp.Key, kvp.Value, dbType));
            if (conditions.Count >= 4) break;
        }

        if (conditions.Count > 0)
        {
            return string.Join(" AND ", conditions);
        }

        return "1 = 1";
    }

    private static string FormatCondition(string col, string val, string dbType)
    {
        string escapedCol = FormatIdentifier(col, dbType);
        if (string.Equals(val, "NULL", StringComparison.OrdinalIgnoreCase))
        {
            return $"{escapedCol} IS NULL";
        }
        if (long.TryParse(val, out _) || double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return $"{escapedCol} = {val}";
        }
        return $"{escapedCol} = '{val.Replace("'", "''")}'";
    }

    public static string FindIdColumn(QueryResultRow row)
    {
        foreach (var key in row.Values.Keys)
        {
            if (string.Equals(key, "id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "user_id", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(key, "userid", StringComparison.OrdinalIgnoreCase) ||
                key.EndsWith("_id", StringComparison.OrdinalIgnoreCase) ||
                (key.EndsWith("id", StringComparison.OrdinalIgnoreCase) && key.Length > 2))
            {
                return key;
            }
        }
        return row.Id > 0 ? "Id" : string.Empty;
    }

    public static string FormatIdentifier(string identifier, string dbType)
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

    [RelayCommand]
    public void DismissMessage()
    {
        HasLastQueryMessage = false;
        LastQueryMessage = string.Empty;
    }

    [RelayCommand]
    public async Task Refresh()
    {
        IsRefreshing = true;
        try
        {
            if (RefreshCallback != null)
            {
                await RefreshCallback();
            }
            else
            {
                ResetToSampleData();
            }
        }
        catch (Exception ex)
        {
            LastQueryMessage = $"Refresh failed: {ex.Message}";
            HasLastQueryMessage = true;
        }
        finally
        {
            IsRefreshing = false;
        }
    }

    public void ResetToSampleData()
    {
        var sampleRows = new List<QueryResultRow>
        {
            new QueryResultRow { RowIndex = 1, Id = 101, Name = "Alice Johnson", Email = "alice@example.com", Role = "DBA", Status = "Active", CreatedAt = "2026-01-15 08:30:00" },
            new QueryResultRow { RowIndex = 2, Id = 102, Name = "Bob Smith", Email = "bob@example.com", Role = "Developer", Status = "Active", CreatedAt = "2026-02-10 11:15:22" },
            new QueryResultRow { RowIndex = 3, Id = 103, Name = "Charlie Davis", Email = "charlie@example.com", Role = "Analyst", Status = "Pending", CreatedAt = "2026-03-01 14:45:00" },
            new QueryResultRow { RowIndex = 4, Id = 104, Name = "Dana White", Email = "dana@example.com", Role = "Admin", Status = "Active", CreatedAt = "2026-03-12 09:20:11" },
            new QueryResultRow { RowIndex = 5, Id = 105, Name = "Evan Wright", Email = "evan@example.com", Role = "Developer", Status = "Inactive", CreatedAt = "2026-04-05 16:00:45" }
        };

        foreach (var r in sampleRows)
        {
            r.OriginalValues["Id"] = r.Id.ToString();
            r.OriginalValues["Name"] = r.Name;
            r.OriginalValues["Email"] = r.Email;
            r.OriginalValues["Role"] = r.Role;
            r.OriginalValues["Status"] = r.Status;
            r.OriginalValues["CreatedAt"] = r.CreatedAt;
        }

        _allRows.Clear();
        _allRows.AddRange(sampleRows);

        Rows.Clear();
        foreach (var r in sampleRows)
        {
            Rows.Add(r);
        }

        if (!string.IsNullOrWhiteSpace(SearchFilter))
        {
            ApplyFilter(SearchFilter);
        }
        else
        {
            TotalRows = Rows.Count;
        }

        ExecutionTime = "0 ms";
        LastQueryMessage = $"Table refreshed ({TotalRows} rows)";
        HasLastQueryMessage = true;
    }

    [RelayCommand]
    private Task CopyCell() => WriteClipboardAsync(CopySelection?.Invoke());

    [RelayCommand]
    private Task CopyRow() => WriteClipboardAsync(CopyFocusedRow?.Invoke());

    private static async Task WriteClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        if (Application.Current?.ApplicationLifetime is IClassicDesktopStyleApplicationLifetime desktop
            && desktop.MainWindow?.Clipboard is { } clipboard)
        {
            await clipboard.SetTextAsync(text);
        }
    }
}
