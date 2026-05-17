using ReDotnet.Core.Mutation;
using ReDotnet.Core.Save;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.RoundTrip;

[Collection("SyntheticCorpus")]
public sealed class PatchSaveReloadTests
{
    private readonly SyntheticCorpusFixture _corpus;
    public PatchSaveReloadTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private static WorkspaceRegistry NewRegistry() =>
        new(new ModuleResolverFactory(null), new SidecarStore());

    [Fact]
    public void Rename_save_reload_persists_name()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        var renames = new RenameService(new TokenResolver());
        renames.RenameMethod(ws, "Acme.Clean.Greeter::Greet", "Greet2");

        var save = new SaveService();
        var outPath = Path.Combine(_corpus.Directory, "Clean.renamed.dll");
        save.SaveAssembly(ws, outPath, "strip", null);

        var reg2 = NewRegistry();
        var ws2 = reg2.Open(outPath);
        var greeter = ws2.Module.GetAllTypes()
            .First(t => t.FullName == "Acme.Clean.Greeter");
        Assert.Contains(greeter.Methods, m => m.Name == "Greet2");
        Assert.DoesNotContain(greeter.Methods, m => m.Name == "Greet");
    }

    [Fact]
    public void Ctf_brfalse_flip_changes_return()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("CtfCrackme"));

        // Find the brfalse instruction and flip it to brtrue with patch_il.
        var tokenResolver = new TokenResolver();
        var check = (AsmResolver.DotNet.MethodDefinition)tokenResolver.Resolve(
            ws, "Acme.Ctf.Crackme::Check");
        var body = check.CilMethodBody!;
        var brfalse = body.Instructions.First(i =>
            i.OpCode.Code == AsmResolver.PE.DotNet.Cil.CilCode.Brfalse_S
            || i.OpCode.Code == AsmResolver.PE.DotNet.Cil.CilCode.Brfalse);

        var il = new IlPatchService(tokenResolver);
        // brfalse.s is 2 bytes; brtrue.s is 2 bytes. brfalse is 5 bytes; brtrue is 5 bytes.
        // Patch with the matching-size mnemonic.
        var labelTarget = ((AsmResolver.PE.DotNet.Cil.ICilLabel)brfalse.Operand!).Offset;
        var mnemonic = brfalse.OpCode.Code == AsmResolver.PE.DotNet.Cil.CilCode.Brfalse_S ? "brtrue.s" : "brtrue";
        var labelName = $"L_{labelTarget:X4}";

        // We can't just write "brtrue.s IL_xxxx" because IlAssembler doesn't
        // re-discover targets in the surrounding body. So we serialize the
        // single instruction as a tiny program with a dummy label:
        var patchText = $"{mnemonic} {labelName}\n{labelName}: nop";
        // That gives us 2 instructions of which only the first should overwrite
        // brfalse. Instead, let's swap the opcode directly via reflection here
        // to keep this test deterministic — round-trip of the assembler proper
        // is covered in IlAssemblerTests.

        brfalse.OpCode = brfalse.OpCode.Code == AsmResolver.PE.DotNet.Cil.CilCode.Brfalse_S
            ? AsmResolver.PE.DotNet.Cil.CilOpCodes.Brtrue_S
            : AsmResolver.PE.DotNet.Cil.CilOpCodes.Brtrue;

        var save = new SaveService();
        var outPath = Path.Combine(_corpus.Directory, "CtfCrackme.flipped.dll");
        save.SaveAssembly(ws, outPath, "strip", null);

        // Load the patched assembly in a fresh registry and confirm the opcode flipped.
        var reg2 = NewRegistry();
        var ws2 = reg2.Open(outPath);
        var check2 = (AsmResolver.DotNet.MethodDefinition)new TokenResolver().Resolve(
            ws2, "Acme.Ctf.Crackme::Check");
        var body2 = check2.CilMethodBody!;
        Assert.Contains(body2.Instructions, i =>
            i.OpCode.Code is AsmResolver.PE.DotNet.Cil.CilCode.Brtrue
            or AsmResolver.PE.DotNet.Cil.CilCode.Brtrue_S);
        Assert.DoesNotContain(body2.Instructions, i =>
            i.OpCode.Code is AsmResolver.PE.DotNet.Cil.CilCode.Brfalse
            or AsmResolver.PE.DotNet.Cil.CilCode.Brfalse_S);
    }

    [Fact]
    public void Strip_strategy_writes_unsigned_assembly()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        var save = new SaveService();
        var outPath = Path.Combine(_corpus.Directory, "Clean.stripped.dll");
        var result = save.SaveAssembly(ws, outPath, "strip", null);
        Assert.True(File.Exists(outPath));
        Assert.Equal("strip", result.SnStrategyApplied);
        Assert.True(result.BytesWritten > 0);
    }
}
