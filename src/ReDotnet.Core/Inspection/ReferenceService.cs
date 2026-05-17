using System.Collections.Concurrent;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Inspection;

public sealed class ReferenceService
{
    private readonly TokenResolver _tokens;
    private readonly ConcurrentDictionary<string, XrefIndex> _indexes = new(StringComparer.Ordinal);

    public ReferenceService(TokenResolver tokens) => _tokens = tokens;

    public Page<AssemblyRefInfo> ListAssemblyRefs(Workspace.Workspace ws, string? filterRegex, int? offset, int? limit)
    {
        var rx = filterRegex is null ? null : new System.Text.RegularExpressions.Regex(filterRegex);
        var all = ws.Module.AssemblyReferences
            .Where(r => rx is null || rx.IsMatch(r.FullName))
            .Select(r => new AssemblyRefInfo(
                Token: TokenFormatHelper.Hex(r),
                Name: r.Name?.ToString() ?? "",
                Version: r.Version?.ToString() ?? "",
                Culture: r.Culture?.ToString() ?? "",
                PublicKeyToken: r.GetPublicKeyToken() is { } pk ? Convert.ToHexString(pk) : null,
                HasHash: r.HashValue is { Length: > 0 }))
            .ToList();
        return Page<AssemblyRefInfo>.From(all, offset, limit);
    }

    public Page<ModuleRefInfo> ListModuleRefs(Workspace.Workspace ws, int? offset, int? limit)
    {
        var all = ws.Module.ModuleReferences
            .Select(r => new ModuleRefInfo(TokenFormatHelper.Hex(r), r.Name?.ToString() ?? ""))
            .ToList();
        return Page<ModuleRefInfo>.From(all, offset, limit);
    }

    public Page<TypeRefInfo> ListTypeRefs(Workspace.Workspace ws, string? filterRegex, int? offset, int? limit)
    {
        var rx = filterRegex is null ? null : new System.Text.RegularExpressions.Regex(filterRegex);
        var all = new List<TypeRefInfo>();
        foreach (var typeRef in ws.Module.GetImportedTypeReferences())
        {
            if (rx is not null && !rx.IsMatch(typeRef.FullName)) continue;
            all.Add(new TypeRefInfo(
                Token: TokenFormatHelper.Hex(typeRef),
                FullName: typeRef.FullName,
                Scope: typeRef.Scope?.Name?.ToString() ?? "<null>"));
        }
        return Page<TypeRefInfo>.From(all, offset, limit);
    }

    public Page<MemberRefInfo> ListMemberRefs(Workspace.Workspace ws, string? filterRegex, int? offset, int? limit)
    {
        var rx = filterRegex is null ? null : new System.Text.RegularExpressions.Regex(filterRegex);
        var all = new List<MemberRefInfo>();
        foreach (var memberRef in ws.Module.GetImportedMemberReferences())
        {
            var name = memberRef.FullName;
            if (rx is not null && !rx.IsMatch(name)) continue;
            all.Add(new MemberRefInfo(
                Token: TokenFormatHelper.Hex(memberRef),
                FullName: name,
                DeclaringType: memberRef.DeclaringType?.FullName ?? "?"));
        }
        return Page<MemberRefInfo>.From(all, offset, limit);
    }

    public Page<XrefEdge> GetXrefsTo(Workspace.Workspace ws, string identifier, int? offset, int? limit)
    {
        var target = _tokens.Resolve(ws, identifier);
        var index = GetOrBuildIndex(ws);
        if (!index.IncomingByToken.TryGetValue(target.MetadataToken.ToUInt32(), out var edges))
            edges = new List<XrefEdge>();
        return Page<XrefEdge>.From(edges, offset, limit);
    }

    public Page<XrefEdge> GetXrefsFrom(Workspace.Workspace ws, string methodId, int? offset, int? limit)
    {
        var m = _tokens.Resolve(ws, methodId);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{methodId}' did not resolve to a MethodDefinition");

        var edges = new List<XrefEdge>();
        var body = md.CilMethodBody;
        if (body is not null)
        {
            var seen = new HashSet<uint>();
            foreach (var instr in body.Instructions)
            {
                if (instr.Operand is IMetadataMember mm && seen.Add(mm.MetadataToken.ToUInt32()))
                {
                    edges.Add(new XrefEdge(
                        FromToken: TokenFormatHelper.Hex(md),
                        FromName: md.FullName,
                        ToToken: TokenFormatHelper.Hex(mm),
                        ToName: NameOf(mm),
                        Opcode: instr.OpCode.Mnemonic,
                        Offset: $"IL_{instr.Offset:X4}"));
                }
            }
        }
        return Page<XrefEdge>.From(edges, offset, limit);
    }

