using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class ModuleMetadataTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly ModuleMetadataService _svc;

    public ModuleMetadataTools(WorkspaceRegistry registry, ModuleMetadataService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "list_streams")]
    [Description("List the .NET metadata streams (#~, #-, #Strings, #US, #GUID, #Blob, #Pdb) with sizes.")]
    public IReadOnlyList<StreamInfo> ListStreams([Description("Assembly id.")] string id)
        => _svc.ListStreams(_registry.Get(id));

    [McpServerTool(Name = "list_sections")]
    [Description("List the PE sections of the assembly with RVA/size/characteristics.")]
    public IReadOnlyList<SectionInfo> ListSections([Description("Assembly id.")] string id)
        => _svc.ListSections(_registry.Get(id));

    [McpServerTool(Name = "get_metadata_table_counts")]
    [Description("Get row counts for each metadata table.")]
    public IReadOnlyDictionary<string, int> GetMetadataTableCounts([Description("Assembly id.")] string id)
        => _svc.GetMetadataTableCounts(_registry.Get(id));
}
