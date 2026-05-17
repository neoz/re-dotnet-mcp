using System;
using System.Text;

namespace CrackMe.Internal;

internal static class _0xStr
{
    public static string Roll(byte[] src, byte seed, byte step)
    {
        var buf = new byte[src.Length];
        byte k = seed;
        for (int i = 0; i < src.Length; i++)
        {
            buf[i] = (byte)(src[i] ^ k);
            k = (byte)(k + step);
        }
        return Encoding.UTF8.GetString(buf);
    }

    public static byte[] KeyXor(byte[] cipher, byte[] key)
    {
        var buf = new byte[cipher.Length];
        for (int i = 0; i < cipher.Length; i++)
        {
            buf[i] = (byte)(cipher[i] ^ key[i % key.Length]);
        }
        return buf;
    }
}
