using AsmResolver.PE;
using AsmResolver.PE.DotNet.Metadata;
using AsmResolver.PE.DotNet.Metadata.Tables;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Lifecycle;

[Collection("SyntheticCorpus")]
public sealed class LifecycleTests
{
    private readonly SyntheticCorpusFixture _corpus;

    public LifecycleTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private WorkspaceRegistry NewRegistry() =>
        new(new ModuleResolverFactory(null), new SidecarStore());

    [Fact]
    public void Open_then_list_returns_workspace()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        Assert.Equal("Clean", ws.AssemblyId);
        Assert.Single(reg.All());
    }

    [Fact]
    public void Reopen_same_path_returns_same_workspace()
    {
        var reg = NewRegistry();
        var ws1 = reg.Open(_corpus.Get("Clean"));
        var ws2 = reg.Open(_corpus.Get("Clean"));
        Assert.Same(ws1, ws2);
        Assert.Single(reg.All());
    }

    [Fact]
    public void Close_evicts_workspace()
    {
        var reg = NewRegistry();
        reg.Open(_corpus.Get("Clean"));
        Assert.True(reg.Close("Clean", save: false));
        Assert.Empty(reg.All());
    }

    [Fact]
    public void Close_disposes_workspace_and_releases_file_handle()
    {
        // Copy the corpus DLL to a scratch path so we can prove the file is
        // unlocked after Close by deleting it. Deleting a memory-mapped file
        // on Windows throws IOException if the mapping is still held, so a
        // successful Delete is direct evidence that Close released the handle.
        var scratch = Path.Combine(Path.GetTempPath(), $"redotnet-close-{Guid.NewGuid():N}.dll");
        File.Copy(_corpus.Get("Clean"), scratch);
        try
        {
            var reg = NewRegistry();
            var ws = reg.Open(scratch);
            Assert.False(ws.IsDisposed);

            Assert.True(reg.Close(ws.AssemblyId, save: false));
            Assert.True(ws.IsDisposed);

            File.Delete(scratch);
            Assert.False(File.Exists(scratch));
        }
        finally
        {
            if (File.Exists(scratch)) File.Delete(scratch);
        }
    }

    [Fact]
    public void Close_invalidates_xref_index_for_evicted_id()
    {
        // Without invalidation, ReferenceService would return an XrefIndex
        // built against the disposed module on the next call after re-open,
        // dereferencing freed AsmResolver members.
        var reg = NewRegistry();
        var refs = new ReferenceService(new TokenResolver());
        reg.WorkspaceEvicting += refs.InvalidateIndex;

        var ws1 = reg.Open(_corpus.Get("Clean"));
        var before = refs.GetXrefsTo(ws1, "Acme.Clean.Greeter::Greet", null, null);
        Assert.NotNull(before);

        Assert.True(reg.Close(ws1.AssemblyId, save: false));

        var ws2 = reg.Open(_corpus.Get("Clean"));
        Assert.NotSame(ws1, ws2);
        // Must succeed without ObjectDisposedException from the stale index.
        var after = refs.GetXrefsTo(ws2, "Acme.Clean.Greeter::Greet", null, null);
        Assert.NotNull(after);
    }

    [Fact]
    public void GetAssemblyInfo_returns_expected_fields()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        var info = new AssemblyInfoService().Capture(ws);

        Assert.Equal("Clean", info.AssemblyId);
        Assert.Equal("Clean", info.Name);
        Assert.False(string.IsNullOrEmpty(info.Mvid));
        Assert.Contains("#~", info.Streams.ToList());
        Assert.Contains("#Strings", info.Streams.ToList());
    }

    [Fact]
    public void Open_assembly_with_hostile_corlib_ref_version_succeeds()
    {
        // Obfuscators (.NET Reactor among them) set the corlib AssemblyRef
        // version to 65535.65535.65535.65535. AsmResolver's runtime prober
        // picks the highest corlib version it sees, maps anything >= 5 to
        // .NET Core, then throws while selecting an implementation corlib for
        // that impossible version. Opening must not depend on that probe.
        var scratch = Path.Combine(Path.GetTempPath(), $"redotnet-hostile-{Guid.NewGuid():N}.dll");
        File.Copy(_corpus.Get("Clean"), scratch);
        var reg = NewRegistry();
        try
        {
            MaxOutCorLibRefVersion(scratch);

            var ws = reg.Open(scratch);
            Assert.Contains(ws.Module.GetAllTypes(), t => t.Name == "Greeter");
            reg.Close(ws.AssemblyId, save: false);
        }
        finally
        {
            try { if (File.Exists(scratch)) File.Delete(scratch); }
            catch { /* leave the scratch file; the OS temp dir reclaims it */ }
        }
    }

    [Fact]
    public void Parse_diagnostics_are_empty_for_a_clean_assembly()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        TouchEveryMember(ws);

        var info = new AssemblyInfoService();
        Assert.Equal(0, info.Capture(ws).ParseWarnings);
        Assert.Empty(info.ListParseDiagnostics(ws, null, null).Items);
    }

    [Fact]
    public void Parse_diagnostics_are_paged_for_corrupt_metadata()
    {
        // AsmResolver's default error listener swallows recoverable metadata
        // damage, so a protected assembly can look intact. The workspace keeps
        // a DiagnosticBag instead: get_assembly_info carries the count and the
        // messages come back through a paged listing.
        var scratch = Path.Combine(Path.GetTempPath(), $"redotnet-corrupt-{Guid.NewGuid():N}.dll");
        File.Copy(_corpus.Get("Clean"), scratch);
        var reg = NewRegistry();
        try
        {
            CorruptFirstMethodSignatureBlobIndex(scratch);

            var ws = reg.Open(scratch);
            TouchEveryMember(ws);

            var info = new AssemblyInfoService();
            var page = info.ListParseDiagnostics(ws, null, null);

            Assert.Equal(info.Capture(ws).ParseWarnings, page.Total);
            Assert.Contains(page.Items, d => d.Contains("signature blob index"));
            reg.Close(ws.AssemblyId, save: false);
        }
        finally
        {
            try { if (File.Exists(scratch)) File.Delete(scratch); }
            catch { /* leave the scratch file; the OS temp dir reclaims it */ }
        }
    }

    /// AsmResolver reads members lazily, so diagnostics only accumulate once
    /// something touches them. Walking the members is what a real inspection
    /// session does before asking for assembly info.
    private static void TouchEveryMember(Core.Workspace.Workspace ws)
    {
        foreach (var type in ws.Module.GetAllTypes())
        foreach (var method in type.Methods)
        {
            try { _ = method.Signature?.ToString(); } catch { /* recorded in the bag */ }
            try { _ = method.CilMethodBody; } catch { /* recorded in the bag */ }
        }
    }

    /// Points the first MethodDef row's signature at a blob index of 0xFF..,
    /// which is past the end of the #Blob heap. AsmResolver reports this to the
    /// error listener rather than throwing.
    private static void CorruptFirstMethodSignatureBlobIndex(string path)
    {
        long signatureOffset;
        int signatureSize;
        {
            var metadata = PEImage.FromFile(path).DotNetDirectory!.Metadata!;
            var table = metadata.GetStream<TablesStream>()
                .GetTable<MethodDefinitionRow>(TableIndex.Method);

            // Row layout: Rva(4) ImplAttributes(2) Attributes(2) Name Signature ParamList
            var nameSize = table.Layout.Columns[3].Size;
            signatureSize = (int)table.Layout.Columns[4].Size;
            signatureOffset = (long)table.Offset + 4 + 2 + 2 + nameSize;
        }

        var bytes = File.ReadAllBytes(path);
        for (var i = 0; i < signatureSize; i++) bytes[signatureOffset + i] = 0xFF;
        File.WriteAllBytes(path, bytes);
    }

    /// Overwrites the four version fields of the corlib AssemblyRef row with
    /// 0xFFFF. They are the first eight bytes of the row, so the patch is
    /// size-preserving and leaves the rest of the metadata untouched.
    private static void MaxOutCorLibRefVersion(string path)
    {
        long rowOffset;
        {
            var metadata = PEImage.FromFile(path).DotNetDirectory!.Metadata!;
            var strings = metadata.GetStream<StringsStream>();
            var table = metadata.GetStream<TablesStream>()
                .GetTable<AssemblyReferenceRow>(TableIndex.AssemblyRef);

            var index = -1;
            for (var i = 0; i < table.Count; i++)
            {
                var name = strings.GetStringByIndex(table[i].Name)?.ToString();
                if (name is "mscorlib" or "System.Runtime" or "netstandard" or "System.Private.CoreLib")
                {
                    index = i;
                    break;
                }
            }

            Assert.True(index >= 0, "corpus fixture has no corlib AssemblyRef to patch");
            rowOffset = (long)table.Offset + index * table.Layout.RowSize;
        }

        var bytes = File.ReadAllBytes(path);
        for (var i = 0; i < 8; i++) bytes[rowOffset + i] = 0xFF;
        File.WriteAllBytes(path, bytes);
    }
}
