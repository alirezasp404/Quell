using System;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Reflection;
using System.Text.Json;
using System.Threading.Tasks;
using Quell.Models;

namespace Quell.Services;

/// <summary>
/// Service responsible for GitHub release versioning checks, semantic version comparisons,
/// and update notifications across Quell Studio.
/// </summary>
public class AppUpdateService
{
    private static AppUpdateService? _instance;
    public static AppUpdateService Instance => _instance ??= new AppUpdateService();

    public static readonly Version CurrentVersion = new(1, 0, 0);
    public static string CurrentVersionString => $"v{CurrentVersion.Major}.{CurrentVersion.Minor}.{CurrentVersion.Build}";

    public const string GitHubRepo = "cipherunits/Quell";
    public const string ReleasesApiUrl = $"https://api.github.com/repos/{GitHubRepo}/releases/latest";
    public const string DefaultReleasesPageUrl = $"https://github.com/{GitHubRepo}/releases";

    private readonly HttpClient _httpClient;

    public bool IsUpdateAvailable { get; private set; }
    public bool IsCornerBadgeVisible { get; set; }
    public UpdateReleaseModel? LatestRelease { get; private set; }

    public event EventHandler<UpdateReleaseModel>? UpdateAvailable;

    public AppUpdateService(HttpClient? client = null)
    {
        _httpClient = client ?? new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(4)
        };
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Quell-Studio", CurrentVersionString));
        }
    }

    /// <summary>
    /// Checks GitHub repository releases for a newer version than CurrentVersion.
    /// Runs asynchronously and fails gracefully on network errors or timeouts.
    /// </summary>
    public async Task<UpdateReleaseModel?> CheckForUpdatesAsync(bool isManualCheck = false)
    {
        try
        {
            using var response = await _httpClient.GetAsync(ReleasesApiUrl).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode)
            {
                return null;
            }

            var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;

            string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
            string name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? tagName : tagName;
            string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
            string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : DefaultReleasesPageUrl;
            string publishedAt = root.TryGetProperty("published_at", out var pubElem) ? pubElem.GetString() ?? "" : "";

            if (TryParseVersion(tagName, out var remoteVer) && remoteVer != null)
            {
                bool isNewer = remoteVer > CurrentVersion;
                var release = new UpdateReleaseModel
                {
                    TagName = tagName,
                    Name = string.IsNullOrWhiteSpace(name) ? tagName : name,
                    Body = body,
                    HtmlUrl = htmlUrl,
                    PublishedAt = publishedAt,
                    ParsedVersion = remoteVer,
                    IsNewer = isNewer
                };

                if (isNewer)
                {
                    LatestRelease = release;
                    IsUpdateAvailable = true;
                    UpdateAvailable?.Invoke(this, release);
                }

                return release;
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppUpdateService] CheckForUpdates error: {ex.Message}");
        }

        return null;
    }

    /// <summary>
    /// Parses version string from release tag name (e.g., 'v1.1.0' -> 1.1.0).
    /// </summary>
    public static bool TryParseVersion(string? tag, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(tag)) return false;

        string clean = tag.Trim().TrimStart('v', 'V');
        int dashIndex = clean.IndexOf('-');
        if (dashIndex > 0) clean = clean[..dashIndex];

        // Ensure 3 segments for System.Version if only 1 or 2 are present
        int dotCount = clean.Count(c => c == '.');
        if (dotCount == 0) clean += ".0.0";
        else if (dotCount == 1) clean += ".0";

        return Version.TryParse(clean, out version);
    }

    /// <summary>
    /// Helper to trigger update availability (used for verification testing and offline preview).
    /// </summary>
    public void SimulateUpdateAvailable(string newVersion = "v1.1.0", string? releaseTitle = null, string? notes = null, string? releaseUrl = null)
    {
        TryParseVersion(newVersion, out var ver);
        var release = new UpdateReleaseModel
        {
            TagName = newVersion,
            Name = releaseTitle ?? $"Quell Studio {newVersion} - Performance & ER Engine Update",
            Body = notes ?? "• Automatic GitHub Releases version detection & notification\n• Hardware GPU-accelerated fluid modal pop-in animations\n• Real-time startup sequence linked to actual sub-systems\n• Schema properties navigation & multi-dialect SQL autocomplete",
            HtmlUrl = releaseUrl ?? DefaultReleasesPageUrl,
            ParsedVersion = ver ?? new Version(1, 1, 0),
            IsNewer = true
        };

        LatestRelease = release;
        IsUpdateAvailable = true;
        UpdateAvailable?.Invoke(this, release);
    }

    /// <summary>
    /// Resets update detection state (e.g. for testing).
    /// </summary>
    public void Reset()
    {
        IsUpdateAvailable = false;
        IsCornerBadgeVisible = false;
        LatestRelease = null;
    }
}
