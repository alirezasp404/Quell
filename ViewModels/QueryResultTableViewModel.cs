using Quell.Services;
using Quell.Services.DatabaseProviders;
using Quell.Services.DemoData;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quell.ViewModels;

public sealed record PasteCellSlot(QueryResultRow Row, string ColumnName, int MatrixRow, int MatrixCol);

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

#pragma warning disable MVVMTK0034
    public void SetRawValue(string key, string value)
    {
        Values[key] = value;
        switch (key.ToUpperInvariant())
        {
            case "ID":
                if (int.TryParse(value, out int idVal)) _id = idVal;
                break;
            case "NAME":
                _name = value;
                break;
            case "EMAIL":
                _email = value;
                break;
            case "ROLE":
                _role = value;
                break;
            case "COURSE":
                _course = value;
                break;
            case "STATUS":
                _status = value;
                break;
            case "CREATED AT" or "CREATEDAT":
                _createdAt = value;
                break;
        }
    }
#pragma warning restore MVVMTK0034

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
            OnPropertyChanged("Item[]");
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
    [NotifyPropertyChangedFor(nameof(CanInteractWithTable))]
    private bool _isRefreshing;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(CanInteractWithTable))]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Loading table data...";

    [ObservableProperty]
    private string _loadingElapsedText = "0.0 s";

    public Action? CancelQueryCallback { get; set; }

    [RelayCommand]
    private void CancelQuery()
    {
        LoadingMessage = "Cancelling query...";
        CancelQueryCallback?.Invoke();
    }

    public bool CanInteractWithTable => !IsLoading && !IsRefreshing;

    public Func<Task>? RefreshCallback { get; set; }

    public Func<bool>? CheckConnectionCallback { get; set; }

    private bool HasLiveConnection => IsConnectedToDatabase || (CheckConnectionCallback?.Invoke() ?? false);

    private readonly List<QueryResultRow> _allRows = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasRows))]
    [NotifyPropertyChangedFor(nameof(ShowEmptyState))]
    [NotifyPropertyChangedFor(nameof(PaginationStatusText))]
    private int _totalRows;

    public string PaginationStatusText
    {
        get
        {
            if (TotalRows == 0) return "0 rows";
            if (!string.IsNullOrWhiteSpace(SearchFilter) && Rows.Count != TotalRows)
            {
                return $"{Rows.Count:N0} of {TotalRows:N0} rows (filtered)";
            }
            return $"{TotalRows:N0} rows";
        }
    }

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
    public bool ShowEmptyState => !HasRows && !IsLoading;

    partial void OnTotalRowsChanged(int value)
    {
        OnPropertyChanged(nameof(IsFormulaBarVisible));
    }

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
                Rows.ReplaceAll(_allRows);
                TotalRows = Rows.Count;
            }
            return;
        }

        string term = filter.Trim();
        var filtered = _allRows.Where(r => MatchesFilter(r, term)).ToList();

        Rows.ReplaceAll(filtered);
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

    public TableSchemaDetail? CurrentTableSchema { get; private set; }

    public Dictionary<string, ColumnMetadata> ColumnMetadataMap { get; } = new(StringComparer.OrdinalIgnoreCase);

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

    public ColumnMetadata? GetColumnMetadata(string? columnName)
    {
        if (string.IsNullOrWhiteSpace(columnName)) return null;
        return ColumnMetadataMap.TryGetValue(columnName, out var col) ? col : null;
    }

    public string? GetColumnDataType(string? columnName)
        => GetColumnMetadata(columnName)?.FormattedDataType;

    public bool IsColumnNullable(string? columnName)
        => GetColumnMetadata(columnName)?.IsNullable ?? true;

    public bool IsColumnIdentity(string? columnName)
        => GetColumnMetadata(columnName)?.IsIdentity ?? false;

    public string GetColumnHeaderTooltip(string columnName)
    {
        var meta = GetColumnMetadata(columnName);
        var fk = GetForeignKeyRelation(columnName);
        bool isPk = IsColumnPrimaryKey(columnName);

        var parts = new List<string>();
        if (meta != null)
        {
            parts.Add($"{meta.Name}: {meta.FormattedDataType}");
            parts.Add(meta.IsNullable ? "NULL" : "NOT NULL");
            if (meta.IsIdentity) parts.Add("IDENTITY");
            if (meta.IsUnique && !isPk) parts.Add("UNIQUE");
            if (!string.IsNullOrWhiteSpace(meta.DefaultValue)) parts.Add($"DEFAULT {meta.DefaultValue}");
        }
        else
        {
            parts.Add(columnName);
        }

        if (isPk) parts.Add("Primary Key");
        if (fk != null) parts.Add($"FK -> {fk.TargetTable}({fk.TargetColumn})");
        if (!string.IsNullOrWhiteSpace(meta?.Description)) parts.Add(meta.Description);

        return string.Join(" • ", parts);
    }

    public void ClearMetadata()
    {
        PrimaryKeyColumns.Clear();
        PrimaryKeyColumnName = string.Empty;
        HasPrimaryKey = false;
        ForeignKeyRelations.Clear();
        ColumnMetadataMap.Clear();
        CurrentTableSchema = null;
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

    /// <summary>
    /// Applies full table schema metadata from DatabaseMetadataService and refreshes columns if already bound.
    /// </summary>
    public void SetTableSchema(TableSchemaDetail? schema)
    {
        PrepareTableSchema(schema);

        if (_lastColumns != null)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    /// <summary>
    /// Applies PK/FK metadata silently before SetResults runs so DataGrid columns are only built once.
    /// </summary>
    public void PrepareTableMetadata(IEnumerable<string> pks, IEnumerable<ForeignKeyRelation> fks)
    {
        ApplyPrimaryKeys(pks);
        ApplyForeignKeyRelations(fks);
    }

    /// <summary>
    /// Applies full table schema metadata silently before SetResults runs so DataGrid columns are only built once.
    /// </summary>
    public void PrepareTableSchema(TableSchemaDetail? schema)
    {
        CurrentTableSchema = schema;
        ColumnMetadataMap.Clear();

        if (schema == null)
        {
            PrimaryKeyColumns.Clear();
            PrimaryKeyColumnName = string.Empty;
            HasPrimaryKey = false;
            ForeignKeyRelations.Clear();
            return;
        }

        foreach (var col in schema.Columns)
        {
            if (!string.IsNullOrWhiteSpace(col.Name) && !ColumnMetadataMap.ContainsKey(col.Name))
            {
                ColumnMetadataMap[col.Name] = col;
            }
        }

        ApplyPrimaryKeys(schema.PrimaryKeyColumns);
        ApplyForeignKeyRelations(schema.ForeignKeys);
    }

    /// <summary>
    /// Loads and applies table metadata directly via DatabaseMetadataService.
    /// </summary>
    public async Task<TableSchemaDetail?> LoadTableMetadataAsync(
        string tableName,
        string? sessionKey = null,
        bool forceRefresh = false)
    {
        if (string.IsNullOrWhiteSpace(tableName)) return null;
        var schema = await DatabaseMetadataService.Instance
            .GetTableSchemaAsync(tableName, sessionKey, forceRefresh)
            .ConfigureAwait(false);
        if (schema != null)
        {
            SetTableSchema(schema);
        }
        return schema;
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
        HashSet<string> PrimaryKeys,
        TableSchemaDetail? Schema = null);

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
            var rowsCopy = new List<QueryResultRow>(Rows);
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
                pksCopy,
                CurrentTableSchema);

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
            if (prev.Schema != null)
            {
                PrepareTableSchema(prev.Schema);
            }
            else
            {
                PrepareTableMetadata(prev.PrimaryKeys, prev.FkRelations.Values);
            }
            SetResults(prev.Columns, prev.Rows, prev.ExecutionTime);

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

    [ObservableProperty]
    private string _uiModel = "ExcelClassic";

    [ObservableProperty]
    private bool _showFormulaBar = true;

    [ObservableProperty]
    private bool _showExcelColumnLetters = true;

    [ObservableProperty]
    private bool _showColumnDataTypes;

    [ObservableProperty]
    private bool _showExcelStatusStats = true;

    [ObservableProperty]
    private string _stripingMode = "Rows";

    [ObservableProperty]
    private string _gridLinesMode = "All";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveCell))]
    private string _activeCellReference = "—";

    [ObservableProperty]
    private string _activeCellColumnName = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasActiveCellDataType))]
    private string _activeCellDataType = string.Empty;

    [ObservableProperty]
    private string _activeCellFormulaText = string.Empty;

    [ObservableProperty]
    private bool _activeCellIsNull;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasExcelStats))]
    private string _excelStatsText = string.Empty;

    public bool HasActiveCell => !string.IsNullOrEmpty(ActiveCellReference) && ActiveCellReference != "—";
    public bool HasActiveCellDataType => !string.IsNullOrEmpty(ActiveCellDataType);
    public bool HasExcelStats => ShowExcelStatusStats && !string.IsNullOrEmpty(ExcelStatsText);
    public bool IsFormulaBarVisible => ShowFormulaBar && HasRows;

    public Action? CopyActiveCellCallback { get; set; }
    public Action? EditActiveCellCallback { get; set; }

    [RelayCommand]
    private void CopyActiveCellFromFormulaBar() => CopyActiveCellCallback?.Invoke();

    [RelayCommand]
    private void EditActiveCellFromFormulaBar() => EditActiveCellCallback?.Invoke();

    public static string ToExcelColumnName(int oneBasedColumn)
    {
        if (oneBasedColumn <= 0) return "?";
        var chars = new Stack<char>();
        int n = oneBasedColumn;
        while (n > 0)
        {
            n--;
            chars.Push((char)('A' + (n % 26)));
            n /= 26;
        }
        return new string(chars.ToArray());
    }

    public void UpdateActiveCellInfo(string cellRef, string columnName, string? value, string? statsText)
    {
        ActiveCellReference = cellRef;
        ActiveCellColumnName = columnName;
        ActiveCellDataType = GetColumnDataType(columnName) ?? string.Empty;
        ActiveCellIsNull = value == null || value.Equals("NULL", StringComparison.OrdinalIgnoreCase);
        ActiveCellFormulaText = value ?? string.Empty;
        ExcelStatsText = statsText ?? string.Empty;
        OnPropertyChanged(nameof(IsFormulaBarVisible));
    }

    public void ClearActiveCellInfo()
    {
        ActiveCellReference = "—";
        ActiveCellColumnName = string.Empty;
        ActiveCellDataType = string.Empty;
        ActiveCellFormulaText = string.Empty;
        ActiveCellIsNull = false;
        ExcelStatsText = string.Empty;
        OnPropertyChanged(nameof(IsFormulaBarVisible));
    }

    public void ApplyTableCustomization(TableCustomizationRecord custom)
    {
        bool rebuildColumns =
            ShowExcelColumnLetters != custom.ShowExcelColumnLetters ||
            ShowColumnDataTypes != custom.ShowColumnDataTypes ||
            !string.Equals(StripingMode, custom.StripingMode, StringComparison.OrdinalIgnoreCase) ||
            !string.Equals(UiModel, custom.UiModel, StringComparison.OrdinalIgnoreCase);

        UiModel = custom.UiModel;
        ShowFormulaBar = custom.ShowFormulaBar;
        ShowExcelColumnLetters = custom.ShowExcelColumnLetters;
        ShowColumnDataTypes = custom.ShowColumnDataTypes;
        ShowExcelStatusStats = custom.ShowExcelStatusStats;
        StripingMode = custom.StripingMode;
        GridLinesMode = custom.GridLinesMode;

        OnPropertyChanged(nameof(IsFormulaBarVisible));
        OnPropertyChanged(nameof(HasExcelStats));

        if (rebuildColumns && _lastColumns != null && _lastColumns.Count > 0)
        {
            ColumnsChanged?.Invoke(_lastColumns);
        }
    }

    public Func<string?>? CopySelection { get; set; }

    public Func<string?>? CopyFocusedRow { get; set; }

    public EditCellModalViewModel EditCellModal { get; } = new();

    public Action<string, string, string?, Action>? RequestConfirmation { get; set; }

    public Func<string, Task<bool>>? ExecuteQueryCallback { get; set; }

    public Func<string, Task<(bool Success, string? ErrorMessage)>>? ExecuteQueryDetailedCallback { get; set; }

    public Action<string>? InsertQueryToEditorCallback { get; set; }

    public Func<IReadOnlyList<QueryResultRow>>? GetSelectedRowsCallback { get; set; }

    public Func<IReadOnlyList<string>>? GetSelectedColumnsCallback { get; set; }

    public Func<Task<string?>>? GetClipboardTextCallback { get; set; }

    public Func<int, int, IReadOnlyList<PasteCellSlot>>? ResolvePasteTargetsCallback { get; set; }

    public event Action<IReadOnlyList<string>>? ColumnsChanged;

    public BulkObservableCollection<QueryResultRow> Rows { get; } = new();

    public QueryResultTableViewModel()
    {
        EditCellModal.OnUpdatedCallback = msg =>
        {
            LastQueryMessage = msg;
            HasLastQueryMessage = true;
        };

        ApplyTableCustomization(ThemeService.Instance.CurrentTableCustomization);
        ThemeService.Instance.TableCustomizationChanged += (_, custom) =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() => ApplyTableCustomization(custom));
        };
    }

    public void SetResults(IReadOnlyList<string> columns, IReadOnlyList<QueryResultRow> newRows, string execTime)
    {
        IsLoading = false;

        void Apply()
        {
            try
            {
                IsLoading = false;
                _lastColumns = columns;
                _allRows.Clear();
                _allRows.AddRange(newRows);

                foreach (var r in newRows)
                {
                    foreach (var kvp in r.Values)
                    {
                        r.OriginalValues[kvp.Key] = kvp.Value;
                    }
                }

                SelectedRow = null;

                // Notify column changes first so DataGrid reconfigures column headers/bindings and clears old selections
                ColumnsChanged?.Invoke(columns);

                // Populate new rows
                Rows.ReplaceAll(newRows);

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
                OnPropertyChanged(nameof(ShowEmptyState));
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[QueryResultTableViewModel] Apply results error: {ex.Message}");
            }
        }

        if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
        {
            Apply();
        }
        else if (ColumnsChanged == null || Avalonia.Application.Current?.ApplicationLifetime == null)
        {
            // Headless verification or background unit run with no active UI message pump
            Apply();
        }
        else
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(Apply);
        }
    }

    public void OpenEditCell(QueryResultRow? row, string? columnName = null)
    {
        if (!HasRows) return;

        var selectedRows = GetSelectedRowsCallback?.Invoke();
        var selectedCols = GetSelectedColumnsCallback?.Invoke();

        if (selectedRows != null && selectedRows.Count > 1)
        {
            var cols = !string.IsNullOrWhiteSpace(columnName) && columnName != "ROW #"
                ? new[] { columnName }
                : (selectedCols != null && selectedCols.Count > 0 ? selectedCols : Array.Empty<string>());
            if (cols.Count == 0) return;

            EditCellModal.OpenBatch(selectedRows, cols, CurrentTableName, CurrentDatabaseType, PrimaryKeyColumns);
            return;
        }

        var targetRow = row ?? selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        if (targetRow == null) return;

        string? col = !string.IsNullOrWhiteSpace(columnName) && columnName != "ROW #"
            ? columnName
            : selectedCols?.FirstOrDefault();
        if (string.IsNullOrWhiteSpace(col) || col == "ROW #") return;

        EditCellModal.Open(targetRow, col, CurrentTableName, CurrentDatabaseType, PrimaryKeyColumns);
    }

    [RelayCommand]
    public void OpenEditSelectedCell()
    {
        if (!HasRows) return;

        var selectedRows = GetSelectedRowsCallback?.Invoke();
        var selectedCols = GetSelectedColumnsCallback?.Invoke();

        if (selectedRows != null && selectedRows.Count > 1)
        {
            if (selectedCols == null || selectedCols.Count == 0) return;
            EditCellModal.OpenBatch(selectedRows, selectedCols, CurrentTableName, CurrentDatabaseType, PrimaryKeyColumns);
            return;
        }

        var row = selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        string? col = selectedCols?.FirstOrDefault();
        if (row == null || string.IsNullOrWhiteSpace(col) || col == "ROW #") return;

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
            var rowSet = new HashSet<QueryResultRow>(rows);
            _allRows.RemoveAll(r => rowSet.Contains(r));
            Rows.RemoveRange(rows);

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
        IsLoading = true;
        LoadingMessage = "Refreshing table data...";
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
            IsLoading = false;
        }
    }

    public void LoadSampleReferencedTable(string tableName, string? filterColumn = null, string? filterValue = null)
    {
        if (HasLiveConnection) return;
        IsLoading = false;

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
        IsLoading = false;

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

    [RelayCommand]
    private async Task PasteCell()
    {
        if (!HasRows)
        {
            return;
        }

        string? clipboardText = GetClipboardTextCallback != null
            ? await GetClipboardTextCallback.Invoke()
            : await ClipboardService.GetTextAsync();

        if (string.IsNullOrEmpty(clipboardText))
        {
            LastQueryMessage = "Clipboard is empty or contains no text.";
            HasLastQueryMessage = true;
            return;
        }

        var matrix = ParseClipboardTsv(clipboardText);
        if (matrix.Count == 0 || matrix[0].Count == 0)
        {
            LastQueryMessage = "Clipboard is empty or contains no text.";
            HasLastQueryMessage = true;
            return;
        }

        int matrixRows = matrix.Count;
        int matrixCols = matrix.Max(r => r.Count);
        var slots = ResolvePasteSlots(matrixRows, matrixCols);
        if (slots.Count == 0)
        {
            return;
        }

        var edits = new List<(QueryResultRow Row, string ColumnName, string OldValue, string NewValue)>();
        foreach (var slot in slots)
        {
            if (slot.Row == null || string.IsNullOrWhiteSpace(slot.ColumnName) || slot.ColumnName == "ROW #")
            {
                continue;
            }

            if (slot.MatrixRow < 0 || slot.MatrixRow >= matrix.Count)
            {
                continue;
            }

            var rowValues = matrix[slot.MatrixRow];
            if (slot.MatrixCol < 0 || slot.MatrixCol >= rowValues.Count)
            {
                continue;
            }

            string normalizedValue = NormalizePastedValue(rowValues[slot.MatrixCol]);
            edits.Add((slot.Row, slot.ColumnName, slot.Row[slot.ColumnName], normalizedValue));
        }

        if (edits.Count == 0)
        {
            return;
        }

        if (!HasLiveConnection)
        {
            foreach (var edit in edits)
            {
                edit.Row[edit.ColumnName] = edit.NewValue;
            }

            LastQueryMessage = edits.Count == 1
                ? $"Pasted value into {edits[0].ColumnName} (offline mode)"
                : $"Pasted {edits.Count} cells locally (offline mode)";
            HasLastQueryMessage = true;
            return;
        }

        if (string.IsNullOrWhiteSpace(CurrentTableName))
        {
            LastQueryMessage = "Cannot paste to database: query result has no single target table.";
            HasLastQueryMessage = true;
            return;
        }

        // Build SQL UPDATE per row using pre-paste row state BEFORE mutating any cell values.
        var rowGroups = new List<(QueryResultRow Row, List<(string ColumnName, string OldValue, string NewValue)> Cells, string Sql)>();
        var rowIndexMap = new Dictionary<QueryResultRow, int>(ReferenceEqualityComparer.Instance);

        foreach (var edit in edits)
        {
            if (!rowIndexMap.TryGetValue(edit.Row, out int groupIdx))
            {
                groupIdx = rowGroups.Count;
                rowIndexMap[edit.Row] = groupIdx;
                rowGroups.Add((edit.Row, new List<(string, string, string)>(), string.Empty));
            }
            rowGroups[groupIdx].Cells.Add((edit.ColumnName, edit.OldValue, edit.NewValue));
        }

        string escapedTable = FormatIdentifier(CurrentTableName, CurrentDatabaseType);
        for (int i = 0; i < rowGroups.Count; i++)
        {
            var group = rowGroups[i];
            var setClauses = group.Cells.Select(c =>
                $"{FormatIdentifier(c.ColumnName, CurrentDatabaseType)} = {EditCellModalViewModel.FormatLiteral(c.NewValue, CurrentDatabaseType)}");
            string whereClause = BuildWhereClause(group.Row, CurrentDatabaseType);
            string sql = $"UPDATE {escapedTable}\nSET {string.Join(", ", setClauses)}\nWHERE {whereClause};";
            rowGroups[i] = (group.Row, group.Cells, sql);
        }

        int appliedCells = 0;
        string? errorMsg = null;

        foreach (var group in rowGroups)
        {
            bool success = true;
            string? rowError = null;

            if (ExecuteQueryDetailedCallback != null)
            {
                (success, rowError) = await ExecuteQueryDetailedCallback.Invoke(group.Sql);
            }
            else if (ExecuteQueryCallback != null)
            {
                success = await ExecuteQueryCallback.Invoke(group.Sql);
            }

            if (!success)
            {
                errorMsg = rowError ?? "Database rejected UPDATE (check column type, constraints, or connection).";
                break;
            }

            foreach (var cell in group.Cells)
            {
                group.Row[cell.ColumnName] = cell.NewValue;
                group.Row.OriginalValues[cell.ColumnName] = cell.NewValue;
                appliedCells++;
            }
        }

        if (!string.IsNullOrEmpty(errorMsg))
        {
            LastQueryMessage = appliedCells > 0
                ? $"Paste partially failed after {appliedCells} cell(s) (reverted failed row): {errorMsg}"
                : $"Paste failed (reverted): {errorMsg}";
            HasLastQueryMessage = true;
            return;
        }

        LastQueryMessage = appliedCells == 1
            ? $"Pasted & saved 1 cell in {CurrentTableName}.{edits[0].ColumnName}"
            : $"Pasted & saved {appliedCells} cells in {CurrentTableName}";
        HasLastQueryMessage = true;
    }

    private IReadOnlyList<PasteCellSlot> ResolvePasteSlots(int matrixRows, int matrixCols)
    {
        if (ResolvePasteTargetsCallback != null)
        {
            return ResolvePasteTargetsCallback.Invoke(matrixRows, matrixCols);
        }

        var selectedRows = GetSelectedRowsCallback?.Invoke();
        var selectedCols = GetSelectedColumnsCallback?.Invoke();
        var cleanSelectedCols = selectedCols?
            .Where(c => !string.IsNullOrWhiteSpace(c) && c != "ROW #")
            .ToList() ?? new List<string>();

        if (matrixRows == 1 && matrixCols == 1)
        {
            var rows = selectedRows != null && selectedRows.Count > 0
                ? selectedRows
                : (SelectedRow != null ? new[] { SelectedRow } : Array.Empty<QueryResultRow>());
            if (rows.Count == 0 || cleanSelectedCols.Count == 0)
            {
                return Array.Empty<PasteCellSlot>();
            }

            var singleValueSlots = new List<PasteCellSlot>(rows.Count * cleanSelectedCols.Count);
            foreach (var row in rows)
            {
                foreach (var col in cleanSelectedCols)
                {
                    singleValueSlots.Add(new PasteCellSlot(row, col, 0, 0));
                }
            }
            return singleValueSlots;
        }

        var allDataCols = (_lastColumns ?? Rows.FirstOrDefault()?.Values.Keys.ToList() ?? new List<string>())
            .Where(c => !string.IsNullOrWhiteSpace(c) && !string.Equals(c, "ROW #", StringComparison.OrdinalIgnoreCase))
            .ToList();

        var startRow = selectedRows?.FirstOrDefault() ?? SelectedRow ?? Rows.FirstOrDefault();
        int startRowIdx = startRow != null ? Rows.IndexOf(startRow) : -1;
        string? startCol = cleanSelectedCols.FirstOrDefault() ?? allDataCols.FirstOrDefault();
        int startColIdx = !string.IsNullOrEmpty(startCol)
            ? allDataCols.FindIndex(c => string.Equals(c, startCol, StringComparison.OrdinalIgnoreCase))
            : -1;

        if (startRowIdx < 0 || startColIdx < 0)
        {
            return Array.Empty<PasteCellSlot>();
        }

        var matrixSlots = new List<PasteCellSlot>();
        for (int r = 0; r < matrixRows && startRowIdx + r < Rows.Count; r++)
        {
            var row = Rows[startRowIdx + r];
            for (int c = 0; c < matrixCols && startColIdx + c < allDataCols.Count; c++)
            {
                matrixSlots.Add(new PasteCellSlot(row, allDataCols[startColIdx + c], r, c));
            }
        }
        return matrixSlots;
    }

    public static string NormalizePastedValue(string value)
    {
        return string.Equals(value, "NULL", StringComparison.OrdinalIgnoreCase)
            ? "NULL"
            : value;
    }

    public static List<List<string>> ParseClipboardTsv(string text)
    {
        var rows = new List<List<string>>();
        var currentRow = new List<string>();
        var cell = new StringBuilder();
        bool inQuotes = false;
        bool atCellStart = true;

        for (int i = 0; i < text.Length; i++)
        {
            char ch = text[i];

            if (inQuotes)
            {
                if (ch == '"')
                {
                    if (i + 1 < text.Length && text[i + 1] == '"')
                    {
                        cell.Append('"');
                        i++;
                    }
                    else
                    {
                        inQuotes = false;
                    }
                }
                else
                {
                    cell.Append(ch);
                }
                continue;
            }

            if (atCellStart && ch == '"')
            {
                inQuotes = true;
                atCellStart = false;
                continue;
            }

            if (ch == '\t')
            {
                currentRow.Add(cell.ToString());
                cell.Clear();
                atCellStart = true;
                continue;
            }

            if (ch == '\r' || ch == '\n')
            {
                if (ch == '\r' && i + 1 < text.Length && text[i + 1] == '\n')
                {
                    i++;
                }

                currentRow.Add(cell.ToString());
                rows.Add(currentRow);
                currentRow = new List<string>();
                cell.Clear();
                atCellStart = true;
                continue;
            }

            cell.Append(ch);
            atCellStart = false;
        }

        // Ignore a single trailing phantom row created by a final unquoted newline (e.g. Excel copy).
        bool endedWithUnquotedNewline = !inQuotes && atCellStart && currentRow.Count == 0 && cell.Length == 0 && rows.Count > 0;
        if (!endedWithUnquotedNewline)
        {
            currentRow.Add(cell.ToString());
            rows.Add(currentRow);
        }

        return rows;
    }

    private static async Task WriteClipboardAsync(string? text)
    {
        if (string.IsNullOrEmpty(text))
            return;

        await ClipboardService.CopyTextAsync(text);
    }
}
