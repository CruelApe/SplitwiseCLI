using System.Text;

namespace SplitwiseCLI.Cli;

// Splits a typed interactive-mode line into argv tokens, honoring double-quoted
// segments (e.g. import "C:/My Expenses/*.xlsx") the same way a shell would.
public static class CommandLineTokenizer
{
    public static string[] Tokenize(string line)
    {
        var tokens = new List<string>();
        var current = new StringBuilder();
        var inQuotes = false;

        foreach (var c in line)
        {
            if (c == '"')
            {
                inQuotes = !inQuotes;
                continue;
            }

            if (!inQuotes && char.IsWhiteSpace(c))
            {
                if (current.Length > 0)
                {
                    tokens.Add(current.ToString());
                    current.Clear();
                }
                continue;
            }

            current.Append(c);
        }

        if (current.Length > 0)
        {
            tokens.Add(current.ToString());
        }

        return [.. tokens];
    }
}
