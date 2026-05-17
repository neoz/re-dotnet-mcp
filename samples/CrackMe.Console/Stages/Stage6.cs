using System;
using System.IO;
using System.Reflection;
using System.Text;
using CrackMe.Internal;

namespace CrackMe.Stages;

internal static class _0xS6
{
    private const string _resName = "CrackMe.Console.Resources.Stage6.blob";
    private static readonly string _frag = Decrypt();

    public static bool Probe(string input, ref int pos)
    {
        if (input.Length - pos < _frag.Length)
        {
            return false;
        }

        for (int i = 0; i < _frag.Length; i++)
        {
            if (input[pos + i] != _frag[i])
            {
                return false;
            }
        }

        pos += _frag.Length;
        return true;
    }

    private static string Decrypt()
    {
        var asm = typeof(_0xS6).Assembly;
        using var s = asm.GetManifestResourceStream(_resName);
        if (s is null) { return string.Empty; }
        using var ms = new MemoryStream();
        s.CopyTo(ms);
        var cipher = ms.ToArray();

        var attr = asm.GetCustomAttribute<CrackMeChallengeAttribute>();
        var key = Encoding.UTF8.GetBytes(attr?.Material ?? string.Empty);
        if (key.Length == 0) { return string.Empty; }

        var plain = _0xStr.KeyXor(cipher, key);
        return Encoding.UTF8.GetString(plain);
    }
}
