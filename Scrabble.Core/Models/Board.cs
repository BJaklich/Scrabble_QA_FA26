namespace Scrabble.Core.Models;

public enum PremiumType { Normal, DoubleLetter, TripleLetter, DoubleWord, TripleWord, Center }

public sealed class Square
{
    public required int Row { get; init; }
    public required int Column { get; init; }
    public required PremiumType Premium { get; set; }
    public Tile? Tile { get; set; }
}

public sealed class Board
{
    public const int Size = 15;
    public const int Center = 7;

    private static readonly PremiumType[,] Premiums = BuildPremiums();
    public Square[,] Squares { get; } = new Square[Size, Size];

    public Board()
    {
        for (var row = 0; row < Size; row++)
        for (var column = 0; column < Size; column++)
        {
            Squares[row, column] = new Square
            {
                Row = row,
                Column = column,
                Premium = Premiums[row, column]
            };
        }
    }

    public bool IsEmpty(int row, int column) => IsInBounds(row, column) && Squares[row, column].Tile is null;
    public bool IsInBounds(int row, int column) => row >= 0 && row < Size && column >= 0 && column < Size;

    private static PremiumType[,] BuildPremiums()
    {
        var result = new PremiumType[Size, Size];
        for (var row = 0; row < Size; row++)
        for (var column = 0; column < Size; column++)
        {
            result[row, column] = PremiumType.Normal;
        }

        void Set(params (int Row, int Column)[] cells)
        {
            foreach (var (row, column) in cells)
                result[row, column] = PremiumType.TripleWord;
        }

        Set((0,0),(0,7),(0,14),(7,0),(7,14),(14,0),(14,7),(14,14));

        var doubleWordCells = new[] { (1,1),(2,2),(3,3),(4,4),(10,10),(11,11),(12,12),(13,13),
            (13,1),(12,2),(11,3),(10,4),(4,10),(3,11),(2,12),(1,13) };
        foreach (var (row, column) in doubleWordCells)
            result[row, column] = PremiumType.DoubleWord;

        var tripleLetterCells = new[] { (1,5),(1,9),(5,1),(5,5),(5,9),(5,13),(9,1),(9,5),(9,9),(9,13),(13,5),(13,9) };
        foreach (var (row, column) in tripleLetterCells)
            result[row, column] = PremiumType.TripleLetter;

        var doubleLetterCells = new[] { (0,3),(0,11),(2,6),(2,8),(3,0),(3,7),(3,14),(6,2),(6,6),(6,8),(6,12),(7,3),(7,11),(8,2),(8,6),(8,8),(8,12),(11,0),(11,7),(11,14),(12,6),(12,8),(14,3),(14,11) };
        foreach (var (row, column) in doubleLetterCells)
            result[row, column] = PremiumType.DoubleLetter;

        result[Center, Center] = PremiumType.Center;
        return result;
    }
}
