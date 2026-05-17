using System;
using System.Reflection;
using CrackMe.Internal;

namespace CrackMe.Stages;

internal static class _0xS7
{
    private const int _len = 1;

    private static readonly byte[] _typeCt =
    {
        0x39, 0xFF, 0xC1, 0xD0, 0xAD, 0x94, 0x89, 0xD1,
        0x42, 0x49, 0x4D, 0x2C, 0x37, 0x1F, 0xAA, 0xC7,
        0xC6, 0xC8, 0xB7, 0x8A, 0x98, 0x5F, 0x7D, 0x43,
        0x2B, 0x31, 0x09, 0x0F, 0xE1, 0xD3
    };

    private static readonly byte[] _methCt =
    {
        0x2C, 0xE8, 0xD2, 0xDA, 0xA0, 0xA0
    };

    public static bool Probe(string input, ref int pos)
    {
        if (input.Length - pos < _len)
        {
            return false;
        }
        var frag = input.Substring(pos, _len);

        var typeName = _0xStr.Roll(_typeCt, 0x7A, 0x13);
        var methName = _0xStr.Roll(_methCt, 0x7A, 0x13);

        var asmName = new AssemblyName("CrackMe.Plugin");
        var asm = Assembly.Load(asmName);
        var t = asm.GetType(typeName);
        var m = t?.GetMethod(methName, BindingFlags.Public | BindingFlags.Static);
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
}
