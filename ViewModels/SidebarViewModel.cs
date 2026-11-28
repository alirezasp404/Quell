using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExplorerTab))]
    [NotifyPropertyChangedFor(nameof(IsSavedQueriesTab))]
    [NotifyPropertyChangedFor(nameof(IsDatabasesTab))]
    [NotifyPropertyChangedFor(nameof(IsHistoryTab))]
    [NotifyPropertyChangedFor(nameof(IsDatabasesTabActive))]
    [NotifyPropertyChangedFor(nameof(IsExplorerTabActive))]
    [NotifyPropertyChangedFor(nameof(IsSavedQueriesTabActive))]
    [NotifyPropertyChangedFor(nameof(IsHistoryTabActive))]
    [NotifyPropertyChangedFor(nameof(DrawerTitle))]
    private string _selectedTab = "Explorer";

    public bool IsExplorerTab => SelectedTab == "Explorer";
    public bool IsSavedQueriesTab => SelectedTab == "SavedQueries";
    public bool IsDatabasesTab => SelectedTab == "Databases";
    public bool IsHistoryTab => SelectedTab == "History";

    public bool IsDatabasesTabActive => IsDatabasesTab && IsExpanded;
    public bool IsExplorerTabActive => IsExplorerTab && IsExpanded;
    public bool IsSavedQueriesTabActive => IsSavedQueriesTab && IsExpanded;
    public bool IsHistoryTabActive => IsHistoryTab && IsExpanded;

    public string DrawerTitle => SelectedTab switch
    {
        "Databases" => "CONNECTIONS",
        "SavedQueries" => "SAVED QUERIES",
        "History" => "HISTORY",
        _ => "EXPLORER"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpanded))]
    [NotifyPropertyChangedFor(nameof(PinTooltipText))]
    [NotifyPropertyChangedFor(nameof(PinStatusText))]
    [NotifyPropertyChangedFor(nameof(IsDatabasesTabActive))]
    [NotifyPropertyChangedFor(nameof(IsExplorerTabActive))]
    [NotifyPropertyChangedFor(nameof(IsSavedQueriesTabActive))]
    [NotifyPropertyChangedFor(nameof(IsHistoryTabActive))]
    private bool _isPinned = false;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsExpanded))]
    [NotifyPropertyChangedFor(nameof(IsDatabasesTabActive))]
    [NotifyPropertyChangedFor(nameof(IsExplorerTabActive))]
    [NotifyPropertyChangedFor(nameof(IsSavedQueriesTabActive))]
    [NotifyPropertyChangedFor(nameof(IsHistoryTabActive))]
    private bool _isDockExpanded = false;

    public bool IsExpanded => IsPinned || IsDockExpanded;

    public string PinTooltipText => IsPinned 
        ? "Pinned (Click to unpin / auto-collapse)" 
        : "Pin Sidebar (Keep open)";

    public string PinStatusText => IsPinned ? "Pinned" : "Pin";

    [RelayCommand]
    public void ToggleDatabasesTab()
    {
        if (IsExpanded && SelectedTab == "Databases")
        {
            CollapseDock();
        }
        else
        {
            SelectedTab = "Databases";
            IsDockExpanded = true;
        }
    }

    [RelayCommand]
    public void ToggleExplorerTab()
    {
        if (IsExpanded && SelectedTab == "Explorer")
        {
            CollapseDock();
        }
        else
        {
            SelectedTab = "Explorer";
            IsDockExpanded = true;
        }
    }

    [RelayCommand]
    public void ToggleSavedQueriesTab()
    {
        if (IsExpanded && SelectedTab == "SavedQueries")
        {
            CollapseDock();
        }
        else
        {
            SelectedTab = "SavedQueries";
            IsDockExpanded = true;
            _ = RefreshSavedQueriesAsync();
        }
    }

    [RelayCommand]
    public void ToggleHistoryTab()
    {
        if (IsExpanded && SelectedTab == "History")
        {
            CollapseDock();
        }
        else
        {
            SelectedTab = "History";
            IsDockExpanded = true;
            _ = RefreshHistoryAsync();
        }
    }

    [RelayCommand]
    public void CollapseDock()
    {
        IsPinned = false;
        IsDockExpanded = false;
    }

    [RelayCommand]
    public void TogglePin()
    {
        IsPinned = !IsPinned;
        if (IsPinned)
        {
            IsDockExpanded = true;
        }
        else
        {
            IsDockExpanded = false;
        }
    }

    public ObservableCollection<string> Tabs { get; } = new()
    {
        "Explorer",
        "SavedQueries",
        "History",
        "Databases"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholderText))]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionTitle = "Not Connected";

    [ObservableProperty]
    private DatabaseType? _databaseType;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholderText))]
    private string _databaseName = string.Empty;

    [ObservableProperty]
    private string _serverVersion = string.Empty;

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSearchFilter))]
    private string _tableSearchFilter = string.Empty;

    public bool HasSearchFilter => !string.IsNullOrWhiteSpace(TableSearchFilter);

    public string SearchPlaceholderText
    {
        get
        {
            if (IsConnected && Tables.Count > 0)
            {
                return ExplorerCategory switch
                {
                    "Views" => $"Filter {Tables.Count(t => t.IsView)} views…",
                    "Procs" => "Filter procedures…",
                    _ => $"Filter {Tables.Count(t => !t.IsView)} tables…"
                };
            }
            if (IsConnected && !string.IsNullOrWhiteSpace(DatabaseName))
            {
                return $"Filter tables in {DatabaseName}…";
            }
            return "Filter tables & databases…";
        }
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsTablesCategory))]
    [NotifyPropertyChangedFor(nameof(IsViewsCategory))]
    [NotifyPropertyChangedFor(nameof(IsProcsCategory))]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholderText))]
    private string _explorerCategory = "Tables";

    public bool IsTablesCategory => ExplorerCategory == "Tables";
    public bool IsViewsCategory => ExplorerCategory == "Views";
    public bool IsProcsCategory => ExplorerCategory == "Procs";

    [RelayCommand]
    public async Task SelectExplorerCategory(string category)
    {
        if (string.IsNullOrWhiteSpace(category)) return;
        ExplorerCategory = category;

        if (category == "Procs")
        {
            var openNodes = DatabaseNodes.Where(n => n.IsConnected && n.AllRoutineNodes.Count == 0).ToList();
            foreach (var node in openNodes)
            {
                var routines = await Task.Run(() => DatabaseSessionService.Instance.GetRoutinesAsync(node.ConnectionKey));
                node.LoadRoutines(routines, OpenProcedureNodeAsync);
            }
        }

        ApplyFilter();
    }

    [ObservableProperty]
    private TableMetadata? _selectedTable;

    [ObservableProperty]
    private TableTreeNode? _activeTableNode;

    [ObservableProperty]
    private SidebarTreeNodeBase? _selectedTreeNode;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<DatabaseConnectionTreeNode> DatabaseNodes { get; } = new();

    public ObservableCollection<DatabaseConnectionTreeNode> FilteredDatabaseNodes { get; } = new();

    public ObservableCollection<TableMetadata> Tables { get; } = new();

    public ObservableCollection<TableMetadata> FilteredTables { get; } = new();

    public ObservableCollection<ConnectionRecord> SavedConnections { get; } = new();

    public ObservableCollection<QueryHistoryRecord> HistoryItems { get; } = new();

    // Panel B: Saved Queries state
    private readonly System.Collections.Generic.List<SavedQueryFolderRecord> _allFolderRecords = new();
    private readonly System.Collections.Generic.List<SavedQueryRecord> _allSavedQueryRecords = new();

    public ObservableCollection<SavedQueryFolderViewModel> SavedQueryFolders { get; } = new();
    public ObservableCollection<SavedQueryItemViewModel> UnfiledSavedQueries { get; } = new();
    public ObservableCollection<SavedQueryItemViewModel> PinnedSavedQueries { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasSavedQuerySearchFilter))]
    private string _savedQuerySearchFilter = string.Empty;

    public bool HasSavedQuerySearchFilter => !string.IsNullOrWhiteSpace(SavedQuerySearchFilter);

    public bool HasPinnedSavedQueries => PinnedSavedQueries.Count > 0;
    public bool HasNoSavedQueries => SavedQueryFolders.Count == 0 && UnfiledSavedQueries.Count == 0 && PinnedSavedQueries.Count == 0;

    [ObservableProperty]
    private SavedQueryItemViewModel? _selectedSavedQuery;

    [ObservableProperty]
    private bool _isCreatingFolder;

    [ObservableProperty]
    private string _newFolderName = string.Empty;

    [ObservableProperty]
    private bool _isCreatingSavedQuery;

    [ObservableProperty]
    private string _newSavedQueryTitle = string.Empty;

    [ObservableProperty]
    private string _newSavedQueryFolder = string.Empty;

    // Panel C: History Panel state
    private readonly System.Collections.Generic.List<HistoryEntryViewModel> _allHistoryEntries = new();
    public ObservableCollection<HistoryDateGroupViewModel> GroupedHistoryItems { get; } = new();

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(HasHistorySearchFilter))]
    private string _historySearchFilter = string.Empty;

    public bool HasHistorySearchFilter => !string.IsNullOrWhiteSpace(HistorySearchFilter);

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsHistoryAllFilter))]
    [NotifyPropertyChangedFor(nameof(IsHistoryFailedFilter))]
    [NotifyPropertyChangedFor(nameof(IsHistoryThisDbFilter))]
    private string _historyFilterMode = "All";

    public bool IsHistoryAllFilter => HistoryFilterMode == "All";
    public bool IsHistoryFailedFilter => HistoryFilterMode == "Failed";
    public bool IsHistoryThisDbFilter => HistoryFilterMode == "ThisDB";
    public bool HasNoHistoryEntries => GroupedHistoryItems.Count == 0;

    [ObservableProperty]
    private HistoryEntryViewModel? _selectedHistoryEntry;

    public Action<string>? InsertQueryToEditor { get; set; }
    public Func<string, Task>? ExecuteQueryInEditor { get; set; }
    public Action<string, string>? OpenQueryInNewTab { get; set; }
    public Func<string>? GetCurrentEditorQuery { get; set; }
    public Func<string>? GetCurrentEditorTitle { get; set; }
    public Action? OpenConnectModal { get; set; }
    public Action<ConnectionRecord>? RequestActivateConnection { get; set; }
    public Action<ConnectionRecord>? RequestEditConnection { get; set; }
    public Func<string, Task>? ViewTableStructureCallback { get; set; }
    public Func<TableSchemaDetail, string, Task>? ShowTableMetadataInResultCallback { get; set; }
    public Func<string, Task>? ShowTableInDiagramCallback { get; set; }
    public Func<string, string, string, Task>? GenerateTableScriptCallback { get; set; }
    public Action<string, string, string, Action>? RequestTruncateConfirmationCallback { get; set; }
    public Action<string, string, Action>? RequestClearHistoryConfirmationCallback { get; set; }
    public event Action<ConnectionRecord>? ConnectionDeleted;
    public event Action<ConnectionRecord>? ConnectionDisconnected;

    public SidebarViewModel()
    {
        DatabaseSessionService.Instance.ConnectionChanged += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await RefreshAsync();
            });
        };
    }

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsExplorerTab));
        OnPropertyChanged(nameof(IsSavedQueriesTab));
        OnPropertyChanged(nameof(IsDatabasesTab));
        OnPropertyChanged(nameof(IsHistoryTab));
    }

    partial void OnTableSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSavedQuerySearchFilterChanged(string value)
    {
        ApplySavedQueriesFilter();
    }

    partial void OnHistorySearchFilterChanged(string value)
    {
        ApplyHistoryFilter();
    }

    partial void OnSelectedTreeNodeChanged(SidebarTreeNodeBase? value)
    {
        // Clicking a database node does NOT auto-connect.
        // Single-clicking a table node expands/shows its metadata; double-clicking runs the last-100 query.
        if (value is TableTreeNode tableNode && !tableNode.IsProcedure)
        {
            if (ActiveTableNode != null && ActiveTableNode != tableNode)
            {
                ActiveTableNode.IsActive = false;
            }
            tableNode.IsActive = true;
            ActiveTableNode = tableNode;
            SelectedTable = tableNode.Table;
        }
    }

    [RelayCommand]
    public void ClearSearchFilter()
    {
        TableSearchFilter = string.Empty;
    }

    [RelayCommand]
    public void ClearSavedQuerySearchFilter()
    {
        SavedQuerySearchFilter = string.Empty;
    }

    [RelayCommand]
    public void ClearHistorySearchFilter()
    {
        HistorySearchFilter = string.Empty;
    }

    private void ApplyFilter()
    {
        FilteredTables.Clear();
        FilteredDatabaseNodes.Clear();
        var filter = TableSearchFilter?.Trim().ToLowerInvariant();

        foreach (var t in Tables)
        {
            bool categoryMatch = ExplorerCategory switch
            {
                "Views" => t.IsView,
                "Procs" => t.IsProcedure,
                _ => !t.IsView && !t.IsProcedure
            };
            if (!categoryMatch) continue;

            if (string.IsNullOrEmpty(filter) ||
                t.FullName.ToLowerInvariant().Contains(filter) ||
                t.TableName.ToLowerInvariant().Contains(filter))
            {
                FilteredTables.Add(t);
            }
        }

        if (string.IsNullOrEmpty(filter))
        {
            foreach (var node in DatabaseNodes)
            {
                node.FilterObjects(null, ExplorerCategory);
                FilteredDatabaseNodes.Add(node);
            }

            var active = DatabaseNodes.FirstOrDefault(n => n.IsActive);
            if (active != null)
            {
                StatusText = $"{active.TableCount} table(s) in {active.DatabaseName}";
            }
            else if (DatabaseNodes.Count > 0)
            {
                StatusText = $"{DatabaseNodes.Count} saved database(s)";
            }
            else
            {
                StatusText = "No database connected";
            }
            return;
        }

        int totalMatchingTables = 0;
        foreach (var node in DatabaseNodes)
        {
            int matchingCount = node.FilterObjects(filter, ExplorerCategory);
            bool dbMatch = node.Title.ToLowerInvariant().Contains(filter) ||
                           node.DatabaseType.ToDialectString().ToLowerInvariant().Contains(filter) ||
                           node.Server.ToLowerInvariant().Contains(filter) ||
                           node.DatabaseName.ToLowerInvariant().Contains(filter);

            if (dbMatch || matchingCount > 0)
            {
                totalMatchingTables += matchingCount;
                node.IsExpanded = true;
                if (node.TablesFolder != null) node.TablesFolder.IsExpanded = true;
                FilteredDatabaseNodes.Add(node);
            }
        }

        StatusText = $"{totalMatchingTables} matching item(s)";
    }

    private void WireDatabaseNodeCallbacks(DatabaseConnectionTreeNode node)
    {
        node.ConnectAction = ConnectDatabaseNodeAsync;
        node.DisconnectAction = DisconnectDatabaseNodeAsync;
        node.EditAction = EditDatabaseNode;
        node.RefreshAction = RefreshDatabaseNodeAsync;
        node.DeleteAction = DeleteDatabaseNodeAsync;
        node.ExpandConnectAction = ConnectDatabaseNodeAsync;

        node.OpenTableDataAction = ExecuteTableLast100Async;
        node.LoadMetadataAction = LoadTableMetadataNodeAsync;
        node.ViewStructureAction = async t =>
        {
            if (ViewTableStructureCallback != null)
            {
                await ViewTableStructureCallback.Invoke(t.FullName);
            }
        };
        node.ShowInDiagramAction = async t =>
        {
            if (ShowTableInDiagramCallback != null)
            {
                await ShowTableInDiagramCallback.Invoke(t.FullName);
            }
        };
        node.GenerateScriptAction = async (t, mode) =>
        {
            if (GenerateTableScriptCallback != null)
            {
                await GenerateTableScriptCallback.Invoke(t.FullName, t.DatabaseNode.ConnectionKey, mode);
            }
        };
        node.TruncateTableAction = async t =>
        {
            await RequestTruncateTableAsync(t);
        };
    }

    private async Task RequestTruncateTableAsync(TableTreeNode node)
    {
        if (node.IsProcedure || node.IsView) return;

        var dbType = node.DatabaseNode.DatabaseType;
        string formattedTable = QueryResultTableViewModel.FormatIdentifier(node.FullName, dbType);
        string sql = dbType == Quell.Services.DatabaseProviders.DatabaseType.SQLite
            ? $"DELETE FROM {formattedTable};"
            : $"TRUNCATE TABLE {formattedTable};";

        async void ExecuteTruncate()
        {
            var res = await Task.Run(() => DatabaseSessionService.Instance.ExecuteQueryAsync(sql, node.DatabaseNode.ConnectionKey));
            await RefreshDatabaseNodeAsync(node.DatabaseNode);
            if (ExecuteQueryInEditor != null && res.Success)
            {
                string verifyQuery = dbType == Quell.Services.DatabaseProviders.DatabaseType.MSSQL
                    ? $"SELECT TOP 100 * FROM {formattedTable};"
                    : $"SELECT * FROM {formattedTable} LIMIT 100;";
                await ExecuteQueryInEditor.Invoke(verifyQuery);
            }
        }

        if (RequestTruncateConfirmationCallback != null)
        {
            RequestTruncateConfirmationCallback.Invoke(
                "Truncate Table?",
                $"All rows in {node.FullName} will be permanently removed using:\n{sql}",
                node.FullName,
                ExecuteTruncate);
        }
        else
        {
            await Task.Run(() => DatabaseSessionService.Instance.ExecuteQueryAsync(sql, node.DatabaseNode.ConnectionKey));
        }
    }

    public Task OpenProcedureNodeAsync(TableTreeNode? node)
    {
        if (node == null) return Task.CompletedTask;
        if (ActiveTableNode != null) ActiveTableNode.IsActive = false;
        node.IsActive = true;
        ActiveTableNode = node;

        string formatted = QueryResultTableViewModel.FormatIdentifier(node.FullName, node.DatabaseNode.DatabaseType);
        string sql = node.DatabaseNode.DatabaseType switch
        {
            Quell.Services.DatabaseProviders.DatabaseType.MSSQL => $"EXEC {formatted};",
            Quell.Services.DatabaseProviders.DatabaseType.PostgreSQL => $"SELECT * FROM {formatted}();",
            _ => $"CALL {formatted}();"
        };
        InsertQueryToEditor?.Invoke(sql);
        return Task.CompletedTask;
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            // Load saved connections from SQLite
            var conns = (await DatabaseStorageService.Instance.GetRecentConnectionsAsync())
                .Where(c => !c.Server.Contains("quell", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("quell", StringComparison.OrdinalIgnoreCase) && !c.Server.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase))
                .ToList();
            SavedConnections.Clear();
            foreach (var c in conns)
            {
                string key = c.Id > 0 ? $"id_{c.Id}" : $"{c.DatabaseType}_{c.Server}_{c.Port}_{c.DatabaseName}";
                c.IsActive = DatabaseSessionService.Instance.IsSessionOpen(key);
                SavedConnections.Add(c);
            }

            // Sync DatabaseNodes with saved connections
            var currentKeys = conns.Select(c => c.Id > 0 ? $"id_{c.Id}" : $"{c.DatabaseType}_{c.Server}_{c.Port}_{c.DatabaseName}").ToHashSet();
            for (int i = DatabaseNodes.Count - 1; i >= 0; i--)
            {
                if (!currentKeys.Contains(DatabaseNodes[i].ConnectionKey))
                {
                    DatabaseNodes.RemoveAt(i);
                }
            }

            foreach (var c in conns)
            {
                string key = c.Id > 0 ? $"id_{c.Id}" : $"{c.DatabaseType}_{c.Server}_{c.Port}_{c.DatabaseName}";
                var existing = DatabaseNodes.FirstOrDefault(n => n.ConnectionKey == key);
                bool isOpen = DatabaseSessionService.Instance.IsSessionOpen(key);
                bool isActive = (DatabaseSessionService.Instance.ActiveSession?.Key == key);

                if (existing == null)
                {
                    existing = new DatabaseConnectionTreeNode(c);
                    DatabaseNodes.Add(existing);
                }
                else
                {
                    existing.UpdateRecord(c);
                }

                WireDatabaseNodeCallbacks(existing);

                existing.IsConnected = isOpen;
                existing.IsActive = isActive;

                if (isOpen)
                {
                    var dbTables = await Task.Run(() => DatabaseSessionService.Instance.GetTablesAsync(key));
                    existing.LoadTables(dbTables, ExecuteTableLast100Async);
                    if (ExplorerCategory == "Procs")
                    {
                        var routines = await Task.Run(() => DatabaseSessionService.Instance.GetRoutinesAsync(key));
                        existing.LoadRoutines(routines, OpenProcedureNodeAsync);
                    }
                }
                else
                {
                    existing.ClearTables();
                }
            }

            // Active session state
            var session = DatabaseSessionService.Instance;
            if (session.IsConnected && session.ActiveConfig != null)
            {
                IsConnected = true;
                ConnectionTitle = session.ConnectionDisplayText;
                DatabaseType = session.ActiveConfig.DatabaseType;
                DatabaseName = session.ActiveConfig.Database;
                ServerVersion = session.ServerVersion ?? "Connected";

                var activeNode = DatabaseNodes.FirstOrDefault(n => n.IsActive);
                var activeTables = activeNode != null && activeNode.AllTableNodes.Count > 0
                    ? activeNode.AllTableNodes.Select(t => t.Table).ToList()
                    : await Task.Run(() => session.GetTablesAsync());
                Tables.Clear();
                foreach (var t in activeTables)
                {
                    Tables.Add(t);
                }
                StatusText = $"{Tables.Count} table(s) found";
                OnPropertyChanged(nameof(SearchPlaceholderText));
                SqlKeywordsService.Instance.UpdateActiveTables(DatabaseName, Tables);
            }
            else
            {
                SqlKeywordsService.Instance.ClearActiveTables();
                IsConnected = false;
                ConnectionTitle = "Not Connected";
                DatabaseType = null;
                DatabaseName = string.Empty;
                ServerVersion = string.Empty;
                Tables.Clear();
                OnPropertyChanged(nameof(SearchPlaceholderText));
                StatusText = DatabaseNodes.Count > 0 ? $"{DatabaseNodes.Count} saved database(s)" : "No database connected";
            }

            ApplyFilter();

            // Re-apply active table highlight if one was previously selected
            if (SelectedTable != null || ActiveTableNode != null)
            {
                var targetFullName = ActiveTableNode?.FullName ?? SelectedTable?.FullName;
                var activeDb = DatabaseNodes.FirstOrDefault(n => n.IsActive);
                if (activeDb != null && targetFullName != null)
                {
                    var match = activeDb.AllTableNodes.FirstOrDefault(t => t.FullName.Equals(targetFullName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.IsActive = true;
                        ActiveTableNode = match;
                    }
                }
            }

            await RefreshHistoryAsync();
            await RefreshSavedQueriesAsync();
        }
        catch (Exception ex)
        {
            StatusText = $"Error: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    public async Task ConnectDatabaseNodeAsync(DatabaseConnectionTreeNode? node)
    {
        if (node == null) return;

        if (!node.IsConnected)
        {
            node.IsLoading = true;
            node.Subtitle = "Connecting...";
            try
            {
                var config = new DatabaseConnectionConfig
                {
                    Id = node.Record.Id,
                    DatabaseType = node.Record.DatabaseType,
                    Server = node.Record.Server,
                    Port = node.Record.Port,
                    Database = node.Record.DatabaseName,
                    AuthType = node.Record.AuthType,
                    Username = node.Record.Username,
                    Password = node.Record.Password ?? string.Empty,
                    FilePath = node.Record.Server,
                    TrustServerCertificate = true
                };

                var testResult = await DatabaseSessionService.Instance.ConnectAsync(config);
                if (testResult.Success)
                {
                    node.IsConnected = true;
                    node.IsActive = true;
                    foreach (var other in DatabaseNodes)
                    {
                        if (other != node) other.IsActive = false;
                    }

                    var tables = await DatabaseSessionService.Instance.GetTablesAsync(node.ConnectionKey);
                    node.LoadTables(tables, ExecuteTableLast100Async);
                    node.IsExpanded = true;
                    node.Subtitle = $"{tables.Count} table(s) found";

                    await SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync(node.DatabaseType);

                    IsConnected = true;
                    ConnectionTitle = node.Title;
                    DatabaseType = node.DatabaseType;
                    DatabaseName = node.DatabaseName;
                    Tables.Clear();
                    foreach (var t in tables)
                    {
                        Tables.Add(t);
                    }
                    StatusText = $"{tables.Count} table(s) loaded";
                    SqlKeywordsService.Instance.UpdateActiveTables(node.DatabaseName, Tables);

                    ApplyFilter();

            // Re-apply active table highlight if one was previously selected
            if (SelectedTable != null || ActiveTableNode != null)
            {
                var targetFullName = ActiveTableNode?.FullName ?? SelectedTable?.FullName;
                var activeDb = DatabaseNodes.FirstOrDefault(n => n.IsActive);
                if (activeDb != null && targetFullName != null)
                {
                    var match = activeDb.AllTableNodes.FirstOrDefault(t => t.FullName.Equals(targetFullName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.IsActive = true;
                        ActiveTableNode = match;
                    }
                }
            }
                    RequestActivateConnection?.Invoke(node.Record);
                }
                else
                {
                    node.IsConnected = false;
                    node.Subtitle = $"Failed: {testResult.Message}";
                }
            }
            catch (Exception ex)
            {
                node.IsConnected = false;
                node.Subtitle = $"Error: {ex.Message}";
            }
            finally
            {
                node.IsLoading = false;
            }
        }
        else
        {
            DatabaseSessionService.Instance.SetActiveSession(node.ConnectionKey);
            node.IsActive = true;
            foreach (var other in DatabaseNodes)
            {
                if (other != node) other.IsActive = false;
            }
            node.IsExpanded = true;

            var tables = await DatabaseSessionService.Instance.GetTablesAsync(node.ConnectionKey);
            node.LoadTables(tables, ExecuteTableLast100Async);

            IsConnected = true;
            ConnectionTitle = node.Title;
            DatabaseType = node.DatabaseType;
            DatabaseName = node.DatabaseName;
            Tables.Clear();
            foreach (var t in tables)
            {
                Tables.Add(t);
            }
            StatusText = $"{tables.Count} table(s) loaded";
            SqlKeywordsService.Instance.UpdateActiveTables(node.DatabaseName, Tables);

            ApplyFilter();

            // Re-apply active table highlight if one was previously selected
            if (SelectedTable != null || ActiveTableNode != null)
            {
                var targetFullName = ActiveTableNode?.FullName ?? SelectedTable?.FullName;
                var activeDb = DatabaseNodes.FirstOrDefault(n => n.IsActive);
                if (activeDb != null && targetFullName != null)
                {
                    var match = activeDb.AllTableNodes.FirstOrDefault(t => t.FullName.Equals(targetFullName, StringComparison.OrdinalIgnoreCase));
                    if (match != null)
                    {
                        match.IsActive = true;
                        ActiveTableNode = match;
                    }
                }
            }
            RequestActivateConnection?.Invoke(node.Record);
        }
    }

    [RelayCommand]
    public async Task DisconnectDatabaseNodeAsync(DatabaseConnectionTreeNode? node)
    {
        if (node == null) return;
        await DatabaseSessionService.Instance.DisconnectAsync(node.ConnectionKey);
        node.IsConnected = false;
        node.IsActive = false;
        node.ClearTables();
        node.Subtitle = node.Record.DatabaseType.ToDialectString();

        if (DatabaseSessionService.Instance.ActiveSession == null)
        {
            IsConnected = false;
            ConnectionTitle = "Not Connected";
            DatabaseType = null;
            DatabaseName = string.Empty;
            Tables.Clear();
            StatusText = "No active database connection";
        }
        else
        {
            var active = DatabaseNodes.FirstOrDefault(n => n.ConnectionKey == DatabaseSessionService.Instance.ActiveSession.Key);
            if (active != null)
            {
                await ConnectDatabaseNodeAsync(active);
            }
        }

        ApplyFilter();
        ConnectionDisconnected?.Invoke(node.Record);
    }

    [RelayCommand]
    public async Task RefreshDatabaseNodeAsync(DatabaseConnectionTreeNode? node)
    {
        if (node == null) return;
        if (node.IsConnected)
        {
            var tables = await DatabaseSessionService.Instance.GetTablesAsync(node.ConnectionKey);
            node.LoadTables(tables, ExecuteTableLast100Async);
            if (node.IsActive)
            {
                Tables.Clear();
                foreach (var t in tables) Tables.Add(t);
                StatusText = $"{tables.Count} table(s) found";
            }
        }
        else
        {
            await ConnectDatabaseNodeAsync(node);
        }
        ApplyFilter();
    }

    [RelayCommand]
    public async Task DeleteDatabaseNodeAsync(DatabaseConnectionTreeNode? node)
    {
        if (node == null) return;
        await DisconnectDatabaseNodeAsync(node);
        await DatabaseStorageService.Instance.DeleteConnectionAsync(node.Record.Id);
        DatabaseNodes.Remove(node);
        FilteredDatabaseNodes.Remove(node);
        SavedConnections.Remove(node.Record);
        ConnectionDeleted?.Invoke(node.Record);
    }

    [RelayCommand]
    public void EditDatabaseNode(DatabaseConnectionTreeNode? node)
    {
        if (node == null) return;
        RequestEditConnection?.Invoke(node.Record);
    }

    [RelayCommand]
    public void EditSavedConnection(ConnectionRecord? record)
    {
        if (record == null) return;
        RequestEditConnection?.Invoke(record);
    }

    [RelayCommand]
    public async Task DisconnectSavedConnection(ConnectionRecord? record)
    {
        if (record == null) return;
        string sessionKey = record.Id > 0 ? $"id_{record.Id}" : $"{record.DatabaseType}_{record.Server}_{record.Port}_{record.DatabaseName}";
        await DatabaseSessionService.Instance.DisconnectAsync(sessionKey);
        await RefreshAsync();
        ConnectionDisconnected?.Invoke(record);
    }

    [RelayCommand]
    public async Task SelectTableNode(TableTreeNode? node)
    {
        if (node == null) return;

        try
        {
            if (ActiveTableNode != null && ActiveTableNode != node)
            {
                ActiveTableNode.IsActive = false;
            }

            node.IsActive = true;
            ActiveTableNode = node;
            SelectedTable = node.Table;
            SelectedTreeNode = node;

            if (!node.IsProcedure)
            {
                node.IsExpanded = !node.IsExpanded;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] SelectTableNode error: {ex.Message}");
        }
    }

    public async Task LoadTableMetadataNodeAsync(TableTreeNode? node)
    {
        if (node == null || node.IsProcedure) return;

        try
        {
            if (!node.DatabaseNode.IsConnected)
            {
                await ConnectDatabaseNodeAsync(node.DatabaseNode);
            }

            var schema = await DatabaseMetadataService.Instance
                .GetTableSchemaAsync(node.FullName, node.DatabaseNode.ConnectionKey);
            node.PopulateMetadata(schema);
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] LoadTableMetadataNodeAsync error: {ex.Message}");
            node.PopulateMetadata(null);
        }
    }

    public async Task OpenTableMetadataGroupAsync(TableMetadataGroupNode? groupNode)
    {
        if (groupNode == null) return;

        try
        {
            var tableNode = groupNode.ParentTable;
            if (!tableNode.DatabaseNode.IsConnected || !tableNode.DatabaseNode.IsActive)
            {
                await ConnectDatabaseNodeAsync(tableNode.DatabaseNode);
            }

            var schema = tableNode.CachedSchema ?? await DatabaseMetadataService.Instance
                .GetTableSchemaAsync(tableNode.FullName, tableNode.DatabaseNode.ConnectionKey);

            if (schema != null && ShowTableMetadataInResultCallback != null)
            {
                await ShowTableMetadataInResultCallback.Invoke(schema, groupNode.GroupType);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] OpenTableMetadataGroupAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExecuteTableLast100Async(TableTreeNode? node)
    {
        if (node == null) return;

        try
        {
            if (ActiveTableNode != null && ActiveTableNode != node)
            {
                ActiveTableNode.IsActive = false;
            }

            node.IsActive = true;
            ActiveTableNode = node;
            SelectedTable = node.Table;
            SelectedTreeNode = node;

            if (!node.DatabaseNode.IsConnected || !node.DatabaseNode.IsActive)
            {
                await ConnectDatabaseNodeAsync(node.DatabaseNode);
            }

            var dbType = node.DatabaseNode.DatabaseType;
            string formattedTable = QueryResultTableViewModel.FormatIdentifier(node.FullName, dbType);

            var schema = node.CachedSchema ?? await DatabaseMetadataService.Instance
                .GetTableSchemaAsync(node.FullName, node.DatabaseNode.ConnectionKey);

            string orderClause = string.Empty;
            if (schema != null && schema.PrimaryKeyColumns.Count > 0)
            {
                string pkOrder = string.Join(", ", schema.PrimaryKeyColumns.Select(pk =>
                    $"{QueryResultTableViewModel.FormatIdentifier(pk, dbType)} DESC"));
                orderClause = $" ORDER BY {pkOrder}";
            }

            string query = dbType == Quell.Services.DatabaseProviders.DatabaseType.MSSQL
                ? $"SELECT TOP 100 * FROM {formattedTable}{orderClause};"
                : $"SELECT * FROM {formattedTable}{orderClause} LIMIT 100;";

            InsertQueryToEditor?.Invoke(query);
            if (ExecuteQueryInEditor != null)
            {
                await ExecuteQueryInEditor.Invoke(query);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] ExecuteTableLast100Async error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExecuteTableNodeAsync(TableTreeNode? node)
    {
        await ExecuteTableLast100Async(node);
    }

    [RelayCommand]
    public async Task SelectTable(TableMetadata table)
    {
        if (table == null) return;

        try
        {
            SelectedTable = table;

            // Highlight matching TableTreeNode in active DB
            var activeDb = DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            if (activeDb != null)
            {
                var match = activeDb.AllTableNodes.FirstOrDefault(t =>
                    t.FullName.Equals(table.FullName, StringComparison.OrdinalIgnoreCase) ||
                    t.TableName.Equals(table.TableName, StringComparison.OrdinalIgnoreCase));
                if (match != null)
                {
                    if (ActiveTableNode != null)
                    {
                        ActiveTableNode.IsActive = false;
                    }
                    match.IsActive = true;
                    ActiveTableNode = match;
                    SelectedTreeNode = match;
                }
            }

            var dbType = DatabaseType ?? Quell.Services.DatabaseProviders.DatabaseType.MSSQL;
            string formattedTable = QueryResultTableViewModel.FormatIdentifier(table.FullName, dbType);
            string query = dbType == Quell.Services.DatabaseProviders.DatabaseType.MSSQL
                ? $"SELECT TOP 100 * FROM {formattedTable};"
                : $"SELECT * FROM {formattedTable} LIMIT 100;";

            InsertQueryToEditor?.Invoke(query);
            if (ExecuteQueryInEditor != null)
            {
                await ExecuteQueryInEditor.Invoke(query);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] SelectTable error: {ex.Message}");
        }
    }

    [RelayCommand]
    public async Task ExecuteTableQuery(TableMetadata table)
    {
        await SelectTable(table);
    }

    [RelayCommand]
    public async Task ConnectSavedConnection(ConnectionRecord record)
    {
        if (record == null) return;

        var config = new DatabaseConnectionConfig
        {
            Id = record.Id,
            DatabaseType = record.DatabaseType,
            Server = record.Server,
            Port = record.Port,
            Database = record.DatabaseName,
            AuthType = record.AuthType,
            Username = record.Username,
            Password = record.Password ?? string.Empty,
            FilePath = record.Server,
            TrustServerCertificate = true
        };

        var result = await DatabaseSessionService.Instance.ConnectAsync(config);
        if (result.Success)
        {
            await SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync(config.DatabaseType);
            await RefreshAsync();
            RequestActivateConnection?.Invoke(record);
        }
    }

    [RelayCommand]
    public void OpenConnectDialog()
    {
        OpenConnectModal?.Invoke();
    }

    [RelayCommand]
    public async Task DisconnectActive()
    {
        await DatabaseSessionService.Instance.DisconnectAsync();
        await RefreshAsync();
    }

    [RelayCommand]
    public async Task ActivateConnection(ConnectionRecord record)
    {
        if (record == null) return;
        if (RequestActivateConnection != null)
        {
            RequestActivateConnection.Invoke(record);
        }
        else
        {
            await ConnectSavedConnection(record);
        }
    }

    [RelayCommand]
    public async Task DisconnectAndRemoveConnection(ConnectionRecord record)
    {
        if (record == null) return;
        string sessionKey = record.Id > 0 ? $"id_{record.Id}" : $"{record.DatabaseType}_{record.Server}_{record.Port}_{record.DatabaseName}";
        await DatabaseSessionService.Instance.DisconnectAsync(sessionKey);
        await DatabaseStorageService.Instance.DeleteConnectionAsync(record.Id);
        await RefreshAsync();
        ConnectionDeleted?.Invoke(record);
    }

    [RelayCommand]
    public void SelectHistoryItem(QueryHistoryRecord item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.QueryText)) return;
        InsertQueryToEditor?.Invoke(item.QueryText);
    }

    public async Task RefreshHistoryAsync()
    {
        try
        {
            var history = await Task.Run(() => DatabaseStorageService.Instance.GetQueryHistoryAsync());
            HistoryItems.Clear();
            _allHistoryEntries.Clear();
            foreach (var h in history)
            {
                HistoryItems.Add(h);
                _allHistoryEntries.Add(new HistoryEntryViewModel(h));
            }
            ApplyHistoryFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] RefreshHistoryAsync error: {ex.Message}");
        }
    }

    [RelayCommand]
    public void SelectHistoryFilter(string mode)
    {
        if (string.IsNullOrWhiteSpace(mode)) return;
        HistoryFilterMode = mode;
        ApplyHistoryFilter();
    }

    private void ApplyHistoryFilter()
    {
        GroupedHistoryItems.Clear();
        string term = HistorySearchFilter?.Trim().ToLowerInvariant() ?? string.Empty;
        string activeDb = DatabaseName?.Trim() ?? string.Empty;

        var filtered = _allHistoryEntries.Where(entry =>
        {
            if (HistoryFilterMode == "Failed" && !entry.IsFailed) return false;
            if (HistoryFilterMode == "ThisDB")
            {
                if (string.IsNullOrEmpty(activeDb)) return false;
                if (!entry.DatabaseName.Contains(activeDb, StringComparison.OrdinalIgnoreCase)) return false;
            }
            if (!string.IsNullOrEmpty(term))
            {
                return entry.QueryText.ToLowerInvariant().Contains(term) ||
                       entry.DatabaseName.ToLowerInvariant().Contains(term);
            }
            return true;
        }).ToList();

        var groupOrder = new[] { "TODAY", "YESTERDAY", "EARLIER" };
        foreach (var groupKey in groupOrder)
        {
            var items = filtered.Where(e => e.DateGroup == groupKey).ToList();
            if (items.Count > 0)
            {
                var grp = new HistoryDateGroupViewModel(groupKey);
                foreach (var it in items) grp.Items.Add(it);
                GroupedHistoryItems.Add(grp);
            }
        }

        OnPropertyChanged(nameof(HasNoHistoryEntries));
    }

    [RelayCommand]
    public void SelectHistoryEntry(HistoryEntryViewModel? entry)
    {
        if (entry == null) return;
        bool wasSelected = entry.IsSelected;
        foreach (var item in _allHistoryEntries)
        {
            item.IsSelected = false;
        }
        entry.IsSelected = !wasSelected;
        SelectedHistoryEntry = entry.IsSelected ? entry : null;
    }

    [RelayCommand]
    public async Task RunHistoryAgainAsync(HistoryEntryViewModel? entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.QueryText)) return;
        InsertQueryToEditor?.Invoke(entry.QueryText);
        if (ExecuteQueryInEditor != null)
        {
            await ExecuteQueryInEditor.Invoke(entry.QueryText);
        }
    }

    [RelayCommand]
    public void OpenHistoryInTab(HistoryEntryViewModel? entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.QueryText)) return;
        if (OpenQueryInNewTab != null)
        {
            OpenQueryInNewTab.Invoke($"History #{entry.Id}.sql", entry.QueryText);
        }
        else
        {
            InsertQueryToEditor?.Invoke(entry.QueryText);
        }
    }

    [RelayCommand]
    public async Task SaveHistoryEntryAsync(HistoryEntryViewModel? entry)
    {
        if (entry == null || string.IsNullOrWhiteSpace(entry.QueryText)) return;
        string suggestedTitle = SuggestQueryTitle(entry.QueryText);
        await DatabaseStorageService.Instance.SaveOrUpdateSavedQueryAsync(new SavedQueryRecord
        {
            Title = suggestedTitle,
            QueryText = entry.QueryText,
            FolderName = string.Empty,
            IsPinned = false
        });
        await RefreshSavedQueriesAsync();
        SelectedTab = "SavedQueries";
        IsDockExpanded = true;
    }

    [RelayCommand]
    public async Task ClearHistoryAsync()
    {
        async void PerformClear()
        {
            await DatabaseStorageService.Instance.ClearQueryHistoryAsync();
            await RefreshHistoryAsync();
        }

        if (RequestClearHistoryConfirmationCallback != null)
        {
            RequestClearHistoryConfirmationCallback.Invoke(
                "Clear Query History?",
                "All recorded query history from the last 30 days on this device will be permanently cleared.",
                PerformClear);
        }
        else
        {
            await DatabaseStorageService.Instance.ClearQueryHistoryAsync();
            await RefreshHistoryAsync();
        }
    }

    // =========================================================================
    // Panel B: Saved Queries Management
    // =========================================================================

    public async Task RefreshSavedQueriesAsync()
    {
        try
        {
            var foldersTask = Task.Run(() => DatabaseStorageService.Instance.GetSavedQueryFoldersAsync());
            var queriesTask = Task.Run(() => DatabaseStorageService.Instance.GetSavedQueriesAsync());
            await Task.WhenAll(foldersTask, queriesTask);

            _allFolderRecords.Clear();
            _allFolderRecords.AddRange(await foldersTask);
            _allSavedQueryRecords.Clear();
            _allSavedQueryRecords.AddRange(await queriesTask);

            ApplySavedQueriesFilter();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[SidebarViewModel] RefreshSavedQueriesAsync error: {ex.Message}");
        }
    }

    private void ApplySavedQueriesFilter()
    {
        var expandedMap = SavedQueryFolders.ToDictionary(f => f.Name, f => f.IsExpanded, StringComparer.OrdinalIgnoreCase);

        SavedQueryFolders.Clear();
        UnfiledSavedQueries.Clear();
        PinnedSavedQueries.Clear();

        string filter = SavedQuerySearchFilter?.Trim().ToLowerInvariant() ?? string.Empty;

        var matchingQueries = _allSavedQueryRecords.Where(q =>
            string.IsNullOrEmpty(filter) ||
            q.Title.ToLowerInvariant().Contains(filter) ||
            q.QueryText.ToLowerInvariant().Contains(filter) ||
            q.FolderName.ToLowerInvariant().Contains(filter)
        ).ToList();

        var folderNames = _allFolderRecords.Select(f => f.Name)
            .Concat(matchingQueries.Select(q => q.FolderName).Where(fn => !string.IsNullOrWhiteSpace(fn)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(n => n)
            .ToList();

        foreach (var folderName in folderNames)
        {
            var folderQueries = matchingQueries
                .Where(q => string.Equals(q.FolderName, folderName, StringComparison.OrdinalIgnoreCase))
                .ToList();

            if (!string.IsNullOrEmpty(filter) &&
                folderQueries.Count == 0 &&
                !folderName.ToLowerInvariant().Contains(filter))
            {
                continue;
            }

            bool isExp = expandedMap.TryGetValue(folderName, out var prev) ? prev : true;
            var folderVm = new SavedQueryFolderViewModel(folderName, isExp);
            foreach (var q in folderQueries)
            {
                folderVm.Items.Add(new SavedQueryItemViewModel(q));
            }
            folderVm.NotifyCountChanged();
            SavedQueryFolders.Add(folderVm);
        }

        foreach (var q in matchingQueries.Where(q => string.IsNullOrWhiteSpace(q.FolderName)))
        {
            UnfiledSavedQueries.Add(new SavedQueryItemViewModel(q));
        }

        foreach (var q in matchingQueries.Where(q => q.IsPinned))
        {
            PinnedSavedQueries.Add(new SavedQueryItemViewModel(q));
        }

        OnPropertyChanged(nameof(HasPinnedSavedQueries));
        OnPropertyChanged(nameof(HasNoSavedQueries));
    }

    [RelayCommand]
    public void BeginCreateFolder()
    {
        IsCreatingSavedQuery = false;
        NewFolderName = string.Empty;
        IsCreatingFolder = true;
    }

    [RelayCommand]
    public void CancelCreateFolder()
    {
        IsCreatingFolder = false;
        NewFolderName = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmCreateFolderAsync()
    {
        if (string.IsNullOrWhiteSpace(NewFolderName)) return;
        await DatabaseStorageService.Instance.SaveQueryFolderAsync(NewFolderName.Trim());
        IsCreatingFolder = false;
        NewFolderName = string.Empty;
        await RefreshSavedQueriesAsync();
    }

    [RelayCommand]
    public void BeginCreateSavedQuery()
    {
        IsCreatingFolder = false;
        string sql = GetCurrentEditorQuery?.Invoke() ?? string.Empty;
        string title = GetCurrentEditorTitle?.Invoke()?.Replace(".sql", "") ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title) || title.StartsWith("Query ", StringComparison.OrdinalIgnoreCase))
        {
            title = SuggestQueryTitle(sql);
        }
        NewSavedQueryTitle = title;
        NewSavedQueryFolder = SavedQueryFolders.FirstOrDefault()?.Name ?? string.Empty;
        IsCreatingSavedQuery = true;
    }

    [RelayCommand]
    public void CancelCreateSavedQuery()
    {
        IsCreatingSavedQuery = false;
        NewSavedQueryTitle = string.Empty;
    }

    [RelayCommand]
    public async Task ConfirmCreateSavedQueryAsync()
    {
        string sql = GetCurrentEditorQuery?.Invoke() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(sql))
        {
            sql = "SELECT 1;";
        }
        string title = string.IsNullOrWhiteSpace(NewSavedQueryTitle)
            ? SuggestQueryTitle(sql)
            : NewSavedQueryTitle.Trim();

        await DatabaseStorageService.Instance.SaveOrUpdateSavedQueryAsync(new SavedQueryRecord
        {
            Title = title,
            QueryText = sql,
            FolderName = NewSavedQueryFolder?.Trim() ?? string.Empty,
            IsPinned = false
        });
        IsCreatingSavedQuery = false;
        NewSavedQueryTitle = string.Empty;
        await RefreshSavedQueriesAsync();
    }

    public async Task QuickSaveQueryAsync(string sql, string? titleHint = null)
    {
        if (string.IsNullOrWhiteSpace(sql)) return;
        string title = !string.IsNullOrWhiteSpace(titleHint)
            ? titleHint.Replace(".sql", "", StringComparison.OrdinalIgnoreCase).Trim()
            : SuggestQueryTitle(sql);
        if (title.StartsWith("Query ", StringComparison.OrdinalIgnoreCase))
        {
            title = SuggestQueryTitle(sql);
        }

        await DatabaseStorageService.Instance.SaveOrUpdateSavedQueryAsync(new SavedQueryRecord
        {
            Title = title,
            QueryText = sql,
            FolderName = string.Empty,
            IsPinned = false
        });
        await RefreshSavedQueriesAsync();
    }

    [RelayCommand]
    public void SelectSavedQueryItem(SavedQueryItemViewModel? item)
    {
        if (item == null) return;
        void ClearSelection(System.Collections.Generic.IEnumerable<SavedQueryItemViewModel> list)
        {
            foreach (var it in list) it.IsSelected = false;
        }
        foreach (var f in SavedQueryFolders) ClearSelection(f.Items);
        ClearSelection(UnfiledSavedQueries);
        ClearSelection(PinnedSavedQueries);

        item.IsSelected = true;
        SelectedSavedQuery = item;
    }

    [RelayCommand]
    public void OpenSavedQueryInNewTab(SavedQueryItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.QueryText)) return;
        if (OpenQueryInNewTab != null)
        {
            OpenQueryInNewTab.Invoke($"{item.Title}.sql", item.QueryText);
        }
        else
        {
            InsertQueryToEditor?.Invoke(item.QueryText);
        }
    }

    [RelayCommand]
    public async Task RunSavedQueryAsync(SavedQueryItemViewModel? item)
    {
        if (item == null || string.IsNullOrWhiteSpace(item.QueryText)) return;
        InsertQueryToEditor?.Invoke(item.QueryText);
        if (ExecuteQueryInEditor != null)
        {
            await ExecuteQueryInEditor.Invoke(item.QueryText);
        }
    }

    [RelayCommand]
    public void StartRenameSavedQuery(SavedQueryItemViewModel? item)
    {
        if (item == null) return;
        item.EditingTitle = item.Title;
        item.IsRenaming = true;
    }

    [RelayCommand]
    public void CancelRenameSavedQuery(SavedQueryItemViewModel? item)
    {
        if (item == null) return;
        item.IsRenaming = false;
    }

    [RelayCommand]
    public async Task CommitRenameSavedQueryAsync(SavedQueryItemViewModel? item)
    {
        if (item == null) return;
        item.IsRenaming = false;
        if (!string.IsNullOrWhiteSpace(item.EditingTitle) && item.EditingTitle.Trim() != item.Title)
        {
            await DatabaseStorageService.Instance.RenameSavedQueryAsync(item.Id, item.EditingTitle.Trim());
            await RefreshSavedQueriesAsync();
        }
    }

    [RelayCommand]
    public async Task TogglePinSavedQueryAsync(SavedQueryItemViewModel? item)
    {
        if (item == null) return;
        await DatabaseStorageService.Instance.ToggleSavedQueryPinAsync(item.Id, !item.IsPinned);
        await RefreshSavedQueriesAsync();
    }

    [RelayCommand]
    public async Task DeleteSavedQueryItemAsync(SavedQueryItemViewModel? item)
    {
        if (item == null) return;
        await DatabaseStorageService.Instance.DeleteSavedQueryAsync(item.Id);
        await RefreshSavedQueriesAsync();
    }

    [RelayCommand]
    public async Task DeleteSavedQueryFolderAsync(SavedQueryFolderViewModel? folder)
    {
        if (folder == null) return;
        await DatabaseStorageService.Instance.DeleteSavedQueryFolderAsync(folder.Name);
        await RefreshSavedQueriesAsync();
    }

    public async Task MoveSavedQueryToFolderAsync(int queryId, string targetFolderName)
    {
        await DatabaseStorageService.Instance.MoveSavedQueryToFolderAsync(queryId, targetFolderName);
        await RefreshSavedQueriesAsync();
    }

    private static string SuggestQueryTitle(string sql)
    {
        if (string.IsNullOrWhiteSpace(sql)) return "Untitled Query";
        var lines = sql.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        foreach (var l in lines)
        {
            if (l.StartsWith("--"))
            {
                string comment = l.TrimStart('-', ' ').Trim();
                if (!string.IsNullOrEmpty(comment) && comment.Length <= 42 &&
                    !comment.Equals("SQL Query Editor", StringComparison.OrdinalIgnoreCase) &&
                    !comment.StartsWith("Type query", StringComparison.OrdinalIgnoreCase))
                {
                    return comment;
                }
            }
        }
        var fromMatch = System.Text.RegularExpressions.Regex.Match(
            sql, @"\bFROM\s+([\[\]""`\w\.]+)", System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (fromMatch.Success)
        {
            return $"Query {fromMatch.Groups[1].Value.Trim('[', ']', '"', '`', ';')}";
        }
        string clean = sql.Replace("\r", " ").Replace("\n", " ").Trim();
        return clean.Length > 34 ? clean.Substring(0, 34) + "…" : clean;
    }

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = tabName;
        _ = RefreshAsync();
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
    }

    [RelayCommand]
    private void ClosePanel()
    {
        IsVisible = false;
    }

    [RelayCommand]
    private void CloseTab(string tabName)
    {
        if (Tabs.Contains(tabName))
        {
            Tabs.Remove(tabName);
            if (Tabs.Count == 0)
            {
                IsVisible = false;
            }
            else if (SelectedTab == tabName)
            {
                SelectedTab = Tabs.First();
            }
        }
    }
}
