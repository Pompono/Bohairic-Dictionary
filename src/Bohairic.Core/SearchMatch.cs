using System.Globalization;
using System.Text;

namespace Bohairic.Core;

public sealed record MatchRange(int Start, int Length);
public sealed record HighlightedText(string Text, IReadOnlyList<MatchRange> Ranges);
public sealed record SearchMatch(DictionaryEntry Entry, string MatchLabel, HighlightedText WordText,
    HighlightedText PreviewText, HighlightedText EvidenceText)
{
    public HighlightedText LabelText => new(MatchLabel, []);
    public string Id => Entry.Id;
    public string Subtitle => Entry.Subtitle;
}

public static class SearchText
{
    // Map normalized offsets back to complete original text elements, never split a combining mark.
    public static HighlightedText Highlight(string text, string query, bool ignoreMarks)
    {
        var needle = DictionaryService.Normalize(query, ignoreMarks);
        if (needle.Length == 0) return new(text, []);
        var foldedText = DictionaryService.Normalize(text, ignoreMarks);
        if (!needle.Split(' ', StringSplitOptions.RemoveEmptyEntries).Any(term => foldedText.Contains(term, StringComparison.Ordinal)))
            return new(text, []);
        var normalized = new StringBuilder();
        var starts = new List<int>();
        var ends = new List<int>();
        var elements = StringInfo.GetTextElementEnumerator(text);
        while (elements.MoveNext())
        {
            var element = elements.GetTextElement();
            var value = DictionaryService.Normalize(element, ignoreMarks);
            if (string.IsNullOrWhiteSpace(element)) value = " ";
            foreach (var c in value)
            {
                normalized.Append(c);
                starts.Add(elements.ElementIndex);
                ends.Add(elements.ElementIndex + element.Length);
            }
        }
        var ranges = new List<MatchRange>();
        var haystack = normalized.ToString();
        var terms = new[] { needle };
        if (!haystack.Contains(needle, StringComparison.Ordinal))
            terms = needle.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        foreach (var term in terms.Distinct())
        {
            int at = 0;
            while ((at = haystack.IndexOf(term, at, StringComparison.Ordinal)) >= 0)
            {
                int last = at + term.Length - 1;
                // A strict query must cover the entire original text element.
                if (ignoreMarks || last + 1 == starts.Count || starts[last + 1] != starts[last])
                    ranges.Add(new(starts[at], ends[last] - starts[at]));
                at += term.Length;
            }
        }
        return new(text, ranges.OrderBy(r => r.Start).ToArray());
    }
}

/// <summary>Convenience spellings for lookup, not phonetic transcription or pronunciation rules.</summary>
public static class Transliteration
{
    private static readonly Dictionary<char, string[]> Letters = new()
    {
        ['ⲁ'] = ["a"], ['ⲃ'] = ["b", "v"], ['ⲅ'] = ["g"], ['ⲇ'] = ["d"],
        ['ⲉ'] = ["e"], ['ⲋ'] = ["so", "6"], ['ⲍ'] = ["z"], ['ⲏ'] = ["e", "i", "ee"],
        ['ⲑ'] = ["th"], ['ⲓ'] = ["i", "y"], ['ⲕ'] = ["k"], ['ⲗ'] = ["l"],
        ['ⲙ'] = ["m"], ['ⲛ'] = ["n"], ['ⲝ'] = ["x", "ks"], ['ⲟ'] = ["o"],
        ['ⲡ'] = ["p"], ['ⲣ'] = ["r"], ['ⲥ'] = ["s"], ['ⲧ'] = ["t"],
        ['ⲩ'] = ["u", "y"], ['ⲫ'] = ["ph", "f"], ['ⲭ'] = ["ch", "kh"],
        ['ⲯ'] = ["ps"], ['ⲱ'] = ["o", "oo"], ['ϣ'] = ["sh"], ['ϥ'] = ["f"],
        ['ϧ'] = ["kh", "x"], ['ϩ'] = ["h"], ['ϫ'] = ["j", "dj"],
        ['ϭ'] = ["ch", "tsh"], ['ϯ'] = ["ti", "tee"]
    };
    public static string[][] Index(string text) => DictionaryService.Normalize(text, true)
        .Select(c => Letters.TryGetValue(c, out var spellings) ? spellings : new[] { c.ToString() }).ToArray();
    public static bool IsQuery(string query) => query.Length > 0 && query.Any(c => c is >= 'a' and <= 'z') &&
        query.All(c => c is >= 'a' and <= 'z' or ' ' or '-' or '=' or '6');

    // Track reachable query offsets instead of generating exponentially many spelling variants.
    public static int Match(string[][] letters, string query)
    {
        if (!IsQuery(query)) return -1;
        var offsets = new HashSet<int> { 0 };
        bool prefix = false;
        foreach (var aliases in letters)
        {
            var next = new HashSet<int>();
            foreach (int offset in offsets)
                foreach (var alias in aliases)
                {
                    int remaining = query.Length - offset;
                    if (remaining < alias.Length && alias.StartsWith(query[offset..], StringComparison.Ordinal)) prefix = true;
                    if (remaining >= alias.Length && query.AsSpan(offset, alias.Length).SequenceEqual(alias))
                        next.Add(offset + alias.Length);
                }
            offsets = next;
            if (offsets.Count == 0) break;
        }
        return offsets.Contains(query.Length) ? 0 : prefix ? 1 : -1;
    }
}
