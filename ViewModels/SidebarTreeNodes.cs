using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Fdb.Services;
using Fdb.Services.DatabaseProviders;

namespace Fdb.ViewModels;

public abstract partial class SidebarTreeNodeBase : ViewModelBase
{
    [ObservableProperty]
    private string _id = string.Empty;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _subtitle = string.Empty;

    [ObservableProperty]
    private bool _isExpanded;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isLoading;

    public ObservableCollection<SidebarTreeNodeBase> Children { get; } = new();
}

public partial class DatabaseConnectionTreeNode : SidebarTreeNodeBase
{
    public ConnectionRecord Record { get; private set; }

    public string DatabaseType => Record.DatabaseType ?? "MSSQL";
    public string BadgeColor => Record.BadgeColor;
    public string Server => Record.Server;
    public int Port => Record.Port;
    public string DatabaseName => Record.DatabaseName;
    public string ConnectionKey => Record.Id > 0 ? $"id_{Record.Id}" : $"{Record.DatabaseType}_{Record.Server}_{Record.Port}_{Record.DatabaseName}";

    public Func<DatabaseConnectionTreeNode, Task>? ConnectAction { get; set; }
    public Func<DatabaseConnectionTreeNode, Task>? DisconnectAction { get; set; }
    public Action<DatabaseConnectionTreeNode>? EditAction { get; set; }
    public Func<DatabaseConnectionTreeNode, Task>? RefreshAction { get; set; }
    public Func<DatabaseConnectionTreeNode, Task>? DeleteAction { get; set; }

    [RelayCommand]
    public async Task ConnectAsync()
    {
        if (ConnectAction != null)
        {
            await ConnectAction(this);
        }
    }

    [RelayCommand]
    public async Task DisconnectAsync()
    {
        if (DisconnectAction != null)
        {
            await DisconnectAction(this);
        }
    }

    [RelayCommand]
    public void Edit()
    {
        EditAction?.Invoke(this);
    }

    [RelayCommand]
    public async Task RefreshAsync()
    {
        if (RefreshAction != null)
        {
            await RefreshAction(this);
        }
    }

    [RelayCommand]
    public async Task DeleteAsync()
    {
        if (DeleteAction != null)
        {
            await DeleteAction(this);
        }
    }

    [RelayCommand]
    public async Task ToggleExpandAsync()
    {
        if (!IsConnected)
        {
            if (ConnectAction != null)
            {
                await ConnectAction(this);
            }
            IsExpanded = true;
        }
        else
        {
            IsExpanded = !IsExpanded;
        }
    }

