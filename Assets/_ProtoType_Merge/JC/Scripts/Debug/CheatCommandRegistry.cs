using System;
using System.Collections.Generic;

public static class CheatCommandRegistry
{
    public delegate string Handler(string[] args);

    private static readonly Dictionary<string, Handler> handlers =
        new Dictionary<string, Handler>(StringComparer.OrdinalIgnoreCase);

    public static void Register(string name, Handler handler)
    {
        if (string.IsNullOrWhiteSpace(name) || handler == null) return;
        handlers[name.Trim()] = handler;
    }

    public static bool TryExecute(string line, out string result)
    {
        result = string.Empty;
        if (string.IsNullOrWhiteSpace(line)) return false;

        var tokens = line.Trim().Split((char[])null, StringSplitOptions.RemoveEmptyEntries);
        if (tokens.Length == 0) return false;

        for (int take = Math.Min(tokens.Length, 4); take >= 1; take--)
        {
            string key = string.Join(" ", tokens, 0, take);
            if (handlers.TryGetValue(key, out var handler))
            {
                var rest = new string[tokens.Length - take];
                Array.Copy(tokens, take, rest, 0, rest.Length);
                try
                {
                    result = handler(rest) ?? string.Empty;
                }
                catch (Exception ex)
                {
                    result = $"[err] {ex.Message}";
                }
                return true;
            }
        }

        result = $"unknown command: {line}";
        return false;
    }
}
