using ReDotnet.Core.Il;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Il;

[Collection("SyntheticCorpus")]
public sealed class IlAssemblerTests
{
    private readonly SyntheticCorpusFixture _corpus;
    public IlAssemblerTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private Core.Workspace.Workspace Load() =>
        new WorkspaceRegistry(new ModuleResolverFactory(null), new SidecarStore())
            .Open(_corpus.Get("Clean"));

    [Fact]
    public void Single_instruction_no_operand()
    {
        var ws = Load();
        var result = new IlAssembler().Assemble("nop", ws.Module);
        Assert.Single(result.Instructions);
        Assert.Equal("nop", result.Instructions[0].OpCode.Mnemonic);
    }

    [Fact]
    public void Multiple_instructions_emit_in_order()
    {
        var ws = Load();
        var result = new IlAssembler().Assemble("ldarg.0\nldarg.1\nadd\nret", ws.Module);
        var mnemonics = result.Instructions.Select(i => i.OpCode.Mnemonic).ToList();
        Assert.Equal(new[] { "ldarg.0", "ldarg.1", "add", "ret" }, mnemonics);
    }

    [Fact]
    public void Branch_with_label_resolves()
    {
        var ws = Load();
        var text = """
            ldc.i4.0
            brfalse.s END
            ldc.i4.1
            END: ret
            """;
        var result = new IlAssembler().Assemble(text, ws.Module);
        Assert.Equal(4, result.Instructions.Count);
        Assert.Equal("brfalse.s", result.Instructions[1].OpCode.Mnemonic);
    }

    [Fact]
    public void Invalid_opcode_reports_parse_error()
    {
        var ws = Load();
        var ex = Assert.Throws<IlParseException>(
            () => new IlAssembler().Assemble("xyzzy", ws.Module));
        Assert.Single(ex.Errors);
        Assert.Equal("xyzzy", ex.Errors[0].Found);
    }

    [Fact]
    public void Ldstr_literal_parses()
    {
        var ws = Load();
        var result = new IlAssembler().Assemble("ldstr \"hello\\nworld\"", ws.Module);
        Assert.Single(result.Instructions);
        Assert.Equal("ldstr", result.Instructions[0].OpCode.Mnemonic);
        Assert.Equal("hello\nworld", result.Instructions[0].Operand);
    }

    [Fact]
    public void Ldc_i4_with_signed_int_parses()
    {
        var ws = Load();
        var result = new IlAssembler().Assemble("ldc.i4 -42", ws.Module);
        Assert.Equal(-42, (int)result.Instructions[0].Operand!);
    }
}
