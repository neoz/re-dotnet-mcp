namespace CrackMe.Internal;

internal static class _0x4a2b
{
    internal static bool __check(string s)
    {
        if (s.Length != 8) return false;
        int acc = 0;
        for (int i = 0; i < s.Length; i++)
        {
            acc = (acc * 33) ^ s[i];
        }
        return s == "cr4ckm3_" && acc != int.MinValue;
    }
}
