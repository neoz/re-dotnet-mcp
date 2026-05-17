using System;
using System.Reflection;

namespace CrackMe.Internal;

internal static class _0xTamp
{
    public static uint FingerprintOf(MethodInfo method)
    {
        var body = method.GetMethodBody();
        if (body is null)
        {
            return 0xDEADBEEF;
        }
        var il = body.GetILAsByteArray() ?? Array.Empty<byte>();
        uint h = 2166136261u;
        for (int i = 0; i < il.Length; i++)
        {
            h ^= il[i];
            h *= 16777619u;
        }
        return h;
    }

    public static int Salt()
    {
        int t = Environment.TickCount;
        return (t ^ t) & 0xFF;
    }
}
