using System.Text.Json.Serialization;

namespace ReDotnet.Core.Sidecar;

public sealed class SidecarDocument
{
    public const int CurrentVersion = 1;

    [JsonPropertyName("version")]
    public int Version { get; set; } = CurrentVersion;

    [JsonPropertyName("assembly")]
    public string Assembly { get; set; } = "";

    [JsonPropertyName("module_mvid")]
    public string? ModuleMvid { get; set; }

    [JsonPropertyName("annotations")]
    public Dictionary<string, AnnotationEntry> Annotations { get; set; } = new();

    [JsonPropertyName("il_comments")]
    public Dictionary<string, Dictionary<string, string>> IlComments { get; set; } = new();

    [JsonPropertyName("bookmarks")]
    public List<BookmarkEntry> Bookmarks { get; set; } = new();
}

public sealed class AnnotationEntry
{
    [JsonPropertyName("rename")] public string? Rename { get; set; }
    [JsonPropertyName("comment")] public string? Comment { get; set; }
    [JsonPropertyName("color")] public string? Color { get; set; }
}

public sealed class BookmarkEntry
{
    [JsonPropertyName("slot")] public int Slot { get; set; }
    [JsonPropertyName("token")] public string Token { get; set; } = "";
    [JsonPropertyName("desc")] public string? Description { get; set; }
}
