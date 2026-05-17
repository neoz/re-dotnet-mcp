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
            Constant: f.Constant?.Value?.InterpretData(f.Constant.Type).ToString());
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

public sealed record PropertySummary(
    string Token, string Name, string DeclaringType, string Type,
    string? GetterToken, string? SetterToken);

public sealed record EventSummary(
    string Token, string Name, string DeclaringType, string HandlerType,
    string? AddToken, string? RemoveToken);
