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

        _sampleQueryResult.EditCellModal.ExecuteQueryCallback = async sql =>
        {
            string? key = SelectedTargetDatabase?.Key ?? DatabaseSessionService.Instance.ActiveSession?.Key;
            if (string.IsNullOrEmpty(key))
            {
                return true;
            }
            var res = await DatabaseSessionService.Instance.ExecuteQueryAsync(sql, key);
            if (res.Success)
            {
                await Fdb.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
                    sql, res.ExecutionTime, 1, "Success");
                return true;
            }
            return false;
        };

        _sampleQueryResult.EditCellModal.InsertQueryToEditorCallback = sql =>
        {
            if (Editor.SelectedTab != null)
            {
                Editor.SelectedTab.Text = sql;
            }
            else
            {
                Editor.AddTab();
                if (Editor.SelectedTab != null) Editor.SelectedTab.Text = sql;
            }
        };

        _sampleQueryResult.RequestConfirmation = (title, msg, target, onConfirm) =>
        {
            _confirmModal.Show(title, msg, target, isDestructive: true, confirmText: "Delete Row", onConfirmed: onConfirm);
        };
        _sampleQueryResult.ExecuteQueryCallback = _sampleQueryResult.EditCellModal.ExecuteQueryCallback;
        _sampleQueryResult.InsertQueryToEditorCallback = _sampleQueryResult.EditCellModal.InsertQueryToEditorCallback;
                _sampleQueryResult.RefreshCallback = RefreshQueryResultAsync;
        _sampleQueryResult.NavigateToForeignKeyCallback = NavigateToForeignKeyTableAsync;

        // Wire sidebar actions to editor and connect modal
        _sidebar.InsertQueryToEditor = q =>
        {
            if (Editor.SelectedTab != null)
            {
                Editor.SelectedTab.Text = q;
            }
            else
            {
                Editor.AddTab();
                if (Editor.SelectedTab != null) Editor.SelectedTab.Text = q;
            }
        };

        _sidebar.ExecuteQueryInEditor = async q =>
        {
            if (Editor.SelectedTab != null)
            {
                Editor.SelectedTab.Text = q;
            }
            else
            {
                Editor.AddTab();
                if (Editor.SelectedTab != null) Editor.SelectedTab.Text = q;
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

        // Listen for live database connection changes to update the UI badge and reload available databases
        DatabaseSessionService.Instance.ConnectionChanged += () =>
        {
            Navbar.UpdateConnectionStatus();
            _ = RefreshAvailableDatabasesAsync();
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

    private bool _isUpdatingTargetSilently;

    public async Task RefreshAvailableDatabasesAsync()
    {
        var conns = (await Fdb.Services.DatabaseStorageService.Instance.GetRecentConnectionsAsync())
            .Where(c => !c.Server.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase))
            .ToList();
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
            if (SelectedTargetDatabase?.Key != itemToSelect.Key)
            {
                _isUpdatingTargetSilently = true;
                try
                {
                    SelectedTargetDatabase = itemToSelect;
                }
                finally
                {
                    _isUpdatingTargetSilently = false;
                }
            }
        }
        else if (SelectedTargetDatabase != null && !AvailableTargetDatabases.Any(d => d.Key == SelectedTargetDatabase.Key))
        {
            _isUpdatingTargetSilently = true;
            try
            {
                SelectedTargetDatabase = null;
                SampleQueryResult.TargetDatabaseName = "(No Connection)";
            }
            finally
            {
                _isUpdatingTargetSilently = false;
            }
        }
    }

    partial void OnSelectedTargetDatabaseChanged(TargetDatabaseItem? value)
    {
        if (_isUpdatingTargetSilently) return;

        if (value == null)
        {
            SampleQueryResult.TargetDatabaseName = "(No Connection)";
            return;
        }

        SampleQueryResult.TargetDatabaseName = value.FullDisplayName;
        if (DatabaseSessionService.Instance.IsSessionOpen(value.Key) &&
            DatabaseSessionService.Instance.ActiveSession?.Key != value.Key)
        {
            DatabaseSessionService.Instance.SetActiveSession(value.Key);
        }
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
                Password = record.Password ?? string.Empty,
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
            SampleQueryResult.LastExecutedQuery = query;
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

        string? inferredTable = ExtractTableNameFromQuery(query) ?? Sidebar.SelectedTable?.FullName;
        if (!string.IsNullOrWhiteSpace(inferredTable))
        {
            SampleQueryResult.CurrentTableName = inferredTable;
            _ = Task.Run(async () =>
            {
                try
                {
                    var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(inferredTable, key);
                    var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(inferredTable, key);
                    Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                    {
                        if (pks != null && pks.Count > 0) SampleQueryResult.SetPrimaryKeys(pks);
                        if (fks != null && fks.Count > 0) SampleQueryResult.SetForeignKeyRelations(fks);
                    });
                }
                catch { }
            });
        }
        SampleQueryResult.CurrentDatabaseType = SelectedTargetDatabase?.DatabaseType ?? "MSSQL";

        SampleQueryResult.TargetDatabaseName = targetDbName;

        await Fdb.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
            query, execTime, rowCount, status);

        _ = Sidebar.RefreshAsync();
        ShowFooter();
    }

    public async Task NavigateToForeignKeyTableAsync(string targetTable, string targetColumn, string? cellValue, string? sourceColumn)
    {
        string? activeKey = SelectedTargetDatabase?.Key;
        string cleanVal = cellValue?.Replace("'", "''") ?? string.Empty;
        bool isNumeric = long.TryParse(cellValue, out _) || double.TryParse(cellValue, out _);

        string whereClause = string.IsNullOrWhiteSpace(cellValue) || cellValue == "NULL"
            ? string.Empty
            : (isNumeric ? $"WHERE {targetColumn} = {cellValue}" : $"WHERE {targetColumn} = '{cleanVal}'");

        string query = SelectedTargetDatabase?.DatabaseType == "MSSQL"
            ? $"SELECT TOP 100 * FROM {targetTable} {whereClause};".Replace("  ", " ")
            : $"SELECT * FROM {targetTable} {whereClause} LIMIT 100;".Replace("  ", " ");

        if (Editor.SelectedTab != null)
        {
            Editor.SelectedTab.Text = query;
        }
        else
        {
            Editor.AddTab();
            if (Editor.SelectedTab != null) Editor.SelectedTab.Text = query;
        }

        if (!string.IsNullOrEmpty(activeKey) && DatabaseSessionService.Instance.IsSessionOpen(activeKey))
        {
            var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(query, activeKey);
            if (result.Success && result.ColumnNames.Count > 0)
            {
                SampleQueryResult.CurrentTableName = targetTable;
                SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
                SampleQueryResult.LastExecutedQuery = query;
                SampleQueryResult.LastQueryMessage = $"Navigated to {targetTable} via FK [{sourceColumn} -> {targetTable}.{targetColumn}] ({result.Rows.Count} rows)";
                SampleQueryResult.HasLastQueryMessage = true;

                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(targetTable, activeKey);
                        var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(targetTable, activeKey);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (pks != null && pks.Count > 0) SampleQueryResult.SetPrimaryKeys(pks);
                            if (fks != null && fks.Count > 0) SampleQueryResult.SetForeignKeyRelations(fks);
                        });
                    }
                    catch { }
                });

                ShowFooter();
                return;
            }
        }

        SampleQueryResult.LoadSampleReferencedTable(targetTable, targetColumn, cellValue);
        SampleQueryResult.LastExecutedQuery = query;
        SampleQueryResult.LastQueryMessage = $"Navigated to {targetTable} via FK [{sourceColumn} -> {targetTable}.{targetColumn}] ({SampleQueryResult.TotalRows} rows)";
        SampleQueryResult.HasLastQueryMessage = true;
        ShowFooter();
    }

    public async Task RefreshQueryResultAsync()
    {
        string? activeKey = SelectedTargetDatabase?.Key;
        if (string.IsNullOrEmpty(activeKey) || SelectedTargetDatabase == null || !DatabaseSessionService.Instance.IsSessionOpen(activeKey))
        {
            SampleQueryResult.ResetToSampleData();
            return;
        }

        string queryToRun;
        if (!string.IsNullOrWhiteSpace(SampleQueryResult.LastExecutedQuery) &&
            SampleQueryResult.LastExecutedQuery.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            queryToRun = SampleQueryResult.LastExecutedQuery;
        }
        else if (!string.IsNullOrWhiteSpace(SampleQueryResult.CurrentTableName))
        {
            queryToRun = SampleQueryResult.CurrentDatabaseType == "MSSQL"
                ? $"SELECT TOP 100 * FROM {SampleQueryResult.CurrentTableName};"
                : $"SELECT * FROM {SampleQueryResult.CurrentTableName} LIMIT 100;";
        }
        else if (!string.IsNullOrWhiteSpace(Editor.SelectedTab?.Text) &&
                 Editor.SelectedTab.Text.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            queryToRun = Editor.SelectedTab.Text;
        }
        else
        {
            queryToRun = "SELECT * FROM Users LIMIT 100;";
        }

        var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(queryToRun, activeKey);
        if (result.Success && result.ColumnNames.Count > 0)
        {
            SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
            SampleQueryResult.LastExecutedQuery = queryToRun;
            SampleQueryResult.LastQueryMessage = $"Table refreshed ({result.Rows.Count} rows) · {result.ExecutionTime}";
            SampleQueryResult.HasLastQueryMessage = true;

            string? inferredTable = ExtractTableNameFromQuery(queryToRun) ?? SampleQueryResult.CurrentTableName;
            if (!string.IsNullOrWhiteSpace(inferredTable))
            {
                _ = Task.Run(async () =>
                {
                    try
                    {
                        var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(inferredTable, activeKey);
                        var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(inferredTable, activeKey);
                        Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                        {
                            if (pks != null && pks.Count > 0) SampleQueryResult.SetPrimaryKeys(pks);
                            if (fks != null && fks.Count > 0) SampleQueryResult.SetForeignKeyRelations(fks);
                        });
                    }
                    catch { }
                });
            }
        }
        else if (!result.Success)
        {
            SampleQueryResult.LastQueryMessage = $"Refresh failed: {result.Message}";
            SampleQueryResult.HasLastQueryMessage = true;
        }
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

    private static string? ExtractTableNameFromQuery(string query)
    {
        if (string.IsNullOrWhiteSpace(query)) return null;
        var match = System.Text.RegularExpressions.Regex.Match(
            query,
            @"\bFROM\s+([\[\]""`\w\.]+)",
            System.Text.RegularExpressions.RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return match.Groups[1].Value.Trim().TrimEnd(';');
        }
        return null;
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
