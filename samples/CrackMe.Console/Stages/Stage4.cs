using System;
using System.Reflection;
using CrackMe.Internal;

namespace CrackMe.Stages;

internal static class _0xS4
{
    private const int _len = 8;

    private static readonly byte[] _typeCt =
    {
        0x39, 0xFF, 0xC1, 0xD0, 0xAD, 0x94, 0x89, 0xD1,
        0x5B, 0x4B, 0x4C, 0x2E, 0x2C, 0x1F, 0xE5, 0xFB,
        0x84, 0xE2, 0xE0, 0x9B, 0xC2, 0x68, 0x2E, 0x4D
    };

    private static readonly byte[] _methCt =
    {
        0x25, 0xD2, 0xC3, 0xDB, 0xA3, 0xBA, 0x87
    };

    private static MethodInfo? _cached;

    public static bool Probe(string input, ref int pos)
    {
        if (input.Length - pos < _len)
        {
            return false;
        }
        var frag = input.Substring(pos, _len);

        var m = _cached ??= Resolve();
        if (m is null)
        {
            return false;
        }

        var ok = (bool)(m.Invoke(null, new object?[] { frag }) ?? false);
        if (!ok)
        {
            return false;
        }
        pos += _len;
        return true;
    }

    private static MethodInfo? Resolve()
    {
        var typeName = _0xStr.Roll(_typeCt, 0x7A, 0x13);
        var methName = _0xStr.Roll(_methCt, 0x7A, 0x13);
        var t = Type.GetType(typeName, throwOnError: false);
        return t?.GetMethod(methName, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
    }
}
