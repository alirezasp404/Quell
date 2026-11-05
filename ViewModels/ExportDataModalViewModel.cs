using System.Collections.ObjectModel;
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
    private string _selectedFormat = "CSV (Comma Separated Values)";

    public ObservableCollection<string> ExportFormats { get; } = new()
    {
        "CSV (Comma Separated Values)",
        "JSON (JavaScript Object Notation)",
        "SQL (INSERT INTO statements)",
        "TSV (Tab Separated Values)"
    };

    [ObservableProperty]
    private bool _includeHeaders = true;

    [ObservableProperty]
    private string _delimiter = ",";

    [ObservableProperty]
    private string _filePath = "C:\\Users\\ehsan\\Desktop\\export_results.csv";

    [RelayCommand]
    public void Open()
    {
        IsOpen = true;
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
    }

    [RelayCommand]
    public void Export()
    {
        // Ready for streaming export execution
        IsOpen = false;
    }

    [RelayCommand]
    public async Task BrowseFile(TopLevel? topLevel)
    {
        if (topLevel?.StorageProvider == null) return;

        var file = await topLevel.StorageProvider.SaveFilePickerAsync(new FilePickerSaveOptions
        {
            Title = "Save Export File",
            SuggestedFileName = "export_results",
            DefaultExtension = "csv"
        });

        if (file != null)
        {
            FilePath = file.Path.LocalPath;
        }
    }
}
