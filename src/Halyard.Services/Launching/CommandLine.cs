using System.Text;

namespace Halyard.Services.Launching;

/// <summary>Splits a user-typed command line into arguments, honouring double quotes.</summary>
internal static class CommandLine
{
    public static IReadOnlyList<string> Split(string? commandLine)
    {
        if (string.IsNullOrWhiteSpace(commandLine))
        {
            return [];
        }

        List<string> arguments = [];
        var current = new StringBuilder();
        var quoted = false;

        foreach (var character in commandLine)
        {
            if (character == '"')
            {
                quoted = !quoted;
            }
            else if (char.IsWhiteSpace(character) && !quoted)
            {
                Flush();
            }
            else
            {
                current.Append(character);
            }
        }

        Flush();
        return arguments;

        void Flush()
        {
            if (current.Length > 0)
            {
                arguments.Add(current.ToString());
                current.Clear();
            }
        }
    }
}
