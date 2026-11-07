using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia;
using Avalonia.Input.Platform;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

public class DiagramColumnItem
{
    public string ColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsForeignKey { get; set; }
    public string? ReferencedTable { get; set; }
    public string? ReferencedColumn { get; set; }

    public string BadgeText => IsPrimaryKey ? "PK" : (IsForeignKey ? "FK" : string.Empty);
    public string FullDisplay => $"{ColumnName} : {DataType}";
}

public partial class DiagramTableItem : ObservableObject
{
    public string TableName { get; set; } = string.Empty;
    public string Schema { get; set; } = "dbo";
    public string DatabaseType { get; set; } = "SQL";
    public string FullName => string.IsNullOrWhiteSpace(Schema) ? TableName : $"{Schema}.{TableName}";

    public ObservableCollection<DiagramColumnItem> Columns { get; } = new();
    public ObservableCollection<string> OutgoingRelations { get; } = new();

    public int PrimaryKeysCount => Columns.Count(c => c.IsPrimaryKey);
    public int ForeignKeysCount => Columns.Count(c => c.IsForeignKey);
    public int TotalColumnsCount => Columns.Count;
}

public partial class DatabaseDiagramModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _databaseTitle = "Active Database";

    [ObservableProperty]
    private string _databaseType = "SQL";

    [ObservableProperty]
    private string _searchFilter = string.Empty;

    [ObservableProperty]
    private string _filterMode = "All"; // "All", "Relations", "PK"

    [ObservableProperty]
    private double _zoomLevel = 1.0; // 0.7 .. 1.5

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    [NotifyPropertyChangedFor(nameof(IsEmptyState))]
    private bool _isLoading;

    [ObservableProperty]
    private string _loadingMessage = "Analyzing database schema & relationships...";

    [ObservableProperty]
    private string _loadingDetailText = "Scanning tables, primary keys, and foreign keys...";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsIndeterminate))]
    private double _loadingProgress;

    public bool IsIndeterminate => LoadingProgress <= 0.0;

    public bool IsEmptyState => !IsLoading && FilteredTables.Count == 0;

    public ObservableCollection<DiagramTableItem> AllTables { get; } = new();
    public ObservableCollection<DiagramTableItem> FilteredTables { get; } = new();

    public int TotalTablesCount => AllTables.Count;
    public int TotalRelationsCount => AllTables.Sum(t => t.OutgoingRelations.Count);
    public int TablesWithRelationsCount => AllTables.Count(t => t.OutgoingRelations.Count > 0 || t.Columns.Any(c => c.IsForeignKey));
    public int TablesWithPkCount => AllTables.Count(t => t.Columns.Any(c => c.IsPrimaryKey));

    public Func<string, Task>? OpenTableDataCallback { get; set; }
    public Action<string>? GenerateDdlCallback { get; set; }
    public Func<Task>? RefreshDiagramCallback { get; set; }

    public DatabaseDiagramModalViewModel()
    {
    }

    [RelayCommand]
    public void Open()
    {
        IsOpen = true;
        ApplyFilter();
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        IsLoading = false;
    }

    [RelayCommand]
    public void SetFilterMode(string mode)
    {
        FilterMode = mode;
        ApplyFilter();
    }

    [RelayCommand]
    public void ZoomIn()
    {
        ZoomLevel = Math.Min(1.5, Math.Round(ZoomLevel + 0.1, 1));
    }

    [RelayCommand]
    public void ZoomOut()
    {
        ZoomLevel = Math.Max(0.7, Math.Round(ZoomLevel - 0.1, 1));
    }

    [RelayCommand]
    public void ResetZoom()
    {
        ZoomLevel = 1.0;
    }

    [RelayCommand]
    public async Task RefreshDiagramAsync()
    {
        if (RefreshDiagramCallback != null)
        {
            await RefreshDiagramCallback();
        }
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    partial void OnFilterModeChanged(string value)
    {
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredTables.Clear();
        var filter = SearchFilter?.Trim();

        foreach (var table in AllTables)
        {
            if (FilterMode == "Relations" && table.OutgoingRelations.Count == 0 && !table.Columns.Any(c => c.IsForeignKey))
            {
                continue;
            }
            if (FilterMode == "PK" && !table.Columns.Any(c => c.IsPrimaryKey))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(filter) ||
                table.TableName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                table.Columns.Any(c => c.ColumnName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredTables.Add(table);
            }
        }

        OnPropertyChanged(nameof(TotalTablesCount));
        OnPropertyChanged(nameof(TotalRelationsCount));
        OnPropertyChanged(nameof(TablesWithRelationsCount));
        OnPropertyChanged(nameof(TablesWithPkCount));
        OnPropertyChanged(nameof(IsEmptyState));
    }

    public void LoadTables(IEnumerable<DiagramTableItem> tables, string databaseName, string dbType)
    {
        DatabaseTitle = databaseName;
        DatabaseType = dbType;
        AllTables.Clear();

        foreach (var t in tables)
        {
            AllTables.Add(t);
        }

        ApplyFilter();
        IsLoading = false;
    }

    [RelayCommand]
    public async Task CopyMermaidDiagramAsync()
    {
        var sb = new StringBuilder();
        sb.AppendLine("erDiagram");

        foreach (var table in AllTables)
        {
            sb.AppendLine($"    {table.TableName} {{");
            foreach (var col in table.Columns)
            {
                string keyType = col.IsPrimaryKey ? "PK" : (col.IsForeignKey ? "FK" : "");
                string cleanType = col.DataType.Replace(" ", "_").Replace("(", "_").Replace(")", "_");
                if (string.IsNullOrEmpty(cleanType)) cleanType = "string";
                sb.AppendLine($"        {cleanType} {col.ColumnName} {keyType}".TrimEnd());
            }
            sb.AppendLine("    }");

            foreach (var rel in table.OutgoingRelations)
            {
                var parts = rel.Split(new[] { "->" }, StringSplitOptions.RemoveEmptyEntries);
                if (parts.Length == 2)
                {
                    string target = parts[1].Trim().Split(' ')[0];
                    sb.AppendLine($"    {table.TableName} }}o--|| {target} : references");
                }
            }
        }

        string mermaid = sb.ToString();

        try
        {
            if (Application.Current?.ApplicationLifetime is Avalonia.Controls.ApplicationLifetimes.IClassicDesktopStyleApplicationLifetime desktop)
            {
                var clipboard = desktop.MainWindow?.Clipboard;
                if (clipboard != null)
                {
                    await clipboard.SetTextAsync(mermaid);
                    StatusMessage = "Mermaid ER Diagram copied to clipboard!";
                    HasStatusMessage = true;
                    return;
                }
            }
            StatusMessage = "Mermaid ER Diagram generated!";
            HasStatusMessage = true;
        }
        catch
        {
            StatusMessage = "Mermaid ER Diagram generated!";
            HasStatusMessage = true;
        }
    }

    [RelayCommand]
    public async Task ViewTableData(DiagramTableItem? table)
    {
        if (table == null || OpenTableDataCallback == null) return;
        IsOpen = false;
        await OpenTableDataCallback.Invoke(table.FullName);
    }

    [RelayCommand]
    public void GenerateDdl(DiagramTableItem? table)
    {
        if (table == null || GenerateDdlCallback == null) return;
        IsOpen = false;
        GenerateDdlCallback.Invoke(table.FullName);
    }
}
