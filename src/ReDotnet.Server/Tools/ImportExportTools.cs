using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class ImportExportTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly ImportExportService _svc;

    public ImportExportTools(WorkspaceRegistry registry, ImportExportService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "list_pinvokes")]
    [Description("List all DllImport methods with target DLL, entry point, charset, calling convention.")]
    public PageDto<PInvokeEntry> ListPInvokes(
        [Description("Assembly id.")] string id,
        [Description("Optional substring filter on the target module name.")] string? module_filter = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<PInvokeEntry>.From(_svc.ListPInvokes(ws, module_filter, offset, limit)));

    [McpServerTool(Name = "list_unmanaged_exports")]
    [Description("List methods exported via [DllExport]-style VTable fixups.")]
    public PageDto<UnmanagedExportEntry> ListUnmanagedExports(
        [Description("Assembly id.")] string id,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<UnmanagedExportEntry>.From(_svc.ListUnmanagedExports(ws, offset, limit)));

    [McpServerTool(Name = "list_type_forwarders")]
    [Description("List TypeForwardedTo entries (exported types re-routed to another assembly).")]
    public PageDto<TypeForwarderEntry> ListTypeForwarders(
        [Description("Assembly id.")] string id,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<TypeForwarderEntry>.From(_svc.ListTypeForwarders(ws, offset, limit)));
}
