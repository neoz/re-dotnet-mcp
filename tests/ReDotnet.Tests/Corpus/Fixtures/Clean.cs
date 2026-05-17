// Synthetic fixture for M0/M1 tests. Compiled at test init via Roslyn.

using System;

namespace Acme.Clean;

public class Greeter
{
    private readonly string _name;

    public Greeter(string name) { _name = name; }

    public string Greet() => $"Hello, {_name}!";

    public static int Add(int a, int b) => a + b;
}

internal struct Point
{
    public int X;
    public int Y;
    public Point(int x, int y) { X = x; Y = y; }
}

public enum Color { Red, Green, Blue }

public interface IShape
{
    double Area();
}

public sealed class Circle : IShape
{
    public double Radius { get; }
    public Circle(double r) { Radius = r; }
    public double Area() => Math.PI * Radius * Radius;
}
