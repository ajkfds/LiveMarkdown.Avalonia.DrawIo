using System.Globalization;
using System.Xml.Linq;
using Avalonia;
using Avalonia.Media;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Parser and renderer for draw.io <c>mxStencil</c> XML shape definitions
/// (the <c>&lt;shape&gt;</c> grammar used by the stencil sets under
/// <c>mxStencilRegistry</c>).
///
/// <para>Stencil definitions use a normalised view box (<c>w</c> × <c>h</c>,
/// default 100×100) and a small drawing DSL:</para>
///
/// <list type="bullet">
///   <item><description>state: <c>save</c> / <c>restore</c></description></item>
///   <item><description>paths: <c>path</c>, <c>move</c>, <c>line</c>,
///     <c>quad</c>, <c>curve</c>, <c>arc</c>, <c>rect</c>, <c>roundrect</c>,
///     <c>ellipse</c>, <c>close</c></description></item>
///   <item><description>paint: <c>fill</c>, <c>stroke</c>,
///     <c>fillstroke</c></description></item>
///   <item><description>style: <c>strokecolor</c>, <c>fillcolor</c>,
///     <c>strokewidth</c>, <c>dashed</c>, <c>dashpattern</c>, <c>alpha</c>,
///     <c>font*</c>, <c>linecap</c>, <c>linejoin</c>,
///     <c>miterlimit</c></description></item>
/// </list>
///
/// <para>Register additional stencil sets with <see cref="Register"/>; the
/// presenter then renders them instead of the plain rectangle fallback.</para>
/// </summary>
internal static class DrawIoStencil
{
    private static readonly Dictionary<string, XElement> Shapes = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>True when a stencil definition exists for the given shape id.</summary>
    public static bool Contains(string? shapeName) =>
        !string.IsNullOrEmpty(shapeName) && Shapes.ContainsKey(shapeName);

    /// <summary>
    /// Registers every <c>&lt;shape name="..."&gt;</c> element found in a
    /// stencil set document. <paramref name="package"/> is an optional prefix
    /// (e.g. <c>mxgraph.flowchart.</c>) applied to short names.
    /// </summary>
    public static void RegisterStencilSet(string xml, string? package = null)
    {
        XDocument document;
        try
        {
            document = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
        }
        catch
        {
            return;
        }

        var root = document.Root;
        if (root is null)
        {
            return;
        }

        foreach (var shape in root.Descendants().Where(e => e.Name.LocalName == "shape"))
        {
            var name = shape.Attribute("name")?.Value;
            if (string.IsNullOrEmpty(name))
            {
                continue;
            }

            Shapes[package is null ? name : package + name] = shape;
            // Also register under the bare name so short references resolve.
            Shapes.TryAdd(name, shape);
        }
    }

    /// <summary>Registers a single stencil definition.</summary>
    public static void Register(string name, string xml)
    {
        try
        {
            Shapes[name] = XDocument.Parse(xml, LoadOptions.PreserveWhitespace).Root!;
        }
        catch
        {
            // Ignore malformed definitions.
        }
    }

    /// <summary>Removes all registered stencils (used by tests).</summary>
    public static void Clear() => Shapes.Clear();

