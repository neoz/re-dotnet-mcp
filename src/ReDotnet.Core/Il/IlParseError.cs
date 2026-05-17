namespace ReDotnet.Core.Il;

public sealed record IlParseError(int Line, int Column, string Snippet, string Expected, string Found);

public sealed class IlParseException : Exception
{
    public IReadOnlyList<IlParseError> Errors { get; }
    public IlParseException(IReadOnlyList<IlParseError> errors)
        : base($"IL parse failed with {errors.Count} error(s); first: {errors[0].Found} (line {errors[0].Line})")
    {
        Errors = errors;
    }
}
