using System.Runtime.InteropServices;

namespace CrackMe.Internal;

internal static class _0xPI
{
    [DllImport("kernel32.dll", EntryPoint = "GetTickCount", ExactSpelling = true)]
    public static extern uint GetTickCount();

    [DllImport("kernel32.dll", EntryPoint = "IsDebuggerPresent", ExactSpelling = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    public static extern bool IsDebuggerPresent();
}