    public void UpdateRecord(ConnectionRecord record)
    {
        Record = record;
        Title = record.DisplayTitle;
        Subtitle = IsConnected ? $"{TableCount} table(s) found" : (record.DatabaseType ?? "MSSQL");
        OnPropertyChanged(nameof(Record));
        OnPropertyChanged(nameof(DatabaseName));
        OnPropertyChanged(nameof(Server));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(DatabaseType));
        OnPropertyChanged(nameof(BadgeColor));
    }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private bool _isConnected;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(StatusBadgeText))]
    [NotifyPropertyChangedFor(nameof(StatusColor))]
    private bool _isActive;

    [ObservableProperty]
    private int _tableCount;

    /// <summary>
    /// Called when user clicks "+" on a disconnected node to auto-connect and load tables.
    /// </summary>
    public Func<DatabaseConnectionTreeNode, Task>? ExpandConnectAction { get; set; }

    public string StatusBadgeText => IsConnected ? (IsActive ? "ACTIVE" : "CONNECTED") : "DISCONNECTED";
    public string StatusColor => IsConnected ? (IsActive ? "#10B981" : "#3B82F6") : "#9CA3AF";

    public FolderTreeNode? TablesFolder { get; set; }

    public List<TableTreeNode> AllTableNodes { get; } = new();

    public DatabaseConnectionTreeNode(ConnectionRecord record)
    {
        Record = record;
        Id = ConnectionKey;
        Title = record.DisplayTitle;
        Subtitle = record.DatabaseType ?? "MSSQL";
        IsConnected = false;
        IsActive = false;
        // Add placeholder child so TreeView shows the expand "+" toggle even when disconnected
        Children.Add(new PlaceholderTreeNode());

        // When user clicks "+" on a disconnected database, auto-connect and load tables
        PropertyChanged += (_, e) =>
        {
            if (e.PropertyName == nameof(IsExpanded) && IsExpanded && !IsConnected && ExpandConnectAction != null)
            {
                _ = ExpandConnectAction(this);
            }
        };
    }

    public void LoadTables(System.Collections.Generic.IEnumerable<TableMetadata> tables, Func<TableTreeNode, Task>? executeAction = null)
    {
        var tableList = tables is System.Collections.Generic.List<TableMetadata> list ? list : new System.Collections.Generic.List<TableMetadata>(tables);
        TableCount = tableList.Count;

        // If TablesFolder already exists, reconcile existing nodes to preserve state (like IsActive)!
        if (TablesFolder != null && AllTableNodes.Count > 0)
        {
            var existingMap = AllTableNodes.ToDictionary(n => n.FullName, StringComparer.OrdinalIgnoreCase);
            var updatedNodes = new List<TableTreeNode>();

            foreach (var table in tableList)
            {
                if (existingMap.TryGetValue(table.FullName, out var existingNode))
                {
                    existingNode.ExecuteAction = executeAction;
                    updatedNodes.Add(existingNode);
                }
                else
                {
                    var newNode = new TableTreeNode(table, this)
                    {
                        ExecuteAction = executeAction
                    };
                    updatedNodes.Add(newNode);
                }
            }

            AllTableNodes.Clear();
            AllTableNodes.AddRange(updatedNodes);

            TablesFolder.Children.Clear();
            foreach (var node in AllTableNodes)
            {
                TablesFolder.Children.Add(node);
            }
            TablesFolder.Title = $"Tables ({TableCount})";
            Subtitle = $"{TableCount} table(s) found";
            return;
        }

        Children.Clear();
        AllTableNodes.Clear();

        var folder = new FolderTreeNode($"Tables ({TableCount})", this)
        {
            IsExpanded = true
        };

        foreach (var table in tableList)
        {
            var node = new TableTreeNode(table, this)
            {
                ExecuteAction = executeAction
            };
            AllTableNodes.Add(node);
            folder.Children.Add(node);
        }

        Children.Add(folder);
        TablesFolder = folder;
        Subtitle = $"{TableCount} table(s) found";
    }

    public int FilterTables(string? filter)
    {
        if (TablesFolder == null) return 0;

        TablesFolder.Children.Clear();
        var normalizedFilter = filter?.Trim().ToLowerInvariant();

        if (string.IsNullOrEmpty(normalizedFilter))
        {
            foreach (var node in AllTableNodes)
            {
                TablesFolder.Children.Add(node);
            }
            TablesFolder.Title = $"Tables ({AllTableNodes.Count})";
            return AllTableNodes.Count;
        }

        var matches = AllTableNodes.Where(n =>
            n.FullName.ToLowerInvariant().Contains(normalizedFilter) ||
            n.TableName.ToLowerInvariant().Contains(normalizedFilter) ||
            n.SchemaName.ToLowerInvariant().Contains(normalizedFilter)
        ).ToList();

        foreach (var node in matches)
        {
            TablesFolder.Children.Add(node);
        }

        TablesFolder.Title = $"Tables ({matches.Count}/{AllTableNodes.Count})";
        if (matches.Count > 0)
        {
            IsExpanded = true;
            TablesFolder.IsExpanded = true;
        }

        return matches.Count;
    }

    public void ClearTables()
    {
        Children.Clear();
        AllTableNodes.Clear();
        TablesFolder = null;
        TableCount = 0;
        Subtitle = DatabaseType ?? "MSSQL";
        // Re-add placeholder so "+" toggle remains visible
        Children.Add(new PlaceholderTreeNode());
    }
}

public partial class FolderTreeNode : SidebarTreeNodeBase
{
    public DatabaseConnectionTreeNode DatabaseNode { get; }

    [RelayCommand]
    public void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    public FolderTreeNode(string title, DatabaseConnectionTreeNode dbNode)
    {
        Title = title;
        DatabaseNode = dbNode;
        IsExpanded = true;
    }
}

public partial class TableTreeNode : SidebarTreeNodeBase
{
    public TableMetadata Table { get; }
    public DatabaseConnectionTreeNode DatabaseNode { get; }

    public string TableName => Table.TableName;
    public string SchemaName => Table.SchemaName;
    public string FullName => Table.FullName;
    public string DisplayType => Table.DisplayType;

    public Func<TableTreeNode, Task>? ExecuteAction { get; set; }

    [ObservableProperty]
    private bool _isActive;

    [RelayCommand]
    public async Task ExecuteTop100Async()
    {
        if (ExecuteAction != null)
        {
            await ExecuteAction(this);
        }
    }

    [RelayCommand]
    public async Task CopyTableNameAsync()
    {
        if (Avalonia.Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
        {
            var clipboard = desktop.MainWindow?.Clipboard;
            if (clipboard != null)
            {
                await clipboard.SetTextAsync(FullName);
            }
        }
    }

    public TableTreeNode(TableMetadata table, DatabaseConnectionTreeNode dbNode)
    {
        Table = table;
        DatabaseNode = dbNode;
        Title = table.FullName;
        Subtitle = table.DisplayType;
    }
}

/// <summary>
/// Invisible placeholder child node. Ensures the TreeView shows
/// the expand/collapse "+" toggle on disconnected database nodes.
/// When the user clicks "+", the expand event fires and
/// SidebarViewModel auto-connects and loads real table nodes.
/// </summary>
public partial class PlaceholderTreeNode : SidebarTreeNodeBase
{
    public PlaceholderTreeNode()
    {
        Title = "Loading...";
    }
}
