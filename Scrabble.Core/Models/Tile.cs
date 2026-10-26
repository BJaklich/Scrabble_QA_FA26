namespace Scrabble.Core.Models;

public sealed class Tile
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public required string Letter { get; init; }
    public required int PointValue { get; init; }
    public bool IsBlank { get; init; }
    public string? AssignedLetter { get; init; }

    public string EffectiveLetter => (IsBlank ? AssignedLetter : Letter)?.ToUpperInvariant() ?? "?";

    public Tile Clone() => new()
    {
        Id = Id,
        Letter = Letter,
        PointValue = PointValue,
        IsBlank = IsBlank,
        AssignedLetter = AssignedLetter
    };
}
