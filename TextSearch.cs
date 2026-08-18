using System.Text;

namespace Jester;

/// <summary>
/// The find/replace text arithmetic, with no editor attached.
///
/// The tricky parts of searching — where a backwards scan starts, when a wrap
/// is allowed, how far to advance so a replacement containing the search term
/// cannot match itself — are the same whether the text came from an open tab or
/// a file on disk. Keeping them here means one implementation, covered by tests,
/// rather than one buried in each caller.
/// </summary>
internal static class TextSearch
{
    /// <summary>
    /// Index of the next match, or -1 if there is none.
    /// </summary>
    /// <param name="text">Text to search.</param>
    /// <param name="search">Term to look for.</param>
    /// <param name="from">
    /// Where to search from. Going down this is the first index that may match;
    /// going up it is the highest index a match may start at.
    /// </param>
    /// <param name="searchDown">Search forwards when true, backwards when false.</param>
    /// <param name="matchCase">Ordinal comparison when true, case-insensitive when false.</param>
    /// <param name="wrapAround">Continue from the other end when the first scan finds nothing.</param>
    public static int FindNext(
        string text,
        string search,
        int from,
        bool searchDown,
        bool matchCase,
        bool wrapAround)
    {
        if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(text))
            return -1;

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;

        if (searchDown)
        {
            int start = Math.Clamp(from, 0, text.Length);
            int index = text.IndexOf(search, start, comparison);
            if (index < 0 && wrapAround)
                index = text.IndexOf(search, 0, comparison);
            return index;
        }

        // Backwards. A negative start means the caret sits at the very beginning,
        // so there is nothing behind it to find.
        int back = Math.Min(from, text.Length - 1);
        int found = back >= 0 ? text.LastIndexOf(search, back, comparison) : -1;
        if (found < 0 && wrapAround)
            found = text.LastIndexOf(search, text.Length - 1, comparison);
        return found;
    }

    /// <summary>
    /// Replaces every occurrence of <paramref name="search"/>.
    /// </summary>
    /// <returns>The rewritten text and how many replacements were made.</returns>
    public static (string Text, int Count) ReplaceAll(
        string text,
        string search,
        string replace,
        bool matchCase)
    {
        if (string.IsNullOrEmpty(search) || string.IsNullOrEmpty(text))
            return (text, 0);

        var comparison = matchCase ? StringComparison.Ordinal : StringComparison.OrdinalIgnoreCase;
        var builder = new StringBuilder(text.Length);
        int index = 0, count = 0;

        while (true)
        {
            int found = text.IndexOf(search, index, comparison);
            if (found < 0)
            {
                builder.Append(text, index, text.Length - index);
                break;
            }

            builder.Append(text, index, found - index);
            builder.Append(replace);
            // Skip past what was matched, not what was written: replacing "a"
            // with "aa" would otherwise rescan its own output forever.
            index = found + search.Length;
            count++;
        }

        return count > 0 ? (builder.ToString(), count) : (text, 0);
    }
}
