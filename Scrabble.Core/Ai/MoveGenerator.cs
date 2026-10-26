using Scrabble.Core.Dictionary;
using Scrabble.Core.Models;
using Scrabble.Core.Rules;

namespace Scrabble.Core.Ai;

public sealed record GeneratedMove(IReadOnlyList<Placement> Placements, int Score);

public sealed class MoveGenerator
{
    private readonly IWordDictionary dictionary;

    public MoveGenerator(IWordDictionary dictionary) => this.dictionary = dictionary;

    public GeneratedMove? FindBestMove(Board board, Rack rack)
    {
        var candidates = new List<GeneratedMove>();
        var validator = new MoveValidator(dictionary);
        var scorer = new Scorer();

        foreach (var word in dictionary.Words.Where(word => word.Length <= Rack.Capacity)
                     .OrderByDescending(word => word.Length))
        for (var row = 0; row < Board.Size; row++)
        for (var column = 0; column < Board.Size; column++)
        for (var horizontal = 0; horizontal < 2; horizontal++)
        {
            var rackTiles = rack.Tiles.ToList();
            var placements = new List<Placement>();
            var fits = true;
            var hasNewTile = false;

            for (var offset = 0; offset < word.Length && fits; offset++)
            {
                var tileRow = horizontal == 1 ? row + offset : row;
                var tileColumn = horizontal == 1 ? column : column + offset;
                if (!board.IsInBounds(tileRow, tileColumn)) { fits = false; break; }

                var boardTile = board.Squares[tileRow, tileColumn].Tile;
                if (boardTile is not null)
                {
                    if (!boardTile.EffectiveLetter.Equals(word[offset].ToString(), StringComparison.OrdinalIgnoreCase))
                        fits = false;
                    continue;
                }

                var letter = word[offset].ToString();
                var rackIndex = rackTiles.FindIndex(tile => !tile.IsBlank && tile.Letter.Equals(letter, StringComparison.OrdinalIgnoreCase));
                Tile playedTile;
                if (rackIndex >= 0)
                {
                    playedTile = rackTiles[rackIndex].Clone();
                    rackTiles.RemoveAt(rackIndex);
                }
                else
                {
                    rackIndex = rackTiles.FindIndex(tile => tile.IsBlank);
                    if (rackIndex < 0) { fits = false; break; }
                    var blank = rackTiles[rackIndex];
                    playedTile = new Tile { Letter = "?", PointValue = 0, IsBlank = true, AssignedLetter = letter, Id = blank.Id };
                    rackTiles.RemoveAt(rackIndex);
                }

                placements.Add(new Placement(tileRow, tileColumn, playedTile));
                hasNewTile = true;
            }

            if (!fits || !hasNewTile) continue;
            var move = new Move { Placements = placements };
            if (!validator.Validate(board, move).IsValid) continue;
            var score = scorer.Score(board, placements).Score;
            candidates.Add(new GeneratedMove(placements, score));
        }

        return candidates
            .OrderByDescending(candidate => candidate.Score)
            .ThenByDescending(candidate => candidate.Placements.Count)
            .FirstOrDefault();
    }
}
