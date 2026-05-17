using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Inspection;

public sealed class TypeDetailService
{
    private readonly TokenResolver _tokens;
    public TypeDetailService(TokenResolver tokens) => _tokens = tokens;

    public TypeDetail GetType(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not TypeDefinition t)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a TypeDefinition");

        return new TypeDetail(
            Token: TokenFormatHelper.Hex(t),
            FullName: t.FullName,
            Name: t.Name?.ToString() ?? "",
            Namespace: t.Namespace?.ToString() ?? "",
            Kind: TypeListService.KindOf(t),
            Visibility: TypeListService.VisOf(t),
            BaseType: t.BaseType?.FullName,
            Interfaces: t.Interfaces.Select(i => i.Interface?.FullName ?? "").ToList(),
            Methods: t.Methods.Select(mm => new TokenName(TokenFormatHelper.Hex(mm), mm.Name?.ToString() ?? "")).ToList(),
            Fields: t.Fields.Select(f => new TokenName(TokenFormatHelper.Hex(f), f.Name?.ToString() ?? "")).ToList(),
            Properties: t.Properties.Select(p => new TokenName(TokenFormatHelper.Hex(p), p.Name?.ToString() ?? "")).ToList(),
            Events: t.Events.Select(e => new TokenName(TokenFormatHelper.Hex(e), e.Name?.ToString() ?? "")).ToList(),
            NestedTypes: t.NestedTypes.Select(nt => new TokenName(TokenFormatHelper.Hex(nt), nt.FullName)).ToList(),
            HasCustomAttributes: t.CustomAttributes.Count > 0,
            CustomAttributeCount: t.CustomAttributes.Count);
    }

    public TypeLayout GetTypeLayout(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not TypeDefinition t)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a TypeDefinition");

        var layoutKind =
            t.IsExplicitLayout ? "explicit" :
            t.IsSequentialLayout ? "sequential" : "auto";

        var fields = t.Fields.Select(f => new FieldLayoutEntry(
            Token: TokenFormatHelper.Hex(f),
            Name: f.Name?.ToString() ?? "",
            Type: f.Signature?.FieldType?.FullName ?? "?",
            Offset: f.FieldOffset)).ToList();

        return new TypeLayout(
            Token: TokenFormatHelper.Hex(t),
            FullName: t.FullName,
            LayoutKind: layoutKind,
            PackingSize: t.ClassLayout?.PackingSize,
            ClassSize: t.ClassLayout is null ? null : (uint?)t.ClassLayout.ClassSize,
            Fields: fields);
    }

    public IReadOnlyList<NamespaceInfo> ListNamespaces(Workspace.Workspace ws)
    {
        var groups = ws.Module.GetAllTypes()
            .Where(t => !t.IsModuleType)
            .GroupBy(t => t.Namespace?.ToString() ?? "");
        return groups
            .OrderBy(g => g.Key, StringComparer.Ordinal)
            .Select(g => new NamespaceInfo(g.Key, g.Count()))
            .ToList();
    }
}

public sealed record TypeDetail(
    string Token, string FullName, string Name, string Namespace,
    string Kind, string Visibility, string? BaseType,
    IReadOnlyList<string> Interfaces,
    IReadOnlyList<TokenName> Methods,
    IReadOnlyList<TokenName> Fields,
    IReadOnlyList<TokenName> Properties,
    IReadOnlyList<TokenName> Events,
    IReadOnlyList<TokenName> NestedTypes,
    bool HasCustomAttributes,
    int CustomAttributeCount);

public sealed record TokenName(string Token, string Name);

public sealed record TypeLayout(
    string Token, string FullName, string LayoutKind,
    int? PackingSize, uint? ClassSize, IReadOnlyList<FieldLayoutEntry> Fields);

public sealed record FieldLayoutEntry(string Token, string Name, string Type, int? Offset);

public sealed record NamespaceInfo(string Namespace, int TypeCount);
