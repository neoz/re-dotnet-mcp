using AsmResolver.DotNet;
using AsmResolver.DotNet.Resources;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Resources;

public sealed class ResourceService
{
    private readonly TokenResolver _tokens;
    public ResourceService(TokenResolver tokens) => _tokens = tokens;

    public Page<ManifestResourceInfo> ListResources(Workspace.Workspace ws, int? offset, int? limit)
    {
        var all = ws.Module.Resources.Select(r => new ManifestResourceInfo(
            Name: r.Name?.ToString() ?? "",
            IsEmbedded: r.IsEmbedded,
            IsPublic: r.IsPublic,
            Size: r.IsEmbedded ? r.GetData()?.Length : null,
            ImplementationName: r.Implementation?.Name?.ToString())).ToList();
        return Page<ManifestResourceInfo>.From(all, offset, limit);
    }

    public ResourceData ReadResource(Workspace.Workspace ws, string name, int maxBytes)
    {
        var res = ws.Module.Resources.FirstOrDefault(r => r.Name == name)
            ?? throw BackendError.NotFound("resource", name);
        if (!res.IsEmbedded)
            return new ResourceData(name, "", "", 0, 0, false, "<linked, not embedded>");
        var data = res.GetData() ?? Array.Empty<byte>();
        var capped = data.Length > maxBytes ? data[..maxBytes] : data;
        var detected = Detect(capped);
        return new ResourceData(
            Name: name,
            EncodingDetected: detected,
            HexPreview: Convert.ToHexString(capped[..Math.Min(64, capped.Length)]),
            BytesReturned: capped.Length,
            TotalBytes: data.Length,
            Truncated: capped.Length < data.Length,
            Excerpt: detected == "utf8-string"
                ? System.Text.Encoding.UTF8.GetString(capped)
                : null);
    }

    public Page<CustomAttrInfo> ListCustomAttributes(Workspace.Workspace ws, string? targetTokenOrName, string? typeFilter, int? offset, int? limit)
    {
        var all = new List<CustomAttrInfo>();
        if (targetTokenOrName is not null)
        {
            var m = _tokens.Resolve(ws, targetTokenOrName);
            if (m is IHasCustomAttribute hca) AppendAttrs(hca, all, typeFilter);
        }
        else
        {
            foreach (var t in ws.Module.GetAllTypes())
            {
                AppendAttrs(t, all, typeFilter);
                foreach (var mm in t.Methods) AppendAttrs(mm, all, typeFilter);
                foreach (var f in t.Fields) AppendAttrs(f, all, typeFilter);
                foreach (var p in t.Properties) AppendAttrs(p, all, typeFilter);
                foreach (var e in t.Events) AppendAttrs(e, all, typeFilter);
            }
        }
        return Page<CustomAttrInfo>.From(all, offset, limit);
    }

    private static void AppendAttrs(IHasCustomAttribute owner, List<CustomAttrInfo> sink, string? filter)
    {
        foreach (var ca in owner.CustomAttributes)
        {
            var attrType = ca.Constructor?.DeclaringType?.FullName ?? "?";
            if (filter is not null && !attrType.Contains(filter, StringComparison.OrdinalIgnoreCase)) continue;
            sink.Add(new CustomAttrInfo(
                Owner: (owner as IFullNameProvider)?.FullName
                    ?? (owner as INameProvider)?.Name?.ToString() ?? "?",
                OwnerToken: owner is IMetadataMember mm ? TokenFormatHelper.Hex(mm) : "",
                AttributeType: attrType,
                FixedArguments: ca.Signature?.FixedArguments.Select(a => a.Element?.ToString() ?? "").ToList()
                    ?? new List<string>()));
        }
    }

    private static string Detect(byte[] data)
    {
        if (data.Length >= 4 && data[0] == 0x89 && data[1] == 0x50 && data[2] == 0x4E && data[3] == 0x47)
            return "png";
        if (data.Length >= 2 && data[0] == 0x4D && data[1] == 0x5A) return "pe";
        if (LooksLikeUtf8(data)) return "utf8-string";
        return "binary";
    }

    private static bool LooksLikeUtf8(byte[] data)
    {
        if (data.Length == 0) return false;
        try
        {
            var s = System.Text.Encoding.UTF8.GetString(data);
            var printable = s.Count(c => !char.IsControl(c) || c == '\n' || c == '\r' || c == '\t');
            return (double)printable / s.Length > 0.9;
        }
        catch { return false; }
    }
}

public sealed record ManifestResourceInfo(
    string Name, bool IsEmbedded, bool IsPublic, int? Size, string? ImplementationName);

public sealed record ResourceData(
    string Name, string EncodingDetected, string HexPreview,
    int BytesReturned, int TotalBytes, bool Truncated, string? Excerpt);

public sealed record CustomAttrInfo(
    string Owner, string OwnerToken, string AttributeType,
    IReadOnlyList<string> FixedArguments);
