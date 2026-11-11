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
    public int TargetColumnIndex { get; set; }

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsMapped))]
    [NotifyPropertyChangedFor(nameof(IsSkipped))]
    [NotifyPropertyChangedFor(nameof(IsConstant))]
    [NotifyPropertyChangedFor(nameof(StatusBadge))]
    [NotifyPropertyChangedFor(nameof(StatusForeground))]
    [NotifyPropertyChangedFor(nameof(StatusBackground))]
    [NotifyPropertyChangedFor(nameof(HasWarning))]
    [NotifyPropertyChangedFor(nameof(NodeColor))]
    [NotifyPropertyChangedFor(nameof(CardBorderBrush))]
    private string _selectedExcelColumn = SkipOption;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(NodeColor))]
    [NotifyPropertyChangedFor(nameof(CardBorderBrush))]
    private string _constantValue = string.Empty;

    [ObservableProperty]
    private string _samplePreviewValue = "(no source connected)";

    [ObservableProperty]
    private string _matchQuality = "Unmapped"; // "Exact Match", "Smart Match", "1:1 Position", "Manual", "Constant", "Unmapped"

    public ObservableCollection<string> AvailableExcelColumns { get; } = new();

    public Action? OnMappingChanged { get; set; }
    public Func<string, string>? ResolveSampleValueCallback { get; set; }

    public bool IsMapped => !IsSkipped;
    public bool IsSkipped => string.IsNullOrWhiteSpace(SelectedExcelColumn) || SelectedExcelColumn == SkipOption;
    public bool IsConstant => SelectedExcelColumn == ConstantOption;
    public bool HasWarning => IsRequired && IsSkipped;

    public string StatusBadge
    {
        get
        {
            if (IsConstant) return "Constant Value";
            if (IsMapped) return string.IsNullOrWhiteSpace(MatchQuality) ? "Mapped" : MatchQuality;
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
            if (IsRequired) return "#25F59E0B";
            return "#1094A3B8";
        }
    }

    public string PkBadgeText => IsPrimaryKey ? "PK" : (IsRequired ? "REQ" : "NULL");
    public string PkBadgeColor => IsPrimaryKey ? "#F59E0B" : (IsRequired ? "#EF4444" : "#64748B");

    public string DataTypeBadgeColor => DataType.ToUpperInvariant() switch
    {
        var t when t.Contains("INT") || t.Contains("NUM") || t.Contains("FLOAT") || t.Contains("DECIMAL") || t.Contains("DOUBLE") || t.Contains("REAL") => "#2563EB",
        var t when t.Contains("CHAR") || t.Contains("TEXT") || t.Contains("STRING") || t.Contains("CLOB") => "#7C3AED",
        var t when t.Contains("DATE") || t.Contains("TIME") => "#D97706",
        var t when t.Contains("BOOL") || t.Contains("BIT") => "#059669",
        _ => "#64748B"
    };

    public string NodeColor => IsConstant ? "#38BDF8" : (IsMapped ? "#10B981" : (HasWarning ? "#EF4444" : "#475569"));
    public string CardBorderBrush => IsConstant ? "#4038BDF8" : (IsMapped ? "#4010B981" : (HasWarning ? "#50EF4444" : "#20475569"));

    [RelayCommand]
    public void Unlink()
    {
        SelectedExcelColumn = SkipOption;
        MatchQuality = "Unmapped";
        UpdateSamplePreview();
        OnMappingChanged?.Invoke();
    }

    public void UpdateSamplePreview()
    {
        if (ResolveSampleValueCallback != null)
        {
            SamplePreviewValue = ResolveSampleValueCallback(SelectedExcelColumn);
        }
    }

    partial void OnSelectedExcelColumnChanged(string value)
    {
        if (value == ConstantOption)
        {
            MatchQuality = "Constant";
        }
        else if (value == SkipOption)
        {
            MatchQuality = "Unmapped";
        }
        else if (MatchQuality is "Unmapped" or "Constant")
        {
            MatchQuality = "Manual";
        }

        UpdateSamplePreview();
        OnPropertyChanged(nameof(StatusBadge));
        OnPropertyChanged(nameof(StatusForeground));
        OnPropertyChanged(nameof(StatusBackground));
        OnPropertyChanged(nameof(NodeColor));
        OnPropertyChanged(nameof(CardBorderBrush));
        OnMappingChanged?.Invoke();
    }

    partial void OnConstantValueChanged(string value)
    {
        UpdateSamplePreview();
        OnPropertyChanged(nameof(StatusBadge));
        OnMappingChanged?.Invoke();
    }
}

