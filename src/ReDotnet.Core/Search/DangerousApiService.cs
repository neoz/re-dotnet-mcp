using System.Reflection;
using System.Text.Json;
using AsmResolver.DotNet;
using AsmResolver.PE.DotNet.Cil;
using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;

namespace ReDotnet.Core.Search;

public sealed class DangerousApiService
{
    private readonly List<DangerousApiRule> _rules;

    public DangerousApiService(string? userRulesFile)
    {
        _rules = new List<DangerousApiRule>(LoadBundled());
        if (!string.IsNullOrEmpty(userRulesFile) && File.Exists(userRulesFile))
            MergeUserRules(userRulesFile);
    }

    public IReadOnlyList<DangerousApiRule> Rules => _rules;

    public Page<DangerousApiHit> Find(Workspace.Workspace ws, string? preset, int? offset, int? limit)
    {
        var active = preset is null
            ? _rules
            : _rules.Where(r => string.Equals(r.Preset, preset, StringComparison.OrdinalIgnoreCase)).ToList();

        var hits = new List<DangerousApiHit>();
        foreach (var t in ws.Module.GetAllTypes())
        {
            foreach (var m in t.Methods)
            {
                var body = m.CilMethodBody;
                if (body is null) continue;
                foreach (var instr in body.Instructions)
                {
                    if (instr.OpCode.OperandType != CilOperandType.InlineMethod
                        && instr.OpCode.OperandType != CilOperandType.InlineField) continue;
                    if (instr.Operand is not IMetadataMember mm) continue;

                    foreach (var rule in active)
                    {
                        if (Matches(rule, mm))
                        {
                            hits.Add(new DangerousApiHit(
                                RuleId: rule.Id,
                                Preset: rule.Preset,
                                Severity: rule.Severity,
                                Rationale: rule.Rationale,
                                MethodToken: TokenFormatHelper.Hex(m),
                                MethodName: m.FullName,
                                Offset: $"IL_{instr.Offset:X4}",
                                Operand: NameOf(mm)));
                        }
                    }
                }
            }
        }
        return Page<DangerousApiHit>.From(hits, offset, limit);
    }

    private static bool Matches(DangerousApiRule rule, IMetadataMember mm)
    {
        var declaringType = (mm as IMemberDescriptor)?.DeclaringType?.FullName
            ?? (mm as TypeDefinition)?.FullName
            ?? (mm as TypeReference)?.FullName;
        if (declaringType != rule.MatchType) return false;
        var memberName = (mm as INameProvider)?.Name?.ToString();
        return memberName == rule.MatchMember;
    }

    private static string NameOf(IMetadataMember m) => m switch
    {
        IFullNameProvider fn => fn.FullName,
        INameProvider n => n.Name?.ToString() ?? "?",
        _ => m.ToString() ?? "?",
    };

    private static IEnumerable<DangerousApiRule> LoadBundled()
    {
        var asm = Assembly.GetExecutingAssembly();
        foreach (var name in asm.GetManifestResourceNames())
        {
            if (!name.EndsWith(".json", StringComparison.OrdinalIgnoreCase)) continue;
            using var s = asm.GetManifestResourceStream(name);
            if (s is null) continue;
            var rules = JsonSerializer.Deserialize<List<DangerousApiRule>>(s) ?? new();
            foreach (var r in rules) yield return r;
        }
    }

    private void MergeUserRules(string yamlPath)
    {
        try
        {
            var yamlText = File.ReadAllText(yamlPath);
            var deserializer = new YamlDotNet.Serialization.DeserializerBuilder()
                .IgnoreUnmatchedProperties()
                .Build();
            var userRules = deserializer.Deserialize<List<DangerousApiRule>>(yamlText) ?? new();
            foreach (var u in userRules)
            {
                var idx = _rules.FindIndex(r => r.Id == u.Id);
                if (idx >= 0) _rules[idx] = u; else _rules.Add(u);
            }
        }
        catch (Exception ex)
        {
            throw BackendError.BadInput($"failed to load user rules from {yamlPath}: {ex.Message}");
        }
    }
}

public sealed class DangerousApiRule
{
    public string Id { get; set; } = "";
    public string Preset { get; set; } = "";
    public string MatchType { get; set; } = "";
    public string MatchMember { get; set; } = "";
    public string Severity { get; set; } = "medium";
    public string Rationale { get; set; } = "";
}

public sealed record DangerousApiHit(
    string RuleId, string Preset, string Severity, string Rationale,
    string MethodToken, string MethodName, string Offset, string Operand);
