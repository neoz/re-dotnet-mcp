using System;

namespace CrackMe.Internal;

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class CrackMeChallengeAttribute : Attribute
{
    public string Material { get; }
    public CrackMeChallengeAttribute(string material) { Material = material; }
}

[AttributeUsage(AttributeTargets.Assembly, AllowMultiple = false)]
public sealed class CrackMeAuthorAttribute : Attribute
{
    public string Name { get; }
    public CrackMeAuthorAttribute(string name) { Name = name; }
}
