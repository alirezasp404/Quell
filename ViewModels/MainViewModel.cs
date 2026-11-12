using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Data;
using System.Globalization;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;
using Quell.Services.DatabaseProviders;
using Quell.Services.DemoData;

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
    private ImportDataModalViewModel _importModal;

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
        _importModal = new ImportDataModalViewModel();
        _diagramModal = new DatabaseDiagramModalViewModel();
        _settingsModal = new SettingsModalViewModel();
        _updateModal = new UpdateModalViewModel();
        _sampleQueryResult = new QueryResultTableViewModel();
        _sampleSchema = new SchemaPropertiesTableViewModel();

        _exportModal.PerformExportCallback = async (source, tableName, includeHeaders, filePath) =>
        {
            return await ExecuteDataExportAsync(source, tableName, includeHeaders, filePath);
        };
        _importModal.PerformImportCallback = async (tableName, mappedCols, rawRows, importMode, keyColumn, skipErrors, batchSize, progress) =>
        {
            return await ExecuteDataImportAsync(tableName, mappedCols, rawRows, importMode, keyColumn, skipErrors, batchSize, progress);
        };

        _importModal.LoadTableColumnsCallback = async tableName =>
        {
            string? key = ResolveActiveSessionKey();
            if (!string.IsNullOrEmpty(key) && DatabaseSessionService.Instance.IsSessionOpen(key))
            {
                var cols = await DatabaseSessionService.Instance.GetColumnsAsync(tableName, key);
                var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(tableName, key);

                return cols.Select(c =>
                {
                    bool isPk = pks.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase);
                    bool isReq = isPk;
                    return (c.ColumnName, c.DataType, isPk, isReq);
                }).ToList();
            }

            return DemoDataProvider.GetSampleImportColumns();
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

        _sampleQueryResult.ForeignKeyInspectorModal.FetchRecordCallback = async (tgtTable, tgtCol, val) =>
        {
            string? key = ResolveActiveSessionKey();
            if (!string.IsNullOrEmpty(key) && DatabaseSessionService.Instance.IsSessionOpen(key))
            {
                DatabaseType dbType = SelectedTargetDatabase?.DatabaseType
                    ?? DatabaseSessionService.Instance.ActiveSession?.DatabaseType
                    ?? DatabaseType.MSSQL;

                string quotedTable = QueryResultTableViewModel.FormatIdentifier(tgtTable, dbType);
                string quotedCol = QueryResultTableViewModel.FormatIdentifier(tgtCol, dbType);
                string formattedVal = EditCellModalViewModel.FormatLiteralOrNumber(val, dbType);
                string whereClause = $"WHERE {quotedCol} = {formattedVal}";

                string q = dbType == DatabaseType.MSSQL
                    ? $"SELECT TOP 1 * FROM {quotedTable} {whereClause};"
                    : $"SELECT * FROM {quotedTable} {whereClause} LIMIT 1;";

                var res = await DatabaseSessionService.Instance.ExecuteQueryAsync(q, key);
                if (res.Success && res.Rows.Count > 0)
                {
                    return res.Rows[0];
                }

                // Connected to real DB: never fall back to demo/sample rows
                return null;
            }

            return DemoDataProvider.GetSampleRow(tgtTable, tgtCol, val);
        };
        _sampleQueryResult.ForeignKeyInspectorModal.FetchTableSchemaCallback = async tgtTable =>
        {
            string? key = ResolveActiveSessionKey();
            if (!string.IsNullOrEmpty(key) && DatabaseSessionService.Instance.IsSessionOpen(key))
            {
                var colsTask = DatabaseSessionService.Instance.GetColumnsAsync(tgtTable, key);
                var pksTask = DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(tgtTable, key);
                var fksTask = DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(tgtTable, key);

                await Task.WhenAll(colsTask, pksTask, fksTask);

                return new TableSchemaMetadata(
                    await colsTask,
                    await pksTask,
                    await fksTask);
            }

            return DemoDataProvider.GetSampleTableMetadata(tgtTable);
        };
        _sampleQueryResult.ForeignKeyInspectorModal.NavigateToTableCallback = NavigateToForeignKeyTableAsync;
        _sampleQueryResult.CheckConnectionCallback = HasActiveDatabaseConnection;

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
            SampleQueryResult.IsConnectedToDatabase = HasActiveDatabaseConnection();
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

    private string? ResolveActiveSessionKey() =>
        SelectedTargetDatabase?.Key ?? DatabaseSessionService.Instance.ActiveSession?.Key;

    private bool HasActiveDatabaseConnection()
    {
        if (DatabaseSessionService.Instance.IsConnected) return true;
        string? key = SelectedTargetDatabase?.Key;
        return !string.IsNullOrEmpty(key) && DatabaseSessionService.Instance.IsSessionOpen(key);
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

        SampleQueryResult.IsConnectedToDatabase = HasActiveDatabaseConnection();
    }

    partial void OnSelectedTargetDatabaseChanged(TargetDatabaseItem? value)
    {
        if (_isUpdatingTargetSilently) return;

        if (value == null)
        {
            SampleQueryResult.TargetDatabaseName = "(No Connection)";
            SampleQueryResult.IsConnectedToDatabase = HasActiveDatabaseConnection();
            return;
        }

        SampleQueryResult.TargetDatabaseName = value.FullDisplayName;
        if (DatabaseSessionService.Instance.IsSessionOpen(value.Key) &&
            DatabaseSessionService.Instance.ActiveSession?.Key != value.Key)
        {
            DatabaseSessionService.Instance.SetActiveSession(value.Key);
        }
        SampleQueryResult.IsConnectedToDatabase = HasActiveDatabaseConnection();
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

        if (SelectedTargetDatabase == null && !HasActiveDatabaseConnection())
        {
            SampleQueryResult.IsConnectedToDatabase = false;
            string? checkTable = ExtractTableNameFromQuery(query);
            if (DemoDataProvider.IsSampleTableName(checkTable))
            {
                SampleQueryResult.LoadSampleReferencedTable(checkTable!, null, null);
                SampleQueryResult.LastExecutedQuery = query;
                SampleQueryResult.LastQueryMessage = $"Loaded sample data for '{checkTable}' ({SampleQueryResult.TotalRows} rows)";
                SampleQueryResult.HasLastQueryMessage = true;
                SampleQueryResult.TargetDatabaseName = "Sample DB";
                ShowQueryResultFooter();
                return;
            }

            SampleQueryResult.CurrentTableName = string.Empty;
            SampleQueryResult.ClearMetadata();
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

        string targetDbName = SelectedTargetDatabase?.FullDisplayName ?? DatabaseSessionService.Instance.TargetDisplayName;
        string key = ResolveActiveSessionKey() ?? string.Empty;

        if (!string.IsNullOrEmpty(key) && !DatabaseSessionService.Instance.IsSessionOpen(key) && SelectedTargetDatabase != null)
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
                SampleQueryResult.IsConnectedToDatabase = HasActiveDatabaseConnection();
                SampleQueryResult.CurrentTableName = string.Empty;
                SampleQueryResult.ClearMetadata();
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

        SampleQueryResult.IsConnectedToDatabase = true;
        SampleQueryResult.CurrentDatabaseType = SelectedTargetDatabase?.DatabaseType
            ?? DatabaseSessionService.Instance.ActiveSession?.Config.DatabaseType
            ?? DatabaseType.MSSQL;

        var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(query, key);
        execTime = result.ExecutionTime;
        status = result.Success ? "Success" : "Failed";

        string? inferredTable = result.Success && result.ColumnNames.Count > 0
            ? ExtractTableNameFromQuery(query)
            : null;
        SampleQueryResult.CurrentTableName = inferredTable ?? string.Empty;
        SampleQueryResult.ClearMetadata();

        if (result.Success && result.ColumnNames.Count > 0)
        {
            SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
            SampleQueryResult.LastExecutedQuery = query;
            rowCount = result.Rows.Count;

            if (!string.IsNullOrWhiteSpace(inferredTable))
            {
                try
                {
                    var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(inferredTable, key);
                    var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(inferredTable, key);
                    if (string.Equals(SampleQueryResult.CurrentTableName, inferredTable, StringComparison.OrdinalIgnoreCase))
                    {
                        SampleQueryResult.SetTableMetadata(pks, fks);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MainViewModel] Metadata lookup failed for '{inferredTable}': {ex.Message}");
                }
            }
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

        SampleQueryResult.TargetDatabaseName = targetDbName;

        await Quell.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
            query, execTime, rowCount, status);

        _ = Sidebar.RefreshAsync();
        ShowQueryResultFooter();
    }

    public async Task NavigateToForeignKeyTableAsync(string targetTable, string targetColumn, string? cellValue, string? sourceColumn)
    {
        if (string.IsNullOrWhiteSpace(cellValue) || cellValue.Equals("NULL", StringComparison.OrdinalIgnoreCase))
        {
            return;
        }

        string sourceDesc = $"Navigated via [{sourceColumn ?? "FK"} -> {targetTable}.{targetColumn} = {cellValue}]";
        SampleQueryResult.PushNavigationState(sourceDesc);

        string? activeKey = ResolveActiveSessionKey();
        bool isConnected = !string.IsNullOrEmpty(activeKey) && DatabaseSessionService.Instance.IsSessionOpen(activeKey);
        DatabaseType dbType = SelectedTargetDatabase?.DatabaseType
            ?? DatabaseSessionService.Instance.ActiveSession?.DatabaseType
            ?? DatabaseType.MSSQL;

        string quotedTable = QueryResultTableViewModel.FormatIdentifier(targetTable, dbType);
        string quotedColumn = QueryResultTableViewModel.FormatIdentifier(targetColumn, dbType);
        string formattedValue = EditCellModalViewModel.FormatLiteralOrNumber(cellValue, dbType);
        string whereClause = $"WHERE {quotedColumn} = {formattedValue}";

        string query = dbType == DatabaseType.MSSQL
            ? $"SELECT TOP 100 * FROM {quotedTable} {whereClause};"
            : $"SELECT * FROM {quotedTable} {whereClause} LIMIT 100;";

        if (Editor.SelectedTab != null)
        {
            Editor.SelectedTab.Text = query;
        }
        else
        {
            Editor.AddTab();
            if (Editor.SelectedTab != null) Editor.SelectedTab.Text = query;
        }

        if (isConnected)
        {
            SampleQueryResult.IsConnectedToDatabase = true;
            SampleQueryResult.CurrentDatabaseType = dbType;
            var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(query, activeKey);

            if (result.Success && result.ColumnNames.Count > 0)
            {
                SampleQueryResult.CurrentTableName = targetTable;
                SampleQueryResult.ClearMetadata();
                SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
                SampleQueryResult.LastExecutedQuery = query;
                SampleQueryResult.LastQueryMessage = result.Rows.Count == 0
                    ? $"Navigated to {targetTable} via FK [{sourceColumn} -> {targetTable}.{targetColumn}] — No matching records found (0 rows)"
                    : $"Navigated to {targetTable} via FK [{sourceColumn} -> {targetTable}.{targetColumn}] ({result.Rows.Count} rows)";
                SampleQueryResult.HasLastQueryMessage = true;

                try
                {
                    var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(targetTable, activeKey);
                    var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(targetTable, activeKey);
                    if (string.Equals(SampleQueryResult.CurrentTableName, targetTable, StringComparison.OrdinalIgnoreCase))
                    {
                        SampleQueryResult.SetTableMetadata(pks, fks);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MainViewModel] Metadata lookup failed for FK target '{targetTable}': {ex.Message}");
                }

                ShowQueryResultFooter();
                return;
            }

            SampleQueryResult.CurrentTableName = string.Empty;
            SampleQueryResult.ClearMetadata();
            SampleQueryResult.SetResults(
                new List<string> { "Error", "TargetTable" },
                new List<QueryResultRow>
                {
                    new QueryResultRow
                    {
                        RowIndex = 1,
                        ["Error"] = result.Message,
                        ["TargetTable"] = targetTable
                    }
                },
                result.ExecutionTime);
            SampleQueryResult.LastQueryMessage = $"FK navigation failed: {result.Message}";
            SampleQueryResult.HasLastQueryMessage = true;
            ShowQueryResultFooter();
            return;
        }

        // Disconnected / demo mode only
        SampleQueryResult.IsConnectedToDatabase = false;
        SampleQueryResult.LoadSampleReferencedTable(targetTable, targetColumn, cellValue);
        SampleQueryResult.LastExecutedQuery = query;
        string matchedInfo = SampleQueryResult.SelectedRow != null ? $" (User: {SampleQueryResult.SelectedRow.Name})" : "";
        SampleQueryResult.LastQueryMessage = $"Navigated to {targetTable} via FK [{sourceColumn} -> {targetTable}]{matchedInfo} ({SampleQueryResult.TotalRows} rows)";
        SampleQueryResult.HasLastQueryMessage = true;
        ShowQueryResultFooter();
    }

    public async Task RefreshQueryResultAsync()
    {
        string? activeKey = ResolveActiveSessionKey();
        if (string.IsNullOrEmpty(activeKey) || SelectedTargetDatabase == null || !DatabaseSessionService.Instance.IsSessionOpen(activeKey))
        {
            SampleQueryResult.IsConnectedToDatabase = false;
            SampleQueryResult.ResetToSampleData();
            return;
        }

        SampleQueryResult.IsConnectedToDatabase = true;
        string? queryToRun = null;
        if (!string.IsNullOrWhiteSpace(SampleQueryResult.LastExecutedQuery) &&
            SampleQueryResult.LastExecutedQuery.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            queryToRun = SampleQueryResult.LastExecutedQuery;
        }
        else if (!string.IsNullOrWhiteSpace(SampleQueryResult.CurrentTableName))
        {
            string quotedTable = QueryResultTableViewModel.FormatIdentifier(SampleQueryResult.CurrentTableName, SampleQueryResult.CurrentDatabaseType);
            queryToRun = SampleQueryResult.CurrentDatabaseType == DatabaseType.MSSQL
                ? $"SELECT TOP 100 * FROM {quotedTable};"
                : $"SELECT * FROM {quotedTable} LIMIT 100;";
        }
        else if (!string.IsNullOrWhiteSpace(Editor.SelectedTab?.Text) &&
                 Editor.SelectedTab.Text.TrimStart().StartsWith("SELECT", StringComparison.OrdinalIgnoreCase))
        {
            queryToRun = Editor.SelectedTab.Text;
        }
        else
        {
            string? firstTable = DatabaseSessionService.Instance.GetActiveCachedTables().FirstOrDefault()?.FullName;
            if (!string.IsNullOrWhiteSpace(firstTable))
            {
                string quotedFirst = QueryResultTableViewModel.FormatIdentifier(firstTable, SampleQueryResult.CurrentDatabaseType);
                queryToRun = SampleQueryResult.CurrentDatabaseType == DatabaseType.MSSQL
                    ? $"SELECT TOP 100 * FROM {quotedFirst};"
                    : $"SELECT * FROM {quotedFirst} LIMIT 100;";
            }
        }

        if (string.IsNullOrWhiteSpace(queryToRun))
        {
            SampleQueryResult.LastQueryMessage = "No active query or table selected to refresh.";
            SampleQueryResult.HasLastQueryMessage = true;
            return;
        }

        var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(queryToRun, activeKey);
        if (result.Success && result.ColumnNames.Count > 0)
        {
            string? inferredTable = ExtractTableNameFromQuery(queryToRun)
                ?? (string.IsNullOrWhiteSpace(SampleQueryResult.CurrentTableName) ? null : SampleQueryResult.CurrentTableName);
            SampleQueryResult.CurrentTableName = inferredTable ?? string.Empty;
            SampleQueryResult.ClearMetadata();
            SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
            SampleQueryResult.LastExecutedQuery = queryToRun;
            SampleQueryResult.LastQueryMessage = $"Table refreshed ({result.Rows.Count} rows) · {result.ExecutionTime}";
            SampleQueryResult.HasLastQueryMessage = true;

            if (!string.IsNullOrWhiteSpace(inferredTable))
            {
                try
                {
                    var pks = await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(inferredTable, activeKey);
                    var fks = await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(inferredTable, activeKey);
                    if (string.Equals(SampleQueryResult.CurrentTableName, inferredTable, StringComparison.OrdinalIgnoreCase))
                    {
                        SampleQueryResult.SetTableMetadata(pks, fks);
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"[MainViewModel] Metadata refresh failed for '{inferredTable}': {ex.Message}");
                }
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
                SampleQueryResult.IsConnectedToDatabase = HasActiveDatabaseConnection();
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

        if (tables.Count == 0 && !HasActiveDatabaseConnection())
        {
            tables.AddRange(DemoDataProvider.GetSampleTableNames());
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

    private void PopulateImportModalTables(string? defaultTable = null)
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

        if (tables.Count == 0 && !HasActiveDatabaseConnection())
        {
            tables.AddRange(DemoDataProvider.GetSampleTableNames());
        }

        if (!string.IsNullOrWhiteSpace(defaultTable) && !tables.Contains(defaultTable))
        {
            tables.Insert(0, defaultTable);
        }

        string? selected = defaultTable ?? Sidebar.SelectedTable?.FullName ?? SampleQueryResult.CurrentTableName;
        ImportModal.TargetDatabaseName = activeDb?.DatabaseName ?? SelectedTargetDatabase?.DisplayName ?? "Active Database";
        ImportModal.SetAvailableTables(tables, selected);
    }

    [RelayCommand]
    public void OpenImportModal()
    {
        PopulateImportModalTables();
        ImportModal.Open();
    }

    [RelayCommand]
    public void OpenImportModalForTable(string? tableName)
    {
        PopulateImportModalTables(tableName);
        ImportModal.Open();
    }

    [RelayCommand]
    public async Task DownloadTableTemplate()
    {
        await DownloadTemplateForTable(ImportModal.SelectedTable ?? Sidebar.SelectedTable?.FullName ?? SampleQueryResult.CurrentTableName);
    }

    [RelayCommand]
    public async Task DownloadTemplateForTable(string? tableName)
    {
        bool isConnected = HasActiveDatabaseConnection();
        string? sessionKey = ResolveActiveSessionKey();

        if (string.IsNullOrWhiteSpace(tableName))
        {
            var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            tableName = activeDb?.AllTableNodes.FirstOrDefault(t => t.IsActive)?.Table.FullName 
                ?? Sidebar.SelectedTable?.FullName 
                ?? SampleQueryResult.CurrentTableName;
        }

        if (string.IsNullOrWhiteSpace(tableName))
        {
            if (isConnected)
            {
                SampleQueryResult.LastQueryMessage = "No table selected to generate an Excel template.";
                SampleQueryResult.HasLastQueryMessage = true;
                return;
            }
            tableName = "Users";
        }

        try
        {
            var cols = isConnected
                ? await DatabaseSessionService.Instance.GetColumnsAsync(tableName, sessionKey)
                : new List<(string ColumnName, string DataType)>();
            var pks = isConnected
                ? await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(tableName, sessionKey)
                : new List<string>();

            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop)) desktop = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string cleanName = tableName.Replace(".", "_").Replace("[", "").Replace("]", "").Replace("\"", "");
            string templatePath = System.IO.Path.Combine(desktop, $"{cleanName}_import_template.xlsx");

            var colTuples = cols.Select(c => (c.ColumnName, c.DataType, pks.Contains(c.ColumnName, StringComparer.OrdinalIgnoreCase))).ToList();
            if (colTuples.Count == 0)
            {
                if (isConnected)
                {
                    SampleQueryResult.LastQueryMessage = $"No columns found in database metadata for '{tableName}'.";
                    SampleQueryResult.HasLastQueryMessage = true;
                    return;
                }

                colTuples.AddRange(DemoDataProvider.GetSampleImportColumns().Select(c => (c.ColumnName, c.DataType, c.IsPrimaryKey)));
            }

            ExcelDataService.CreateExcelTemplate(templatePath, cleanName, colTuples);

            SampleQueryResult.LastQueryMessage = $"Excel Template saved to Desktop: '{System.IO.Path.GetFileName(templatePath)}'";
            SampleQueryResult.HasLastQueryMessage = true;
        }
        catch (Exception ex)
        {
            SampleQueryResult.LastQueryMessage = $"Failed to create template: {ex.Message}";
            SampleQueryResult.HasLastQueryMessage = true;
        }
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
        bool isConnected = HasActiveDatabaseConnection();
        string? sessionKey = ResolveActiveSessionKey();
        var activeSession = DatabaseSessionService.Instance.ActiveSession;
        string dbKey = activeSession?.Key ?? SelectedTargetDatabase?.Key ?? activeSession?.DisplayName ?? SelectedTargetDatabase?.DisplayName ?? "Default";
        string dbName = activeSession?.DisplayName ?? SelectedTargetDatabase?.DisplayName ?? "Quell Studio DB";
        DatabaseType dbType = activeSession?.DatabaseType ?? SelectedTargetDatabase?.DatabaseType ?? DatabaseType.SQLite;

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
            if (!isConnected)
            {
                var sampleTables = DemoDataProvider.GetSampleDiagramTables();
                DiagramModal.LoadTables(sampleTables, dbName, dbType, dbKey);
                DiagramCacheService.Instance.Store(dbKey, dbName, dbType, sampleTables);
                return;
            }

            var cachedTables = DatabaseSessionService.Instance.GetActiveCachedTables();
            var activeDb = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);
            if (cachedTables.Count == 0 && activeDb != null)
            {
                cachedTables = activeDb.AllTableNodes.Select(t => t.Table).ToList();
            }

            if (cachedTables.Count == 0)
            {
                var emptyTables = new List<DiagramTableItem>();
                DiagramModal.LoadTables(emptyTables, dbName, dbType, dbKey);
                DiagramModal.StatusMessage = "No user tables found in database.";
                DiagramModal.HasStatusMessage = true;
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
                    var pksTask = DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(t.FullName, sessionKey);
                    var fksTask = DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(t.FullName, sessionKey);
                    var colsTask = DatabaseSessionService.Instance.GetColumnsAsync(t.FullName, sessionKey);

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
            DiagramModal.LoadTables(sortedTables, dbName, dbType, dbKey);
            DiagramCacheService.Instance.Store(dbKey, dbName, dbType, sortedTables);
            DiagramModal.StatusMessage = sortedTables.Count == 0
                ? "No user tables found in database."
                : $"Diagram generated & cached ({sortedTables.Count} tables)";
            DiagramModal.HasStatusMessage = true;
        }
        catch (Exception ex)
        {
            DiagramModal.StatusMessage = $"Schema analysis warning: {ex.Message}";
            DiagramModal.HasStatusMessage = true;
            if (!isConnected && DiagramModal.AllTables.Count == 0)
            {
                var samples = DemoDataProvider.GetSampleDiagramTables();
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
        string? target = Sidebar.SelectedTable?.FullName ?? SampleQueryResult.CurrentTableName;
        if (string.IsNullOrWhiteSpace(target))
        {
            if (HasActiveDatabaseConnection())
            {
                SampleQueryResult.LastQueryMessage = "No table selected to generate DDL.";
                SampleQueryResult.HasLastQueryMessage = true;
                return;
            }
            target = "Users";
        }
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
            target = cached.FirstOrDefault()?.FullName ?? SampleQueryResult.CurrentTableName;
        }

        if (string.IsNullOrWhiteSpace(target))
        {
            if (HasActiveDatabaseConnection())
            {
                SampleSchema.TableName = string.Empty;
                SampleSchema.Columns.Clear();
                SampleQueryResult.LastQueryMessage = "No table selected to inspect properties.";
                SampleQueryResult.HasLastQueryMessage = true;
                ShowSchemaPropertiesFooter();
                return;
            }
            target = "Users";
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
        DatabaseType dbType = SelectedTargetDatabase?.DatabaseType ?? activeSession?.DatabaseType ?? activeNode?.DatabaseType ?? DatabaseType.SQLite;
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
        if (dbType == DatabaseType.PostgreSQL || dbType == DatabaseType.SQLite)
        {
            sql = "VACUUM;";
        }
        else if (dbType == DatabaseType.MySQL)
        {
            var cached = DatabaseSessionService.Instance.GetActiveCachedTables();
            string? tableTarget = Sidebar.SelectedTable?.TableName ?? cached.FirstOrDefault()?.TableName ?? SampleQueryResult.CurrentTableName;
            if (string.IsNullOrWhiteSpace(tableTarget))
            {
                SampleQueryResult.LastQueryMessage = "No table selected or cached to optimize.";
                SampleQueryResult.HasLastQueryMessage = true;
                return;
            }
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

    private async Task<(bool Success, string Message, int RowsImported)> ExecuteDataImportAsync(
        string tableName,
        List<ImportColumnMappingItem> mappedCols,
        List<List<string>> rawRows,
        string importMode,
        string keyColumn,
        bool skipErrors,
        int batchSize,
        Action<int, int> progressCallback)
    {
        if (string.IsNullOrWhiteSpace(tableName) || mappedCols.Count == 0 || rawRows.Count == 0)
        {
            return (false, "No valid data or mappings provided for import.", 0);
        }

        string sessionKey = SelectedTargetDatabase?.Key 
            ?? DatabaseSessionService.Instance.ActiveSession?.Key 
            ?? string.Empty;

        var activeNode = Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsActive) 
            ?? Sidebar.DatabaseNodes.FirstOrDefault(n => n.IsConnected);

        if (string.IsNullOrEmpty(sessionKey) && activeNode != null)
        {
            sessionKey = activeNode.ConnectionKey;
        }

        var activeSession = DatabaseSessionService.Instance.ActiveSession 
            ?? DatabaseSessionService.Instance.OpenSessions.FirstOrDefault(s => s.Connection.State == ConnectionState.Open);

        if (string.IsNullOrEmpty(sessionKey) && activeSession != null)
        {
            sessionKey = activeSession.Key;
        }

        if (string.IsNullOrEmpty(sessionKey) && activeSession == null)
        {
            return (false, "No active database connection found. Please connect to a database first.", 0);
        }

        DatabaseType dbType = activeSession?.DatabaseType 
            ?? SelectedTargetDatabase?.DatabaseType 
            ?? activeNode?.DatabaseType 
            ?? DatabaseType.SQLite;

        string QuoteCol(string col)
        {
            string clean = col.Trim('[', ']', '"', '`');
            return dbType switch
            {
                DatabaseType.PostgreSQL => $"\"{clean.Replace("\"", "\"\"")}\"",
                DatabaseType.MySQL => $"`{clean.Replace("`", "``")}`",
                _ => $"[{clean.Replace("]", "]]")}]"
            };
        }

        string QuoteTable(string tbl)
        {
            string clean = tbl.Trim();
            var parts = clean.Split('.');
            var quotedParts = parts.Select(p => QuoteCol(p));
            return string.Join(".", quotedParts);
        }

        string quotedTarget = QuoteTable(tableName);

        bool isTruncate = importMode.StartsWith("Truncate", StringComparison.OrdinalIgnoreCase);
        bool isOverwrite = importMode.StartsWith("Overwrite", StringComparison.OrdinalIgnoreCase);

        try
        {
            if (isTruncate)
            {
                string deleteSql = dbType == DatabaseType.SQLite
                    ? $"DELETE FROM {quotedTarget};"
                    : $"TRUNCATE TABLE {quotedTarget};";
                
                var truncRes = await DatabaseSessionService.Instance.ExecuteQueryAsync(deleteSql, sessionKey);
                if (!truncRes.Success && truncRes.Message.Contains("TRUNCATE", StringComparison.OrdinalIgnoreCase))
                {
                    await DatabaseSessionService.Instance.ExecuteQueryAsync($"DELETE FROM {quotedTarget};", sessionKey);
                }
            }

            var targetColsList = mappedCols.Select(m => QuoteCol(m.TargetColumnName)).ToList();
            string colsSql = string.Join(", ", targetColsList);

            var keyMapping = mappedCols.FirstOrDefault(m => m.TargetColumnName.Equals(keyColumn, StringComparison.OrdinalIgnoreCase))
                ?? mappedCols.FirstOrDefault(m => m.IsPrimaryKey)
                ?? mappedCols.First();
            string quotedKey = QuoteCol(keyMapping.TargetColumnName);
            int keyColIndex = mappedCols.IndexOf(keyMapping);

            int totalRows = rawRows.Count;
            int importedRows = 0;
            int errorRows = 0;

            int effectiveBatchSize = isOverwrite ? Math.Max(5, Math.Min(50, batchSize)) : Math.Max(10, Math.Min(500, batchSize));

            for (int i = 0; i < totalRows; i += effectiveBatchSize)
            {
                var chunk = rawRows.Skip(i).Take(effectiveBatchSize).ToList();

                if (isOverwrite)
                {
                    var batchStatements = new List<string>();
                    foreach (var row in chunk)
                    {
                        var rowVals = ExtractRowValues(row, mappedCols, dbType);
                        string keyVal = rowVals[keyColIndex];

                        if (keyVal == "NULL")
                        {
                            batchStatements.Add($"INSERT INTO {quotedTarget} ({colsSql}) VALUES ({string.Join(", ", rowVals)});");
                            continue;
                        }

                        var updateAssignments = new List<string>();
                        for (int c = 0; c < mappedCols.Count; c++)
                        {
                            if (c == keyColIndex) continue;
                            updateAssignments.Add($"{QuoteCol(mappedCols[c].TargetColumnName)} = {rowVals[c]}");
                        }

                        if (updateAssignments.Count > 0)
                        {
                            string updateStmt = $"UPDATE {quotedTarget} SET {string.Join(", ", updateAssignments)} WHERE {quotedKey} = {keyVal};";
                            string insertStmt = $"INSERT INTO {quotedTarget} ({colsSql}) SELECT {string.Join(", ", rowVals)} WHERE NOT EXISTS (SELECT 1 FROM {quotedTarget} WHERE {quotedKey} = {keyVal});";
                            batchStatements.Add($"{updateStmt}\n{insertStmt}");
                        }
                        else
                        {
                            string insertStmt = $"INSERT INTO {quotedTarget} ({colsSql}) SELECT {string.Join(", ", rowVals)} WHERE NOT EXISTS (SELECT 1 FROM {quotedTarget} WHERE {quotedKey} = {keyVal});";
                            batchStatements.Add(insertStmt);
                        }
                    }

                    if (batchStatements.Count > 0)
                    {
                        string batchSql = string.Join("\n", batchStatements);
                        var res = await DatabaseSessionService.Instance.ExecuteQueryAsync(batchSql, sessionKey);
                        if (res.Success)
                        {
                            importedRows += chunk.Count;
                        }
                        else
                        {
                            if (skipErrors)
                            {
                                foreach (var singleStmt in batchStatements)
                                {
                                    var sRes = await DatabaseSessionService.Instance.ExecuteQueryAsync(singleStmt, sessionKey);
                                    if (sRes.Success) importedRows++;
                                    else errorRows++;
                                }
                            }
                            else
                            {
                                return (false, $"Overwrite failed at row {i + 1}: {res.Message}", importedRows);
                            }
                        }
                    }
                }
                else
                {
                    var batchValues = new List<string>();
                    foreach (var row in chunk)
                    {
                        var rowVals = ExtractRowValues(row, mappedCols, dbType);
                        batchValues.Add($"({string.Join(", ", rowVals)})");
                    }

                    if (batchValues.Count > 0)
                    {
                        string insertSql = $"INSERT INTO {quotedTarget} ({colsSql}) VALUES\n{string.Join(",\n", batchValues)};";
                        var res = await DatabaseSessionService.Instance.ExecuteQueryAsync(insertSql, sessionKey);

                        if (res.Success)
                        {
                            importedRows += chunk.Count;
                        }
                        else
                        {
                            if (skipErrors)
                            {
                                foreach (var singleRowVals in batchValues)
                                {
                                    string singleInsert = $"INSERT INTO {quotedTarget} ({colsSql}) VALUES {singleRowVals};";
                                    var singleRes = await DatabaseSessionService.Instance.ExecuteQueryAsync(singleInsert, sessionKey);
                                    if (singleRes.Success) importedRows++;
                                    else errorRows++;
                                }
                            }
                            else
                            {
                                return (false, $"Import failed at row {i + 1}: {res.Message}", importedRows);
                            }
                        }
                    }
                }

                progressCallback?.Invoke(importedRows, totalRows);
            }

            await Quell.Services.DatabaseStorageService.Instance.SaveQueryHistoryAsync(
                $"-- Bulk Imported {importedRows} rows from spreadsheet into {tableName}", "0ms", importedRows, "Success");

            // Automatically query and refresh the active result table view so user immediately sees the imported data
            try
            {
                string selectQuery = dbType switch
                {
                    DatabaseType.MSSQL => $"SELECT TOP 200 * FROM {quotedTarget};",
                    _ => $"SELECT * FROM {quotedTarget} LIMIT 200;"
                };
                var selectRes = await DatabaseSessionService.Instance.ExecuteQueryAsync(selectQuery, sessionKey);
                Console.WriteLine($"[ExecuteDataImportAsync] Post-import select: Success={selectRes.Success}, Cols={selectRes.ColumnNames.Count}, Rows={selectRes.Rows.Count}, Msg='{selectRes.Message}'");
                if (selectRes.Success && selectRes.ColumnNames.Count > 0)
                {
                    void UpdateResults()
                    {
                        SampleQueryResult.CurrentTableName = tableName;
                        SampleQueryResult.CurrentDatabaseType = dbType;
                        SampleQueryResult.SetResults(selectRes.ColumnNames, selectRes.Rows, selectRes.ExecutionTime);
                        SampleQueryResult.LastExecutedQuery = selectQuery;
                        SampleQueryResult.LastQueryMessage = $"Imported {importedRows:N0} rows into {tableName} · Table View Updated";
                        SampleQueryResult.HasLastQueryMessage = true;
                    }

                    try
                    {
                        UpdateResults();
                    }
                    catch
                    {
                        Avalonia.Threading.Dispatcher.UIThread.Post(UpdateResults);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ExecuteDataImportAsync] Post-import refresh error: {ex.Message}");
            }

            string summary = errorRows > 0
                ? $"Successfully imported {importedRows:N0} rows into {tableName} ({errorRows} rows skipped due to errors)."
                : $"Successfully imported all {importedRows:N0} rows into {tableName}!";

            return (true, summary, importedRows);
        }
        catch (Exception ex)
        {
            return (false, $"Import error: {ex.Message}", 0);
        }
    }

    private static List<string> ExtractRowValues(List<string> row, List<ImportColumnMappingItem> mappedCols, DatabaseType dbType)
    {
        var rowVals = new List<string>();
        foreach (var mapping in mappedCols)
        {
            string rawVal = string.Empty;
            if (mapping.IsConstant)
            {
                rawVal = mapping.ConstantValue;
            }
            else
            {
                int excelIdx = ExtractExcelColumnIndex(mapping.SelectedExcelColumn);
                if (excelIdx >= 0 && excelIdx < row.Count)
                {
                    rawVal = row[excelIdx];
                }
            }

            if (string.IsNullOrWhiteSpace(rawVal) || rawVal.Trim().Equals("NULL", StringComparison.OrdinalIgnoreCase))
            {
                rowVals.Add("NULL");
            }
            else
            {
                rawVal = rawVal.Trim();
                string dt = (mapping.DataType ?? string.Empty).ToUpperInvariant();
                if ((dt.Contains("INT") || dt.Contains("DECIMAL") || dt.Contains("NUMERIC") || dt.Contains("FLOAT") || dt.Contains("DOUBLE") || dt.Contains("REAL"))
                    && decimal.TryParse(rawVal, NumberStyles.Any, CultureInfo.InvariantCulture, out var numVal))
                {
                    rowVals.Add(numVal.ToString(CultureInfo.InvariantCulture));
                }
                else if ((dt.Contains("BOOL") || dt.Contains("BIT")) && bool.TryParse(rawVal, out var bVal))
                {
                    if (dbType == DatabaseType.PostgreSQL)
                    {
                        rowVals.Add(bVal ? "TRUE" : "FALSE");
                    }
                    else
                    {
                        rowVals.Add(bVal ? "1" : "0");
                    }
                }
                else
                {
                    string escaped = rawVal.Replace("'", "''");
                    rowVals.Add($"'{escaped}'");
                }
            }
        }
        return rowVals;
    }

    private static int ExtractExcelColumnIndex(string option)
    {
        if (string.IsNullOrWhiteSpace(option)) return -1;
        string letter = option.Contains(':') ? option.Split(':')[0].Trim() : option.Trim();
        if (string.IsNullOrEmpty(letter) || !char.IsLetter(letter[0])) return -1;
        return ExcelDataService.ParseColumnIndexFromRef(letter);
    }

    private async Task ExecuteDatabaseBackupAsync()
    {
        try
        {
            var activeSession = DatabaseSessionService.Instance.ActiveSession;
            DatabaseType dbType = activeSession?.DatabaseType ?? SelectedTargetDatabase?.DatabaseType ?? DatabaseType.SQLite;
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
        bool isConnected = HasActiveDatabaseConnection();
        string? sessionKey = ResolveActiveSessionKey();
        string clean = tableName.Trim('[', ']', '"');
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"-- DDL Script generated by Quell for {clean}");
        sb.AppendLine($"CREATE TABLE {clean} (");

        var cols = isConnected
            ? DatabaseSessionService.Instance.GetColumnsAsync(clean, sessionKey).GetAwaiter().GetResult()
            : new List<(string ColumnName, string DataType)>();
        var pks = isConnected
            ? DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(clean, sessionKey).GetAwaiter().GetResult()
            : new List<string>();

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
        else if (!isConnected)
        {
            sb.AppendLine("    [Id] INT PRIMARY KEY IDENTITY(1,1),");
            sb.AppendLine("    [Name] NVARCHAR(100) NOT NULL,");
            sb.AppendLine("    [CreatedAt] DATETIME DEFAULT CURRENT_TIMESTAMP");
        }
        else
        {
            sb.AppendLine("    -- No columns found for this table in database metadata");
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
        bool isConnected = HasActiveDatabaseConnection();
        string? sessionKey = ResolveActiveSessionKey();
        string clean = tableName.Trim('[', ']', '"');
        SampleSchema.TableName = clean;

        var cols = isConnected
            ? await DatabaseSessionService.Instance.GetColumnsAsync(clean, sessionKey)
            : new List<(string ColumnName, string DataType)>();
        var pks = isConnected
            ? await DatabaseSessionService.Instance.GetPrimaryKeyColumnsAsync(clean, sessionKey)
            : new List<string>();
        var fks = isConnected
            ? await DatabaseSessionService.Instance.GetForeignKeyRelationsAsync(clean, sessionKey)
            : new List<ForeignKeyRelation>();

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
        else if (!isConnected)
        {
            SampleSchema.LoadDemoColumns(clean);
        }
        else
        {
            SampleSchema.Columns.Clear();
        }

        SampleQueryResult.LastQueryMessage = SampleSchema.Columns.Count > 0
            ? $"Table schema & properties loaded for '{clean}' ({SampleSchema.Columns.Count} columns)"
            : $"No schema columns found in database metadata for '{clean}'";
        SampleQueryResult.HasLastQueryMessage = true;
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

