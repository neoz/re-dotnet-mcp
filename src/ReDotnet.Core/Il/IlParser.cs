namespace ReDotnet.Core.Il;

/// Parses tokens produced by IlLexer into RawInstructions. Newline-terminated.
internal sealed class IlParser
{
    private readonly IReadOnlyList<IlToken> _tokens;
    private int _pos;
    private readonly List<IlParseError> _errors = new();

    public IlParser(IEnumerable<IlToken> tokens) => _tokens = tokens.ToList();

    public IReadOnlyList<IlParseError> Errors => _errors;

    public IReadOnlyList<RawInstruction> Parse()
    {
        var result = new List<RawInstruction>();
        while (!IsAtEnd)
        {
            // Skip blank lines.
            while (Match(IlTokenKind.Newline)) { }
            if (IsAtEnd) break;

            string? label = null;
            if (Peek(0).Kind == IlTokenKind.Identifier && Peek(1).Kind == IlTokenKind.Colon)
            {
                label = Peek(0).Text;
                _pos += 2;
            }

            if (!Match(IlTokenKind.Identifier, out var opcodeTok))
            {
                Error("opcode identifier", Peek(0));
                SkipToNewline();
                continue;
            }

            var operand = ParseOperand(opcodeTok.Text);

            if (_errors.Count >= 20) break;

            result.Add(new RawInstruction(label, opcodeTok.Text.ToLowerInvariant(), operand, opcodeTok.Line));

            // Consume to next newline or eof.
            while (!IsAtEnd && Peek(0).Kind != IlTokenKind.Newline) _pos++;
            Match(IlTokenKind.Newline);
        }
        return result;
    }

    private RawOperand? ParseOperand(string opcode)
    {
        var peek = Peek(0);
        switch (peek.Kind)
        {
            case IlTokenKind.Newline:
            case IlTokenKind.Eof:
                return null;
            case IlTokenKind.HexNumber:
                _pos++;
                return new RawOperandHex(Convert.ToUInt32(peek.Text, 16));
            case IlTokenKind.IntNumber:
                _pos++;
                return new RawOperandInt(long.Parse(peek.Text, System.Globalization.CultureInfo.InvariantCulture));
            case IlTokenKind.FloatNumber:
                _pos++;
                return new RawOperandFloat(double.Parse(peek.Text, System.Globalization.CultureInfo.InvariantCulture));
            case IlTokenKind.String:
                _pos++;
                return new RawOperandString(peek.Text);
            case IlTokenKind.LParen:
                // Switch operand: '(' label (',' label)* ')'
                _pos++;
                var labels = new List<string>();
                while (Peek(0).Kind != IlTokenKind.RParen && !IsAtEnd)
                {
                    if (!Match(IlTokenKind.Identifier, out var labelTok))
                    {
                        Error("branch label", Peek(0));
                        break;
                    }
                    labels.Add(labelTok.Text);
                    Match(IlTokenKind.Comma);
                }
                Match(IlTokenKind.RParen);
                return new RawOperandLabels(labels);
            case IlTokenKind.Identifier:
                _pos++;
                if (peek.Text.StartsWith("V_", StringComparison.OrdinalIgnoreCase)
                    && int.TryParse(peek.Text.AsSpan(2), out var vIdx))
                    return new RawOperandVar(vIdx);
                // Treat any identifier as a label name (consumed by encoder).
                return new RawOperandLabel(peek.Text);
            default:
                Error("operand", peek);
                _pos++;
                return null;
        }
    }

    private void Error(string expected, IlToken found)
    {
        _errors.Add(new IlParseError(
            Line: found.Line,
            Column: found.Column,
            Snippet: found.Text,
            Expected: expected,
            Found: found.Text));
    }

    private bool Match(IlTokenKind kind)
    {
        if (Peek(0).Kind != kind) return false;
        _pos++;
        return true;
    }

    private bool Match(IlTokenKind kind, out IlToken token)
    {
        token = Peek(0);
        if (token.Kind != kind) return false;
        _pos++;
        return true;
    }

    private IlToken Peek(int offset) =>
        _pos + offset < _tokens.Count ? _tokens[_pos + offset] : _tokens[^1];

    private bool IsAtEnd => Peek(0).Kind == IlTokenKind.Eof;

    private void SkipToNewline()
    {
        while (!IsAtEnd && Peek(0).Kind != IlTokenKind.Newline) _pos++;
        Match(IlTokenKind.Newline);
    }
}
