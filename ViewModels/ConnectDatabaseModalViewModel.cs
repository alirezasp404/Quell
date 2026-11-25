using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading.Tasks;
using Avalonia.Controls;
using Avalonia.Platform.Storage;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Services;
using Quell.Services.DatabaseProviders;

namespace Quell.ViewModels;

public class DatabaseEngineItem : ObservableObject
{
    public DatabaseType Id { get; init; } = DatabaseType.MSSQL;
    public string Name { get; init; } = "SQL Server";
    public string Badge { get; init; } = "MSSQL";
    public string DefaultPortText { get; init; } = "1433";
    public string BadgeBackground { get; init; } = "#2563EB";

    private bool _isSelected;
    public bool IsSelected
    {
        get => _isSelected;
        set => SetProperty(ref _isSelected, value);
    }
}

public partial class ConnectDatabaseModalViewModel : ViewModelBase
{
    [ObservableProperty]
    private bool _isOpen;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsEditMode))]
    [NotifyPropertyChangedFor(nameof(ModalTitle))]
    [NotifyPropertyChangedFor(nameof(ConnectButtonText))]
    private int? _editingConnectionId;

    public bool IsEditMode => EditingConnectionId.HasValue && EditingConnectionId.Value > 0;
    public string ModalTitle => IsEditMode ? "Edit Database Connection" : "Connect to Database";
    public string ConnectButtonText => IsEditMode ? "Save & Connect" : "Connect";

    [ObservableProperty]
    private DatabaseType _selectedDatabaseType = DatabaseType.MSSQL;

    public ObservableCollection<DatabaseEngineItem> EngineOptions { get; } = new()
    {
        new DatabaseEngineItem { Id = DatabaseType.MSSQL, Name = "SQL Server", Badge = "MSSQL", DefaultPortText = "1433", BadgeBackground = "#2563EB", IsSelected = true },
        new DatabaseEngineItem { Id = DatabaseType.PostgreSQL, Name = "PostgreSQL", Badge = "PG", DefaultPortText = "5432", BadgeBackground = "#336791", IsSelected = false },
        new DatabaseEngineItem { Id = DatabaseType.MySQL, Name = "MySQL", Badge = "MYSQL", DefaultPortText = "3306", BadgeBackground = "#00758F", IsSelected = false },
        new DatabaseEngineItem { Id = DatabaseType.SQLite, Name = "SQLite", Badge = "SQLITE", DefaultPortText = "File", BadgeBackground = "#003B57", IsSelected = false }
    };

    public ObservableCollection<ConnectionRecord> SavedConnections { get; } = new();

    public event Action<ConnectionRecord>? ConnectionDeleted;

    [ObservableProperty]
    private ConnectionRecord? _selectedSavedConnection;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreviewSummary))]
    private string _server = "localhost";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreviewSummary))]
    private int _port = 1433;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreviewSummary))]
    private string _database = "master";

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(ConnectionPreviewSummary))]
    private string _filePath = string.Empty;

    [ObservableProperty]
    private string _authType = "SQL Server Authentication";

    public ObservableCollection<string> AuthTypes { get; } = new()
    {
        "SQL Server Authentication",
        "Windows Authentication",
        "Microsoft Entra (Azure AD)"
    };

    [ObservableProperty]
    private string _username = "sa";

    [ObservableProperty]
    private string _password = string.Empty;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsPasswordHidden))]
    private bool _isPasswordRevealed;

    public bool IsPasswordHidden => !IsPasswordRevealed;

    [ObservableProperty]
    private bool _trustServerCertificate = true;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusSuccess))]
    [NotifyPropertyChangedFor(nameof(IsStatusError))]
    [NotifyPropertyChangedFor(nameof(IsStatusInfo))]
    private string? _statusMessage;

    [ObservableProperty]
    [NotifyPropertyChangedFor(nameof(IsStatusSuccess))]
    [NotifyPropertyChangedFor(nameof(IsStatusError))]
    [NotifyPropertyChangedFor(nameof(IsStatusInfo))]
    private bool? _isSuccess;

    public bool IsStatusSuccess => IsSuccess == true;
    public bool IsStatusError => IsSuccess == false;
    public bool IsStatusInfo => IsSuccess == null && !string.IsNullOrWhiteSpace(StatusMessage);

    public bool HasSavedConnections => SavedConnections.Count > 0;

    [ObservableProperty]
    private bool _isTesting;

    [ObservableProperty]
    private bool _isConnecting;

    public bool IsFileBased => SelectedDatabaseType == DatabaseType.SQLite;
    public bool IsServerBased => SelectedDatabaseType != DatabaseType.SQLite;
    public bool SupportsWindowsAuth => SelectedDatabaseType == DatabaseType.MSSQL;
    public bool SupportsTrustCertificate => SelectedDatabaseType == DatabaseType.MSSQL;
    public bool IsCredentialsEnabled => AuthType != "Windows Authentication" || IsFileBased;

    public string ConnectionPreviewSummary
    {
        get
        {
            if (IsFileBased)
            {
                string pathDisplay = string.IsNullOrWhiteSpace(FilePath) ? ":memory:" : System.IO.Path.GetFileName(FilePath.Trim());
                return $"SQLite  •  {pathDisplay}";
            }

            string hostDisplay = string.IsNullOrWhiteSpace(Server) ? "localhost" : Server.Trim();
            string dbDisplay = string.IsNullOrWhiteSpace(Database) ? "default" : Database.Trim();
            string engineTag = SelectedDatabaseType switch
            {
                DatabaseType.MSSQL => "MSSQL",
                DatabaseType.PostgreSQL => "PostgreSQL",
                DatabaseType.MySQL => "MySQL",
                _ => SelectedDatabaseType.ToString()
            };
            return $"{engineTag}  •  {hostDisplay}:{Port}  •  {dbDisplay}";
        }
    }

    public string ModalSubtitle => SelectedDatabaseType switch
    {
        DatabaseType.MSSQL => "Configure Microsoft SQL Server / Azure SQL connection",
        DatabaseType.PostgreSQL => "Configure PostgreSQL instance connection",
        DatabaseType.MySQL => "Configure MySQL / MariaDB instance connection",
        DatabaseType.SQLite => "Configure local SQLite embedded database file",
        _ => "Configure database connection"
    };

    [RelayCommand]
    public void SelectEngine(DatabaseType engineId)
    {
        SelectedDatabaseType = engineId;
        foreach (var opt in EngineOptions)
        {
            opt.IsSelected = opt.Id == engineId;
        }

        var provider = DatabaseProviderRegistry.Instance.GetProvider(engineId);
        Port = provider.DefaultPort;
        Database = provider.DefaultDatabase;
        if (!string.IsNullOrEmpty(provider.DefaultUsername))
        {
            Username = provider.DefaultUsername;
        }

        StatusMessage = null;
        IsSuccess = null;

        OnPropertyChanged(nameof(IsFileBased));
        OnPropertyChanged(nameof(IsServerBased));
        OnPropertyChanged(nameof(SupportsWindowsAuth));
        OnPropertyChanged(nameof(SupportsTrustCertificate));
        OnPropertyChanged(nameof(IsCredentialsEnabled));
        OnPropertyChanged(nameof(ModalSubtitle));
        OnPropertyChanged(nameof(ConnectionPreviewSummary));
    }

    [RelayCommand]
    public void TogglePasswordVisibility()
    {
        IsPasswordRevealed = !IsPasswordRevealed;
    }

    [RelayCommand]
    public void UseInMemorySqlite()
    {
        FilePath = ":memory:";
    }

    [RelayCommand]
    public async Task BrowseSqliteFileAsync(TopLevel? topLevel)
    {
        if (topLevel?.StorageProvider == null) return;

        var files = await topLevel.StorageProvider.OpenFilePickerAsync(new FilePickerOpenOptions
        {
            Title = "Select SQLite Database File",
            AllowMultiple = false,
            FileTypeFilter = new List<FilePickerFileType>
            {
                new("SQLite Databases (*.db, *.sqlite, *.sqlite3)")
                {
                    Patterns = new[] { "*.db", "*.sqlite", "*.sqlite3", "*.db3" }
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
        }
    }

    [RelayCommand]
    public void SelectSavedConnection(ConnectionRecord record)
    {
        if (record == null) return;
        SelectedSavedConnection = record;

        SelectEngine(record.DatabaseType);
        Server = record.Server;
        Port = record.Port;
        Database = record.DatabaseName;
        Password = !string.IsNullOrEmpty(record.Password) ? record.Password : string.Empty;
        if (!string.IsNullOrEmpty(record.AuthType))
        {
            AuthType = record.AuthType;
        }
        if (!string.IsNullOrEmpty(record.Username))
        {
            Username = record.Username;
        }
        if (record.DatabaseType == DatabaseType.SQLite)
        {
            FilePath = record.Server;
        }

        OnPropertyChanged(nameof(Server));
        OnPropertyChanged(nameof(Port));
        OnPropertyChanged(nameof(Database));
        OnPropertyChanged(nameof(Password));
        OnPropertyChanged(nameof(Username));
        OnPropertyChanged(nameof(AuthType));
        OnPropertyChanged(nameof(FilePath));
    }

    partial void OnAuthTypeChanged(string value)
    {
        OnPropertyChanged(nameof(IsCredentialsEnabled));
    }

    public async Task LoadSavedConnectionsAsync()
    {
        try
        {
            var conns = await DatabaseStorageService.Instance.GetRecentConnectionsAsync();
            SavedConnections.Clear();
            foreach (var c in conns.Where(c => !c.Server.Contains("quell", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("quell", StringComparison.OrdinalIgnoreCase) && !c.Server.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase) && !c.DatabaseName.Contains("fdb_studio", StringComparison.OrdinalIgnoreCase)))
            {
                SavedConnections.Add(c);
            }

            // Auto-prefill password if current form matches a saved connection
            if (!IsEditMode && SavedConnections.Count > 0)
            {
                var match = SavedConnections.FirstOrDefault(s =>
                    s.Server == Server && s.Port == Port && s.DatabaseName == Database && s.DatabaseType == SelectedDatabaseType)
                    ?? SavedConnections.FirstOrDefault(s => s.IsActive);

                if (match != null && !string.IsNullOrEmpty(match.Password) && string.IsNullOrEmpty(Password))
                {
                    Password = match.Password;
                    OnPropertyChanged(nameof(Password));
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[ConnectModal] LoadSavedConnections error: {ex.Message}");
        }
        finally
        {
            OnPropertyChanged(nameof(HasSavedConnections));
        }
    }

    [RelayCommand]
    public void Open()
    {
        EditingConnectionId = null;
        IsOpen = true;
        IsPasswordRevealed = false;
        StatusMessage = null;
        IsSuccess = null;
        _ = LoadSavedConnectionsAsync();
    }

    public void OpenForEdit(ConnectionRecord record)
    {
        if (record == null) return;
        EditingConnectionId = record.Id;
        SelectEngine(record.DatabaseType);
        Server = record.Server;
        Port = record.Port;
        Database = record.DatabaseName;
        Password = record.Password ?? string.Empty;
        IsPasswordRevealed = false;
        if (!string.IsNullOrEmpty(record.AuthType)) AuthType = record.AuthType;
        if (!string.IsNullOrEmpty(record.Username)) Username = record.Username;
        if (record.DatabaseType == DatabaseType.SQLite) FilePath = record.Server;
        StatusMessage = null;
        IsSuccess = null;
        IsOpen = true;
        _ = LoadSavedConnectionsAsync();
    }

    [RelayCommand]
    public void Close()
    {
        IsOpen = false;
        IsPasswordRevealed = false;
        StatusMessage = null;
        IsSuccess = null;
        IsTesting = false;
        IsConnecting = false;
    }

    public DatabaseConnectionConfig BuildCurrentConfig()
    {
        string actualServer = IsFileBased
            ? (string.IsNullOrWhiteSpace(FilePath) ? ":memory:" : FilePath.Trim())
            : (string.IsNullOrWhiteSpace(Server) ? "localhost" : Server.Trim());
        string actualDb = IsFileBased
            ? (string.IsNullOrWhiteSpace(Database) || Database == "master" || Database == "main"
                ? (actualServer == ":memory:" ? "memory" : System.IO.Path.GetFileNameWithoutExtension(actualServer))
                : Database)
            : (string.IsNullOrWhiteSpace(Database) ? "master" : Database.Trim());

        return new DatabaseConnectionConfig
        {
            DatabaseType = SelectedDatabaseType,
            Server = actualServer,
            Port = Port,
            Database = actualDb,
            AuthType = AuthType,
            Username = Username,
            Password = Password,
            TrustServerCertificate = TrustServerCertificate,
            FilePath = IsFileBased ? actualServer : FilePath,
            TimeoutSeconds = 8
        };
    }

    [RelayCommand]
    public async Task TestConnection()
    {
        StatusMessage = $"Connecting to {SelectedDatabaseType} ({Server}:{Port})...";
        IsTesting = true;
        IsSuccess = null;

        try
        {
            var config = BuildCurrentConfig();
            var provider = DatabaseProviderRegistry.Instance.GetProvider(SelectedDatabaseType);
            var result = await provider.TestConnectionAsync(config);

            if (result.Success)
            {
                StatusMessage = $"Connection succeeded! (Server version: {result.ServerVersion ?? "Microsoft SQL Server 2022"})";
                IsSuccess = true;
            }
            else
            {
                StatusMessage = $"Connection failed: {result.Message}";
                IsSuccess = false;
            }
        }
        catch (Exception ex)
        {
            StatusMessage = $"Connection failed: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsTesting = false;
        }
    }

    [RelayCommand]
    public async Task Connect()
    {
        IsConnecting = true;
        StatusMessage = $"Establishing connection to {SelectedDatabaseType}...";
        IsSuccess = null;

        try
        {
            var config = BuildCurrentConfig();

            // 1. Persist or update connection record in SQLite storage
            int connId;
            if (IsEditMode)
            {
                connId = EditingConnectionId!.Value;
                await DatabaseStorageService.Instance.UpdateConnectionAsync(
                    connId, config.Server, config.Port, config.Database, config.AuthType, config.Username, SelectedDatabaseType, Password);
            }
            else
            {
                connId = await DatabaseStorageService.Instance.SaveConnectionAsync(
                    config.Server, config.Port, config.Database, config.AuthType, config.Username, SelectedDatabaseType, Password);
            }

            // Reload saved connections
            await LoadSavedConnectionsAsync();

            config = config with { Id = connId };

            // 2. Establish live session with the selected provider
            var result = await DatabaseSessionService.Instance.ConnectAsync(config);
            if (!result.Success)
            {
                StatusMessage = $"Could not connect: {result.Message}";
                IsSuccess = false;
                return;
            }

            // 3. Switch dialect in SqlKeywordsService
            await SqlKeywordsService.Instance.LoadKeywordsFromDatabaseAsync(SelectedDatabaseType);

            StatusMessage = $"Connected to {SelectedDatabaseType} successfully!";
            IsSuccess = true;
            IsOpen = false;
        }
        catch (Exception ex)
        {
            StatusMessage = $"Error: {ex.Message}";
            IsSuccess = false;
        }
        finally
        {
            IsConnecting = false;
        }
    }

    [RelayCommand]
    public async Task DeleteSavedConnection(ConnectionRecord record)
    {
        if (record == null) return;
        string sessionKey = record.Id > 0 ? $"id_{record.Id}" : $"{record.DatabaseType}_{record.Server}_{record.Port}_{record.DatabaseName}";
        await DatabaseSessionService.Instance.DisconnectAsync(sessionKey);
        await DatabaseStorageService.Instance.DeleteConnectionAsync(record.Id);
        await LoadSavedConnectionsAsync();
        ConnectionDeleted?.Invoke(record);
    }
}