    /// <summary>
    /// Renders a stencil shape into <paramref name="dc"/>. Returns false when no
    /// stencil is registered for <paramref name="shapeName"/>.
    /// </summary>
    public static bool TryDraw(
        DrawingContext dc,
        string? shapeName,
        Rect bounds,
        DrawIoStyle style,
        IBrush defaultFill,
        IBrush defaultStroke,
        double defaultStrokeThickness)
    {
        if (string.IsNullOrEmpty(shapeName) || !Shapes.TryGetValue(shapeName, out var shape))
        {
            return false;
        }

        var w0 = Number(shape.Attribute("w")?.Value, 100);
        var h0 = Number(shape.Attribute("h")?.Value, 100);
        if (w0 <= 0 || h0 <= 0)
        {
            return false;
        }

        var aspect = ComputeAspect(shape, bounds, w0, h0);
        var swAttr = shape.Attribute("strokewidth")?.Value;
        var minScale = Math.Min(Math.Abs(aspect.Sx), Math.Abs(aspect.Sy));
        var baseStrokeWidth = string.Equals(swAttr, "inherit", StringComparison.OrdinalIgnoreCase)
            ? defaultStrokeThickness
            : Number(swAttr, 1) * minScale;

        var state = new RenderState
        {
            Stroke = defaultStroke,
            Fill = defaultFill,
            StrokeWidth = baseStrokeWidth,
            FontColor = defaultStroke,
            FontFamily = "Helvetica",
            FontSize = 12,
            Alpha = 1.0,
        };

        var background = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "background");
        var foreground = shape.Elements().FirstOrDefault(e => e.Name.LocalName == "foreground");

