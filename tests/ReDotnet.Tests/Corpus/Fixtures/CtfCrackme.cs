// Synthetic CTF crackme — exposes a method whose result we can flip via
// a single brfalse/brtrue patch.

namespace Acme.Ctf;

public static class Crackme
{
    public static int Check(int n)
    {
        if (n == 0)
            return 42;     // "fail" branch
        return 1337;       // "win" branch
    }
}
