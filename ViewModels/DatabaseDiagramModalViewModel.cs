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
    private string _statusMessage = string.Empty;

    [ObservableProperty]
    private bool _hasStatusMessage;

    public ObservableCollection<DiagramTableItem> AllTables { get; } = new();
    public ObservableCollection<DiagramTableItem> FilteredTables { get; } = new();

    public int TotalTablesCount => AllTables.Count;
    public int TotalRelationsCount => AllTables.Sum(t => t.OutgoingRelations.Count);

    public Func<string, Task>? OpenTableDataCallback { get; set; }
    public Action<string>? GenerateDdlCallback { get; set; }

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
    }

    partial void OnSearchFilterChanged(string value)
    {
        ApplyFilter();
    }

    public void ApplyFilter()
    {
        FilteredTables.Clear();
        var filter = SearchFilter?.Trim();

        foreach (var table in AllTables)
        {
            if (string.IsNullOrWhiteSpace(filter) ||
                table.TableName.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                table.Columns.Any(c => c.ColumnName.Contains(filter, StringComparison.OrdinalIgnoreCase)))
            {
                FilteredTables.Add(table);
            }
        }

        OnPropertyChanged(nameof(TotalTablesCount));
        OnPropertyChanged(nameof(TotalRelationsCount));
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
                // Format: SourceTable -> TargetTable
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
