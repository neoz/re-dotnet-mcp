using AsmResolver.DotNet;
using AsmResolver.DotNet.Serialized;

namespace ReDotnet.Core.Workspace;

/// Builds the ReaderParameters used when loading modules. Holds the configured
/// search paths so opt-in AssemblyRef resolution (PRD 11.5) hits a consistent set
/// of directories across every open call.
public sealed class ModuleResolverFactory
{
    private readonly IReadOnlyList<string> _searchPaths;

    public ModuleResolverFactory(IEnumerable<string>? searchPaths)
    {
        _searchPaths = searchPaths?
            .Where(p => !string.IsNullOrWhiteSpace(p))
            .Select(Path.GetFullPath)
            .ToList() ?? (IReadOnlyList<string>)Array.Empty<string>();
    }

    public IReadOnlyList<string> SearchPaths => _searchPaths;

    public ModuleReaderParameters CreateReaderParameters()
    {
        var parameters = new ModuleReaderParameters();
        // Caller can attach a DotNetCoreAssemblyResolver post-load via
        // module.MetadataResolver if it wants AssemblyRef chasing; that is opt-in
        // per PRD 11.5 and not done by default in M0/M1.
        return parameters;
    }

    public IAssemblyResolver? BuildResolver()
    {
        if (_searchPaths.Count == 0) return null;
        var parameters = new ModuleReaderParameters();
        var resolver = new DotNetCoreAssemblyResolver(new Version(9, 0, 0), null, parameters);
        foreach (var path in _searchPaths)
            resolver.SearchDirectories.Add(path);
        return resolver;
    }
}
