# Bohairic Dictionary for Windows

A native **C# / WinUI 3** desktop dictionary. It does not use a browser, web server, Electron, or an internet service.

## Run the app

Download **BohairicDictionary-windows-x64.zip** from the [latest release](https://github.com/Pompono/Bohairic-Dictionary/releases/latest), extract the entire ZIP, and open `BohairicDictionary.exe`. Keep all extracted files together.

To build it yourself, use `./scripts/build.ps1 -Test -Publish`, then open `artifacts/BohairicDictionary/BohairicDictionary.exe`. You can also download the Windows build artifact from a successful run in the repository's Actions tab. Extract the full artifact before launching the executable.

The published Windows x64 build contains its .NET and Windows App SDK runtimes. Keep the entire `BohairicDictionary` folder together when moving it to another location. No developer tools are needed to run it. Windows 10 version 1809 or newer is required; this build was exercised on this Windows machine.

## Included

- Only the supplied `bohairic_dictionary.json`: **8,134 entries** and **7,098 distinct headwords**. No Sahidic or combined dataset is bundled or loaded.
- Ranked Coptic and English search, including variants, complete English meanings, whole-word matches, multiword queries, and optional spelling suggestions. Search all fields, Coptic words, or English meanings.
- Transliteration lookup using English letters (for example, `agape`, `agapi`, or `agapee` for ⲁⲅⲁⲡⲏ), with exact/prefix matching of headwords and variants. Select Transliteration to search only these spellings; All fields includes them below strong English matches.
- Match explanations and highlighted original text, including matching variants and metadata. Highlights preserve combining marks and the Coptic font.
- Optional search without combining marks, preserving original displayed text and favoring exact spelling.
- Coptic alphabet ordering, initial-letter browsing, and part-of-speech filtering.
- Entry meanings, variants, grammatical information, etymology, Greek-origin metadata, Crum references, and source references when present.
- Favorites, personal notes, recent searches, copy controls, and system/light/dark themes.
- Clickable dictionary cross-references, adjustable English/Coptic reading sizes, and a draggable divider with a narrow-window layout.
- Coptic keyboard with uppercase, combining marks, numeral forms, and Unicode-aware backspace.
- Embedded Free Serif Avva Shenouda font from the original repository for offline display.

Use **Ctrl+F** to focus dictionary search. Press Enter or leave the search box to save a recent search. Recent searches are available in Settings. Filters and the query are retained when moving between Dictionary, Favorites, and Browse; use Clear and “All parts of speech” to broaden the results. Browse additionally offers an initial-letter selector.

Examples are supported by the reader, but the supplied file currently has no populated example arrays. This first version is a dictionary; lessons and machine translation are not included.

## Your data

The original JSON is copied without modification to `src/Bohairic.Desktop/Data/bohairic_dictionary.json` and included verbatim in the published app. SHA-256:

`3F7EB9D9BE2807DCBBBBDE1A2F5DBDFEB2CC8BA5A63DF7FADEE59FAEDBFF9FAC`

Duplicate headwords are preserved as separate entries. Favorite IDs hash complete records and distinguish identical duplicates; moving the unchanged dataset preserves favorites. Editing the contents of a record changes its ID.

Preferences, favorites, notes, and recent searches are saved locally to `%LOCALAPPDATA%\BohairicDictionary\preferences.json`. There is no account, sync, or application network request. Unhandled errors, if any, are logged beside preferences as `error.log`.

## Build and test

Requires a .NET 8 SDK on Windows and internet access for the first NuGet restore. The project-local SDK is automatically used if available under `.tools/dotnet`.

```powershell
# Build and run the automated checks
.\scripts\build.ps1 -Test

# Produce the self-contained app directory
.\scripts\build.ps1 -Publish
```

The solution `BohairicDictionary.sln` can also be opened in Visual Studio 2022 with .NET/WinUI development tooling. Build the desktop project for x64. Package versions are pinned in its project file.

`Bohairic.Core` contains the data model, normalized search index, ranking, alphabet ordering, and atomic preference storage. `Bohairic.Desktop` contains the native WinUI application and its assets. `Bohairic.Tests` is a dependency-free executable test suite against the actual dictionary and controlled ranking fixtures.

The portable distribution is unpackaged, not an MSIX installer, and is not code-signed. A signed installer can be added as a separate distribution step.

## Credits

Dictionary data: the user's supplied Bohairic dictionary. Its existing source and reference fields are retained; this project does not assign a new license to that data.

Coptic keyboard layout and dictionary concepts are adapted from the user's [Pompono/Coptic](https://github.com/Pompono/Coptic) project. The native Windows interface and C# implementation are new.

Free Serif Avva Shenouda: copied unchanged from `Pompono/Coptic/public/FreeSerifAvvaShenouda.ttf`. The embedded font metadata identifies the Free Software Foundation with Avva Shenouda merged and grants use under the GNU General Public License. See `src/Bohairic.Desktop/Assets/FONT-NOTICE.txt` (also distributed with the app).

## Transliteration lookup

This is a typing aid, not a pronunciation guide. It ignores Coptic combining marks, accepts uppercase Latin input, and searches from the beginning of a word or variant. Collisions can produce several results, so all such matches are labeled approximate. English and Coptic scopes retain their original behavior.

Supported spellings: ⲁ a; ⲃ b/v; ⲅ g; ⲇ d; ⲉ e; ⲋ so/6; ⲍ z; ⲏ e/i/ee; ⲑ th; ⲓ i/y; ⲕ k; ⲗ l; ⲙ m; ⲛ n; ⲝ x/ks; ⲟ o; ⲡ p; ⲣ r; ⲥ s; ⲧ t; ⲩ u/y; ⲫ ph/f; ⲭ ch/kh; ⲯ ps; ⲱ o/oo; ϣ sh; ϥ f; ϧ kh/x; ϩ h; ϫ j/dj; ϭ ch/tsh; ϯ ti/tee. These are explicit lookup conventions, not context-sensitive phonetic rules.

## Releases

Pushing a version tag such as `v1.2.0` runs the Windows tests and build, packages the complete self-contained Windows x64 app, and publishes a GitHub release with a ZIP and SHA-256 checksum. Branch builds continue to provide Actions artifacts. The portable executable is unsigned.
