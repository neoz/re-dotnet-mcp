using ReDotnet.Core.Inspection;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Inspection;

[Collection("SyntheticCorpus")]
public sealed class TypeListTests
{
    private readonly SyntheticCorpusFixture _corpus;

    public TypeListTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private Core.Workspace.Workspace LoadClean()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        return reg.Open(_corpus.Get("Clean"));
    }

    [Fact]
    public void List_includes_user_types()
    {
        var ws = LoadClean();
        var page = new TypeListService().List(ws, null, null, null, null, null, 1000);
        var names = page.Items.Select(t => t.FullName).ToList();

        Assert.Contains("Acme.Clean.Greeter", names);
        Assert.Contains("Acme.Clean.Point", names);
        Assert.Contains("Acme.Clean.Color", names);
        Assert.Contains("Acme.Clean.IShape", names);
        Assert.Contains("Acme.Clean.Circle", names);
    }

    [Fact]
    public void Namespace_filter_restricts_results()
    {
        var ws = LoadClean();
        var page = new TypeListService().List(ws, null, @namespace: "Acme.Clean", null, null, null, 1000);
        Assert.All(page.Items, t => Assert.Equal("Acme.Clean", t.Namespace));
    }

    [Fact]
    public void Kind_filter_distinguishes_enum_and_class()
    {
        var svc = new TypeListService();
        var ws = LoadClean();
        var enums = svc.List(ws, null, null, kind: "enum", null, null, 1000);
        Assert.Contains(enums.Items, t => t.FullName == "Acme.Clean.Color");
        Assert.DoesNotContain(enums.Items, t => t.FullName == "Acme.Clean.Greeter");

        var classes = svc.List(ws, null, null, kind: "class", null, null, 1000);
        Assert.Contains(classes.Items, t => t.FullName == "Acme.Clean.Greeter");
        Assert.DoesNotContain(classes.Items, t => t.FullName == "Acme.Clean.Color");
    }

    [Fact]
    public void Filter_regex_matches_full_name()
    {
        var ws = LoadClean();
        var page = new TypeListService().List(ws, filterRegex: "^Acme\\.Clean\\.G", null, null, null, null, 1000);
        Assert.Contains(page.Items, t => t.FullName == "Acme.Clean.Greeter");
        Assert.DoesNotContain(page.Items, t => t.FullName == "Acme.Clean.Circle");
    }
}
