namespace Sofa.Code.Pushes;

public sealed class UndonePush
{
    public required Guid Id { get; init; }

    public required string Repository { get; init; }

    public required string Ref { get; init; }

    public required string PutBackTo { get; init; }

    public required DateTimeOffset UndoneAt { get; init; }
}
