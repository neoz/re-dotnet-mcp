using AsmResolver;
using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Inspection;

public sealed class MemberInspectionService
{
    private readonly TokenResolver _tokens;
    public MemberInspectionService(TokenResolver tokens) => _tokens = tokens;

    public Page<FieldSummary> ListFields(Workspace.Workspace ws, string? type, int? offset, int? limit)
    {
        IEnumerable<TypeDefinition> types = type is null
            ? ws.Module.GetAllTypes().Where(t => !t.IsModuleType)
            : new[] { ResolveType(ws, type) };

        var all = types.SelectMany(t => t.Fields.Select(f => BuildField(f, t))).ToList();
        return Page<FieldSummary>.From(all, offset, limit);
    }

    public FieldDetail GetField(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not FieldDefinition f)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a FieldDefinition");
        return new FieldDetail(
            Token: TokenFormatHelper.Hex(f),
            Name: f.Name?.ToString() ?? "",
            DeclaringType: f.DeclaringType?.FullName ?? "<global>",
            Type: f.Signature?.FieldType?.FullName ?? "?",
            IsStatic: f.IsStatic,
            IsLiteral: f.IsLiteral,
            IsInitOnly: f.IsInitOnly,
            Visibility: VisOf(f),
            Constant: f.Constant is { } c ? c.Value?.InterpretData(c.Type)?.ToString() : null);
    }

    public FieldRvaData GetFieldRvaData(Workspace.Workspace ws, string identifier, int maxBytes)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not FieldDefinition f)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a FieldDefinition");
        if (!f.HasFieldRva || f.FieldRva is not IReadableSegment seg)
            throw BackendError.NotFound("field_rva", identifier);

        // Logical size is the field's value-type ClassLayout.ClassSize when present
        // (e.g. <PrivateImplementationDetails>+__StaticArrayInitTypeSize=N). The
        // physical segment may carry trailing alignment padding.
        int? declared = null;
        // The static-array-init helper types are emitted in-module, so the underlying
        // ITypeDefOrRef is a TypeDefinition we can read directly without a runtime resolve.
        if (f.Signature?.FieldType?.GetUnderlyingTypeDefOrRef() is TypeDefinition typeDef
            && typeDef.ClassLayout is { } cl && cl.ClassSize > 0)
            declared = (int)cl.ClassSize;

        var data = seg.ToArray();
        var cap = Math.Max(0, maxBytes);
        var capped = data.Length > cap ? data[..cap] : data;
        return new FieldRvaData(
            Token: TokenFormatHelper.Hex(f),
            Name: f.Name?.ToString() ?? "",
            DeclaringType: f.DeclaringType?.FullName ?? "<global>",
            DeclaredSize: declared,
            TotalBytes: data.Length,
            BytesReturned: capped.Length,
            Truncated: capped.Length < data.Length,
            HexPreview: Convert.ToHexString(capped[..Math.Min(64, capped.Length)]),
            DataB64: Convert.ToBase64String(capped));
    }

    public Page<PropertySummary> ListProperties(Workspace.Workspace ws, string? type, int? offset, int? limit)
    {
        IEnumerable<TypeDefinition> types = type is null
            ? ws.Module.GetAllTypes().Where(t => !t.IsModuleType)
            : new[] { ResolveType(ws, type) };

        var all = types.SelectMany(t => t.Properties.Select(p => new PropertySummary(
            Token: TokenFormatHelper.Hex(p),
            Name: p.Name?.ToString() ?? "",
            DeclaringType: t.FullName,
            Type: p.Signature?.ReturnType?.FullName ?? "?",
            GetterToken: p.GetMethod is { } gm ? TokenFormatHelper.Hex(gm) : null,
            SetterToken: p.SetMethod is { } sm ? TokenFormatHelper.Hex(sm) : null))).ToList();
        return Page<PropertySummary>.From(all, offset, limit);
    }

    public PropertySummary GetProperty(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not PropertyDefinition p)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a PropertyDefinition");
        return new PropertySummary(
            Token: TokenFormatHelper.Hex(p),
            Name: p.Name?.ToString() ?? "",
            DeclaringType: p.DeclaringType?.FullName ?? "<global>",
            Type: p.Signature?.ReturnType?.FullName ?? "?",
            GetterToken: p.GetMethod is { } gm ? TokenFormatHelper.Hex(gm) : null,
            SetterToken: p.SetMethod is { } sm ? TokenFormatHelper.Hex(sm) : null);
    }

    public Page<EventSummary> ListEvents(Workspace.Workspace ws, string? type, int? offset, int? limit)
    {
        IEnumerable<TypeDefinition> types = type is null
            ? ws.Module.GetAllTypes().Where(t => !t.IsModuleType)
            : new[] { ResolveType(ws, type) };

        var all = types.SelectMany(t => t.Events.Select(e => new EventSummary(
            Token: TokenFormatHelper.Hex(e),
            Name: e.Name?.ToString() ?? "",
            DeclaringType: t.FullName,
            HandlerType: e.EventType?.FullName ?? "?",
            AddToken: e.AddMethod is { } am ? TokenFormatHelper.Hex(am) : null,
            RemoveToken: e.RemoveMethod is { } rm ? TokenFormatHelper.Hex(rm) : null))).ToList();
        return Page<EventSummary>.From(all, offset, limit);
    }

    public EventSummary GetEvent(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not EventDefinition e)
            throw BackendError.BadInput($"'{identifier}' did not resolve to an EventDefinition");
        return new EventSummary(
            Token: TokenFormatHelper.Hex(e),
            Name: e.Name?.ToString() ?? "",
            DeclaringType: e.DeclaringType?.FullName ?? "<global>",
            HandlerType: e.EventType?.FullName ?? "?",
            AddToken: e.AddMethod is { } am ? TokenFormatHelper.Hex(am) : null,
            RemoveToken: e.RemoveMethod is { } rm ? TokenFormatHelper.Hex(rm) : null);
    }

    private static FieldSummary BuildField(FieldDefinition f, TypeDefinition t) =>
        new(Token: TokenFormatHelper.Hex(f),
            Name: f.Name?.ToString() ?? "",
            DeclaringType: t.FullName,
            Type: f.Signature?.FieldType?.FullName ?? "?",
            IsStatic: f.IsStatic,
            Visibility: VisOf(f));

    private static string VisOf(FieldDefinition f)
    {
        if (f.IsPublic) return "public";
        if (f.IsAssembly) return "internal";
        if (f.IsFamily) return "protected";
        if (f.IsFamilyOrAssembly) return "protected_or_internal";
        if (f.IsFamilyAndAssembly) return "protected_and_internal";
        if (f.IsPrivate) return "private";
        return "unknown";
    }

    private TypeDefinition ResolveType(Workspace.Workspace ws, string id)
    {
        var m = _tokens.Resolve(ws, id);
        if (m is not TypeDefinition t)
            throw BackendError.BadInput($"'{id}' did not resolve to a TypeDefinition");
        return t;
    }
}

public sealed record FieldSummary(
    string Token, string Name, string DeclaringType, string Type, bool IsStatic, string Visibility);

public sealed record FieldDetail(
    string Token, string Name, string DeclaringType, string Type,
    bool IsStatic, bool IsLiteral, bool IsInitOnly, string Visibility, string? Constant);

public sealed record FieldRvaData(
    string Token, string Name, string DeclaringType,
    int? DeclaredSize, int TotalBytes, int BytesReturned, bool Truncated,
    string HexPreview, string DataB64);

public sealed record PropertySummary(
    string Token, string Name, string DeclaringType, string Type,
    string? GetterToken, string? SetterToken);

public sealed record EventSummary(
    string Token, string Name, string DeclaringType, string HandlerType,
    string? AddToken, string? RemoveToken);
