using ReDotnet.Server.MetaTools;

namespace ReDotnet.Tests.Meta;

public sealed class ToolCatalogTests
{
    [Fact]
    public void All_pinned_tools_are_present()
    {
        var catalog = new ToolCatalog();
        var names = catalog.All.Select(t => t.Name).ToHashSet(StringComparer.Ordinal);
        foreach (var pin in PinningRegistry.Pinned)
        {
            Assert.True(names.Contains(pin), $"pinned tool '{pin}' is missing from the catalog");
        }
    }

    [Fact]
    public void Catalog_marks_pinned_correctly()
    {
        var catalog = new ToolCatalog();
        var openAsm = catalog.Get("open_assembly");
        Assert.NotNull(openAsm);
        Assert.True(openAsm!.Pinned);
    }

    [Fact]
    public void Every_tool_has_description()
    {
        var catalog = new ToolCatalog();
        foreach (var t in catalog.All)
            Assert.False(string.IsNullOrWhiteSpace(t.Description), $"{t.Name} missing description");
    }
}
