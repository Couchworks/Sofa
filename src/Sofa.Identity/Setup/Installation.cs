namespace Sofa.Identity.Setup;

public sealed class Installation
{
    public const int TheOnlyOne = 1;

    public int Id { get; init; } = TheOnlyOne;

    public string? SetupCodeHash { get; set; }

    public DateTimeOffset? SetUpAt { get; set; }

    public bool IsSetUp => SetUpAt is not null;
}
