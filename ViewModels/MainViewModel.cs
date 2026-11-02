using System;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
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

        // Listen for live database connection changes to update the UI badge and reload sidebar tables
        DatabaseSessionService.Instance.ConnectionChanged += () =>
        {
            if (Avalonia.Threading.Dispatcher.UIThread.CheckAccess())
            {
                Navbar.UpdateConnectionStatus();
                _ = Sidebar.RefreshAsync();
            }
            else
            {
                Avalonia.Threading.Dispatcher.UIThread.Post(() =>
                {
                    Navbar.UpdateConnectionStatus();
                    _ = Sidebar.RefreshAsync();
                });
            }
        };

        // Initialize SQLite local storage and load tables
        _ = InitializeStartupAsync();
    }

    private async Task InitializeStartupAsync()
    {
        await Fdb.Services.DatabaseStorageService.Instance.InitializeAsync();
        await Sidebar.RefreshAsync();
    }

    [RelayCommand]
    public async Task ExecuteQuery()
    {
        string query = Editor.SelectedTab?.Text ?? "SELECT * FROM dbo.Users;";
        string execTime = "14 ms";
        int rowCount = SampleQueryResult.Rows.Count;
        string status = "Success";

        if (DatabaseSessionService.Instance.IsConnected)
        {
            var result = await DatabaseSessionService.Instance.ExecuteQueryAsync(query);
            execTime = result.ExecutionTime;
            status = result.Success ? "Success" : "Failed";

            if (result.Success && result.ColumnNames.Count > 0)
            {
                SampleQueryResult.SetResults(result.ColumnNames, result.Rows, result.ExecutionTime);
                rowCount = result.Rows.Count;
            }
        }
        else
        {
            // Execute against local SQLite database so queries immediately return real results
            var localResult = await Fdb.Services.DatabaseStorageService.Instance.ExecuteArbitraryQueryAsync(query);
            execTime = localResult.ExecutionTime;
            status = localResult.Success ? "Success" : "Failed";

            if (localResult.Success && localResult.ColumnNames.Count > 0)
            {
                SampleQueryResult.SetResults(localResult.ColumnNames, localResult.Rows, localResult.ExecutionTime);
                rowCount = localResult.Rows.Count;
            }
        }

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
        string target = DatabaseSessionService.Instance.TargetDisplayName;
        ConfirmModal.Show(
            title: "Disconnect Database",
            message: $"Are you sure you want to disconnect from {target}? Active queries in tabs will remain preserved.",
            targetName: target,
            isDestructive: true,
            confirmText: "Disconnect",
            onConfirmed: async () =>
            {
                await DatabaseSessionService.Instance.DisconnectAsync();
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
