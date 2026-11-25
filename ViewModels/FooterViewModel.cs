using System;
using System.Collections.ObjectModel;
using System.Linq;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quell.ViewModels;

public partial class FooterTabItem : ObservableObject
{
    public string Name { get; set; } = string.Empty;
    public string Icon { get; set; } = "Document";
    public bool IsResults => string.Equals(Name, "Results", StringComparison.OrdinalIgnoreCase);
public bool IsTerminal => string.Equals(Name, "Terminal", StringComparison.OrdinalIgnoreCase);
    public bool IsSchema => string.Equals(Name, "Schema", StringComparison.OrdinalIgnoreCase);

    [ObservableProperty]
    private bool _isActive;
}

public partial class FooterViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isMaximized;

    [ObservableProperty]
    private QueryResultTableViewModel? _queryResult;

    [ObservableProperty]
    private SchemaPropertiesTableViewModel? _schema;

    [ObservableProperty]
    private TerminalViewModel? _terminal;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsResultsTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsTerminalTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsSchemaTabSelected))]
    private string _selectedTab = "Results";

    public bool IsResultsTabSelected => string.Equals(SelectedTab, "Results", StringComparison.OrdinalIgnoreCase);
    public bool IsTerminalTabSelected => string.Equals(SelectedTab, "Terminal", StringComparison.OrdinalIgnoreCase);
    public bool IsSchemaTabSelected => string.Equals(SelectedTab, "Schema", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<string> Tabs { get; } = new()
    {
        "Results",
        "Terminal"
    };
    public ObservableCollection<FooterTabItem> TabItems { get; } = new();

    public FooterViewModel()
    {
        Tabs.CollectionChanged += (s, e) => SyncTabItems();
        SyncTabItems();
    }

    partial void OnSelectedTabChanged(string value)
    {
        UpdateActiveStates();
        if (IsTerminalTabSelected)
        {
            TerminalTabActivated?.Invoke();
        }
    }

    private void SyncTabItems()
    {
        var existingMap = TabItems.ToDictionary(t => t.Name, StringComparer.OrdinalIgnoreCase);
        var newSet = new System.Collections.Generic.HashSet<string>(Tabs, StringComparer.OrdinalIgnoreCase);

        for (int i = TabItems.Count - 1; i >= 0; i--)
        {
            if (!newSet.Contains(TabItems[i].Name))
            {
                TabItems.RemoveAt(i);
            }
        }

        for (int i = 0; i < Tabs.Count; i++)
        {
            var tab = Tabs[i];
            if (!existingMap.TryGetValue(tab, out var item))
            {
                item = new FooterTabItem
                {
                    Name = tab,
                    Icon = GetTabIcon(tab),
                    IsActive = string.Equals(tab, SelectedTab, StringComparison.OrdinalIgnoreCase)
                };
                TabItems.Insert(Math.Min(i, TabItems.Count), item);
            }
            else
            {
                item.IsActive = string.Equals(tab, SelectedTab, StringComparison.OrdinalIgnoreCase);
                int currentIndex = TabItems.IndexOf(item);
                if (currentIndex != i && currentIndex >= 0)
                {
                    TabItems.Move(currentIndex, i);
                }
            }
        }
    }

    private void UpdateActiveStates()
    {
        foreach (var item in TabItems)
        {
            item.IsActive = string.Equals(item.Name, SelectedTab, StringComparison.OrdinalIgnoreCase);
        }
    }

    private static string GetTabIcon(string tabName)
    {
        return tabName.ToLowerInvariant() switch
        {
            "terminal" => "Play",
            "schema" => "Tag",
            "results" => "Document",
            _ => "Document"
        };
    }

    public event Action? TerminalTabActivated;

    [RelayCommand]
    public void SelectTab(string tabName)
    {
        if (string.Equals(SelectedTab, tabName, StringComparison.OrdinalIgnoreCase))
        {
            if (IsTerminalTabSelected)
            {
                TerminalTabActivated?.Invoke();
            }
            return;
        }

        SelectedTab = tabName;
    }

    [RelayCommand]
    public void SwitchTab()
    {
        if (Tabs.Count <= 1) return;
        int currentIndex = Tabs.IndexOf(SelectedTab);
        if (currentIndex < 0) currentIndex = 0;
        int nextIndex = (currentIndex + 1) % Tabs.Count;
        SelectTab(Tabs[nextIndex]);
    }

    [RelayCommand]
    private void ToggleVisibility()
    {
        IsVisible = !IsVisible;
        if (!IsVisible)
        {
            IsMaximized = false;
        }
    }

    [RelayCommand]
    public void ToggleMaximize()
    {
        IsMaximized = !IsMaximized;
        if (IsMaximized)
        {
            IsVisible = true;
        }
    }

    [RelayCommand]
    private void ClosePanel()
    {
        IsMaximized = false;
        IsVisible = false;
    }

    [RelayCommand]
    private void CloseTab(string tabName)
    {
        if (Tabs.Contains(tabName))
        {
            Tabs.Remove(tabName);
            if (Tabs.Count == 0)
            {
                IsVisible = false;
            }
            else if (SelectedTab == tabName)
            {
                SelectedTab = Tabs.First();
            }
        }
    }

    [RelayCommand]
    private void CloseOtherTabs(string keepTab)
    {
        var toRemove = Tabs.Where(t => t != keepTab).ToList();
        foreach (var t in toRemove)
        {
            Tabs.Remove(t);
        }
        SelectedTab = keepTab;
    }
}
