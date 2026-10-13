using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace Bohairic.Core;

public enum SearchScope { All, Coptic, English, Transliteration }
public sealed record SearchOptions(string Query = "", string PartOfSpeech = "", string Initial = "",
    bool IgnoreMarks = true, IReadOnlySet<string>? FavoriteIds = null, SearchScope Scope = SearchScope.All);

public sealed class DictionaryService
{
    private sealed record Indexed(DictionaryEntry Entry, string Word, string FoldedWord,
        string[] Forms, string[] FoldedForms, string Gloss, string FoldedGloss, string Metadata, string FoldedMetadata, string SortKey,
        string[] Meanings, string[] Tokens, bool CrossReference, string[][][] LatinForms);
    private static readonly Regex Words = new(@"[\p{L}\p{M}\p{N}]+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private static readonly Regex MeaningPrefix = new(@"^(?:(?:vt|vi|vb|v|n|m|f|adj|adv)\.\s*|to\s+)+", RegexOptions.Compiled | RegexOptions.CultureInvariant);
    private readonly List<Indexed> index = [];
    public IReadOnlyList<DictionaryEntry> Entries { get; private set; } = [];

    public async Task LoadAsync(string path)
    {
        await using var stream = File.OpenRead(path);
        var entries = await JsonSerializer.DeserializeAsync<List<DictionaryEntry>>(stream)
            ?? throw new InvalidDataException("The dictionary is empty or invalid.");
        index.Clear();
        var occurrences = new Dictionary<string, int>();
        foreach (var e in entries)
        {
            // Hash the complete source record, then distinguish even identical duplicate records.
            var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(e))));
            occurrences.TryGetValue(hash, out int occurrence);
            occurrences[hash] = occurrence + 1;
            e.Id = $"boh-{hash}-{occurrence}";
            var forms = e.Variants.Append(e.Word).Distinct().ToArray();
            var gloss = string.Join(" • ", e.Senses);
            var metadata = string.Join(" ", e.CrumReference, e.Etymology, e.GreekOrigin, e.Tag);
            index.Add(new(e, Normalize(e.Word, false), Normalize(e.Word, true),
                forms.Select(x => Normalize(x, false)).ToArray(), forms.Select(x => Normalize(x, true)).ToArray(),
                Normalize(gloss, false), Normalize(gloss, true), Normalize(metadata, false), Normalize(metadata, true), AlphabetKey(e.Word),
                e.Senses.SelectMany(s => s.Split([',', ';', '\n'])).Select(s => MeaningPrefix.Replace(Normalize(s, true), "").Trim(' ', '.', '!', '?', '"')).ToArray(),
                Tokens(Normalize(gloss, true)), e.PartOfSpeech.Equals("Cross-reference", StringComparison.OrdinalIgnoreCase) || e.Senses.All(s => s.StartsWith("See:", StringComparison.OrdinalIgnoreCase)), forms.Select(Transliteration.Index).ToArray()));
        }
        Entries = entries;
    }

    private static string[] Tokens(string text) => Words.Matches(text).Select(m => m.Value).Distinct().ToArray();
    private static bool ContainsCoptic(string text) => text.Any(c => c is >= '\u2C80' and <= '\u2CFF' or >= '\u03E2' and <= '\u03EF');
    private static bool FormMatch(string form, string query, bool prefix, bool ignoreMarks)
    {
        int at = form.IndexOf(query, StringComparison.Ordinal);
        while (at >= 0)
        {
            if (prefix && at != 0) return false;
            int end = at + query.Length;
            // A base character is not a complete match for a marked character in strict mode.
            if (ignoreMarks || end == form.Length || CharUnicodeInfo.GetUnicodeCategory(form, end) is not
                (UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)) return true;
            if (prefix) return false;
            at = form.IndexOf(query, at + 1, StringComparison.Ordinal);
        }
        return false;
    }
    private static bool Allowed(Indexed item, SearchOptions options, string initial) =>
        (options.FavoriteIds == null || options.FavoriteIds.Contains(item.Entry.Id)) &&
        (options.PartOfSpeech.Length == 0 || item.Entry.PartOfSpeech == options.PartOfSpeech) &&
        (initial.Length == 0 || item.FoldedWord.StartsWith(initial, StringComparison.Ordinal));

    private static string AlphabetKey(string word)
    {
        const string alphabet = "ⲁⲃⲅⲇⲉⲋⲍⲏⲑⲓⲕⲗⲙⲛⲝⲟⲡⲣⲥⲧⲩⲫⲭⲯⲱϣϥϧϩϫϭϯ";
        return new string(Normalize(word, true).Select(c =>
        {
            int position = alphabet.IndexOf(c);
            return position >= 0 ? (char)(0x100 + position) : c;
        }).ToArray());
    }

    public static string Normalize(string text, bool ignoreMarks)
    {
        var value = text.Normalize(NormalizationForm.FormKD).ToLowerInvariant();
        var output = new StringBuilder();
        bool previousSpace = false;
        foreach (var rune in value.EnumerateRunes())
        {
            var category = Rune.GetUnicodeCategory(rune);
            if (ignoreMarks && category is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark or UnicodeCategory.EnclosingMark)
                continue;
            if (Rune.IsWhiteSpace(rune))
            {
                if (!previousSpace) output.Append(' ');
                previousSpace = true;
            }
            else { output.Append(rune.ToString()); previousSpace = false; }
        }
        return output.ToString().Trim().Normalize(NormalizationForm.FormC);
    }

    public IReadOnlyList<DictionaryEntry> Search(SearchOptions options, CancellationToken cancellation = default)
        => SearchMatches(options, cancellation).Select(x => x.Entry).ToArray();

    public IReadOnlyList<SearchMatch> SearchMatches(SearchOptions options, CancellationToken cancellation = default)
    {
        string query = Normalize(options.Query, options.IgnoreMarks);
        string exactQuery = Normalize(options.Query, false);
        string initial = Normalize(options.Initial, true);
        string englishQuery = MeaningPrefix.Replace(Normalize(query, true).Trim(' ', '.', '!', '?', '"'), "");
        var queryTokens = Tokens(englishQuery);
        string latinQuery = Normalize(query, true);
        bool useLatin = options.Scope is SearchScope.All or SearchScope.Transliteration && Transliteration.IsQuery(latinQuery);
        var results = new List<(int Rank, Indexed Item, string Label, string Evidence, bool Latin)>();
        foreach (var item in index)
        {
            cancellation.ThrowIfCancellationRequested();
            var entry = item.Entry;
            if (!Allowed(item, options, initial)) continue;
            var word = options.IgnoreMarks ? item.FoldedWord : item.Word;
            var forms = options.IgnoreMarks ? item.FoldedForms : item.Forms;
            var gloss = options.IgnoreMarks ? item.FoldedGloss : item.Gloss;
            var metadata = options.IgnoreMarks ? item.FoldedMetadata : item.Metadata;
            int rank = query.Length == 0 ? 0 : options.Scope is SearchScope.English or SearchScope.Transliteration ? -1
                : item.Word == exactQuery ? 0
                : word == query ? 1
                : forms.Contains(query) ? 2
                : FormMatch(word, query, true, options.IgnoreMarks) ? 3
                : forms.Any(f => FormMatch(f, query, true, options.IgnoreMarks)) ? 4
                : forms.Any(f => FormMatch(f, query, false, options.IgnoreMarks)) ? 5 : -1;
            string label = query.Length == 0 ? "" : rank switch { 0 or 1 => "Headword · exact", 2 => "Variant · exact", 3 => "Headword · starts with", 4 or 5 => "Variant or headword · contains", _ => "" };
            string evidence = rank is 2 or 4 or 5 ? entry.Variants.FirstOrDefault(v => FormMatch(Normalize(v, options.IgnoreMarks), query, false, options.IgnoreMarks)) ?? "" : "";
            bool latin = false;
            if (rank < 0 && options.Scope is SearchScope.All or SearchScope.English)
            {
                // Complete meanings beat words in definitions; whole words beat incidental substrings.
                rank = !ContainsCoptic(query) && item.Meanings.Contains(englishQuery) ? 10
                    : !ContainsCoptic(query) && queryTokens.Length > 0 && queryTokens.All(t => item.Tokens.Contains(t))
                        ? (gloss.Contains(query, StringComparison.Ordinal) ? 11 : 12)
                    : gloss.Contains(query, StringComparison.Ordinal) ? 14
                    : options.Scope == SearchScope.All && metadata.Contains(query, StringComparison.Ordinal) ? 16 : -1;
                label = rank switch { 10 => "English meaning · exact", 11 or 12 => "Definition · whole words", 14 => "Definition · contains", 16 => "Reference or etymology", _ => "" };
                if (rank == 16) evidence = string.Join(" · ", new[] { entry.CrumReference, entry.Etymology, entry.GreekOrigin, entry.Tag }.Where(v => Normalize(v, options.IgnoreMarks).Contains(query, StringComparison.Ordinal)));
                if (rank >= 0 && item.CrossReference) rank += 4;
            }
            if (useLatin && (rank < 0 || rank > 13))
            {
                for (int i = 0; i < item.LatinForms.Length; i++)
                {
                    int match = Transliteration.Match(item.LatinForms[i], latinQuery);
                    int latinRank = match == 0 ? 13 : 17;
                    if (match < 0 || (rank >= 0 && rank <= latinRank)) continue;
                    rank = latinRank;
                    latin = true;
                    label = match == 0 ? "Transliteration · approximate spelling" : "Transliteration · starts with (approximate)";
                    evidence = entry.Variants.Append(entry.Word).Distinct().ElementAt(i);
                }
            }
            if (rank >= 0) results.Add((rank, item, label, evidence, latin));
        }
        return results.OrderBy(x => x.Rank).ThenBy(x => x.Item.SortKey, StringComparer.Ordinal)
            .ThenBy(x => x.Item.Word, StringComparer.Ordinal).ThenBy(x => x.Item.Entry.PartOfSpeech, StringComparer.Ordinal)
            .Select(x => new SearchMatch(x.Item.Entry, x.Label,
                x.Latin && x.Evidence == x.Item.Entry.Word
                    ? new(x.Item.Entry.Word, new[] { new MatchRange(0, x.Item.Entry.Word.Length) })
                    : SearchText.Highlight(x.Item.Entry.Word, options.Scope == SearchScope.English || x.Latin ? "" : query, options.IgnoreMarks),
                SearchText.Highlight(x.Item.Entry.Preview, options.Scope is SearchScope.Coptic or SearchScope.Transliteration || x.Latin ? "" : query, options.IgnoreMarks),
                x.Latin ? x.Evidence == x.Item.Entry.Word ? new("", []) : new(x.Evidence, new[] { new MatchRange(0, x.Evidence.Length) })
                    : SearchText.Highlight(x.Evidence, query, options.IgnoreMarks))).ToArray();
    }

    /// <summary>Conservative spelling suggestions, explicitly chosen by the user; never substitute the query.</summary>
    public IReadOnlyList<string> Suggest(SearchOptions options, CancellationToken cancellation = default)
    {
        var query = Normalize(options.Query, options.IgnoreMarks);
        if (options.Scope == SearchScope.Transliteration) return [];
        if (query.Length is < 3 or > 48 || query.Contains(' ')) return [];
        bool coptic = ContainsCoptic(query);
        if ((coptic && options.Scope == SearchScope.English) || (!coptic && options.Scope == SearchScope.Coptic)) return [];
        int limit = query.Length >= 6 ? 2 : 1;
        var candidates = new Dictionary<string, int>(StringComparer.Ordinal);
        foreach (var item in index)
        {
            cancellation.ThrowIfCancellationRequested();
            if (!Allowed(item, options, Normalize(options.Initial, true))) continue;
            var values = coptic ? (options.IgnoreMarks ? item.FoldedForms : item.Forms) : item.Tokens;
            foreach (var value in values)
            {
                if (value.Length < 3 || Math.Abs(query.Length - value.Length) > limit || candidates.ContainsKey(value)) continue;
                int distance = EditDistance(query, value);
                if (distance is > 0 && distance <= limit) candidates[value] = distance;
            }
        }
        return candidates.OrderBy(x => x.Value).ThenBy(x => Math.Abs(x.Key.Length - query.Length))
            .ThenBy(x => x.Key, StringComparer.Ordinal).Take(3).Select(x => x.Key).ToArray();
    }

    // Optimal string alignment distance includes adjacent transpositions (e.g. "lvoe").
    private static int EditDistance(string a, string b)
    {
        var d = new int[a.Length + 1, b.Length + 1];
        for (int i = 0; i <= a.Length; i++) d[i, 0] = i;
        for (int j = 0; j <= b.Length; j++) d[0, j] = j;
        for (int i = 1; i <= a.Length; i++)
            for (int j = 1; j <= b.Length; j++)
            {
                int cost = a[i - 1] == b[j - 1] ? 0 : 1;
                d[i, j] = Math.Min(Math.Min(d[i - 1, j] + 1, d[i, j - 1] + 1), d[i - 1, j - 1] + cost);
                if (i > 1 && j > 1 && a[i - 1] == b[j - 2] && a[i - 2] == b[j - 1])
                    d[i, j] = Math.Min(d[i, j], d[i - 2, j - 2] + 1);
            }
        return d[a.Length, b.Length];
    }

    public IReadOnlyList<string> GetCrossReferences(DictionaryEntry entry)
    {
        var targets = new List<string>();
        foreach (var sense in entry.Senses)
        {
            var match = Regex.Match(sense, @"\bSee:\s*(?:\(\d+\))?\s*([\u2C80-\u2CFF\u03E2-\u03EF\p{M}]+(?:[-=])?)", RegexOptions.IgnoreCase);
            if (!match.Success) continue;
            var target = match.Groups[1].Value;
            if (index.Any(x => x.FoldedForms.Contains(Normalize(target, true)))) targets.Add(target);
        }
        return targets.Distinct().ToArray();
    }
}
