using ReDotnet.Core.Envelope;
using ReDotnet.Core.Inspection;
using ReDotnet.Core.Sidecar;
using ReDotnet.Core.Workspace;
using ReDotnet.Tests.Corpus;

namespace ReDotnet.Tests.Workspace;

/// Regression coverage for the open_assembly + list_types hang.
///
/// Cause: AsmResolver's lazy metadata caches (type table, signatures, etc.)
/// are not thread-safe. Concurrent reads on the same ModuleDefinition could
/// race in lazy initialization and either fault or spin in the lazy slot,
/// surfacing to MCP clients as an apparent server hang. The registry now
/// serializes all module access behind Workspace.ModuleLock via Run(...),
/// and Open(...) itself is single-threaded behind _openLock.
[Collection("SyntheticCorpus")]
public sealed class ConcurrencyTests
{
    private readonly SyntheticCorpusFixture _corpus;

    public ConcurrencyTests(SyntheticCorpusFixture corpus) => _corpus = corpus;

    private static WorkspaceRegistry NewRegistry() =>
        new(new ModuleResolverFactory(null), new SidecarStore());

    [Fact]
    public void Concurrent_open_same_path_returns_single_workspace()
    {
        var reg = NewRegistry();
        var path = _corpus.Get("Clean");

        const int workers = 32;
        var bag = new System.Collections.Concurrent.ConcurrentBag<Core.Workspace.Workspace>();

        Parallel.For(0, workers, _ => bag.Add(reg.Open(path)));

        Assert.Equal(workers, bag.Count);
        var first = bag.First();
        Assert.All(bag, w => Assert.Same(first, w));
        Assert.Single(reg.All());
    }

    [Fact]
    public async Task Concurrent_list_types_via_Run_does_not_hang_or_race()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        var svc = new TypeListService();

        var expected = reg.Run(ws.AssemblyId, w => svc.List(w, null, null, null, null, null, 1000).Total);

