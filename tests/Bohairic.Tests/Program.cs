using Bohairic.Core;
using System.Diagnostics;
using System.Text.Json;

int passed = 0;
void Check(bool condition, string name)
{
    if (!condition) throw new Exception("FAIL: " + name);
    Console.WriteLine("PASS: " + name);
    passed++;
}
var dictionary = new DictionaryService();
await dictionary.LoadAsync(Path.Combine(AppContext.BaseDirectory, "bohairic_dictionary.json"));
Check(dictionary.Entries.Count == 8134, "All 8,134 Bohairic records load");
Check(dictionary.Entries.Select(x => x.Id).Distinct().Count() == 8134, "Every record has a unique favorite ID, including duplicate headwords");
Check(dictionary.Search(new()).Count == 8134, "An empty query preserves every entry");
var ordered = dictionary.Search(new()).ToList();
Check(ordered.FindIndex(x => x.Word == "ⲁ") < ordered.FindIndex(x => x.Word == "ϣ"), "Coptic alphabet order places alpha before shai");
Check(dictionary.Entries.Any(x => x.CrumReference.Length > 0), "Crum references are preserved");
Check(dictionary.Entries.Any(x => x.Etymology.Length > 0), "Etymology is preserved");
Check(DictionaryService.Normalize("Ⲁ\u0304", true) == "ⲁ", "Coptic case and combining-mark folding");
Check(DictionaryService.Normalize("ⲁ\u0304", false) != DictionaryService.Normalize("ⲁ", false), "Strict search retains combining marks");
Check(DictionaryService.Normalize("  LOVE\t  of  God ", true) == "love of god", "Whitespace and English case normalization");
var a = dictionary.Search(new("ⲁ"));
Check(a.Count > 0 && a[0].Word == "ⲁ", "Exact unmarked headword ranks first");
var marked = dictionary.Search(new("ⲁ̄"));
Check(marked.Count > 0 && marked[0].Word == "ⲁ̄", "Exact marked headword outranks mark-folded matches");
var love = dictionary.Search(new("love"));
Check(love.Any(x => x.Word == "ⲁⲅⲁⲡⲏ"), "English definitions find Coptic entries");
var variant = dictionary.Entries.First(x => x.Variants.Any(v => v.Length > 1));
Check(dictionary.Search(new(variant.Variants.First(v => v.Length > 1))).Any(x => x.Id == variant.Id), "Variant search finds its parent record");
var pos = dictionary.Entries.First(x => x.PartOfSpeech.Length > 0).PartOfSpeech;
Check(dictionary.Search(new(PartOfSpeech: pos)).All(x => x.PartOfSpeech == pos), "Part-of-speech filter");
Check(dictionary.Search(new(Initial: "ϣ")).All(x => DictionaryService.Normalize(x.Word, true).StartsWith("ϣ")), "Browse filters by Coptic initial");
var favoriteId = dictionary.Entries[0].Id;
var favorites = dictionary.Search(new(FavoriteIds: new HashSet<string> { favoriteId }));
Check(favorites.Count == 1 && favorites[0].Id == favoriteId, "Favorites distinguish individual entries");
Check(dictionary.Search(new(FavoriteIds: new HashSet<string>())).Count == 0, "Empty favorites shows no entries");
Check(dictionary.Search(new("zzzz_no_such_word_zzzz")).Count == 0, "Unmatched searches return an empty result");
using (var cancellation = new CancellationTokenSource())
{
    cancellation.Cancel();
    bool cancelled = false;
    try { dictionary.Search(new(), cancellation.Token); }
    catch (OperationCanceledException) { cancelled = true; }
    Check(cancelled, "Obsolete searches can be cancelled");
}
var reloaded = new DictionaryService();
await reloaded.LoadAsync(Path.Combine(AppContext.BaseDirectory, "bohairic_dictionary.json"));
Check(dictionary.Entries.Select(x => x.Id).SequenceEqual(reloaded.Entries.Select(x => x.Id)), "Favorite IDs survive reload");
var temp = Path.Combine(Path.GetTempPath(), "Bohairic-tests-" + Guid.NewGuid().ToString("N"));
try
{
    var store = new PreferencesStore(temp);
    var prefs = store.Load();
    prefs.Favorites.Add(favoriteId);
    prefs.RecentSearches.Add("ⲁⲅⲁⲡⲏ");
    prefs.Theme = "Dark";
    store.Save(prefs);
    var loaded = store.Load();
    Check(loaded.Favorites.Contains(favoriteId) && loaded.RecentSearches[0] == "ⲁⲅⲁⲡⲏ" && loaded.Theme == "Dark", "Unicode favorites and settings persist");
    loaded.Theme = "Light"; store.Save(loaded);
    Check(store.Load().Theme == "Light" && Directory.GetFiles(temp, "*.tmp").Length == 0, "Atomic preference replacement leaves no temporary files");
    File.WriteAllText(Path.Combine(temp, "preferences.json"), "{broken");
    Check(store.Load().Favorites.Count == 0 && store.LoadWarning != null, "Corrupt preferences recover with a warning");
    // Independent fixture verifies ranking across exact, prefix, substring, and definition matches.
    var fixture = new[] {
        new DictionaryEntry { Word = "ⲃ", Senses = ["ⲁ"] },
        new DictionaryEntry { Word = "ⲃⲁ" },
        new DictionaryEntry { Word = "ⲁⲃ" },
        new DictionaryEntry { Word = "ⲁ" }
    };
    var fixturePath = Path.Combine(temp, "fixture.json");
    File.WriteAllText(fixturePath, JsonSerializer.Serialize(fixture));
    var sample = new DictionaryService(); await sample.LoadAsync(fixturePath);
    Check(sample.Search(new("ⲁ")).Select(x => x.Word).SequenceEqual(new[] { "ⲁ", "ⲁⲃ", "ⲃⲁ", "ⲃ" }), "Exact > prefix > substring > definition ranking");
    var relevanceFixture = new[] {
        new DictionaryEntry { Word = "ⲁ", Senses = ["Cattle"] },
        new DictionaryEntry { Word = "ⲃ", Senses = ["A small cat sleeps"] },
        new DictionaryEntry { Word = "ⲅ", Senses = ["Cat."] },
        new DictionaryEntry { Word = "ⲇ", Senses = ["See: ⲅ - Cat."], PartOfSpeech = "Cross-reference" },
        new DictionaryEntry { Word = "ⲉ", Senses = ["To drink fresh water."] },
        new DictionaryEntry { Word = "ⲍ", Senses = ["To love."] },
        new DictionaryEntry { Word = "ⲁⲅⲁⲡⲏ", Senses = ["Love"] },
        new DictionaryEntry { Word = "ⲁ̄", Senses = ["One"] },
        new DictionaryEntry { Word = "ⲏ", Senses = ["See: (2)ⲁⲅⲁⲡⲏ - Love."] },
        new DictionaryEntry { Word = "ⲑ", Senses = ["See: ⲭⲭⲭⲭ - Missing target."] }
    };
    File.WriteAllText(fixturePath, JsonSerializer.Serialize(relevanceFixture));
    await sample.LoadAsync(fixturePath);
    var catResults = sample.Search(new("cat"));
    Check(catResults[0].Word == "ⲅ" && catResults[1].Word == "ⲃ", "Exact English meaning then whole-word definition precede substring matches");
    Check(catResults.ToList().FindIndex(x => x.Word == "ⲇ") > 1, "Cross-references do not displace direct meanings");
    Check(sample.Search(new("water drink")).Single().Word == "ⲉ", "Multiword queries match whole words in any order");
    Check(sample.Search(new("to love")).Any(x => x.Word == "ⲍ"), "Infinitive English meanings are normalized");
    Check(sample.Search(new("cat", Scope: SearchScope.Coptic)).Count == 0, "Coptic-only scope excludes English definitions");
    Check(sample.Search(new("ⲁⲅⲁⲡⲏ", Scope: SearchScope.English)).All(x => x.Word != "ⲁⲅⲁⲡⲏ"), "English scope excludes Coptic headword matches");
    Check(sample.Search(new("ⲁ", IgnoreMarks: false, Scope: SearchScope.Coptic)).All(x => x.Word != "ⲁ̄"), "Strict Coptic search preserves mark distinctions");
    Check(sample.Suggest(new("lvoe")).Contains("love"), "Spelling suggestions detect transposed English letters");
    Check(sample.Suggest(new("ⲁⲅⲁⲡⲓ", Scope: SearchScope.Coptic)).Contains("ⲁⲅⲁⲡⲏ"), "Spelling suggestions support Coptic words");
    Check(sample.Suggest(new("ca")).Count == 0, "Very short queries do not generate noisy suggestions");
    Check(sample.Suggest(new("lvoe", FavoriteIds: new HashSet<string>())).Count == 0, "Suggestions respect favorites filtering");
    Check(sample.GetCrossReferences(sample.Entries.First(x => x.Word == "ⲏ")).Single() == "ⲁⲅⲁⲡⲏ", "Numbered cross-references resolve a known target");
    Check(sample.GetCrossReferences(sample.Entries.First(x => x.Word == "ⲑ")).Count == 0, "Unresolved cross-references do not create broken links");
    prefs.Notes[favoriteId] = "Bohairic note ⲁ̄"; prefs.EnglishTextSize = 20; prefs.CopticTextSize = 36; store.Save(prefs);
    var withNotes = store.Load();
    Check(withNotes.Notes[favoriteId] == "Bohairic note ⲁ̄" && withNotes.EnglishTextSize == 20 && withNotes.CopticTextSize == 36, "Notes and reading sizes persist");
}
finally
{
    // This directory was created exclusively by this test run.
    if (Directory.Exists(temp)) Directory.Delete(temp, true);
}
var timer = Stopwatch.StartNew();
for (int i = 0; i < 100; i++) dictionary.Search(new(i % 2 == 0 ? "love" : "ⲁ"));
timer.Stop();
Console.WriteLine($"Search benchmark: {timer.Elapsed.TotalMilliseconds / 100:F2} ms/query over 8,134 records.");
Console.WriteLine($"All {passed} checks passed.");
