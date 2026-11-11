using Quell.Services.DatabaseProviders;
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
    private string _currentDatabaseType = "MSSQL";

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

    public HashSet<string> PrimaryKeyColumns { get; } = new(StringComparer.OrdinalIgnoreCase) { "Id", "ID" };

    public bool IsColumnPrimaryKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        return PrimaryKeyColumns.Contains(columnName) || IsProbablePrimaryKey(columnName, CurrentTableName);
    }

    public static bool IsProbablePrimaryKey(string? columnName, string? tableName = null)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        string c = columnName.Trim().ToLowerInvariant();
        if (c == "id" || c == "rowid" || c == "uuid" || c == "guid" || c == "pk")
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

        return false;
    }

    public Dictionary<string, ForeignKeyRelation> ForeignKeyRelations { get; } = new(StringComparer.OrdinalIgnoreCase);

    public Func<string, string, string?, string?, Task>? NavigateToForeignKeyCallback { get; set; }

    public bool IsColumnForeignKey(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return false;
        if (ForeignKeyRelations.ContainsKey(columnName)) return true;
        return DetectProbableForeignKey(columnName, CurrentTableName) != null;
    }

    public ForeignKeyRelation? GetForeignKeyRelation(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return null;
        if (ForeignKeyRelations.TryGetValue(columnName, out var rel)) return rel;
        return DetectProbableForeignKey(columnName, CurrentTableName);
    }

    public void SetForeignKeyRelations(IEnumerable<ForeignKeyRelation> fks)
    {
        ForeignKeyRelations.Clear();
        foreach (var fk in fks)
        {
            if (!string.IsNullOrWhiteSpace(fk.ColumnName))
            {
                ForeignKeyRelations[fk.ColumnName] = fk;
            }
        }

        if (_lastColumns != null)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    public static ForeignKeyRelation? DetectProbableForeignKey(string? columnName, string? tableName = null)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return null;
        string c = columnName.Trim();
        string lower = c.ToLowerInvariant();

        // Avoid detecting table PK as FK
        if (QueryResultTableViewModel.IsProbablePrimaryKey(columnName, tableName))
        {
            if (!string.IsNullOrEmpty(tableName))
            {
                string t = tableName.Trim('[', ']', '"', '`').ToLowerInvariant();
                if (t.Contains('.')) t = t.Split('.')[1];
                string singular = t.EndsWith("s") ? t.Substring(0, t.Length - 1) : t;
                if (lower == "id" || lower == $"{t}_id" || lower == $"{t}id" || lower == $"{singular}_id" || lower == $"{singular}id")
                    return null;
            }
            else if (lower == "id" || lower == "pk" || lower == "rowid")
            {
                return null;
            }
        }

        // Course and User relations
        if (lower == "course" || lower == "course_name" || lower == "coursetitle" || lower == "course_title")
        {
            if (!string.IsNullOrEmpty(tableName) && tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                return new ForeignKeyRelation { ColumnName = c, TargetTable = "Courses", TargetColumn = "Title" };
            }
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Users", TargetColumn = "Course" };
        }
        if (lower == "course_id" || lower == "courseid")
        {
            if (!string.IsNullOrEmpty(tableName) && tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
            {
                return new ForeignKeyRelation { ColumnName = c, TargetTable = "Courses", TargetColumn = "Id" };
            }
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Users", TargetColumn = "Id" };
        }

        if (lower == "user" || lower == "user_name" || lower == "username" || lower == "student" || lower == "student_name")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Users", TargetColumn = "Name" };
        }
        if (lower == "user_id" || lower == "userid" || lower == "student_id" || lower == "studentid" || lower == "customer_id" || lower == "customerid")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Users", TargetColumn = "Id" };
        }
        if (lower == "order_id" || lower == "orderid")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Orders", TargetColumn = "Id" };
        }
        if (lower == "product_id" || lower == "productid")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Products", TargetColumn = "Id" };
        }

        if (lower == "role" || lower == "role_name")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Roles", TargetColumn = "Title" };
        }
        if (lower == "role_id" || lower == "roleid")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Roles", TargetColumn = "Id" };
        }
        if (lower == "department" || lower == "department_name")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Departments", TargetColumn = "Name" };
        }
        if (lower == "department_id" || lower == "departmentid")
        {
            return new ForeignKeyRelation { ColumnName = c, TargetTable = "Departments", TargetColumn = "Id" };
        }

        if (lower.EndsWith("_id") && lower.Length > 3)
        {
            string entity = lower.Substring(0, lower.Length - 3);
            string targetTable = entity.EndsWith("y") && !entity.EndsWith("ay") && !entity.EndsWith("ey")
                ? entity.Substring(0, entity.Length - 1) + "ies"
                : entity + "s";
            string properTable = char.ToUpperInvariant(targetTable[0]) + targetTable.Substring(1);
            return new ForeignKeyRelation { ColumnName = c, TargetTable = properTable, TargetColumn = "Id" };
        }

        if (lower.EndsWith("id") && lower.Length > 2 && char.IsUpper(c[0]))
        {
            string entity = lower.Substring(0, lower.Length - 2);
            string targetTable = entity.EndsWith("y") && !entity.EndsWith("ay") && !entity.EndsWith("ey")
                ? entity.Substring(0, entity.Length - 1) + "ies"
                : entity + "s";
            string properTable = char.ToUpperInvariant(targetTable[0]) + targetTable.Substring(1);
            return new ForeignKeyRelation { ColumnName = c, TargetTable = properTable, TargetColumn = "Id" };
        }

        return null;
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

                        foreach (var col in columns)
            {
                if (!ForeignKeyRelations.ContainsKey(col))
                {
                    var probableFk = DetectProbableForeignKey(col, CurrentTableName);
                    if (probableFk != null)
                    {
                        ForeignKeyRelations[col] = probableFk;
                    }
                }
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

    private static string FormatLiteralOrNumber(string val, string dbType)
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

        public void LoadSampleReferencedTable(string tableName, string? filterColumn = null, string? filterValue = null)
    {
        CurrentTableName = tableName;
        if (tableName.Equals("Roles", StringComparison.OrdinalIgnoreCase))
        {
            var roleRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 1, Name = "DBA", Email = "dba@company.internal", Role = "SUPERUSER", Status = "Active", CreatedAt = "2025-01-01 00:00:00" },
                new QueryResultRow { RowIndex = 2, Id = 2, Name = "Developer", Email = "dev@company.internal", Role = "READ_WRITE", Status = "Active", CreatedAt = "2025-01-10 10:00:00" },
                new QueryResultRow { RowIndex = 3, Id = 3, Name = "Analyst", Email = "analyst@company.internal", Role = "READ_ONLY", Status = "Active", CreatedAt = "2025-01-15 12:30:00" },
                new QueryResultRow { RowIndex = 4, Id = 4, Name = "Admin", Email = "admin@company.internal", Role = "FULL_CONTROL", Status = "Active", CreatedAt = "2025-01-01 00:00:00" }
            };

            foreach (var r in roleRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["TITLE"] = r.Name;
                r.Values["DESCRIPTION"] = r.Id switch
                {
                    1 => "Database Administrator with full cluster access",
                    2 => "Application Software Engineer",
                    3 => "Business & Data Intelligence Analyst",
                    4 => "System Administrator & Security Officer",
                    _ => "Standard Role"
                };
                r.Values["PERMISSIONS"] = r.Role;
                r.Values["CREATED AT"] = r.CreatedAt;

                foreach (var kvp in r.Values)
                {
                    r.OriginalValues[kvp.Key] = kvp.Value;
                }
            }

            var cols = new List<string> { "ID", "TITLE", "DESCRIPTION", "PERMISSIONS", "CREATED AT" };
            SetResults(cols, roleRows, "12 ms");
            SetPrimaryKeys(new[] { "ID" });
            ForeignKeyRelations.Clear();
            ColumnsChanged?.Invoke(cols);

            if (!string.IsNullOrWhiteSpace(filterValue))
            {
                var match = roleRows.FirstOrDefault(r => r.Values.TryGetValue("TITLE", out var t) && t.Equals(filterValue, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectedRow = match;
                }
            }
        }
        else if (tableName.Equals("Departments", StringComparison.OrdinalIgnoreCase))
        {
            var deptRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 1, Name = "Engineering", Email = "eng@company.internal", Role = "Tech Park A", Status = "Active", CreatedAt = "$2,500,000" },
                new QueryResultRow { RowIndex = 2, Id = 2, Name = "Data Science", Email = "data@company.internal", Role = "Tech Park B", Status = "Active", CreatedAt = "$1,800,000" },
                new QueryResultRow { RowIndex = 3, Id = 3, Name = "IT Operations", Email = "ops@company.internal", Role = "Main Tower", Status = "Active", CreatedAt = "$1,200,000" }
            };

            foreach (var r in deptRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["NAME"] = r.Name;
                r.Values["BUILDING"] = r.Role;
                r.Values["BUDGET"] = r.CreatedAt;

                foreach (var kvp in r.Values)
                {
                    r.OriginalValues[kvp.Key] = kvp.Value;
                }
            }

            var cols = new List<string> { "ID", "NAME", "BUILDING", "BUDGET" };
            SetResults(cols, deptRows, "10 ms");
            SetPrimaryKeys(new[] { "ID" });
            ForeignKeyRelations.Clear();
            ColumnsChanged?.Invoke(cols);
        }
        else if (tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            var userRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 101, Name = "Alice Johnson", Email = "alice@example.com", Role = "DBA", Course = "C# & Avalonia UI", Status = "Active", CreatedAt = "2026-01-15 08:30:00" },
                new QueryResultRow { RowIndex = 2, Id = 102, Name = "Bob Smith", Email = "bob@example.com", Role = "Developer", Course = "SQL Server Architecture", Status = "Active", CreatedAt = "2026-02-10 11:15:22" },
                new QueryResultRow { RowIndex = 3, Id = 103, Name = "Charlie Davis", Email = "charlie@example.com", Role = "Analyst", Course = "Database Performance Tuning", Status = "Pending", CreatedAt = "2026-03-01 14:45:00" },
                new QueryResultRow { RowIndex = 4, Id = 104, Name = "Dana White", Email = "dana@example.com", Role = "Admin", Course = "PostgreSQL Administration", Status = "Active", CreatedAt = "2026-03-12 09:20:11" },
                new QueryResultRow { RowIndex = 5, Id = 105, Name = "Evan Wright", Email = "evan@example.com", Role = "Developer", Course = "Cloud Databases & Docker", Status = "Inactive", CreatedAt = "2026-04-05 16:00:45" }
            };

            foreach (var r in userRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["NAME"] = r.Name;
                r.Values["EMAIL"] = r.Email;
                r.Values["ROLE"] = r.Role;
                r.Values["COURSE"] = r.Course;
                r.Values["STATUS"] = r.Status;
                r.Values["CREATED AT"] = r.CreatedAt;

                foreach (var kvp in r.Values)
                {
                    r.OriginalValues[kvp.Key] = kvp.Value;
                }
            }

            var cols = new List<string> { "ID", "NAME", "EMAIL", "ROLE", "COURSE", "STATUS", "CREATED AT" };
            SetResults(cols, userRows, "11 ms");
            SetPrimaryKeys(new[] { "ID" });
            ForeignKeyRelations.Clear();
            ForeignKeyRelations["Role"] = new ForeignKeyRelation { ColumnName = "ROLE", TargetTable = "Roles", TargetColumn = "Title" };
            ForeignKeyRelations["Course"] = new ForeignKeyRelation { ColumnName = "COURSE", TargetTable = "Courses", TargetColumn = "Title" };
            ColumnsChanged?.Invoke(cols);

            if (!string.IsNullOrWhiteSpace(filterValue))
            {
                var match = userRows.FirstOrDefault(r => 
                    r.Id.ToString() == filterValue ||
                    r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Course.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    (r.Values.TryGetValue("COURSE", out var cVal) && cVal.Equals(filterValue, StringComparison.OrdinalIgnoreCase))
                );
                if (match != null)
                {
                    SelectedRow = match;
                }
            }
        }
        else if (tableName.Equals("Courses", StringComparison.OrdinalIgnoreCase))
        {
            var courseRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 1, Name = "C# & Avalonia UI", Email = "Alice Johnson", Role = "101", Course = "Active", Status = "Active", CreatedAt = "40 Hours" },
                new QueryResultRow { RowIndex = 2, Id = 2, Name = "SQL Server Architecture", Email = "Bob Smith", Role = "102", Course = "Active", Status = "Active", CreatedAt = "50 Hours" },
                new QueryResultRow { RowIndex = 3, Id = 3, Name = "Database Performance Tuning", Email = "Charlie Davis", Role = "103", Course = "In Progress", Status = "In Progress", CreatedAt = "35 Hours" },
                new QueryResultRow { RowIndex = 4, Id = 4, Name = "PostgreSQL Administration", Email = "Dana White", Role = "104", Course = "Active", Status = "Active", CreatedAt = "45 Hours" },
                new QueryResultRow { RowIndex = 5, Id = 5, Name = "Cloud Databases & Docker", Email = "Evan Wright", Role = "105", Course = "Completed", Status = "Completed", CreatedAt = "30 Hours" }
            };

            foreach (var r in courseRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["TITLE"] = r.Name;
                r.Values["USER_ID"] = r.Role;
                r.Values["STUDENT_NAME"] = r.Email;
                r.Values["DURATION"] = r.CreatedAt;
                r.Values["STATUS"] = r.Status;

                foreach (var kvp in r.Values)
                {
                    r.OriginalValues[kvp.Key] = kvp.Value;
                }
            }

            var cols = new List<string> { "ID", "TITLE", "USER_ID", "STUDENT_NAME", "DURATION", "STATUS" };
            SetResults(cols, courseRows, "9 ms");
            SetPrimaryKeys(new[] { "ID" });
            ForeignKeyRelations.Clear();
            ForeignKeyRelations["USER_ID"] = new ForeignKeyRelation { ColumnName = "USER_ID", TargetTable = "Users", TargetColumn = "Id" };
            ForeignKeyRelations["STUDENT_NAME"] = new ForeignKeyRelation { ColumnName = "STUDENT_NAME", TargetTable = "Users", TargetColumn = "Name" };
            ForeignKeyRelations["TITLE"] = new ForeignKeyRelation { ColumnName = "TITLE", TargetTable = "Users", TargetColumn = "Course" };
            ColumnsChanged?.Invoke(cols);

            if (!string.IsNullOrWhiteSpace(filterValue))
            {
                var match = courseRows.FirstOrDefault(r =>
                    r.Id.ToString() == filterValue ||
                    r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Email.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Role == filterValue);
                if (match != null)
                {
                    SelectedRow = match;
                }
            }
        }
        else if (tableName.Equals("Orders", StringComparison.OrdinalIgnoreCase))
        {
            var orderRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 5001, Name = "ORD-2026-001", Email = "$249.50", Role = "101", Course = "Alice Johnson", Status = "Completed", CreatedAt = "2026-04-01 10:20:00" },
                new QueryResultRow { RowIndex = 2, Id = 5002, Name = "ORD-2026-002", Email = "$1,120.00", Role = "102", Course = "Bob Smith", Status = "Processing", CreatedAt = "2026-04-02 14:15:30" },
                new QueryResultRow { RowIndex = 3, Id = 5003, Name = "ORD-2026-003", Email = "$75.00", Role = "101", Course = "Alice Johnson", Status = "Completed", CreatedAt = "2026-04-03 09:00:12" },
                new QueryResultRow { RowIndex = 4, Id = 5004, Name = "ORD-2026-004", Email = "$430.80", Role = "103", Course = "Charlie Davis", Status = "Shipped", CreatedAt = "2026-04-04 16:45:00" },
                new QueryResultRow { RowIndex = 5, Id = 5005, Name = "ORD-2026-005", Email = "$89.99", Role = "104", Course = "Dana White", Status = "Delivered", CreatedAt = "2026-04-05 11:30:45" }
            };

            foreach (var r in orderRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["ORDER_NUMBER"] = r.Name;
                r.Values["USER_ID"] = r.Role;
                r.Values["CUSTOMER_NAME"] = r.Course;
                r.Values["TOTAL_AMOUNT"] = r.Email;
                r.Values["STATUS"] = r.Status;
                r.Values["ORDER_DATE"] = r.CreatedAt;

                foreach (var kvp in r.Values)
                {
                    r.OriginalValues[kvp.Key] = kvp.Value;
                }
            }

            var cols = new List<string> { "ID", "ORDER_NUMBER", "USER_ID", "CUSTOMER_NAME", "TOTAL_AMOUNT", "STATUS", "ORDER_DATE" };
            SetResults(cols, orderRows, "7 ms");
            SetPrimaryKeys(new[] { "ID" });
            ForeignKeyRelations.Clear();
            ForeignKeyRelations["USER_ID"] = new ForeignKeyRelation { ColumnName = "USER_ID", TargetTable = "Users", TargetColumn = "Id", ConstraintName = "FK_Orders_Users" };
            ForeignKeyRelations["CUSTOMER_NAME"] = new ForeignKeyRelation { ColumnName = "CUSTOMER_NAME", TargetTable = "Users", TargetColumn = "Name", ConstraintName = "FK_Orders_Users_Name" };
            ColumnsChanged?.Invoke(cols);

            if (!string.IsNullOrWhiteSpace(filterValue))
            {
                var match = orderRows.FirstOrDefault(r =>
                    r.Id.ToString() == filterValue ||
                    r.Role == filterValue ||
                    r.Name.Equals(filterValue, StringComparison.OrdinalIgnoreCase) ||
                    r.Course.Equals(filterValue, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    SelectedRow = match;
                }
            }
        }
        else
        {
            var genRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 1, Name = filterValue ?? "Item 1", Email = $"{tableName.ToLower()}@test", Role = "Active", Status = "Standard", CreatedAt = DateTime.Now.ToString("yyyy-MM-dd") }
            };
            genRows[0].Values["ID"] = "1";
            genRows[0].Values["NAME"] = filterValue ?? "Item 1";
            genRows[0].Values["STATUS"] = "Active";
            var cols = new List<string> { "ID", "NAME", "STATUS" };
            SetResults(cols, genRows, "8 ms");
            SetPrimaryKeys(new[] { "ID" });
            ColumnsChanged?.Invoke(cols);
        }
    }


    public void ResetToSampleData()
    {
        var sampleRows = new List<QueryResultRow>
        {
            new QueryResultRow { RowIndex = 1, Id = 101, Name = "Alice Johnson", Email = "alice@example.com", Role = "DBA", Course = "C# & Avalonia UI", Status = "Active", CreatedAt = "2026-01-15 08:30:00" },
            new QueryResultRow { RowIndex = 2, Id = 102, Name = "Bob Smith", Email = "bob@example.com", Role = "Developer", Course = "SQL Server Architecture", Status = "Active", CreatedAt = "2026-02-10 11:15:22" },
            new QueryResultRow { RowIndex = 3, Id = 103, Name = "Charlie Davis", Email = "charlie@example.com", Role = "Analyst", Course = "Database Performance Tuning", Status = "Pending", CreatedAt = "2026-03-01 14:45:00" },
            new QueryResultRow { RowIndex = 4, Id = 104, Name = "Dana White", Email = "dana@example.com", Role = "Admin", Course = "PostgreSQL Administration", Status = "Active", CreatedAt = "2026-03-12 09:20:11" },
            new QueryResultRow { RowIndex = 5, Id = 105, Name = "Evan Wright", Email = "evan@example.com", Role = "Developer", Course = "Cloud Databases & Docker", Status = "Inactive", CreatedAt = "2026-04-05 16:00:45" }
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

        CurrentTableName = "Users";
        PrimaryKeyColumns.Clear();
        PrimaryKeyColumns.Add("Id");
        PrimaryKeyColumnName = "Id";
        HasPrimaryKey = true;
        ForeignKeyRelations.Clear();
        ForeignKeyRelations["Role"] = new ForeignKeyRelation
        {
            ColumnName = "Role",
            TargetTable = "Roles",
            TargetColumn = "Title",
            ConstraintName = "FK_Users_Roles"
        };

        ExecutionTime = "0 ms";
        LastQueryMessage = $"Table refreshed ({TotalRows} rows)";
        HasLastQueryMessage = true;
    }

    public static QueryResultRow? GetSampleRow(string tableName, string targetColumn, string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return null;

        if (tableName.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            var userRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 101, Name = "Alice Johnson", Email = "alice@example.com", Role = "DBA", Course = "C# & Avalonia UI", Status = "Active", CreatedAt = "2026-01-15 08:30:00" },
                new QueryResultRow { RowIndex = 2, Id = 102, Name = "Bob Smith", Email = "bob@example.com", Role = "Developer", Course = "SQL Server Architecture", Status = "Active", CreatedAt = "2026-02-10 11:15:22" },
                new QueryResultRow { RowIndex = 3, Id = 103, Name = "Charlie Davis", Email = "charlie@example.com", Role = "Analyst", Course = "Database Performance Tuning", Status = "Pending", CreatedAt = "2026-03-01 14:45:00" },
                new QueryResultRow { RowIndex = 4, Id = 104, Name = "Dana White", Email = "dana@example.com", Role = "Admin", Course = "PostgreSQL Administration", Status = "Active", CreatedAt = "2026-03-12 09:20:11" },
                new QueryResultRow { RowIndex = 5, Id = 105, Name = "Evan Wright", Email = "evan@example.com", Role = "Developer", Course = "Cloud Databases & Docker", Status = "Inactive", CreatedAt = "2026-04-05 16:00:45" }
            };
            foreach (var r in userRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["NAME"] = r.Name;
                r.Values["EMAIL"] = r.Email;
                r.Values["ROLE"] = r.Role;
                r.Values["COURSE"] = r.Course;
                r.Values["STATUS"] = r.Status;
                r.Values["CREATED AT"] = r.CreatedAt;
            }
            return userRows.FirstOrDefault(r =>
                r.Id.ToString() == value ||
                r.Name.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                (r.Values.TryGetValue(targetColumn, out var cv) && cv.Equals(value, StringComparison.OrdinalIgnoreCase)));
        }

        if (tableName.Equals("Orders", StringComparison.OrdinalIgnoreCase))
        {
            var orderRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 5001, Name = "ORD-2026-001", Email = "$249.50", Role = "101", Course = "Alice Johnson", Status = "Completed", CreatedAt = "2026-04-01 10:20:00" },
                new QueryResultRow { RowIndex = 2, Id = 5002, Name = "ORD-2026-002", Email = "$1,120.00", Role = "102", Course = "Bob Smith", Status = "Processing", CreatedAt = "2026-04-02 14:15:30" },
                new QueryResultRow { RowIndex = 3, Id = 5003, Name = "ORD-2026-003", Email = "$75.00", Role = "101", Course = "Alice Johnson", Status = "Completed", CreatedAt = "2026-04-03 09:00:12" },
                new QueryResultRow { RowIndex = 4, Id = 5004, Name = "ORD-2026-004", Email = "$430.80", Role = "103", Course = "Charlie Davis", Status = "Shipped", CreatedAt = "2026-04-04 16:45:00" },
                new QueryResultRow { RowIndex = 5, Id = 5005, Name = "ORD-2026-005", Email = "$89.99", Role = "104", Course = "Dana White", Status = "Delivered", CreatedAt = "2026-04-05 11:30:45" }
            };
            foreach (var r in orderRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["ORDER_NUMBER"] = r.Name;
                r.Values["USER_ID"] = r.Role;
                r.Values["CUSTOMER_NAME"] = r.Course;
                r.Values["TOTAL_AMOUNT"] = r.Email;
                r.Values["STATUS"] = r.Status;
                r.Values["ORDER_DATE"] = r.CreatedAt;
            }
            return orderRows.FirstOrDefault(r =>
                r.Id.ToString() == value ||
                r.Role == value ||
                r.Name.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                (r.Values.TryGetValue(targetColumn, out var cv) && cv.Equals(value, StringComparison.OrdinalIgnoreCase)));
        }

        if (tableName.Equals("Roles", StringComparison.OrdinalIgnoreCase))
        {
            var roleRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 1, Name = "DBA", Email = "dba@company.internal", Role = "SUPERUSER", Status = "Active", CreatedAt = "2025-01-01 00:00:00" },
                new QueryResultRow { RowIndex = 2, Id = 2, Name = "Developer", Email = "dev@company.internal", Role = "READ_WRITE", Status = "Active", CreatedAt = "2025-01-01 00:00:00" },
                new QueryResultRow { RowIndex = 3, Id = 3, Name = "Analyst", Email = "analytics@company.internal", Role = "READ_ONLY", Status = "Active", CreatedAt = "2025-01-01 00:00:00" },
                new QueryResultRow { RowIndex = 4, Id = 4, Name = "Admin", Email = "root@company.internal", Role = "ALL_PRIVILEGES", Status = "Active", CreatedAt = "2025-01-01 00:00:00" }
            };
            foreach (var r in roleRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["TITLE"] = r.Name;
                r.Values["PERMISSIONS"] = r.Role;
                r.Values["CREATED_AT"] = r.CreatedAt;
            }
            return roleRows.FirstOrDefault(r =>
                r.Id.ToString() == value ||
                r.Name.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                (r.Values.TryGetValue(targetColumn, out var cv) && cv.Equals(value, StringComparison.OrdinalIgnoreCase)));
        }

        if (tableName.Equals("Courses", StringComparison.OrdinalIgnoreCase))
        {
            var courseRows = new List<QueryResultRow>
            {
                new QueryResultRow { RowIndex = 1, Id = 1, Name = "C# & Avalonia UI", Email = "Alice Johnson", Role = "101", Course = "Active", Status = "Active", CreatedAt = "40 Hours" },
                new QueryResultRow { RowIndex = 2, Id = 2, Name = "SQL Server Architecture", Email = "Bob Smith", Role = "102", Course = "Active", Status = "Active", CreatedAt = "50 Hours" },
                new QueryResultRow { RowIndex = 3, Id = 3, Name = "Database Performance Tuning", Email = "Charlie Davis", Role = "103", Course = "In Progress", Status = "In Progress", CreatedAt = "35 Hours" },
                new QueryResultRow { RowIndex = 4, Id = 4, Name = "PostgreSQL Administration", Email = "Dana White", Role = "104", Course = "Active", Status = "Active", CreatedAt = "45 Hours" },
                new QueryResultRow { RowIndex = 5, Id = 5, Name = "Cloud Databases & Docker", Email = "Evan Wright", Role = "105", Course = "Completed", Status = "Completed", CreatedAt = "30 Hours" }
            };
            foreach (var r in courseRows)
            {
                r.Values["ID"] = r.Id.ToString();
                r.Values["TITLE"] = r.Name;
                r.Values["USER_ID"] = r.Role;
                r.Values["STUDENT_NAME"] = r.Email;
                r.Values["DURATION"] = r.CreatedAt;
                r.Values["STATUS"] = r.Status;
            }
            return courseRows.FirstOrDefault(r =>
                r.Id.ToString() == value ||
                r.Name.Equals(value, StringComparison.OrdinalIgnoreCase) ||
                (r.Values.TryGetValue(targetColumn, out var cv) && cv.Equals(value, StringComparison.OrdinalIgnoreCase)));
        }

        return null;
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
