using System.Reflection;

namespace Scrabble.Core.Dictionary;

public sealed record DictionaryLoadResult(IWordDictionary Dictionary, int WordCount, int AddedCount, int RemovedCount, string Source);

public static class WordDictionaryLoader
{
    public static DictionaryLoadResult Load(string? filePath = null, Assembly? assembly = null)
    {
        assembly ??= typeof(WordDictionaryLoader).Assembly;

        var fallback = ReadEmbeddedFallback(assembly);
        if (string.IsNullOrWhiteSpace(filePath))
            return new DictionaryLoadResult(new HashSetDictionary(fallback), fallback.Count, 0, 0, "Embedded words.txt");

        var path = Path.GetFullPath(filePath);
        if (!File.Exists(path))
            throw new FileNotFoundException($"Dictionary file not found: {path}", path);

        var lines = File.ReadLines(path)
            .Select(line => line.Trim())
            .Where(line => line.Length > 0 && !line.StartsWith('#'))
            .ToList();
        var isDiff = lines.Any(line => line.StartsWith('+') || line.StartsWith('-'));
        var normalizedFallback = fallback
            .Where(word => !string.IsNullOrWhiteSpace(word))
            .Select(word => word.Trim().ToUpperInvariant())
            .Where(word => word.Length <= 15 && word.All(char.IsAsciiLetter));
        var words = new HashSet<string>(isDiff ? normalizedFallback : [], StringComparer.Ordinal);
        var added = 0;
        var removed = 0;

        foreach (var rawLine in lines)
        {
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;

            var operation = '+';
            if (line[0] is '+' or '-')
            {
                operation = line[0];
                line = line[1..].Trim();
            }

            if (line.Length == 0 || line.Length > 15 || line.Any(character => !char.IsAsciiLetter(character))) continue;
            var word = line.ToUpperInvariant();

            if (operation == '-')
            {
                if (words.Remove(word)) removed++;
            }
            else
            {
                if (words.Add(word)) added++;
            }
        }

        return new DictionaryLoadResult(new HashSetDictionary(words), words.Count, added, removed, path);
    }

    private static List<string> ReadEmbeddedFallback(Assembly assembly)
    {
        var resourceName = assembly.GetManifestResourceNames()
            .SingleOrDefault(name => name.EndsWith("Dictionary.words.txt", StringComparison.OrdinalIgnoreCase));
        if (resourceName is null)
            throw new InvalidOperationException("Embedded dictionary resource was not found.");

        using var stream = assembly.GetManifestResourceStream(resourceName)
            ?? throw new InvalidOperationException($"Could not open dictionary resource: {resourceName}");
        using var reader = new StreamReader(stream);

        var result = new List<string>();
        string? line;
        while ((line = reader.ReadLine()) is not null)
        {
            line = line.Trim();
            if (line.Length > 0 && !line.StartsWith('#')) result.Add(line);
        }
        return result;
    }
}
