using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

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
    private TerminalViewModel _terminal;

    [ObservableProperty]
    private ConnectDatabaseModalViewModel _connectModal;

    [ObservableProperty]
    private ConfirmActionModalViewModel _confirmModal;

    [ObservableProperty]
    private ExportDataModalViewModel _exportModal;

    [ObservableProperty]
    private DatabaseDiagramModalViewModel _diagramModal;

    [ObservableProperty]
    private SettingsModalViewModel _settingsModal;

    [ObservableProperty]
    private UpdateModalViewModel _updateModal;

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
        _terminal = new TerminalViewModel();
        _connectModal = new ConnectDatabaseModalViewModel();
        _confirmModal = new ConfirmActionModalViewModel();
        _exportModal = new ExportDataModalViewModel();
        _diagramModal = new DatabaseDiagramModalViewModel();
        _settingsModal = new SettingsModalViewModel();
        _updateModal = new UpdateModalViewModel();
        _sampleQueryResult = new QueryResultTableViewModel();
        _sampleSchema = new SchemaPropertiesTableViewModel();

        _exportModal.PerformExportCallback = async (source, tableName, includeHeaders, filePath) =>
        {
            return await ExecuteDataExportAsync(source, tableName, includeHeaders, filePath);
        };

        _diagramModal.OpenTableDataCallback = async tableName =>
        {
            string q = $"SELECT * FROM {tableName} LIMIT 100;";
            if (Editor.SelectedTab != null) Editor.SelectedTab.Text = q;
            await ExecuteQuery();
        };

        _diagramModal.GenerateDdlCallback = tableName =>
        {
            GenerateTableDdlFor(tableName);
        };
        _diagramModal.RefreshDiagramCallback = async () =>
        {
            await OpenDiagramModal(forceRefresh: true);
        };
        _diagramModal.RefreshDiagramCallbackWithFlag = async (forceRefresh) =>
        {
            await OpenDiagramModal(forceRefresh: forceRefresh);
        };

        _footer.TerminalTabActivated += () =>
        {
            if (_terminal.Sessions.Count == 0)
            {
                _terminal.NewSession();
            }
        };

        ResetFooterTabs();

        _sampleQueryResult.EditCellModal.ExecuteQueryDetailedCallback = async sql =>
        {
            string? key = SelectedTargetDatabase?.Key ?? DatabaseSessionService.Instance.ActiveSession?.Key;
            if (string.IsNullOrEmpty(key))
            {
                return (true, null);
            }
            var res = await DatabaseSessionService.Instance.ExecuteQueryAsync(sql, key);
            if (res.Success)
            {
                await Quell.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
                    sql, res.ExecutionTime, res.RowCount > 0 ? res.RowCount : 1, "Success");
                return (true, null);
            }
            return (false, res.Message);
        };
        _sampleQueryResult.EditCellModal.ExecuteQueryCallback = async sql =>
        {
            var (ok, _) = await _sampleQueryResult.EditCellModal.ExecuteQueryDetailedCallback(sql);
            return ok;
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
            Terminal.UpdateConnectionContext(DatabaseSessionService.Instance.ActiveConfig);
            _ = RefreshAvailableDatabasesAsync();
        };

        // Initialize SQLite local storage, load available databases and tables
        _ = InitializeStartupAsync();
    }

    private async Task InitializeStartupAsync()
    {
        await Quell.Services.DatabaseStorageService.Instance.InitializeAsync();
        await RefreshAvailableDatabasesAsync();
        await Sidebar.RefreshAsync();

        // If an update was discovered during splash screen, surface the modal
        if (AppUpdateService.Instance.IsUpdateAvailable && AppUpdateService.Instance.LatestRelease != null)
        {
            Avalonia.Threading.Dispatcher.UIThread.Post(() =>
            {
                UpdateModal.ApplyRelease(AppUpdateService.Instance.LatestRelease);
                UpdateModal.Open();
            });
        }

        // Secondary non-blocking background check in case network was slow at launch
        _ = Task.Run(async () =>
        {
            await Task.Delay(2000).ConfigureAwait(false);
            if (!AppUpdateService.Instance.IsUpdateAvailable)
            {
                await AppUpdateService.Instance.CheckForUpdatesAsync().ConfigureAwait(false);
            }
        });
    }

    private bool _isUpdatingTargetSilently;

    public async Task RefreshAvailableDatabasesAsync()
    {
        var conns = (await Quell.Services.DatabaseStorageService.Instance.GetRecentConnectionsAsync())
            .Where(c => !c.Server.Contains("quell", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("quell", StringComparison.OrdinalIgnoreCase) && !c.Server.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase))
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
        string query = Editor.GetQueryToExecute();
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
            ShowQueryResultFooter();
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
                ShowQueryResultFooter();
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
        else if (result.Success && result.ColumnNames.Count == 0)
        {
            SampleQueryResult.SetResults(
                new List<string> { "Status", "Message", "TargetDatabase" },
                new List<QueryResultRow>
                {
                    new QueryResultRow
                    {
                        RowIndex = 1,
                        ["Status"] = "Success",
                        ["Message"] = result.Message,
                        ["TargetDatabase"] = targetDbName
                    }
                },
                execTime);
            rowCount = result.RowCount;
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
        SampleQueryResult.CurrentDatabaseType = SelectedTargetDatabase?.DatabaseType ?? DatabaseSessionService.Instance.ActiveSession?.Config.DatabaseType ?? "MSSQL";

        SampleQueryResult.TargetDatabaseName = targetDbName;

        await Quell.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
            query, execTime, rowCount, status);

        _ = Sidebar.RefreshAsync();
        ShowQueryResultFooter();
    }

    public async Task NavigateToForeignKeyTableAsync(string targetTable, string targetColumn, string? cellValue, string? sourceColumn)
    {
        string? activeKey = SelectedTargetDatabase?.Key;
        string cleanVal = cellValue?.Replace("'", "''") ?? string.Empty;
        bool isNumeric = long.TryParse(cellValue, out _) || double.TryParse(cellValue, out _);

        string whereClause;
        if (string.IsNullOrWhiteSpace(cellValue) || cellValue == "NULL")
        {
            whereClause = string.Empty;
        }
        else if (targetTable.Equals("Users", StringComparison.OrdinalIgnoreCase))
        {
            if (sourceColumn != null && (sourceColumn.Equals("Course", StringComparison.OrdinalIgnoreCase) || sourceColumn.Equals("TITLE", StringComparison.OrdinalIgnoreCase) || sourceColumn.Equals("CourseTitle", StringComparison.OrdinalIgnoreCase)))
            {
                whereClause = isNumeric ? $"WHERE Id = {cellValue} OR Course = '{cleanVal}'" : $"WHERE Course = '{cleanVal}'";
            }
            else if (isNumeric)
            {
                whereClause = $"WHERE {targetColumn} = {cellValue}";
            }
            else
            {
                string col = targetColumn.Equals("Id", StringComparison.OrdinalIgnoreCase) ? "Name" : targetColumn;
                whereClause = $"WHERE {col} = '{cleanVal}'";
            }
        }
        else if (isNumeric)
        {
            whereClause = $"WHERE {targetColumn} = {cellValue}";
        }
        else
        {
            whereClause = $"WHERE {targetColumn} = '{cleanVal}'";
        }

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

                ShowQueryResultFooter();
                return;
            }
        }

        SampleQueryResult.LoadSampleReferencedTable(targetTable, targetColumn, cellValue);
        SampleQueryResult.LastExecutedQuery = query;
        string matchedInfo = SampleQueryResult.SelectedRow != null ? $" (User: {SampleQueryResult.SelectedRow.Name})" : "";
        SampleQueryResult.LastQueryMessage = $"Navigated to {targetTable} via FK [{sourceColumn} -> {targetTable}]{matchedInfo} ({SampleQueryResult.TotalRows} rows)";
        SampleQueryResult.HasLastQueryMessage = true;
        ShowQueryResultFooter();
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
            await Quell.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
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
    public void CloseSidebar()
    {
        Sidebar.IsVisible = false;
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
    public void ToggleTerminal()
    {
        if (!Footer.IsVisible)
        {
            Footer.IsVisible = true;

            if (!Footer.Tabs.Contains("Terminal"))
            {
                Footer.Tabs.Add("Terminal");
            }
            Footer.SelectedTab = "Terminal";
            if (Terminal.Sessions.Count == 0)
            {
                Terminal.NewSession();
            }
        }
        else if (Footer.SelectedTab == "Terminal")
        {
            Footer.IsVisible = false;
        }
        else
        {
            if (!Footer.Tabs.Contains("Terminal"))
            {
                Footer.Tabs.Add("Terminal");
            }
            Footer.SelectedTab = "Terminal";
            if (Terminal.Sessions.Count == 0)
            {
                Terminal.NewSession();
            }
        }
    }

    [RelayCommand]
    public void RunSelectionInTerminal(string? queryText = null)
    {
        var textToSend = queryText;
        if (string.IsNullOrWhiteSpace(textToSend))
        {
            textToSend = Editor.SelectedTab?.Text;
        }
        if (string.IsNullOrWhiteSpace(textToSend)) return;

        Footer.IsVisible = true;

        if (!Footer.Tabs.Contains("Terminal"))
        {
            Footer.Tabs.Add("Terminal");
        }
        Footer.SelectedTab = "Terminal";

        Terminal.SendTextToActiveTerminal(textToSend);
    }

    [RelayCommand]
    public void LaunchCliInTerminal(Quell.Services.DatabaseProviders.DatabaseConnectionConfig? config = null)
    {
        Footer.IsVisible = true;

        if (!Footer.Tabs.Contains("Terminal"))
        {
            Footer.Tabs.Add("Terminal");
        }
        Footer.SelectedTab = "Terminal";
        Terminal.LaunchDatabaseCli(config ?? Quell.Services.DatabaseProviders.DatabaseSessionService.Instance.ActiveConfig);
    }

    [RelayCommand]
    public void ShowFooter()
    {
        Footer.IsVisible = true;
    }

    [RelayCommand]
    public void ShowQueryResultFooter()
    {
        Footer.IsVisible = true;
        if (!Footer.Tabs.Contains("Results"))
        {
            Footer.Tabs.Insert(0, "Results");
        }
        Footer.SelectedTab = "Results";
    }

    [RelayCommand]
    public void ShowSchemaPropertiesFooter()
    {
        Footer.IsVisible = true;
        if (!Footer.Tabs.Contains("Schema"))
        {
            int idx = Footer.Tabs.IndexOf("Results");
            Footer.Tabs.Insert(idx >= 0 ? idx + 1 : 0, "Schema");
        }
        Footer.SelectedTab = "Schema";
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

    [RelayCommand]
    public void ResetFooterTabs()
    {
        Footer.Tabs.Clear();
        Footer.Tabs.Add("Results");
        Footer.Tabs.Add("Terminal");
        Footer.SelectedTab = "Results";
    }

    [RelayCommand]
    public void SwitchFooterTab()
    {
        Footer.SwitchTab();
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

    #region Database Management, Backup, Export &amp; Diagram Commands

    public void PopulateExportModalTables(string? defaultTable = null)
    {
        var tables = new List<string>();
        var cached = DatabaseSessionService.Instance.GetActiveCachedTables();
        foreach (var t in cached)
        {
            tables.Add(t.FullName);
        }

        var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
        if (tables.Count == 0 && activeDb != null)
        {
            foreach (var t in activeDb.AllTableNodes)
            {
                tables.Add(t.FullName);
            }
        }

        if (tables.Count == 0)
        {
            tables.AddRange(new[] { "Users", "Orders", "Products", "Roles" });
        }

        string? selected = defaultTable ?? Sidebar.SelectedTable?.FullName ?? SampleQueryResult.CurrentTableName;
        ExportModal.SetAvailableTables(tables, selected);
    }

    [RelayCommand]
    public void OpenExportTableModal()
    {
        PopulateExportModalTables();
        ExportModal.Open();
    }

    [RelayCommand]
    public void QuickExportExcel()
    {
        PopulateExportModalTables();
        ExportModal.SelectedFormat = "Excel Spreadsheet (*.xlsx)";
        ExportModal.ExportSource = "Specific Database Table";
        ExportModal.Open();
    }

    [RelayCommand]
    public async Task BackupDatabase()
    {
        await ExecuteDatabaseBackupAsync();
    }

    [RelayCommand]
    public async Task RestoreDatabase()
    {
        await ExecuteDatabaseRestoreAsync();
    }

    [RelayCommand]
    public async Task OpenDiagramModal()
    {
        await OpenDiagramModalInternal(forceRefresh: false);
    }

    [RelayCommand]
    public async Task ForceRefreshDiagramModal()
    {
        await OpenDiagramModalInternal(forceRefresh: true);
    }

    public async Task OpenDiagramModal(bool forceRefresh)
    {
        await OpenDiagramModalInternal(forceRefresh);
    }

    private async Task OpenDiagramModalInternal(bool forceRefresh)
    {
        var activeSession = DatabaseSessionService.Instance.ActiveSession;
        string dbKey = activeSession?.Key ?? SelectedTargetDatabase?.Key ?? activeSession?.DisplayName ?? SelectedTargetDatabase?.DisplayName ?? "Default";
        string dbName = activeSession?.DisplayName ?? SelectedTargetDatabase?.DisplayName ?? "Quell Studio DB";
        string dbType = activeSession?.DatabaseType ?? SelectedTargetDatabase?.DatabaseType ?? "SQLite";

        // 1. Check in-memory cache if not forcing refresh
        if (!forceRefresh && DiagramCacheService.Instance.TryGet(dbKey, out var cachedData) && cachedData != null)
        {
            var cachedTables = DatabaseSessionService.Instance.GetActiveCachedTables();
            var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            if (cachedTables.Count == 0 && activeDb != null)
            {
                cachedTables = activeDb.AllTableNodes.Select(t => t.Table).ToList();
            }

            // Check if schema changed (e.g. new table added or table dropped)
            bool schemaChanged = cachedTables.Count > 0 &&
                (cachedTables.Count != cachedData.TableCount ||
                 !cachedTables.All(t => cachedData.KnownTableNames.Contains(t.TableName)));

            if (!schemaChanged)
            {
                DiagramModal.LoadFromCache(cachedData.Tables.Select(t => t.Clone()), dbName, dbType, dbKey, cachedData.CachedAt);
                DiagramModal.StatusMessage = $"Loaded instantly from cache ({cachedData.TableCount} tables)";
                DiagramModal.HasStatusMessage = true;
                DiagramModal.Open();
                return;
            }
        }

        // 2. Cache miss, schema changed, or forced reload:
        DiagramModal.DatabaseTitle = dbName;
        DiagramModal.DatabaseType = dbType;
        DiagramModal.DatabaseKey = dbKey;
        DiagramModal.IsLoading = true;
        DiagramModal.LoadingMessage = forceRefresh ? "Re-analyzing database schema & refreshing cache..." : "Discovering database tables and relationships...";
        DiagramModal.LoadingDetailText = "Querying schema information...";
        DiagramModal.LoadingProgress = 0.0;
        DiagramModal.AllTables.Clear();
        DiagramModal.FilteredTables.Clear();
        DiagramModal.Open();

        await Task.Delay(150);

        try
        {
            var cachedTables = DatabaseSessionService.Instance.GetActiveCachedTables();
            var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            if (cachedTables.Count == 0 && activeDb != null)
            {
                cachedTables = activeDb.AllTableNodes.Select(t => t.Table).ToList();
            }

            if (cachedTables.Count == 0)
            {
                var sampleTables = GetSampleDiagramTables();
                DiagramModal.LoadTables(sampleTables, dbName, dbType, dbKey);
                DiagramCacheService.Instance.Store(dbKey, dbName, dbType, sampleTables);
                return;
            }

            var tables = new System.Collections.Concurrent.ConcurrentBag<DiagramTableItem>();
            int total = cachedTables.Count;
            int processed = 0;

            using var semaphore = new System.Threading.SemaphoreSlim(4);
            var tasks = cachedTables.Select(async t =>
            {
                await semaphore.WaitAsync();
                try
                {
                    var pksTask = DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(t.FullName);
                    var fksTask = DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(t.FullName);
                    var colsTask = DatabaseSessionService.Instance.GetColumnsAsync(t.FullName);

                    await Task.WhenAll(pksTask, fksTask, colsTask);

                    var pks = await pksTask;
                    var fks = await fksTask;
                    var cols = await colsTask;

                    var item = new DiagramTableItem
                    {
                        TableName = t.TableName,
                        Schema = t.SchemaName,
                        DatabaseType = dbType
                    };

                    foreach (var c in cols)
                    {
                        bool isPk = pks.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase);
                        var fk = fks.FirstOrDefault(f => f.ColumnName.Equals(c.ColumnName, StringComparison.OrdinalIgnoreCase));

                        item.Columns.Add(new DiagramColumnItem
                        {
                            ColumnName = c.ColumnName,
                            DataType = c.DataType,
                            IsPrimaryKey = isPk,
                            IsForeignKey = fk != null,
                            ReferencedTable = fk?.TargetTable,
                            ReferencedColumn = fk?.TargetColumn
                        });

                        if (fk != null)
                        {
                            item.OutgoingRelations.Add($"{item.TableName}.{c.ColumnName} -> {fk.TargetTable}.{fk.TargetColumn}");
                        }
                    }

                    tables.Add(item);

                    int current = System.Threading.Interlocked.Increment(ref processed);
                    double progress = (double)current / total * 100.0;

                    void UpdateProgress()
                    {
                        DiagramModal.LoadingMessage = $"Analyzing schema: {t.TableName} ({current} of {total})";
                        DiagramModal.LoadingDetailText = $"Loaded {cols.Count} columns for {t.TableName}";
                        DiagramModal.LoadingProgress = progress;
                    }

                    if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
                    {
                        UpdateProgress();
                    }
                    else
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateProgress);
                    }
                }
                finally
                {
                    semaphore.Release();
                }
            });

            await Task.WhenAll(tasks);

            var sortedTables = tables.OrderBy(t => t.TableName).ToList();
            if (sortedTables.Count == 0)
            {
                sortedTables = GetSampleDiagramTables();
            }

            DiagramModal.LoadTables(sortedTables, dbName, dbType, dbKey);
            DiagramCacheService.Instance.Store(dbKey, dbName, dbType, sortedTables);
            DiagramModal.StatusMessage = $"Diagram generated & cached ({sortedTables.Count} tables)";
            DiagramModal.HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            DiagramModal.StatusMessage = $"Schema analysis warning: {ex.Message}";
            DiagramModal.HasStatusMessage = true;
            if (DiagramModal.AllTables.Count == 0)
            {
                var samples = GetSampleDiagramTables();
                DiagramModal.LoadTables(samples, dbName, dbType, dbKey);
                DiagramCacheService.Instance.Store(dbKey, dbName, dbType, samples);
            }
        }
        finally
        {
            DiagramModal.IsLoading = false;
        }
    }

    [RelayCommand]
    public void GenerateTableDdl()
    {
        string target = Sidebar.SelectedTable?.FullName ?? SampleQueryResult.CurrentTableName ?? "Users";
        GenerateTableDdlFor(target);
    }

    [RelayCommand]
    public async Task ShowTableProperties()
    {
        string? target = Sidebar.SelectedTable?.FullName ?? Sidebar.SelectedTable?.TableName;
        if (string.IsNullOrWhiteSpace(target))
        {
            var cached = DatabaseSessionService.Instance.GetActiveCachedTables();
            var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            if (cached.Count == 0 && activeDb != null)
            {
                cached = activeDb.AllTableNodes.Select(t => t.Table).ToList();
            }
            target = cached.FirstOrDefault()?.FullName ?? SampleQueryResult.CurrentTableName ?? "Users";
        }
        await LoadTablePropertiesAsync(target);
        ShowSchemaPropertiesFooter();
    }

    [RelayCommand]
    public async Task OptimizeDatabase()
    {
        var activeSession = DatabaseSessionService.Instance.ActiveSession;
        var activeNode = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);

        string key = SelectedTargetDatabase?.Key ?? activeSession?.Key ?? activeNode?.ConnectionKey ?? string.Empty;
        string dbType = SelectedTargetDatabase?.DatabaseType ?? activeSession?.DatabaseType ?? activeNode?.DatabaseType ?? "SQLite";
        string dbName = SelectedTargetDatabase?.DisplayName ?? activeSession?.DisplayName ?? activeNode?.DatabaseName ?? "Active Database";

        if (string.IsNullOrEmpty(key) && DatabaseSessionService.Instance.ActiveSession == null)
        {
            SampleQueryResult.LastQueryMessage = "Please connect to a database before optimizing.";
            SampleQueryResult.HasLastQueryMessage = true;
            ShowQueryResultFooter();
            return;
        }

        if (string.IsNullOrEmpty(key) && DatabaseSessionService.Instance.ActiveSession != null)
        {
            key = DatabaseSessionService.Instance.ActiveSession.Key;
            dbType = DatabaseSessionService.Instance.ActiveSession.DatabaseType;
        }

        string sql;
        if (dbType.Equals("PostgreSQL", StringComparison.OrdinalIgnoreCase))
        {
            sql = "VACUUM;";
        }
        else if (dbType.Equals("SQLite", StringComparison.OrdinalIgnoreCase))
        {
            sql = "VACUUM;";
        }
        else if (dbType.Equals("MySQL", StringComparison.OrdinalIgnoreCase))
        {
            var cached = DatabaseSessionService.Instance.GetActiveCachedTables();
            string tableTarget = Sidebar.SelectedTable?.TableName ?? cached.FirstOrDefault()?.TableName ?? "Users";
            sql = $"OPTIMIZE TABLE {tableTarget};";
        }
        else
        {
            sql = "CHECKPOINT;";
        }

        SampleQueryResult.LastQueryMessage = $"Executing {sql} on {dbName}...";
        SampleQueryResult.HasLastQueryMessage = true;
        ShowQueryResultFooter();

        var res = await DatabaseSessionService.Instance.ExecuteQueryAsync(sql, key);
        if (res.Success)
        {
            SampleQueryResult.LastQueryMessage = $"Database optimization ({sql.Trim(';')}) completed successfully for '{dbName}' ({res.ExecutionTime})";
        }
        else
        {
            SampleQueryResult.LastQueryMessage = $"Optimization notice: {res.Message}";
        }
        SampleQueryResult.HasLastQueryMessage = true;
    }

    [RelayCommand]
    public async Task RefreshDatabaseSchema()
    {
        await Sidebar.RefreshAsync();
        string? dbKey = DatabaseSessionService.Instance.ActiveSession?.Key ?? SelectedTargetDatabase?.Key;
        if (!string.IsNullOrEmpty(dbKey))
        {
            DiagramCacheService.Instance.Invalidate(dbKey);
        }
        SampleQueryResult.LastQueryMessage = "Database schema & tables refreshed (Diagram cache invalidated)";
        SampleQueryResult.HasLastQueryMessage = true;
    }

    private async Task<(bool Success, string Message, int RowCount)> ExecuteDataExportAsync(
        string source, string tableName, bool includeHeaders, string filePath)
    {
        try
        {
            List<string> columns = new();
            List<Dictionary<string, string>> rows = new();

            if (source.Contains("Table", StringComparison.OrdinalIgnoreCase) && !string.IsNullOrWhiteSpace(tableName))
            {
                string key = SelectedTargetDatabase?.Key ?? DatabaseSessionService.Instance.ActiveSession?.Key ?? string.Empty;
                var res = await DatabaseSessionService.Instance.ExecuteQueryAsync($"SELECT * FROM {tableName};", key);
                if (!res.Success)
                {
                    return (false, $"Failed to query table {tableName}: {res.Message}", 0);
                }
                columns = res.ColumnNames;
                foreach (var r in res.Rows)
                {
                    var dict = new Dictionary<string, string>();
                    foreach (var c in columns)
                    {
                        dict[c] = r[c];
                    }
                    rows.Add(dict);
                }
            }
            else
            {
                columns = SampleQueryResult.LastColumns?.ToList() ?? SampleQueryResult.Rows.FirstOrDefault()?.Values.Keys.ToList() ?? new List<string>();
                foreach (var r in SampleQueryResult.Rows)
                {
                    var dict = new Dictionary<string, string>();
                    foreach (var c in columns)
                    {
                        dict[c] = r[c];
                    }
                    rows.Add(dict);
                }
            }

            if (columns.Count == 0 && rows.Count == 0)
            {
                return (false, "No data available to export.", 0);
            }

            string dir = System.IO.Path.GetDirectoryName(filePath) ?? "";
            if (!string.IsNullOrEmpty(dir) && !System.IO.Directory.Exists(dir))
            {
                System.IO.Directory.CreateDirectory(dir);
            }

            string ext = System.IO.Path.GetExtension(filePath).ToLowerInvariant();
            if (ext is ".xlsx" or ".xml")
            {
                string excelContent = GenerateExcelXml(columns, rows);
                await System.IO.File.WriteAllTextAsync(filePath, excelContent, System.Text.Encoding.UTF8);
            }
            else if (ext == ".json")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("[");
                for (int i = 0; i < rows.Count; i++)
                {
                    sb.Append("  { ");
                    var pairs = rows[i].Select(kv => $"\"{kv.Key.Replace("\"", "\\\"")}\": \"{kv.Value.Replace("\"", "\\\"")}\"");
                    sb.Append(string.Join(", ", pairs));
                    sb.AppendLine(i < rows.Count - 1 ? " }," : " }");
                }
                sb.AppendLine("]");
                await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            }
            else if (ext == ".sql")
            {
                string target = !string.IsNullOrWhiteSpace(tableName) ? tableName : "ExportedTable";
                var sb = new System.Text.StringBuilder();
                sb.AppendLine($"-- Exported data for table {target}");
                foreach (var row in rows)
                {
                    var colNames = string.Join(", ", columns.Select(c => $"[{c}]"));
                    var values = string.Join(", ", columns.Select(c => $"'{row.GetValueOrDefault(c, "").Replace("'", "''")}'"));
                    sb.AppendLine($"INSERT INTO {target} ({colNames}) VALUES ({values});");
                }
                await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            }
            else if (ext == ".html")
            {
                var sb = new System.Text.StringBuilder();
                sb.AppendLine("<!DOCTYPE html><html><head><meta charset='utf-8'/><style>table{border-collapse:collapse;font-family:sans-serif;width:100%}th,td{border:1px solid #ccc;padding:6px 10px;text-align:left}th{background:#f0f0f0}</style></head><body>");
                sb.AppendLine("<table><thead><tr>");
                foreach (var col in columns) sb.Append($"<th>{System.Net.WebUtility.HtmlEncode(col)}</th>");
                sb.AppendLine("</tr></thead><tbody>");
                foreach (var row in rows)
                {
                    sb.Append("<tr>");
                    foreach (var col in columns) sb.Append($"<td>{System.Net.WebUtility.HtmlEncode(row.GetValueOrDefault(col, ""))}</td>");
                    sb.AppendLine("</tr>");
                }
                sb.AppendLine("</tbody></table></body></html>");
                await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), System.Text.Encoding.UTF8);
            }
            else
            {
                char delim = ext == ".tsv" ? '\t' : ',';
                var sb = new System.Text.StringBuilder();
                if (includeHeaders)
                {
                    sb.AppendLine(string.Join(delim, columns.Select(c => EscapeCsv(c, delim))));
                }
                foreach (var row in rows)
                {
                    sb.AppendLine(string.Join(delim, columns.Select(c => EscapeCsv(row.GetValueOrDefault(c, ""), delim))));
                }
                await System.IO.File.WriteAllTextAsync(filePath, sb.ToString(), new System.Text.UTF8Encoding(true));
            }

            return (true, $"Exported {rows.Count} rows to {System.IO.Path.GetFileName(filePath)}", rows.Count);
        }
        catch (Exception ex)
        {
            return (false, $"Export error: {ex.Message}", 0);
        }
    }

    private static string EscapeCsv(string val, char delimiter)
    {
        if (string.IsNullOrEmpty(val)) return "";
        if (val.Contains(delimiter) || val.Contains('"') || val.Contains('\n') || val.Contains('\r'))
        {
            return $"\"{val.Replace("\"", "\"\"")}\"";
        }
        return val;
    }

    private static string GenerateExcelXml(List<string> columns, List<Dictionary<string, string>> rows)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("<?xml version=\"1.0\"?>");
        sb.AppendLine("<?mso-application progid=\"Excel.Sheet\"?>");
        sb.AppendLine("<Workbook xmlns=\"urn:schemas-microsoft-com:office:spreadsheet\"");
        sb.AppendLine(" xmlns:o=\"urn:schemas-microsoft-com:office:office\"");
        sb.AppendLine(" xmlns:x=\"urn:schemas-microsoft-com:office:excel\"");
        sb.AppendLine(" xmlns:ss=\"urn:schemas-microsoft-com:office:spreadsheet\">");
        sb.AppendLine(" <Styles>");
        sb.AppendLine("  <Style ss:ID=\"HeaderStyle\">");
        sb.AppendLine("   <Font ss:Bold=\"1\" ss:Color=\"#FFFFFF\"/>");
        sb.AppendLine("   <Interior ss:Color=\"#0284C7\" ss:Pattern=\"Solid\"/>");
        sb.AppendLine("  </Style>");
        sb.AppendLine(" </Styles>");
        sb.AppendLine(" <Worksheet ss:Name=\"ExportedData\">");
        sb.AppendLine("  <Table>");

        sb.AppendLine("   <Row ss:StyleID=\"HeaderStyle\">");
        foreach (var col in columns)
        {
            sb.AppendLine($"    <Cell><Data ss:Type=\"String\">{System.Security.SecurityElement.Escape(col)}</Data></Cell>");
        }
        sb.AppendLine("   </Row>");

        foreach (var row in rows)
        {
            sb.AppendLine("   <Row>");
            foreach (var col in columns)
            {
                string val = row.GetValueOrDefault(col, "");
                string type = double.TryParse(val, out _) && !val.StartsWith("0") && val.Length < 11 ? "Number" : "String";
                sb.AppendLine($"    <Cell><Data ss:Type=\"{type}\">{System.Security.SecurityElement.Escape(val)}</Data></Cell>");
            }
            sb.AppendLine("   </Row>");
        }

        sb.AppendLine("  </Table>");
        sb.AppendLine(" </Worksheet>");
        sb.AppendLine("</Workbook>");
        return sb.ToString();
    }

    private async Task ExecuteDatabaseBackupAsync()
    {
        try
        {
            var activeSession = DatabaseSessionService.Instance.ActiveSession;
            string dbType = activeSession?.DatabaseType ?? SelectedTargetDatabase?.DatabaseType ?? "SQLite";
            string dbName = activeSession?.DisplayName ?? SelectedTargetDatabase?.DisplayName ?? "quell_db";
            string cleanName = dbName.Replace(" ", "_").Replace(":", "_").Replace("/", "_").Replace("\\", "_");
            string timestamp = DateTime.Now.ToString("yyyyMMdd_HHmmss");

            string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktopPath)) desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string key = SelectedTargetDatabase?.Key ?? activeSession?.Key ?? string.Empty;

            var tables = await DatabaseSessionService.Instance.GetTablesAsync(key);
            var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            if (tables.Count == 0 && activeDb != null)
            {
                tables = activeDb.AllTableNodes.Select(t => t.Table).ToList();
            }

            string backupFile = System.IO.Path.Combine(desktopPath, $"{cleanName}_backup_{timestamp}.sql");

            var sb = new System.Text.StringBuilder();
            sb.AppendLine($"-- =============================================");
            sb.AppendLine($"-- Quell Database Backup");
            sb.AppendLine($"-- Database: {dbName} ({dbType})");
            sb.AppendLine($"-- Backup Date: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"-- Total Tables: {tables.Count}");
            sb.AppendLine($"-- =============================================\n");

            foreach (var t in tables)
            {
                sb.AppendLine($"-- Table: {t.FullName}");
                var queryRes = await DatabaseSessionService.Instance.ExecuteQueryAsync($"SELECT * FROM {t.FullName};", key);
                if (queryRes.Success && queryRes.ColumnNames.Count > 0)
                {
                    string colList = string.Join(", ", queryRes.ColumnNames.Select(c => $"[{c}]"));
                    foreach (var row in queryRes.Rows)
                    {
                        var valList = string.Join(", ", queryRes.ColumnNames.Select(c => $"'{row[c].Replace("'", "''")}'"));
                        sb.AppendLine($"INSERT INTO {t.FullName} ({colList}) VALUES ({valList});");
                    }
                }
                sb.AppendLine();
            }

            await System.IO.File.WriteAllTextAsync(backupFile, sb.ToString(), System.Text.Encoding.UTF8);

            SampleQueryResult.LastQueryMessage = $"Backup saved: {System.IO.Path.GetFileName(backupFile)} ({tables.Count} tables)";
            SampleQueryResult.HasLastQueryMessage = true;
            ShowQueryResultFooter();
        }
        catch (Exception ex)
        {
            SampleQueryResult.LastQueryMessage = $"Backup failed: {ex.Message}";
            SampleQueryResult.HasLastQueryMessage = true;
        }
    }

    private async Task ExecuteDatabaseRestoreAsync()
    {
        SampleQueryResult.LastQueryMessage = "Ready to restore. Open a .sql backup script in the Editor and click Run (F5).";
        SampleQueryResult.HasLastQueryMessage = true;
        ShowQueryResultFooter();
    }

    private void GenerateTableDdlFor(string tableName)
    {
        string clean = tableName.Trim('[', ']', '"');
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"-- DDL Script generated by Quell for {clean}");
        sb.AppendLine($"CREATE TABLE {clean} (");

        var cols = DatabaseSessionService.Instance.GetColumnsAsync(clean).GetAwaiter().GetResult();
        var pks = DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(clean).GetAwaiter().GetResult();

        if (cols.Count > 0)
        {
            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                string pkStr = pks.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase) ? " PRIMARY KEY" : "";
                string comma = (i < cols.Count - 1) ? "," : "";
                sb.AppendLine($"    [{col.ColumnName}] {col.DataType}{pkStr}{comma}");
            }
        }
        else
        {
            sb.AppendLine("    [Id] INT PRIMARY KEY IDENTITY(1,1),");
            sb.AppendLine("    [Name] NVARCHAR(100) NOT NULL,");
            sb.AppendLine("    [CreatedAt] DATETIME DEFAULT CURRENT_TIMESTAMP");
        }

        sb.AppendLine(");");

        Editor.AddTab();
        if (Editor.SelectedTab != null)
        {
            Editor.SelectedTab.Title = $"{clean}_DDL.sql";
            Editor.SelectedTab.Text = sb.ToString();
        }
    }

    private async Task LoadTablePropertiesAsync(string tableName)
    {
        string clean = tableName.Trim('[', ']', '"');
        SampleSchema.TableName = clean;
        var cols = await DatabaseSessionService.Instance.GetColumnsAsync(clean);
        var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(clean);
        var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(clean);

        if (cols.Count > 0)
        {
            SampleSchema.Columns.Clear();
            int ord = 1;
            foreach (var col in cols)
            {
                bool isPk = pks.Contains(col.ColumnName, StringComparer.OrdinalIgnoreCase);
                var fk = fks.FirstOrDefault(f => f.ColumnName.Equals(col.ColumnName, StringComparison.OrdinalIgnoreCase));

                SampleSchema.Columns.Add(new Quell.Models.TableColumnItem
                {
                    Name = col.ColumnName,
                    DataType = col.DataType,
                    IsNullable = !isPk,
                    IsPrimaryKey = isPk,
                    IsForeignKey = fk != null,
                    DefaultValue = string.Empty,
                    Ordinal = ord++
                });
            }
        }
        else if (SampleSchema.Columns.Count == 0)
        {
            SampleSchema.LoadDefaultSampleColumns(clean);
        }

        SampleQueryResult.LastQueryMessage = $"Table schema & properties loaded for '{clean}' ({SampleSchema.Columns.Count} columns)";
        SampleQueryResult.HasLastQueryMessage = true;
    }

    private List<DiagramTableItem> GetSampleDiagramTables()
    {
        var list = new List<DiagramTableItem>();

        var users = new DiagramTableItem { TableName = "Users", Schema = "dbo", DatabaseType = "SQL" };
        users.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "Name", DataType = "VARCHAR(100)" });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "Email", DataType = "VARCHAR(150)" });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "RoleId", DataType = "INT", IsForeignKey = true, ReferencedTable = "Roles", ReferencedColumn = "Id" });
        users.Columns.Add(new DiagramColumnItem { ColumnName = "CreatedAt", DataType = "DATETIME" });
        users.OutgoingRelations.Add("Users.RoleId -> Roles.Id");
        list.Add(users);

        var roles = new DiagramTableItem { TableName = "Roles", Schema = "dbo", DatabaseType = "SQL" };
        roles.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        roles.Columns.Add(new DiagramColumnItem { ColumnName = "Title", DataType = "VARCHAR(50)" });
        roles.Columns.Add(new DiagramColumnItem { ColumnName = "Description", DataType = "VARCHAR(255)" });
        list.Add(roles);

        var orders = new DiagramTableItem { TableName = "Orders", Schema = "dbo", DatabaseType = "SQL" };
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "UserId", DataType = "INT", IsForeignKey = true, ReferencedTable = "Users", ReferencedColumn = "Id" });
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "TotalAmount", DataType = "DECIMAL(10,2)" });
        orders.Columns.Add(new DiagramColumnItem { ColumnName = "OrderDate", DataType = "DATETIME" });
        orders.OutgoingRelations.Add("Orders.UserId -> Users.Id");
        list.Add(orders);

        var products = new DiagramTableItem { TableName = "Products", Schema = "dbo", DatabaseType = "SQL" };
        products.Columns.Add(new DiagramColumnItem { ColumnName = "Id", DataType = "INT", IsPrimaryKey = true });
        products.Columns.Add(new DiagramColumnItem { ColumnName = "Name", DataType = "VARCHAR(100)" });
        products.Columns.Add(new DiagramColumnItem { ColumnName = "Price", DataType = "DECIMAL(10,2)" });
        products.Columns.Add(new DiagramColumnItem { ColumnName = "StockQuantity", DataType = "INT" });
        list.Add(products);

        return list;
    }

    #endregion

    [RelayCommand]
    public void OpenUpdateModal()
    {
        UpdateModal.Open();
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

