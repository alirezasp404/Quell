using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Quell.Models;

namespace Quell.Services;

/// <summary>
/// Singleton service managing GitHub Releases checks, semantic version comparisons,
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
    public const string AllReleasesApiUrl = $"https://api.github.com/repos/{GitHubRepo}/releases";
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
            Timeout = TimeSpan.FromSeconds(5)
        };
        if (!_httpClient.DefaultRequestHeaders.Contains("User-Agent"))
        {
            _httpClient.DefaultRequestHeaders.UserAgent.Add(new ProductInfoHeaderValue("Quell-Studio", CurrentVersionString));
        }
    }

    /// <summary>
    /// Checks GitHub repository releases for a newer version than CurrentVersion.
    /// Runs asynchronously and fails gracefully on network errors or timeouts.
    /// Only returns a release if a strictly newer version (> CurrentVersion) is found.
    /// </summary>
    public async Task<UpdateReleaseModel?> CheckForUpdatesAsync(bool isManualCheck = false)
    {
        try
        {
            // 1. First attempt: Query releases/latest
            using var response = await _httpClient.GetAsync(ReleasesApiUrl).ConfigureAwait(false);
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var doc = JsonDocument.Parse(json);
                var release = ParseReleaseElement(doc.RootElement);
                if (release != null && release.IsNewer)
                {
                    LatestRelease = release;
                    IsUpdateAvailable = true;
                    UpdateAvailable?.Invoke(this, release);
                    return release;
                }
            }

            // 2. Second attempt: Query full releases list to find any newer version tag
            using var allResponse = await _httpClient.GetAsync(AllReleasesApiUrl).ConfigureAwait(false);
            if (allResponse.IsSuccessStatusCode)
            {
                var allJson = await allResponse.Content.ReadAsStringAsync().ConfigureAwait(false);
                using var allDoc = JsonDocument.Parse(allJson);
                if (allDoc.RootElement.ValueKind == JsonValueKind.Array)
                {
                    UpdateReleaseModel? bestCandidate = null;
                    foreach (var elem in allDoc.RootElement.EnumerateArray())
                    {
                        var candidate = ParseReleaseElement(elem);
                        if (candidate != null && candidate.IsNewer)
                        {
                            if (bestCandidate == null || candidate.ParsedVersion > bestCandidate.ParsedVersion)
                            {
                                bestCandidate = candidate;
                            }
                        }
                    }

                    if (bestCandidate != null)
                    {
                        LatestRelease = bestCandidate;
                        IsUpdateAvailable = true;
                        UpdateAvailable?.Invoke(this, bestCandidate);
                        return bestCandidate;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[AppUpdateService] CheckForUpdates error: {ex.Message}");
        }

        // No newer release found on GitHub; ensure update state is clean
        IsUpdateAvailable = false;
        LatestRelease = null;
        return null;
    }

    /// <summary>
    /// Parses a GitHub Release JSON element into an UpdateReleaseModel.
    /// Resolves the actual semantic version from tag_name, title, body, or asset filenames.
    /// IsNewer is only true if the resolved version is strictly greater than CurrentVersion.
    /// </summary>
    public UpdateReleaseModel? ParseReleaseElement(JsonElement root)
    {
        string tagName = root.TryGetProperty("tag_name", out var tagElem) ? tagElem.GetString() ?? "" : "";
        string name = root.TryGetProperty("name", out var nameElem) ? nameElem.GetString() ?? tagName : tagName;
        string body = root.TryGetProperty("body", out var bodyElem) ? bodyElem.GetString() ?? "" : "";
        string htmlUrl = root.TryGetProperty("html_url", out var urlElem) ? urlElem.GetString() ?? "" : DefaultReleasesPageUrl;
        string publishedAt = root.TryGetProperty("published_at", out var pubElem) ? pubElem.GetString() ?? "" : "";

        Version? remoteVer = null;
        bool hasVersion = TryParseVersion(tagName, out remoteVer) || 
                          TryParseVersionFromNameOrBody(name, out remoteVer) ||
                          TryParseVersionFromNameOrBody(body, out remoteVer);

        // If not found in tag, name or body, check asset names (e.g., Quell-1.0.0-win-x64-portable.zip)
        if (!hasVersion && root.TryGetProperty("assets", out var assetsElem) && assetsElem.ValueKind == JsonValueKind.Array)
        {
            foreach (var asset in assetsElem.EnumerateArray())
            {
                if (asset.TryGetProperty("name", out var assetNameElem))
                {
                    string assetName = assetNameElem.GetString() ?? "";
                    if (TryParseVersionFromNameOrBody(assetName, out remoteVer))
                    {
                        hasVersion = true;
                        break;
                    }
                }
            }
        }

        // Only mark as newer if a valid version was resolved AND it strictly exceeds CurrentVersion
        bool isNewer = hasVersion && remoteVer != null && remoteVer > CurrentVersion;

        return new UpdateReleaseModel
        {
            TagName = string.IsNullOrWhiteSpace(tagName) ? (remoteVer != null ? $"v{remoteVer}" : CurrentVersionString) : tagName,
            Name = string.IsNullOrWhiteSpace(name) ? "Quell Studio Update" : name,
            Body = body,
            HtmlUrl = htmlUrl,
            PublishedAt = publishedAt,
            ParsedVersion = remoteVer ?? CurrentVersion,
            IsNewer = isNewer
        };
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

    public static bool TryParseVersionFromNameOrBody(string? text, out Version? version)
    {
        version = null;
        if (string.IsNullOrWhiteSpace(text)) return false;

        var match = Regex.Match(text, @"\bv?(\d+\.\d+(\.\d+)?)\b", RegexOptions.IgnoreCase);
        if (match.Success)
        {
            return TryParseVersion(match.Groups[1].Value, out version);
        }
        return false;
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
