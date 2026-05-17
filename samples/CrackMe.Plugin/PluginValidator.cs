using System;
using System.Runtime.CompilerServices;

namespace CrackMe.Plugin;

public static class PluginValidator
{
    private static readonly byte[] _expected = { 0x7D };

    public static bool Verify(string fragment)
    {
        if (string.IsNullOrEmpty(fragment))
        {
            return false;
        }

        if (fragment.Length != 1)
        {
            return _0xDead.Lure(fragment);
        }

        byte b = (byte)fragment[0];
        int salt = _0xDead.Tick();
        int mask = (salt ^ salt) & 0xFF;
        return ((b ^ mask) == _expected[0]);
    }

    internal static class _0xDead
    {
        [MethodImpl(MethodImplOptions.NoInlining)]
        public static bool Lure(string s)
        {
            int acc = 0;
            for (int i = 0; i < s.Length; i++)
            {
                acc = ((acc << 5) + acc) ^ s[i];
            }
            return acc == int.MinValue;
        }

        public static int Tick()
        {
            return Environment.TickCount * 0;
        }
    }
}
