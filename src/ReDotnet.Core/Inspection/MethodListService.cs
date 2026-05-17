using System.Text.RegularExpressions;
using AsmResolver.DotNet;
using ReDotnet.Core.Envelope;

namespace ReDotnet.Core.Inspection;

public sealed class MethodListService
{
    private readonly Workspace.TokenResolver _tokens;

    public MethodListService(Workspace.TokenResolver tokens) => _tokens = tokens;

    public Page<MethodSummary> List(
        Workspace.Workspace ws,
        string? filterRegex,
        string? type,
        string? modifier,
        int? offset,
        int? limit)
    {
        Regex? rx = null;
        if (!string.IsNullOrWhiteSpace(filterRegex))
        {
            try { rx = new Regex(filterRegex!, RegexOptions.Compiled); }
            catch (ArgumentException ex)
            {
                throw BackendError.BadInput($"invalid filter regex: {ex.Message}");
            }
        }

        IEnumerable<TypeDefinition> typesToScan;
        if (!string.IsNullOrWhiteSpace(type))
        {
            var resolved = _tokens.Resolve(ws, type!);
            if (resolved is not TypeDefinition td)
                throw BackendError.BadInput($"'type' did not resolve to a TypeDefinition: '{type}'");
            typesToScan = new[] { td };
        }
        else
        {
            typesToScan = ws.Module.GetAllTypes().Where(t => !t.IsModuleType);
        }

        var all = new List<MethodSummary>();
        foreach (var t in typesToScan)
        {
            foreach (var m in t.Methods)
            {
                if (rx is not null && !rx.IsMatch($"{t.FullName}::{m.Name}")) continue;
                if (modifier is not null && !MatchesModifier(m, modifier)) continue;
                all.Add(Build(m, t));
            }
        }
        return Page<MethodSummary>.From(all, offset, limit);
    }

    public static MethodSummary Build(MethodDefinition m, TypeDefinition? owner = null)
    {
        var t = owner ?? m.DeclaringType;
        return new MethodSummary(
            Token: TokenFormatHelper.Hex(m),
            Name: m.Name?.ToString() ?? "",
            DeclaringType: t?.FullName ?? "<global>",
            Signature: Signature(m),
            IsStatic: m.IsStatic,
            IsVirtual: m.IsVirtual,
            IsAbstract: m.IsAbstract,
            Visibility: VisOf(m),
            CodeSize: m.CilMethodBody?.Instructions.Sum(i => i.Size));
    }

    private static bool MatchesModifier(MethodDefinition m, string modifier)
    {
        return modifier.ToLowerInvariant() switch
        {
            "static" => m.IsStatic,
            "instance" => !m.IsStatic,
            "virtual" => m.IsVirtual,
            "abstract" => m.IsAbstract,
            "public" => m.IsPublic,
            "private" => m.IsPrivate,
            "internal" => m.IsAssembly,
            "protected" => m.IsFamily,
            _ => true,
        };
    }

    private static string Signature(MethodDefinition m)
    {
        var ret = m.Signature?.ReturnType?.FullName ?? "?";
        var pars = string.Join(", ", m.Parameters.Select(p =>
            $"{p.ParameterType?.FullName ?? "?"} {p.Name ?? $"arg{p.Index}"}"));
        return $"{ret} {m.Name}({pars})";
    }

    public static string VisOf(MethodDefinition m)
    {
        if (m.IsPublic) return "public";
        if (m.IsAssembly) return "internal";
        if (m.IsFamily) return "protected";
        if (m.IsFamilyOrAssembly) return "protected_or_internal";
        if (m.IsFamilyAndAssembly) return "protected_and_internal";
        if (m.IsPrivate) return "private";
        return "unknown";
    }
}

public sealed record MethodSummary(
    string Token,
    string Name,
    string DeclaringType,
    string Signature,
    bool IsStatic,
    bool IsVirtual,
    bool IsAbstract,
    string Visibility,
    int? CodeSize);
