using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata.Tables;

namespace ReDotnet.Core.Il;

/// Public entry-point for the ilasm-subset assembler used by patch_il and
/// replace_method_body. Pipeline: lex -> parse -> label-resolve -> encode.
/// Operand resolution accepts hex tokens (`0x06000123`) and label names today;
/// full ilasm type/method/field signatures are deferred to v1.1.
public sealed class IlAssembler
{
    private static readonly Dictionary<string, CilOpCode> Opcodes = BuildOpcodeMap();

    public AssembleResult Assemble(string ilText, ModuleDefinition module)
    {
        var tokens = new IlLexer(ilText).Tokenize().ToList();
        var parser = new IlParser(tokens);
        var raw = parser.Parse();
        var errors = parser.Errors.ToList();
        if (errors.Count > 0)
            throw new IlParseException(errors);

        var labelToIndex = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < raw.Count; i++)
        {
            if (raw[i].Label is { } lbl)
            {
                if (labelToIndex.ContainsKey(lbl))
                    errors.Add(new IlParseError(raw[i].Line, 1, lbl, "unique label", $"duplicate '{lbl}'"));
                else
                    labelToIndex[lbl] = i;
            }
        }

        var instructions = new List<CilInstruction>(raw.Count);
        // First pass: emit instructions with placeholders for branch targets.
        for (var i = 0; i < raw.Count; i++)
        {
            var ri = raw[i];
            if (!Opcodes.TryGetValue(ri.Opcode, out var opcode))
            {
                errors.Add(new IlParseError(ri.Line, 1, ri.Opcode, "valid opcode", ri.Opcode));
                continue;
            }
            var encoded = EncodeOperand(opcode, ri, labelToIndex, raw, errors, module);
            instructions.Add(encoded);
        }

        if (errors.Count > 0)
            throw new IlParseException(errors);

        // Compute offsets so labels resolve to absolute offsets when wired
        // into a method body. AsmResolver computes offsets lazily, but
        // doing it eagerly here means we can validate now.
        var offset = 0;
        foreach (var instr in instructions)
        {
            instr.Offset = offset;
            offset += instr.Size;
        }

