using Bohairic.Core;
using Microsoft.UI;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Automation;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Controls.Primitives;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Input;
using Microsoft.UI.Xaml.Markup;
using Microsoft.UI.Xaml.Media;
using Windows.ApplicationModel.DataTransfer;
using Windows.System;

namespace Bohairic.Desktop;

public sealed class MainWindow : Window
{
    private readonly DictionaryService dictionary = new();
    private readonly PreferencesStore store = new(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "BohairicDictionary"));
    private readonly Preferences preferences;
    private readonly NavigationView navigation = new();
    private readonly Grid root = (Grid)XamlReader.Load("<Grid xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation' Background='{ThemeResource ApplicationPageBackgroundThemeBrush}'/>");
    private readonly Grid dictionaryPage = new();
    private readonly StackPanel settingsPage = new() { Spacing = 22, Padding = new Thickness(32), MaxWidth = 760, HorizontalAlignment = HorizontalAlignment.Left };
    private readonly TextBlock heading = Text("Dictionary", 32);
    private readonly TextBlock resultCount = Text("Loading your dictionary…", 13);
    private readonly TextBox search = new() { PlaceholderText = "Search Coptic or English", FontSize = 19, MinHeight = 48 };
    private readonly ComboBox posFilter = new() { Width = 190, PlaceholderText = "All parts of speech" };
    private readonly ComboBox letterFilter = new() { Width = 165, Visibility = Visibility.Collapsed };
    private readonly ComboBox searchScope = new() { Width = 130, ItemsSource = new[] { "All fields", "Coptic words", "English meanings" }, SelectedIndex = 0 };
    private readonly StackPanel suggestions = new() { Orientation = Orientation.Horizontal, Spacing = 8, Visibility = Visibility.Collapsed };
    private readonly Grid dictionarySplit = new();
    private readonly Grid listPane = new();
    private readonly ScrollViewer detailPane = new();
    private readonly Thumb divider = new() { MinWidth = 8, MinHeight = 8, IsTabStop = true };
    private double splitRatio = 0.43;
    private bool narrowLayout;
    private string lastQuery = "";
    private Action? saveCurrentNote;
    private readonly ListView results = new() { SelectionMode = ListViewSelectionMode.Single };
    private readonly StackPanel details = new() { Spacing = 18, Padding = new Thickness(28) };
    private readonly StackPanel keyboard = new() { Spacing = 6, Padding = new Thickness(12), Visibility = Visibility.Collapsed };
    private readonly InfoBar notice = new() { IsOpen = false, IsClosable = true };
    private readonly TextBlock emptyResults = Text("", 16);
    private readonly ProgressRing loading = new() { IsActive = true, Width = 32, Height = 32 };
    private static FontFamily copticFont => CopticTypography.Font;
    private readonly Button favoriteButton = new();
    private readonly Button moreButton = new() { Content = "Show more results", HorizontalAlignment = HorizontalAlignment.Stretch };
    private IReadOnlyList<DictionaryEntry> currentResults = [];
    private CancellationTokenSource? searchCancellation;
    private DictionaryEntry? selected;
    private string currentPage = "Dictionary";
    private bool ready;
    private int shownCount;
    private bool shifted;
    private bool symbolKeys;
    private bool closed;
    private static readonly string[] Alphabet = ["ⲁ", "ⲃ", "ⲅ", "ⲇ", "ⲉ", "ⲋ", "ⲍ", "ⲏ", "ⲑ", "ⲓ", "ⲕ", "ⲗ", "ⲙ", "ⲛ", "ⲝ", "ⲟ", "ⲡ", "ⲣ", "ⲥ", "ⲧ", "ⲩ", "ⲫ", "ⲭ", "ⲯ", "ⲱ", "ϣ", "ϥ", "ϧ", "ϩ", "ϫ", "ϭ", "ϯ"];

    public MainWindow()
    {
        preferences = store.Load();
        Title = "Bohairic Dictionary";
        AppWindow.Resize(new Windows.Graphics.SizeInt32(1240, 820));
        Content = root;
        ApplyTheme();
        BuildNavigation();
        BuildDictionaryPage();
        BuildSettings();
        BuildKeyboard();
        Closed += (_, _) => { saveCurrentNote?.Invoke(); closed = true; searchCancellation?.Cancel(); };
        root.Loaded += async (_, _) =>
        {
            var scale = root.XamlRoot.RasterizationScale;
            var area = Microsoft.UI.Windowing.DisplayArea.GetFromWindowId(AppWindow.Id, Microsoft.UI.Windowing.DisplayAreaFallback.Primary).WorkArea;
            int width = Math.Min((int)(1200 * scale), area.Width - 40);
            int height = Math.Min((int)(850 * scale), area.Height - 40);
            AppWindow.MoveAndResize(new Windows.Graphics.RectInt32(area.X + (area.Width - width) / 2, area.Y + (area.Height - height) / 2, width, height));
            await LoadDictionaryAsync();
        };
        var shortcut = new KeyboardAccelerator { Key = VirtualKey.F, Modifiers = VirtualKeyModifiers.Control };
        shortcut.Invoked += (_, args) => { ShowPage("Dictionary"); search.Focus(FocusState.Programmatic); args.Handled = true; };
        root.KeyboardAccelerators.Add(shortcut);
    }

    private static TextBlock Text(string value, double size = 14)
    {
        var block = new TextBlock { FontSize = size, TextWrapping = TextWrapping.Wrap, IsTextSelectionEnabled = true, FontFamily = CopticTypography.InterfaceFont, LineHeight = Math.Ceiling(size * 1.5), LineStackingStrategy = LineStackingStrategy.MaxHeight, UseLayoutRounding = true };
        Typography.SetStandardLigatures(block, false);
        // Plain labels are updated through Text elsewhere; use runs only for mixed-script content.
        if (HasCoptic(value)) CopticTypography.SetText(block, value);
        else block.Text = value;
        return block;
    }
    private static bool HasCoptic(string value) => value.Any(c => c is >= '\u2C80' and <= '\u2CFF' or >= '\u03E2' and <= '\u03EF');
    private static Brush Brush(string resource) => (Brush)Application.Current.Resources[resource];
    private static Button Button(string label, RoutedEventHandler action)
    {
        var button = new Button { Content = label, MinHeight = 34 };
        if (HasCoptic(label)) button.FontFamily = copticFont;
        button.Click += action;
        return button;
    }
    private static void Name(DependencyObject element, string name) => AutomationProperties.SetName(element, name);

    private void BuildNavigation()
    {
        navigation.PaneDisplayMode = NavigationViewPaneDisplayMode.Auto;
        navigation.OpenPaneLength = 210;
        navigation.CompactModeThresholdWidth = 800;
        navigation.ExpandedModeThresholdWidth = 1120;
        navigation.IsBackButtonVisible = NavigationViewBackButtonVisible.Collapsed;
        navigation.IsSettingsVisible = true;
        var brand = new StackPanel { Spacing = 3, Margin = new Thickness(16, 16, 8, 24) };
        brand.Children.Add(Text("Ⲃ", 40));
        brand.Children.Add(Text("BOHAIRIC", 17));
        brand.Children.Add(Text("Coptic dictionary", 12));
        navigation.PaneHeader = brand;
        var footer = new TextBlock { Text = "OFFLINE · BOHAIRIC ONLY", FontSize = 10, Margin = new Thickness(16, 20, 0, 16) };
        navigation.PaneFooter = footer;
        navigation.DisplayModeChanged += (_, args) => footer.Visibility = args.DisplayMode == NavigationViewDisplayMode.Expanded ? Visibility.Visible : Visibility.Collapsed;
        foreach (var (label, icon) in new[] { ("Dictionary", Symbol.Find), ("Favorites", Symbol.Favorite), ("Browse", Symbol.AllApps) })
            navigation.MenuItems.Add(new NavigationViewItem { Content = label, Tag = label, Icon = new SymbolIcon(icon) });
        navigation.SelectedItem = navigation.MenuItems[0];
        navigation.SelectionChanged += (_, args) => ShowPage(args.IsSettingsSelected ? "Settings" : (args.SelectedItemContainer?.Tag?.ToString() ?? "Dictionary"));
        var content = new Grid();
        content.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        content.RowDefinitions.Add(new RowDefinition());
        notice.Margin = new Thickness(24, 12, 24, 0);
        content.Children.Add(notice);
        Grid.SetRow(dictionaryPage, 1);
        content.Children.Add(dictionaryPage);
        var settingsScroll = new ScrollViewer { Content = settingsPage, Visibility = Visibility.Collapsed };
        settingsScroll.Name = "SettingsScroll";
        Grid.SetRow(settingsScroll, 1);
        content.Children.Add(settingsScroll);
        navigation.Content = content;
        root.Children.Add(navigation);
    }

    private void BuildDictionaryPage()
    {
        dictionaryPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        dictionaryPage.RowDefinitions.Add(new RowDefinition());
        dictionaryPage.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        var header = new StackPanel { Spacing = 14, Margin = new Thickness(28, 22, 28, 20) };
        heading.FontWeight = Microsoft.UI.Text.FontWeights.SemiBold;
        header.Children.Add(heading);
        header.Children.Add(Text("Explore the words of the Bohairic language.", 14));
        var searchRow = new Grid { ColumnSpacing = 8 };
        searchRow.ColumnDefinitions.Add(new ColumnDefinition());
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        searchRow.ColumnDefinitions.Add(new ColumnDefinition { Width = GridLength.Auto });
        Name(search, "Search Coptic or English");
        search.TextChanged += (_, _) =>
        {
            search.FontFamily = HasCoptic(search.Text) ? copticFont : CopticTypography.InterfaceFont;
            QueueSearch();
        };
        search.KeyDown += (_, e) => { if (e.Key == VirtualKey.Enter) RememberSearch(); };
        search.LostFocus += (_, _) => RememberSearch();
        searchRow.Children.Add(search);
        var clear = Button("Clear", (_, _) => { search.Text = ""; search.Focus(FocusState.Programmatic); });
        Grid.SetColumn(clear, 1); searchRow.Children.Add(clear);
        var keys = Button("ⲁⲃⲅ", (_, _) =>
        {
            preferences.ShowKeyboard = !preferences.ShowKeyboard;
            keyboard.Visibility = preferences.ShowKeyboard ? Visibility.Visible : Visibility.Collapsed;
            SavePreferences();
        });
        Name(keys, "Toggle Coptic keyboard");
        ToolTipService.SetToolTip(keys, "Coptic keyboard");
        Grid.SetColumn(keys, 2); searchRow.Children.Add(keys);
        header.Children.Add(searchRow);
        var filters = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 12 };
        Name(posFilter, "Part of speech");
        posFilter.SelectionChanged += (_, _) => QueueSearch();
        filters.Children.Add(posFilter);
        letterFilter.FontFamily = copticFont;
        letterFilter.Items.Add("All letters");
        foreach (var letter in Alphabet) letterFilter.Items.Add(letter);
        letterFilter.SelectedIndex = 0;
        letterFilter.SelectionChanged += (_, _) => QueueSearch();
        Name(letterFilter, "Starting Coptic letter");
        filters.Children.Add(letterFilter);
        Name(searchScope, "Search in");
        searchScope.SelectionChanged += (_, _) => QueueSearch();
        filters.Children.Insert(0, searchScope);
        header.Children.Add(new ScrollViewer { Content = filters, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Disabled, HorizontalScrollMode = ScrollMode.Enabled });
        header.Children.Add(suggestions);
        resultCount.Opacity = 0.7;
        header.Children.Add(resultCount);
        dictionaryPage.Children.Add(header);

        Grid.SetRow(dictionarySplit, 1);
        dictionaryPage.Children.Add(dictionarySplit);
        listPane.RowDefinitions.Add(new RowDefinition());
        listPane.RowDefinitions.Add(new RowDefinition { Height = GridLength.Auto });
        results.Margin = new Thickness(12, 8, 8, 8);
        ApplyReadingSizes();
        Name(results, "Dictionary results");
        results.SelectionChanged += (_, _) =>
        {
            selected = results.SelectedItem as DictionaryEntry;
            ShowEntry();
        };
        listPane.Children.Add(results);
        emptyResults.Margin = new Thickness(24);
        emptyResults.VerticalAlignment = VerticalAlignment.Center;
        emptyResults.HorizontalAlignment = HorizontalAlignment.Center;
        emptyResults.Visibility = Visibility.Collapsed;
        listPane.Children.Add(emptyResults);
        listPane.Children.Add(loading);
        moreButton.Visibility = Visibility.Collapsed;
        moreButton.Margin = new Thickness(20, 4, 20, 12);
        moreButton.Click += (_, _) => ShowMore();
        Grid.SetRow(moreButton, 1);
        listPane.Children.Add(moreButton);
        dictionarySplit.Children.Add(listPane);
        detailPane.HorizontalScrollBarVisibility = ScrollBarVisibility.Disabled;
        detailPane.Content = details;
        dictionarySplit.Children.Add(detailPane);
        Name(divider, "Resize results and definition panes. Use arrow keys or drag.");
        divider.Template = (ControlTemplate)XamlReader.Load("<ControlTemplate xmlns='http://schemas.microsoft.com/winfx/2006/xaml/presentation'><Border Background='{ThemeResource DividerStrokeColorDefaultBrush}' CornerRadius='4' Margin='2'/></ControlTemplate>");
        divider.DragDelta += (_, args) => ResizePanes((narrowLayout ? args.VerticalChange : args.HorizontalChange) / Math.Max(1, narrowLayout ? dictionarySplit.ActualHeight : dictionarySplit.ActualWidth));
        divider.KeyDown += (_, args) =>
        {
            if (args.Key is VirtualKey.Left or VirtualKey.Up) { ResizePanes(-0.03); args.Handled = true; }
            if (args.Key is VirtualKey.Right or VirtualKey.Down) { ResizePanes(0.03); args.Handled = true; }
        };
        dictionarySplit.Children.Add(divider);
        dictionarySplit.SizeChanged += (_, _) => ArrangePanes();
        ArrangePanes();
        var keyboardScroll = new ScrollViewer { Content = keyboard, HorizontalScrollBarVisibility = ScrollBarVisibility.Auto, VerticalScrollBarVisibility = ScrollBarVisibility.Auto, MaxHeight = 240, HorizontalScrollMode = ScrollMode.Enabled };
        Grid.SetRow(keyboardScroll, 2); dictionaryPage.Children.Add(keyboardScroll);
        favoriteButton.Click += (_, _) => ToggleFavorite();
        ShowEntry();
    }

    private async Task LoadDictionaryAsync()
    {
        if (ready) return;
        try
        {
            await Task.Run(() => dictionary.LoadAsync(Path.Combine(AppContext.BaseDirectory, "Data", "bohairic_dictionary.json")));
            if (closed) return;
            posFilter.Items.Add("All parts of speech");
            foreach (var pos in dictionary.Entries.Select(x => x.PartOfSpeech).Where(x => !string.IsNullOrWhiteSpace(x)).Distinct().Order())
                posFilter.Items.Add(pos);
            letterFilter.Items.Clear();
            letterFilter.Items.Add("All letters");
            foreach (var letter in Alphabet)
            {
                int count = dictionary.Entries.Count(x => DictionaryService.Normalize(x.Word, true).StartsWith(letter, StringComparison.Ordinal));
                letterFilter.Items.Add(new ComboBoxItem { Content = $"{letter}  ·  {count:N0}", Tag = letter });
            }
            letterFilter.SelectedIndex = 0;
            posFilter.SelectedIndex = 0;
            ready = true;
            loading.IsActive = false; loading.Visibility = Visibility.Collapsed;
            QueueSearch();
            if (store.LoadWarning != null) Notify(store.LoadWarning, InfoBarSeverity.Warning);
        }
        catch (Exception ex)
        {
            loading.IsActive = false; loading.Visibility = Visibility.Collapsed;
            resultCount.Text = "Dictionary could not be loaded";
            Notify($"Unable to load the local dictionary. Keep the Data folder beside the app. {ex.Message}", InfoBarSeverity.Error);
        }
    }

    private async void QueueSearch()
    {
        if (!ready || closed) return;
        searchCancellation?.Cancel();
        var cancellation = new CancellationTokenSource();
        searchCancellation = cancellation;
        var options = new SearchOptions(search.Text,
            posFilter.SelectedIndex > 0 ? posFilter.SelectedItem?.ToString() ?? "" : "",
            currentPage == "Browse" && letterFilter.SelectedItem is ComboBoxItem selectedLetter ? selectedLetter.Tag?.ToString() ?? "" : "",
            preferences.IgnoreMarks, currentPage == "Favorites" ? new HashSet<string>(preferences.Favorites) : null, (SearchScope)Math.Max(0, searchScope.SelectedIndex));
        try
        {
            await Task.Delay(110, cancellation.Token);
            var found = await Task.Run(() => dictionary.Search(options, cancellation.Token), cancellation.Token);
            var spelling = found.Count == 0 ? await Task.Run(() => dictionary.Suggest(options, cancellation.Token), cancellation.Token) : [];
            if (closed || cancellation.IsCancellationRequested) return;
            var previousId = lastQuery == options.Query ? selected?.Id : null;
            lastQuery = options.Query;
            suggestions.Children.Clear();
            suggestions.Visibility = spelling.Count > 0 ? Visibility.Visible : Visibility.Collapsed;
            if (spelling.Count > 0) suggestions.Children.Add(Text("Did you mean?"));
            foreach (var suggestion in spelling) suggestions.Children.Add(Button(suggestion, (_, _) => search.Text = suggestion));
            currentResults = found;
            results.Items.Clear();
            shownCount = 0;
            ShowMore();
            var previous = found.Take(shownCount).FirstOrDefault(x => x.Id == previousId);
            if (previous != null) results.SelectedItem = previous;
            else if (results.Items.Count > 0) results.SelectedIndex = 0;
            else { selected = null; ShowEntry(); }
            resultCount.Text = $"{found.Count:N0} {(found.Count == 1 ? "entry" : "entries")}  ·  Bohairic only  ·  {dictionary.Entries.Count:N0} in dictionary";
            emptyResults.Text = currentPage == "Favorites" && preferences.Favorites.Count == 0
                ? "Your saved words will appear here.\nSelect an entry and choose Save word."
                : "No matching entries.\nTry another spelling or clear the filters.";
            emptyResults.Visibility = found.Count == 0 ? Visibility.Visible : Visibility.Collapsed;
        }
        catch (OperationCanceledException) { }
        catch (Exception ex) { Notify($"Search failed: {ex.Message}", InfoBarSeverity.Error); }
        finally
        {
            if (ReferenceEquals(searchCancellation, cancellation)) searchCancellation = null;
            cancellation.Dispose();
        }
    }

    private void ShowMore()
    {
        var end = Math.Min(shownCount + 100, currentResults.Count);
        for (int i = shownCount; i < end; i++) results.Items.Add(currentResults[i]);
        shownCount = end;
        moreButton.Content = $"Show more ({shownCount:N0} of {currentResults.Count:N0})";
        moreButton.Visibility = shownCount < currentResults.Count ? Visibility.Visible : Visibility.Collapsed;
    }

    private void ShowEntry()
    {
        saveCurrentNote?.Invoke();
        saveCurrentNote = null;
        // Detach the reusable button before rebuilding the details panel.
        if (favoriteButton.Parent is Panel oldParent) oldParent.Children.Remove(favoriteButton);
        details.Children.Clear();
        if (selected == null)
        {
            details.Children.Add(Text("ⲃⲟϩⲁⲓⲣⲓⲕⲟⲛ", 30));
            details.Children.Add(Text("A word opens a world.", 24));
            details.Children.Add(Text("Choose an entry to explore its meanings, variants, and references. Everything in this dictionary is available offline."));
            return;
        }
        var badge = Text("BOHAIRIC  /  DICTIONARY", 11); badge.Opacity = 0.65;
        details.Children.Add(badge);
        var word = Text(selected.Word, preferences.CopticTextSize + 12); word.FontFamily = copticFont;
        details.Children.Add(word);
        if (!string.IsNullOrWhiteSpace(selected.PartOfSpeech)) details.Children.Add(Text(selected.PartOfSpeech, 15));
        var actions = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        UpdateFavoriteButton(); actions.Children.Add(favoriteButton);
        actions.Children.Add(Button("Copy word", (_, _) => Copy(selected?.Word ?? "")));
        details.Children.Add(actions);
        AddSection("MEANINGS", selected.Senses.Select((sense, i) => $"{i + 1}.  {sense}"));
        foreach (var target in dictionary.GetCrossReferences(selected))
        {
            var label = Text("Open “" + target + "” →", preferences.EnglishTextSize);
            label.IsTextSelectionEnabled = false;
            var link = new HyperlinkButton { Content = label, HorizontalAlignment = HorizontalAlignment.Left };
            link.Click += (_, _) => { posFilter.SelectedIndex = 0; letterFilter.SelectedIndex = 0; searchScope.SelectedIndex = 1; search.Text = target; ShowPage("Dictionary"); };
            details.Children.Add(link);
        }
        details.Children.Add(Button("Copy definitions", (_, _) => Copy(selected == null ? "" : string.Join(Environment.NewLine, selected.Senses))));
        AddSection("VARIANTS", selected.Variants, true);
        AddSection("ETYMOLOGY", [selected.Etymology]);
        AddSection("GREEK ORIGIN", [selected.GreekOrigin]);
        AddSection("CRUM REFERENCE", [selected.CrumReference]);
        AddSection("GRAMMATICAL TAG", [selected.Tag]);
        foreach (var example in selected.Examples)
            AddSection("EXAMPLE", [example.Sentence, example.Translation, example.Source]);
        var references = new[] { selected.TlaId.Length > 0 ? $"TLA ID: {selected.TlaId}" : "", selected.EntryUrl };
        AddSection("SOURCE REFERENCE", references);
        details.Children.Add(Text("PERSONAL NOTE", 11));
        string entryId = selected.Id;
        preferences.Notes.TryGetValue(entryId, out string? savedNote);
        var note = new TextBox { Text = savedNote ?? "", PlaceholderText = "Add your own note about this word…", AcceptsReturn = true, TextWrapping = TextWrapping.Wrap, MinHeight = 80, MaxLength = 4000 };
        Name(note, "Personal note for " + selected.Word);
        saveCurrentNote = () =>
        {
            preferences.Notes.TryGetValue(entryId, out var previous);
            if ((previous ?? "") == note.Text) return;
            if (string.IsNullOrWhiteSpace(note.Text)) preferences.Notes.Remove(entryId); else preferences.Notes[entryId] = note.Text;
            SavePreferences();
        };
        var persistNote = saveCurrentNote;
        note.LostFocus += (_, _) => persistNote();
        details.Children.Add(note);
        details.Children.Add(new Border { Height = 1, Background = Brush("DividerStrokeColorDefaultBrush"), Margin = new Thickness(0, 10, 0, 0) });
        var source = Text("Source: your supplied Bohairic dictionary. Original spellings and definitions are preserved.", 12);
        source.Opacity = 0.6; details.Children.Add(source);
        detailPane.ChangeView(null, 0, null);
    }

    private void ResizePanes(double change)
    {
        splitRatio = Math.Clamp(splitRatio + change, 0.25, 0.7);
        ArrangePanes();
    }

    private void ArrangePanes()
    {
        bool narrow = dictionarySplit.ActualWidth is > 0 and < 650;
        if (narrowLayout != narrow || (dictionarySplit.ColumnDefinitions.Count == 0 && dictionarySplit.RowDefinitions.Count == 0))
        {
            narrowLayout = narrow;
            dictionarySplit.ColumnDefinitions.Clear(); dictionarySplit.RowDefinitions.Clear();
            foreach (var child in dictionarySplit.Children) { Grid.SetRow((FrameworkElement)child, 0); Grid.SetColumn((FrameworkElement)child, 0); }
            if (narrow)
            {
                dictionarySplit.RowDefinitions.Add(new RowDefinition());
                dictionarySplit.RowDefinitions.Add(new RowDefinition { Height = new GridLength(10) });
                dictionarySplit.RowDefinitions.Add(new RowDefinition());
                Grid.SetRow(divider, 1); Grid.SetRow(detailPane, 2);
            }
            else
            {
                dictionarySplit.ColumnDefinitions.Add(new ColumnDefinition());
                dictionarySplit.ColumnDefinitions.Add(new ColumnDefinition { Width = new GridLength(10) });
                dictionarySplit.ColumnDefinitions.Add(new ColumnDefinition());
                Grid.SetColumn(divider, 1); Grid.SetColumn(detailPane, 2);
            }
        }
        if (narrow)
        {
            dictionarySplit.RowDefinitions[0].Height = new GridLength(splitRatio, GridUnitType.Star);
            dictionarySplit.RowDefinitions[2].Height = new GridLength(1 - splitRatio, GridUnitType.Star);
        }
        else
        {
            dictionarySplit.ColumnDefinitions[0].Width = new GridLength(splitRatio, GridUnitType.Star);
            dictionarySplit.ColumnDefinitions[2].Width = new GridLength(1 - splitRatio, GridUnitType.Star);
        }
    }

    private void ApplyReadingSizes()
    {
        var xaml = FormattableString.Invariant($$"""
            <DataTemplate xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation" xmlns:local="using:Bohairic.Desktop">
              <StackPanel Padding="8,14" Spacing="8" UseLayoutRounding="True">
                <TextBlock Text="{Binding Word}" FontFamily="ms-appx:///Assets/FreeSerifAvvaShenouda.ttf#FreeSerifAvvaShenouda" FontSize="{{preferences.CopticTextSize}}" LineHeight="{{Math.Ceiling(preferences.CopticTextSize * 1.5)}}" LineStackingStrategy="MaxHeight" TextWrapping="Wrap"/>
                <TextBlock local:CopticTypography.Text="{Binding Preview}" FontFamily="Segoe UI" FontSize="{{preferences.EnglishTextSize}}" LineHeight="{{Math.Ceiling(preferences.EnglishTextSize * 1.5)}}" LineStackingStrategy="MaxHeight" Typography.StandardLigatures="False" TextWrapping="Wrap" MaxLines="3" TextTrimming="CharacterEllipsis"/>
                <TextBlock Text="{Binding Subtitle}" FontFamily="Segoe UI" FontSize="12" LineHeight="20" Opacity="0.7"/>
              </StackPanel>
            </DataTemplate>
            """);
        results.ItemTemplate = (DataTemplate)XamlReader.Load(xaml);
        if (selected != null) ShowEntry();
    }

    private void AddSection(string title, IEnumerable<string> values, bool coptic = false)
    {
        var lines = values.Where(x => !string.IsNullOrWhiteSpace(x)).ToArray();
        if (lines.Length == 0) return;
        var section = new StackPanel { Spacing = 9, Margin = new Thickness(0, 8, 0, 0) };
        var label = Text(title, 11); label.Opacity = 0.65;
        section.Children.Add(label);
        foreach (var line in lines)
        {
            var block = Text(line, coptic ? preferences.CopticTextSize : preferences.EnglishTextSize);
            if (coptic) block.FontFamily = copticFont;
            section.Children.Add(block);
        }
        details.Children.Add(section);
    }

    private void ShowPage(string page)
    {
        currentPage = page;
        var content = (Grid)navigation.Content;
        foreach (var scroll in content.Children.OfType<ScrollViewer>()) scroll.Visibility = page == "Settings" ? Visibility.Visible : Visibility.Collapsed;
        dictionaryPage.Visibility = page == "Settings" ? Visibility.Collapsed : Visibility.Visible;
        heading.Text = page;
        letterFilter.Visibility = page == "Browse" ? Visibility.Visible : Visibility.Collapsed;
        if (page != "Settings")
        {
            var item = navigation.MenuItems.OfType<NavigationViewItem>().FirstOrDefault(x => (string)x.Tag == page);
            if (item != null && !ReferenceEquals(navigation.SelectedItem, item)) navigation.SelectedItem = item;
            QueueSearch();
        }
        else BuildSettings();
    }

    private void ToggleFavorite()
    {
        if (selected == null) return;
        if (!preferences.Favorites.Add(selected.Id)) preferences.Favorites.Remove(selected.Id);
        SavePreferences(); UpdateFavoriteButton();
        if (currentPage == "Favorites") QueueSearch();
    }
    private void UpdateFavoriteButton() => favoriteButton.Content = selected != null && preferences.Favorites.Contains(selected.Id) ? "★ Saved" : "☆ Save word";
    private void Copy(string text)
    {
        try
        {
            var data = new DataPackage(); data.SetText(text); Clipboard.SetContent(data); Clipboard.Flush();
            Notify("Copied to clipboard.", InfoBarSeverity.Success);
        }
        catch (Exception ex) { Notify($"Could not copy: {ex.Message}", InfoBarSeverity.Warning); }
    }
    private void Notify(string message, InfoBarSeverity severity) { notice.Message = message; notice.Severity = severity; notice.IsOpen = true; }
    private void SavePreferences()
    {
        try { store.Save(preferences); }
        catch (Exception ex) { Notify($"Could not save preferences: {ex.Message}", InfoBarSeverity.Warning); }
    }
    private void RememberSearch()
    {
        var query = search.Text.Trim();
        if (query.Length == 0 || !ready) return;
        preferences.RecentSearches.RemoveAll(x => x.Equals(query, StringComparison.OrdinalIgnoreCase));
        preferences.RecentSearches.Insert(0, query);
        preferences.RecentSearches = preferences.RecentSearches.Take(12).ToList();
        SavePreferences();
    }

    private void ApplyTheme()
    {
        root.RequestedTheme = preferences.Theme switch { "Light" => ElementTheme.Light, "Dark" => ElementTheme.Dark, _ => ElementTheme.Default };
    }
    private void BuildSettings()
    {
        settingsPage.Children.Clear();
        settingsPage.Children.Add(Text("Settings", 32));
        settingsPage.Children.Add(Text("Make this dictionary your own.", 15));
        settingsPage.Children.Add(Text("APPEARANCE", 12));
        var theme = new ComboBox { Width = 220, ItemsSource = new[] { "System", "Light", "Dark" }, SelectedItem = preferences.Theme };
        Name(theme, "Color theme");
        theme.SelectionChanged += (_, _) => { preferences.Theme = theme.SelectedItem?.ToString() ?? "System"; ApplyTheme(); SavePreferences(); };
        settingsPage.Children.Add(theme);
        settingsPage.Children.Add(Text("READING SIZE", 12));
        var englishSize = new Slider { Minimum = 14, Maximum = 24, StepFrequency = 1, Value = preferences.EnglishTextSize, Width = 300, Header = "English text" };
        var copticSize = new Slider { Minimum = 24, Maximum = 42, StepFrequency = 1, Value = preferences.CopticTextSize, Width = 300, Header = "Coptic text" };
        var preview = Text("First letter of the alphabet", preferences.EnglishTextSize);
        var copticPreview = Text("ⲁ̄  ⲁⲅⲁⲡⲏ  ϣ", preferences.CopticTextSize);
        englishSize.ValueChanged += (_, args) => { preferences.EnglishTextSize = args.NewValue; preview.FontSize = args.NewValue; preview.LineHeight = Math.Ceiling(args.NewValue * 1.5); ApplyReadingSizes(); SavePreferences(); };
        copticSize.ValueChanged += (_, args) => { preferences.CopticTextSize = args.NewValue; copticPreview.FontSize = args.NewValue; copticPreview.LineHeight = Math.Ceiling(args.NewValue * 1.5); ApplyReadingSizes(); SavePreferences(); };
        settingsPage.Children.Add(englishSize); settingsPage.Children.Add(preview);
        settingsPage.Children.Add(copticSize); settingsPage.Children.Add(copticPreview);
        var marks = new ToggleSwitch { Header = "Search without combining marks", IsOn = preferences.IgnoreMarks, OnContent = "Tolerant search", OffContent = "Exact marks" };
        marks.Toggled += (_, _) => { preferences.IgnoreMarks = marks.IsOn; SavePreferences(); QueueSearch(); };
        settingsPage.Children.Add(marks);
        settingsPage.Children.Add(Text("Original spelling always stays visible. Exact matches appear before matches with different marks.", 13));
        settingsPage.Children.Add(Text("RECENT SEARCHES", 12));
        if (preferences.RecentSearches.Count == 0) settingsPage.Children.Add(Text("Search for a word and press Enter to keep it here.", 13));
        foreach (var query in preferences.RecentSearches.ToArray())
            settingsPage.Children.Add(Button(query, (_, _) => { search.Text = query; ShowPage("Dictionary"); }));
        if (preferences.RecentSearches.Count > 0)
            settingsPage.Children.Add(Button("Clear recent searches", (_, _) => { preferences.RecentSearches.Clear(); SavePreferences(); BuildSettings(); }));
        settingsPage.Children.Add(Text("ABOUT THIS DICTIONARY", 12));
        settingsPage.Children.Add(Text("Bohairic Dictionary 1.1\nNative Windows app · works offline\nOnly the supplied Bohairic dataset is included. No Sahidic or combined lexicon is loaded."));
        settingsPage.Children.Add(Text("Favorites and preferences are stored on this computer:\n" + store.DirectoryPath, 12));
        settingsPage.Children.Add(Text("Coptic font: Free Serif Avva Shenouda, from your Coptic repository. Font attribution and embedded license information are included in Assets/FONT-NOTICE.txt.", 12));
    }

    private void BuildKeyboard()
    {
        keyboard.Children.Clear();
        keyboard.Visibility = preferences.ShowKeyboard ? Visibility.Visible : Visibility.Collapsed;
        var top = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8 };
        top.Children.Add(Text("Coptic keyboard", 12));
        top.Children.Add(Button(symbolKeys ? "Letters" : "Marks & numbers", (_, _) => { symbolKeys = !symbolKeys; BuildKeyboard(); }));
        top.Children.Add(Button(shifted ? "Lowercase" : "Uppercase", (_, _) => { shifted = !shifted; BuildKeyboard(); }));
        top.Children.Add(Button("Hide", (_, _) => { preferences.ShowKeyboard = false; BuildKeyboard(); SavePreferences(); }));
        keyboard.Children.Add(top);
        string[][] rows = symbolKeys
            ? [["ⲁ̅", "ⲃ̅", "ⲅ̅", "ⲇ̅", "ⲉ̅", "ⲋ̅", "ⲍ̅", "ⲏ̅", "ⲑ̅", "ⲓ̅"], ["̀", "́", "̄", "̅", "̈", "̂", "̣", "︤", "︥", "⳾"]]
            : [["ϧ", "ⲱ", "ⲉ", "ⲣ", "ⲧ", "ⲏ", "ⲩ", "ⲓ", "ⲟ", "ⲡ"], ["ⲁ", "ϣ", "ⲇ", "ⲫ", "ⲅ", "ϩ", "ϫ", "ⲕ", "ⲗ", "ⲑ"], ["ⲍ", "ⲝ", "ⲥ", "ϥ", "ⲃ", "ⲛ", "ⲙ", "ⲭ", "ⲯ", "ϭ", "ϯ"]];
        foreach (var row in rows)
        {
            var panel = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 5, HorizontalAlignment = HorizontalAlignment.Center };
            foreach (var letter in row)
            {
                var value = shifted ? letter.ToUpperInvariant() : letter;
                var key = Button(value, (_, _) => InsertText(value));
                key.FontFamily = copticFont; key.FontSize = 20; key.MinWidth = 39; key.Padding = new Thickness(6, 3, 6, 3);
                key.AllowFocusOnInteraction = false;
                Name(key, "Insert " + value);
                panel.Children.Add(key);
            }
            keyboard.Children.Add(panel);
        }
        var bottom = new StackPanel { Orientation = Orientation.Horizontal, Spacing = 8, HorizontalAlignment = HorizontalAlignment.Center };
        var space = Button("Space", (_, _) => InsertText(" ")); space.Width = 170; space.AllowFocusOnInteraction = false;
        bottom.Children.Add(space);
        var backspace = Button("⌫", (_, _) =>
        {
            int start = search.SelectionStart;
            int length = search.SelectionLength;
            if (length == 0 && start > 0)
            {
                // Remove one Unicode text element (base letter plus combining marks).
                var boundaries = System.Globalization.StringInfo.ParseCombiningCharacters(search.Text[..start]);
                int previous = boundaries.Length > 0 ? boundaries[^1] : 0;
                length = start - previous; start = previous;
            }
            if (length > 0) { search.Text = search.Text.Remove(start, length); search.SelectionStart = start; }
            search.Focus(FocusState.Programmatic);
        });
        backspace.AllowFocusOnInteraction = false; Name(backspace, "Backspace"); bottom.Children.Add(backspace);
        keyboard.Children.Add(bottom);
    }
    private void InsertText(string text)
    {
        int start = search.SelectionStart;
        search.Text = search.Text.Remove(start, search.SelectionLength).Insert(start, text);
        search.SelectionStart = start + text.Length;
        search.SelectionLength = 0;
        search.Focus(FocusState.Programmatic);
    }
}
