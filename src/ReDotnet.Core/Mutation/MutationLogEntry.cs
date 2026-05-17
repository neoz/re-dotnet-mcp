namespace ReDotnet.Core.Mutation;

public enum MutationKind
{
    RenameType,
    RenameMethod,
    RenameField,
    RenameParameter,
    RenameLocal,
    PatchIl,
    NopIlRange,
    ReplaceMethodBody,
    PatchBytes,
    SetComment,
    SetMethodIlComment,
    SetBookmark,
    DeleteBookmark,
    SetColor,
}

public sealed record MutationLogEntry(
    MutationKind Kind,
    string AssemblyId,
    string? TargetToken,
    object? Old,
    object? New,
    DateTimeOffset At);
