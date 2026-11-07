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
    [NotifyPropertyChangedFor(nameof(IsResultsTabSelected))]
    [NotifyPropertyChangedFor(nameof(IsTerminalTabSelected))]
    private string _selectedTab = "Results";

    public bool IsResultsTabSelected => string.Equals(SelectedTab, "Results", StringComparison.OrdinalIgnoreCase);
    public bool IsTerminalTabSelected => string.Equals(SelectedTab, "Terminal", StringComparison.OrdinalIgnoreCase);

    public ObservableCollection<string> Tabs { get; } = new();
    public ObservableCollection<FooterTabItem> TabItems { get; } = new();

    public FooterViewModel()
    {
        Tabs.CollectionChanged += (s, e) => SyncTabItems();
        SyncTabItems();
    }

    partial void OnSelectedTabChanged(string value)
    {
        UpdateActiveStates();
    }

    private void SyncTabItems()
    {
        TabItems.Clear();
        foreach (var tab in Tabs)
        {
            TabItems.Add(new FooterTabItem
            {
                Name = tab,
                Icon = GetTabIcon(tab),
                IsActive = string.Equals(tab, SelectedTab, StringComparison.OrdinalIgnoreCase)
            });
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
            "results" => "Document",
            _ => "Document"
        };
    }

    public event Action? TerminalTabActivated;

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = tabName;
        if (IsTerminalTabSelected)
        {
            TerminalTabActivated?.Invoke();
        }
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
