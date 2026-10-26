namespace Scrabble.Core.Models;

public enum MoveType { Play, Exchange, Pass }

public sealed record Placement(int Row, int Column, Tile Tile);

public sealed class Move
{
    public MoveType Type { get; init; } = MoveType.Play;
    public IReadOnlyList<Placement> Placements { get; init; } = [];
}
