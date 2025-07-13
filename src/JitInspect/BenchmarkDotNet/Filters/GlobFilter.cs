using System.Linq;
using System.Text.RegularExpressions;

namespace BenchmarkDotNet.Filters;

/// <summary>
/// filters benchmarks by provided glob patterns
/// </summary>
public class GlobFilter
{
    readonly Regex[] patterns;

    public GlobFilter(string[] patterns)
    {
        this.patterns = ToRegex(patterns);
    }

    internal static Regex[] ToRegex(string[] patterns)
    {
        return patterns.Select(pattern => new Regex(WildcardToRegex(pattern), RegexOptions.IgnoreCase | RegexOptions.CultureInvariant)).ToArray();
    }

    // https://stackoverflow.com/a/6907849/5852046 not perfect but should work for all we need
    static string WildcardToRegex(string pattern)
    {
        return $"^{Regex.Escape(pattern).Replace(@"\*", ".*").Replace(@"\?", ".")}$";
    }
}