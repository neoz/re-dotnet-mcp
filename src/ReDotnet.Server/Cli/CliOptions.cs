namespace ReDotnet.Server.Cli;

public sealed class CliOptions
{
    public IReadOnlyList<string> SearchPaths { get; init; } = Array.Empty<string>();
    public string? LogFile { get; init; }
    public string? RulesFile { get; init; }
    public bool ShowHelp { get; init; }
    public bool ShowVersion { get; init; }
    public string? Error { get; init; }

    public static CliOptions Parse(string[] args)
    {
        var searchPaths = new List<string>();
        string? logFile = null;
        string? rulesFile = null;
        var showHelp = false;
        var showVersion = false;

        // Env-var defaults (overridden by explicit flags).
        var envSearch = Environment.GetEnvironmentVariable("REDOTNET_SEARCH_PATHS");
        if (!string.IsNullOrWhiteSpace(envSearch))
            searchPaths.AddRange(SplitPaths(envSearch));
        var envRules = Environment.GetEnvironmentVariable("REDOTNET_RULES");
        if (!string.IsNullOrWhiteSpace(envRules)) rulesFile = envRules;

        for (var i = 0; i < args.Length; i++)
        {
            var a = args[i];
            switch (a)
            {
                case "--help" or "-h":
                    showHelp = true; break;
                case "--version" or "-V":
                    showVersion = true; break;
                case "--search-paths":
                    if (++i >= args.Length) return new CliOptions { Error = "--search-paths requires a value" };
                    searchPaths.Clear();
                    searchPaths.AddRange(SplitPaths(args[i]));
                    break;
                case "--log-file":
                    if (++i >= args.Length) return new CliOptions { Error = "--log-file requires a value" };
                    logFile = args[i];
                    break;
                case "--rules":
                    if (++i >= args.Length) return new CliOptions { Error = "--rules requires a value" };
                    rulesFile = args[i];
                    break;
                default:
                    return new CliOptions { Error = $"unknown argument '{a}'" };
            }
        }

        return new CliOptions
        {
            SearchPaths = searchPaths,
            LogFile = logFile,
            RulesFile = rulesFile,
            ShowHelp = showHelp,
            ShowVersion = showVersion,
        };
    }

    private static IEnumerable<string> SplitPaths(string value)
    {
        var sep = OperatingSystem.IsWindows() ? ';' : ':';
        return value.Split(sep, StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
    }

    public static void PrintHelp(TextWriter w)
    {
        w.WriteLine("re-dotnet — MCP server for .NET assembly inspection and IL patching");
        w.WriteLine();
        w.WriteLine("Usage:");
        w.WriteLine("  re-dotnet [options]");
        w.WriteLine();
        w.WriteLine("Options:");
        w.WriteLine("  --search-paths PATHS  Semicolon/colon separated paths to probe for");
        w.WriteLine("                         AssemblyRef resolution (or set REDOTNET_SEARCH_PATHS).");
        w.WriteLine("  --log-file PATH       Write JSON-lines log to PATH (default: no logging).");
        w.WriteLine("  --rules PATH          YAML file with dangerous_apis rule overrides");
        w.WriteLine("                         (or set REDOTNET_RULES).");
        w.WriteLine("  --version, -V         Print version and exit.");
        w.WriteLine("  --help, -h            Print this help and exit.");
        w.WriteLine();
        w.WriteLine("Transport: stdio. Plug into any MCP client.");
    }
}
