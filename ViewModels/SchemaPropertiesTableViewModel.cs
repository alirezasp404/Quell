using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Models;
using Quell.Services.DemoData;

namespace Quell.ViewModels;

public partial class SchemaPropertiesTableViewModel : ViewModelBase
{
    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _tableName = string.Empty;

    [ObservableProperty]
    private string _statusMessage = string.Empty;

    public ObservableCollection<TableColumnItem> Columns { get; } = new();

    public void LoadDemoColumns(string tableName)
    {
        TableName = tableName;
        Columns.Clear();
        foreach (var col in DemoDataProvider.GetSampleSchemaColumns(tableName))
        {
            Columns.Add(col);
        }
    }

    [RelayCommand]
    public async Task CopyColumnNamesAsync()
    {
        var names = string.Join(", ", Columns.Select(c => c.Name));
        await Quell.Services.ClipboardService.CopyTextAsync(names);
        StatusMessage = "Copied column names to clipboard!";
    }
}