        DrawChildren(dc, background, aspect, state);
        DrawChildren(dc, foreground, aspect, state);
        return true;
    }

    // ---- Rendering ----

    private sealed class RenderState
    {
        public IBrush? Stroke;
        public IBrush? Fill;
        public double StrokeWidth;
        public IBrush? FontColor;
        public string? FontFamily;
        public double FontSize;
        public bool Bold;
        public bool Italic;
        public bool Dashed;
        public double[] DashPattern = [];
        public double Alpha;

        public RenderState Clone() => (RenderState)MemberwiseClone();
    }

    private sealed record Aspect(double X, double Y, double Sx, double Sy);

    private static Aspect ComputeAspect(XElement shape, Rect bounds, double w0, double h0)
    {
        var sx = bounds.Width / w0;
        var sy = bounds.Height / h0;

        var direction = shape.Attribute("direction")?.Value;
        var inverse = direction is "north" or "south";
        var x0 = bounds.X;
        var y0 = bounds.Y;

        if (inverse)
        {
            (sx, sy) = (sy, sx);
            var delta = (bounds.Width - bounds.Height) / 2;
            x0 += delta;
            y0 -= delta;
        }

        if (string.Equals(shape.Attribute("aspect")?.Value, "fixed", StringComparison.OrdinalIgnoreCase))
        {
            var s = Math.Min(sx, sy);
            sx = s;
            sy = s;
            x0 += (bounds.Width - w0 * sx) / 2;
            y0 += (bounds.Height - h0 * sy) / 2;
        }

        return new Aspect(x0, y0, sx, sy);
    }

    private static void DrawChildren(DrawingContext dc, XElement? node, Aspect aspect, RenderState state)
    {
        if (node is null)
        {
            return;
        }

        // The state stack models mxStencil's save/restore: a save pushes a copy
        // and restore restores it, so style changes are scoped.
        var stack = new Stack<RenderState>();
        var current = state;

        foreach (var child in node.Elements())
        {
            switch (child.Name.LocalName)
            {
                case "save":
                    stack.Push(current.Clone());
                    break;
                case "restore":
                    if (stack.Count > 0)
                    {
                        current = stack.Pop();
                    }

                    break;
                default:
                    DrawNode(dc, child, aspect, current);
                    break;
            }
        }
    }

    private static void DrawNode(DrawingContext dc, XElement node, Aspect aspect, RenderState state)
    {
        var name = node.Name.LocalName;
        switch (name)
        {
            case "path":
                DrawPath(dc, node, aspect, state);
                break;
            case "text":
                DrawText(dc, node, aspect, state);
                break;
            case "strokecolor":
                state.Stroke = ResolveColor(node.Attribute("color")?.Value, state.Stroke);
                break;
            case "fillcolor":
                state.Fill = ResolveColor(node.Attribute("color")?.Value, state.Fill);
                break;
            case "fontcolor":
                state.FontColor = ResolveColor(node.Attribute("color")?.Value, state.FontColor) ?? state.FontColor;
                break;
            case "strokewidth":
            {
                var minScale = Math.Min(Math.Abs(aspect.Sx), Math.Abs(aspect.Sy));
                var s = node.Attribute("fixed")?.Value == "1" ? 1 : minScale;
                state.StrokeWidth = Number(node.Attribute("width")?.Value, 1) * s;
                break;
            }
            case "dashed":
                state.Dashed = node.Attribute("dashed")?.Value == "1";
                break;
            case "dashpattern":
            {
                var value = node.Attribute("pattern")?.Value;
                if (value is { Length: > 0 })
                {
                    var minScale = Math.Min(Math.Abs(aspect.Sx), Math.Abs(aspect.Sy));
                    state.DashPattern = value
                        .Split([' '], StringSplitOptions.RemoveEmptyEntries)
                        .Select(p => Number(p, 0) * minScale)
                        .ToArray();
                }

                break;
            }
            case "alpha":
            case "fillalpha":
            case "strokealpha":
                state.Alpha = Clamp01(Number(node.Attribute("alpha")?.Value, 1));
                break;
            case "fontfamily":
                state.FontFamily = node.Attribute("family")?.Value ?? state.FontFamily;
                break;
            case "fontsize":
            {
                var minScale = Math.Min(Math.Abs(aspect.Sx), Math.Abs(aspect.Sy));
                state.FontSize = Number(node.Attribute("size")?.Value, 12) * minScale;
                break;
            }
            case "fontstyle":
            {
                var styleBits = (int)Number(node.Attribute("style")?.Value, 0);
                state.Bold = (styleBits & 1) != 0;
                state.Italic = (styleBits & 2) != 0;
                break;
            }
        }
    }

    private static void DrawPath(DrawingContext dc, XElement pathNode, Aspect aspect, RenderState state)
    {
        // Handle rect/roundrect/ellipse directly: they are standalone figures
        // with their own paint.
        foreach (var child in pathNode.Elements())
        {
            var name = child.Name.LocalName;
            if (name is "rect" or "roundrect" or "ellipse")
            {
                DrawStandaloneFigure(dc, child, aspect, state);
                return;
            }
        }

        var geometry = new StreamGeometry();
        StreamGeometryContext? ctx = null;
        var figureOpen = false;
        var current = new Point(0, 0);
        var start = new Point(0, 0);

        void Ensure()
        {
            if (ctx is null)
            {
                ctx = geometry.Open();
            }
        }

        void Commit(bool fill, bool stroke)
        {
            if (ctx is not null)
            {
                if (figureOpen)
                {
                    ctx.EndFigure(false);
                    figureOpen = false;
                }

                ctx.Dispose();
                ctx = null;
            }

            var fillBrush = fill ? ApplyAlpha(state.Fill, state.Alpha) : null;
            var pen = stroke ? BuildPen(state) : null;
            if (fillBrush is not null || pen is not null)
            {
                dc.DrawGeometry(fillBrush, pen, geometry);
            }

            geometry = new StreamGeometry();
        }

        void MoveTo(Point p)
        {
            Ensure();
            if (figureOpen)
            {
                ctx!.EndFigure(false);
            }

            ctx!.BeginFigure(p, false);
            figureOpen = true;
            current = p;
            start = p;
        }

        void LineTo(Point p)
        {
            Ensure();
            if (!figureOpen)
            {
                MoveTo(current);
            }

            ctx!.LineTo(p);
            current = p;
        }

        // Handle a path with rounded="1" first (mxStencil's rounded polygon).
        var rounded = pathNode.Attribute("rounded")?.Value == "1";
        if (rounded)
        {
            DrawRoundedPath(dc, pathNode, aspect, state);
            return;
        }

        foreach (var child in pathNode.Elements())
        {
            var name = child.Name.LocalName;
            switch (name)
            {
                case "move":
                    MoveTo(MapPoint(child, aspect));
                    break;
                case "line":
                    LineTo(MapPoint(child, aspect));
                    break;
                case "quad":
                {
                    Ensure();
                    if (!figureOpen) MoveTo(current);
                    var c1 = MapPoint(child, aspect, "x1", "y1");
                    var p = MapPoint(child, aspect, "x2", "y2");
                    ctx!.QuadraticBezierTo(c1, p);
                    current = p;
                    break;
                }
                case "curve":
                {
                    Ensure();
                    if (!figureOpen) MoveTo(current);
                    var c1 = MapPoint(child, aspect, "x1", "y1");
                    var c2 = MapPoint(child, aspect, "x2", "y2");
                    var p = MapPoint(child, aspect, "x3", "y3");
                    ctx!.CubicBezierTo(c1, c2, p);
                    current = p;
                    break;
                }
                case "arc":
                {
                    Ensure();
                    if (!figureOpen) MoveTo(current);
                    var rx = Number(child.Attribute("rx")?.Value, 0) * aspect.Sx;
                    var ry = Number(child.Attribute("ry")?.Value, 0) * aspect.Sy;
                    var large = Number(child.Attribute("large-arc-flag")?.Value, 0) != 0;
                    var sweep = Number(child.Attribute("sweep-flag")?.Value, 0) != 0;
                    var p = MapPoint(child, aspect);
                    try
                    {
                        ctx!.ArcTo(p, new Size(Math.Abs(rx), Math.Abs(ry)), 0, large,
                            sweep ? SweepDirection.Clockwise : SweepDirection.CounterClockwise);
                    }
                    catch
                    {
                        ctx!.LineTo(p);
                    }

                    current = p;
                    break;
                }
                case "rect":
                {
                    var r = Bounds(child, aspect);
                    MoveTo(r.TopLeft);
                    LineTo(r.TopRight);
                    LineTo(r.BottomRight);
                    LineTo(r.BottomLeft);
                    break;
                }
                case "close":
                    if (figureOpen)
                    {
                        ctx!.EndFigure(true);
                        figureOpen = false;
                        current = start;
                    }

                    break;
                case "fill":
                    Commit(true, false);
                    return;
                case "stroke":
                    Commit(false, true);
                    return;
                case "fillstroke":
                    Commit(true, true);
                    return;
            }
        }

        // Implicit commit (some stencils omit an explicit paint op).
        if (ctx is not null)
        {
            Commit(true, true);
        }
    }

    private static void DrawRoundedPath(DrawingContext dc, XElement pathNode, Aspect aspect, RenderState state)
    {
        var points = new List<Point>();
        foreach (var child in pathNode.Elements())
        {
            if (child.Name.LocalName is "move" or "line")
            {
                points.Add(MapPoint(child, aspect));
            }
        }

        if (points.Count < 2)
        {
            return;
        }

        var arcSize = Number(pathNode.Attribute("arcSize")?.Value, 6) / 2;
        var radius = Math.Min(arcSize, Math.Min(Math.Abs(aspect.Sx), Math.Abs(aspect.Sy)) * 6);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            var n = points.Count;
            var startPt = MoveToward(points[0], points[1 % n], radius);
            ctx.BeginFigure(startPt, true);
            for (var i = 0; i < n; i++)
            {
                var corner = points[i];
                var prev = points[(i - 1 + n) % n];
                var next = points[(i + 1) % n];
                var enter = MoveToward(corner, prev, radius);
                var exit = MoveToward(corner, next, radius);
                ctx.LineTo(enter);
                ctx.QuadraticBezierTo(corner, exit);
            }

            ctx.EndFigure(true);
        }

        dc.DrawGeometry(ApplyAlpha(state.Fill, state.Alpha), BuildPen(state), geometry);
    }

    private static Rect Bounds(XElement node, Aspect aspect)
    {
        var x = Number(node.Attribute("x")?.Value, 0);
        var y = Number(node.Attribute("y")?.Value, 0);
        var w = Number(node.Attribute("w")?.Value, 0);
        var h = Number(node.Attribute("h")?.Value, 0);
        return new Rect(Map(x, y, aspect), Map(x + w, y + h, aspect));
    }

    private static void DrawStandaloneFigure(DrawingContext dc, XElement node, Aspect aspect, RenderState state)
    {
        var r = Bounds(node, aspect);
        Geometry geometry = node.Name.LocalName switch
        {
            "roundrect" => new RectangleGeometry(
                r,
                Math.Min(r.Width, r.Height) * (Number(node.Attribute("arcsize")?.Value, 15) / 100.0),
                Math.Min(r.Width, r.Height) * (Number(node.Attribute("arcsize")?.Value, 15) / 100.0)),
            "ellipse" => new EllipseGeometry(r),
            _ => new RectangleGeometry(r),
        };

        dc.DrawGeometry(ApplyAlpha(state.Fill, state.Alpha), BuildPen(state), geometry);
    }

    private static void DrawText(DrawingContext dc, XElement node, Aspect aspect, RenderState state)
    {
        var str = node.Attribute("str")?.Value;
        if (string.IsNullOrEmpty(str))
        {
            return;
        }

        var x = Number(node.Attribute("x")?.Value, 0);
        var y = Number(node.Attribute("y")?.Value, 0);
        var align = node.Attribute("align")?.Value ?? "left";
        var valign = node.Attribute("valign")?.Value ?? "top";
        var size = Number(node.Attribute("fontsize")?.Value, state.FontSize / Math.Max(Math.Abs(aspect.Sx), 1e-6));

        var family = string.IsNullOrEmpty(state.FontFamily) ? "Helvetica" : state.FontFamily;
        var typeface = new Typeface(
            new FontFamily(family),
            state.Italic ? FontStyle.Italic : FontStyle.Normal,
            state.Bold ? FontWeight.Bold : FontWeight.Normal);

        var formatted = new FormattedText(
            str,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            typeface,
            size,
            ApplyAlpha(state.FontColor, state.Alpha) ?? Brushes.Black);

        var origin = Map(x, y, aspect);
        var dx = align switch
        {
            "center" => -formatted.Width / 2,
            "right" => -formatted.Width,
            _ => 0,
        };
        var dy = valign switch
        {
            "middle" => -formatted.Height / 2,
            "bottom" => -formatted.Height,
            _ => 0,
        };

        dc.DrawText(formatted, new Point(origin.X + dx, origin.Y + dy));
    }

    private static IPen? BuildPen(RenderState state)
    {
        var brush = ApplyAlpha(state.Stroke, state.Alpha);
        if (brush is null || state.StrokeWidth <= 0)
        {
            return null;
        }

        var dash = state.Dashed && state.DashPattern.Length >= 2
            ? new DashStyle(state.DashPattern, 0)
            : null;
        return new Pen(brush, state.StrokeWidth, dash);
    }

    // ---- Helpers ----

    private static Point MapPoint(XElement node, Aspect aspect, string xa = "x", string ya = "y") =>
        Map(Number(node.Attribute(xa)?.Value, 0), Number(node.Attribute(ya)?.Value, 0), aspect);

    private static Point Map(double x, double y, Aspect aspect) =>
        new(aspect.X + x * aspect.Sx, aspect.Y + y * aspect.Sy);

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

    private static IBrush? ResolveColor(string? value, IBrush? fallback)
    {
        if (string.IsNullOrEmpty(value))
        {
            return fallback;
        }

        if (string.Equals(value, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(value, "stroke", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "fill", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(value, "font", StringComparison.OrdinalIgnoreCase))
        {
            return fallback;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(value));
        }
        catch
        {
            return fallback;
        }
    }

    private static IBrush? ApplyAlpha(IBrush? brush, double alpha)
    {
        if (brush is not ISolidColorBrush solid || alpha >= 1.0)
        {
            return brush;
        }

        var c = solid.Color;
        return new SolidColorBrush(Color.FromArgb((byte)Math.Round(c.A * alpha), c.R, c.G, c.B));
    }

    private static double Number(string? value, double fallback) =>
        double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) ? d : fallback;

    private static double Clamp01(double value) => Math.Clamp(value, 0, 1);
}
