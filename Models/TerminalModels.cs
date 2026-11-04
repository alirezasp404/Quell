using System;
using System.Collections.Generic;

namespace Fdb.Models;

public class ShellProfile
{
    public string Id { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string ExecutablePath { get; set; } = string.Empty;
    public string Arguments { get; set; } = string.Empty;
    public string IconKey { get; set; } = "Terminal";
    public bool IsDatabaseCli { get; set; }
    public string DatabaseType { get; set; } = string.Empty;

    public override string ToString() => DisplayName;
}

public class TerminalSettingsModel
{
    public string DefaultShellId { get; set; } = "default";
    public string FontFamily { get; set; } = "Cascadia Mono, Consolas, Courier New, monospace";
    public double FontSize { get; set; } = 13.0;
    public string CursorStyle { get; set; } = "Block"; // Block, Underline, Bar
    public bool CursorBlink { get; set; } = true;
    public int MaxScrollback { get; set; } = 5000;

    // Custom CLI overrides if not in PATH
    public string PsqlPath { get; set; } = string.Empty;
    public string MySqlPath { get; set; } = string.Empty;
    public string SqlitePath { get; set; } = string.Empty;
    public string SqlCmdPath { get; set; } = string.Empty;
}

public class CliLaunchInfo
{
    public bool IsAvailable { get; set; }
    public string ExecutablePath { get; set; } = string.Empty;
    public List<string> Arguments { get; set; } = new();
    public Dictionary<string, string> EnvironmentVariables { get; set; } = new();
    public string MissingToolName { get; set; } = string.Empty;
    public string DownloadUrl { get; set; } = string.Empty;
    public string Instructions { get; set; } = string.Empty;
}
