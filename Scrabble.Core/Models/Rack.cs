namespace Scrabble.Core.Models;

public sealed class Rack
{
    public const int Capacity = 7;
    private readonly List<Tile> tiles = [];

    public IReadOnlyList<Tile> Tiles => tiles;

    public void Add(Tile tile)
    {
        if (tiles.Count >= Capacity)
            throw new InvalidOperationException("The rack is full.");
        tiles.Add(tile);
    }

    public bool Remove(Tile tile) => tiles.RemoveAll(candidate => candidate.Id == tile.Id) == 1;

    public Tile? FindMatch(string letter, bool isBlank, string? assignedLetter = null, IReadOnlySet<Guid>? excludedIds = null)
    {
        if (isBlank)
        {
            if (string.IsNullOrWhiteSpace(assignedLetter) || assignedLetter.Trim().Length != 1)
                return null;
            return tiles.FirstOrDefault(tile => tile.IsBlank &&
                (excludedIds is null || !excludedIds.Contains(tile.Id)));
        }

        return tiles.FirstOrDefault(tile => !tile.IsBlank &&
            (excludedIds is null || !excludedIds.Contains(tile.Id)) &&
            tile.Letter.Equals(letter?.Trim(), StringComparison.OrdinalIgnoreCase));
    }
}
