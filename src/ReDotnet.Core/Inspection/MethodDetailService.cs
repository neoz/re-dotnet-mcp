using AsmResolver.DotNet;
using AsmResolver.DotNet.Code.Cil;
using AsmResolver.PE.DotNet.Cil;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Workspace;

namespace ReDotnet.Core.Inspection;

public sealed class MethodDetailService
{
    private readonly TokenResolver _tokens;
    public MethodDetailService(TokenResolver tokens) => _tokens = tokens;

    public MethodDetail GetMethod(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a MethodDefinition");

        var body = md.CilMethodBody;
        return new MethodDetail(
            Token: TokenFormatHelper.Hex(md),
            Name: md.Name?.ToString() ?? "",
            DeclaringType: md.DeclaringType?.FullName ?? "<global>",
            Signature: md.Signature?.ToString() ?? "",
            ImplAttributes: md.ImplAttributes.ToString(),
            Attributes: md.Attributes.ToString(),
            IsStatic: md.IsStatic,
            IsVirtual: md.IsVirtual,
            IsAbstract: md.IsAbstract,
            Visibility: MethodListService.VisOf(md),
            PInvokeDll: md.ImplementationMap?.Scope?.Name?.ToString(),
            PInvokeEntryPoint: md.ImplementationMap?.Name?.ToString(),
            CodeSize: body?.Instructions.Sum(i => i.Size),
            MaxStack: body?.MaxStack is null ? null : (ushort)body.MaxStack,
            LocalCount: body?.LocalVariables.Count ?? 0,
            ExceptionHandlerCount: body?.ExceptionHandlers.Count ?? 0,
            Parameters: md.Parameters.Select(p => new ParameterInfo(
                p.Index, p.Name?.ToString(), p.ParameterType?.FullName ?? "?")).ToList());
    }

    public DisassembledMethod Disassemble(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a MethodDefinition");

        var body = md.CilMethodBody;
        if (body is null)
            return new DisassembledMethod(TokenFormatHelper.Hex(md), md.FullName, Array.Empty<IlInstruction>());

        var instrs = new List<IlInstruction>(body.Instructions.Count);
        foreach (var instr in body.Instructions)
        {
            instrs.Add(new IlInstruction(
                Offset: $"IL_{instr.Offset:X4}",
                Opcode: instr.OpCode.Mnemonic,
                Operand: FormatOperand(instr)));
        }
        return new DisassembledMethod(TokenFormatHelper.Hex(md), md.FullName, instrs);
    }

    public static string? FormatOperand(CilInstruction instr)
    {
        var op = instr.Operand;
        if (op is null) return null;
        return op switch
        {
            ICilLabel lbl => $"IL_{lbl.Offset:X4}",
            IList<ICilLabel> labels => "(" + string.Join(", ", labels.Select(l => $"IL_{l.Offset:X4}")) + ")",
            string s => "\"" + EscapeString(s) + "\"",
            CilLocalVariable lv => $"V_{lv.Index}",
            IMetadataMember mm => $"{TokenFormatHelper.Hex(mm)} /* {NameOf(mm)} */",
            _ => op.ToString(),
        };
    }

    private static string NameOf(IMetadataMember m) => m switch
    {
        IFullNameProvider fn => fn.FullName,
        INameProvider n => n.Name?.ToString() ?? "?",
        _ => m.ToString() ?? "?",
    };

    private static string EscapeString(string s)
    {
        var sb = new System.Text.StringBuilder(s.Length + 2);
        foreach (var c in s)
        {
            switch (c)
            {
                case '\\': sb.Append(@"\\"); break;
                case '"': sb.Append("\\\""); break;
                case '\n': sb.Append(@"\n"); break;
                case '\r': sb.Append(@"\r"); break;
                case '\t': sb.Append(@"\t"); break;
                default:
                    if (c < 32 || c > 126)
                        sb.AppendFormat("\\u{0:X4}", (int)c);
                    else sb.Append(c);
                    break;
            }
        }
        return sb.ToString();
    }

    public IReadOnlyList<LocalVariableInfo> GetLocals(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a MethodDefinition");
        var body = md.CilMethodBody;
        if (body is null) return Array.Empty<LocalVariableInfo>();

        return body.LocalVariables.Select(lv => new LocalVariableInfo(
            Index: lv.Index,
            Type: lv.VariableType?.FullName ?? "?",
            Pinned: lv.VariableType is AsmResolver.DotNet.Signatures.PinnedTypeSignature))
            .ToList();
    }

    public IReadOnlyList<ExceptionHandlerInfo> GetExceptionHandlers(Workspace.Workspace ws, string identifier)
    {
        var m = _tokens.Resolve(ws, identifier);
        if (m is not MethodDefinition md)
            throw BackendError.BadInput($"'{identifier}' did not resolve to a MethodDefinition");
        var body = md.CilMethodBody;
        if (body is null) return Array.Empty<ExceptionHandlerInfo>();

        return body.ExceptionHandlers.Select(eh => new ExceptionHandlerInfo(
            HandlerType: eh.HandlerType.ToString(),
            TryStart: $"IL_{eh.TryStart?.Offset ?? 0:X4}",
            TryEnd: $"IL_{eh.TryEnd?.Offset ?? 0:X4}",
            HandlerStart: $"IL_{eh.HandlerStart?.Offset ?? 0:X4}",
            HandlerEnd: $"IL_{eh.HandlerEnd?.Offset ?? 0:X4}",
            FilterStart: eh.FilterStart is null ? null : $"IL_{eh.FilterStart.Offset:X4}",
            CatchType: eh.ExceptionType?.FullName)).ToList();
    }
}

public sealed record MethodDetail(
    string Token, string Name, string DeclaringType, string Signature,
    string ImplAttributes, string Attributes,
    bool IsStatic, bool IsVirtual, bool IsAbstract, string Visibility,
    string? PInvokeDll, string? PInvokeEntryPoint,
    int? CodeSize, ushort? MaxStack, int LocalCount, int ExceptionHandlerCount,
    IReadOnlyList<ParameterInfo> Parameters);

public sealed record ParameterInfo(int Index, string? Name, string Type);

public sealed record DisassembledMethod(
    string Token, string FullName, IReadOnlyList<IlInstruction> Instructions);

public sealed record IlInstruction(string Offset, string Opcode, string? Operand);

public sealed record LocalVariableInfo(int Index, string Type, bool Pinned);

public sealed record ExceptionHandlerInfo(
    string HandlerType, string TryStart, string TryEnd,
    string HandlerStart, string HandlerEnd,
    string? FilterStart, string? CatchType);
