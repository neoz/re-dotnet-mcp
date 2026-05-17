using System.Collections.Immutable;
using System.Reflection;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.Emit;

namespace ReDotnet.Tests.Corpus;

/// Compiles the synthetic *.cs fixtures via Roslyn into a temp directory once
/// per test session, then exposes their paths. Rebuilding on every run keeps
/// the repo free of binaries and avoids licensing/malware concerns (PLAN section 7).
public sealed class SyntheticCorpusFixture : IDisposable
{
    public string Directory { get; }
    public IReadOnlyDictionary<string, string> AssemblyPaths { get; }

    public SyntheticCorpusFixture()
    {
        Directory = Path.Combine(Path.GetTempPath(), "redotnet-corpus-" + Guid.NewGuid().ToString("N")[..8]);
        System.IO.Directory.CreateDirectory(Directory);

        var fixturesRoot = LocateFixturesDir();
        var paths = new Dictionary<string, string>(StringComparer.Ordinal);

        foreach (var src in System.IO.Directory.EnumerateFiles(fixturesRoot, "*.cs"))
        {
            var name = Path.GetFileNameWithoutExtension(src);
            var outPath = Path.Combine(Directory, name + ".dll");
            Compile(src, outPath, OutputKind.DynamicallyLinkedLibrary);
            paths[name] = outPath;
        }

        AssemblyPaths = paths;
    }

    public string Get(string name) =>
        AssemblyPaths.TryGetValue(name, out var p)
            ? p
            : throw new KeyNotFoundException($"corpus fixture '{name}' not built (looked under {Directory})");

    private static string LocateFixturesDir()
    {
        var dir = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(dir))
        {
            var candidate = Path.Combine(dir, "Corpus", "Fixtures");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            candidate = Path.Combine(dir, "tests", "ReDotnet.Tests", "Corpus", "Fixtures");
            if (System.IO.Directory.Exists(candidate)) return candidate;
            dir = Path.GetDirectoryName(dir);
        }
        throw new InvalidOperationException("could not locate Corpus/Fixtures directory");
    }

    private static void Compile(string sourcePath, string outPath, OutputKind kind)
    {
        var source = File.ReadAllText(sourcePath);
        var tree = CSharpSyntaxTree.ParseText(source, path: sourcePath);
        var assemblyName = Path.GetFileNameWithoutExtension(outPath);

        var refs = ImmutableArray.CreateBuilder<MetadataReference>();
        var tpa = (string?)AppContext.GetData("TRUSTED_PLATFORM_ASSEMBLIES") ?? "";
        foreach (var dll in tpa.Split(Path.PathSeparator, StringSplitOptions.RemoveEmptyEntries))
        {
            if (File.Exists(dll)) refs.Add(MetadataReference.CreateFromFile(dll));
        }

        var compilation = CSharpCompilation.Create(
            assemblyName,
            new[] { tree },
            refs.ToArray(),
            new CSharpCompilationOptions(kind, optimizationLevel: OptimizationLevel.Debug));

        using var peStream = File.Create(outPath);
        var emit = compilation.Emit(peStream);
        if (!emit.Success)
        {
            var msgs = string.Join("\n", emit.Diagnostics
                .Where(d => d.Severity == DiagnosticSeverity.Error)
                .Select(d => d.ToString()));
            throw new InvalidOperationException($"failed to compile '{sourcePath}':\n{msgs}");
        }
    }

    public void Dispose()
    {
        try { if (System.IO.Directory.Exists(Directory)) System.IO.Directory.Delete(Directory, recursive: true); }
        catch { /* leave temp dir on cleanup failure; OS will reclaim it eventually */ }
    }
}

[CollectionDefinition("SyntheticCorpus")]
public sealed class SyntheticCorpusCollection : ICollectionFixture<SyntheticCorpusFixture> { }
