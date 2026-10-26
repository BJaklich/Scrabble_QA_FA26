using Scrabble.Core.Dictionary;
using Scrabble.Core.Models;

namespace Scrabble.Core.Rules;

public sealed record ValidationResult(bool IsValid, string? Error = null);

public sealed class MoveValidator
{
    private readonly IWordDictionary dictionary;

    public MoveValidator(IWordDictionary dictionary) => this.dictionary = dictionary;

    public ValidationResult Validate(Board board, Move move)
    {
        var placements = move.Placements;
        if (placements.Count == 0)
            return new ValidationResult(false, "A play must contain at least one tile.");

        if (placements.Select(p => (p.Row, p.Column)).Distinct().Count() != placements.Count)
            return new ValidationResult(false, "Duplicate tile placement.");

        foreach (var placement in placements)
        {
            if (!board.IsInBounds(placement.Row, placement.Column))
                return new ValidationResult(false, "A tile is outside the board.");
            if (placement.Tile is null)
                return new ValidationResult(false, "A placement is missing its tile.");
            if (!board.IsEmpty(placement.Row, placement.Column))
                return new ValidationResult(false, "A tile may not be placed on an occupied square.");
            if (placement.Tile.IsBlank)
            {
                var assigned = placement.Tile.AssignedLetter?.Trim();
                if (assigned is null || assigned.Length != 1 || !char.IsAsciiLetter(assigned[0]))
                    return new ValidationResult(false, "A blank tile must assign one A-Z letter.");
            }
        }

        var distinctRows = placements.Select(p => p.Row).Distinct().Count();
        var distinctColumns = placements.Select(p => p.Column).Distinct().Count();
        if (distinctRows > 1 && distinctColumns > 1)
            return new ValidationResult(false, "Tiles must be placed in a single row or column.");

        if (!IsContiguous(board, placements, out var isHorizontal))
            return new ValidationResult(false, "Tiles must form a contiguous word.");

        var anyTilesOnBoard = board.Squares.Cast<Square>().Any(square => square.Tile is not null);
        if (!anyTilesOnBoard)
        {
            if (placements.Count < 2)
                return new ValidationResult(false, "The first word must contain at least two tiles.");
            if (!placements.Any(p => p.Row == Board.Center && p.Column == Board.Center))
                return new ValidationResult(false, "The first word must cover the center square.");
        }
        else if (!placements.Concat(occupiedExistingNeighbors(board, placements))
                     .DistinctBy(p => (p.Row, p.Column))
                     .Any(p => HasAdjacentOccupiedSquare(board, placements, p.Row, p.Column)))
        {
            return new ValidationResult(false, "The play must connect to an existing tile.");
        }

        var words = GetFormedWords(board, placements, isHorizontal);
        foreach (var word in words)
        {
            if (word.Length < 2) continue;
            if (!dictionary.Contains(word))
                return new ValidationResult(false, $"Invalid word: {word}");
        }

        return new ValidationResult(true);
    }

    private static bool IsContiguous(Board board, IReadOnlyList<Placement> placements, out bool isHorizontal)
    {
        isHorizontal = placements.Select(p => p.Row).Distinct().Count() == 1;
        if (placements.Count == 1) return true;

        var placementPositions = placements.Select(p => (p.Row, p.Column)).ToHashSet();
        var ordered = isHorizontal
            ? placements.OrderBy(p => p.Column).Select(p => (p.Row, p.Column)).ToList()
            : placements.OrderBy(p => p.Row).Select(p => (p.Row, p.Column)).ToList();

        var min = isHorizontal ? ordered.Min(p => p.Column) : ordered.Min(p => p.Row);
        var max = isHorizontal ? ordered.Max(p => p.Column) : ordered.Max(p => p.Row);
        for (var coordinate = min; coordinate <= max; coordinate++)
        {
            var row = isHorizontal ? ordered[0].Row : coordinate;
            var column = isHorizontal ? coordinate : ordered[0].Column;
            if (!placementPositions.Contains((row, column)) && board.Squares[row, column].Tile is null)
                return false;
        }
        return true;
    }

    private static IEnumerable<Placement> occupiedExistingNeighbors(Board board, IReadOnlyList<Placement> placements)
    {
        foreach (var placement in placements)
        {
            foreach (var (row, column) in Neighbors(placement.Row, placement.Column))
                if (board.IsInBounds(row, column) && board.Squares[row, column].Tile is not null)
                    yield return new Placement(row, column, board.Squares[row, column].Tile!);
        }
    }

    private static bool HasAdjacentOccupiedSquare(Board board, IReadOnlyList<Placement> placements, int row, int column)
    {
        foreach (var (neighborRow, neighborColumn) in Neighbors(row, column))
            if (board.IsInBounds(neighborRow, neighborColumn) && board.Squares[neighborRow, neighborColumn].Tile is not null)
                return true;
        return false;
    }

    private static IEnumerable<(int Row, int Column)> Neighbors(int row, int column)
    {
        yield return (row - 1, column);
        yield return (row + 1, column);
        yield return (row, column - 1);
        yield return (row, column + 1);
    }

    private static IReadOnlyList<string> GetFormedWords(Board board, IReadOnlyList<Placement> placements, bool isHorizontal)
    {
        var overlay = placements.ToDictionary(p => (p.Row, p.Column), p => p.Tile);
        Tile? TileAt(int row, int column) => overlay.TryGetValue((row, column), out var tile)
            ? tile
            : board.Squares[row, column].Tile;

        var words = new List<string>();
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
            while (row - rowStep >= 0 && row - rowStep < Board.Size && column - columnStep >= 0 && column - columnStep < Board.Size && TileAt(row - rowStep, column - columnStep) is not null)
            {
                row -= rowStep;
                column -= columnStep;
            }

            var letters = new List<string>();
            while (row >= 0 && row < Board.Size && column >= 0 && column < Board.Size)
            {
                var tile = TileAt(row, column);
                if (tile is null) break;
                letters.Add(tile.EffectiveLetter);
                row += rowStep;
                column += columnStep;
            }
            words.Add(string.Concat(letters));
        }

        return words.Distinct(StringComparer.Ordinal).ToList();
    }
}