    public CallGraphNode GetCallGraph(Workspace.Workspace ws, string methodId, int depth)
    {
        if (depth < 1) depth = 1;
        if (depth > 3) depth = 3;
        var m = _tokens.Resolve(ws, methodId);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{methodId}' did not resolve to a MethodDefinition");

        var visited = new HashSet<uint>();
        return BuildCallGraph(ws, md, depth, visited);
    }

    private CallGraphNode BuildCallGraph(Workspace.Workspace ws, MethodDefinition md, int depth, HashSet<uint> visited)
    {
        var node = new CallGraphNode(TokenFormatHelper.Hex(md), md.FullName, new List<CallGraphNode>());
        if (depth == 0 || !visited.Add(md.MetadataToken.ToUInt32())) return node;

        var body = md.CilMethodBody;
        if (body is null) return node;

        foreach (var instr in body.Instructions)
        {
            if (instr.OpCode.OperandType == CilOperandType.InlineMethod
                && instr.Operand is MethodDefinition callee)
            {
                node.Callees.Add(BuildCallGraph(ws, callee, depth - 1, visited));
            }
        }
        return node;
    }

    private XrefIndex GetOrBuildIndex(Workspace.Workspace ws)
    {
        // Callers already hold ws.ModuleLock (via WorkspaceRegistry.Run), so
        // the build is naturally serialized per workspace. The cache itself
        // is concurrent only to allow lookups across workspaces in parallel.
        return _indexes.GetOrAdd(ws.AssemblyId, _ => XrefIndex.Build(ws));
    }

    public void InvalidateIndex(Workspace.Workspace ws)
    {
        _indexes.TryRemove(ws.AssemblyId, out _);
    }

    private static string NameOf(IMetadataMember m) => m switch
    {
        IFullNameProvider fn => fn.FullName,
        INameProvider n => n.Name?.ToString() ?? "?",
        _ => m.ToString() ?? "?",
    };
}

public sealed record AssemblyRefInfo(
    string Token, string Name, string Version, string Culture,
    string? PublicKeyToken, bool HasHash);

public sealed record ModuleRefInfo(string Token, string Name);
public sealed record TypeRefInfo(string Token, string FullName, string Scope);
public sealed record MemberRefInfo(string Token, string FullName, string DeclaringType);

public sealed record XrefEdge(
    string FromToken, string FromName, string ToToken, string ToName,
    string Opcode, string Offset);

public sealed record CallGraphNode(string Token, string FullName, List<CallGraphNode> Callees);

internal sealed class XrefIndex
{
    public Dictionary<uint, List<XrefEdge>> IncomingByToken { get; } = new();

    public static XrefIndex Build(Workspace.Workspace ws)
    {
        var idx = new XrefIndex();
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var method in t.Methods)
            {
                var body = method.CilMethodBody;
                if (body is null) continue;
                foreach (var instr in body.Instructions)
                {
                    if (instr.Operand is not IMetadataMember mm) continue;
                    var k = mm.MetadataToken.ToUInt32();
                    if (!idx.IncomingByToken.TryGetValue(k, out var list))
                        idx.IncomingByToken[k] = list = new();
                    list.Add(new XrefEdge(
                        FromToken: TokenFormatHelper.Hex(method),
                        FromName: method.FullName,
                        ToToken: TokenFormatHelper.Hex(mm),
                        ToName: NameOf(mm),
                        Opcode: instr.OpCode.Mnemonic,
                        Offset: $"IL_{instr.Offset:X4}"));
                }
            }
        }
        return idx;
    }

    private static string NameOf(IMetadataMember m) => m switch
    {
        IFullNameProvider fn => fn.FullName,
        INameProvider n => n.Name?.ToString() ?? "?",
        _ => m.ToString() ?? "?",
    };
}
