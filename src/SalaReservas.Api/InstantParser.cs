using System.Globalization;
using System.Text.RegularExpressions;

namespace SalaReservas.Api;

public static partial class InstantParser
{
    private static readonly string[] Formats =
    [
        "yyyy-MM-dd'T'HH:mm:sszzz",
        "yyyy-MM-dd'T'HH:mm:ss.FFFFFFFzzz"
    ];

    public static bool TryParse(string? input, out DateTimeOffset instant)
    {
        instant = default;

        if (input is null || input.Length > 33 || !InstantPattern().IsMatch(input))
        {
            return false;
        }

        var normalized = input.EndsWith('Z') ? input[..^1] + "+00:00" : input;
        if (!DateTimeOffset.TryParseExact(
            normalized, Formats, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
        {
            return false;
        }

        instant = parsed.ToUniversalTime();
        return true;
    }

    [GeneratedRegex(@"\A[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}(?:\.[0-9]{1,7})?(?:Z|[+-][0-9]{2}:[0-9]{2})\z")]
    private static partial Regex InstantPattern();
}
