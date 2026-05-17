using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Inspection;

[Collection("SyntheticCorpus")]
public sealed class MethodListTests
{
    private readonly SyntheticCorpusFixture _corpus;

    public MethodListTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private (Core.Workspace.Workspace ws, MethodListService svc) Load()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        var ws = reg.Open(_corpus.Get("Clean"));
        return (ws, new MethodListService(new TokenResolver()));
    }

    [Fact]
    public void List_methods_finds_greeter_methods()
    {
        var (ws, svc) = Load();
        var page = svc.List(ws, null, null, null, 0, 1000);
        var names = page.Items.Select(m => $"{m.DeclaringType}::{m.Name}").ToList();
        Assert.Contains("Acme.Clean.Greeter::Greet", names);
        Assert.Contains("Acme.Clean.Greeter::Add", names);
    }

    [Fact]
    public void List_methods_filter_by_type()
    {
        var (ws, svc) = Load();
        var page = svc.List(ws, null, type: "Acme.Clean.Greeter", null, 0, 1000);
        Assert.All(page.Items, m => Assert.Equal("Acme.Clean.Greeter", m.DeclaringType));
        Assert.Contains(page.Items, m => m.Name == "Greet");
    }

    [Fact]
    public void Pagination_boundary_sets_has_more()
    {
        var (ws, svc) = Load();
        var first = svc.List(ws, null, null, null, offset: 0, limit: 2);
        Assert.Equal(2, first.Items.Count);
        Assert.True(first.Total > 2);
        Assert.True(first.HasMore);

        var second = svc.List(ws, null, null, null, offset: 2, limit: 2);
        Assert.True(second.Items.Count <= 2);
        Assert.Equal(first.Total, second.Total);
    }

    [Fact]
    public void Modifier_static_filters_correctly()
    {
        var (ws, svc) = Load();
        var statics = svc.List(ws, null, null, modifier: "static", 0, 1000);
        Assert.Contains(statics.Items, m => m.Name == "Add");
        Assert.DoesNotContain(statics.Items, m => m.Name == "Greet");
    }
}
