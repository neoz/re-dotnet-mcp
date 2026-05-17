namespace ReDotnet.Core.Envelope;

public sealed class BackendError : Exception
{
    public string Code { get; }
    public IReadOnlyDictionary<string, object?>? Detail { get; }

    public BackendError(string code, string message, IReadOnlyDictionary<string, object?>? detail = null)
        : base(message)
    {
        Code = code;
        Detail = detail;
    }

    public static BackendError NotFound(string what, string id) =>
        new("not-found", $"{what} not found: {id}", new Dictionary<string, object?> { ["what"] = what, ["id"] = id });

    public static BackendError BadInput(string message, IReadOnlyDictionary<string, object?>? detail = null) =>
        new("bad-input", message, detail);

    public static BackendError Ambiguous(string what, IReadOnlyList<object> candidates) =>
        new("ambiguous", $"{what} matches multiple candidates",
            new Dictionary<string, object?> { ["what"] = what, ["candidates"] = candidates });

    public static BackendError StrongNameRequired(string message, IReadOnlyDictionary<string, object?>? detail = null) =>
        new("strong-name-required", message, detail);
}
