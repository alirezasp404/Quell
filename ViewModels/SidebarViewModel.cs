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
    private string _selectedTab = "Explorer";

    public bool IsExplorerTab => SelectedTab == "Explorer";
    public bool IsDatabasesTab => SelectedTab == "Databases";
    public bool IsHistoryTab => SelectedTab == "History";

    public ObservableCollection<string> Tabs { get; } = new()
    {
        "Explorer",
        "Databases",
        "History"
    };

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(SearchPlaceholderText))]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionTitle = "Not Connected";

    [ObservableProperty]
    private string _databaseType = string.Empty;

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

    [ObservableProperty]
    private bool _filterActiveOnly;

    partial void OnFilterActiveOnlyChanged(bool value)
    {
        ApplyFilter();
    }

    public string SearchPlaceholderText
    {
        get
        {
            if (IsConnected && !string.IsNullOrWhiteSpace(DatabaseName))
            {
                return $"Search tables in {DatabaseName}...";
            }
            return "Filter databases and tables...";
        }
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

    public Action<string>? InsertQueryToEditor { get; set; }
    public Func<string, Task>? ExecuteQueryInEditor { get; set; }
    public Action? OpenConnectModal { get; set; }
    public Action<ConnectionRecord>? RequestActivateConnection { get; set; }
    public Action<ConnectionRecord>? RequestEditConnection { get; set; }
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
        OnPropertyChanged(nameof(IsDatabasesTab));
        OnPropertyChanged(nameof(IsHistoryTab));
    }

    partial void OnTableSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnSelectedTreeNodeChanged(SidebarTreeNodeBase? value)
    {
        // Clicking a database node does NOT auto-connect.
        // Use right-click -> Connect to connect.
        // Clicking a table node auto-generates and executes SELECT TOP 100.
        if (value is TableTreeNode tableNode)
        {
            _ = SelectTableNode(tableNode);
        }
    }

    [RelayCommand]
    public void ClearSearchFilter()
    {
        TableSearchFilter = string.Empty;
    }

    private void ApplyFilter()
    {
        FilteredTables.Clear();
        FilteredDatabaseNodes.Clear();
        var filter = TableSearchFilter?.Trim().ToLowerInvariant();

        foreach (var t in Tables)
        {
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
                node.FilterTables(null);
                if (FilterActiveOnly)
                {
                    if (node.IsActive)
                    {
                        FilteredDatabaseNodes.Add(node);
                    }
                }
                else
                {
                    FilteredDatabaseNodes.Add(node);
                }
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
            if (FilterActiveOnly && !node.IsActive)
            {
                continue;
            }

            int matchingCount = node.FilterTables(filter);
            bool dbMatch = node.Title.ToLowerInvariant().Contains(filter) ||
                           node.DatabaseType.ToLowerInvariant().Contains(filter) ||
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

        StatusText = $"{totalMatchingTables} matching table(s)";
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

                existing.ConnectAction = ConnectDatabaseNodeAsync;
                existing.DisconnectAction = DisconnectDatabaseNodeAsync;
                existing.EditAction = EditDatabaseNode;
                existing.RefreshAction = RefreshDatabaseNodeAsync;
                existing.DeleteAction = DeleteDatabaseNodeAsync;
                existing.ExpandConnectAction = ConnectDatabaseNodeAsync;

                existing.IsConnected = isOpen;
                existing.IsActive = isActive;

                if (isOpen)
                {
                    var dbTables = await DatabaseSessionService.Instance.GetTablesAsync(key);
                    existing.LoadTables(dbTables, SelectTableNode);
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

                var activeTables = await session.GetTablesAsync();
                Tables.Clear();
                foreach (var t in activeTables)
                {
                    Tables.Add(t);
                }
                StatusText = $"{Tables.Count} table(s) found";
                SqlKeywordsService.Instance.UpdateActiveTables(DatabaseName, Tables);
            }
            else
            {
                SqlKeywordsService.Instance.ClearActiveTables();
                IsConnected = false;
                ConnectionTitle = "Not Connected";
                DatabaseType = string.Empty;
                DatabaseName = string.Empty;
                ServerVersion = string.Empty;
                Tables.Clear();
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

            // Load query history
            var history = await DatabaseStorageService.Instance.GetQueryHistoryAsync();
            HistoryItems.Clear();
            foreach (var h in history)
            {
                HistoryItems.Add(h);
            }
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
                    DatabaseType = node.Record.DatabaseType ?? "MSSQL",
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
                    node.LoadTables(tables, SelectTableNode);
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
            node.LoadTables(tables, SelectTableNode);

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
        node.Subtitle = node.Record.DatabaseType ?? "MSSQL";

        if (DatabaseSessionService.Instance.ActiveSession == null)
        {
            IsConnected = false;
            ConnectionTitle = "Not Connected";
            DatabaseType = string.Empty;
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
            node.LoadTables(tables, SelectTableNode);
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

        // Reset active state across all tables in all databases
        foreach (var db in DatabaseNodes)
        {
            foreach (var t in db.AllTableNodes)
            {
                t.IsActive = false;
            }
        }

        node.IsActive = true;
        ActiveTableNode = node;
        SelectedTable = node.Table;
        SelectedTreeNode = node;

        if (!node.DatabaseNode.IsConnected || !node.DatabaseNode.IsActive)
        {
            await ConnectDatabaseNodeAsync(node.DatabaseNode);
        }

        string query = node.DatabaseNode.DatabaseType == "MSSQL"
            ? $"SELECT TOP 100 * FROM {node.FullName};"
            : $"SELECT * FROM {node.FullName} LIMIT 100;";

        InsertQueryToEditor?.Invoke(query);
        if (ExecuteQueryInEditor != null)
        {
            await ExecuteQueryInEditor.Invoke(query);
        }
    }

    [RelayCommand]
    public async Task ExecuteTableNodeAsync(TableTreeNode? node)
    {
        await SelectTableNode(node);
    }

    [RelayCommand]
    public async Task SelectTable(TableMetadata table)
    {
        if (table == null) return;
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
                foreach (var db in DatabaseNodes)
                {
                    foreach (var t in db.AllTableNodes) t.IsActive = false;
                }
                match.IsActive = true;
                ActiveTableNode = match;
                SelectedTreeNode = match;
            }
        }

        string query = DatabaseType == "MSSQL"
            ? $"SELECT TOP 100 * FROM {table.FullName};"
            : $"SELECT * FROM {table.FullName} LIMIT 100;";

        InsertQueryToEditor?.Invoke(query);
        if (ExecuteQueryInEditor != null)
        {
            await ExecuteQueryInEditor.Invoke(query);
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
            DatabaseType = record.DatabaseType ?? "MSSQL",
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