public partial class ImportDataModalViewModel : ViewModelBase
{
    // Comprehensive English & Persian common synonym dictionary for database column auto-mapping
    private static readonly Dictionary<string, string[]> CommonSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["id"] = new[] { "id", "identifier", "code", "no", "number", "key", "شناسه", "کد", "شماره", "ردیف", "کد_شناسایی" },
        ["code"] = new[] { "code", "id", "no", "number", "کد", "شناسه", "شماره" },
        ["name"] = new[] { "name", "title", "fullname", "full_name", "first_name", "firstname", "fname", "user", "username", "نام", "اسم", "عنوان", "نام و نام خانوادگی", "نام کامل" },
        ["first_name"] = new[] { "first_name", "firstname", "fname", "given_name", "forename", "name", "نام", "اسم", "نام_کوچک" },
        ["last_name"] = new[] { "last_name", "lastname", "lname", "surname", "family", "family_name", "نام خانوادگی", "نام_خانوادگی", "فامیلی", "شهرت" },
        ["full_name"] = new[] { "full_name", "fullname", "name", "display_name", "نام کامل", "نام و نام خانوادگی", "نام_کامل" },
        ["email"] = new[] { "email", "e_mail", "mail", "email_address", "electronic_mail", "ایمیل", "پست الکترونیک", "پست_الکترونیک", "رایانامه" },
        ["phone"] = new[] { "phone", "phonenumber", "phone_number", "telephone", "tel", "mobile", "cell", "cellphone", "mobile_number", "contact", "شماره تماس", "تلفن", "موبایل", "همراه", "شماره همراه", "شماره موبایل", "شماره تلفن" },
        ["mobile"] = new[] { "mobile", "mobile_number", "cell", "cellphone", "phone", "موبایل", "همراه", "شماره همراه", "شماره موبایل", "تلفن همراه" },
        ["tel"] = new[] { "tel", "telephone", "phone", "landline", "تلفن", "تلفن ثابت", "شماره تلفن" },
        ["national_code"] = new[] { "national_code", "national_id", "nationalcode", "nationalid", "meli_code", "melicode", "melli_code", "ssn", "کد ملی", "کدملی", "شماره ملی", "شناسه ملی" },
        ["address"] = new[] { "address", "addr", "location", "residence", "street", "آدرس", "نشانی", "محل سکونت" },
        ["city"] = new[] { "city", "town", "شهر", "شهرستان" },
        ["state"] = new[] { "state", "province", "استان", "ایالت" },
        ["country"] = new[] { "country", "کشور" },
        ["postal_code"] = new[] { "postal_code", "zip_code", "postalcode", "zipcode", "zip", "postcode", "کد پستی", "کدپستی", "زیپ کد" },
        ["gender"] = new[] { "gender", "sex", "جنسیت", "جنس" },
        ["age"] = new[] { "age", "سن", "سال" },
        ["birth_date"] = new[] { "birth_date", "birthdate", "birthday", "bdate", "dob", "تاریخ تولد", "تولد", "تاریخ_تولد" },
        ["date"] = new[] { "date", "datetime", "timestamp", "time", "تاریخ", "زمان" },
        ["created_at"] = new[] { "created_at", "createdat", "create_date", "creation_date", "register_date", "تاریخ ثبت", "تاریخ ایجاد", "زمان ایجاد", "تاریخ" },
        ["updated_at"] = new[] { "updated_at", "updatedat", "update_date", "modify_date", "modified_at", "تاریخ ویرایش", "تاریخ تغییر", "تاریخ بروزرسانی" },
        ["price"] = new[] { "price", "amount", "cost", "fee", "total", "unit_price", "قیمت", "مبلغ", "هزینه", "بها", "ارزش", "فی" },
        ["amount"] = new[] { "amount", "price", "total", "sum", "cost", "مبلغ", "مقدار", "میزان", "جمع", "قیمت" },
        ["salary"] = new[] { "salary", "wage", "income", "pay", "حقوق", "دستمزد", "درآمد", "پایه حقوق" },
        ["role"] = new[] { "role", "role_name", "position", "job", "title", "job_title", "نقش", "سمت", "عنوان شغلی", "جایگاه", "مسئولیت" },
        ["title"] = new[] { "title", "subject", "heading", "name", "caption", "عنوان", "موضوع", "تیتر" },
        ["status"] = new[] { "status", "state", "condition", "وضعیت", "حالت", "شرایط" },
        ["description"] = new[] { "description", "desc", "details", "note", "notes", "comment", "comments", "explanation", "info", "توضیحات", "شرح", "توضیح", "یادداشت" },
        ["quantity"] = new[] { "quantity", "qty", "count", "number", "num", "تعداد", "مقدار", "شمار" },
        ["username"] = new[] { "username", "user_name", "user", "login", "نام کاربری", "کاربری", "شناسه کاربری" },
        ["password"] = new[] { "password", "pass", "pwd", "secret", "رمز عبور", "کلمه عبور", "رمز" },
        ["active"] = new[] { "active", "is_active", "enabled", "is_enabled", "فعال", "وضعیت فعال" },
        ["company"] = new[] { "company", "organization", "firm", "شرکت", "سازمان", "موسسه" },
        ["department"] = new[] { "department", "dept", "section", "واحد", "بخش", "دپارتمان" },
        ["course"] = new[] { "course", "lesson", "subject", "دوره", "درس", "رشته" },
    };

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

    [ObservableProperty]
    private string _filterMode = "All"; // "All", "Mapped", "Unmapped", "Required"

    public ObservableCollection<ImportColumnMappingItem> ColumnMappings { get; } = new();
    public ObservableCollection<ImportColumnMappingItem> FilteredColumnMappings { get; } = new();
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
    private int _unmappedColumnsCount;

    [ObservableProperty]
    private int _requiredColumnsCount;

    [ObservableProperty]
    private int _missingRequiredCount;

    [ObservableProperty]
    private int _mappedPercentage;

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
    public void SetFilter(string filter)
    {
        FilterMode = filter;
        ApplyFilter();
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

            for (int i = 0; i < cols.Count; i++)
            {
                var col = cols[i];
                var item = new ImportColumnMappingItem
                {
                    TargetColumnName = col.ColumnName,
                    DataType = col.DataType,
                    IsPrimaryKey = col.IsPk,
                    IsRequired = col.IsRequired,
                    TargetColumnIndex = i,
                    OnMappingChanged = () =>
                    {
                        UpdateMappingCounts();
                        ApplyFilter();
                        if (IsImportPreviewTab) RefreshMappedPreview();
                    },
                    ResolveSampleValueCallback = colOption => ResolveSampleValue(colOption)
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
                ApplyFilter();
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Failed to load table schema: {ex.Message}";
            HasStatusMessage = true;
            IsSuccess = false;
        }
    }

    private string ResolveSampleValue(string colOption)
    {
        if (string.IsNullOrWhiteSpace(colOption) || colOption == ImportColumnMappingItem.SkipOption)
            return "(will be skipped / NULL)";
        if (colOption == ImportColumnMappingItem.ConstantOption)
            return "(fixed constant)";

        int colIdx = ExtractExcelColumnIndex(colOption);
        if (colIdx >= 0 && AllRawRows.Count > 0 && colIdx < AllRawRows[0].Count)
        {
            string val = AllRawRows[0][colIdx];
            return string.IsNullOrEmpty(val) ? "(empty cell)" : $"Sample: \"{val}\"";
        }
        return "(no sample)";
    }

    private void PopulateColumnDropdown(ImportColumnMappingItem item)
    {
        item.AvailableExcelColumns.Clear();
        item.AvailableExcelColumns.Add(ImportColumnMappingItem.SkipOption);
        item.AvailableExcelColumns.Add(ImportColumnMappingItem.ConstantOption);

        for (int i = 0; i < ExcelHeaders.Count; i++)
        {
            string letter = ExcelDataService.IndexToColumnLetter(i);
            string headerText = ExcelHeaders[i];
            string sampleHint = "";
            if (AllRawRows.Count > 0 && i < AllRawRows[0].Count && !string.IsNullOrWhiteSpace(AllRawRows[0][i]))
            {
                string sampleVal = AllRawRows[0][i].Trim();
                if (sampleVal.Length > 20) sampleVal = sampleVal.Substring(0, 18) + "..";
                sampleHint = $" (e.g. \"{sampleVal}\")";
            }
            item.AvailableExcelColumns.Add($"{letter}: {headerText}{sampleHint}");
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

        // Collect all scored candidate pairs between target columns and Excel headers
        var candidates = new List<(ImportColumnMappingItem Target, int ExcelIndex, int Score, string MatchType)>();

        for (int tIdx = 0; tIdx < ColumnMappings.Count; tIdx++)
        {
            var target = ColumnMappings[tIdx];
            for (int eIdx = 0; eIdx < ExcelHeaders.Count; eIdx++)
            {
                var (score, matchType) = CalculateMatchScore(target.TargetColumnName, ExcelHeaders[eIdx], tIdx, eIdx);
                if (score > 0)
                {
                    candidates.Add((target, eIdx, score, matchType));
                }
            }
        }

        // Sort candidates descending by score to assign highest confidence links first
        candidates = candidates.OrderByDescending(c => c.Score).ToList();

        var assignedTargets = new HashSet<ImportColumnMappingItem>();
        var assignedExcelIndices = new HashSet<int>();

        foreach (var c in candidates)
        {
            if (assignedTargets.Contains(c.Target) || assignedExcelIndices.Contains(c.ExcelIndex))
                continue;

            string letter = ExcelDataService.IndexToColumnLetter(c.ExcelIndex);
            string matchingOption = c.Target.AvailableExcelColumns.FirstOrDefault(opt => opt.StartsWith($"{letter}:"))
                                    ?? $"{letter}: {ExcelHeaders[c.ExcelIndex]}";

            c.Target.SelectedExcelColumn = matchingOption;
            c.Target.MatchQuality = c.MatchType;
            c.Target.UpdateSamplePreview();

            assignedTargets.Add(c.Target);
            assignedExcelIndices.Add(c.ExcelIndex);
        }

        // Remaining unassigned columns stay Skipped
        foreach (var target in ColumnMappings)
        {
            if (!assignedTargets.Contains(target))
            {
                target.SelectedExcelColumn = ImportColumnMappingItem.SkipOption;
                target.MatchQuality = "Unmapped";
                target.UpdateSamplePreview();
            }
        }

        UpdateMappingCounts();
        ApplyFilter();
        RefreshMappedPreview();
    }

    [RelayCommand]
    public void MapByPosition()
    {
        if (!HasFileLoaded || ColumnMappings.Count == 0 || ExcelHeaders.Count == 0) return;

        for (int i = 0; i < ColumnMappings.Count; i++)
        {
            var target = ColumnMappings[i];
            if (i < ExcelHeaders.Count)
            {
                string letter = ExcelDataService.IndexToColumnLetter(i);
                string matchingOption = target.AvailableExcelColumns.FirstOrDefault(opt => opt.StartsWith($"{letter}:"))
                                        ?? $"{letter}: {ExcelHeaders[i]}";
                target.SelectedExcelColumn = matchingOption;
                target.MatchQuality = "1:1 Position";
            }
            else
            {
                target.SelectedExcelColumn = ImportColumnMappingItem.SkipOption;
                target.MatchQuality = "Unmapped";
            }
            target.UpdateSamplePreview();
        }

        UpdateMappingCounts();
        ApplyFilter();
        RefreshMappedPreview();
    }

    [RelayCommand]
    public void ClearMappings()
    {
        foreach (var m in ColumnMappings)
        {
            m.SelectedExcelColumn = ImportColumnMappingItem.SkipOption;
            m.MatchQuality = "Unmapped";
            m.UpdateSamplePreview();
        }
        UpdateMappingCounts();
        ApplyFilter();
        RefreshMappedPreview();
    }

    public void ApplyFilter()
    {
        FilteredColumnMappings.Clear();
        var source = FilterMode switch
        {
            "Mapped" => ColumnMappings.Where(m => m.IsMapped),
            "Unmapped" => ColumnMappings.Where(m => m.IsSkipped),
            "Required" => ColumnMappings.Where(m => m.IsRequired),
            _ => ColumnMappings
        };

        foreach (var item in source)
        {
            FilteredColumnMappings.Add(item);
        }
    }

    public void UpdateMappingCounts()
    {
        MappedColumnsCount = ColumnMappings.Count(m => m.IsMapped);
        TotalTableColumnsCount = ColumnMappings.Count;
        UnmappedColumnsCount = ColumnMappings.Count(m => m.IsSkipped);
        RequiredColumnsCount = ColumnMappings.Count(m => m.IsRequired);
        MissingRequiredCount = ColumnMappings.Count(m => m.HasWarning);
        MappedPercentage = TotalTableColumnsCount > 0
            ? (int)Math.Round((double)MappedColumnsCount / TotalTableColumnsCount * 100.0)
            : 0;

        OnPropertyChanged(nameof(MappedColumnsCount));
        OnPropertyChanged(nameof(TotalTableColumnsCount));
        OnPropertyChanged(nameof(UnmappedColumnsCount));
        OnPropertyChanged(nameof(RequiredColumnsCount));
        OnPropertyChanged(nameof(MissingRequiredCount));
        OnPropertyChanged(nameof(MappedPercentage));
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

    private static (int Score, string MatchType) CalculateMatchScore(string targetCol, string excelHeader, int targetIndex, int excelIndex)
    {
        if (string.IsNullOrWhiteSpace(targetCol) || string.IsNullOrWhiteSpace(excelHeader))
            return (0, "None");

        // 1. Exact string match (case-insensitive)
        if (targetCol.Equals(excelHeader, StringComparison.OrdinalIgnoreCase))
            return (100, "Exact Match");

        string normTarget = NormalizeName(targetCol);
        string normExcel = NormalizeName(excelHeader);

        // 2. Normalized alphanumeric match (ignoring underscores, spaces, dashes)
        if (normTarget == normExcel && !string.IsNullOrEmpty(normTarget))
            return (95, "Exact Match");

        // 3. Synonym dictionary matching (English & Persian)
        foreach (var (key, synonyms) in CommonSynonyms)
        {
            string normKey = NormalizeName(key);
            bool targetMatches = normTarget == normKey || synonyms.Any(s => NormalizeName(s) == normTarget);
            bool excelMatches = normExcel == normKey || synonyms.Any(s => NormalizeName(s) == normExcel);

            if (targetMatches && excelMatches)
            {
                return (90, "Smart Match");
            }
        }

        // 4. Word boundary / Token overlap (minimum word length 3 chars)
        var targetTokens = SplitWords(targetCol);
        var excelTokens = SplitWords(excelHeader);
        var commonTokens = targetTokens.Intersect(excelTokens, StringComparer.OrdinalIgnoreCase)
                                       .Where(t => t.Length >= 3)
                                       .ToList();

        if (commonTokens.Count > 0)
        {
            return (75 + Math.Min(commonTokens.Count * 5, 10), "Token Match");
        }

        // 5. Prefix / Suffix match for longer words (length >= 4)
        if (normTarget.Length >= 4 && normExcel.Length >= 4)
        {
            if (normExcel.StartsWith(normTarget) || normExcel.EndsWith(normTarget) ||
                normTarget.StartsWith(normExcel) || normTarget.EndsWith(normExcel))
            {
                return (65, "Prefix Match");
            }
        }

        return (0, "None");
    }

    private static List<string> SplitWords(string input)
    {
        if (string.IsNullOrWhiteSpace(input)) return new List<string>();
        var spaced = Regex.Replace(input, "([a-z])([A-Z])", "$1 $2");
        return Regex.Split(spaced, @"[\s_\-\[\]""]+")
                    .Where(w => !string.IsNullOrWhiteSpace(w))
                    .Select(w => w.ToLowerInvariant())
                    .ToList();
    }

    private static string NormalizeName(string name)
    {
        if (string.IsNullOrEmpty(name)) return string.Empty;
        string n = name.Replace("ي", "ی").Replace("ك", "ک");
        return Regex.Replace(n, @"[\s_\-\[\]""]", "").ToLowerInvariant();
    }
}
