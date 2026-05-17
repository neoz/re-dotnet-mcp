using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Inspection;

[Collection("SyntheticCorpus")]
public sealed class FieldRvaDataTests
{
    private readonly SyntheticCorpusFixture _corpus;

    public FieldRvaDataTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private (Core.Workspace.Workspace Ws, MemberInspectionService Svc) Open()
    {
        var reg = new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore());
        var ws = reg.Open(_corpus.Get("RvaInit"));
        return (ws, new MemberInspectionService(new TokenResolver()));
    }

    private static AsmResolver.DotNet.FieldDefinition? FindRvaField(Core.Workspace.Workspace ws) =>
        ws.Module.GetAllTypes()
            .SelectMany(t => t.Fields)
            .FirstOrDefault(f => f.HasFieldRva);

    [Fact]
    public void Returns_bytes_for_compiler_generated_blob()
    {
        var (ws, svc) = Open();
        var f = FindRvaField(ws);
        Assert.NotNull(f);
        var token = $"0x{f!.MetadataToken.ToUInt32():X8}";

        var data = svc.GetFieldRvaData(ws, token, maxBytes: 4096);

        Assert.Equal(32, data.DeclaredSize);
        Assert.True(data.TotalBytes >= 32);
        Assert.False(data.Truncated);
        var raw = Convert.FromBase64String(data.DataB64);
        Assert.Equal(0xDE, raw[0]);
        Assert.Equal(0xAD, raw[1]);
        Assert.Equal(0xBE, raw[2]);
        Assert.Equal(0xEF, raw[3]);
        Assert.Equal(0x1B, raw[31]);
        Assert.StartsWith("DEADBEEF", data.HexPreview);
    }

    [Fact]
    public void Truncates_to_max_bytes_and_flags_truncation()
    {
        var (ws, svc) = Open();
        var f = FindRvaField(ws);
        var token = $"0x{f!.MetadataToken.ToUInt32():X8}";

        var data = svc.GetFieldRvaData(ws, token, maxBytes: 8);

        Assert.Equal(8, data.BytesReturned);
        Assert.True(data.Truncated);
        Assert.True(data.TotalBytes > 8);
        Assert.Equal("DEADBEEF0001020304050607".Substring(0, 16), data.HexPreview);
    }

    [Fact]
    public void Throws_NotFound_for_field_without_rva()
    {
        var (ws, svc) = Open();
        // The Magic auto-property backing field has no FieldRva — pick any non-RVA field.
        var plain = ws.Module.GetAllTypes()
            .SelectMany(t => t.Fields)
            .FirstOrDefault(x => !x.HasFieldRva);
        Assert.NotNull(plain);
        var token = $"0x{plain!.MetadataToken.ToUInt32():X8}";

        var ex = Assert.Throws<BackendError>(() => svc.GetFieldRvaData(ws, token, 4096));
        Assert.Equal("not-found", ex.Code);
    }
}
