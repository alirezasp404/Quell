using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.ObjectModel;
using System.Linq;

namespace Fdb.ViewModels;

public partial class FooterViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isVisible = true;

    [ObservableProperty]
    private bool _isMaximized;

    [ObservableProperty]
    private string _selectedTab = string.Empty;

    public ObservableCollection<string> Tabs { get; } = new();

    [RelayCommand]
    private void SelectTab(string tabName)
    {
        SelectedTab = tabName;
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
