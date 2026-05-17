using ReDotnet.Core.Envelope;

namespace ReDotnet.Server.Envelope;

// Wire-shape mirrors of Core envelopes. Keep these snake-cased for parity with
// re-mcp and stable across the protocol.

public sealed record PageDto<T>(
    IReadOnlyList<T> items,
    int offset,
    int limit,
    int total,
    bool has_more)
{
    public static PageDto<T> From(Page<T> p) =>
        new(p.Items, p.Offset, p.Limit, p.Total, p.HasMore);
}

public sealed record AssemblyInfoDto(
    string assembly_id,
    string path,
    string name,
    string runtime,
    string? target_framework,
    string machine,
    string characteristics,
    bool has_strong_name,
    bool has_authenticode,
    bool has_pdb,
    string mvid,
    IReadOnlyList<string> streams,
    string? entry_point);

public sealed record TypeSummaryDto(
    string token,
    string name,
    string @namespace,
    string full_name,
    string kind,
    string visibility,
    int method_count,
    int field_count);

public sealed record MethodSummaryDto(
    string token,
    string name,
    string declaring_type,
    string signature,
    bool is_static,
    bool is_virtual,
    bool is_abstract,
    string visibility,
    int? code_size);

public sealed record OpenAssemblyResultDto(
    string assembly_id,
    string path,
    string mvid);

public sealed record CloseAssemblyResultDto(
    string assembly_id,
    bool closed,
    bool saved_sidecar);

public sealed record ResolveTokenResultDto(
    string token,
    string kind,
    string name,
    string? full_name);
