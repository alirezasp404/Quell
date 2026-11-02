using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fdb.Services;
using Fdb.Services.DatabaseProviders;

namespace Fdb.ViewModels;

public partial class MainViewModel : ViewModelBase
{
    [ObservableProperty]
    private NavbarViewModel _navbar;

    [ObservableProperty]
    private SidebarViewModel _sidebar;

    [ObservableProperty]
    private EditorViewModel _editor;

    [ObservableProperty]
    private FooterViewModel _footer;

    [ObservableProperty]
    private ConnectDatabaseModalViewModel _connectModal;

    [ObservableProperty]
    private ConfirmActionModalViewModel _confirmModal;

    [ObservableProperty]
    private ExportDataModalViewModel _exportModal;

    [ObservableProperty]
    private SettingsModalViewModel _settingsModal;

    [ObservableProperty]
    private QueryResultTableViewModel _sampleQueryResult;

    [ObservableProperty]
    private SchemaPropertiesTableViewModel _sampleSchema;

    public ObservableCollection<TargetDatabaseItem> AvailableTargetDatabases { get; } = new();

    [ObservableProperty]
    private TargetDatabaseItem? _selectedTargetDatabase;

    public MainViewModel()
    {
        _navbar = new NavbarViewModel();
        _sidebar = new SidebarViewModel();
        _editor = new EditorViewModel();
        _footer = new FooterViewModel();
        _connectModal = new ConnectDatabaseModalViewModel();
        _confirmModal = new ConfirmActionModalViewModel();
        _exportModal = new ExportDataModalViewModel();
        _settingsModal = new SettingsModalViewModel();
        _sampleQueryResult = new QueryResultTableViewModel();
        _sampleSchema = new SchemaPropertiesTableViewModel();

        // Wire sidebar actions to editor and connect modal
        _sidebar.InsertQueryToEditor = q =>
        {
            if (_editor.SelectedTab != null)
            {
                _editor.SelectedTab.Text = q;
            }
            else
            {
                _editor.AddTab();
                if (_editor.SelectedTab != null) _editor.SelectedTab.Text = q;
            }
        };

        _sidebar.ExecuteQueryInEditor = async q =>
        {
            if (_editor.SelectedTab != null)
            {
                _editor.SelectedTab.Text = q;
            }
            else
            {
                _editor.AddTab();
                if (_editor.SelectedTab != null) _editor.SelectedTab.Text = q;
            }
            await ExecuteQuery();
        };

        _sidebar.OpenConnectModal = () => _connectModal.Open();

        _sidebar.RequestActivateConnection = rec =>
        {
            _ = ActivateDatabaseConnectionAsync(rec);
        };

        _sidebar.RequestEditConnection = rec =>
        {
            _connectModal.OpenForEdit(rec);
        };

        _sidebar.ConnectionDeleted += rec =>
        {
            _ = RefreshAvailableDatabasesAsync();
        };

        _sidebar.ConnectionDisconnected += rec =>
        {
            if (SelectedTargetDatabase?.ConnectionId == rec.Id)
            {
                SelectedTargetDatabase = null;
                SampleQueryResult.TargetDatabaseName = "(No Connection)";
            }
            _ = RefreshAvailableDatabasesAsync();
        };

        _connectModal.ConnectionDeleted += rec =>
        {
            _ = RefreshAvailableDatabasesAsync();
        };

        // Listen for live database connection changes to update the UI badge and reload sidebar tables
        DatabaseSessionService.Instance.ConnectionChanged += () =>
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Navbar.UpdateConnectionStatus();
                _ = Sidebar.RefreshAsync();
                _ = RefreshAvailableDatabasesAsync();
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Navbar.UpdateConnectionStatus();
                    _ = Sidebar.RefreshAsync();
                    _ = RefreshAvailableDatabasesAsync();
                });
            }
        };

        // Initialize SQLite local storage, load available databases and tables
        _ = InitializeStartupAsync();
    }

    private async Task InitializeStartupAsync()
    {
        await Fdb.Services.DatabaseStorageService.Instance.InitializeAsync();
        await RefreshAvailableDatabasesAsync();
        await Sidebar.RefreshAsync();
    }

    public async Task RefreshAvailableDatabasesAsync()
    {
        var conns = await Fdb.Services.DatabaseStorageService.Instance.GetRecentConnectionsAsync();
        var currentSelectedKey = SelectedTargetDatabase?.Key;

        AvailableTargetDatabases.Clear();
        TargetDatabaseItem? itemToSelect = null;

        foreach (var c in conns)
        {
            string key = c.Id > 0 ? $"id_{c.Id}" : $"{c.DatabaseType}_{c.Server}_{c.Port}_{c.DatabaseName}";
            bool isOpen = DatabaseSessionService.Instance.IsSessionOpen(key);
            var targetItem = new TargetDatabaseItem
            {
                ConnectionId = c.Id,
                Key = key,
                DatabaseType = c.DatabaseType,
                DisplayName = c.DisplayTitle,
                Server = c.Server,
                Port = c.Port,
                DatabaseName = c.DatabaseName,
                AuthType = c.AuthType,
                Username = c.Username,
                IsLocalSqlite = false,
                IsConnected = isOpen
            };

            AvailableTargetDatabases.Add(targetItem);

            if (key == currentSelectedKey)
            {
                itemToSelect = targetItem;
            }
            else if (itemToSelect == null && isOpen)
            {
                itemToSelect = targetItem;
            }
        }

        if (itemToSelect != null)
        {
            SelectedTargetDatabase = itemToSelect;
        }
        else if (SelectedTargetDatabase != null && !AvailableTargetDatabases.Any(d => d.Key == SelectedTargetDatabase.Key))
        {
            SelectedTargetDatabase = null;
            SampleQueryResult.TargetDatabaseName = "(No Connection)";
        }
    }

    partial void OnSelectedTargetDatabaseChanged(TargetDatabaseItem? value)
    {
        if (value == null)
        {
            SampleQueryResult.TargetDatabaseName = "(No Connection)";
            return;
        }

        SampleQueryResult.TargetDatabaseName = value.FullDisplayName;
        if (DatabaseSessionService.Instance.IsSessionOpen(value.Key))
        {
            DatabaseSessionService.Instance.SetActiveSession(value.Key);
        }
        _ = Sidebar.RefreshAsync();
    }

    public async Task ActivateDatabaseConnectionAsync(ConnectionRecord record)
    {
        if (record == null) return;
        string key = record.Id > 0 ? $"id_{record.Id}" : $"{record.DatabaseType}_{record.Server}_{record.Port}_{record.DatabaseName}";

        if (!DatabaseSessionService.Instance.IsSessionOpen(key))
        {
            var config = new DatabaseConnectionConfig
            {
                Id = record.Id,
                DatabaseType = record.DatabaseType ?? "MSSQL",
                Server = record.Server,
                Port = record.Port,
                Database = record.DatabaseName,
                AuthType = record.AuthType,
                Username = record.Username,
                FilePath = record.Server,
                TrustServerCertificate = true
            };
            await DatabaseSessionService.Instance.ConnectAsync(config);
        }
        else
        {
            DatabaseSessionService.Instance.SetActiveSession(key);
        }

        await RefreshAvailableDatabasesAsync();
        var match = AvailableTargetDatabases.FirstOrDefault(d => d.Key == key);
        if (match != null)
        {
            SelectedTargetDatabase = match;
        }
        await Sidebar.RefreshAsync();
    }

    [RelayCommand]
    public async Task ExecuteQuery()
    {
        string query = Editor.SelectedTab?.Text ?? "SELECT * FROM dbo.Users;";
        string execTime = "0 ms";
        int rowCount = 0;
        string status = "Success";

        if (SelectedTargetDatabase == null)
        {
            var activeSession = DatabaseSessionService.Instance.ActiveSession;
            if (activeSession != null)
            {
                var match = AvailableTargetDatabases.FirstOrDefault(d => d.Key == activeSession.Key);
                if (match != null)
                {
                    SelectedTargetDatabase = match;
                }
            }
        }

        if (SelectedTargetDatabase == null)
        {
            SampleQueryResult.SetResults(
                new List<string> { "Status", "Message" },
                new List<QueryResultRow>
                {
                    new QueryResultRow
                    {
                        RowIndex = 1,
                        ["Status"] = "Not Connected",
                        ["Message"] = "No active database connection. Click a database in the Object Explorer to connect."
                    }
                },
                "0 ms");
            SampleQueryResult.TargetDatabaseName = "(No Connection)";
            ShowFooter();
            return;
        }

        string targetDbName = SelectedTargetDatabase.FullDisplayName;
        string key = SelectedTargetDatabase.Key;

        if (!DatabaseSessionService.Instance.IsSessionOpen(key))
        {
            var config = new DatabaseConnectionConfig
            {
                Id = SelectedTargetDatabase.ConnectionId ?? 0,
                DatabaseType = SelectedTargetDatabase.DatabaseType,
                Server = SelectedTargetDatabase.Server,
                Port = SelectedTargetDatabase.Port,
                Database = SelectedTargetDatabase.DatabaseName,
                AuthType = SelectedTargetDatabase.AuthType,
                Username = SelectedTargetDatabase.Username,
                FilePath = SelectedTargetDatabase.Server,
                TrustServerCertificate = true
            };
            var connResult = await DatabaseSessionService.Instance.ConnectAsync(config);
            if (!connResult.Success)
            {
                SampleQueryResult.SetResults(
                    new List<string> { "Error", "TargetDatabase" },
                    new List<QueryResultRow>
                    {
                        new QueryResultRow
                        {
                            RowIndex = 1,
                            ["Error"] = connResult.Message,
                            ["TargetDatabase"] = targetDbName
                        }
                    },
                    "0 ms");
                SampleQueryResult.TargetDatabaseName = targetDbName;
                ShowFooter();
                return;
            }
        }
        else
        {
            DatabaseSessionService.Instance.SetActiveSession(key);
        }

        var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(query, key);
        execTime = result.ExecutionTime;
        status = result.Success ? "Success" : "Failed";

        if (result.Success && result.ColumnNames.Count > 0)
        {
            SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
            rowCount = result.Rows.Count;
        }
        else if (!result.Success)
        {
            SampleQueryResult.SetResults(
                new List<string> { "Error", "TargetDatabase" },
                new List<QueryResultRow>
                {
                    new QueryResultRow
                    {
                        RowIndex = 1,
                        ["Error"] = result.Message,
                        ["TargetDatabase"] = targetDbName
                    }
                },
                execTime);
        }

        SampleQueryResult.TargetDatabaseName = targetDbName;

        await Fdb.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
            query, execTime, rowCount, status);

        _ = Sidebar.RefreshAsync();
        ShowFooter();
    }

    [RelayCommand]
    public async Task SaveQuery()
    {
        if (Editor.SelectedTab != null)
        {
            await Fdb.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
                Editor.SelectedTab.Text, "0 ms", 0, "Saved");
            _ = Sidebar.RefreshAsync();
        }
    }

    [RelayCommand]
    public void Disconnect()
    {
        string target = SelectedTargetDatabase?.DisplayName ?? DatabaseSessionService.Instance.TargetDisplayName;
        ConfirmModal.Show(
            title: "Disconnect Database",
            message: $"Are you sure you want to disconnect from {target}? Active queries in tabs will remain preserved.",
            targetName: target,
            isDestructive: true,
            confirmText: "Disconnect",
            onConfirmed: async () =>
            {
                if (SelectedTargetDatabase != null)
                {
                    await DatabaseSessionService.Instance.DisconnectAsync(SelectedTargetDatabase.Key);
                }
                else
                {
                    await DatabaseSessionService.Instance.DisconnectAsync();
                }

                await RefreshAvailableDatabasesAsync();
                SelectedTargetDatabase = null;
                SampleQueryResult.TargetDatabaseName = "(No Connection)";
                Navbar.UpdateConnectionStatus();
                await Sidebar.RefreshAsync();
            });
    }

    [RelayCommand]
    public void ToggleSidebar()
    {
        Sidebar.IsVisible = !Sidebar.IsVisible;
    }

    [RelayCommand]
    public void ShowSidebar()
    {
        Sidebar.IsVisible = true;
    }

    [RelayCommand]
    public void ToggleFooter()
    {
        Footer.IsVisible = !Footer.IsVisible;
        if (Footer.IsVisible && Footer.Tabs.Count == 0)
        {
            ResetFooterTabs();
        }
    }

    [RelayCommand]
    public void ToggleFooterMaximized()
    {
        Footer.ToggleMaximizeCommand.Execute(null);
    }

    [RelayCommand]
    public void ShowFooter()
    {
        Footer.IsVisible = true;
        if (Footer.Tabs.Count == 0)
        {
            ResetFooterTabs();
        }
    }

    [RelayCommand]
    public void RestoreFooterTab(string tabName)
    {
        Footer.IsVisible = true;
        if (!Footer.Tabs.Contains(tabName))
        {
            Footer.Tabs.Add(tabName);
        }
        Footer.SelectedTab = tabName;
    }

    [RelayCommand]
    public void RestoreSidebarTab(string tabName)
    {
        Sidebar.IsVisible = true;
        if (!Sidebar.Tabs.Contains(tabName))
        {
            Sidebar.Tabs.Add(tabName);
        }
        Sidebar.SelectedTab = tabName;
    }

    [RelayCommand]
    public void ResetLayout()
    {
        // Restore Sidebar
        Sidebar.IsVisible = true;
        Sidebar.Tabs.Clear();
        Sidebar.Tabs.Add("Explorer");
        Sidebar.Tabs.Add("Databases");
        Sidebar.Tabs.Add("History");
        Sidebar.SelectedTab = "Explorer";

        // Restore Footer
        Footer.IsVisible = true;
        ResetFooterTabs();

        // Restore Editor
        if (Editor.Tabs.Count == 0)
        {
            Editor.Tabs.Add(new EditorTabItem { Title = "Tab 1", IsActive = true });
        }
    }

    private void ResetFooterTabs()
    {
        Footer.Tabs.Clear();
        Footer.Tabs.Add("Terminal");
        Footer.Tabs.Add("Output");
        Footer.Tabs.Add("Problems");
        Footer.SelectedTab = "Terminal";
    }

    [RelayCommand]
    public void Exit()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            desktop.Shutdown();
        }
    }
}
