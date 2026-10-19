using System.Text;
using System.Xml.Linq;

namespace LiveMarkdown.Avalonia;

/// <summary>A styled run of text extracted from a draw.io label.</summary>
public sealed class DrawIoTextSpan
{
    /// <summary>The (decoded) plain text.</summary>
    public required string Text { get; init; }

    /// <summary>Optional foreground color (CSS color string).</summary>
    public string? Foreground { get; init; }

    /// <summary>True when the run is bold.</summary>
    public bool Bold { get; init; }

    /// <summary>True when the run is italic.</summary>
    public bool Italic { get; init; }
}

/// <summary>
/// Converts draw.io HTML label values (with <c>&lt;span&gt;</c>/<c>&lt;b&gt;</c>/
/// <c>&lt;br&gt;</c> markup and <c>%Placeholder%</c> tokens) into plain text and
/// styled runs that the presenter can render.
/// </summary>
internal static class DrawIoLabelParser
{
    /// <summary>
    /// Parses a label value, substituting <c>%Key%</c> placeholders from the
    /// provided attributes, and returns the plain text plus styled spans.
    /// </summary>
    public static (string Text, List<DrawIoTextSpan> Spans) Parse(
        string? value, IReadOnlyDictionary<string, string> attributes)
    {
        if (string.IsNullOrEmpty(value))
        {
            return (string.Empty, []);
        }

        var substituted = SubstitutePlaceholders(value, attributes);
        var normalized = NormalizeMarkup(substituted);
        var text = ExtractPlainText(normalized);
        var spans = ExtractSpans(normalized);
        return (text, spans);
    }

    /// <summary>Replaces <c>%Name%</c> style placeholders with attribute values.</summary>
    public static string SubstitutePlaceholders(
        string value, IReadOnlyDictionary<string, string> attributes)
    {
        if (attributes.Count == 0 || !value.Contains('%'))
        {
            return value;
        }

        var builder = new StringBuilder(value.Length);
        var i = 0;
        while (i < value.Length)
        {
            if (value[i] == '%')
            {
                var end = value.IndexOf('%', i + 1);
                if (end > i + 1)
                {
                    var key = value[(i + 1)..end];
                    if (attributes.TryGetValue(key, out var replacement))
                    {
                        builder.Append(replacement);
                        i = end + 1;
                        continue;
                    }
                }
            }

            builder.Append(value[i]);
            i++;
        }

        return builder.ToString();
    }

    /// <summary>
    /// Normalizes draw.io label markup into well-formed XML so it can be parsed,
    /// decoding HTML entities and closing void elements like <c>&lt;br&gt;</c>.
    /// </summary>
    private static string NormalizeMarkup(string value)
    {
        var decoded = System.Net.WebUtility.HtmlDecode(value);

        // Wrap in a root element and self-close common void tags.
        decoded = decoded
            .Replace("<br>", "<br/>", StringComparison.OrdinalIgnoreCase)
            .Replace("<br />", "<br/>", StringComparison.OrdinalIgnoreCase)
            .Replace("<hr>", "<hr/>", StringComparison.OrdinalIgnoreCase)
            .Replace("<hr />", "<hr/>", StringComparison.OrdinalIgnoreCase);

        return "<root>" + decoded + "</root>";
    }

    private static string ExtractPlainText(string normalizedMarkup)
    {
        try
        {
            var root = XElement.Parse(normalizedMarkup, LoadOptions.PreserveWhitespace);
            var builder = new StringBuilder();
            Walk(root, builder);
            return builder.ToString().Trim();
        }
        catch
        {
            // Not well-formed markup: fall back to stripping tags manually.
            return StripTags(normalizedMarkup);
        }
    }

    private static void Walk(XElement element, StringBuilder builder)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                    builder.Append(text.Value);
                    break;
                case XElement child:
                    if (child.Name.LocalName is "br" or "hr")
                    {
                        builder.Append('\n');
                    }
                    else if (child.Name.LocalName is "div" or "p")
                    {
                        if (builder.Length > 0 && builder[^1] != '\n')
                        {
                            builder.Append('\n');
                        }

                        Walk(child, builder);
                        if (builder.Length > 0 && builder[^1] != '\n')
                        {
                            builder.Append('\n');
                        }
                    }
                    else
                    {
                        Walk(child, builder);
                    }

                    break;
            }
        }
    }

    private static string StripTags(string value)
    {
        var builder = new StringBuilder(value.Length);
        var inTag = false;
        foreach (var c in value)
        {
            if (c == '<')
            {
                inTag = true;
            }
            else if (c == '>')
            {
                inTag = false;
            }
            else if (!inTag)
            {
                builder.Append(c);
            }
        }

        return builder.ToString().Trim();
    }

    private static List<DrawIoTextSpan> ExtractSpans(string normalizedMarkup)
    {
        var spans = new List<DrawIoTextSpan>();
        try
        {
            var root = XElement.Parse(normalizedMarkup, LoadOptions.PreserveWhitespace);
            WalkSpans(root, spans, inheritBold: false, inheritItalic: false, inheritColor: null);
        }
        catch
        {
            // Ignore; plain text fallback is handled by the caller.
        }

        return spans;
    }

    private static void WalkSpans(
        XElement element,
        List<DrawIoTextSpan> spans,
        bool inheritBold,
        bool inheritItalic,
        string? inheritColor)
    {
        foreach (var node in element.Nodes())
        {
            switch (node)
            {
                case XText text:
                {
                    if (text.Value.Length > 0)
                    {
                        spans.Add(new DrawIoTextSpan
                        {
                            Text = text.Value,
                            Bold = inheritBold,
                            Italic = inheritItalic,
                            Foreground = inheritColor
                        });
                    }

                    break;
                }
                case XElement child:
                {
                    var name = child.Name.LocalName;
                    if (name is "br" or "hr")
                    {
                        spans.Add(new DrawIoTextSpan { Text = "\n" });
                        break;
                    }

                    var bold = inheritBold || name is "b" or "strong";
                    var italic = inheritItalic || name is "i" or "em";
                    var color = ResolveColor(child.Attribute("style")?.Value) ?? inheritColor;

                    if (name is "b" or "strong")
                    {
                        bold = true;
                    }

                    if (name is "i" or "em")
                    {
                        italic = true;
                    }

                    WalkSpans(child, spans, bold, italic, color);
                    break;
                }
            }
        }
    }

    private static string? ResolveColor(string? style)
    {
        if (string.IsNullOrEmpty(style))
        {
            return null;
        }

        foreach (var token in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var idx = token.IndexOf(':');
            if (idx <= 0)
            {
                continue;
            }

            var key = token[..idx].Trim();
            if (key.Equals("color", StringComparison.OrdinalIgnoreCase))
            {
                return token[(idx + 1)..].Trim();
            }
        }

        return null;
    }
}
