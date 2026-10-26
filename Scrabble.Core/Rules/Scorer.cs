using Scrabble.Core.Models;

namespace Scrabble.Core.Rules;

public sealed record ScoreResult(int Score, IReadOnlyList<string> Words);

public sealed class Scorer
{
    public ScoreResult Score(Board board, IReadOnlyList<Placement> placements)
    {
        var overlay = placements.ToDictionary(p => (p.Row, p.Column), p => p.Tile);
        Tile? TileAt(int row, int column) => overlay.TryGetValue((row, column), out var tile)
            ? tile
            : board.Squares[row, column].Tile;

        var words = GetWords(board, placements, TileAt);
        var total = 0;

        foreach (var word in words)
        {
            var wordScore = 0;
            var wordMultiplier = 1;
            foreach (var (row, column, tile) in word)
            {
                var letterMultiplier = 1;
                if (overlay.TryGetValue((row, column), out _))
                {
                    var premium = board.Squares[row, column].Premium;
                    if (premium == PremiumType.DoubleLetter) letterMultiplier = 2;
                    else if (premium == PremiumType.TripleLetter) letterMultiplier = 3;
                    else if (premium == PremiumType.DoubleWord || premium == PremiumType.Center) wordMultiplier *= 2;
                    else if (premium == PremiumType.TripleWord) wordMultiplier *= 3;
                }

                wordScore += tile.IsBlank ? 0 : tile.PointValue * letterMultiplier;
            }
            total += wordScore * wordMultiplier;
        }

        if (placements.Count == Rack.Capacity)
            total += 50;

        return new ScoreResult(total, words.Select(WordText).Distinct(StringComparer.Ordinal).ToList());
    }

    private static IReadOnlyList<IReadOnlyList<(int Row, int Column, Tile Tile)>> GetWords(
        Board board,
        IReadOnlyList<Placement> placements,
        Func<int, int, Tile?> tileAt)
    {
        var result = new List<IReadOnlyList<(int Row, int Column, Tile Tile)>>();
        var isHorizontal = placements.Select(p => p.Row).Distinct().Count() == 1;

        if (placements.Count == 1)
        {
            AddWord(placements[0].Row, placements[0].Column, 0, 1);
            AddWord(placements[0].Row, placements[0].Column, 1, 0);
        }
        else
        {
            AddWord(placements[0].Row, placements[0].Column, isHorizontal ? 0 : 1, isHorizontal ? 1 : 0);
            foreach (var placement in placements)
                AddWord(placement.Row, placement.Column, isHorizontal ? 1 : 0, isHorizontal ? 0 : 1);
        }

        void AddWord(int row, int column, int rowStep, int columnStep)
        {
            while (row - rowStep >= 0 && row - rowStep < Board.Size && column - columnStep >= 0 && column - columnStep < Board.Size && tileAt(row - rowStep, column - columnStep) is not null)
            {
                row -= rowStep;
                column -= columnStep;
            }

            var word = new List<(int Row, int Column, Tile Tile)>();
            while (row >= 0 && row < Board.Size && column >= 0 && column < Board.Size)
            {
                var tile = tileAt(row, column);
                if (tile is null) break;
                word.Add((row, column, tile));
                row += rowStep;
                column += columnStep;
            }
            if (word.Count >= 2)
                result.Add(word);
        }

        return result;
    }

    private static string WordText(IReadOnlyList<(int Row, int Column, Tile Tile)> word)
        => string.Concat(word.Select(item => item.Tile.EffectiveLetter));
}
