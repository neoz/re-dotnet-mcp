using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Il;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Mutation;

public sealed class IlPatchService
{
    private readonly TokenResolver _tokens;
    private readonly IlAssembler _assembler = new();

    public IlPatchService(TokenResolver tokens) => _tokens = tokens;

    public PatchResult PatchIl(Workspace.Workspace ws, string methodIdentifier, string offset, string newIl)
    {
        var md = ResolveMethod(ws, methodIdentifier);
        var body = md.CilMethodBody ?? throw BackendError.BadInput("method has no IL body");

        // Snapshot the old IL for undo + response.
        var oldIl = SerializeIl(body);

        var assembled = _assembler.Assemble(newIl, ws.Module);
        var startOffset = ParseOffset(offset);
        var insertAt = FindInstructionIndex(body, startOffset)
            ?? throw BackendError.NotFound("il_offset", offset);

        // Remove instructions starting at insertAt until covered length matches
        // the new IL's byte length, so we are an in-place size-preserving patch.
        var newSize = assembled.Instructions.Sum(i => i.Size);
        var removed = 0;
        while (insertAt + removed < body.Instructions.Count && removed < newSize)
        {
            removed += body.Instructions[insertAt + removed].Size;
        }
        if (removed != newSize)
        {
            throw BackendError.BadInput(
                $"new IL is {newSize} bytes; closest aligned removal is {removed}. " +
                "Pad with nops or use replace_method_body to change method size.");
        }
        for (var i = 0; i < removed; i++)
            body.Instructions.RemoveAt(insertAt);

        // Resolve deferred labels (instruction-index based) to real label objects.
        ResolveDeferredLabels(assembled.Instructions);

        for (var i = 0; i < assembled.Instructions.Count; i++)
            body.Instructions.Insert(insertAt + i, assembled.Instructions[i]);
        body.Instructions.CalculateOffsets();

        var newIlText = SerializeIl(body);
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.PatchIl, ws.AssemblyId, $"0x{md.MetadataToken.ToUInt32():X8}",
            new { offset, il = oldIl }, new { offset, il = newIlText }, DateTimeOffset.UtcNow));
        return new PatchResult($"0x{md.MetadataToken.ToUInt32():X8}", oldIl, newIlText);
    }

    public PatchResult NopIlRange(Workspace.Workspace ws, string methodIdentifier, string startOff, string endOff)
    {
        var md = ResolveMethod(ws, methodIdentifier);
        var body = md.CilMethodBody ?? throw BackendError.BadInput("method has no IL body");
        var oldIl = SerializeIl(body);

        var s = ParseOffset(startOff);
        var e = ParseOffset(endOff);
        for (var i = 0; i < body.Instructions.Count; i++)
        {
            var instr = body.Instructions[i];
            if (instr.Offset >= s && instr.Offset < e)
            {
                body.Instructions[i] = new CilInstruction(CilOpCodes.Nop);
            }
        }
        body.Instructions.CalculateOffsets();
        var newIlText = SerializeIl(body);
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.NopIlRange, ws.AssemblyId, $"0x{md.MetadataToken.ToUInt32():X8}",
            oldIl, newIlText, DateTimeOffset.UtcNow));
        return new PatchResult($"0x{md.MetadataToken.ToUInt32():X8}", oldIl, newIlText);
    }

    public PatchResult ReplaceMethodBody(Workspace.Workspace ws, string methodIdentifier, string ilText)
    {
        var md = ResolveMethod(ws, methodIdentifier);
        var body = md.CilMethodBody
            ?? throw BackendError.BadInput("method has no IL body; replace_method_body requires an existing body in v1");
        var oldIl = SerializeIl(body);

        var assembled = _assembler.Assemble(ilText, ws.Module);
        body.Instructions.Clear();
        ResolveDeferredLabels(assembled.Instructions);
        foreach (var i in assembled.Instructions) body.Instructions.Add(i);
        body.Instructions.CalculateOffsets();

        var newIlText = SerializeIl(body);
        ws.MutationLog.Record(new MutationLogEntry(
            MutationKind.ReplaceMethodBody, ws.AssemblyId, $"0x{md.MetadataToken.ToUInt32():X8}",
            oldIl, newIlText, DateTimeOffset.UtcNow));
        return new PatchResult($"0x{md.MetadataToken.ToUInt32():X8}", oldIl, newIlText);
    }

    private MethodDefinition ResolveMethod(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a MethodDefinition");
        return md;
    }

    private static int ParseOffset(string offset)
    {
        if (offset.StartsWith("IL_", StringComparison.OrdinalIgnoreCase))
            return int.Parse(offset.AsSpan(3), System.Globalization.NumberStyles.HexNumber);
        if (offset.StartsWith("0x", StringComparison.OrdinalIgnoreCase))
            return int.Parse(offset.AsSpan(2), System.Globalization.NumberStyles.HexNumber);
        return int.Parse(offset);
    }

    private static int? FindInstructionIndex(CilMethodBody body, int offset)
    {
        for (var i = 0; i < body.Instructions.Count; i++)
            if (body.Instructions[i].Offset == offset) return i;
        return null;
    }

    private static void ResolveDeferredLabels(IReadOnlyList<CilInstruction> instructions)
    {
        for (var i = 0; i < instructions.Count; i++)
        {
            var instr = instructions[i];
            if (instr.Operand is DeferredCilLabel dl)
                instr.Operand = new CilInstructionLabel(instructions[dl.TargetIndex]);
            else if (instr.Operand is IList<ICilLabel> labels)
            {
                for (var j = 0; j < labels.Count; j++)
                    if (labels[j] is DeferredCilLabel dl2)
                        labels[j] = new CilInstructionLabel(instructions[dl2.TargetIndex]);
            }
        }
    }

    private static string SerializeIl(CilMethodBody body)
    {
        var sb = new System.Text.StringBuilder();
        foreach (var instr in body.Instructions)
        {
            sb.Append($"IL_{instr.Offset:X4}: {instr.OpCode.Mnemonic}");
            if (instr.Operand is not null) sb.Append(' ').Append(instr.Operand);
            sb.AppendLine();
        }
        return sb.ToString();
    }
}

public sealed record PatchResult(string MethodToken, string OldIl, string NewIl);
