using System;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;

namespace Quell.ViewModels;

public partial class ConfirmActionModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    private string _title = "Confirm Action";

    [ObservableProperty]
    private string _message = "Are you sure you want to proceed with this operation?";

    [ObservableProperty]
    private string? _targetName;

    [ObservableProperty]
    private bool _isDestructive = true;

    [ObservableProperty]
    private string _confirmButtonText = "Confirm";

    public Action? OnConfirmed { get; set; }

    public void Show(string title, string message, string? targetName = null, bool isDestructive = true, string confirmText = "Confirm", Action? onConfirmed = null)
    {
        Title = title;
        Message = message;
        TargetName = targetName;
        IsDestructive = isDestructive;
        ConfirmButtonText = confirmText;
        OnConfirmed = onConfirmed;
        IsOpen = true;
    }

    [RelayCommand]
    public void Confirm()
    {
        IsOpen = false;
        OnConfirmed?.Invoke();
    }

    [RelayCommand]
    public void Cancel()
    {
        IsOpen = false;
    }
}
