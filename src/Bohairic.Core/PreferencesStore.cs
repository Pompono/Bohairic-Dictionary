using System.Text.Json;

namespace Bohairic.Core;

public sealed class Preferences
{
    public HashSet<string> Favorites { get; set; } = [];
    public List<string> RecentSearches { get; set; } = [];
    public string Theme { get; set; } = "System";
    public bool IgnoreMarks { get; set; } = true;
    public bool ShowKeyboard { get; set; }
    public double EnglishTextSize { get; set; } = 16;
    public double CopticTextSize { get; set; } = 30;
    public Dictionary<string, string> Notes { get; set; } = [];
}

public sealed class PreferencesStore(string directory)
{
    public string DirectoryPath { get; } = directory;
    public string? LoadWarning { get; private set; }
    public Preferences Load()
    {
        var path = Path.Combine(DirectoryPath, "preferences.json");
        if (!File.Exists(path)) return new();
        try
        {
            var prefs = JsonSerializer.Deserialize<Preferences>(File.ReadAllText(path)) ?? new();
            prefs.Favorites ??= [];
            prefs.RecentSearches ??= [];
            prefs.Notes ??= [];
            prefs.EnglishTextSize = double.IsFinite(prefs.EnglishTextSize) ? Math.Clamp(prefs.EnglishTextSize, 14, 24) : 16;
            prefs.CopticTextSize = double.IsFinite(prefs.CopticTextSize) ? Math.Clamp(prefs.CopticTextSize, 24, 42) : 30;
            return prefs;
        }
        catch (Exception ex) when (ex is JsonException or IOException or UnauthorizedAccessException)
        {
            LoadWarning = "Saved preferences could not be read. Default settings are in use.";
            return new();
        }
    }

    public void Save(Preferences preferences)
    {
        Directory.CreateDirectory(DirectoryPath);
        var target = Path.Combine(DirectoryPath, "preferences.json");
        var temporary = Path.Combine(DirectoryPath, $"preferences-{Guid.NewGuid():N}.tmp");
        try
        {
            File.WriteAllText(temporary, JsonSerializer.Serialize(preferences, new JsonSerializerOptions { WriteIndented = true }));
            File.Move(temporary, target, true);
        }
        finally { if (File.Exists(temporary)) File.Delete(temporary); }
    }
}
