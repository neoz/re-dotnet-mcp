using System.Text.Json;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Sidecar;

public sealed class SidecarStore
{
    private static readonly JsonSerializerOptions JsonOpts = new()
    {
        WriteIndented = true,
        DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull,
    };

    public string PathFor(Workspace.Workspace ws)
    {
        var dir = Path.Combine(Path.GetDirectoryName(ws.OriginalPath) ?? ".", ".redotnet");
        return Path.Combine(dir, ws.AssemblyId + ".json");
    }

    public SidecarDocument LoadOrCreate(Workspace.Workspace ws)
    {
        var path = PathFor(ws);
        if (!File.Exists(path))
        {
            return new SidecarDocument
            {
                Assembly = Path.GetFileName(ws.OriginalPath),
                ModuleMvid = ws.Module.Mvid.ToString("D"),
            };
        }

        var json = File.ReadAllText(path);
        var doc = JsonSerializer.Deserialize<SidecarDocument>(json, JsonOpts)
            ?? new SidecarDocument { Assembly = Path.GetFileName(ws.OriginalPath) };
        if (doc.Version > SidecarDocument.CurrentVersion)
            throw new InvalidOperationException($"sidecar version {doc.Version} is newer than supported ({SidecarDocument.CurrentVersion})");
        return doc;
    }

    public void Save(Workspace.Workspace ws, SidecarDocument doc)
    {
        var path = PathFor(ws);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);

        doc.Version = SidecarDocument.CurrentVersion;
        doc.Assembly = Path.GetFileName(ws.OriginalPath);
        doc.ModuleMvid ??= ws.Module.Mvid.ToString("D");

        var json = JsonSerializer.Serialize(doc, JsonOpts);
        var tmp = path + ".tmp";
        File.WriteAllText(tmp, json);
        if (File.Exists(path)) File.Delete(path);
        File.Move(tmp, path);
    }
}
