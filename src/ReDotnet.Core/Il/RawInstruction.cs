namespace ReDotnet.Core.Il;

/// Output of the parse phase before operand resolution + label resolution.
internal sealed record RawInstruction(
    string? Label,
    string Opcode,
    RawOperand? Operand,
    int Line);

internal abstract record RawOperand;
internal sealed record RawOperandLabel(string Name) : RawOperand;
internal sealed record RawOperandLabels(IReadOnlyList<string> Names) : RawOperand;
internal sealed record RawOperandHex(uint Value) : RawOperand;
internal sealed record RawOperandInt(long Value) : RawOperand;
internal sealed record RawOperandFloat(double Value) : RawOperand;
internal sealed record RawOperandString(string Value) : RawOperand;
internal sealed record RawOperandVar(int Index) : RawOperand;
