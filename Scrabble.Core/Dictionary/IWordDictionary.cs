namespace Scrabble.Core.Dictionary;

public interface IWordDictionary
{
    bool Contains(string word);
    IEnumerable<string> Words { get; }
}

public sealed class HashSetDictionary : IWordDictionary
{
    private readonly HashSet<string> words;

    public HashSetDictionary(IEnumerable<string> words)
    {
        this.words = new HashSet<string>(
            words.Where(word => !string.IsNullOrWhiteSpace(word))
                .Select(word => word.Trim().ToUpperInvariant()),
            StringComparer.Ordinal);
    }

    public IEnumerable<string> Words => words;
    public bool Contains(string word) => words.Contains(word.Trim().ToUpperInvariant());
}
