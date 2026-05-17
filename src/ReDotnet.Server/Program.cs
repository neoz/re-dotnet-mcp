using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Cli;
using ReDotnet.Server.Tools;

namespace ReDotnet.Server;

public static class Program
{
    public static async Task<int> Main(string[] args)
    {
        var parsed = CliOptions.Parse(args);
        if (parsed.ShowHelp)
        {
            CliOptions.PrintHelp(Console.Out);
            return 0;
        }
        if (parsed.ShowVersion)
        {
            Console.Out.WriteLine($"re-dotnet {ThisAssembly.InformationalVersion}");
            return 0;
        }
        if (parsed.Error is { } err)
        {
            Console.Error.WriteLine($"re-dotnet: {err}");
            CliOptions.PrintHelp(Console.Error);
            return 2;
        }

        var builder = Host.CreateEmptyApplicationBuilder(settings: null);

        builder.Services.AddSingleton(new ModuleResolverFactory(parsed.SearchPaths));
        builder.Services.AddSingleton<SidecarStore>();
        builder.Services.AddSingleton<WorkspaceRegistry>();
        builder.Services.AddSingleton<TokenResolver>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.AssemblyInfoService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.TypeListService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.MethodListService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.ModuleMetadataService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.TypeDetailService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.MethodDetailService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.MemberInspectionService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.ReferenceService>();
        builder.Services.AddSingleton<ReDotnet.Core.Inspection.ImportExportService>();
        builder.Services.AddSingleton<ReDotnet.Core.Search.StringService>();
        builder.Services.AddSingleton<ReDotnet.Core.Search.IlSearchService>();
        builder.Services.AddSingleton(sp => new ReDotnet.Core.Search.DangerousApiService(parsed.RulesFile));
        builder.Services.AddSingleton<ReDotnet.Core.Resources.ResourceService>();
        builder.Services.AddSingleton<ReDotnet.Core.Sidecar.SidecarReadService>();
        builder.Services.AddSingleton<ReDotnet.Core.Mutation.SidecarMutationService>();
        builder.Services.AddSingleton<ReDotnet.Core.Mutation.RenameService>();
        builder.Services.AddSingleton<ReDotnet.Core.Mutation.IlPatchService>();
        builder.Services.AddSingleton<ReDotnet.Core.Mutation.UndoRedoService>();
        builder.Services.AddSingleton<ReDotnet.Core.Save.SaveService>();
        builder.Services.AddSingleton<MetaTools.ToolCatalog>();
        builder.Services.AddSingleton(parsed);

        builder.Logging.ClearProviders();
        if (parsed.LogFile is { Length: > 0 } logPath)
        {
            var dir = Path.GetDirectoryName(Path.GetFullPath(logPath));
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);
            builder.Logging.AddProvider(new JsonLineFileLoggerProvider(logPath));
            builder.Logging.SetMinimumLevel(LogLevel.Information);
        }
        else
        {
            builder.Logging.SetMinimumLevel(LogLevel.Warning);
        }

        builder.Services
            .AddMcpServer(opts =>
            {
                opts.ServerInfo = new ModelContextProtocol.Protocol.Implementation
                {
                    Name = "re-dotnet",
                    Version = ThisAssembly.InformationalVersion,
                };
            })
            .WithStdioServerTransport()
            .WithTools<LifecycleTools>()
            .WithTools<TypeTools>()
            .WithTools<MethodTools>()
            .WithTools<ModuleTools>()
            .WithTools<TokenTools>()
            .WithTools<ModuleMetadataTools>()
            .WithTools<TypeDetailTools>()
            .WithTools<MethodDetailTools>()
            .WithTools<MemberTools>()
            .WithTools<ReferenceTools>()
            .WithTools<ImportExportTools>()
            .WithTools<SearchTools>()
            .WithTools<ResourceTools>()
            .WithTools<SidecarTools>()
            .WithTools<MutationTools>()
            .WithTools<SaveTools>()
            .WithTools<MetaTools.MetaTools>();

        try
        {
            await builder.Build().RunAsync().ConfigureAwait(false);
            return 0;
        }
        catch (Exception ex)
        {
            await Console.Error.WriteLineAsync($"re-dotnet: fatal: {ex.Message}");
            return 3;
        }
    }
}

internal static class ThisAssembly
{
    public static string InformationalVersion =>
        typeof(ThisAssembly).Assembly
            .GetCustomAttributes(typeof(System.Reflection.AssemblyInformationalVersionAttribute), false)
            .Cast<System.Reflection.AssemblyInformationalVersionAttribute>()
            .FirstOrDefault()?.InformationalVersion
        ?? "0.1.0-dev";
}
