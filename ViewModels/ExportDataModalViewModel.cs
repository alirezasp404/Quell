using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quell.ViewModels;

public partial class ExportDataModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _exportSource = "Specific Database Table";

    public ObservableCollection<string> ExportSources { get; } = new()
    {
        "Specific Database Table",
        "Active Query Results"
    };

    [ObservableProperty]
    private string? _selectedTable;

    public ObservableCollection<string> AvailableTables { get; } = new();

    public bool IsTableSourceSelected => ExportSource == "Specific Database Table";

    [ObservableProperty]
    private string _selectedFormat = "Excel Spreadsheet (*.xlsx)";

    public ObservableCollection<string> ExportFormats { get; } = new()
    {
        "Excel Spreadsheet (*.xlsx)",
        "CSV (Comma Separated Values)",
        "JSON (JavaScript Object Notation)",
        "SQL (INSERT INTO statements)",
        "TSV (Tab Separated Values)",
        "HTML Document (*.html)"
    };

    [ObservableProperty]
    private bool _includeHeaders = true;

    [ObservableProperty]
    private string _delimiter = ",";

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isSuccess;

    public Func<string, string, bool, string, Task<(bool Success, string Message, int RowCount)>>? PerformExportCallback { get; set; }

    public ExportDataModalViewModel()
    {
        UpdateSuggestedFilePath();
    }

    partial void OnExportSourceChanged(string value)
    {
        OnPropertyChanged(nameof(IsTableSourceSelected));
        UpdateSuggestedFilePath();
    }

    partial void OnSelectedTableChanged(string? value)
    {
        UpdateSuggestedFilePath();
    }

    partial void OnSelectedFormatChanged(string value)
    {
        UpdateSuggestedFilePath();
    }

    public void UpdateSuggestedFilePath()
    {
        string desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        if (string.IsNullOrEmpty(desktopPath))
        {
            desktopPath = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
        }

        string baseName = "export_results";
        if (IsTableSourceSelected && !string.IsNullOrWhiteSpace(SelectedTable))
        {
            baseName = SelectedTable.Replace(".", "_").Replace("[", "").Replace("]", "").Replace("\"", "");
        }

        string ext = ".xlsx";
        if (SelectedFormat.Contains("CSV", StringComparison.OrdinalIgnoreCase)) ext = ".csv";
        else if (SelectedFormat.Contains("JSON", StringComparison.OrdinalIgnoreCase)) ext = ".json";
        else if (SelectedFormat.Contains("SQL", StringComparison.OrdinalIgnoreCase)) ext = ".sql";
        else if (SelectedFormat.Contains("TSV", StringComparison.OrdinalIgnoreCase)) ext = ".tsv";
        else if (SelectedFormat.Contains("HTML", StringComparison.OrdinalIgnoreCase)) ext = ".html";
        else if (SelectedFormat.Contains("Excel", StringComparison.OrdinalIgnoreCase)) ext = ".xlsx";

        FilePath = Path.Combine(desktopPath, $"{baseName}{ext}");
    }

    public void SetAvailableTables(IEnumerable<string> tables, string? defaultSelected = null)
    {
        AvailableTables.Clear();
        foreach (var t in tables)
        {
            AvailableTables.Add(t);
        }

        if (!string.IsNullOrWhiteSpace(defaultSelected) && AvailableTables.Contains(defaultSelected))
        {
            SelectedTable = defaultSelected;
        }
        else if (AvailableTables.Count > 0)
        {
            SelectedTable = AvailableTables[0];
        }

        UpdateSuggestedFilePath();
    }

    [RelayCommand]
    public void Open()
    {
        StatusMessage = null;
        HasStatusMessage = false;
        UpdateSuggestedFilePath();
        IsOpen = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    public async Task ExportAsync()
    {
        StatusMessage = "Exporting data...";
        HasStatusMessage = true;

        if (PerformExportCallback != null)
        {
            var res = await PerformExportCallback.Invoke(ExportSource, SelectedTable ?? string.Empty, IncludeHeaders, FilePath);
            IsSuccess = res.Success;
            StatusMessage = res.Message;
            if (res.Success)
            {
                await Task.Delay(600);
                IsOpen = false;
            }
        }
        else
        {
            IsOpen = false;
        }
    }

    [RelayCommand]
    public async Task BrowseFile(TopLevel? topLevel)
    {
        if (topLevel?.StorageProvider == null) return;

        string ext = "xlsx";
        if (SelectedFormat.Contains("CSV", StringComparison.OrdinalIgnoreCase)) ext = "csv";
        else if (SelectedFormat.Contains("JSON", StringComparison.OrdinalIgnoreCase)) ext = "json";
        else if (SelectedFormat.Contains("SQL", StringComparison.OrdinalIgnoreCase)) ext = "sql";
        else if (SelectedFormat.Contains("TSV", StringComparison.OrdinalIgnoreCase)) ext = "tsv";
        else if (SelectedFormat.Contains("HTML", StringComparison.OrdinalIgnoreCase)) ext = "html";

        string suggestedName = Path.GetFileNameWithoutExtension(FilePath);
        if (string.IsNullOrEmpty(suggestedName)) suggestedName = "export_results";

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Export File",
            SuggestedFileName = suggestedName,
            DefaultExtension = ext
        });

        if (file != null)
        {
            FilePath = file.Path.LocalPath;
        }
    }
}
