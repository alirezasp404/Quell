using Quell.Services.DatabaseProviders;
using Quell.Services.DemoData;
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

namespace Quell.ViewModels;

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
    private string _course = string.Empty;

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
                "COURSE" => Course,
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
                case "COURSE":
                    Course = value;
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
            OnPropertyChanged(nameof(Course));
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

    public Func<bool>? CheckConnectionCallback { get; set; }

    private bool HasLiveConnection => IsConnectedToDatabase || (CheckConnectionCallback?.Invoke() ?? false);

    private readonly List<QueryResultRow> _allRows = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    private int _totalRows;

    [ObservableProperty]
    private string _executionTime = string.Empty;

    [ObservableProperty]
    private string _targetDatabaseName = "(No Connection)";

    [ObservableProperty]
    private string _currentTableName = string.Empty;

    [ObservableProperty]
    private DatabaseType _currentDatabaseType = DatabaseType.MSSQL;

    [ObservableProperty]
    private bool _isConnectedToDatabase;

    [ObservableProperty]
    private string _primaryKeyColumnName = string.Empty;

    [ObservableProperty]
    private bool _hasPrimaryKey;

    public bool HasRows => Rows != null && Rows.Count > 0;

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

    public HashSet<string> PrimaryKeyColumns { get; } = new(StringComparer.OrdinalIgnoreCase);

    public bool IsColumnPrimaryKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        return PrimaryKeyColumns.Contains(columnName);
    }

    public Dictionary<string, ForeignKeyRelation> ForeignKeyRelations { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Func<string, string, string?, string?, Task>? NavigateToForeignKeyCallback { get; set; }

    public bool IsColumnForeignKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        return ForeignKeyRelations.ContainsKey(columnName);
    }

    public ForeignKeyRelation? GetForeignKeyRelation(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return null;
        return ForeignKeyRelations.TryGetValue(columnName, out var rel) ? rel : null;
    }

    public void ClearMetadata()
    {
        PrimaryKeyColumns.Clear();
        PrimaryKeyColumnName = string.Empty;
        HasPrimaryKey = false;
        ForeignKeyRelations.Clear();
    }

    public void SetForeignKeyRelations(IEnumerable<ForeignKeyRelation> fks)
    {
        ApplyForeignKeyRelations(fks);

        if (_lastColumns != null)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    public void SetTableMetadata(IEnumerable<string> pks, IEnumerable<ForeignKeyRelation> fks)
    {
        ApplyPrimaryKeys(pks);
        ApplyForeignKeyRelations(fks);

        if (_lastColumns != null)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    private void ApplyForeignKeyRelations(IEnumerable<ForeignKeyRelation> fks)
    {
        ForeignKeyRelations.Clear();
        foreach (var fk in fks)
        {
            if (!string.IsNullOrWhiteSpace(fk.ColumnName))
            {
                ForeignKeyRelations[fk.ColumnName] = fk;
            }
        }
    }

    public record TableNavigationState(
        string TableName,
        string ExecutedQuery,
        IReadOnlyList<string> Columns,
        List<QueryResultRow> Rows,
        string ExecutionTime,
        string SourceDescription,
        Dictionary<string, ForeignKeyRelation> FkRelations,
        HashSet<string> PrimaryKeys);

    private readonly Stack<TableNavigationState> _navigationHistory = new();

    [ObservableProperty]
    private bool _canNavigateBack;

    [ObservableProperty]
    private string _navigateBackText = string.Empty;

    [ObservableProperty]
    private string _navigationBreadcrumbText = string.Empty;

    public ForeignKeyInspectorModalViewModel ForeignKeyInspectorModal { get; } = new();

    public void PushNavigationState(string sourceDescription)
    {
        if (Rows.Count > 0 && !string.IsNullOrEmpty(CurrentTableName))
        {
            var rowsCopy = new List<QueryResultRow>();
            foreach (var r in Rows)
            {
                var newR = new QueryResultRow
                {
                    RowIndex = r.RowIndex,
                    Id = r.Id,
                    Name = r.Name,
                    Email = r.Email,
                    Role = r.Role,
                    Course = r.Course,
                    Status = r.Status,
                    CreatedAt = r.CreatedAt
                };
                foreach (var kvp in r.Values) newR.Values[kvp.Key] = kvp.Value;
                foreach (var kvp in r.OriginalValues) newR.OriginalValues[kvp.Key] = kvp.Value;
                rowsCopy.Add(newR);
            }

            var fksCopy = new Dictionary<string, ForeignKeyRelation>(ForeignKeyRelations, StringComparer.OrdinalIgnoreCase);
            var pksCopy = new HashSet<string>(PrimaryKeyColumns, StringComparer.OrdinalIgnoreCase);

            var state = new TableNavigationState(
                CurrentTableName,
                LastExecutedQuery,
                LastColumns != null ? new List<string>(LastColumns) : new List<string>(),
                rowsCopy,
                ExecutionTime,
                sourceDescription,
                fksCopy,
                pksCopy);

            _navigationHistory.Push(state);
            CanNavigateBack = true;
            NavigateBackText = $"Back to {CurrentTableName}";
            NavigationBreadcrumbText = sourceDescription;
        }
    }

    [RelayCommand]
    public void NavigateBack()
    {
        if (_navigationHistory.Count > 0)
        {
            var prev = _navigationHistory.Pop();
            CurrentTableName = prev.TableName;
            LastExecutedQuery = prev.ExecutedQuery;
            SetResults(prev.Columns, prev.Rows, prev.ExecutionTime);
            SetPrimaryKeys(prev.PrimaryKeys);
            SetForeignKeyRelations(prev.FkRelations.Values);

            LastQueryMessage = $"Returned to {prev.TableName} ({prev.Rows.Count} rows)";
            HasLastQueryMessage = true;

            CanNavigateBack = _navigationHistory.Count > 0;
            if (CanNavigateBack)
            {
                var top = _navigationHistory.Peek();
                NavigateBackText = $"Back to {top.TableName}";
                NavigationBreadcrumbText = top.SourceDescription;
            }
            else
            {
                NavigateBackText = string.Empty;
                NavigationBreadcrumbText = string.Empty;
            }
        }
    }

    public async Task InspectForeignKeyAsync(string columnName, string? cellValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue) || cellValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rel = GetForeignKeyRelation(columnName);
        if (rel == null) return;

        await ForeignKeyInspectorModal.OpenAsync(
            sourceTable: CurrentTableName,
            sourceColumn: columnName,
            sourceValue: cellValue,
            targetTable: rel.TargetTable,
            targetColumn: rel.TargetColumn,
            dbType: CurrentDatabaseType,
            constraintName: rel.ConstraintName);
    }

    public async Task NavigateToForeignKeyAsync(string columnName, string? cellValue)
    {
        if (string.IsNullOrWhiteSpace(cellValue) || cellValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        var rel = GetForeignKeyRelation(columnName);
        if (rel == null) return;

        if (NavigateToForeignKeyCallback != null)
        {
            await NavigateToForeignKeyCallback.Invoke(rel.TargetTable, rel.TargetColumn, cellValue, columnName);
        }
    }

    public void SetPrimaryKeys(IEnumerable<string> pks)
    {
        ApplyPrimaryKeys(pks);

        if (_lastColumns != null)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    private void ApplyPrimaryKeys(IEnumerable<string> pks)
    {
        PrimaryKeyColumns.Clear();
        foreach (var pk in pks)
        {
            if (!string.IsNullOrWhiteSpace(pk))
            {
                PrimaryKeyColumns.Add(pk);
            }
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
    }

    private IReadOnlyList<string>? _lastColumns;
    public IReadOnlyList<string>? LastColumns => _lastColumns;

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

    public ObservableCollection<QueryResultRow> Rows { get; } = new();

    public QueryResultTableViewModel()
    {
        EditCellModal.OnUpdatedCallback = msg =>
        {
            LastQueryMessage = msg;
            HasLastQueryMessage = true;
        };
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

            var matchingPks = PrimaryKeyColumns
                .Where(pk => columns.Contains(pk, StringComparer.OrdinalIgnoreCase))
                .ToList();
            if (matchingPks.Count > 0)
            {
                HasPrimaryKey = true;
                PrimaryKeyColumnName = string.Join(", ", matchingPks);
            }
            else
            {
                HasPrimaryKey = false;
                PrimaryKeyColumnName = string.Empty;
            }

            OnPropertyChanged(nameof(HasRows));
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
            EditCellModal.OpenBatch(selectedRows, cols, CurrentTableName, CurrentDatabaseType, PrimaryKeyColumns);
            return;
        }

        var targetRow = row ?? selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        if (targetRow == null) return;

        string col = string.IsNullOrWhiteSpace(columnName) || columnName == "ROW #" 
            ? (selectedCols?.FirstOrDefault() ?? "Name") 
            : columnName;
        EditCellModal.Open(targetRow, col, CurrentTableName, CurrentDatabaseType, PrimaryKeyColumns);
    }

    [RelayCommand]
    public void OpenEditSelectedCell()
    {
        var selectedRows = GetSelectedRowsCallback?.Invoke();
        var selectedCols = GetSelectedColumnsCallback?.Invoke();

        if (selectedRows != null && selectedRows.Count > 1)
        {
            var cols = selectedCols != null && selectedCols.Count > 0 ? selectedCols : new[] { "Name" };
            EditCellModal.OpenBatch(selectedRows, cols, CurrentTableName, CurrentDatabaseType, PrimaryKeyColumns);
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
            var idList = rows.Select(r => FormatLiteralOrNumber(r[pkCol], CurrentDatabaseType));
            return $"DELETE FROM {escapedTable} WHERE {escapedPk} IN ({string.Join(", ", idList)});";
        }

        var whereClauses = rows.Select(r => $"({BuildWhereClause(r, CurrentDatabaseType)})");
        return $"DELETE FROM {escapedTable} WHERE {string.Join(" OR ", whereClauses)};";
    }

    private static string FormatLiteralOrNumber(string val, DatabaseType dbType)
    {
        if (long.TryParse(val, out _) || double.TryParse(val, System.Globalization.NumberStyles.Float, System.Globalization.CultureInfo.InvariantCulture, out _))
        {
            return val;
        }
        return EditCellModalViewModel.FormatLiteral(val, dbType);
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

        return CurrentDatabaseType switch
        {
            DatabaseType.MSSQL => $"ALTER TABLE {escapedTable} DROP COLUMN {string.Join(", ", escapedCols)};",
            DatabaseType.PostgreSQL or DatabaseType.MySQL => $"ALTER TABLE {escapedTable} {string.Join(", ", escapedCols.Select(c => $"DROP COLUMN {c}"))};",
            DatabaseType.SQLite => string.Join("\n", escapedCols.Select(c => $"ALTER TABLE {escapedTable} DROP COLUMN {c};")),
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

    public string BuildWhereClause(QueryResultRow row, DatabaseType dbType)
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

    private static string FormatCondition(string col, string val, DatabaseType dbType)
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
        return $"{escapedCol} = {EditCellModalViewModel.FormatLiteral(val, dbType)}";
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

    public static string FormatIdentifier(string identifier, DatabaseType dbType)
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

        return dbType switch
        {
            DatabaseType.MSSQL => $"[{clean}]",
            DatabaseType.MySQL => $"`{clean}`",
            DatabaseType.PostgreSQL or DatabaseType.SQLite => $"\"{clean}\"",
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
            else if (!HasLiveConnection)
            {
                ResetToSampleData();
            }
            else
            {
                LastQueryMessage = "No active refresh query configured.";
                HasLastQueryMessage = true;
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

    public void LoadSampleReferencedTable(string tableName, string? filterColumn = null, string? filterValue = null)
    {
        if (HasLiveConnection) return;

        var (dataset, matchedRow) = DemoDataProvider.GetSampleTable(tableName, filterColumn, filterValue);
        CurrentTableName = dataset.TableName;
        ClearMetadata();
        SetResults(dataset.Columns, dataset.Rows, "9 ms");
        SetPrimaryKeys(dataset.PrimaryKeys);
        SetForeignKeyRelations(dataset.ForeignKeys);

        if (matchedRow != null)
        {
            SelectedRow = matchedRow;
        }
    }

    public void ResetToSampleData()
    {
        if (HasLiveConnection) return;

        var dataset = DemoDataProvider.GetDefaultUsersDataset();
        CurrentTableName = dataset.TableName;
        ClearMetadata();
        SetResults(dataset.Columns, dataset.Rows, "0 ms");
        SetPrimaryKeys(dataset.PrimaryKeys);
        SetForeignKeyRelations(dataset.ForeignKeys);
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
