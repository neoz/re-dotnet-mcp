using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Mutation;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class MutationTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly SidecarMutationService _sidecar;
    private readonly RenameService _renames;
    private readonly IlPatchService _il;
    private readonly UndoRedoService _undo;

    public MutationTools(
        WorkspaceRegistry registry,
        SidecarMutationService sidecar,
        RenameService renames,
        IlPatchService il,
        UndoRedoService undo)
    {
        _registry = registry;
        _sidecar = sidecar;
        _renames = renames;
        _il = il;
        _undo = undo;
    }

    [McpServerTool(Name = "set_comment")]
    [Description("Set or clear (pass null) a sidecar comment for a token.")]
    public SetResult SetComment(
        [Description("Assembly id.")] string id,
        [Description("Token to annotate.")] string token,
        [Description("Comment text, or null to clear.")] string? text)
        => _registry.Run(id, ws => _sidecar.SetComment(ws, token, text));

    [McpServerTool(Name = "set_method_il_comment")]
    [Description("Set or clear an IL-offset-scoped comment inside a method.")]
    public SetResult SetMethodIlComment(
        [Description("Assembly id.")] string id,
        [Description("Method token.")] string method_token,
        [Description("IL offset (e.g. 'IL_0020' or '0x0020').")] string il_offset,
        [Description("Comment text, or null to clear.")] string? text)
        => _registry.Run(id, ws => _sidecar.SetIlComment(ws, method_token, il_offset, text));

    [McpServerTool(Name = "set_bookmark")]
    [Description("Add a bookmark targeting a token.")]
    public BookmarkEntry SetBookmark(
        [Description("Assembly id.")] string id,
        [Description("Token to bookmark.")] string token,
        [Description("Optional human description.")] string? description = null)
        => _registry.Run(id, ws => _sidecar.SetBookmark(ws, token, description));

    [McpServerTool(Name = "delete_bookmark")]
    [Description("Remove a bookmark by slot.")]
    public bool DeleteBookmark(
        [Description("Assembly id.")] string id,
        [Description("Bookmark slot.")] int slot)
        => _registry.Run(id, ws => _sidecar.DeleteBookmark(ws, slot));

    [McpServerTool(Name = "set_color")]
    [Description("Set or clear (pass null) a sidecar color tag for a token (rrggbb).")]
    public SetResult SetColor(
        [Description("Assembly id.")] string id,
        [Description("Token to color.")] string token,
        [Description("Hex color rrggbb, or null to clear.")] string? color)
        => _registry.Run(id, ws => _sidecar.SetColor(ws, token, color));

    [McpServerTool(Name = "rename_type")]
    [Description("Rename a type. Updates the TypeDef row; cross-module refs are unaffected.")]
    public RenameResult RenameType(
        [Description("Assembly id.")] string id,
        [Description("Type identifier.")] string token,
        [Description("New simple name.")] string new_name,
        [Description("Optional new namespace.")] string? new_namespace = null)
        => _registry.Run(id, ws => _renames.RenameType(ws, token, new_name, new_namespace));

    [McpServerTool(Name = "rename_method")]
    [Description("Rename a method.")]
    public RenameResult RenameMethod(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string token,
        [Description("New name.")] string new_name)
        => _registry.Run(id, ws => _renames.RenameMethod(ws, token, new_name));

    [McpServerTool(Name = "rename_field")]
    [Description("Rename a field.")]
    public RenameResult RenameField(
        [Description("Assembly id.")] string id,
        [Description("Field identifier.")] string token,
        [Description("New name.")] string new_name)
        => _registry.Run(id, ws => _renames.RenameField(ws, token, new_name));

    [McpServerTool(Name = "rename_parameter")]
    [Description("Rename a method parameter.")]
    public RenameResult RenameParameter(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_token,
        [Description("Parameter index (0-based, before 'this').")] int index,
        [Description("New name.")] string new_name)
        => _registry.Run(id, ws => _renames.RenameParameter(ws, method_token, index, new_name));

    [McpServerTool(Name = "rename_local")]
    [Description("Rename a local variable. Written to the sidecar (PortablePdb writeback v1.1+).")]
    public RenameResult RenameLocal(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_token,
        [Description("Local index.")] int index,
        [Description("New name.")] string new_name)
        => _registry.Run(id, ws => _renames.RenameLocal(ws, method_token, index, new_name));

    [McpServerTool(Name = "patch_il")]
    [Description("Replace IL starting at offset with assembled new_il. Must match length exactly; pad with nops.")]
    public PatchResult PatchIl(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_token,
        [Description("IL offset to start patching at (e.g. 'IL_0020').")] string offset,
        [Description("ilasm-subset text for the new instructions.")] string new_il)
        => _registry.Run(id, ws => _il.PatchIl(ws, method_token, offset, new_il));

    [McpServerTool(Name = "nop_il_range")]
    [Description("Replace IL instructions in [start_offset, end_offset) with nops preserving length.")]
    public PatchResult NopIlRange(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_token,
        [Description("Start offset (inclusive).")] string start_offset,
        [Description("End offset (exclusive).")] string end_offset)
        => _registry.Run(id, ws => _il.NopIlRange(ws, method_token, start_offset, end_offset));

    [McpServerTool(Name = "replace_method_body")]
    [Description("Replace the entire IL body of a method with the assembled il_text.")]
    public PatchResult ReplaceMethodBody(
        [Description("Assembly id.")] string id,
        [Description("Method identifier.")] string method_token,
        [Description("ilasm-subset text for the entire body.")] string il_text)
        => _registry.Run(id, ws => _il.ReplaceMethodBody(ws, method_token, il_text));

    [McpServerTool(Name = "undo")]
    [Description("Pop and revert the most recent mutation for this assembly.")]
    public MutationLogEntry? Undo([Description("Assembly id.")] string id)
        => _registry.Run(id, ws => _undo.Undo(ws));

    [McpServerTool(Name = "redo")]
    [Description("Re-apply the most recently undone mutation for this assembly.")]
    public MutationLogEntry? Redo([Description("Assembly id.")] string id)
        => _registry.Run(id, ws => _undo.Redo(ws));
}
