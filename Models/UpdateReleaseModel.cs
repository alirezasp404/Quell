using System;

namespace Quell.Models;

/// <summary>
/// Model representing a GitHub release payload and version metadata.
/// </summary>
public class UpdateReleaseModel
{
    public string TagName { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string HtmlUrl { get; set; } = string.Empty;
    public string PublishedAt { get; set; } = string.Empty;
    public Version? ParsedVersion { get; set; }
    public bool IsNewer { get; set; }
}
