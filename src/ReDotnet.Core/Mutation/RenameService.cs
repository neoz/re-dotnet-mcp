using AsmResolver;
using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Mutation;

public sealed class RenameService
{
    private readonly TokenResolver _tokens;
    public RenameService(TokenResolver tokens) => _tokens = tokens;

    public RenameResult RenameType(Workspace.Workspace ws, string identifier, string newName, string? newNamespace)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not TypeDefinition t)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a TypeDefinition");

        var oldName = t.Name?.ToString() ?? "";
        var oldNs = t.Namespace?.ToString() ?? "";
        t.Name = newName;
        if (newNamespace is not null) t.Namespace = newNamespace;

        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.RenameType, ws.AssemblyId, $"0x{t.MetadataToken.ToUInt32():X8}",
            new { name = oldName, ns = oldNs },
            new { name = newName, ns = newNamespace ?? oldNs },
            DateTimeOffset.UtcNow));

        return new RenameResult(
            Token: $"0x{t.MetadataToken.ToUInt32():X8}",
            OldFullName: string.IsNullOrEmpty(oldNs) ? oldName : $"{oldNs}.{oldName}",
            NewFullName: t.FullName,
            AffectedRefs: 0);
    }

    public RenameResult RenameMethod(Workspace.Workspace ws, string identifier, string newName)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a MethodDefinition");
        var old = md.Name?.ToString() ?? "";
        md.Name = newName;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.RenameMethod, ws.AssemblyId, $"0x{md.MetadataToken.ToUInt32():X8}",
            old, newName, DateTimeOffset.UtcNow));
        return new RenameResult($"0x{md.MetadataToken.ToUInt32():X8}", old, md.Name!, 0);
    }

    public RenameResult RenameField(Workspace.Workspace ws, string identifier, string newName)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not FieldDefinition fd)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a FieldDefinition");
        var old = fd.Name?.ToString() ?? "";
        fd.Name = newName;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.RenameField, ws.AssemblyId, $"0x{fd.MetadataToken.ToUInt32():X8}",
            old, newName, DateTimeOffset.UtcNow));
        return new RenameResult($"0x{fd.MetadataToken.ToUInt32():X8}", old, fd.Name!, 0);
    }

    public RenameResult RenameParameter(Workspace.Workspace ws, string methodIdentifier, int index, string newName)
    {
        var m = _tokens.Resolve(ws, methodIdentifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{methodIdentifier}' did not resolve to a MethodDefinition");
        var p = md.Parameters.FirstOrDefault(x => x.Index == index)
            ?? throw BackendError.NotFound("parameter", $"{md.FullName}#{index}");
        var old = p.Name?.ToString() ?? "";
        p.GetOrCreateDefinition().Name = newName;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.RenameParameter, ws.AssemblyId, $"0x{md.MetadataToken.ToUInt32():X8}",
            new { index, name = old }, new { index, name = newName }, DateTimeOffset.UtcNow));
        return new RenameResult($"0x{md.MetadataToken.ToUInt32():X8}#{index}", old, newName, 0);
    }

    public RenameResult RenameLocal(Workspace.Workspace ws, string methodIdentifier, int index, string newName)
    {
        // PortablePdb writeback is non-obvious; for v1 we store local-name overrides
        // in the sidecar so analysts get persistent renames regardless of PDB presence.
        var m = _tokens.Resolve(ws, methodIdentifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{methodIdentifier}' did not resolve to a MethodDefinition");
        var key = $"{md.MetadataToken.ToUInt32():X8}#local#{index}";
        var sidecar = ws.Sidecar;
        sidecar.Annotations.TryGetValue(key, out var existing);
        var old = existing?.Rename;
        if (!sidecar.Annotations.TryGetValue(key, out var entry))
            sidecar.Annotations[key] = entry = new Sidecar.AnnotationEntry();
        entry.Rename = newName;
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.RenameLocal, ws.AssemblyId, key, old, newName, DateTimeOffset.UtcNow));
        return new RenameResult(key, old ?? "", newName, 0);
    }
}

public sealed record RenameResult(
    string Token, string OldFullName, string NewFullName, int AffectedRefs);
