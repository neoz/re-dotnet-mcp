using System;
using System.Text;
using CrackMe.Internal;
using CrackMe.Stages;

namespace CrackMe;

public static class Program
{
    public static int Main(string[] args)
    {
        Console.OutputEncoding = Encoding.UTF8;
        Console.WriteLine("=== CrackMe v1.0 (re-dotnet sample) ===");
        Console.WriteLine("Find the 7-stage flag.");
        Console.Write("Enter flag: ");
        var input = Console.ReadLine() ?? string.Empty;

        if (_0xCFG.Drive(input))
        {
            Console.WriteLine("Correct! Flag accepted.");
            return 0;
        }

        Console.WriteLine("Wrong flag.");
        return 1;
    }
}
