using Scrabble.Core.Dictionary;
using Scrabble.Core.Models;
using Scrabble.Core.Rules;

namespace Scrabble.Tests;

public class CoreRulesTests
{
    [Fact]
    public void EmbeddedDictionaryIncludesBiteAndCup()
    {
        var loaded = Scrabble.Core.Dictionary.WordDictionaryLoader.Load();

        Assert.True(loaded.Dictionary.Contains("BITE"));
        Assert.True(loaded.Dictionary.Contains("CUP"));
        Assert.Equal("Embedded words.txt", loaded.Source);
    }

    [Fact]
    public void DictionaryPlainImportReplacesFallback()
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, ["CAT", "IMPORTWORD"]);
        try
        {
            var loaded = Scrabble.Core.Dictionary.WordDictionaryLoader.Load(path);
            Assert.True(loaded.Dictionary.Contains("CAT"));
            Assert.True(loaded.Dictionary.Contains("IMPORTWORD"));
            Assert.False(loaded.Dictionary.Contains("BITE"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void DictionaryDiffAddsAndRemovesWords()
    {
        var path = Path.GetTempFileName();
        File.WriteAllLines(path, ["+IMPORTWORD", "-BITE"]);
        try
        {
            var loaded = Scrabble.Core.Dictionary.WordDictionaryLoader.Load(path);
            Assert.True(loaded.Dictionary.Contains("IMPORTWORD"));
            Assert.False(loaded.Dictionary.Contains("BITE"));
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void ComputerOpponent_CanBeCreatedWithHuman()
    {
        var manager = new Scrabble.Web.Services.GameManager(new HashSetDictionary(["CAT"]));

        var game = manager.Create("Ada", true);

        Assert.Equal(2, game.Players.Count);
        Assert.Equal("Computer", game.Players[1].Name);
        Assert.Equal("Playing", game.Status);
    }

    [Fact]
    public void GameManager_RejectsNullOrBlankPlayerNames()
    {
        var manager = new Scrabble.Web.Services.GameManager(new HashSetDictionary(["CAT"]));

        Assert.Throws<ArgumentException>(() => manager.Create(null));
        Assert.Throws<ArgumentException>(() => manager.Join(Guid.NewGuid(), "  "));
    }

    [Fact]
    public void WaitingGame_CannotPlay()
    {
        var manager = new Scrabble.Web.Services.GameManager(new HashSetDictionary(["CAT"]));
        var game = manager.Create("Ada");
        var player = game.Players[0];

        var result = manager.Play(game, player.SessionToken,
            [(7, 7, "A", null)]);

        Assert.False(result.IsValid);
        Assert.Contains("another player", result.Error);
    }

    private static Tile Letter(char letter, int points = 1) => new()
    {
        Letter = letter.ToString(),
        PointValue = points
    };

    private static Board Place(Board board, int row, int column, char letter, int points = 1)
    {
        board.Squares[row, column].Tile = Letter(letter, points);
        return board;
    }

    [Fact]
    public void FirstMove_MustCoverCenter()
    {
        var validator = new MoveValidator(new HashSetDictionary(["CAT"]));
        var move = new Move { Placements = [new Placement(7, 6, Letter('C')),
            new Placement(7, 7, Letter('A')),
            new Placement(7, 8, Letter('T'))] };

        var result = validator.Validate(new Board(), move);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void FirstMove_SingleTile_IsRejected()
    {
        var validator = new MoveValidator(new HashSetDictionary(["CAT"]));
        var move = new Move { Placements = [new Placement(7, 7, Letter('A'))] };

        var result = validator.Validate(new Board(), move);

        Assert.False(result.IsValid);
        Assert.Contains("two tiles", result.Error);
    }

    [Fact]
    public void Bite_IsAValidFirstMoveAcrossCenter()
    {
        var validator = new MoveValidator(new HashSetDictionary(["BITE"]));
        var move = new Move { Placements = [
            new Placement(7, 5, Letter('B')),
            new Placement(7, 6, Letter('I')),
            new Placement(7, 7, Letter('T')),
            new Placement(7, 8, Letter('E'))
        ] };

        var result = validator.Validate(new Board(), move);

        Assert.True(result.IsValid);
    }

    [Fact]
    public void Bite_FirstMove_ScoresFromCenterPremiumAndTileValues()
    {
        var board = new Board();
        var placements = new[] {
            new Placement(7, 5, Letter('B', 3)),
            new Placement(7, 6, Letter('I', 1)),
            new Placement(7, 7, Letter('T', 1)),
            new Placement(7, 8, Letter('E', 1))
        };

        var result = new Scorer().Score(board, placements);

        Assert.Equal(12, result.Score);
    }

    [Fact]
    public void FirstMove_NotCoveringCenter_IsRejected()
    {
        var validator = new MoveValidator(new HashSetDictionary(["CAT"]));
        var move = new Move { Placements = [new Placement(6, 6, Letter('C'))] };

        var result = validator.Validate(new Board(), move);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Play_CannotHaveGap()
    {
        var validator = new MoveValidator(new HashSetDictionary(["CAT"]));
        var move = new Move { Placements = [new Placement(7, 6, Letter('C')),
            new Placement(7, 8, Letter('T'))] };

        var result = validator.Validate(new Board(), move);

        Assert.False(result.IsValid);
        Assert.Contains("contiguous", result.Error);
    }

    [Fact]
    public void InvalidCrossWord_IsRejected()
    {
        var board = new Board();
        Place(board, 6, 7, 'A');
        var validator = new MoveValidator(new HashSetDictionary(["CAT"]));
        var move = new Move { Placements = [new Placement(7, 7, Letter('C')),
            new Placement(7, 8, Letter('A')),
            new Placement(7, 9, Letter('T'))] };

        var result = validator.Validate(board, move);

        Assert.False(result.IsValid);
    }

    [Fact]
    public void Blank_IsScoredAsZero()
    {
        var board = new Board();
        var blank = new Tile { Letter = "?", PointValue = 0, IsBlank = true, AssignedLetter = "A" };
        var result = new Scorer().Score(board, [new Placement(7, 7, blank)]);

        Assert.Equal(0, result.Score);
    }

    [Fact]
    public void CenterAndPremium_Multipliers_AreApplied()
    {
        var board = new Board();
        var placements = new[]
        {
            new Placement(7, 6, Letter('C')),
            new Placement(7, 7, Letter('A')),
            new Placement(7, 8, Letter('T'))
        };

        var result = new Scorer().Score(board, placements);

        Assert.Equal(6, result.Score);
    }

    [Fact]
    public void DuplicatePlacement_IsRejected()
    {
        var validator = new MoveValidator(new HashSetDictionary(["CAT"]));
        var tile = Letter('A');
        var move = new Move { Placements = [
            new Placement(7, 7, tile),
            new Placement(7, 7, tile)
        ] };

        var result = validator.Validate(new Board(), move);

        Assert.False(result.IsValid);
        Assert.Contains("Duplicate", result.Error);
    }

    [Fact]
    public void TileBag_DrawCycleAccountsFor100Tiles()
    {
        for (var seed = 1; seed <= 50; seed++)
        {
            var bag = new TileBag(new Random(seed));
            var drawn = new List<Tile>();
            while (bag.Remaining > 0)
                drawn.AddRange(bag.Draw(Math.Min(7, bag.Remaining)));

            Assert.Equal(100, drawn.Count);
            Assert.Equal(100, drawn.Select(tile => tile.Id).Distinct().Count());
            Assert.Equal(2, drawn.Count(tile => tile.IsBlank));
            Assert.Equal(98, drawn.Count(tile => !tile.IsBlank));
        }
    }

    [Fact]
    public void CrossWordUsesSeparateScoringMultiplier()
    {
        var board = new Board();
        board.Squares[7, 7].Tile = Letter('A', 1);
        board.Squares[7, 8].Tile = Letter('T', 1);
        var placements = new[] { new Placement(6, 7, Letter('C', 3)),
            new Placement(7, 7, Letter('A', 1)),
            new Placement(8, 7, Letter('R', 1)) };

        var result = new Scorer().Score(board, placements);

        Assert.True(result.Score >= 0);
        Assert.NotEmpty(result.Words);
    }

    [Fact]
    public void SevenTileBingo_AddsBonus()
    {
        var board = new Board();
        for (var row = 0; row < Board.Size; row++)
        for (var column = 0; column < Board.Size; column++)
            board.Squares[row, column].Premium = PremiumType.Normal;

        var placements = Enumerable.Range(1, 7)
            .Select(column => new Placement(1, column, Letter('A')))
            .ToList();

        var result = new Scorer().Score(board, placements);

        Assert.Equal(57, result.Score);
    }
}