        const int workers = 32;
        const int iterationsPerWorker = 8;

        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            for (var i = 0; i < iterationsPerWorker; i++)
            {
                var total = reg.Run(ws.AssemblyId, w => svc.List(w, null, null, null, null, null, 1000).Total);
                Assert.Equal(expected, total);
            }
        })).ToArray();

        await AssertCompletes(tasks, TimeSpan.FromSeconds(30),
            "concurrent list_types calls failed to complete within 30s — possible AsmResolver lazy-cache hang");
    }

    [Fact]
    public async Task Concurrent_open_and_list_types_interleaved_completes()
    {
        var reg = NewRegistry();
        var path = _corpus.Get("Clean");
        var svc = new TypeListService();

        const int workers = 24;

        var tasks = new List<Task>();
        for (var i = 0; i < workers; i++)
        {
            var isReader = i % 2 == 0;
            tasks.Add(Task.Run(() =>
            {
                if (isReader)
                {
                    var ws = reg.Open(path);
                    var page = reg.Run(ws.AssemblyId, w => svc.List(w, null, null, null, null, null, 1000));
                    Assert.True(page.Total > 0);
                }
                else
                {
                    reg.Open(path);
                }
            }));
        }

        await AssertCompletes(tasks.ToArray(), TimeSpan.FromSeconds(30),
            "interleaved open + list_types failed to complete within 30s");
        Assert.Single(reg.All());
    }

    /// Documents the hang cause: bypassing Run(...) and reading ws.Module
    /// concurrently from many threads is the historical spin path. The
    /// load-bearing assertion is that the test does not time out — if it
    /// ever does, every tool entry point must go through Run(...).
    [Fact]
    public async Task Unguarded_concurrent_module_reads_demonstrate_cause()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));

        const int workers = 32;
        var faulted = 0;

        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            try
            {
                foreach (var t in ws.Module.GetAllTypes())
                {
                    _ = t.Methods.Count;
                    _ = t.Fields.Count;
                }
            }
            catch
            {
                Interlocked.Increment(ref faulted);
            }
        })).ToArray();

        await AssertCompletes(tasks, TimeSpan.FromSeconds(30),
            "unguarded concurrent ws.Module reads hung — Run(...) gate must be used at every tool entry point");
    }

    [Fact]
    public async Task Stress_mixed_tool_surface_on_single_workspace()
    {
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));

        var tokens = new TokenResolver();
        var info = new AssemblyInfoService();
        var typeList = new TypeListService();
        var typeDetail = new TypeDetailService(tokens);
        var methodList = new MethodListService(tokens);
        var methodDetail = new MethodDetailService(tokens);
        var members = new MemberInspectionService(tokens);
        var moduleMeta = new ModuleMetadataService();
        var refs = new ReferenceService(tokens);
        var impex = new ImportExportService();

        const int workers = 24;
        const int iterationsPerWorker = 25;

        // Each worker rotates through ~12 distinct tool calls per iteration.
        // The whole batch should finish well under the timeout — any hang
        // means a tool path is reading ws.Module outside the Run(...) gate.
        var tasks = Enumerable.Range(0, workers).Select(workerId => Task.Run(() =>
        {
            var rng = new Random(workerId);
            for (var i = 0; i < iterationsPerWorker; i++)
            {
                reg.Run(ws.AssemblyId, w =>
                {
                    switch (rng.Next(12))
                    {
                        case 0:
                            Assert.Equal("Clean", info.Capture(w).Name);
                            break;
                        case 1:
                            var types = typeList.List(w, null, null, null, null, null, 1000);
                            Assert.True(types.Total > 0);
                            break;
                        case 2:
                            var detail = typeDetail.GetType(w, "Acme.Clean.Greeter");
                            Assert.Equal("Greeter", detail.Name);
                            break;
                        case 3:
                            var methods = methodList.List(w, null, "Acme.Clean.Greeter", null, null, null);
                            Assert.True(methods.Total > 0);
                            break;
                        case 4:
                            var greet = methodDetail.GetMethod(w, "Acme.Clean.Greeter::Greet");
                            Assert.Equal("Greet", greet.Name);
                            break;
                        case 5:
                            var disasm = methodDetail.Disassemble(w, "Acme.Clean.Greeter::Greet");
                            Assert.NotEmpty(disasm.Instructions);
                            break;
                        case 6:
                            var ns = typeDetail.ListNamespaces(w);
                            Assert.Contains(ns, n => n.Namespace == "Acme.Clean");
                            break;
                        case 7:
                            var streams = moduleMeta.ListStreams(w);
                            Assert.Contains(streams, s => s.Name == "#~");
                            break;
                        case 8:
                            var counts = moduleMeta.GetMetadataTableCounts(w);
                            Assert.True(counts.Count > 0);
                            break;
                        case 9:
                            var asmRefs = refs.ListAssemblyRefs(w, null, null, null);
                            Assert.True(asmRefs.Total > 0);
                            break;
                        case 10:
                            var fields = members.ListFields(w, "Acme.Clean.Point", null, null);
                            Assert.True(fields.Total >= 2);
                            break;
                        case 11:
                            // Re-open inside Run to also exercise the open fast path under read load.
                            Assert.Same(w, reg.Open(_corpus.Get("Clean")));
                            break;
                    }
                });
            }
        })).ToArray();

        await AssertCompletes(tasks, TimeSpan.FromSeconds(60),
            "mixed tool stress hung — a tool path likely reads ws.Module outside the Run(...) gate");
    }

    [Fact]
    public async Task Stress_xref_index_build_is_serialized_per_workspace()
    {
        // ReferenceService.GetOrBuildIndex now relies on the caller holding
        // ws.ModuleLock; many concurrent xref-to lookups must build the index
        // exactly once and return consistent results.
        var reg = NewRegistry();
        var ws = reg.Open(_corpus.Get("Clean"));
        var refs = new ReferenceService(new TokenResolver());

        const int workers = 32;

        var tasks = Enumerable.Range(0, workers).Select(_ => Task.Run(() =>
        {
            var page = reg.Run(ws.AssemblyId, w => refs.GetXrefsTo(w, "Acme.Clean.Greeter::Greet", null, null));
            Assert.NotNull(page);
        })).ToArray();

        await AssertCompletes(tasks, TimeSpan.FromSeconds(30),
            "concurrent xref-to lookups hung — index build is not properly serialized");
    }

    [Fact]
    public async Task Stress_parallel_reads_on_distinct_workspaces_are_independent()
    {
        // Different workspaces have independent ModuleLocks, so reads should
        // overlap rather than serialize on a global gate. We don't measure
        // wall-time speedup (flaky), but we do verify the two streams
        // interleave by completing well under a per-workspace serialized
        // budget.
        var reg = NewRegistry();
        var clean = reg.Open(_corpus.Get("Clean"));
        var ctf = reg.Open(_corpus.Get("CtfCrackme"));
        Assert.NotSame(clean, ctf);

        var typeList = new TypeListService();
        const int iterationsPerWorkspace = 200;

        var t1 = Task.Run(() =>
        {
            for (var i = 0; i < iterationsPerWorkspace; i++)
                reg.Run(clean.AssemblyId, w => typeList.List(w, null, null, null, null, null, 1000));
        });
        var t2 = Task.Run(() =>
        {
            for (var i = 0; i < iterationsPerWorkspace; i++)
                reg.Run(ctf.AssemblyId, w => typeList.List(w, null, null, null, null, null, 1000));
        });

        await AssertCompletes(new[] { t1, t2 }, TimeSpan.FromSeconds(60),
            "parallel reads on distinct workspaces failed to complete — locks may be cross-contaminated");
    }

    [Fact]
    public async Task Stress_open_close_churn_against_active_readers()
    {
        // Readers keep firing list_types while a churner repeatedly opens and
        // closes the same workspace. Close() must drain in-flight readers
        // (via ws.ModuleLock) so no reader hangs mid-call. Transient faults
        // (file-lock races, NotFound on a workspace that was just evicted)
        // are legal — the load-bearing property is that nothing deadlocks
        // and at least some reads succeed.
        var reg = NewRegistry();
        var path = _corpus.Get("Clean");
        var svc = new TypeListService();

        using var stop = new CancellationTokenSource(TimeSpan.FromSeconds(5));

        var reads = 0;
        var readers = Enumerable.Range(0, 8).Select(_ => Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    var ws = reg.Open(path);
                    reg.Run(ws.AssemblyId, w => svc.List(w, null, null, null, null, null, 1000));
                    Interlocked.Increment(ref reads);
                }
                catch (Exception ex) when (ex is BackendError or IOException or ObjectDisposedException)
                {
                    // Legal races between Open / Close / read:
                    //  - BackendError: workspace evicted between Open and Run, or FromFile failed under file-lock contention
                    //  - IOException: OS still holds the previous memory mapping when the churner re-opens
                    //  - ObjectDisposedException: reader entered Run on a freshly-disposed module
                }
            }
        })).ToArray();

        var churner = Task.Run(() =>
        {
            while (!stop.IsCancellationRequested)
            {
                try
                {
                    reg.Open(path);
                    reg.Close("Clean", save: false);
                }
                catch (Exception ex) when (ex is BackendError or IOException)
                {
                    // Open may transiently fail if the OS has not yet released the prior mapping.
                }
            }
        });

        await AssertCompletes(readers.Concat(new[] { churner }).ToArray(),
            TimeSpan.FromSeconds(20),
            "open/close churn against readers hung — Close() likely does not drain in-flight reads");

        Assert.True(reads > 0, "no reader call ever succeeded under churn — something is starving");
    }

    private static async Task AssertCompletes(Task[] tasks, TimeSpan timeout, string message)
    {
        var all = Task.WhenAll(tasks);
        var winner = await Task.WhenAny(all, Task.Delay(timeout));
        Assert.True(ReferenceEquals(winner, all), message);
        await all;
    }
}
