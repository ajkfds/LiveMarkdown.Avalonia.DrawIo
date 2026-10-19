using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Avalonia control that parses draw.io (mxGraph) XML and renders the first
/// diagram with native drawing APIs, without a JavaScript runtime or WebView.
/// </summary>
public class DrawIoPresenter : Control
{
    /// <summary>Defines the <see cref="Text"/> property.</summary>
    public static readonly StyledProperty<string?> TextProperty =
        AvaloniaProperty.Register<DrawIoPresenter, string?>(nameof(Text));

    /// <summary>draw.io XML source (an <c>mxfile</c> or an <c>mxGraphModel</c>).</summary>
    public string? Text
    {
        get => GetValue(TextProperty);
        set => SetValue(TextProperty, value);
    }

    /// <summary>Defines the <see cref="Foreground"/> property.</summary>
    public static readonly StyledProperty<IBrush?> ForegroundProperty =
        TextBlock.ForegroundProperty.AddOwner<DrawIoPresenter>();

    /// <summary>Default foreground for labels and outlines that do not specify a color.</summary>
    public IBrush? Foreground
    {
        get => GetValue(ForegroundProperty);
        set => SetValue(ForegroundProperty, value);
    }

    /// <summary>Defines the <see cref="BackgroundBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> BackgroundBrushProperty =
        AvaloniaProperty.Register<DrawIoPresenter, IBrush?>(nameof(BackgroundBrush), Brushes.Transparent);

    /// <summary>Background brush for the entire diagram. Default is transparent.</summary>
    public IBrush? BackgroundBrush
    {
        get => GetValue(BackgroundBrushProperty);
        set => SetValue(BackgroundBrushProperty, value);
    }

    /// <summary>Defines the <see cref="FontFamily"/> property.</summary>
    public static readonly StyledProperty<FontFamily> FontFamilyProperty =
        TextBlock.FontFamilyProperty.AddOwner<DrawIoPresenter>();

    /// <summary>Font family for diagram text.</summary>
    public FontFamily FontFamily
    {
        get => GetValue(FontFamilyProperty);
        set => SetValue(FontFamilyProperty, value);
    }

    /// <summary>Defines the <see cref="FontSize"/> property.</summary>
    public static readonly StyledProperty<double> FontSizeProperty =
        TextBlock.FontSizeProperty.AddOwner<DrawIoPresenter>();

    /// <summary>Base font size for diagram text. Default is 12.</summary>
    public double FontSize
    {
        get => GetValue(FontSizeProperty);
        set => SetValue(FontSizeProperty, value);
    }

    /// <summary>Defines the <see cref="Scale"/> property.</summary>
    public static readonly StyledProperty<double> ScaleProperty =
        AvaloniaProperty.Register<DrawIoPresenter, double>(nameof(Scale), 1.0);

    /// <summary>Zoom/scale applied when rendering the diagram.</summary>
    public double Scale
    {
        get => GetValue(ScaleProperty);
        set => SetValue(ScaleProperty, value);
    }

    private const double Padding = 8;

    private DrawIoDiagram? _diagram;
    private Rect _contentBounds;
    private Exception? _error;
    private Size _size;

