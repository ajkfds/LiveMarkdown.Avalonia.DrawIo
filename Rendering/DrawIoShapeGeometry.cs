using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Builds Avalonia geometry for the draw.io shape vocabulary supported by the
/// presenter: rectangles, rounded rectangles, ellipses, and generic polygons
/// (<c>mxgraph.basic.polygon</c> / the <c>polyCoords</c> style property).
/// </summary>
internal static class DrawIoShapeGeometry
{
    /// <summary>Builds a fill/stroke geometry for a vertex cell.</summary>
    public static Geometry? Build(DrawIoCell cell, Rect bounds, DrawIoStyle style)
    {
        if (bounds.Width <= 0 && bounds.Height <= 0)
        {
            return null;
        }

        if (style.IsEllipse)
        {
            return new EllipseGeometry(bounds);
        }

        var shape = style.Shape;
        if (shape is not null &&
            (shape.Contains("polygon", StringComparison.OrdinalIgnoreCase) ||
             shape.Contains("triangle", StringComparison.OrdinalIgnoreCase) ||
             shape.Contains("hexagon", StringComparison.OrdinalIgnoreCase)))
        {
            var polygon = BuildPolygon(cell, bounds, style, shape);
            if (polygon is not null)
            {
                return polygon;
            }
        }

        if (style.GetBool("rounded"))
        {
            var arcSize = style.GetDouble("arcSize", -1);
            var radius = arcSize >= 0
                ? arcSize
                : Math.Min(bounds.Width, bounds.Height) * 0.15;
            radius = Math.Min(radius, Math.Min(bounds.Width, bounds.Height) / 2);
            return new RectangleGeometry(bounds, radius, radius);
        }

        return new RectangleGeometry(bounds);
    }

    private static Geometry? BuildPolygon(
        DrawIoCell cell, Rect bounds, DrawIoStyle style, string shape)
    {
        var coords = ParsePolyCoords(style.Get("polyCoords"));
        var curvature = ParsePolyCurves(style.Get("polyCurves"));
        var isPolyline = style.GetBool("polyline");

        if (coords.Count < 2)
        {
            return shape switch
            {
                var s when s.Contains("triangle", StringComparison.OrdinalIgnoreCase) => BuildTriangle(bounds, style),
                _ => new RectangleGeometry(bounds)
            };
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var start = Map(coords[0], bounds);
            ctx.BeginFigure(start, !isPolyline);

            for (var i = 1; i < coords.Count; i++)
            {
                var point = Map(coords[i], bounds);
                var curve = i - 1 < curvature.Count ? curvature[i - 1] : null;
                if (curve is { Count: >= 3 } && curve[0] == "Q")
                {
                    var control = new Point(
                        bounds.X + ParseDouble(curve[1]) * bounds.Width,
                        bounds.Y + ParseDouble(curve[2]) * bounds.Height);
                    ctx.QuadraticBezierTo(control, point);
                }
                else
                {
                    ctx.LineTo(point);
                }
            }

            if (!isPolyline)
            {
                var closeCurve = curvature.Count >= coords.Count ? curvature[coords.Count - 1] : null;
                if (closeCurve is { Count: >= 3 } && closeCurve[0] == "Q")
                {
                    var control = new Point(
                        bounds.X + ParseDouble(closeCurve[1]) * bounds.Width,
                        bounds.Y + ParseDouble(closeCurve[2]) * bounds.Height);
                    ctx.QuadraticBezierTo(control, start);
                }
            }

            ctx.EndFigure(!isPolyline);
        }

        return geometry;
    }

    private static Geometry BuildTriangle(Rect bounds, DrawIoStyle style)
    {
        var direction = style.Get("direction") ?? "east";
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();

        Point a, b, c;
        switch (direction.ToLowerInvariant())
        {
            case "north":
                a = new Point(bounds.Center.X, bounds.Y);
                b = new Point(bounds.Right, bounds.Bottom);
                c = new Point(bounds.X, bounds.Bottom);
                break;
            case "south":
                a = new Point(bounds.X, bounds.Y);
                b = new Point(bounds.Right, bounds.Y);
                c = new Point(bounds.Center.X, bounds.Bottom);
                break;
            case "west":
                a = new Point(bounds.X, bounds.Center.Y);
                b = new Point(bounds.Right, bounds.Y);
                c = new Point(bounds.Right, bounds.Bottom);
                break;
            default:
                a = new Point(bounds.Right, bounds.Center.Y);
                b = new Point(bounds.X, bounds.Y);
                c = new Point(bounds.X, bounds.Bottom);
                break;
        }

        ctx.BeginFigure(a, true);
        ctx.LineTo(b);
        ctx.LineTo(c);
        ctx.EndFigure(true);
        return geometry;
    }

    private static Point Map(IReadOnlyList<double> coord, Rect bounds) =>
        new(bounds.X + coord[0] * bounds.Width, bounds.Y + coord[1] * bounds.Height);

    private static List<List<double>> ParsePolyCoords(string? value)
    {
        var result = new List<List<double>>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        // Format: [[x,y],[x,y],...]
        var inner = value.Trim();
        var depth = 0;
        var current = new List<double>();
        var numberStart = -1;

        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            switch (c)
            {
                case '[':
                    depth++;
                    if (depth == 2)
                    {
                        current = [];
                    }

                    break;
                case ']':
                    if (depth == 2 && current.Count >= 2)
                    {
                        result.Add(current);
                    }

                    depth--;
                    break;
                default:
                    if (depth == 2 && (char.IsDigit(c) || c is '.' or '-' or '+' or 'e' or 'E'))
                    {
                        if (numberStart < 0)
                        {
                            numberStart = i;
                        }
                    }
                    else if (numberStart >= 0)
                    {
                        AppendNumber(current, inner[numberStart..i]);
                        numberStart = -1;
                    }

                    break;
            }

            if (depth == 2 && i == inner.Length - 1 && numberStart >= 0)
            {
                AppendNumber(current, inner[numberStart..(i + 1)]);
            }
        }

        return result;
    }

    private static void AppendNumber(List<double> target, string token)
    {
        if (double.TryParse(token, NumberStyles.Float, CultureInfo.InvariantCulture, out var d))
        {
            target.Add(d);
        }
    }

    private static List<List<string>> ParsePolyCurves(string? value)
    {
        var result = new List<List<string>>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

        var inner = value.Trim();
        var depth = 0;
        var current = new List<string>();
        var tokenStart = -1;

        for (var i = 0; i < inner.Length; i++)
        {
            var c = inner[i];
            switch (c)
            {
                case '[':
                    depth++;
                    if (depth == 2)
                    {
                        current = [];
                    }

                    break;
                case ']':
                    if (depth == 2 && current.Count > 0)
                    {
                        result.Add(current);
                    }

                    depth--;
                    break;
                case ',':
                    if (depth == 2 && tokenStart >= 0)
                    {
                        current.Add(inner[tokenStart..i].Trim().Trim('"', '\''));
                        tokenStart = -1;
                    }

                    break;
                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    if (depth == 2 && tokenStart >= 0)
                    {
                        current.Add(inner[tokenStart..i].Trim().Trim('"', '\''));
                        tokenStart = -1;
                    }

                    break;
                default:
                    if (depth == 2 && tokenStart < 0)
                    {
                        tokenStart = i;
                    }

                    break;
            }

            if (depth == 2 && i == inner.Length - 1 && tokenStart >= 0)
            {
                current.Add(inner[tokenStart..(i + 1)].Trim().Trim('"', '\''));
            }
        }

        return result;
    }

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}
