using System;

namespace CrackMe.Stages;

internal static class _0xS1
{
    private const string _prefix = "FLAG{";

    public static bool Probe(string input, ref int pos)
    {
        if (input.Length - pos < _prefix.Length)
        {
            return false;
        }

        for (int i = 0; i < _prefix.Length; i++)
        {
            if (input[pos + i] != _prefix[i])
            {
                return false;
            }
        }

        pos += _prefix.Length;
        return true;
    }
}
