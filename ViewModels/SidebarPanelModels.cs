using System;
using System.Collections.ObjectModel;
using System.Globalization;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;

namespace Quell.ViewModels;

public partial class SavedQueryItemViewModel : ObservableObject
{
    public SavedQueryRecord Record { get; }

    public int Id => Record.Id;

    [ObservableProperty]
    private string _title = string.Empty;

    [ObservableProperty]
    private string _queryText = string.Empty;

    [ObservableProperty]
    private string _folderName = string.Empty;

    [ObservableProperty]
    private bool _isPinned;

    [ObservableProperty]
    private bool _isSelected;

    [ObservableProperty]
    private bool _isRenaming;

    [ObservableProperty]
    private string _editingTitle = string.Empty;

    public SavedQueryItemViewModel(SavedQueryRecord record)
    {
        Record = record;
        _title = record.Title;
        _queryText = record.QueryText;
        _folderName = record.FolderName;
        _isPinned = record.IsPinned;
        _editingTitle = record.Title;
    }
}

public partial class SavedQueryFolderViewModel : ObservableObject
{
    [ObservableProperty]
    private string _name = string.Empty;

    [ObservableProperty]
    private bool _isExpanded = true;

    [ObservableProperty]
    private bool _isDragOver;

    public int Count => Items.Count;
    public string CountText => Items.Count.ToString();

    public ObservableCollection<SavedQueryItemViewModel> Items { get; } = new();

    public SavedQueryFolderViewModel(string name, bool isExpanded = true)
    {
        _name = name;
        _isExpanded = isExpanded;
    }

    [RelayCommand]
    public void ToggleExpand()
    {
        IsExpanded = !IsExpanded;
    }

    public void NotifyCountChanged()
    {
        OnPropertyChanged(nameof(Count));
        OnPropertyChanged(nameof(CountText));
    }
}

public partial class HistoryEntryViewModel : ObservableObject
{
    public QueryHistoryRecord Record { get; }

    public int Id => Record.Id;
    public string QueryText => Record.QueryText;
    public string ExecutionTime => Record.ExecutionTime;
    public int RowCount => Record.RowCount;
    public string Status => Record.Status;
    public string DatabaseName => Record.DatabaseName;

    public bool IsFailed => Status.Equals("Failed", StringComparison.OrdinalIgnoreCase) ||
                            Status.Equals("Error", StringComparison.OrdinalIgnoreCase);

    public bool IsSaved => Status.Equals("Saved", StringComparison.OrdinalIgnoreCase);
    public bool IsSuccess => !IsFailed && !IsSaved;

    public string StatusLabel => IsFailed ? "● Failed" : (IsSaved ? "● Saved" : "● OK");
    public string StatusColor => IsFailed ? "#F2677A" : (IsSaved ? "#8FA8FF" : "#3DD68C");

    public string RowCountText => IsFailed
        ? string.Empty
        : (RowCount == 1 ? "1 row" : $"{RowCount:N0} rows");

    public bool HasRowCount => !IsFailed && !IsSaved;

    public string FormattedTime { get; }
    public string DateGroup { get; }

    [ObservableProperty]
    private bool _isSelected;

    public HistoryEntryViewModel(QueryHistoryRecord record)
    {
        Record = record;

        if (DateTime.TryParse(record.ExecutedAt, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out var utcDt))
        {
            var localDt = utcDt.ToLocalTime();
            FormattedTime = localDt.ToString("HH:mm");
            var today = DateTime.Today;
            if (localDt.Date == today)
            {
                DateGroup = "TODAY";
            }
            else if (localDt.Date == today.AddDays(-1))
            {
                DateGroup = "YESTERDAY";
            }
            else
            {
                DateGroup = "EARLIER";
            }
        }
        else
        {
            FormattedTime = record.ExecutedAt;
            DateGroup = "TODAY";
        }
    }
}

public class HistoryDateGroupViewModel
{
    public string GroupTitle { get; }
    public ObservableCollection<HistoryEntryViewModel> Items { get; } = new();

    public HistoryDateGroupViewModel(string title)
    {
        GroupTitle = title;
    }
}
