using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata.Tables;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Server.Envelope;

public static class TokenHelpers
{
    public static string FormatToken(MetadataToken token) => TokenFormat.Format(token);

    public static string KindOf(IMetadataMember m) => m switch
    {
        TypeDefinition => "type",
        MethodDefinition => "method",
        FieldDefinition => "field",
        PropertyDefinition => "property",
        EventDefinition => "event",
        ParameterDefinition => "parameter",
        AssemblyReference => "assembly_ref",
        TypeReference => "type_ref",
        MemberReference => "member_ref",
        ModuleDefinition => "module",
        ModuleReference => "module_ref",
        _ => m.GetType().Name,
    };

    public static string VisibilityOf(TypeDefinition t)
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

    public static string VisibilityOf(MethodDefinition m)
    {
        if (m.IsPublic) return "public";
        if (m.IsAssembly) return "internal";
        if (m.IsFamily) return "protected";
        if (m.IsFamilyOrAssembly) return "protected_or_internal";
        if (m.IsFamilyAndAssembly) return "protected_and_internal";
        if (m.IsPrivate) return "private";
        return "unknown";
    }

    public static string KindOf(TypeDefinition t)
    {
        if (t.IsEnum) return "enum";
        if (t.IsInterface) return "interface";
        if (t.IsValueType) return "struct";
        if (IsDelegate(t)) return "delegate";
        return "class";
    }

    private static bool IsDelegate(TypeDefinition t)
    {
        var bt = t.BaseType?.FullName;
        return bt is "System.MulticastDelegate" or "System.Delegate";
    }
}
