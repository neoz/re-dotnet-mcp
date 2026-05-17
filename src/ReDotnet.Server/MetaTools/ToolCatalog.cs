using System.ComponentModel;
using System.Reflection;
using ModelContextProtocol.Server;

namespace ReDotnet.Server.MetaTools;

/// Reflects over the Server's tool classes to expose a catalog used by
/// search_tools and get_schema. Avoids depending on MCP SDK internals so
/// it stays compatible across SDK preview bumps.
public sealed class ToolCatalog
{
    public IReadOnlyList<ToolDescriptor> All { get; }

    public ToolCatalog()
    {
        All = Discover().ToList();
    }

    public ToolDescriptor? Get(string name) =>
        All.FirstOrDefault(t => t.Name == name);

    private static IEnumerable<ToolDescriptor> Discover()
    {
        var asm = typeof(ToolCatalog).Assembly;
        foreach (var type in asm.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) continue;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var toolAttr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (toolAttr is null) continue;
                var name = toolAttr.Name ?? method.Name;
                var desc = method.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "";
                var pars = method.GetParameters().Select(p => new ToolParameter(
                    Name: p.Name ?? "_",
                    Type: SimpleTypeName(p.ParameterType),
                    Required: !p.IsOptional && !p.HasDefaultValue,
                    Description: p.GetCustomAttribute<DescriptionAttribute>()?.Description ?? "",
                    Default: p.HasDefaultValue ? p.DefaultValue?.ToString() : null)).ToList();

                yield return new ToolDescriptor(
                    Name: name,
                    Description: desc,
                    Pinned: PinningRegistry.Pinned.Contains(name),
                    HostType: type.FullName ?? type.Name,
                    Parameters: pars);
            }
        }
    }

    private static string SimpleTypeName(Type t)
    {
        var underlying = Nullable.GetUnderlyingType(t);
        if (underlying is not null) return SimpleTypeName(underlying) + "?";
        if (t == typeof(string)) return "string";
        if (t == typeof(int)) return "int";
        if (t == typeof(long)) return "long";
        if (t == typeof(uint)) return "uint";
        if (t == typeof(bool)) return "bool";
        if (t == typeof(double)) return "double";
        if (t == typeof(float)) return "float";
        return t.Name;
    }
}

public sealed record ToolDescriptor(
    string Name,
    string Description,
    bool Pinned,
    string HostType,
    IReadOnlyList<ToolParameter> Parameters);

public sealed record ToolParameter(
    string Name, string Type, bool Required, string Description, string? Default);
