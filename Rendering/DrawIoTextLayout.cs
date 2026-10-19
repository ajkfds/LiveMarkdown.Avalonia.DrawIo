using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Helpers for laying out draw.io labels: word wrapping (<c>whiteSpace=wrap</c>)
/// and font-style resolution. Runs headlessly (no platform render interface is
/// needed beyond what <see cref="FormattedText"/> already requires).
/// </summary>
internal static class DrawIoTextLayout
{
    /// <summary>
    /// Wraps <paramref name="text"/> so each resulting line fits within
    /// <paramref name="maxWidth"/>. Respects explicit <c>\n</c> line breaks and
    /// breaks on whitespace, falling back to a hard break for very long words.
    /// </summary>
    public static List<string> Wrap(
        string text,
        double maxWidth,
        Typeface typeface,
        double fontSize)
    {
        var result = new List<string>();
        if (maxWidth <= 0 || double.IsInfinity(maxWidth))
        {
            result.AddRange(text.Split('\n'));
            return result;
        }

        foreach (var paragraph in text.Split('\n'))
        {
            WrapParagraph(paragraph, maxWidth, typeface, fontSize, result);
        }

        if (result.Count == 0)
        {
            result.Add(string.Empty);
        }

        return result;
    }

    private static void WrapParagraph(
        string paragraph,
        double maxWidth,
        Typeface typeface,
        double fontSize,
        List<string> output)
    {
        if (paragraph.Length == 0)
        {
            output.Add(string.Empty);
            return;
        }

        if (Measure(paragraph, typeface, fontSize) <= maxWidth)
        {
            output.Add(paragraph);
            return;
        }

        var words = paragraph.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        var current = string.Empty;

        foreach (var word in words)
        {
            var candidate = current.Length == 0 ? word : current + " " + word;
            if (Measure(candidate, typeface, fontSize) <= maxWidth)
            {
                current = candidate;
                continue;
            }

            if (current.Length > 0)
            {
                output.Add(current);
                current = string.Empty;
            }

            // The word alone fits (or is broken into chunks).
            if (Measure(word, typeface, fontSize) <= maxWidth)
            {
                current = word;
                continue;
            }

            var (chunks, last) = BreakLongWord(word, maxWidth, typeface, fontSize);
            output.AddRange(chunks);
            current = last;
        }

        if (current.Length > 0)
        {
            output.Add(current);
        }
    }

    /// <summary>
    /// Splits an over-long word into chunks that each fit within
    /// <paramref name="maxWidth"/>; the trailing chunk is returned separately so
    /// it can be joined with the following word.
    /// </summary>
    private static (List<string> Chunks, string Remainder) BreakLongWord(
        string word, double maxWidth, Typeface typeface, double fontSize)
    {
        var chunks = new List<string>();
        var start = 0;
        var end = 1;

        while (end <= word.Length)
        {
            var candidate = word[start..end];
            if (Measure(candidate, typeface, fontSize) > maxWidth && end - start > 1)
            {
                chunks.Add(word[start..(end - 1)]);
                start = end - 1;
            }

            end++;
        }

        var remainder = start < word.Length ? word[start..] : string.Empty;
        return (chunks, remainder);
    }

    /// <summary>Measures the rendered width of a text run.</summary>
    public static double Measure(string text, Typeface typeface, double fontSize)
    {
        if (text.Length == 0)
        {
            return 0;
        }

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            fontSize,
            Brushes.Black);
        return formatted.Width;
    }

    /// <summary>
    /// Resolves the font style bits draw.io stores in <c>fontStyle</c>:
    /// 1 = bold, 2 = italic, 4 = underline.
    /// </summary>
    public static (bool Bold, bool Italic, bool Underline) ResolveFontStyle(
        DrawIoStyle style, bool fallbackBold)
    {
        var raw = style.Get("fontStyle");
        if (raw is { Length: > 0 } && int.TryParse(raw, out var bits))
        {
            return ((bits & 1) != 0, (bits & 2) != 0, (bits & 4) != 0);
        }

        if (style.GetBool("bold"))
        {
            return (true, false, false);
        }

        return (fallbackBold, false, false);
    }
}
