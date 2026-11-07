using System.Collections.ObjectModel;
using System.Linq;
using AvaloniaEdit.Document;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quell.ViewModels;

public partial class EditorTabItem : ObservableObject
{
    [ObservableProperty]
    private string _title = "New Query.sql";

    [ObservableProperty]
    private bool _isActive;

    [ObservableProperty]
    private string _selectedText = string.Empty;

    private string _rawText = string.Empty;

    public TextDocument Document { get; }

    public string Text
    {
        get
        {
            try
            {
                return Document.Text;
            }
            catch
            {
                return _rawText;
            }
        }
        set
        {
            _rawText = value ?? string.Empty;
            SelectedText = string.Empty;
            try
            {
                if (Document.Text != value)
                {
                    Document.Text = value ?? string.Empty;
                    OnPropertyChanged();
                }
            }
            catch
            {
                OnPropertyChanged();
            }
        }
    }

    public EditorTabItem() : this("New Query.sql", string.Empty) { }

    public EditorTabItem(string title, string initialText)
    {
        _title = title;
        _rawText = initialText ?? string.Empty;
        Document = new TextDocument(initialText);
    }
}

public partial class EditorViewModel : ViewModelBase
{
    public ObservableCollection<EditorTabItem> Tabs { get; } = new()
    {
        new EditorTabItem("Query 1.sql", "-- SQL Query Editor\nSELECT * FROM dbo.Users;\n\n-- Type query and press F5 to execute\n"),
        new EditorTabItem("Query 2.sql", "-- Secondary Analysis\nSELECT COUNT(*) AS TotalUsers, Role FROM dbo.Users GROUP BY Role;\n")
    };

    [ObservableProperty]
    private EditorTabItem? _selectedTab;

    /// <summary>
    /// Optional live delegate returning the active editor's highlighted/selected text (e.g. from AvaloniaEdit.TextEditor.SelectedText).
    /// </summary>
    public System.Func<string>? GetActiveSelection { get; set; }

    /// <summary>
    /// Resolves the SQL query to execute: returns the selected/highlighted text if any;
    /// otherwise falls back to the full text of the currently active editor tab.
    /// </summary>
    public string GetQueryToExecute()
    {
        var selection = GetActiveSelection?.Invoke();
        if (!string.IsNullOrWhiteSpace(selection))
        {
            return selection.Trim();
        }

        if (!string.IsNullOrWhiteSpace(SelectedTab?.SelectedText))
        {
            return SelectedTab.SelectedText.Trim();
        }

        string full = SelectedTab?.Text ?? string.Empty;
        return !string.IsNullOrWhiteSpace(full) ? full : "SELECT * FROM dbo.Users;";
    }

    public EditorViewModel()
    {
        if (Tabs.Count > 0)
        {
            SelectTab(Tabs[0]);
        }
    }

    [RelayCommand]
    public void SelectTab(EditorTabItem tab)
    {
        foreach (var t in Tabs)
        {
            t.IsActive = false;
        }

        tab.IsActive = true;
        SelectedTab = tab;
    }

    [RelayCommand]
    public void AddTab()
    {
        int nextIndex = Tabs.Count + 1;
        var newTab = new EditorTabItem
        (
            $"Query {nextIndex}.sql",
            $"-- Query {nextIndex}\nSELECT * FROM dbo.Users;\n"
        );

        Tabs.Add(newTab);
        SelectTab(newTab);
    }

    [RelayCommand]
    public void CloseTab(EditorTabItem tab)
    {
        if (Tabs.Contains(tab))
        {
            bool wasActive = tab.IsActive;
            Tabs.Remove(tab);
            if (Tabs.Count == 0)
            {
                AddTab();
            }
            else if (wasActive)
            {
                SelectTab(Tabs.Last());
            }
        }
    }

    [RelayCommand]
    public void CloseOtherTabs(EditorTabItem keepTab)
    {
        var toRemove = Tabs.Where(t => t != keepTab).ToList();
        foreach (var tab in toRemove)
        {
            Tabs.Remove(tab);
        }
        SelectTab(keepTab);
    }

    [RelayCommand]
    public void CloseAllTabs()
    {
        Tabs.Clear();
        AddTab();
    }
}
