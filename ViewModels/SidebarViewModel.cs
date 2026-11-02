using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fdb.Services;
using Fdb.Services.DatabaseProviders;

namespace Fdb.ViewModels;

public partial class SidebarViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private string _selectedTab = "Explorer";

    public ObservableCollection<string> Tabs { get; } = new()
    {
        "Explorer",
        "Databases",
        "History"
    };

    [ObservableProperty]
    private bool _isConnected;

    [ObservableProperty]
    private string _connectionTitle = "Local Storage (SQLite)";

    [ObservableProperty]
    private string _databaseType = "SQLite";

    [ObservableProperty]
    private string _databaseName = "fdb_studio.db";

    [ObservableProperty]
    private string _serverVersion = "SQLite";

    [ObservableProperty]
    private string _statusText = "Ready";

    [ObservableProperty]
    private string _tableSearchFilter = string.Empty;

    [ObservableProperty]
    private TableMetadata? _selectedTable;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<TableMetadata> Tables { get; } = new();

    public ObservableCollection<TableMetadata> FilteredTables { get; } = new();

    public ObservableCollection<ConnectionRecord> SavedConnections { get; } = new();

    public ObservableCollection<QueryHistoryRecord> HistoryItems { get; } = new();

    public Action<string>? InsertQueryToEditor { get; set; }
    public Func<string, Task>? ExecuteQueryInEditor { get; set; }
    public Action? OpenConnectModal { get; set; }

    public SidebarViewModel()
    {
        // Listen to live database connection changes
        DatabaseSessionService.Instance.ConnectionChanged += () =>
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(async () =>
            {
                await RefreshAsync();
            });
        };
    }

    partial void OnTableSearchFilterChanged(string value)
    {
        ApplyTableFilter();
    }

    private void ApplyTableFilter()
    {
        FilteredTables.Clear();
        var filter = TableSearchFilter?.Trim().ToLowerInvariant();

        foreach (var t in Tables)
        {
            if (string.IsNullOrEmpty(filter) || t.FullName.ToLowerInvariant().Contains(filter))
            {
                FilteredTables.Add(t);
            }
        }
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        IsLoading = true;
        try
        {
            var session = DatabaseSessionService.Instance;
            if (session.IsConnected && session.ActiveConfig != null)
            {
                IsConnected = true;
                ConnectionTitle = session.ConnectionDisplayText;
                DatabaseType = session.ActiveConfig.DatabaseType;
                DatabaseName = session.ActiveConfig.Database;
                ServerVersion = session.ServerVersion ?? "Connected";

                var dbTables = await session.GetTablesAsync();
                Tables.Clear();
                foreach (var t in dbTables)
                {
                    Tables.Add(t);
                }
                StatusText = $"{Tables.Count} table(s) found";
            }
            else
            {
                IsConnected = false;
                ConnectionTitle = "Local Storage (SQLite)";
                DatabaseType = "SQLite";
                DatabaseName = "fdb_studio.db";
                ServerVersion = "SQLite 3";

                var localTables = await DatabaseStorageService.Instance.GetLocalTablesAsync();
                Tables.Clear();
                foreach (var t in localTables)
                {
                    Tables.Add(t);
                }
                StatusText = $"{Tables.Count} local table(s)";
            }

            ApplyTableFilter();

            // Load saved connections
            var conns = await DatabaseStorageService.Instance.GetRecentConnectionsAsync();
            SavedConnections.Clear();
            foreach (var c in conns)
            {
                SavedConnections.Add(c);
            }

            // Load history
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
    public void SelectTable(TableMetadata table)
    {
        if (table == null) return;
        SelectedTable = table;

        string query = DatabaseType == "MSSQL"
            ? $"SELECT TOP 100 * FROM {table.FullName};"
            : $"SELECT * FROM {table.FullName} LIMIT 100;";

        InsertQueryToEditor?.Invoke(query);
    }

    [RelayCommand]
    public async Task ExecuteTableQuery(TableMetadata table)
    {
        if (table == null) return;
        SelectedTable = table;

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
    public async Task ConnectSavedConnection(ConnectionRecord record)
    {
        if (record == null) return;

        var config = new DatabaseConnectionConfig
        {
            DatabaseType = record.DatabaseType ?? "MSSQL",
            Server = record.Server,
            Port = record.Port,
            Database = record.DatabaseName,
            AuthType = record.AuthType,
            Username = record.Username,
            TrustServerCertificate = true
        };

        var result = await DatabaseSessionService.Instance.ConnectAsync(config);
        if (result.Success)
        {
            await SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync(config.DatabaseType);
            await RefreshAsync();
        }
    }

    [RelayCommand]
    public void OpenConnectDialog()
    {
        OpenConnectModal?.Invoke();
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
