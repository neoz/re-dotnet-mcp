using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Search;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class SearchTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly StringService _strings;
    private readonly IlSearchService _il;
    private readonly DangerousApiService _danger;

    public SearchTools(
        WorkspaceRegistry registry,
        StringService strings,
        IlSearchService il,
        DangerousApiService danger)
    {
        _registry = registry;
        _strings = strings;
        _il = il;
        _danger = danger;
    }

    [McpServerTool(Name = "list_user_strings")]
    [Description("List entries in the #US heap (user strings).")]
    public PageDto<UserStringEntry> ListUserStrings(
        [Description("Assembly id.")] string id,
        string? filter_regex = null,
        [Description("Minimum string length to include (default 4).")] int min_length = 4,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<UserStringEntry>.From(_strings.ListUserStrings(ws, filter_regex, min_length, offset, limit)));

    [McpServerTool(Name = "list_ldstr_strings")]
    [Description("List strings referenced by ldstr IL operands, with the methods that reference each.")]
    public PageDto<LdstrEntry> ListLdstrStrings(
        [Description("Assembly id.")] string id,
        string? filter_regex = null, int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<LdstrEntry>.From(_strings.ListLdstrStrings(ws, filter_regex, offset, limit)));

    [McpServerTool(Name = "find_code_by_string")]
    [Description("Find methods whose IL contains ldstr matching the given regex.")]
    public PageDto<CodeStringMatch> FindCodeByString(
        [Description("Assembly id.")] string id,
        [Description("Regex applied to the literal string content.")] string regex,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<CodeStringMatch>.From(_strings.FindCodeByString(ws, regex, offset, limit)));

    [McpServerTool(Name = "search_il")]
    [Description("Regex over IL mnemonic + resolved operand text (per instruction).")]
    public PageDto<IlSearchHit> SearchIl(
        [Description("Assembly id.")] string id,
        [Description("Regex applied to '<opcode> <operand>' per instruction.")] string pattern,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<IlSearchHit>.From(_il.SearchIl(ws, pattern, offset, limit)));

    [McpServerTool(Name = "search_bytes")]
    [Description("Byte-pattern search inside one or all PE sections.")]
    public PageDto<BytesHit> SearchBytes(
        [Description("Assembly id.")] string id,
        [Description("Hex pattern (whitespace optional, e.g. 'DE AD BE EF').")] string hex_pattern,
        [Description("Optional section name substring filter.")] string? section = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<BytesHit>.From(_il.SearchBytes(ws, hex_pattern, section, offset, limit)));

    [McpServerTool(Name = "find_dangerous_apis")]
    [Description("Scan IL for calls matching dangerous-API rules. Optional preset filter.")]
    public PageDto<DangerousApiHit> FindDangerousApis(
        [Description("Assembly id.")] string id,
        [Description("Preset: deserialization|process|reflection|crypto|network|file.")] string? preset = null,
        int? offset = null, int? limit = null)
        => _registry.Run(id, ws => PageDto<DangerousApiHit>.From(_danger.Find(ws, preset, offset, limit)));
}
