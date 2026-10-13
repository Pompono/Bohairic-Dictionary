# Bohairic Dictionary for Windows

A native **C# / WinUI 3** desktop dictionary. It does not use a browser, web server, Electron, or an internet service.

## Run the app

Build with `./scripts/build.ps1 -Test -Publish`, then open `artifacts/BohairicDictionary/BohairicDictionary.exe`. You can also download the Windows build artifact from a successful run in the repository's Actions tab. Extract the full artifact before launching the executable.

The published Windows x64 build contains its .NET and Windows App SDK runtimes. Keep the entire `BohairicDictionary` folder together when moving it to another location. No developer tools are needed to run it. Windows 10 version 1809 or newer is required; this build was exercised on this Windows machine.

## Included

- Only the supplied `bohairic_dictionary.json`: **8,134 entries** and **7,098 distinct headwords**. No Sahidic or combined dataset is bundled or loaded.
- Ranked Coptic and English search, including variants, complete English meanings, whole-word matches, multiword queries, and optional spelling suggestions. Search all fields, Coptic words, or English meanings.
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
