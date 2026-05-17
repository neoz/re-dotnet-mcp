namespace ReDotnet.Server.MetaTools;

/// Canonical list of tools that should be advertised to clients by default,
/// per PRD section 7. Other tools remain reachable through the `call` and
/// `search_tools` meta-tools.
public static class PinningRegistry
{
    public static readonly IReadOnlySet<string> Pinned = new HashSet<string>(StringComparer.Ordinal)
    {
        "open_assembly", "close_assembly", "save_assembly", "list_assemblies", "get_assembly_info",
        "list_types", "get_type", "list_methods", "get_method", "disassemble_method",
        "get_xrefs_to", "get_xrefs_from", "get_call_graph",
        "list_pinvokes", "list_assembly_refs", "list_user_strings", "find_code_by_string",
        "search_il", "find_dangerous_apis",
        "rename_type", "rename_method", "patch_il",
        "search_tools", "get_schema", "call", "batch", "execute",
    };
}
