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
}
