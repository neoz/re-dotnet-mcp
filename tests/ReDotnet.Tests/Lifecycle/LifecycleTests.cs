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
