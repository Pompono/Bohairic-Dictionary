using Bohairic.Core;
using System.Globalization;
using System.Text;
using Microsoft.UI.Xaml;
using Microsoft.UI.Xaml.Controls;
using Microsoft.UI.Xaml.Documents;
using Microsoft.UI.Xaml.Media;

namespace Bohairic.Desktop;

/// <summary>Uses the repository font only for Coptic text elements, including their combining marks.</summary>
public sealed class CopticTypography : DependencyObject
{
    public static FontFamily Font { get; } = new("ms-appx:///Assets/FreeSerifAvvaShenouda.ttf#FreeSerifAvvaShenouda");
    public static FontFamily InterfaceFont { get; } = new("Segoe UI");
    public static readonly DependencyProperty TextProperty = DependencyProperty.RegisterAttached(
        "Text", typeof(string), typeof(CopticTypography), new PropertyMetadata("", OnTextChanged));

    public static readonly DependencyProperty HighlightedProperty = DependencyProperty.RegisterAttached(
        "Highlighted", typeof(HighlightedText), typeof(CopticTypography), new PropertyMetadata(null, OnHighlightedChanged));
    public static HighlightedText? GetHighlighted(DependencyObject element) => (HighlightedText?)element.GetValue(HighlightedProperty);
    public static void SetHighlighted(DependencyObject element, HighlightedText value) => element.SetValue(HighlightedProperty, value);
    private static void OnHighlightedChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBlock block) return;
        var value = args.NewValue as HighlightedText ?? new HighlightedText("", []);
        block.Visibility = value.Text.Length == 0 ? Visibility.Collapsed : Visibility.Visible;
        SetText(block, value.Text);
        block.TextHighlighters.Clear();
        if (value.Ranges.Count == 0) return;
        var highlighter = new TextHighlighter
        {
            Background = new SolidColorBrush(Microsoft.UI.Colors.Gold),
            Foreground = new SolidColorBrush(Microsoft.UI.Colors.Black)
        };
        foreach (var range in value.Ranges)
            highlighter.Ranges.Add(new TextRange { StartIndex = range.Start, Length = range.Length });
        block.TextHighlighters.Add(highlighter);
    }

    public static string GetText(DependencyObject element) => (string)element.GetValue(TextProperty);
    public static void SetText(DependencyObject element, string value) => element.SetValue(TextProperty, value);
    public static bool ContainsCoptic(string value) => value.Any(c => c is >= '\u2C80' and <= '\u2CFF' or >= '\u03E2' and <= '\u03EF');

    private static void OnTextChanged(DependencyObject element, DependencyPropertyChangedEventArgs args)
    {
        if (element is not TextBlock block) return;
        block.Inlines.Clear();
        var elements = StringInfo.GetTextElementEnumerator(args.NewValue as string ?? "");
        var buffer = new StringBuilder();
        bool currentCoptic = false;
        void Flush()
        {
            if (buffer.Length == 0) return;
            var run = new Run
            {
                Text = buffer.ToString(),
                FontFamily = currentCoptic ? Font : InterfaceFont
            };
            if (!currentCoptic) Typography.SetStandardLigatures(run, false);
            block.Inlines.Add(run);
            buffer.Clear();
        }
        while (elements.MoveNext())
        {
            var text = elements.GetTextElement();
            bool coptic = ContainsCoptic(text);
            if (coptic != currentCoptic) { Flush(); currentCoptic = coptic; }
            buffer.Append(text);
        }
        Flush();
    }
}
