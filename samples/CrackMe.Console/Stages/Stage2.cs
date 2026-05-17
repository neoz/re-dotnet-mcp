using System.Text;

namespace CrackMe.Stages;

internal static class _0xS2
{
    private static readonly byte[] _ct = { 0x30, 0x71, 0x1D };
    private static readonly string _frag;

    static _0xS2()
    {
        var buf = new byte[_ct.Length];
        for (int i = 0; i < _ct.Length; i++)
        {
            buf[i] = (byte)(_ct[i] ^ 0x42);
        }
        _frag = Encoding.UTF8.GetString(buf);
    }

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
}
