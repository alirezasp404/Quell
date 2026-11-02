using System;
using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
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

    public void UpdateRecord(ConnectionRecord record)
    {
        Record = record;
        Title = record.DisplayTitle;
        Subtitle = $"{record.DatabaseType} · {record.Server}:{record.Port}";
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

    public string StatusBadgeText => IsConnected ? (IsActive ? "ACTIVE" : "CONNECTED") : "DISCONNECTED";
    public string StatusColor => IsConnected ? (IsActive ? "#10B981" : "#3B82F6") : "#9CA3AF";

    public FolderTreeNode? TablesFolder { get; set; }

    public DatabaseConnectionTreeNode(ConnectionRecord record)
    {
        Record = record;
        Id = ConnectionKey;
        Title = record.DisplayTitle;
        Subtitle = $"{record.DatabaseType} · {record.Server}:{record.Port}";
        IsConnected = false;
        IsActive = false;
    }

    public void LoadTables(System.Collections.Generic.IEnumerable<TableMetadata> tables)
    {
        Children.Clear();
        var tableList = tables is System.Collections.Generic.List<TableMetadata> list ? list : new System.Collections.Generic.List<TableMetadata>(tables);
        TableCount = tableList.Count;

        var folder = new FolderTreeNode($"Tables ({TableCount})", this)
        {
            IsExpanded = true
        };

        foreach (var table in tableList)
        {
            folder.Children.Add(new TableTreeNode(table, this));
        }

        Children.Add(folder);
        TablesFolder = folder;
        Subtitle = $"{TableCount} table(s) found";
    }

    public void ClearTables()
    {
        Children.Clear();
        TablesFolder = null;
        TableCount = 0;
        Subtitle = "Disconnected · Click to connect";
    }
}

public partial class FolderTreeNode : SidebarTreeNodeBase
{
    public DatabaseConnectionTreeNode DatabaseNode { get; }

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

    public TableTreeNode(TableMetadata table, DatabaseConnectionTreeNode dbNode)
    {
        Table = table;
        DatabaseNode = dbNode;
        Title = table.FullName;
        Subtitle = table.DisplayType;
    }
}
