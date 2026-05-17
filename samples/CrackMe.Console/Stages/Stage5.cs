using System;

namespace CrackMe.Stages;

internal static class _0xS5
{
    private const int _len = 6;

    private static readonly byte[] _expected =
    {
        0x25, 0x25, 0x41, 0x6F, 0x3D, 0x19
    };

    public static bool Probe(string input, ref int pos)
    {
        if (input.Length - pos < _len)
        {
            return false;
        }

        for (int i = 0; i < _len; i++)
        {
            byte c;
            try
            {
                c = checked((byte)input[pos + i]);
            }
            catch (OverflowException)
            {
                return false;
            }

            byte transformed = (byte)(((c ^ 0x55) + (i * 3)) & 0xFF);
            if (transformed != _expected[i])
            {
                return false;
            }
        }

        pos += _len;
        return true;
    }
}
