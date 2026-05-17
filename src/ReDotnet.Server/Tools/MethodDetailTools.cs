using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class MethodDetailTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly MethodDetailService _svc;

    public MethodDetailTools(WorkspaceRegistry registry, MethodDetailService svc)
    {
        _registry = registry;
        _svc = svc;
    }

    [McpServerTool(Name = "get_method")]
    [Description("Get detailed info for a method: signature, attributes, PInvoke info, code size, locals, EHs.")]
    public MethodDetail GetMethod(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string identifier)
        => _svc.GetMethod(_registry.Get(id), identifier);

    [McpServerTool(Name = "disassemble_method")]
    [Description("Get the IL listing for a method with offsets, opcodes, and resolved operands.")]
    public DisassembledMethod Disassemble(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string identifier)
        => _svc.Disassemble(_registry.Get(id), identifier);

    [McpServerTool(Name = "get_method_locals")]
    [Description("Get the local variable signatures for a method.")]
    public IReadOnlyList<LocalVariableInfo> GetLocals(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string identifier)
        => _svc.GetLocals(_registry.Get(id), identifier);

    [McpServerTool(Name = "get_method_exception_handlers")]
    [Description("Get the try/catch/filter/finally regions for a method.")]
    public IReadOnlyList<ExceptionHandlerInfo> GetEhs(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string identifier)
        => _svc.GetExceptionHandlers(_registry.Get(id), identifier);
}
