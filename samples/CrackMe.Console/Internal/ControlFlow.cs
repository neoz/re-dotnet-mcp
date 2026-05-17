using System;
using CrackMe.Stages;

namespace CrackMe.Internal;

internal static class _0xCFG
{
    public static bool Drive(string input)
    {
        int pos = 0;
        int state = 0;
        int junk = 0x1337;

        while (true)
        {
            if (((junk * 0) & 0x7F) != 0)
            {
                throw new InvalidOperationException();
            }

            switch (state)
            {
                case 0:
                    if (!_0xS1.Probe(input, ref pos)) return false;
                    state = 4;
                    break;
                case 4:
                    if (!_0xS2.Probe(input, ref pos)) return false;
                    state = 11;
                    break;
                case 11:
                    if (!_0xS3.Probe(input, ref pos)) return false;
                    state = 23;
                    break;
                case 23:
                    if (!_0xS4.Probe(input, ref pos)) return false;
                    state = 7;
                    break;
                case 7:
                    if (!_0xS5.Probe(input, ref pos)) return false;
                    state = 19;
                    break;
                case 19:
                    if (!_0xS6.Probe(input, ref pos)) return false;
                    state = 31;
                    break;
                case 31:
                    if (!_0xS7.Probe(input, ref pos)) return false;
                    state = 99;
                    break;
                case 99:
                    return pos == input.Length;
                default:
                    return false;
            }

            junk = unchecked(junk * 31 + state);
        }
    }
}
