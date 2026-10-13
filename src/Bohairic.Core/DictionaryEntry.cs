using System.Text.Json;
using System.Text.Json.Serialization;

namespace Bohairic.Core;

public sealed class DictionaryEntry
{
    [JsonPropertyName("word")] public string Word { get; set; } = "";
    [JsonPropertyName("tla_id")] public string TlaId { get; set; } = "";
    [JsonPropertyName("entry_url")] public string EntryUrl { get; set; } = "";
    [JsonPropertyName("bohairic_variants")] public string[] Variants { get; set; } = [];
    [JsonPropertyName("pos")] public string PartOfSpeech { get; set; } = "";
    [JsonPropertyName("scriptorium_tag")] public string Tag { get; set; } = "";
    [JsonPropertyName("greek_origin")] public string GreekOrigin { get; set; } = "";
    [JsonPropertyName("senses")] public string[] Senses { get; set; } = [];
    [JsonPropertyName("bohairic_examples")] public DictionaryExample[] Examples { get; set; } = [];
    [JsonPropertyName("crum_ref")] public string CrumReference { get; set; } = "";
    [JsonPropertyName("etymology")] public string Etymology { get; set; } = "";
    [JsonExtensionData] public Dictionary<string, JsonElement>? AdditionalData { get; set; }
    [JsonIgnore] public string Id { get; internal set; } = "";
    [JsonIgnore] public string Preview => string.Join("; ", Senses);
    [JsonIgnore] public string Subtitle => string.IsNullOrWhiteSpace(PartOfSpeech) ? "Bohairic" : PartOfSpeech;
    public override string ToString() => $"{Word} — {Preview}";
}

public sealed class DictionaryExample
{
    [JsonPropertyName("sentence")] public string Sentence { get; set; } = "";
    [JsonPropertyName("translation")] public string Translation { get; set; } = "";
    [JsonPropertyName("source")] public string Source { get; set; } = "";
}
