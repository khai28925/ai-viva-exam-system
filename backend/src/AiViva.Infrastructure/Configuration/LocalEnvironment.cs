namespace AiViva.Infrastructure.Configuration;

/// <summary>Loads the repository's local .env without replacing process environment variables.</summary>
public static class LocalEnvironment
{
    public static void Load()
    {
        var root = FindRepositoryRoot(Directory.GetCurrentDirectory()) ?? FindRepositoryRoot(AppContext.BaseDirectory);
        if (root is null) return;
        var path = Path.Combine(root, ".env");
        if (!File.Exists(path)) return;

        var lineNumber = 0;
        foreach (var rawLine in File.ReadLines(path))
        {
            lineNumber++;
            var line = rawLine.Trim();
            if (line.Length == 0 || line.StartsWith('#')) continue;
            if (line.StartsWith("export ", StringComparison.Ordinal)) line = line[7..].TrimStart();
            var separator = line.IndexOf('=');
            if (separator < 1) throw InvalidLine(lineNumber);
            var key = line[..separator].Trim();
            if (key.Length == 0 || !(char.IsAsciiLetter(key[0]) || key[0] == '_') ||
                key.Any(c => !(char.IsAsciiLetterOrDigit(c) || c == '_'))) throw InvalidLine(lineNumber);

            var value = line[(separator + 1)..].Trim();
            if (value.StartsWith('"') || value.StartsWith('\''))
            {
                var closingQuote = value.LastIndexOf(value[0]);
                if (closingQuote == 0) throw InvalidLine(lineNumber);
                var trailing = value[(closingQuote + 1)..].Trim();
                if (trailing.Length > 0 && !trailing.StartsWith('#')) throw InvalidLine(lineNumber);
                value = value[1..closingQuote];
            }
            else
            {
                var comment = value.IndexOf(" #", StringComparison.Ordinal);
                if (comment >= 0) value = value[..comment].TrimEnd();
            }

            if (Environment.GetEnvironmentVariable(key) is null)
                Environment.SetEnvironmentVariable(key, value);
        }
    }

    private static string? FindRepositoryRoot(string start)
    {
        for (var directory = new DirectoryInfo(start); directory is not null; directory = directory.Parent)
        {
            if (File.Exists(Path.Combine(directory.FullName, "backend", "AiVivaExamSystem.sln")) &&
                File.Exists(Path.Combine(directory.FullName, "frontend", "package.json"))) return directory.FullName;
        }
        return null;
    }

    private static FormatException InvalidLine(int lineNumber) =>
        new($"Invalid .env entry at line {lineNumber}. Use KEY=value; quote values containing spaces or #.");
}