    static DrawIoPresenter()
    {
        AffectsRender<DrawIoPresenter>(
            TextProperty,
            ForegroundProperty,
            BackgroundBrushProperty,
            FontFamilyProperty,
            FontSizeProperty,
            ScaleProperty);
        AffectsMeasure<DrawIoPresenter>(TextProperty, FontSizeProperty, ScaleProperty);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty)
        {
            _diagram = null;
            _error = null;
            _size = default;
            InvalidateMeasure();
            InvalidateVisual();
        }
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize)
    {
        EnsureBuilt();
        return _size;
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext dc)
    {
        EnsureBuilt();

        if (BackgroundBrush is not null && Bounds.Width > 0 && Bounds.Height > 0)
        {
            dc.DrawRectangle(BackgroundBrush, null, new Rect(0, 0, Bounds.Width, Bounds.Height));
        }

        if (_error is not null)
        {
            DrawError(dc, _error);
            return;
        }

        if (_diagram is { } diagram)
        {
            RenderDiagram(dc, diagram);
        }
    }

    private void EnsureBuilt()
    {
        if (_diagram is not null || _error is not null || Text is null)
        {
            return;
        }

        if (string.IsNullOrWhiteSpace(Text))
        {
            _size = new Size(0, 0);
            return;
        }

        try
        {
            var file = DrawIoParser.Parse(Text);
            _diagram = file.Diagrams[0];
            _contentBounds = ComputeContentBounds(_diagram);
            var scale = Scale <= 0 ? 1.0 : Scale;
            _size = new Size(
                (_contentBounds.Width + Padding * 2) * scale,
                (_contentBounds.Height + Padding * 2) * scale);
        }
        catch (Exception ex)
        {
            _error = ex;
            _size = MeasureError(ex);
        }
    }

    private static Rect ComputeContentBounds(DrawIoDiagram diagram)
    {
        var minX = double.MaxValue;
        var minY = double.MaxValue;
        var maxX = double.MinValue;
        var maxY = double.MinValue;
        var any = false;

        foreach (var cell in diagram.OrderedCells)
        {
            if (!cell.IsVertex || cell.Id is "0" || cell.Id == diagram.LayerId)
            {
                continue;
            }

            var w = cell.Width;
            var h = cell.Height;
            var x = cell.AbsoluteX;
            var y = cell.AbsoluteY;

            minX = Math.Min(minX, x);
            minY = Math.Min(minY, y);
            maxX = Math.Max(maxX, x + Math.Max(w, 0));
            maxY = Math.Max(maxY, y + Math.Max(h, 0));
            any = true;

            foreach (var waypoint in cell.Geometry?.Waypoints ?? [])
            {
                minX = Math.Min(minX, waypoint.X);
                minY = Math.Min(minY, waypoint.Y);
                maxX = Math.Max(maxX, waypoint.X);
                maxY = Math.Max(maxY, waypoint.Y);
            }
        }

        foreach (var cell in diagram.OrderedCells)
        {
            if (!cell.IsEdge)
            {
                continue;
            }

            foreach (var waypoint in cell.Geometry?.Waypoints ?? [])
            {
                minX = Math.Min(minX, waypoint.X);
                minY = Math.Min(minY, waypoint.Y);
                maxX = Math.Max(maxX, waypoint.X);
                maxY = Math.Max(maxY, waypoint.Y);
                any = true;
            }
        }

        if (!any)
        {
            return new Rect(0, 0, 0, 0);
        }

        return new Rect(minX, minY, Math.Max(maxX - minX, 0), Math.Max(maxY - minY, 0));
    }

    private static Size MeasureError(Exception error)
    {
        var message = $"Failed to render draw.io diagram: {error.Message}";
        var width = Math.Clamp(message.Length * 6.5 + 16, 240, 520);
        var lines = Math.Max(1, Math.Ceiling(message.Length * 6.5 / Math.Max(width - 16, 1)));
        var height = lines * 17 + 16;
        return new Size(width, height);
    }

    private void DrawError(DrawingContext dc, Exception error)
    {
        var text = new FormattedText(
            $"Failed to render draw.io diagram: {error.Message}",
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily),
            FontSize,
            Foreground ?? Brushes.Red)
        {
            MaxTextWidth = Bounds.Width > 16 ? Bounds.Width - 16 : double.PositiveInfinity,
            TextAlignment = TextAlignment.Left
        };
        dc.DrawText(text, new Point(8, 8));
    }

    private void RenderDiagram(DrawingContext dc, DrawIoDiagram diagram)
    {
        var scale = Scale <= 0 ? 1.0 : Scale;
        using var _ = dc.PushTransform(
            Matrix.CreateScale(scale, scale) *
            Matrix.CreateTranslation(
                (Padding - _contentBounds.X) * scale,
                (Padding - _contentBounds.Y) * scale));

        DrawBackground(dc, diagram);

        // Edges first so vertices paint over their endpoints.
        foreach (var cell in diagram.OrderedCells)
        {
            if (cell.IsEdge)
            {
                DrawEdge(dc, diagram, cell);
            }
        }

        foreach (var cell in diagram.OrderedCells)
        {
            if (!cell.IsVertex || cell.Id is "0" || cell.Id == diagram.LayerId)
            {
                continue;
            }

            // Edge labels are rendered together with their edge, not as a
            // standalone vertex.
            if (IsEdgeLabel(cell))
            {
                continue;
            }

            DrawVertex(dc, diagram, cell);
        }
    }

    private void DrawBackground(DrawingContext dc, DrawIoDiagram diagram)
    {
        if (diagram.Background is { Length: > 0 } color)
        {
            if (TryBrush(color) is { } brush)
            {
                dc.DrawRectangle(brush, null, _contentBounds);
            }
        }
    }

    private void DrawVertex(DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell)
    {
        var style = cell.Style;
        var isTextOnly = style.GetBool("text") || style.Shape == "text" ||
                         string.Equals(style.Get("shape"), "text", StringComparison.OrdinalIgnoreCase);

        var width = cell.Width;
        var height = cell.Height;
        var bounds = new Rect(cell.AbsoluteX, cell.AbsoluteY, width, height);

        if (!isTextOnly && width > 0 && height > 0)
        {
            var geometry = DrawIoShapeGeometry.Build(cell, bounds, style);
            if (geometry is not null)
            {
                // draw.io default vertex style: white fill and black stroke when
                // the style does not specify fillColor/strokeColor. The default
                // can be suppressed with an explicit "none"/"default" value.
                var fill = ResolveFill(style);
                var stroke = ResolveStroke(style, this);
                var thickness = style.GetDouble("strokeWidth", 1);
                var pen = stroke is not null && thickness > 0 ? new Pen(stroke, thickness) : null;
                var dashed = style.GetBool("dashed");
                if (dashed && pen is not null)
                {
                    pen = new Pen(stroke, thickness, new DashStyle([3, 3], 0));
                }

                dc.DrawGeometry(fill, pen, geometry);
            }
        }

        DrawLabel(dc, diagram, cell, bounds, style, isTextOnly);
    }

    private void DrawLabel(
        DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell, Rect bounds,
        DrawIoStyle style, bool isTextOnly)
    {
        if (string.IsNullOrWhiteSpace(cell.Value))
        {
            return;
        }

        var attributes = ResolvePlaceholderAttributes(diagram, cell);
        var (text, spans) = DrawIoLabelParser.Parse(cell.Value, attributes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fontSize = style.GetDouble("fontSize", FontSize);
        var align = style.Get("align") ?? "center";
        var verticalLabelPosition = style.Get("verticalLabelPosition");
        var labelPosition = style.Get("labelPosition") ?? "center";
        var spacing = style.GetDouble("spacing", 2) + style.GetDouble("spacingLeft", 0);

        var typeface = new Typeface(FontFamily);
        var bold = style.GetBool("fontStyle") || style.GetBool("bold");
        if (style.Get("fontStyle") is { Length: > 0 } fs && int.TryParse(fs, out var styleBits))
        {
            bold = (styleBits & 1) != 0;
        }

        var defaultBrush = ResolveFontColor(style, this);
        var lines = text.Split('\n');
        var lineHeight = fontSize * 1.25;
        var totalHeight = lines.Length * lineHeight;

        // Determine the label box, honoring verticalLabelPosition (top/bottom)
        // and labelPosition (left/right). draw.io places the label adjacent to
        // the shape for left/right positions, outside its bounds.
        double boxX = bounds.X, boxY = bounds.Y, boxW = bounds.Width, boxH = bounds.Height;
        if (verticalLabelPosition == "top")
        {
            boxY = bounds.Y - totalHeight;
            boxH = totalHeight;
        }
        else if (verticalLabelPosition == "bottom")
        {
            boxY = bounds.Bottom;
            boxH = totalHeight;
        }

        var horizontalAnchor = align;
        switch (labelPosition)
        {
            case "left":
                // Label sits to the left; align its text to the shape's left edge.
                boxW = 0;
                boxX = bounds.X - spacing;
                horizontalAnchor = "right";
                break;
            case "right":
                boxW = 0;
                boxX = bounds.Right + spacing;
                horizontalAnchor = "left";
                break;
        }

        if (isTextOnly && (bounds.Width <= 0 || bounds.Height <= 0))
        {
            boxW = double.PositiveInfinity;
            boxH = totalHeight;
        }

        var y = boxY + (boxH - totalHeight) / 2;
        foreach (var line in lines)
        {
            var formatted = new FormattedText(
                line,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                bold ? new Typeface(FontFamily, FontStyle.Normal, FontWeight.Bold) : typeface,
                fontSize,
                defaultBrush);

            double x = horizontalAnchor switch
            {
                "left" => boxX + (labelPosition == "center" ? 3 : 0),
                "right" => boxX + boxW - formatted.Width - (labelPosition == "center" ? 3 : 0),
                _ => boxX + (boxW - formatted.Width) / 2
            };

            if (double.IsInfinity(boxW))
            {
                x = boxX;
            }

            dc.DrawText(formatted, new Point(x, y + (lineHeight - formatted.Height) / 2));
            y += lineHeight;
        }
    }

    private void DrawEdge(DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell)
    {
        var style = cell.Style;
        var source = ResolveEndpoint(diagram, cell, cell.Source, isSource: true);
        var target = ResolveEndpoint(diagram, cell, cell.Target, isSource: false);

        var waypoints = (cell.Geometry?.Waypoints ?? [])
            .Select(p => new Point(p.X, p.Y))
            .ToList();

        // draw.io routes edges through the exit/entry connection points on the
        // source/target perimeters, and inserts orthogonal bends when the edge
        // has no stored waypoints.
        var points = BuildEdgePath(diagram, cell, style, source, target, waypoints);
        if (points.Count < 2)
        {
            return;
        }

        var stroke = ResolveStroke(style, this) ?? Foreground ?? Brushes.Black;
        var thickness = style.GetDouble("strokeWidth", 1);
        var dashed = style.GetBool("dashed");
        var pen = dashed
            ? new Pen(stroke, thickness, new DashStyle([3, 3], 0))
            : new Pen(stroke, thickness);

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false);
            for (var i = 1; i < points.Count; i++)
            {
                ctx.LineTo(points[i]);
            }

            ctx.EndFigure(false);
        }

        dc.DrawGeometry(null, pen, geometry);

        DrawEdgeMarkers(dc, style, points, thickness);

        // The edge's own value is drawn at the edge midpoint; separate child
        // cells with the "edgeLabel" style are drawn at their stored offsets.
        DrawEdgeValueLabel(dc, diagram, cell, style, points);

        foreach (var childId in cell.Children)
        {
            if (diagram.Cells.TryGetValue(childId, out var child) && IsEdgeLabel(child))
            {
                DrawEdgeLabelChild(dc, diagram, child, style, points);
            }
        }
    }

    /// <summary>
    /// Builds the polyline for an edge. When the edge stores waypoints they are
    /// used verbatim; otherwise an orthogonal route is synthesised between the
    /// resolved exit/entry connection points.
    /// </summary>
    private static List<Point> BuildEdgePath(
        DrawIoDiagram diagram,
        DrawIoCell edge,
        DrawIoStyle style,
        Point? source,
        Point? target,
        List<Point> waypoints)
    {
        var points = new List<Point>();
        if (source is { } s)
        {
            points.Add(s);
        }

        points.AddRange(waypoints);

        if (target is { } t)
        {
            points.Add(t);
        }

        // With explicit waypoints draw.io uses them as-is (the connectors are
        // already orthogonal in the stored geometry).
        if (waypoints.Count > 0)
        {
            return points;
        }

        if (source is { } start && target is { } end)
        {
            var isOrthogonal = string.Equals(
                style.Get("edgeStyle"), "orthogonalEdgeStyle", StringComparison.OrdinalIgnoreCase);
            if (!isOrthogonal)
            {
                return points;
            }

            var exitX = style.GetDouble("exitX", double.NaN);
            var exitY = style.GetDouble("exitY", double.NaN);
            var entryX = style.GetDouble("entryX", double.NaN);
            var entryY = style.GetDouble("entryY", double.NaN);

            // Only synthesise bends when we know how the edge leaves/enters;
            // otherwise the straight perimeter-to-perimeter line is used.
            if (!double.IsNaN(exitX) && !double.IsNaN(entryX))
            {
                var horizontal = exitX >= 1 || exitX <= 0 || entryX >= 1 || entryX <= 0;
                if (horizontal)
                {
                    var midX = (start.X + end.X) / 2;
                    return [start, new Point(midX, start.Y), new Point(midX, end.Y), end];
                }
            }
        }

        return points;
    }

    /// <summary>
    /// True for label cells that belong to an edge (the <c>edgeLabel</c> style
    /// flag). These are drawn together with their edge, not as vertices.
    /// </summary>
    private static bool IsEdgeLabel(DrawIoCell cell) =>
        cell.Style.GetBool("edgeLabel") ||
        string.Equals(cell.Style.Get("shape"), "edgeLabel", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Builds the attribute map used for <c>%Placeholder%</c> substitution.
    /// draw.io walks up the parent chain, so a child label can resolve
    /// attributes defined on its parent container (e.g. <c>%ModuleName%</c>).
    /// The cell's own attributes take precedence over ancestors.
    /// </summary>
    private static Dictionary<string, string> ResolvePlaceholderAttributes(
        DrawIoDiagram diagram, DrawIoCell cell)
    {
        var result = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        // Collect from the outermost ancestor down so nearer cells win.
        var chain = new List<DrawIoCell>();
        var current = cell;
        var guard = 0;
        while (current is not null && guard++ < 64)
        {
            chain.Add(current);
            if (current.Parent is not { Length: > 0 } parentId ||
                !diagram.Cells.TryGetValue(parentId, out var parent) ||
                parent.Id == diagram.RootId)
            {
                break;
            }

            current = parent;
        }

        for (var i = chain.Count - 1; i >= 0; i--)
        {
            foreach (var (key, value) in chain[i].Attributes)
            {
                result[key] = value;
            }
        }

        return result;
    }

    private void DrawEdgeMarkers(
        DrawingContext dc, DrawIoStyle style, List<Point> points, double thickness)
    {
        var startArrow = style.Get("startArrow") ?? "none";
        var endArrow = style.Get("endArrow") ?? "classic";
        var brush = ResolveStroke(style, this) ?? Foreground ?? Brushes.Black;

        if (!string.Equals(startArrow, "none", StringComparison.OrdinalIgnoreCase))
        {
            DrawArrowHead(dc, points[0], points[1], brush, thickness, startArrow);
        }

        if (!string.Equals(endArrow, "none", StringComparison.OrdinalIgnoreCase))
        {
            var last = points.Count - 1;
            DrawArrowHead(dc, points[last], points[last - 1], brush, thickness, endArrow);
        }
    }

    private static void DrawArrowHead(
        DrawingContext dc, Point tip, Point from, IBrush brush, double thickness, string kind)
    {
        // draw.io scales arrowheads with the stroke width; keep a sensible floor.
        var size = Math.Max(6, thickness * 3);
        var dx = tip.X - from.X;
        var dy = tip.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6)
        {
            return;
        }

        dx /= len;
        dy /= len;
        var perpX = -dy;
        var perpY = dx;
        var open = string.Equals(kind, "open", StringComparison.OrdinalIgnoreCase);
        const double spread = 0.5;

        var left = new Point(
            tip.X - size * (dx * Math.Cos(spread) - perpX * Math.Sin(spread)),
            tip.Y - size * (dy * Math.Cos(spread) - perpY * Math.Sin(spread)));
        var right = new Point(
            tip.X - size * (dx * Math.Cos(spread) + perpX * Math.Sin(spread)),
            tip.Y - size * (dy * Math.Cos(spread) + perpY * Math.Sin(spread)));

        if (open)
        {
            var pen = new Pen(brush, Math.Max(1, thickness));
            dc.DrawLine(pen, left, tip);
            dc.DrawLine(pen, tip, right);
            return;
        }

        var geometry = new StreamGeometry();
        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(tip, true);
            ctx.LineTo(left);
            ctx.LineTo(right);
            ctx.EndFigure(true);
        }

        dc.DrawGeometry(brush, null, geometry);
    }

    /// <summary>Draws the edge's own value at the edge midpoint.</summary>
    private void DrawEdgeValueLabel(
        DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell, DrawIoStyle style, List<Point> points)
    {
        var mid = EdgeMidpoint(points);
        var offset = cell.Geometry?.Offset ?? new DrawIoPoint(0, 0);
        DrawLabelText(dc, diagram, cell, style, new Point(mid.X + offset.X, mid.Y + offset.Y));
    }

    /// <summary>
    /// Draws an <c>edgeLabel</c> child cell. Its geometry stores a relative
    /// position along the edge (x) plus a vertical pixel offset (y), and an
    /// optional <c>offset</c> point that shifts the text away from the anchor.
    /// </summary>
    private void DrawEdgeLabelChild(
        DrawingContext dc, DrawIoDiagram diagram, DrawIoCell label, DrawIoStyle edgeStyle, List<Point> points)
    {
        var mid = EdgeMidpoint(points);
        var geometry = label.Geometry;
        var offset = geometry?.Offset ?? new DrawIoPoint(0, 0);

        // x is a fraction of the edge length; y is a vertical pixel offset.
        var totalLength = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            totalLength += Distance(points[i - 1], points[i]);
        }

        var centerX = mid.X + (geometry?.X ?? 0) * totalLength + offset.X;
        var centerY = mid.Y + (geometry?.Y ?? 0) + offset.Y;

        // Prefer the label's own style; fall back to the edge's for font size.
        var style = label.Style;
        DrawLabelText(dc, diagram, label, style, new Point(centerX, centerY));
    }

    private void DrawLabelText(
        DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell, DrawIoStyle style, Point center)
    {
        var (text, _) = DrawIoLabelParser.Parse(cell.Value, ResolvePlaceholderAttributes(diagram, cell));
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fontSize = style.GetDouble("fontSize", FontSize);
        var brush = ResolveFontColor(style, this);

        var formatted = new FormattedText(
            text,
            CultureInfo.CurrentCulture,
            FlowDirection.LeftToRight,
            new Typeface(FontFamily),
            fontSize,
            brush);

        var rect = new Rect(
            center.X - formatted.Width / 2,
            center.Y - formatted.Height / 2,
            formatted.Width,
            formatted.Height);
        dc.DrawRectangle(Brushes.White, null, rect);
        dc.DrawText(formatted, rect.Position);
    }

    private static Point EdgeMidpoint(List<Point> points)
    {
        if (points.Count == 0)
        {
            return default;
        }

        if (points.Count == 1)
        {
            return points[0];
        }

        // Walk the polyline to find the true midpoint by arc length.
        var totalLength = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            totalLength += Distance(points[i - 1], points[i]);
        }

        var half = totalLength / 2;
        var travelled = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            var segment = Distance(points[i - 1], points[i]);
            if (travelled + segment >= half && segment > 0)
            {
                var t = (half - travelled) / segment;
                return new Point(
                    points[i - 1].X + (points[i].X - points[i - 1].X) * t,
                    points[i - 1].Y + (points[i].Y - points[i - 1].Y) * t);
            }

            travelled += segment;
        }

        return points[^1];
    }

    private static double Distance(Point a, Point b)
    {
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        return Math.Sqrt(dx * dx + dy * dy);
    }

    /// <summary>
    /// Resolves an edge endpoint. draw.io connects to the source/target vertex
    /// perimeter at the exit/entry connection point (defaulting to the center
    /// when the edge does not constrain it). The exit point is used for the
    /// source and the entry point for the target.
    /// </summary>
    private static Point? ResolveEndpoint(
        DrawIoDiagram diagram, DrawIoCell edge, string? endpointId, bool isSource)
    {
        if (endpointId is null || !diagram.Cells.TryGetValue(endpointId, out var cell))
        {
            return null;
        }

        var width = cell.Width;
        var height = cell.Height;

        var prefix = isSource ? "exit" : "entry";
        var fx = edge.Style.GetDouble(prefix + "X", double.NaN);
        var fy = edge.Style.GetDouble(prefix + "Y", double.NaN);

        // Clamp to the shape box; draw.io connection points are normalized.
        var ox = double.IsNaN(fx) ? 0.5 : Math.Clamp(fx, 0, 1);
        var oy = double.IsNaN(fy) ? 0.5 : Math.Clamp(fy, 0, 1);

        return new Point(cell.AbsoluteX + ox * width, cell.AbsoluteY + oy * height);
    }

    /// <summary>
    /// Resolves a vertex/shape fill. draw.io's default vertex style uses
    /// <c>fillColor=default</c>, which maps to white; an absent value behaves
    /// the same way. <c>none</c> yields no fill.
    /// </summary>
    private static IBrush ResolveFill(DrawIoStyle style)
    {
        var fill = style.Get("fillColor");
        if (fill is null || string.Equals(fill, "default", StringComparison.OrdinalIgnoreCase))
        {
            // draw.io default vertex fill.
            return Brushes.White;
        }

        if (string.Equals(fill, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.Transparent;
        }

        return TryBrush(fill) ?? Brushes.White;
    }

    /// <summary>
    /// Resolves a stroke color. draw.io's default vertex/edge style uses
    /// <c>strokeColor=default</c>, which maps to black; an absent value behaves
    /// the same way. <c>none</c> yields no stroke.
    /// </summary>
    private static IBrush? ResolveStroke(DrawIoStyle style, DrawIoPresenter presenter)
    {
        var stroke = style.Get("strokeColor");
        if (string.Equals(stroke, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (stroke is null || string.Equals(stroke, "default", StringComparison.OrdinalIgnoreCase))
        {
            return presenter.Foreground ?? Brushes.Black;
        }

        return TryBrush(stroke) ?? presenter.Foreground ?? Brushes.Black;
    }

    private static IBrush ResolveFontColor(DrawIoStyle style, DrawIoPresenter presenter)
    {
        var color = style.Get("fontColor");
        if (color is null)
        {
            return presenter.Foreground ?? Brushes.Black;
        }

        return TryBrush(color) ?? presenter.Foreground ?? Brushes.Black;
    }

    /// <summary>
    /// Parses a draw.io/CSS color. Supports plain colors plus the
    /// <c>light-dark(a, b)</c> and <c>var(--x, fallback)</c> forms emitted by
    /// modern draw.io, falling back to the first parseable inner value.
    /// </summary>
    private static IBrush? TryBrush(string color)
    {
        var normalized = NormalizeColor(color);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            return new SolidColorBrush(Color.Parse(normalized));
        }
        catch
        {
            return null;
        }
    }

    private static string? NormalizeColor(string color)
    {
        var value = color.Trim();
        if (value.Length == 0 || value.Equals("none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var lower = value.ToLowerInvariant();
        if (lower.StartsWith("light-dark(", StringComparison.Ordinal))
        {
            // Use the light-theme branch.
            var inner = ExtractArguments(value["light-dark(".Length..^1]);
            return inner.Count > 0 ? NormalizeColor(inner[0]) : null;
        }

        if (lower.StartsWith("var(", StringComparison.Ordinal))
        {
            // Use the fallback argument when present.
            var inner = ExtractArguments(value["var(".Length..^1]);
            return inner.Count > 1 ? NormalizeColor(inner[1]) : null;
        }

        return value;
    }

    /// <summary>Splits a comma-separated argument list at the top nesting level.</summary>
    private static List<string> ExtractArguments(string text)
    {
        var result = new List<string>();
        var depth = 0;
        var start = 0;
        for (var i = 0; i < text.Length; i++)
        {
            switch (text[i])
            {
                case '(':
                    depth++;
                    break;
                case ')':
                    depth--;
                    break;
                case ',' when depth == 0:
                    result.Add(text[start..i].Trim());
                    start = i + 1;
                    break;
            }
        }

        if (start < text.Length)
        {
            result.Add(text[start..].Trim());
        }

        return result;
    }
}
