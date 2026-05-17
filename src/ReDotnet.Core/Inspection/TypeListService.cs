using System.Text.RegularExpressions;
using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;

namespace ReDotnet.Core.Inspection;

public sealed class TypeListService
{
    public Page<TypeSummary> List(
        Workspace.Workspace ws,
        string? filterRegex,
        string? @namespace,
        string? kind,
        string? visibility,
        int? offset,
        int? limit)
    {
        Regex? rx = null;
        if (!string.IsNullOrWhiteSpace(filterRegex))
        {
            try { rx = new Regex(filterRegex!, RegexOptions.Compiled); }
            catch (ArgumentException ex)
            {
                throw BackendError.BadInput($"invalid filter regex: {ex.Message}");
            }
        }

        var all = new List<TypeSummary>();
        foreach (var t in ws.Module.GetAllTypes())
        {
            if (t.IsModuleType) continue;
            if (rx is not null && !rx.IsMatch(t.FullName)) continue;
            if (@namespace is not null && t.Namespace != @namespace) continue;

            var summary = Build(t);
            if (kind is not null && !string.Equals(summary.Kind, kind, StringComparison.OrdinalIgnoreCase)) continue;
            if (visibility is not null && !string.Equals(summary.Visibility, visibility, StringComparison.OrdinalIgnoreCase)) continue;
            all.Add(summary);
        }

        return Page<TypeSummary>.From(all, offset, limit);
    }

    public static TypeSummary Build(TypeDefinition t)
    {
        return new TypeSummary(
            Token: TokenFormatHelper.Hex(t),
            Name: t.Name?.ToString() ?? "",
            Namespace: t.Namespace?.ToString() ?? "",
            FullName: t.FullName,
            Kind: KindOf(t),
            Visibility: VisOf(t),
            MethodCount: t.Methods.Count,
            FieldCount: t.Fields.Count);
    }

    public static string KindOf(TypeDefinition t)
    {
        if (t.IsEnum) return "enum";
        if (t.IsInterface) return "interface";
        if (t.IsValueType) return "struct";
        var bt = t.BaseType?.FullName;
        if (bt is "System.MulticastDelegate" or "System.Delegate") return "delegate";
        return "class";
    }

    public static string VisOf(TypeDefinition t)
    {
        if (t.IsPublic) return "public";
        if (t.IsNotPublic) return "internal";
        if (t.IsNestedPublic) return "nested_public";
        if (t.IsNestedPrivate) return "nested_private";
        if (t.IsNestedFamily) return "nested_protected";
        if (t.IsNestedAssembly) return "nested_internal";
        if (t.IsNestedFamilyAndAssembly) return "nested_protected_and_internal";
        if (t.IsNestedFamilyOrAssembly) return "nested_protected_or_internal";
        return "unknown";
    }
}

public sealed record TypeSummary(
    string Token,
    string Name,
    string Namespace,
    string FullName,
    string Kind,
    string Visibility,
    int MethodCount,
    int FieldCount);

internal static class TokenFormatHelper
{
    public static string Hex(IMetadataMember m) => $"0x{m.MetadataToken.ToUInt32():X8}";
}
