using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;

namespace Quell.ViewModels;

public partial class ImportColumnMappingItem : ObservableObject
{
    public const string SkipOption = "[-- Do Not Import / Skip --]";
    public const string ConstantOption = "[-- Constant / Fixed Value --]";

    public string TargetColumnName { get; set; } = string.Empty;
    public string DataType { get; set; } = string.Empty;
    public bool IsPrimaryKey { get; set; }
    public bool IsRequired { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMapped))]
    [NotifyPropertyChangedFor(nameof(IsSkipped))]
    [NotifyPropertyChangedFor(nameof(IsConstant))]
    [NotifyPropertyChangedFor(nameof(StatusBadge))]
    [NotifyPropertyChangedFor(nameof(StatusForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    private string _selectedExcelColumn = SkipOption;

    [ObservableProperty]
    private string _constantValue = string.Empty;

    public ObservableCollection<string> AvailableExcelColumns { get; } = new();

    public bool IsMapped => !IsSkipped;
    public bool IsSkipped => string.IsNullOrWhiteSpace(SelectedExcelColumn) || SelectedExcelColumn == SkipOption;
    public bool IsConstant => SelectedExcelColumn == ConstantOption;
    public bool HasWarning => IsRequired && IsSkipped;

    public string StatusBadge
    {
        get
        {
            if (IsConstant) return "Constant";
            if (IsMapped) return "Mapped";
            if (IsRequired) return "Required Missing!";
            return "Skipped";
        }
    }

    public string StatusForeground
    {
        get
        {
            if (IsConstant) return "#38BDF8"; // Cyan
            if (IsMapped) return "#10B981";   // Emerald Green
            if (IsRequired) return "#F59E0B"; // Amber warning
            return "#94A3B8";                 // Muted gray
        }
    }

    public string StatusBackground
    {
        get
        {
            if (IsConstant) return "#2038BDF8";
            if (IsMapped) return "#2010B981";
            if (IsRequired) return "#20F59E0B";
            return "#1094A3B8";
        }
    }

    public string PkBadgeText => IsPrimaryKey ? "PK" : (IsRequired ? "REQ" : "NULL");
    public string PkBadgeColor => IsPrimaryKey ? "#F59E0B" : (IsRequired ? "#EF4444" : "#64748B");
}

public partial class ImportDataModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _targetDatabaseName = "Active Database";

    [ObservableProperty]
    private string? _selectedTable;

    public ObservableCollection<string> AvailableTables { get; } = new();

    [ObservableProperty]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private bool _hasFileLoaded;

    [ObservableProperty]
    private string _fileName = string.Empty;

    [ObservableProperty]
    private string _fileSizeText = string.Empty;

    public ObservableCollection<string> AvailableSheets { get; } = new();

    [ObservableProperty]
    private string _selectedSheet = string.Empty;

    [ObservableProperty]
    private bool _firstRowIsHeader = true;

    [ObservableProperty]
    private bool _truncateBeforeImport;

    [ObservableProperty]
    private bool _skipErrors = true;

    [ObservableProperty]
    private int _batchSize = 250;

    public ObservableCollection<int> BatchSizes { get; } = new() { 50, 100, 250, 500, 1000 };

    [ObservableProperty]
    private string _selectedTab = "Mapping"; // "Mapping", "ExcelPreview", "ImportPreview"

    public bool IsMappingTab => SelectedTab == "Mapping";
    public bool IsExcelPreviewTab => SelectedTab == "ExcelPreview";
    public bool IsImportPreviewTab => SelectedTab == "ImportPreview";

    public ObservableCollection<ImportColumnMappingItem> ColumnMappings { get; } = new();
    public ObservableCollection<string> ExcelHeaders { get; } = new();

    // Data previews
    public List<List<string>> AllRawRows { get; } = new();
    public ObservableCollection<Dictionary<string, string>> RawPreviewRows { get; } = new();
    public ObservableCollection<Dictionary<string, string>> MappedPreviewRows { get; } = new();

    [ObservableProperty]
    private int _totalExcelRowsCount;

    [ObservableProperty]
    private int _mappedColumnsCount;

    [ObservableProperty]
    private int _totalTableColumnsCount;

    [ObservableProperty]
    private bool _isImporting;

    [ObservableProperty]
    private double _importProgress;

    [ObservableProperty]
    private string _importProgressText = string.Empty;

    [ObservableProperty]
    private string? _statusMessage;

    [ObservableProperty]
    private bool _hasStatusMessage;

    [ObservableProperty]
    private bool _isSuccess;

    // Callbacks provided by MainViewModel
    public Func<string, Task<List<(string ColumnName, string DataType, bool IsPk, bool IsRequired)>>>? LoadTableColumnsCallback { get; set; }
    public Func<string, List<ImportColumnMappingItem>, List<List<string>>, bool, bool, int, Action<int, int>, Task<(bool Success, string Message, int RowsImported)>>? PerformImportCallback { get; set; }

    public ImportDataModalViewModel()
    {
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
    }

    partial void OnSelectedTableChanged(string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            _ = LoadTargetTableSchemaAsync(value);
        }
    }

    partial void OnSelectedTabChanged(string value)
    {
        OnPropertyChanged(nameof(IsMappingTab));
        OnPropertyChanged(nameof(IsExcelPreviewTab));
        OnPropertyChanged(nameof(IsImportPreviewTab));

        if (value == "ImportPreview")
        {
            RefreshMappedPreview();
        }
    }

    [RelayCommand]
    public void SetTab(string tab)
    {
        SelectedTab = tab;
    }

    [RelayCommand]
    public void Open()
    {
        StatusMessage = null;
        HasStatusMessage = false;
        IsImporting = false;
        IsOpen = true;

        if (!string.IsNullOrWhiteSpace(SelectedTable) && ColumnMappings.Count == 0)
        {
            _ = LoadTargetTableSchemaAsync(SelectedTable);
        }
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    public async Task LoadTargetTableSchemaAsync(string tableName)
    {
        if (LoadTableColumnsCallback == null) return;

        try
        {
            var cols = await LoadTableColumnsCallback(tableName);
            ColumnMappings.Clear();

            foreach (var col in cols)
            {
                var item = new ImportColumnMappingItem
                {
                    TargetColumnName = col.ColumnName,
                    DataType = col.DataType,
                    IsPrimaryKey = col.IsPk,
                    IsRequired = col.IsRequired
                };

                // Populate available Excel column options
                PopulateColumnDropdown(item);
                ColumnMappings.Add(item);
            }

            TotalTableColumnsCount = ColumnMappings.Count;

            // Try to auto-map if Excel file is already loaded
            if (HasFileLoaded)
            {
                AutoMapColumns();
            }
            else
            {
                UpdateMappingCounts();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load table schema: {ex.Message}";
            HasStatusMessage = true;
            IsSuccess = false;
        }
    }

    private void PopulateColumnDropdown(ImportColumnMappingItem item)
    {
        item.AvailableExcelColumns.Clear();
        item.AvailableExcelColumns.Add(ImportColumnMappingItem.SkipOption);
        item.AvailableExcelColumns.Add(ImportColumnMappingItem.ConstantOption);

        for (int i = 0; i < ExcelHeaders.Count; i++)
        {
            string letter = ExcelDataService.IndexToColumnLetter(i);
            item.AvailableExcelColumns.Add($"{letter}: {ExcelHeaders[i]}");
        }
    }

    [RelayCommand]
    public async Task BrowseFileAsync(TopLevel? topLevel)
    {
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select Excel or CSV Data File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("Spreadsheets & CSV (*.xlsx, *.csv, *.tsv, *.xml)")
                {
                    Patterns = new[] { "*.xlsx", "*.csv", "*.tsv", "*.xml", "*.xlsm", "*.txt" }
                },
                new("Excel Workbooks (*.xlsx)")
                {
                    Patterns = new[] { "*.xlsx", "*.xlsm" }
                },
                new("CSV / Delimited Files (*.csv, *.tsv)")
                {
                    Patterns = new[] { "*.csv", "*.tsv", "*.txt" }
                },
                new("All Files (*.*)")
                {
                    Patterns = new[] { "*.*" }
                }
            }
        });

        if (files.Count > 0)
        {
            FilePath = files[0].Path.LocalPath;
            await LoadSpreadsheetFileAsync(FilePath);
        }
    }

    public async Task LoadSpreadsheetFileAsync(string path)
    {
        if (!File.Exists(path)) return;

        try
        {
            FileName = Path.GetFileName(path);
            var fi = new FileInfo(path);
            FileSizeText = fi.Length < 1024 * 1024
                ? $"{fi.Length / 1024.0:F1} KB"
                : $"{fi.Length / (1024.0 * 1024.0):F2} MB";

            var data = await Task.Run(() => ExcelDataService.ReadSpreadsheet(path, targetSheet: null, firstRowIsHeader: FirstRowIsHeader));

            AvailableSheets.Clear();
            foreach (var s in data.SheetNames)
            {
                AvailableSheets.Add(s);
            }
            SelectedSheet = data.ActiveSheet;

            ExcelHeaders.Clear();
            foreach (var h in data.Headers)
            {
                ExcelHeaders.Add(h);
            }

            AllRawRows.Clear();
            AllRawRows.AddRange(data.Rows);
            TotalExcelRowsCount = data.TotalRowCount;

            // Generate raw preview (first 10 rows)
            RawPreviewRows.Clear();
            for (int r = 0; r < Math.Min(10, data.Rows.Count); r++)
            {
                var dict = new Dictionary<string, string>();
                for (int c = 0; c < data.Headers.Count; c++)
                {
                    string colKey = data.Headers[c];
                    string val = c < data.Rows[r].Count ? data.Rows[r][c] : string.Empty;
                    dict[colKey] = val;
                }
                RawPreviewRows.Add(dict);
            }

            HasFileLoaded = true;

            // Re-populate dropdowns for each mapping item
            foreach (var mapping in ColumnMappings)
            {
                PopulateColumnDropdown(mapping);
            }

            AutoMapColumns();

            StatusMessage = $"Loaded {TotalExcelRowsCount} rows and {ExcelHeaders.Count} columns from '{FileName}'";
            HasStatusMessage = true;
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            HasFileLoaded = false;
            StatusMessage = $"Failed to read spreadsheet: {ex.Message}";
            HasStatusMessage = true;
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public void AutoMapColumns()
    {
        if (!HasFileLoaded || ColumnMappings.Count == 0 || ExcelHeaders.Count == 0) return;

        foreach (var mapping in ColumnMappings)
        {
            string targetNormalized = NormalizeName(mapping.TargetColumnName);

            // Find best matching Excel column
            int bestIndex = -1;

            // 1. Exact match (case insensitive)
            for (int i = 0; i < ExcelHeaders.Count; i++)
            {
                if (ExcelHeaders[i].Equals(mapping.TargetColumnName, StringComparison.OrdinalIgnoreCase))
                {
                    bestIndex = i;
                    break;
                }
            }

            // 2. Normalized match (ignoring underscores, spaces, dashes)
            if (bestIndex < 0)
            {
                for (int i = 0; i < ExcelHeaders.Count; i++)
                {
                    if (NormalizeName(ExcelHeaders[i]) == targetNormalized)
                    {
                        bestIndex = i;
                        break;
                    }
                }
            }

            // 3. Contains match
            if (bestIndex < 0)
            {
                for (int i = 0; i < ExcelHeaders.Count; i++)
                {
                    string hNorm = NormalizeName(ExcelHeaders[i]);
                    if (hNorm.Contains(targetNormalized) || targetNormalized.Contains(hNorm))
                    {
                        bestIndex = i;
                        break;
                    }
                }
            }

            if (bestIndex >= 0)
            {
                string letter = ExcelDataService.IndexToColumnLetter(bestIndex);
                string option = $"{letter}: {ExcelHeaders[bestIndex]}";
                if (mapping.AvailableExcelColumns.Contains(option))
                {
                    mapping.SelectedExcelColumn = option;
                }
            }
            else
            {
                mapping.SelectedExcelColumn = ImportColumnMappingItem.SkipOption;
            }
        }

        UpdateMappingCounts();
    }

    [RelayCommand]
    public void ClearMappings()
    {
        foreach (var m in ColumnMappings)
        {
            m.SelectedExcelColumn = ImportColumnMappingItem.SkipOption;
        }
        UpdateMappingCounts();
    }

    public void UpdateMappingCounts()
    {
        MappedColumnsCount = ColumnMappings.Count(m => m.IsMapped);
        TotalTableColumnsCount = ColumnMappings.Count;
    }

    public void RefreshMappedPreview()
    {
        MappedPreviewRows.Clear();
        if (!HasFileLoaded || AllRawRows.Count == 0) return;

        var mappedCols = ColumnMappings.Where(m => m.IsMapped).ToList();
        if (mappedCols.Count == 0) return;

        for (int r = 0; r < Math.Min(8, AllRawRows.Count); r++)
        {
            var dict = new Dictionary<string, string>();
            foreach (var m in mappedCols)
            {
                if (m.IsConstant)
                {
                    dict[m.TargetColumnName] = m.ConstantValue;
                }
                else
                {
                    int colIdx = ExtractExcelColumnIndex(m.SelectedExcelColumn);
                    string val = (colIdx >= 0 && colIdx < AllRawRows[r].Count) ? AllRawRows[r][colIdx] : string.Empty;
                    dict[m.TargetColumnName] = val;
                }
            }
            MappedPreviewRows.Add(dict);
        }
    }

    private int ExtractExcelColumnIndex(string option)
    {
        if (string.IsNullOrWhiteSpace(option) || !option.Contains(':')) return -1;
        string letter = option.Split(':')[0].Trim();
        return ExcelDataService.ParseColumnIndexFromRef(letter);
    }

    [RelayCommand]
    public async Task DownloadTemplateAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTable) || ColumnMappings.Count == 0)
        {
            StatusMessage = "Please select a database table first.";
            HasStatusMessage = true;
            IsSuccess = false;
            return;
        }

        try
        {
            string desktop = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
            if (string.IsNullOrEmpty(desktop)) desktop = Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);

            string cleanName = SelectedTable.Replace(".", "_").Replace("[", "").Replace("]", "").Replace("\"", "");
            string templatePath = Path.Combine(desktop, $"{cleanName}_import_template.xlsx");

            var columns = ColumnMappings.Select(c => (c.TargetColumnName, c.DataType, c.IsRequired)).ToList();
            ExcelDataService.CreateExcelTemplate(templatePath, cleanName, columns);

            StatusMessage = $"Excel Template created on Desktop: '{Path.GetFileName(templatePath)}'";
            HasStatusMessage = true;
            IsSuccess = true;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to create template: {ex.Message}";
            HasStatusMessage = true;
            IsSuccess = false;
        }
    }

    [RelayCommand]
    public async Task ExecuteImportAsync()
    {
        if (string.IsNullOrWhiteSpace(SelectedTable))
        {
            StatusMessage = "Please select a target table.";
            HasStatusMessage = true;
            IsSuccess = false;
            return;
        }

        if (!HasFileLoaded || AllRawRows.Count == 0)
        {
            StatusMessage = "Please select a spreadsheet file with data.";
            HasStatusMessage = true;
            IsSuccess = false;
            return;
        }

        var mappedCols = ColumnMappings.Where(m => m.IsMapped).ToList();
        if (mappedCols.Count == 0)
        {
            StatusMessage = "No columns mapped! Please map at least one column to import.";
            HasStatusMessage = true;
            IsSuccess = false;
            return;
        }

        // Check for required columns missing
        var missingRequired = ColumnMappings.Where(m => m.HasWarning).ToList();
        if (missingRequired.Count > 0)
        {
            string colNames = string.Join(", ", missingRequired.Select(c => c.TargetColumnName));
            // Just warn but allow proceeding
        }

        IsImporting = true;
        ImportProgress = 0.0;
        ImportProgressText = "Preparing bulk insert statements...";
        StatusMessage = "Importing data into database...";
        HasStatusMessage = true;

        try
        {
            if (PerformImportCallback != null)
            {
                var result = await PerformImportCallback(
                    SelectedTable,
                    mappedCols,
                    AllRawRows,
                    TruncateBeforeImport,
                    SkipErrors,
                    BatchSize,
                    (imported, total) =>
                    {
                        double p = (double)imported / total * 100.0;
                        ImportProgress = p;
                        ImportProgressText = $"Imported {imported:N0} of {total:N0} rows ({p:F0}%)...";
                    });

                IsImporting = false;
                IsSuccess = result.Success;
                StatusMessage = result.Message;
                HasStatusMessage = true;

                if (result.Success)
                {
                    await Task.Delay(1500);
                    IsOpen = false;
                }
            }
        }
        catch (Exception ex)
        {
            IsImporting = false;
            IsSuccess = false;
            StatusMessage = $"Import error: {ex.Message}";
            HasStatusMessage = true;
        }
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        return Regex.Replace(name, @"[\s_\-\[\]""]", "").ToLowerInvariant();
    }
}
