using System.ComponentModel;
using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using ModelContextProtocol.Server;

namespace ReDotnet.Server.MetaTools;

[McpServerToolType]
public sealed class MetaTools
{
    private readonly ToolCatalog _catalog;
    private readonly IServiceProvider _services;

    public MetaTools(ToolCatalog catalog, IServiceProvider services)
    {
        _catalog = catalog;
        _services = services;
    }

    [McpServerTool(Name = "search_tools")]
    [Description("Search the full catalog of available tools, including ones hidden from the pinned set.")]
    public IReadOnlyList<ToolDescriptor> SearchTools(
        [Description("Optional substring or regex filter over tool names and descriptions.")] string? filter = null,
        [Description("If true, return only the pinned tools.")] bool pinned_only = false)
    {
        IEnumerable<ToolDescriptor> q = _catalog.All;
        if (pinned_only) q = q.Where(t => t.Pinned);
        if (!string.IsNullOrWhiteSpace(filter))
        {
            try
            {
                var rx = new System.Text.RegularExpressions.Regex(filter,
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase);
                q = q.Where(t => rx.IsMatch(t.Name) || rx.IsMatch(t.Description));
            }
            catch
            {
                q = q.Where(t =>
                    t.Name.Contains(filter, StringComparison.OrdinalIgnoreCase) ||
                    t.Description.Contains(filter, StringComparison.OrdinalIgnoreCase));
            }
        }
        return q.ToList();
    }

    [McpServerTool(Name = "get_schema")]
    [Description("Return the full schema (name, description, parameters) for a tool by name.")]
    public ToolDescriptor? GetSchema(
        [Description("Tool name (matches McpServerToolAttribute.Name).")] string tool_name)
        => _catalog.Get(tool_name);

    [McpServerTool(Name = "call")]
    [Description("Invoke any tool by name, passing arguments as a JSON object string.")]
    public object? Call(
        [Description("Tool name.")] string tool_name,
        [Description("Arguments serialized as JSON ({\"id\":\"Foo\",...}).")] string arguments_json = "{}")
    {
        var (instance, method) = LocateTool(tool_name);
        var json = JsonDocument.Parse(arguments_json).RootElement;
        var pars = method.GetParameters();
        var resolved = new object?[pars.Length];
        for (var i = 0; i < pars.Length; i++)
        {
            var p = pars[i];
            if (json.ValueKind == JsonValueKind.Object && json.TryGetProperty(p.Name!, out var prop))
            {
                resolved[i] = JsonSerializer.Deserialize(prop.GetRawText(), p.ParameterType);
            }
            else if (p.HasDefaultValue)
            {
                resolved[i] = p.DefaultValue;
            }
            else if (Nullable.GetUnderlyingType(p.ParameterType) is not null
                || !p.ParameterType.IsValueType)
            {
                resolved[i] = null;
            }
            else
            {
                throw new ArgumentException($"missing required parameter '{p.Name}' for tool '{tool_name}'");
            }
        }
        return method.Invoke(instance, resolved);
    }

    [McpServerTool(Name = "batch")]
    [Description("Invoke several tools in sequence; each input is {tool_name, arguments_json}.")]
    public IReadOnlyList<BatchResult> Batch(
        [Description("JSON array of {tool_name, arguments_json} objects.")] string calls_json)
    {
        var doc = JsonDocument.Parse(calls_json).RootElement;
        if (doc.ValueKind != JsonValueKind.Array)
            throw new ArgumentException("calls_json must be a JSON array");
        var results = new List<BatchResult>();
        foreach (var entry in doc.EnumerateArray())
        {
            var name = entry.GetProperty("tool_name").GetString() ?? "";
            var args = entry.TryGetProperty("arguments_json", out var a) ? a.GetRawText() : "{}";
            try
            {
                var result = Call(name, args);
                results.Add(new BatchResult(name, true, result, null));
            }
            catch (Exception ex)
            {
                results.Add(new BatchResult(name, false, null, ex.Message));
            }
        }
        return results;
    }

    [McpServerTool(Name = "execute")]
    [Description("Run a small DSL of tool calls; alias of batch with a different payload shape (one call per line).")]
    public IReadOnlyList<BatchResult> Execute(
        [Description("Each line is 'tool_name <json-args>'.")] string script)
    {
        var calls = new List<object>();
        foreach (var line in script.Split('\n', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var space = line.IndexOf(' ');
            var name = space < 0 ? line : line[..space];
            var args = space < 0 ? "{}" : line[(space + 1)..];
            calls.Add(new { tool_name = name, arguments_json = args });
        }
        return Batch(JsonSerializer.Serialize(calls));
    }

    private (object instance, MethodInfo method) LocateTool(string name)
    {
        foreach (var type in typeof(MetaTools).Assembly.GetTypes())
        {
            if (type.GetCustomAttribute<McpServerToolTypeAttribute>() is null) continue;
            foreach (var method in type.GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                var attr = method.GetCustomAttribute<McpServerToolAttribute>();
                if (attr is null) continue;
                if ((attr.Name ?? method.Name) == name)
                {
                    var instance = ActivatorUtilities.GetServiceOrCreateInstance(_services, type);
                    return (instance, method);
                }
            }
        }
        throw new ArgumentException($"unknown tool '{name}'");
    }
}

public sealed record BatchResult(string ToolName, bool Ok, object? Result, string? Error);
