using System.Globalization;

namespace LiveMarkdown.Avalonia;

/// <summary>Point in diagram coordinates.</summary>
public readonly record struct DrawIoPoint(double X, double Y);

/// <summary>A parsed draw.io style string (key=value pairs separated by <c>;</c>).</summary>
public sealed class DrawIoStyle
{
    private readonly Dictionary<string, string> _values = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Parses a draw.io style string. A <c>null</c> style yields an empty instance.</summary>
    public static DrawIoStyle Parse(string? style)
    {
        var result = new DrawIoStyle();
        if (string.IsNullOrEmpty(style))
        {
            return result;
        }

        foreach (var token in style.Split(';', StringSplitOptions.RemoveEmptyEntries))
        {
            var eq = token.IndexOf('=');
            if (eq < 0)
            {
                // Flag-style token (e.g. "text", "ellipse", "html").
                result._values[token.Trim()] = "1";
                continue;
            }

            var key = token[..eq].Trim();
            var value = token[(eq + 1)..].Trim();
            if (key.Length > 0)
            {
                result._values[key] = value;
            }
        }

        return result;
    }

    /// <summary>Returns the raw string for a style key, or <c>null</c> when absent.</summary>
    public string? Get(string key) => _values.TryGetValue(key, out var v) ? v : null;

    /// <summary>Returns a boolean style flag (<c>1</c>/<c>true</c>).</summary>
    public bool GetBool(string key, bool fallback = false)
    {
        if (!_values.TryGetValue(key, out var v))
        {
            return fallback;
        }

        return v is "1" || string.Equals(v, "true", StringComparison.OrdinalIgnoreCase);
    }

    /// <summary>Returns a double style value, or <paramref name="fallback"/> when absent or invalid.</summary>
    public double GetDouble(string key, double fallback)
    {
        if (_values.TryGetValue(key, out var v) &&
            double.TryParse(v, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        return fallback;
    }

    /// <summary>True when the shape is an ellipse (<c>ellipse</c> flag or shape id).</summary>
    public bool IsEllipse =>
        GetBool("ellipse") ||
        (Get("shape")?.Contains("ellipse", StringComparison.OrdinalIgnoreCase) ?? false);

    /// <summary>The shape id from the <c>shape=</c> key, or <c>null</c>.</summary>
    public string? Shape => Get("shape");
}

/// <summary>Geometry of a draw.io cell.</summary>
public sealed class DrawIoGeometry
{
    /// <summary>X coordinate (relative to the parent when <see cref="Relative"/>).</summary>
    public double X { get; set; }

    /// <summary>Y coordinate (relative to the parent when <see cref="Relative"/>).</summary>
    public double Y { get; set; }

    /// <summary>Width. May be unset (<c>NaN</c>) for text/label cells.</summary>
    public double Width { get; set; } = double.NaN;

    /// <summary>Height. May be unset (<c>NaN</c>) for text/label cells.</summary>
    public double Height { get; set; } = double.NaN;

    /// <summary>True when coordinates are relative to the parent vertex.</summary>
    public bool Relative { get; set; }

    /// <summary>Optional routing waypoints (edge geometry <c>Array</c>).</summary>
    public List<DrawIoPoint> Waypoints { get; } = [];

    /// <summary>Edge label offset relative to the edge midpoint.</summary>
    public DrawIoPoint? Offset { get; set; }
}

/// <summary>A parsed draw.io cell (vertex, edge, or label).</summary>
public sealed class DrawIoCell
{
    /// <summary>Cell id.</summary>
    public required string Id { get; init; }

    /// <summary>Raw label value (may contain HTML markup and <c>%Placeholder%</c> tokens).</summary>
    public string Value { get; set; } = string.Empty;

    /// <summary>Parsed style.</summary>
    public DrawIoStyle Style { get; set; } = DrawIoStyle.Parse(null);

    /// <summary>Raw style string.</summary>
    public string? RawStyle { get; set; }

    /// <summary>Parent cell id.</summary>
    public string? Parent { get; init; }

    /// <summary>True for vertex cells.</summary>
    public bool IsVertex { get; init; }

    /// <summary>True for edge cells.</summary>
    public bool IsEdge { get; init; }

    /// <summary>Source cell id for edges.</summary>
    public string? Source { get; init; }

    /// <summary>Target cell id for edges.</summary>
    public string? Target { get; init; }

    /// <summary>Geometry, or <c>null</c> for structural cells.</summary>
    public DrawIoGeometry? Geometry { get; set; }

    /// <summary>Custom attributes carried by a wrapping <c>object</c> element.</summary>
    public Dictionary<string, string> Attributes { get; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Child cell ids (in document order).</summary>
    public List<string> Children { get; } = [];

    /// <summary>Resolved absolute x coordinate (filled in during layout).</summary>
    public double AbsoluteX { get; set; }

    /// <summary>Resolved absolute y coordinate (filled in during layout).</summary>
    public double AbsoluteY { get; set; }

    /// <summary>Width used for rendering. Falls back to a sensible default when unset.</summary>
    public double Width => double.IsNaN(Geometry?.Width ?? double.NaN) ? 0 : Geometry!.Width;

    /// <summary>Height used for rendering. Falls back to a sensible default when unset.</summary>
    public double Height => double.IsNaN(Geometry?.Height ?? double.NaN) ? 0 : Geometry!.Height;
}

/// <summary>A single draw.io page (<c>diagram</c> element).</summary>
public sealed class DrawIoDiagram
{
    /// <summary>Diagram (page) name.</summary>
    public string? Name { get; set; }

    /// <summary>Diagram id.</summary>
    public string? Id { get; set; }

    /// <summary>Graph model background color, or <c>null</c> for transparent/none.</summary>
    public string? Background { get; set; }

    /// <summary>Declared page width (used for scaling when a page attribute is present).</summary>
    public double PageWidth { get; set; }

    /// <summary>Declared page height.</summary>
    public double PageHeight { get; set; }

    /// <summary>All parsed cells by id.</summary>
    public required Dictionary<string, DrawIoCell> Cells { get; init; }

    /// <summary>The root cell id (typically <c>"0"</c>).</summary>
    public required string RootId { get; init; }

    /// <summary>The default layer cell id (typically <c>"1"</c>).</summary>
    public required string LayerId { get; init; }

    /// <summary>Cells in document order (excluding the root and default layer).</summary>
    public required List<DrawIoCell> OrderedCells { get; init; }
}

/// <summary>A parsed draw.io file (<c>mxfile</c>), which may contain multiple pages.</summary>
public sealed class DrawIoFile
{
    /// <summary>The parsed diagrams (pages).</summary>
    public required List<DrawIoDiagram> Diagrams { get; init; }
}
