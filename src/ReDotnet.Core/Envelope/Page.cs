namespace ReDotnet.Core.Envelope;

public sealed record Page<T>(
    IReadOnlyList<T> Items,
    int Offset,
    int Limit,
    int Total,
    bool HasMore)
{
    public const int DefaultLimit = 100;
    public const int MaxLimit = 1000;

    public static Page<T> From(IReadOnlyList<T> all, int? offset, int? limit)
    {
        var off = Math.Max(0, offset ?? 0);
        var lim = Math.Clamp(limit ?? DefaultLimit, 1, MaxLimit);
        var total = all.Count;
        if (off >= total)
            return new Page<T>(Array.Empty<T>(), off, lim, total, false);
        var take = Math.Min(lim, total - off);
        var slice = new T[take];
        for (var i = 0; i < take; i++) slice[i] = all[off + i];
        return new Page<T>(slice, off, lim, total, off + take < total);
    }

    public static Page<T> FromEnumerable(IEnumerable<T> source, int? offset, int? limit)
    {
        return From(source as IReadOnlyList<T> ?? source.ToList(), offset, limit);
    }
}
