using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Quell.Models;
using Quell.Services.DatabaseProviders;
using Quell.Services.Terminal;

namespace Quell.ViewModels;

public partial class TerminalViewModel : ViewModelBase
{
    private readonly ShellDetectorService _shellDetector = ShellDetectorService.Instance;
    private readonly DatabaseCliService _cliService = DatabaseCliService.Instance;
    private readonly TerminalSettingsService _settingsService = TerminalSettingsService.Instance;

    [ObservableProperty]
    private TerminalSessionViewModel? _activeSession;

    [ObservableProperty]
    private ShellProfile? _selectedShellProfile;

    [ObservableProperty]
    private string _activeConnectionName = string.Empty;

    [ObservableProperty]
    private bool _hasActiveConnection;

    [ObservableProperty]
    private string _activeConnectionBadge = string.Empty;

    [ObservableProperty]
    private TerminalSettingsModel _settings = new();

    public ObservableCollection<TerminalSessionViewModel> Sessions { get; } = new();
    public ObservableCollection<ShellProfile> AvailableShells { get; } = new();

    public event Action? RequestFocus;

    public TerminalViewModel()
    {
        InitializeShells();
        _settingsService.SettingsChanged += (_, s) =>
        {
            Settings = s;
            InitializeShells();
        };
        _ = LoadSettingsAsync();
    }

    private async Task LoadSettingsAsync()
    {
        Settings = await _settingsService.LoadSettingsAsync();
        InitializeShells();
    }

    public void InitializeShells()
    {
        AvailableShells.Clear();
        var defaultShell = _shellDetector.GetDefaultShell(Settings.DefaultShellId);
        SelectedShellProfile = defaultShell;

        var detected = _shellDetector.GetAvailableShells();
        foreach (var shell in detected)
        {
            shell.IsDefault = (shell.Id == defaultShell.Id);
            AvailableShells.Add(shell);
        }
    }

    public void UpdateConnectionContext(DatabaseConnectionConfig? config)
    {
        if (config != null)
        {
            HasActiveConnection = true;
            ActiveConnectionName = config.DisplayName;
            ActiveConnectionBadge = $"{config.DatabaseType}: {config.DisplayName}";
        }
        else
        {
            HasActiveConnection = false;
            ActiveConnectionName = string.Empty;
            ActiveConnectionBadge = string.Empty;
        }
    }

    [RelayCommand]
    public void NewSession(object? parameter = null)
    {
        var targetProfile = (parameter as ShellProfile) ?? SelectedShellProfile ?? _shellDetector.GetDefaultShell(Settings.DefaultShellId);
        var sessionCount = Sessions.Count(s => s.Title.StartsWith(targetProfile.DisplayName, StringComparison.OrdinalIgnoreCase)) + 1;

        var session = new TerminalSessionViewModel
        {
            Title = $"{targetProfile.DisplayName} ({sessionCount})",
            IconKey = targetProfile.IconKey,
            ExecutablePath = targetProfile.ExecutablePath,
            Arguments = targetProfile.Arguments,
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        // Inject active connection environment variables if any
        if (DatabaseSessionService.Instance.ActiveConfig != null)
        {
            var env = _cliService.BuildConnectionEnvironment(DatabaseSessionService.Instance.ActiveConfig);
            foreach (var kvp in env)
            {
                session.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }

        AddAndActivateSession(session);
    }

    [RelayCommand]
    public void LaunchDatabaseCli(DatabaseConnectionConfig? connectionConfig = null)
    {
        var config = connectionConfig ?? DatabaseSessionService.Instance.ActiveConfig;
        if (config == null)
        {
            // If no active connection, fallback to default shell
            NewSession();
            return;
        }

        var cliInfo = _cliService.ResolveCli(config, Settings);
        var dbTitle = $"{config.DatabaseType} ({config.DisplayName})";

        var session = new TerminalSessionViewModel
        {
            Title = dbTitle,
            IconKey = "Database",
            WorkingDirectory = Directory.GetCurrentDirectory()
        };

        if (cliInfo.IsAvailable)
        {
            session.ExecutablePath = cliInfo.ExecutablePath;
            session.Arguments = string.Join(" ", cliInfo.Arguments);
            foreach (var kvp in cliInfo.EnvironmentVariables)
            {
                session.EnvironmentVariables[kvp.Key] = kvp.Value;
            }
        }
        else
        {
            session.HasCliError = true;
            session.CliErrorTitle = $"{cliInfo.MissingToolName} Not Found";
            session.CliErrorMessage = cliInfo.Instructions;
            session.CliDownloadUrl = cliInfo.DownloadUrl;
        }

        AddAndActivateSession(session);
    }

    [RelayCommand]
    public void SelectSession(TerminalSessionViewModel session)
    {
        if (session == null || !Sessions.Contains(session)) return;

        foreach (var s in Sessions)
        {
            s.IsActive = (s == session);
        }

        ActiveSession = session;
        RequestFocus?.Invoke();
    }

    [RelayCommand]
    public void CloseSession(TerminalSessionViewModel? session = null)
    {
        var target = session ?? ActiveSession;
        if (target == null || !Sessions.Contains(target)) return;

        target.Kill();
        var index = Sessions.IndexOf(target);
        Sessions.Remove(target);

        if (Sessions.Count > 0)
        {
            var nextIndex = Math.Clamp(index, 0, Sessions.Count - 1);
            SelectSession(Sessions[nextIndex]);
        }
        else
        {
            ActiveSession = null;
        }
    }

    [RelayCommand]
    public void CloseOtherSessions(TerminalSessionViewModel? session = null)
    {
        var target = session ?? ActiveSession;
        if (target == null || !Sessions.Contains(target)) return;

        var others = Sessions.Where(s => s != target).ToList();
        foreach (var other in others)
        {
            other.Kill();
            Sessions.Remove(other);
        }

        SelectSession(target);
    }

    [RelayCommand]
    public void ClearActiveBuffer()
    {
        if (ActiveSession != null)
        {
            ActiveSession.ClearScreen();
            ActiveSession.SendText("\u000c"); // FormFeed / Ctrl+L
        }
    }

    public void SendTextToActiveTerminal(string text)
    {
        if (ActiveSession == null)
        {
            NewSession();
        }

        if (ActiveSession != null)
        {
            ActiveSession.SendText(text.EndsWith("\n") ? text : text + Environment.NewLine);
            RequestFocus?.Invoke();
        }
    }

    private void AddAndActivateSession(TerminalSessionViewModel session)
    {
        session.CloseRequested += CloseSession;
        session.RestartRequested += () => RestartSession(session);

        foreach (var s in Sessions)
        {
            s.IsActive = false;
        }
        session.IsActive = true;
        ActiveSession = session;

        Sessions.Add(session);
        RequestFocus?.Invoke();
    }

    private void RestartSession(TerminalSessionViewModel session)
    {
        session.HasCliError = false;
        if (DatabaseSessionService.Instance.ActiveConfig != null && session.IconKey == "Database")
        {
            var cliInfo = _cliService.ResolveCli(DatabaseSessionService.Instance.ActiveConfig, Settings);
            if (cliInfo.IsAvailable)
            {
                session.ExecutablePath = cliInfo.ExecutablePath;
                session.Arguments = string.Join(" ", cliInfo.Arguments);
                session.HasCliError = false;
            }
            else
            {
                session.HasCliError = true;
            }
        }
    }
}
