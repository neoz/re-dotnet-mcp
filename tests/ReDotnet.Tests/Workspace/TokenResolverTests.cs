using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Workspace;

[Collection("SyntheticCorpus")]
public sealed class TokenResolverTests
{
    private readonly SyntheticCorpusFixture _corpus;

    public TokenResolverTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    [Fact]
    public void Fqn_resolves_to_type()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        var ws = reg.Open(_corpus.Get("Clean"));
        var r = new TokenResolver();
        var t = r.Resolve(ws, "Acme.Clean.Greeter");
        Assert.IsAssignableFrom<TypeDefinition>(t);
        Assert.Equal("Greeter", ((TypeDefinition)t).Name);
    }

    [Fact]
    public void Hex_token_resolves_back_to_same_member()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        var ws = reg.Open(_corpus.Get("Clean"));
        var r = new TokenResolver();

        var t = (TypeDefinition)r.Resolve(ws, "Acme.Clean.Greeter");
        var hex = $"0x{t.MetadataToken.ToUInt32():X8}";
        var again = r.Resolve(ws, hex);
        Assert.Equal(t.MetadataToken, again.MetadataToken);
    }

    [Fact]
    public void Method_fqn_resolves_to_method()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        var ws = reg.Open(_corpus.Get("Clean"));
        var r = new TokenResolver();

        var m = r.Resolve(ws, "Acme.Clean.Greeter::Greet");
        Assert.IsAssignableFrom<MethodDefinition>(m);
        Assert.Equal("Greet", ((MethodDefinition)m).Name);
    }

    [Fact]
    public void Unknown_identifier_throws_not_found()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        var ws = reg.Open(_corpus.Get("Clean"));
        var r = new TokenResolver();

        var ex = Assert.Throws<BackendError>(() => r.Resolve(ws, "Does.Not.Exist"));
        Assert.Equal("not-found", ex.Code);
    }
}
