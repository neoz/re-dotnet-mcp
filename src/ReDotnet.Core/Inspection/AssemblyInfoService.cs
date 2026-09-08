using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Metadata;
using AsmResolver.PE.File;
using ReDotnet.Core.Envelope;

namespace ReDotnet.Core.Inspection;

public sealed class AssemblyInfoService
{
    public AssemblyInfoSnapshot Capture(Workspace.Workspace ws)
    {
        var m = ws.Module;
        var asm = m.Assembly;
        var pe = m.FilePath != null && File.Exists(m.FilePath)
            ? AsmResolver.PE.File.PEFile.FromFile(m.FilePath)
            : null;

        var tfm = asm?.CustomAttributes
            .FirstOrDefault(c => c.Constructor?.DeclaringType?.FullName ==
                "System.Runtime.Versioning.TargetFrameworkAttribute")
            ?.Signature?.FixedArguments.FirstOrDefault()?.Element?.ToString();

        var streams = m.DotNetDirectory?.Metadata?.Streams.Select(s => s.Name).ToList()
            ?? new List<string>();

        return new AssemblyInfoSnapshot(
            AssemblyId: ws.AssemblyId,
            Path: ws.OriginalPath,
            Name: asm?.Name ?? m.Name ?? "<anonymous>",
            Runtime: m.RuntimeVersion ?? "",
            TargetFramework: tfm,
            Machine: pe?.FileHeader.Machine.ToString() ?? "Unknown",
            Characteristics: pe?.FileHeader.Characteristics.ToString() ?? "",
            HasStrongName: asm?.HasPublicKey == true,
            HasAuthenticode: pe?.OptionalHeader.GetDataDirectory(DataDirectoryIndex.CertificateDirectory).Size > 0,
            HasPdb: m.DebugData?.Count > 0,
            Mvid: m.Mvid.ToString("D"),
            Streams: streams,
            EntryPoint: m.ManagedEntryPointMethod is { } ep
                ? $"0x{ep.MetadataToken.ToUInt32():X8}" : null,
            ParseWarnings: ws.Diagnostics.Exceptions.Count);
    }

    /// Messages behind <see cref="AssemblyInfoSnapshot.ParseWarnings"/>. Paged
    /// because a heavily damaged assembly can accumulate thousands of them.
    public Page<string> ListParseDiagnostics(Workspace.Workspace ws, int? offset, int? limit)
    {
        var all = ws.Diagnostics.Exceptions.Select(e => e.Message).ToList();
        return Page<string>.From(all, offset, limit);
    }
}

public sealed record AssemblyInfoSnapshot(
    string AssemblyId,
    string Path,
    string Name,
    string Runtime,
    string? TargetFramework,
    string Machine,
    string Characteristics,
    bool HasStrongName,
    bool HasAuthenticode,
    bool HasPdb,
    string Mvid,
    IReadOnlyList<string> Streams,
    string? EntryPoint,
    int ParseWarnings);
