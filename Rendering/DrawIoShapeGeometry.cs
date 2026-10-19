using System.Globalization;
using Avalonia;
using Avalonia.Media;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Builds Avalonia geometry for the draw.io shape vocabulary supported by the
/// presenter: rectangles, rounded rectangles, ellipses, and a range of basic
/// preset shapes (rhombus, hexagon, parallelogram, trapezoid, step, cylinder,
/// cloud, actor, document, and more), plus generic polygons
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

        var shape = style.Shape ?? string.Empty;

        // Generic polygon: mxgraph.basic.polygon with explicit coordinates.
        if (shape.Contains("polygon", StringComparison.OrdinalIgnoreCase))
        {
            var polygon = BuildPolygon(cell, bounds, style, shape);
            if (polygon is not null)
            {
                return polygon;
            }
        }

        // An explicit shape id takes precedence over the ellipse flag: draw.io
        // writes e.g. "ellipse;shape=cloud", where the shape wins.
        var hasExplicitShape = !string.IsNullOrEmpty(shape) &&
                               !shape.Equals("ellipse", StringComparison.OrdinalIgnoreCase);

        var preset = MatchPreset(shape, style);
        if (preset is not null)
        {
            // "rectangle" is the fallback for unknown shapes; keep the ellipse
            // flag meaningful in that case.
            if (hasExplicitShape)
            {
                return preset(bounds, style);
            }
        }

        if (style.IsEllipse)
        {
            return new EllipseGeometry(bounds);
        }

        if (preset is not null)
        {
            return preset(bounds, style);
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

    /// <summary>
    /// Maps a style to a preset shape builder. Both the <c>shape=</c> key and
    /// the flag keys (e.g. <c>rhombus</c>, <c>triangle</c>, <c>ellipse</c>)
    /// used by draw.io are recognised.
    /// </summary>
    private static Func<Rect, DrawIoStyle, Geometry>? MatchPreset(string shape, DrawIoStyle style)
    {
        // Shape id (may be prefixed, e.g. "mxgraph.flowchart.decision").
        foreach (var token in EnumerateShapeTokens(shape))
        {
            if (Presets.TryGetValue(token, out var builder))
            {
                return builder;
            }
        }

        // Flag-style tokens without a shape= value.
        if (style.GetBool("rhombus")) return Rhombus;
        if (style.GetBool("triangle")) return Triangle;
        if (style.GetBool("hexagon")) return HexagonWrap;

        return null;
    }

    private static IEnumerable<string> EnumerateShapeTokens(string shape)
    {
        if (string.IsNullOrEmpty(shape))
        {
            yield break;
        }

        yield return shape;

        // "mxgraph.flowchart.decision" -> "decision" (last segment).
        var lastDot = shape.LastIndexOf('.');
        if (lastDot >= 0 && lastDot < shape.Length - 1)
        {
            yield return shape[(lastDot + 1)..];
        }
    }

    private static readonly Dictionary<string, Func<Rect, DrawIoStyle, Geometry>> Presets =
        new(StringComparer.OrdinalIgnoreCase)
        {
            ["rectangle"] = (b, _) => new RectangleGeometry(b),
            ["rect"] = (b, _) => new RectangleGeometry(b),
            ["ellipse"] = (b, _) => new EllipseGeometry(b),
            ["rhombus"] = Rhombus,
            ["triangle"] = Triangle,
            ["hexagon"] = HexagonWrap,
            ["parallelogram"] = Parallelogram,
            ["trapezoid"] = Trapezoid,
            ["step"] = Step,
            ["cylinder"] = Cylinder,
            ["cylinder3"] = Cylinder,
            ["cloud"] = Cloud,
            ["umlActor"] = Actor,
            ["actor"] = Actor,
            ["internalStorage"] = InternalStorage,
            ["document"] = Document,
            ["document2"] = Document,
            ["manualInput"] = ManualInput,
            ["card"] = Card,
            ["note"] = Note,
            ["note2"] = Note,
            ["callout"] = Callout,
            ["doubleArrow"] = DoubleArrow,
            ["singleArrow"] = SingleArrow,
            ["doubleEllipse"] = DoubleEllipse,
            ["line"] = Line,
            ["process"] = Process,
            ["loopLimit"] = LoopLimit,
            ["offPageConnector"] = OffPageConnector,
            ["tape"] = Tape,
            ["delay"] = Delay,
            ["cube"] = Cube,
            ["display"] = Display,
            ["dataStorage"] = DataStorage,
            // Flowchart / UML extras (pure geometry).
            ["terminator"] = Terminator,
            ["start"] = Terminator,
            ["end"] = Terminator,
            ["predefinedProcess"] = PredefinedProcess,
            ["process2"] = PredefinedProcess,
            ["sequential_data"] = SequentialData,
            ["tapeData"] = SequentialData,
            ["multi-document"] = MultiDocument,
            ["multi_document"] = MultiDocument,
            ["stored_data"] = StoredData,
            ["manual_operation"] = ManualOperation,
            ["merge_or_storage"] = MergeOrStorage,
            ["sumEllipse"] = SumEllipse,
            ["or"] = OrGate,
            ["and"] = AndGate,
            ["xor"] = XorGate,
            ["cross2"] = CrossShape,
            ["cross"] = CrossShape,
            ["star"] = Star,
            ["octagon"] = Octagon,
            ["pentagon"] = Pentagon,
            ["orthogonal_triangle"] = OrthogonalTriangle,
            ["hexagon2"] = HexagonWrap,
            ["rectCallout"] = Callout,
            ["roundRectCallout"] = Callout,
            ["dc_document"] = Document,
            ["step2"] = Step,
            ["tee"] = Tee,
            ["folder"] = FolderShape,
        };

    // ---- Basic polygon shapes (mxActor derivatives) ----

    private static Geometry Rhombus(Rect b, DrawIoStyle style)
    {
        var points = new[]
        {
            new Point(b.Center.X, b.Y),
            new Point(b.Right, b.Center.Y),
            new Point(b.Center.X, b.Bottom),
            new Point(b.X, b.Center.Y),
        };
        return BuildPolygonGeometry(points, style, close: true);
    }

    private static Geometry Triangle(Rect b, DrawIoStyle style)
    {
        var direction = (style.Get("direction") ?? "east").ToLowerInvariant();
        Point a, c, d;
        switch (direction)
        {
            case "north":
                a = new Point(b.Center.X, b.Y);
                c = new Point(b.Right, b.Bottom);
                d = new Point(b.X, b.Bottom);
                break;
            case "south":
                a = new Point(b.X, b.Y);
                c = new Point(b.Right, b.Y);
                d = new Point(b.Center.X, b.Bottom);
                break;
            case "west":
                a = new Point(b.X, b.Center.Y);
                c = new Point(b.Right, b.Y);
                d = new Point(b.Right, b.Bottom);
                break;
            default: // east
                a = new Point(b.Right, b.Center.Y);
                c = new Point(b.X, b.Y);
                d = new Point(b.X, b.Bottom);
                break;
        }

        return BuildPolygonGeometry([a, c, d], style, close: true);
    }

    private static Geometry HexagonWrap(Rect b, DrawIoStyle style) => Hexagon(b, style);

    private static Geometry Hexagon(Rect b, DrawIoStyle style)
    {
        var direction = (style.Get("direction") ?? "east").ToLowerInvariant();
        var vertical = direction is "north" or "south";

        // mxHexagon: horizontal "size" fraction defaults to 0.25.
        var size = ResolveSize(style, vertical ? b.Height : b.Width, 0.25, 20);

        if (vertical)
        {
            var pts = new[]
            {
                new Point(b.Center.X, b.Y),
                new Point(b.Right, b.Y + size),
                new Point(b.Right, b.Bottom - size),
                new Point(b.Center.X, b.Bottom),
                new Point(b.X, b.Bottom - size),
                new Point(b.X, b.Y + size),
            };
            return BuildPolygonGeometry(pts, style, close: true);
        }
        else
        {
            var pts = new[]
            {
                new Point(b.X + size, b.Y),
                new Point(b.Right - size, b.Y),
                new Point(b.Right, b.Center.Y),
                new Point(b.Right - size, b.Bottom),
                new Point(b.X + size, b.Bottom),
                new Point(b.X, b.Center.Y),
            };
            return BuildPolygonGeometry(pts, style, close: true);
        }
    }

    private static Geometry Parallelogram(Rect b, DrawIoStyle style)
    {
        var direction = (style.Get("direction") ?? "east").ToLowerInvariant();
        var flipH = style.GetBool("flipH");

        if (direction is "north" or "south")
        {
            var size = ResolveSize(style, b.Height, 0.2, 20);
            var pts = new[]
            {
                new Point(b.X, b.Y),
                new Point(b.Right, b.Y + size),
                new Point(b.Right, b.Bottom),
                new Point(b.X, b.Bottom - size),
            };
            return BuildPolygonGeometry(pts, style, close: true);
        }
        else
        {
            var size = ResolveSize(style, b.Width, 0.2, 20, maxFraction: 1.0);
            Point[] pts;
            if (flipH)
            {
                pts =
                [
                    new Point(b.X + size, b.Y),
                    new Point(b.Right, b.Y),
                    new Point(b.Right - size, b.Bottom),
                    new Point(b.X, b.Bottom),
                ];
            }
            else
            {
                pts =
                [
                    new Point(b.X, b.Y),
                    new Point(b.Right - size, b.Y),
                    new Point(b.Right, b.Bottom),
                    new Point(b.X + size, b.Bottom),
                ];
            }

            return BuildPolygonGeometry(pts, style, close: true);
        }
    }

    private static Geometry Trapezoid(Rect b, DrawIoStyle style)
    {
        var direction = (style.Get("direction") ?? "east").ToLowerInvariant();
        var flipV = style.GetBool("flipV");

        switch (direction)
        {
            case "west":
            {
                var size = ResolveSize(style, b.Width, 0.2, 20, maxFraction: 1.0);
                return BuildPolygonGeometry(
                    [new Point(b.X, b.Y), new Point(b.Right, b.Y),
                     new Point(b.Right - size, b.Bottom), new Point(b.X + size, b.Bottom)],
                    style, close: true);
            }
            case "north":
            {
                var size = ResolveSize(style, b.Height, 0.2, 20, maxFraction: 1.0);
                return BuildPolygonGeometry(
                    [new Point(b.X + size, b.Y), new Point(b.Right, b.Y + size),
                     new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom - size)],
                    style, close: true);
            }
            case "south":
            {
                var size = ResolveSize(style, b.Height, 0.2, 20, maxFraction: 1.0);
                return BuildPolygonGeometry(
                    [new Point(b.X, b.Y + size), new Point(b.Right, b.Y),
                     new Point(b.Right, b.Bottom - size), new Point(b.X, b.Bottom)],
                    style, close: true);
            }
            default: // east
            {
                var size = ResolveSize(style, b.Width, 0.2, 20, maxFraction: 0.5);
                if (flipV)
                {
                    return BuildPolygonGeometry(
                        [new Point(b.X, b.Y + size), new Point(b.Right, b.Y + size),
                         new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom)],
                        style, close: true);
                }

                return BuildPolygonGeometry(
                    [new Point(b.X + size, b.Y), new Point(b.Right - size, b.Y),
                     new Point(b.Right, b.Bottom), new Point(b.X, b.Bottom)],
                    style, close: true);
            }
        }
    }

    private static Geometry Step(Rect b, DrawIoStyle style)
    {
        var direction = (style.Get("direction") ?? "east").ToLowerInvariant();
        var vertical = direction is "north" or "south";

        if (vertical)
        {
            var size = ResolveSize(style, b.Height, 0.2, 20);
            var pts = new[]
            {
                new Point(b.X, b.Y + size),
                new Point(b.Center.X, b.Y),
                new Point(b.Right, b.Y + size),
                new Point(b.Right, b.Bottom),
                new Point(b.Center.X, b.Bottom - size),
                new Point(b.X, b.Bottom),
            };
            return BuildPolygonGeometry(pts, style, close: true);
        }
        else
        {
            var size = ResolveSize(style, b.Width, 0.2, 20);
            var pts = new[]
            {
                new Point(b.X, b.Y),
                new Point(b.Right - size, b.Y),
                new Point(b.Right, b.Center.Y),
                new Point(b.Right - size, b.Bottom),
                new Point(b.X, b.Bottom),
                new Point(b.X + size, b.Center.Y),
            };
            return BuildPolygonGeometry(pts, style, close: true);
        }
    }

    // ---- Actor-based curved shapes ----

    private static Geometry Cylinder(Rect b, DrawIoStyle style)
    {
        // mxCylinder uses min(15, round(h/5)) as the cap size.
        var dy = Math.Min(15, Math.Round(b.Height / 5));
        dy = Math.Max(2, Math.Min(dy, b.Height / 2));

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Body: top ellipse arc down to the bottom.
            ctx.BeginFigure(new Point(b.X, b.Y + dy), true);
            ctx.CubicBezierTo(
                new Point(b.X, b.Y + 2 * dy),
                new Point(b.Right, b.Y + 2 * dy),
                new Point(b.Right, b.Y + dy));
            ctx.LineTo(new Point(b.Right, b.Bottom - dy));
            ctx.CubicBezierTo(
                new Point(b.Right, b.Bottom + dy / 3),
                new Point(b.X, b.Bottom + dy / 3),
                new Point(b.X, b.Bottom - dy));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Cloud(Rect b, DrawIoStyle style)
    {
        var w = b.Width;
        var h = b.Height;
        double X(double f) => b.X + f * w;
        double Y(double f) => b.Y + f * h;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(X(0.25), Y(0.25)), true);
            ctx.CubicBezierTo(new Point(X(0.05), Y(0.25)), new Point(X(0.00), Y(0.50)), new Point(X(0.16), Y(0.55)));
            ctx.CubicBezierTo(new Point(X(0.00), Y(0.66)), new Point(X(0.18), Y(0.90)), new Point(X(0.31), Y(0.80)));
            ctx.CubicBezierTo(new Point(X(0.40), Y(1.00)), new Point(X(0.70), Y(1.00)), new Point(X(0.80), Y(0.80)));
            ctx.CubicBezierTo(new Point(X(1.00), Y(0.80)), new Point(X(1.00), Y(0.60)), new Point(X(0.875), Y(0.50)));
            ctx.CubicBezierTo(new Point(X(1.00), Y(0.30)), new Point(X(0.80), Y(0.10)), new Point(X(0.625), Y(0.20)));
            ctx.CubicBezierTo(new Point(X(0.50), Y(0.05)), new Point(X(0.30), Y(0.05)), new Point(X(0.25), Y(0.25)));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Actor(Rect b, DrawIoStyle style)
    {
        var w = b.Width;
        var h = b.Height;
        var bodyWidth = w / 3;
        double X(double f) => b.X + f * w;
        double Y(double f) => b.Y + f * h;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(X(0), Y(1)), true);
            ctx.CubicBezierTo(new Point(X(0), Y(0.6)), new Point(X(0), Y(0.4)), new Point(X(0.5), Y(0.4)));
            ctx.CubicBezierTo(
                new Point(X(0.5) - bodyWidth, Y(0.4)),
                new Point(X(0.5) - bodyWidth, Y(0)),
                new Point(X(0.5), Y(0)));
            ctx.CubicBezierTo(
                new Point(X(0.5) + bodyWidth, Y(0)),
                new Point(X(0.5) + bodyWidth, Y(0.4)),
                new Point(X(0.5), Y(0.4)));
            ctx.CubicBezierTo(new Point(X(1), Y(0.4)), new Point(X(1), Y(0.6)), new Point(X(1), Y(1)));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry InternalStorage(Rect b, DrawIoStyle style)
    {
        var dx = style.GetDouble("dx", 15);
        var dy = style.GetDouble("dy", 15);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Y + dy));
            ctx.LineTo(new Point(b.X + dx, b.Y + dy));
            ctx.LineTo(new Point(b.X + dx, b.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Document(Rect b, DrawIoStyle style)
    {
        var size = style.GetDouble("size", 0.3);
        size = Math.Clamp(size, 0, 1);
        var waveHeight = b.Height * size;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom - waveHeight));
            // bottom wave: cubic approximating mxGraph's quad-ish curve.
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.75, b.Bottom + waveHeight / 3),
                new Point(b.X + b.Width * 0.25, b.Bottom - 2 * waveHeight / 3),
                new Point(b.X, b.Bottom - waveHeight));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry ManualInput(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Height, 0.25, 30, maxFraction: 1.0);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y + size), true);
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Card(Rect b, DrawIoStyle style)
    {
        var size = style.GetDouble("size", 30);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right - size, b.Y));
            ctx.LineTo(new Point(b.Right, b.Y + size));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Note(Rect b, DrawIoStyle style)
    {
        var size = style.GetDouble("size", 30);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right - size, b.Y));
            ctx.LineTo(new Point(b.Right - size, b.Y + size));
            ctx.LineTo(new Point(b.Right, b.Y + size));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Callout(Rect b, DrawIoStyle style)
    {
        var baseSize = style.GetDouble("base", 20);
        var length = style.GetDouble("size", 20);

        // Callout pointer at bottom-left.
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X + length + baseSize, b.Bottom));
            ctx.LineTo(new Point(b.X + length, b.Bottom + length / 2));
            ctx.LineTo(new Point(b.X + length, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry DoubleArrow(Rect b, DrawIoStyle style)
    {
        var arrowWidth = style.GetDouble("arrowWidth", 0.4);
        var arrowSize = style.GetDouble("arrowSize", 0.4);
        var aw = arrowWidth * b.Height / 2;
        var asz = arrowSize * b.Width;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Center.Y), true);
            ctx.LineTo(new Point(b.X + asz, b.Center.Y - aw));
            ctx.LineTo(new Point(b.X + asz, b.Y));
            ctx.LineTo(new Point(b.Right - asz, b.Y));
            ctx.LineTo(new Point(b.Right - asz, b.Center.Y - aw));
            ctx.LineTo(new Point(b.Right, b.Center.Y));
            ctx.LineTo(new Point(b.Right - asz, b.Center.Y + aw));
            ctx.LineTo(new Point(b.Right - asz, b.Bottom));
            ctx.LineTo(new Point(b.X + asz, b.Bottom));
            ctx.LineTo(new Point(b.X + asz, b.Center.Y + aw));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry SingleArrow(Rect b, DrawIoStyle style)
    {
        var arrowWidth = style.GetDouble("arrowWidth", 0.5);
        var arrowSize = style.GetDouble("arrowSize", 0.5);
        var aw = arrowWidth * b.Height / 2;
        var asz = arrowSize * b.Width;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Center.Y - aw), true);
            ctx.LineTo(new Point(b.Right - asz, b.Center.Y - aw));
            ctx.LineTo(new Point(b.Right - asz, b.Y));
            ctx.LineTo(new Point(b.Right, b.Center.Y));
            ctx.LineTo(new Point(b.Right - asz, b.Bottom));
            ctx.LineTo(new Point(b.Right - asz, b.Center.Y + aw));
            ctx.LineTo(new Point(b.X, b.Center.Y + aw));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry DoubleEllipse(Rect b, DrawIoStyle style)
    {
        var inset = style.GetDouble("size", 3);
        inset = Math.Max(inset, Math.Min(b.Width, b.Height) * 0.08);

        var outer = new EllipseGeometry(b);
        var innerBounds = b.Deflate(inset);
        if (innerBounds.Width <= 0 || innerBounds.Height <= 0)
        {
            return outer;
        }

        var geometry = new GeometryGroup();
        geometry.Children.Add(outer);
        geometry.Children.Add(new EllipseGeometry(innerBounds));
        return geometry;
    }

    private static Geometry Line(Rect b, DrawIoStyle style)
    {
        var direction = (style.Get("direction") ?? "east").ToLowerInvariant();
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            if (direction is "north" or "south")
            {
                ctx.BeginFigure(new Point(b.Center.X, b.Y), false);
                ctx.LineTo(new Point(b.Center.X, b.Bottom));
            }
            else
            {
                ctx.BeginFigure(new Point(b.X, b.Center.Y), false);
                ctx.LineTo(new Point(b.Right, b.Center.Y));
            }

            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry Process(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Width, 0.1, 10, maxFraction: 0.5);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
            ctx.BeginFigure(new Point(b.X + size, b.Y), false);
            ctx.LineTo(new Point(b.X + size, b.Bottom));
            ctx.EndFigure(false);
            ctx.BeginFigure(new Point(b.Right - size, b.Y), false);
            ctx.LineTo(new Point(b.Right - size, b.Bottom));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry LoopLimit(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Height, 0.2, 20, maxFraction: 0.5);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y + size), true);
            ctx.LineTo(new Point(b.X + size, b.Y));
            ctx.LineTo(new Point(b.Right - size, b.Y));
            ctx.LineTo(new Point(b.Right, b.Y + size));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry OffPageConnector(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Height, 0.5, 20, maxFraction: 0.5);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom - size));
            ctx.LineTo(new Point(b.Center.X, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom - size));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Tape(Rect b, DrawIoStyle style)
    {
        var size = style.GetDouble("size", 0.4);
        size = Math.Clamp(size, 0, 1);
        var h = b.Height * size;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.66, b.Bottom - h * 0.5),
                new Point(b.X + b.Width * 0.33, b.Bottom + h * 0.5),
                new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Delay(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Width, 0.5, 20, maxFraction: 0.5);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right - size, b.Y));
            ctx.CubicBezierTo(
                new Point(b.Right, b.Y),
                new Point(b.Right, b.Bottom),
                new Point(b.Right - size, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Cube(Rect b, DrawIoStyle style)
    {
        var size = style.GetDouble("size", 20);
        size = Math.Min(size, Math.Min(b.Width, b.Height) / 2);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Front face.
            ctx.BeginFigure(new Point(b.X, b.Y + size), true);
            ctx.LineTo(new Point(b.Right - size, b.Y + size));
            ctx.LineTo(new Point(b.Right - size, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
            // Top face.
            ctx.BeginFigure(new Point(b.X, b.Y + size), true);
            ctx.LineTo(new Point(b.X + size, b.Y));
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right - size, b.Y + size));
            ctx.EndFigure(true);
            // Side face.
            ctx.BeginFigure(new Point(b.Right - size, b.Y + size), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom - size));
            ctx.LineTo(new Point(b.Right - size, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Display(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Width, 0.15, 20, maxFraction: 0.5);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X + size, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.CubicBezierTo(
                new Point(b.Right - size * 2, b.Center.Y),
                new Point(b.Right - size * 2, b.Center.Y),
                new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X + size, b.Bottom));
            ctx.CubicBezierTo(
                new Point(b.X + size * 2, b.Center.Y),
                new Point(b.X + size * 2, b.Center.Y),
                new Point(b.X + size, b.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry DataStorage(Rect b, DrawIoStyle style)
    {
        var offset = ResolveSize(style, b.Width, 0.15, 15, maxFraction: 0.5);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // The left edge has an "S" curve (appears as a wavy side).
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            // Left curve approximated with a cubic across the height.
            ctx.CubicBezierTo(
                new Point(b.X + offset * 2, b.Y + b.Height * 0.75),
                new Point(b.X + offset * 2, b.Y + b.Height * 0.25),
                new Point(b.X, b.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    // ---- Flowchart / UML extras ----

    private static Geometry Terminator(Rect b, DrawIoStyle style)
    {
        // Stadium (fully rounded rectangle), height-limited radius.
        var r = b.Height / 2;
        return new RectangleGeometry(b, r, r);
    }

    private static Geometry PredefinedProcess(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Width, 0.1, 10, maxFraction: 0.5);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
            ctx.BeginFigure(new Point(b.X + size, b.Y), false);
            ctx.LineTo(new Point(b.X + size, b.Bottom));
            ctx.EndFigure(false);
            ctx.BeginFigure(new Point(b.Right - size, b.Y), false);
            ctx.LineTo(new Point(b.Right - size, b.Bottom));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry SequentialData(Rect b, DrawIoStyle style)
    {
        // Rectangle with a wavy right edge.
        return WaveEdge(b, right: true);
    }

    private static Geometry MultiDocument(Rect b, DrawIoStyle style)
    {
        var wave = b.Height * 0.15;

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Front document.
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom - wave));
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.75, b.Bottom + wave / 3),
                new Point(b.X + b.Width * 0.25, b.Bottom - 2 * wave / 3),
                new Point(b.X, b.Bottom - wave));
            ctx.EndFigure(true);
            // Offset copies behind.
            for (var i = 1; i <= 2; i++)
            {
                var off = i * wave * 0.6;
                ctx.BeginFigure(new Point(b.X + off, b.Y - off), false);
                ctx.LineTo(new Point(b.Right - i * wave * 0.6, b.Y - off));
                ctx.EndFigure(false);
            }
        }

        return geometry;
    }

    private static Geometry StoredData(Rect b, DrawIoStyle style)
    {
        // Rectangle with curved left/right edges (cylinder on its side).
        var d = ResolveSize(style, b.Width, 0.15, 10, maxFraction: 0.25);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X + d, b.Y), true);
            ctx.LineTo(new Point(b.Right - d, b.Y));
            ctx.CubicBezierTo(
                new Point(b.Right + d, b.Y), new Point(b.Right + d, b.Bottom), new Point(b.Right - d, b.Bottom));
            ctx.LineTo(new Point(b.X + d, b.Bottom));
            ctx.CubicBezierTo(
                new Point(b.X - d, b.Bottom), new Point(b.X - d, b.Y), new Point(b.X + d, b.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry ManualOperation(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Width, 0.2, 20, maxFraction: 0.5);
        return BuildPolygonGeometry(
            [new Point(b.X, b.Y), new Point(b.Right, b.Y),
             new Point(b.Right - size, b.Bottom), new Point(b.X + size, b.Bottom)],
            style, close: true);
    }

    private static Geometry MergeOrStorage(Rect b, DrawIoStyle style)
    {
        // Triangle pointing down on top of a rectangle.
        var size = b.Height * 0.3;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Center.X, b.Y + size));
            ctx.EndFigure(true);
            ctx.BeginFigure(new Point(b.X, b.Y + size), true);
            ctx.LineTo(new Point(b.Right, b.Y + size));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry SumEllipse(Rect b, DrawIoStyle style)
    {
        return new EllipseGeometry(b);
    }

    private static Geometry OrGate(Rect b, DrawIoStyle style)
    {
        // OR gate: left vertical edge bulging in, right side a pointed arc.
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            // Right side (pointed arc).
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.65, b.Y),
                new Point(b.Right, b.Center.Y),
                new Point(b.Right, b.Center.Y));
            ctx.CubicBezierTo(
                new Point(b.Right, b.Center.Y),
                new Point(b.X + b.Width * 0.65, b.Bottom),
                new Point(b.X, b.Bottom));
            // Left side bulging in.
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.25, b.Bottom),
                new Point(b.X + b.Width * 0.25, b.Y),
                new Point(b.X, b.Y));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry AndGate(Rect b, DrawIoStyle style)
    {
        var r = Math.Min(b.Width / 2, b.Height / 2);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right - r, b.Y));
            ctx.ArcTo(new Point(b.Right - r, b.Bottom), new Size(r, r), 0, false, SweepDirection.Clockwise);
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry XorGate(Rect b, DrawIoStyle style)
    {
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            // Same OR silhouette as OrGate.
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.65, b.Y),
                new Point(b.Right, b.Center.Y),
                new Point(b.Right, b.Center.Y));
            ctx.CubicBezierTo(
                new Point(b.Right, b.Center.Y),
                new Point(b.X + b.Width * 0.65, b.Bottom),
                new Point(b.X, b.Bottom));
            ctx.CubicBezierTo(
                new Point(b.X + b.Width * 0.25, b.Bottom),
                new Point(b.X + b.Width * 0.25, b.Y),
                new Point(b.X, b.Y));
            ctx.EndFigure(true);
            // Inner arc (XOR extra curve).
            var off = b.Width * 0.06;
            ctx.BeginFigure(new Point(b.X + off, b.Y), false);
            ctx.CubicBezierTo(
                new Point(b.X + off + b.Width * 0.2, b.Center.Y),
                new Point(b.X + off + b.Width * 0.2, b.Center.Y),
                new Point(b.X + off, b.Bottom));
            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static Geometry CrossShape(Rect b, DrawIoStyle style)
    {
        var size = style.GetDouble("size", 0.2);
        var s = size <= 1 ? size * Math.Min(b.Width, b.Height) : size;
        s = Math.Min(s, Math.Min(b.Width, b.Height) / 2);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X + s, b.Y), true);
            ctx.LineTo(new Point(b.Right - s, b.Y));
            ctx.LineTo(new Point(b.Right - s, b.Y + s));
            ctx.LineTo(new Point(b.Right, b.Y + s));
            ctx.LineTo(new Point(b.Right, b.Bottom - s));
            ctx.LineTo(new Point(b.Right - s, b.Bottom - s));
            ctx.LineTo(new Point(b.Right - s, b.Bottom));
            ctx.LineTo(new Point(b.X + s, b.Bottom));
            ctx.LineTo(new Point(b.X + s, b.Bottom - s));
            ctx.LineTo(new Point(b.X, b.Bottom - s));
            ctx.LineTo(new Point(b.X, b.Y + s));
            ctx.LineTo(new Point(b.X + s, b.Y + s));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry Star(Rect b, DrawIoStyle style)
    {
        var cx = b.Center.X;
        var cy = b.Center.Y;
        var outerX = b.Width / 2;
        var outerY = b.Height / 2;
        var points = new List<Point>();
        for (var i = 0; i < 10; i++)
        {
            var angle = -Math.PI / 2 + i * Math.PI / 5;
            var f = (i % 2 == 0) ? 1.0 : 0.4;
            points.Add(new Point(cx + Math.Cos(angle) * outerX * f, cy + Math.Sin(angle) * outerY * f));
        }

        return BuildPolygonGeometry([.. points], style, close: true);
    }

    private static Geometry Octagon(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Width, 0.25, 20, maxFraction: 0.5);
        return BuildPolygonGeometry(
        [
            new Point(b.X + size, b.Y), new Point(b.Right - size, b.Y),
            new Point(b.Right, b.Y + size), new Point(b.Right, b.Bottom - size),
            new Point(b.Right - size, b.Bottom), new Point(b.X + size, b.Bottom),
            new Point(b.X, b.Bottom - size), new Point(b.X, b.Y + size),
        ], style, close: true);
    }

    private static Geometry Pentagon(Rect b, DrawIoStyle style)
    {
        var points = new List<Point>();
        for (var i = 0; i < 5; i++)
        {
            var angle = -Math.PI / 2 + i * 2 * Math.PI / 5;
            points.Add(new Point(b.Center.X + Math.Cos(angle) * b.Width / 2, b.Center.Y + Math.Sin(angle) * b.Height / 2));
        }

        return BuildPolygonGeometry([.. points], style, close: true);
    }

    private static Geometry OrthogonalTriangle(Rect b, DrawIoStyle style)
    {
        return BuildPolygonGeometry(
            [new Point(b.X, b.Y), new Point(b.Right, b.Y), new Point(b.X, b.Bottom)],
            style, close: true);
    }

    private static Geometry Tee(Rect b, DrawIoStyle style)
    {
        var size = ResolveSize(style, b.Height, 0.2, 20, maxFraction: 0.5);
        return BuildPolygonGeometry(
            [new Point(b.X, b.Y), new Point(b.Right, b.Y),
             new Point(b.Right, b.Y + size), new Point(b.Center.X + size / 2, b.Y + size),
             new Point(b.Center.X + size / 2, b.Bottom), new Point(b.Center.X - size / 2, b.Bottom),
             new Point(b.Center.X - size / 2, b.Y + size), new Point(b.X, b.Y + size)],
            style, close: true);
    }

    private static Geometry FolderShape(Rect b, DrawIoStyle style)
    {
        var tab = ResolveSize(style, b.Width, 0.25, 40, maxFraction: 0.9);
        var tabH = ResolveSize(style, b.Height, 0.15, 14, maxFraction: 0.4);
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y + tabH), true);
            ctx.LineTo(new Point(b.X + tab, b.Y + tabH));
            ctx.LineTo(new Point(b.X + tab + tabH * 0.5, b.Y));
            ctx.LineTo(new Point(b.Right, b.Y));
            ctx.LineTo(new Point(b.Right, b.Bottom));
            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    private static Geometry WaveEdge(Rect b, bool right)
    {
        var wave = b.Height * 0.15;
        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(new Point(b.X, b.Y), true);
            ctx.LineTo(new Point(b.Right, b.Y));
            if (right)
            {
                ctx.CubicBezierTo(
                    new Point(b.Right, b.Y + wave), new Point(b.Right, b.Bottom - wave), new Point(b.Right, b.Bottom));
            }

            ctx.LineTo(new Point(b.X, b.Bottom));
            ctx.EndFigure(true);
        }

        return geometry;
    }

    // ---- Helpers ----

    private static Geometry BuildPolygonGeometry(Point[] points, DrawIoStyle style, bool close)
    {
        var rounded = style.GetBool("rounded") || style.GetBool("roundedPerimeter");
        if (!rounded)
        {
            var geometry = new StreamGeometry();
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], close);
                for (var i = 1; i < points.Length; i++)
                {
                    ctx.LineTo(points[i]);
                }

                ctx.EndFigure(close);
            }

            return geometry;
        }

        // Rounded polygon: approximate corners with quadratic curves.
        var arcSize = style.GetDouble("arcSize", 6) / 2;
        return RoundedPolygon(points, arcSize, close);
    }

    private static Geometry RoundedPolygon(Point[] points, double radius, bool close)
    {
        var geometry = new StreamGeometry();
        using var ctx = geometry.Open();
        var n = points.Length;

        Point Prev(int i) => points[(i - 1 + n) % n];
        Point Next(int i) => points[(i + 1) % n];

        var start = MoveToward(points[0], Next(0), radius);
        ctx.BeginFigure(start, close);

        for (var i = 0; i < n; i++)
        {
            var p = points[i];
            var towardNext = MoveToward(p, Next(i), radius);
            var towardPrev = MoveToward(p, Prev(i), radius);
            ctx.LineTo(towardPrev);
            ctx.QuadraticBezierTo(p, towardNext);
        }

        ctx.EndFigure(close);
        return geometry;
    }

    private static Point MoveToward(Point from, Point to, double distance)
    {
        var dx = to.X - from.X;
        var dy = to.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6)
        {
            return from;
        }

        var t = Math.Min(distance / len, 0.5);
        return new Point(from.X + dx * t, from.Y + dy * t);
    }

    /// <summary>
    /// Resolves a shape size following draw.io's convention: when
    /// <c>fixedSize=1</c> the <c>size</c> value is in pixels, otherwise it is a
    /// fraction of the relevant dimension. <paramref name="fixedDefault"/> is
    /// used when <c>size</c> is absent under fixed mode.
    /// </summary>
    private static double ResolveSize(
        DrawIoStyle style,
        double dimension,
        double fractionDefault,
        double fixedDefault,
        double maxFraction = 0.5)
    {
        var fixedMode = style.GetBool("fixedSize");
        var size = style.GetDouble("size", double.NaN);

        if (fixedMode)
        {
            var px = double.IsNaN(size) ? fixedDefault : size;
            return Math.Max(0, Math.Min(dimension, px));
        }

        var fraction = double.IsNaN(size) ? fractionDefault : size;
        return dimension * Math.Clamp(fraction, 0, maxFraction);
    }

    /// <summary>
    /// Builds a polygon from the <c>polyCoords</c>/<c>polyCurves</c> style keys.
    /// </summary>
    private static Geometry? BuildPolygon(DrawIoCell cell, Rect bounds, DrawIoStyle style, string shape)
    {
        var coords = ParsePolyCoords(style.Get("polyCoords"));
        var curvature = ParsePolyCurves(style.Get("polyCurves"));
        var isPolyline = style.GetBool("polyline");

        if (coords.Count < 2)
        {
            return null;
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

    private static Point Map(IReadOnlyList<double> coord, Rect bounds) =>
        new(bounds.X + coord[0] * bounds.Width, bounds.Y + coord[1] * bounds.Height);

    /// <summary>
    /// Parses the nested <c>[[x,y],[x,y],...]</c> array used by the
    /// <c>polyCoords</c> style key into a list of points. The pending number
    /// must be flushed when the closing bracket is reached, otherwise the
    /// second component of each pair is dropped.
    /// </summary>
    internal static List<List<double>> ParsePolyCoords(string? value)
    {
        var result = new List<List<double>>();
        if (string.IsNullOrWhiteSpace(value))
        {
            return result;
        }

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
                    if (depth == 2)
                    {
                        FlushNumber(inner, i, ref numberStart, current);
                        if (current.Count >= 2)
                        {
                            result.Add(current);
                        }
                    }

                    depth--;
                    break;
                default:
                    if (depth == 2 && IsNumberChar(c))
                    {
                        if (numberStart < 0)
                        {
                            numberStart = i;
                        }
                    }
                    else
                    {
                        FlushNumber(inner, i, ref numberStart, current);
                    }

                    break;
            }
        }

        return result;
    }

    private static void FlushNumber(string text, int end, ref int start, List<double> target)
    {
        if (start < 0)
        {
            return;
        }

        AppendNumber(target, text[start..end]);
        start = -1;
    }

    private static bool IsNumberChar(char c) =>
        char.IsDigit(c) || c is '.' or '-' or '+' or 'e' or 'E';

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

        void Flush(int end)
        {
            if (tokenStart < 0)
            {
                return;
            }

            current.Add(inner[tokenStart..end].Trim().Trim('"', '\''));
            tokenStart = -1;
        }

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
                    if (depth == 2)
                    {
                        Flush(i);
                        if (current.Count > 0)
                        {
                            result.Add(current);
                        }
                    }

                    depth--;
                    break;
                case ',':
                case ' ':
                case '\t':
                case '\n':
                case '\r':
                    if (depth == 2)
                    {
                        Flush(i);
                    }

                    break;
                default:
                    if (depth == 2 && tokenStart < 0)
                    {
                        tokenStart = i;
                    }

                    break;
            }
        }

        return result;
    }

    private static double ParseDouble(string? value) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : 0;
}
