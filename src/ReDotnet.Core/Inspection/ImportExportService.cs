using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;

namespace ReDotnet.Core.Inspection;

public sealed class ImportExportService
{
    public Page<PInvokeEntry> ListPInvokes(Workspace.Workspace ws, string? moduleFilter, int? offset, int? limit)
    {
        var all = new List<PInvokeEntry>();
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var m in t.Methods)
            {
                var map = m.ImplementationMap;
                if (map is null) continue;
                var moduleName = map.Scope?.Name?.ToString() ?? "";
                if (moduleFilter is not null && moduleName.IndexOf(moduleFilter, StringComparison.OrdinalIgnoreCase) < 0)
                    continue;
                all.Add(new PInvokeEntry(
                    Token: TokenFormatHelper.Hex(m),
                    Method: m.FullName,
                    Module: moduleName,
                    EntryPoint: map.Name?.ToString() ?? "",
                    CallingConvention: map.Attributes.ToString(),
                    CharSet: ExtractCharset(map.Attributes)));
            }
        }
        return Page<PInvokeEntry>.From(all, offset, limit);
    }

    public Page<UnmanagedExportEntry> ListUnmanagedExports(Workspace.Workspace ws, int? offset, int? limit)
    {
        var all = new List<UnmanagedExportEntry>();
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var m in t.Methods)
            {
                if (m.ExportInfo is { } ei)
                {
                    all.Add(new UnmanagedExportEntry(
                        Token: TokenFormatHelper.Hex(m),
                        Method: m.FullName,
                        ExportName: ei.Name ?? "",
                        Ordinal: ei.HasFixedOrdinal ? ei.Ordinal : (uint?)null));
                }
            }
        }
        return Page<UnmanagedExportEntry>.From(all, offset, limit);
    }

    public Page<TypeForwarderEntry> ListTypeForwarders(Workspace.Workspace ws, int? offset, int? limit)
    {
        var all = new List<TypeForwarderEntry>();
        foreach (var et in ws.Module.ExportedTypes)
        {
            all.Add(new TypeForwarderEntry(
                Token: TokenFormatHelper.Hex(et),
                FullName: et.FullName,
                Target: et.Implementation?.FullName ?? "?"));
        }
        return Page<TypeForwarderEntry>.From(all, offset, limit);
    }

    private static string ExtractCharset(AsmResolver.PE.DotNet.Metadata.Tables.ImplementationMapAttributes attrs)
    {
        if ((attrs & AsmResolver.PE.DotNet.Metadata.Tables.ImplementationMapAttributes.CharSetUnicode) != 0) return "unicode";
        if ((attrs & AsmResolver.PE.DotNet.Metadata.Tables.ImplementationMapAttributes.CharSetAnsi) != 0) return "ansi";
        if ((attrs & AsmResolver.PE.DotNet.Metadata.Tables.ImplementationMapAttributes.CharSetAuto) != 0) return "auto";
        return "none";
    }
}

public sealed record PInvokeEntry(
    string Token, string Method, string Module, string EntryPoint,
    string CallingConvention, string CharSet);

public sealed record UnmanagedExportEntry(
    string Token, string Method, string ExportName, uint? Ordinal);

public sealed record TypeForwarderEntry(string Token, string FullName, string Target);
