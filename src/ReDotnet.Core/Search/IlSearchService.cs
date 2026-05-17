using System.Text.RegularExpressions;
using AsmResolver;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.File;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;

namespace ReDotnet.Core.Search;

public sealed class IlSearchService
{
    public Page<IlSearchHit> SearchIl(Workspace.Workspace ws, string pattern, int? offset, int? limit)
    {
        Regex rx;
        try { rx = new Regex(pattern); }
        catch (ArgumentException ex) { throw BackendError.BadInput($"invalid regex: {ex.Message}"); }

        var hits = new List<IlSearchHit>();
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var m in t.Methods)
            {
                var body = m.CilMethodBody;
                if (body is null) continue;
                foreach (var instr in body.Instructions)
                {
                    var line = $"{instr.OpCode.Mnemonic} {Inspection.MethodDetailService.FormatOperand(instr) ?? ""}".Trim();
                    if (rx.IsMatch(line))
                    {
                        hits.Add(new IlSearchHit(
                            MethodToken: TokenFormatHelper.Hex(m),
                            MethodName: m.FullName,
                            Offset: $"IL_{instr.Offset:X4}",
                            Line: line));
                    }
                }
            }
        }
        return Page<IlSearchHit>.From(hits, offset, limit);
    }

    public Page<BytesHit> SearchBytes(Workspace.Workspace ws, string hexPattern, string? section, int? offset, int? limit)
    {
        if (string.IsNullOrEmpty(ws.Module.FilePath) || !File.Exists(ws.Module.FilePath))
            return Page<BytesHit>.From(Array.Empty<BytesHit>(), offset, limit);

        var needle = ParseHex(hexPattern);
        var pe = PEFile.FromFile(ws.Module.FilePath);
        var hits = new List<BytesHit>();
        foreach (var sec in pe.Sections)
        {
            if (section is not null && sec.Name.IndexOf(section, StringComparison.OrdinalIgnoreCase) < 0) continue;
            var contents = sec.Contents;
            if (contents is null) continue;
            var data = AsmResolver.Extensions.WriteIntoArray(contents);
            var pos = 0;
            while ((pos = IndexOf(data, needle, pos)) >= 0)
            {
                hits.Add(new BytesHit(sec.Name, sec.Rva + (uint)pos, Convert.ToHexString(needle)));
                pos += needle.Length;
            }
        }
        return Page<BytesHit>.From(hits, offset, limit);
    }

    private static int IndexOf(byte[] haystack, byte[] needle, int start)
    {
        if (needle.Length == 0 || start + needle.Length > haystack.Length) return -1;
        for (var i = start; i <= haystack.Length - needle.Length; i++)
        {
            var match = true;
            for (var j = 0; j < needle.Length; j++)
                if (haystack[i + j] != needle[j]) { match = false; break; }
            if (match) return i;
        }
        return -1;
    }

    private static byte[] ParseHex(string s)
    {
        s = s.Replace(" ", "").Replace("-", "");
        if (s.StartsWith("0x", StringComparison.OrdinalIgnoreCase)) s = s[2..];
        if (s.Length % 2 != 0) throw BackendError.BadInput("hex pattern must have even length");
        var bytes = new byte[s.Length / 2];
        for (var i = 0; i < bytes.Length; i++)
            bytes[i] = byte.Parse(s.AsSpan(i * 2, 2), System.Globalization.NumberStyles.HexNumber);
        return bytes;
    }
}

public sealed record IlSearchHit(string MethodToken, string MethodName, string Offset, string Line);
public sealed record BytesHit(string Section, uint Rva, string Bytes);
