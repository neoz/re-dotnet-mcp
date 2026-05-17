namespace ReDotnet.Core.Il;

internal enum IlTokenKind
{
    Identifier, // includes opcode names and labels
    HexNumber,
    IntNumber,
    FloatNumber,
    String,
    Colon,
    Comma,
    LParen,
    RParen,
    Newline,
    Eof,
}

internal sealed record IlToken(IlTokenKind Kind, string Text, int Line, int Column);

/// Minimal lexer for the ilasm subset accepted by patch_il. Newline-significant.
/// Skips `// ...` comments. String literals support C-style escapes.
internal sealed class IlLexer
{
    private readonly string _src;
    private int _pos;
    private int _line = 1;
    private int _col = 1;

    public IlLexer(string source) => _src = source;

    public IEnumerable<IlToken> Tokenize()
    {
        while (_pos < _src.Length)
        {
            var c = _src[_pos];
            if (c == '\r')
            {
                if (_pos + 1 < _src.Length && _src[_pos + 1] == '\n') Advance();
                Advance();
                yield return new IlToken(IlTokenKind.Newline, "\\n", _line - 1, 0);
                continue;
            }
            if (c == '\n')
            {
                Advance();
                yield return new IlToken(IlTokenKind.Newline, "\\n", _line - 1, 0);
                continue;
            }
            if (c is ' ' or '\t')
            {
                Advance();
                continue;
            }
            if (c == '/' && _pos + 1 < _src.Length && _src[_pos + 1] == '/')
            {
                while (_pos < _src.Length && _src[_pos] != '\n') Advance();
                continue;
            }
            if (c == ':') { Advance(); yield return new IlToken(IlTokenKind.Colon, ":", _line, _col - 1); continue; }
            if (c == ',') { Advance(); yield return new IlToken(IlTokenKind.Comma, ",", _line, _col - 1); continue; }
            if (c == '(') { Advance(); yield return new IlToken(IlTokenKind.LParen, "(", _line, _col - 1); continue; }
            if (c == ')') { Advance(); yield return new IlToken(IlTokenKind.RParen, ")", _line, _col - 1); continue; }
            if (c == '"') { yield return ReadString(); continue; }
            if (IsIdentStart(c)) { yield return ReadIdentOrLabel(); continue; }
            if (c == '-' || char.IsDigit(c)) { yield return ReadNumber(); continue; }

            // Unknown character — emit as identifier-of-length-1 to keep parsing going;
            // parser will surface a useful error.
            Advance();
            yield return new IlToken(IlTokenKind.Identifier, c.ToString(), _line, _col - 1);
        }
        yield return new IlToken(IlTokenKind.Eof, "<eof>", _line, _col);
    }

    private IlToken ReadString()
    {
        var startLine = _line; var startCol = _col;
        Advance(); // consume opening quote
        var sb = new System.Text.StringBuilder();
        while (_pos < _src.Length && _src[_pos] != '"')
        {
            var c = _src[_pos];
            if (c == '\\' && _pos + 1 < _src.Length)
            {
                Advance();
                var esc = _src[_pos];
                Advance();
                sb.Append(esc switch
                {
                    'n' => '\n',
                    'r' => '\r',
                    't' => '\t',
                    '\\' => '\\',
                    '"' => '"',
                    '0' => '\0',
                    'u' => ReadUnicodeEscape(),
                    _ => esc,
                });
                continue;
            }
            sb.Append(c);
            Advance();
        }
        if (_pos < _src.Length) Advance(); // closing quote
        return new IlToken(IlTokenKind.String, sb.ToString(), startLine, startCol);
    }

    private char ReadUnicodeEscape()
    {
        if (_pos + 4 > _src.Length) return '?';
        var hex = _src.Substring(_pos, 4);
        _pos += 4; _col += 4;
        return (char)Convert.ToInt32(hex, 16);
    }

    private IlToken ReadIdentOrLabel()
    {
        var startLine = _line; var startCol = _col; var start = _pos;
        while (_pos < _src.Length && IsIdentPart(_src[_pos])) Advance();
        var text = _src[start.._pos];
        return new IlToken(IlTokenKind.Identifier, text, startLine, startCol);
    }

    private IlToken ReadNumber()
    {
        var startLine = _line; var startCol = _col; var start = _pos;
        if (_src[_pos] == '-') Advance();
        var isHex = false;
        if (_pos + 1 < _src.Length && _src[_pos] == '0' && (_src[_pos + 1] == 'x' || _src[_pos + 1] == 'X'))
        {
            isHex = true;
            Advance(); Advance();
            while (_pos < _src.Length && IsHex(_src[_pos])) Advance();
            return new IlToken(IlTokenKind.HexNumber, _src[start.._pos], startLine, startCol);
        }
        var isFloat = false;
        while (_pos < _src.Length && (char.IsDigit(_src[_pos]) || _src[_pos] == '.'))
        {
            if (_src[_pos] == '.') isFloat = true;
            Advance();
        }
        return new IlToken(isFloat ? IlTokenKind.FloatNumber : IlTokenKind.IntNumber, _src[start.._pos], startLine, startCol);
    }

    private void Advance()
    {
        if (_pos < _src.Length && _src[_pos] == '\n') { _line++; _col = 1; }
        else _col++;
        _pos++;
    }

    private static bool IsIdentStart(char c) =>
        char.IsLetter(c) || c == '_' || c == '@' || c == '.';
    private static bool IsIdentPart(char c) =>
        char.IsLetterOrDigit(c) || c == '_' || c == '.' || c == '@';
    private static bool IsHex(char c) =>
        char.IsDigit(c) || (c >= 'a' && c <= 'f') || (c >= 'A' && c <= 'F');
}
