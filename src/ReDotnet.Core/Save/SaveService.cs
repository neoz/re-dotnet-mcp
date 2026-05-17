using AsmResolver.DotNet.Builder;
using AsmResolver.PE.DotNet.StrongName;
using ReDotnet.Core.Envelope;

namespace ReDotnet.Core.Save;

public sealed class SaveService
{
    public SaveResult SaveAssembly(
        Workspace.Workspace ws,
        string? path,
        string strongNameStrategy,
        string? strongNameKeyFile)
    {
        var outPath = path ?? DefaultOutputPath(ws.OriginalPath);
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outPath))!);

        var module = ws.Module;
        var hadStrongName = module.Assembly?.HasPublicKey == true;
        string snApplied;

        switch (strongNameStrategy)
        {
            case "preserve":
                if (hadStrongName)
                    throw BackendError.StrongNameRequired(
                        "module is strong-named and 'preserve' would invalidate the signature; pick strip, resign, or force-invalid",
                        new Dictionary<string, object?>
                        {
                            ["options"] = new[] { "strip", "resign", "force-invalid" },
                        });
                module.Write(outPath);
                snApplied = "preserve (unsigned)";
                break;

            case "strip":
                if (hadStrongName)
                {
                    module.Assembly!.PublicKey = null;
                }
                module.Write(outPath);
                snApplied = "strip";
                break;

            case "resign":
                if (string.IsNullOrEmpty(strongNameKeyFile) || !File.Exists(strongNameKeyFile))
                    throw BackendError.BadInput("'resign' requires an existing strong_name_key file");
                var snk = StrongNamePrivateKey.FromFile(strongNameKeyFile);
                var factory = new DotNetDirectoryFactory { StrongNamePrivateKey = snk };
                var imageBuilder = new ManagedPEImageBuilder(factory);
                module.Write(outPath, imageBuilder);
                snApplied = "resign";
                break;

            case "force-invalid":
                // Keep the SN header but skip rehash — exactly the default write path
                // since AsmResolver does not recompute the SN hash unless asked to.
                module.Write(outPath);
                snApplied = "force-invalid";
                break;

            default:
                throw BackendError.BadInput(
                    $"unknown strong_name_strategy '{strongNameStrategy}'; expected preserve|strip|resign|force-invalid");
        }

        var bytes = new FileInfo(outPath).Length;
        return new SaveResult(outPath, bytes, snApplied);
    }

    public string SaveSidecarOnly(Workspace.Workspace ws)
    {
        ws.SaveSidecar();
        return ws.AssemblyId;
    }

    private static string DefaultOutputPath(string original)
    {
        var dir = Path.GetDirectoryName(original) ?? ".";
        var stem = Path.GetFileNameWithoutExtension(original);
        var ext = Path.GetExtension(original);
        return Path.Combine(dir, stem + ".patched" + ext);
    }
}

public sealed record SaveResult(string OutputPath, long BytesWritten, string SnStrategyApplied);
