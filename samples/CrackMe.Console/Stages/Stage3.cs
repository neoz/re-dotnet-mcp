using CrackMe.Internal;

namespace CrackMe.Stages;

internal static class _0xS3
{
    private const string _frag = "d0tn3t_";

    public static bool Probe(string input, ref int pos)
    {
        uint t = _0xPI.GetTickCount();
        bool dbg = _0xPI.IsDebuggerPresent();
        int gate = (int)(t & 0u);
        if (dbg) { gate |= 1; }

        if (gate != 0)
        {
            return false;
        }

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
