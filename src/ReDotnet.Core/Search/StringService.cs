using System.Text.RegularExpressions;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using AsmResolver.PE.DotNet.Metadata;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;

namespace ReDotnet.Core.Search;

public sealed class StringService
{
    public Page<UserStringEntry> ListUserStrings(Workspace.Workspace ws, string? filterRegex, int minLength, int? offset, int? limit)
    {
        var rx = filterRegex is null ? null : new Regex(filterRegex);
        var stream = ws.Module.DotNetDirectory?.Metadata?.GetStream<UserStringsStream>();
        if (stream is null) return Page<UserStringEntry>.From(Array.Empty<UserStringEntry>(), offset, limit);

        var all = new List<UserStringEntry>();
        foreach (var (index, value) in stream.EnumerateStrings())
        {
            if (value.Length < minLength) continue;
            if (rx is not null && !rx.IsMatch(value)) continue;
            all.Add(new UserStringEntry($"#US:{index:X}", value));
        }
        return Page<UserStringEntry>.From(all, offset, limit);
    }

    public Page<LdstrEntry> ListLdstrStrings(Workspace.Workspace ws, string? filterRegex, int? offset, int? limit)
    {
        var rx = filterRegex is null ? null : new Regex(filterRegex);
        var stringToMethods = new Dictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var m in t.Methods)
            {
                var body = m.CilMethodBody;
                if (body is null) continue;
                foreach (var instr in body.Instructions)
                {
                    if (instr.OpCode.Code == CilCode.Ldstr && instr.Operand is string s)
                    {
                        if (rx is not null && !rx.IsMatch(s)) continue;
                        if (!stringToMethods.TryGetValue(s, out var lst))
                            stringToMethods[s] = lst = new();
                        lst.Add(TokenFormatHelper.Hex(m));
                    }
                }
            }
        }
        var all = stringToMethods.Select(kv => new LdstrEntry(kv.Key, kv.Value)).ToList();
        return Page<LdstrEntry>.From(all, offset, limit);
    }

    public Page<CodeStringMatch> FindCodeByString(Workspace.Workspace ws, string regex, int? offset, int? limit)
    {
        var rx = new Regex(regex);
        var matches = new List<CodeStringMatch>();
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var m in t.Methods)
            {
                var body = m.CilMethodBody;
                if (body is null) continue;
                foreach (var instr in body.Instructions)
                {
                    if (instr.OpCode.Code == CilCode.Ldstr && instr.Operand is string s && rx.IsMatch(s))
                    {
                        matches.Add(new CodeStringMatch(
                            MethodToken: TokenFormatHelper.Hex(m),
                            MethodName: m.FullName,
                            Offset: $"IL_{instr.Offset:X4}",
                            String: s));
                    }
                }
            }
        }
        return Page<CodeStringMatch>.From(matches, offset, limit);
    }
}

public sealed record UserStringEntry(string Index, string Value);
public sealed record LdstrEntry(string Value, IReadOnlyList<string> MethodTokens);
public sealed record CodeStringMatch(string MethodToken, string MethodName, string Offset, string String);
