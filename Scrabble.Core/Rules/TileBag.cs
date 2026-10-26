using Scrabble.Core.Models;

namespace Scrabble.Core.Rules;

public sealed class TileBag
{
    private readonly Random random;
    private readonly List<Tile> tiles;

    public TileBag(Random? random = null)
    {
        this.random = random ?? Random.Shared;
        tiles = BuildTiles();
    }

    public int Remaining => tiles.Count;

    public void Return(IEnumerable<Tile> returnedTiles)
    {
        foreach (var tile in returnedTiles)
        {
            if (!tiles.Any(existing => existing.Id == tile.Id))
                tiles.Add(tile);
        }
    }

    public IReadOnlyList<Tile> Draw(int count)
    {
        if (count < 0 || count > tiles.Count)
            throw new ArgumentOutOfRangeException(nameof(count));

        var drawn = new List<Tile>(count);
        for (var index = 0; index < count; index++)
        {
            var tileIndex = random.Next(tiles.Count);
            drawn.Add(tiles[tileIndex]);
            tiles.RemoveAt(tileIndex);
        }
        return drawn;
    }

    private static List<Tile> BuildTiles()
    {
        var letterCounts = new Dictionary<char, (int Count, int Value)>
        {
            ['A']=(9,1), ['B']=(2,3), ['C']=(2,3), ['D']=(4,2), ['E']=(12,1), ['F']=(2,4), ['G']=(3,2), ['H']=(2,4), ['I']=(9,1),
            ['J']=(1,8), ['K']=(1,5), ['L']=(4,1), ['M']=(2,3), ['N']=(6,1), ['O']=(8,1), ['P']=(2,3), ['Q']=(1,10), ['R']=(6,1), ['S']=(4,1),
            ['T']=(6,1), ['U']=(4,1), ['V']=(2,4), ['W']=(2,4), ['X']=(1,8), ['Y']=(2,4), ['Z']=(1,10)
        };

        var result = new List<Tile>(100);
        foreach (var (letter, (count, value)) in letterCounts)
        for (var index = 0; index < count; index++)
            result.Add(new Tile { Letter = letter.ToString(), PointValue = value });

        for (var index = 0; index < 2; index++)
            result.Add(new Tile { Letter = "?", PointValue = 0, IsBlank = true });

        return result;
    }
}
