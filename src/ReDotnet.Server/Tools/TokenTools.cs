using System.ComponentModel;
using ModelContextProtocol.Server;
using ReDotnet.Core.Workspace;
using ReDotnet.Server.Envelope;

namespace ReDotnet.Server.Tools;

[McpServerToolType]
public sealed class TokenTools
{
    private readonly WorkspaceRegistry _registry;
    private readonly TokenResolver _resolver;

    public TokenTools(WorkspaceRegistry registry, TokenResolver resolver)
    {
        _registry = registry;
        _resolver = resolver;
    }

    [McpServerTool(Name = "resolve_token")]
    [Description("Resolve an identifier (hex token, FQN, or @0xRVA) to a canonical member handle.")]
    public ResolveTokenResultDto ResolveToken(
        [Description("Assembly id.")] string id,
        [Description("Identifier in any of the four supported forms.")] string identifier)
        => _registry.Run(id, ws =>
        {
            var m = _resolver.Resolve(ws, identifier);
            var kind = TokenHelpers.KindOf(m);
            var name = (m as AsmResolver.DotNet.INameProvider)?.Name?.ToString() ?? "<anonymous>";
            var fullName = (m as AsmResolver.DotNet.IFullNameProvider)?.FullName;
            return new ResolveTokenResultDto(TokenHelpers.FormatToken(m.MetadataToken), kind, name, fullName);
        });
}