        return new AssembleResult(instructions);
    }

    private static CilInstruction EncodeOperand(
        CilOpCode opcode,
        RawInstruction ri,
        Dictionary<string, int> labels,
        IReadOnlyList<RawInstruction> raw,
        List<IlParseError> errors,
        ModuleDefinition module)
    {
        switch (opcode.OperandType)
        {
            case CilOperandType.InlineNone:
                return new CilInstruction(opcode);

            case CilOperandType.ShortInlineI:
            case CilOperandType.InlineI:
                if (ri.Operand is RawOperandInt ri1)
                    return new CilInstruction(opcode, (int)ri1.Value);
                if (ri.Operand is RawOperandHex rh1)
                    return new CilInstruction(opcode, (int)rh1.Value);
                errors.Add(new IlParseError(ri.Line, 1, ri.Operand?.ToString() ?? "", "integer", "missing"));
                return new CilInstruction(opcode, 0);

            case CilOperandType.InlineI8:
                if (ri.Operand is RawOperandInt ri8)
                    return new CilInstruction(opcode, ri8.Value);
                errors.Add(new IlParseError(ri.Line, 1, "", "int64", "missing"));
                return new CilInstruction(opcode, 0L);

            case CilOperandType.ShortInlineR:
                if (ri.Operand is RawOperandFloat rf)
                    return new CilInstruction(opcode, (float)rf.Value);
                errors.Add(new IlParseError(ri.Line, 1, "", "float32", "missing"));
                return new CilInstruction(opcode, 0f);

            case CilOperandType.InlineR:
                if (ri.Operand is RawOperandFloat rd)
                    return new CilInstruction(opcode, rd.Value);
                errors.Add(new IlParseError(ri.Line, 1, "", "float64", "missing"));
                return new CilInstruction(opcode, 0d);

            case CilOperandType.InlineString:
                if (ri.Operand is RawOperandString rs)
                    return new CilInstruction(opcode, rs.Value);
                errors.Add(new IlParseError(ri.Line, 1, "", "string literal", "missing"));
                return new CilInstruction(opcode, "");

            case CilOperandType.ShortInlineBrTarget:
            case CilOperandType.InlineBrTarget:
                if (ri.Operand is RawOperandLabel rl
                    && labels.TryGetValue(rl.Name, out var targetIdx))
                {
                    // Build a deferred label - resolved at body finalization.
                    return new CilInstruction(opcode, new DeferredCilLabel(targetIdx));
                }
                errors.Add(new IlParseError(ri.Line, 1,
                    (ri.Operand as RawOperandLabel)?.Name ?? "",
                    "branch label",
                    (ri.Operand as RawOperandLabel)?.Name ?? "missing"));
                return new CilInstruction(opcode, new DeferredCilLabel(0));

            case CilOperandType.InlineSwitch:
                if (ri.Operand is RawOperandLabels rls)
                {
                    var targets = new List<ICilLabel>(rls.Names.Count);
                    foreach (var name in rls.Names)
                    {
                        if (labels.TryGetValue(name, out var idx))
                            targets.Add(new DeferredCilLabel(idx));
                        else
                        {
                            errors.Add(new IlParseError(ri.Line, 1, name, "branch label", name));
                            targets.Add(new DeferredCilLabel(0));
                        }
                    }
                    return new CilInstruction(opcode, targets);
                }
                errors.Add(new IlParseError(ri.Line, 1, "", "switch label list", "missing"));
                return new CilInstruction(opcode, new List<ICilLabel>());

            case CilOperandType.ShortInlineVar:
            case CilOperandType.InlineVar:
                if (ri.Operand is RawOperandVar rv) return new CilInstruction(opcode, rv.Index);
                if (ri.Operand is RawOperandInt riv) return new CilInstruction(opcode, (int)riv.Value);
                errors.Add(new IlParseError(ri.Line, 1, "", "local index", "missing"));
                return new CilInstruction(opcode, 0);

            case CilOperandType.ShortInlineArgument:
            case CilOperandType.InlineArgument:
                if (ri.Operand is RawOperandInt ria) return new CilInstruction(opcode, (int)ria.Value);
                errors.Add(new IlParseError(ri.Line, 1, "", "argument index", "missing"));
                return new CilInstruction(opcode, 0);

            case CilOperandType.InlineMethod:
            case CilOperandType.InlineField:
            case CilOperandType.InlineType:
            case CilOperandType.InlineTok:
            case CilOperandType.InlineSig:
                if (ri.Operand is RawOperandHex rht)
                {
                    var token = new MetadataToken(rht.Value);
                    if (module.TryLookupMember(token, out var member))
                        return new CilInstruction(opcode, member);
                    errors.Add(new IlParseError(ri.Line, 1, $"0x{rht.Value:X8}", "valid metadata token", "unknown token"));
                    return new CilInstruction(opcode, 0);
                }
                errors.Add(new IlParseError(ri.Line, 1, "", "hex metadata token (0xNNNNNNNN)", "non-hex operand; full ilasm type refs not yet supported"));
                return new CilInstruction(opcode, 0);

            default:
                errors.Add(new IlParseError(ri.Line, 1, opcode.Mnemonic, "supported opcode form", $"unsupported operand type {opcode.OperandType}"));
                return new CilInstruction(opcode);
        }
    }

    private static Dictionary<string, CilOpCode> BuildOpcodeMap()
    {
        var map = new Dictionary<string, CilOpCode>(StringComparer.Ordinal);
        foreach (var field in typeof(CilOpCodes).GetFields(
            System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static))
        {
            if (field.FieldType != typeof(CilOpCode)) continue;
            var op = (CilOpCode)field.GetValue(null)!;
            map[op.Mnemonic] = op;
        }
        return map;
    }
}

public sealed record AssembleResult(IReadOnlyList<CilInstruction> Instructions);

/// Placeholder branch target carrying an instruction index. The IL patch driver
/// rewrites these to real CilInstructionLabel objects once the instructions are
/// spliced into the live method body (so offsets are known).
internal sealed class DeferredCilLabel : ICilLabel, IEquatable<ICilLabel>
{
    public int TargetIndex { get; }
    public int Offset { get; set; }
    public DeferredCilLabel(int index) => TargetIndex = index;

    public bool Equals(ICilLabel? other) =>
        other is DeferredCilLabel d && d.TargetIndex == TargetIndex;
}
