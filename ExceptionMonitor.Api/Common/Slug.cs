using System.Text.RegularExpressions;

namespace ExceptionMonitor.Api.Common;

public static partial class Slug
{
    public static string FromName(string value)
    {
        var slug = SlugRegex().Replace(value.Trim().ToLowerInvariant(), "-").Trim('-');
        return string.IsNullOrWhiteSpace(slug) ? Guid.NewGuid().ToString("N")[..12] : slug;
    }

    [GeneratedRegex("[^a-z0-9]+")]
    private static partial Regex SlugRegex();
}
