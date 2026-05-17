using AsmResolver.PE.DotNet.Metadata;
using AsmResolver.PE.DotNet.Metadata.Tables;
using AsmResolver.PE.File;

namespace ReDotnet.Core.Inspection;

public sealed class ModuleMetadataService
{
    public IReadOnlyList<StreamInfo> ListStreams(Workspace.Workspace ws)
    {
        var md = ws.Module.DotNetDirectory?.Metadata;
        if (md is null) return Array.Empty<StreamInfo>();
        return md.Streams.Select(s => new StreamInfo(s.Name, EstimateSize(s))).ToList();
    }

    private static long EstimateSize(IMetadataStream stream)
    {
        try { return stream.GetPhysicalSize(); }
        catch { return 0; }
    }

    public IReadOnlyList<SectionInfo> ListSections(Workspace.Workspace ws)
    {
        if (string.IsNullOrEmpty(ws.Module.FilePath) || !File.Exists(ws.Module.FilePath))
            return Array.Empty<SectionInfo>();
        var pe = PEFile.FromFile(ws.Module.FilePath);
        return pe.Sections.Select(s => new SectionInfo(
            s.Name, s.Rva, s.GetVirtualSize(), s.GetPhysicalSize(),
            s.Characteristics.ToString())).ToList();
    }

    public IReadOnlyDictionary<string, int> GetMetadataTableCounts(Workspace.Workspace ws)
    {
        var tablesStream = ws.Module.DotNetDirectory?.Metadata?.GetStream<TablesStream>();
        if (tablesStream is null) return new Dictionary<string, int>();

        var result = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (TableIndex idx in Enum.GetValues<TableIndex>())
        {
            try
            {
                var table = tablesStream.GetTable(idx);
                result[idx.ToString()] = (int)table.Count;
            }
            catch
            {
                // Some table indices may not be valid; skip silently.
            }
        }
        return result;
    }
}

public sealed record StreamInfo(string Name, long Size);
public sealed record SectionInfo(string Name, uint Rva, uint VirtualSize, uint PhysicalSize, string Characteristics);
