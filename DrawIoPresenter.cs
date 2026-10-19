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

            DrawVertex(dc, cell);
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

    private void DrawVertex(DrawingContext dc, DrawIoCell cell)
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

        DrawLabel(dc, cell, bounds, style, isTextOnly);
    }

    private void DrawLabel(DrawingContext dc, DrawIoCell cell, Rect bounds, DrawIoStyle style, bool isTextOnly)
    {
        if (string.IsNullOrWhiteSpace(cell.Value))
        {
            return;
        }

        var (text, spans) = DrawIoLabelParser.Parse(cell.Value, cell.Attributes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fontSize = style.GetDouble("fontSize", FontSize);
        var align = style.Get("align") ?? "center";
        var verticalLabelPosition = style.Get("verticalLabelPosition");

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

        // Determine the label box within the cell bounds, honoring labelPosition.
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

            double x = align switch
            {
                "left" => boxX + 3,
                "right" => boxX + boxW - formatted.Width - 3,
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
        var source = ResolveEndpoint(diagram, cell, cell.Source, isSource: true);
        var target = ResolveEndpoint(diagram, cell, cell.Target, isSource: false);

        var points = new List<Point>();
        if (source is { } s)
        {
            points.Add(s);
        }

        foreach (var waypoint in cell.Geometry?.Waypoints ?? [])
        {
            points.Add(new Point(waypoint.X, waypoint.Y));
        }

        if (target is { } t)
        {
            points.Add(t);
        }

        if (points.Count < 2)
        {
            return;
        }

        var style = cell.Style;
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
        DrawEdgeValueLabel(dc, cell, style, points);

        foreach (var childId in cell.Children)
        {
            if (diagram.Cells.TryGetValue(childId, out var child) && IsEdgeLabel(child))
            {
                DrawEdgeLabelChild(dc, child, style, points);
            }
        }
    }

    /// <summary>
    /// True for label cells that belong to an edge (the <c>edgeLabel</c> style
    /// flag). These are drawn together with their edge, not as vertices.
    /// </summary>
    private static bool IsEdgeLabel(DrawIoCell cell) =>
        cell.Style.GetBool("edgeLabel") ||
        string.Equals(cell.Style.Get("shape"), "edgeLabel", StringComparison.OrdinalIgnoreCase);

    private void DrawEdgeMarkers(
        DrawingContext dc, DrawIoStyle style, List<Point> points, double thickness)
    {
        var startArrow = style.Get("startArrow") ?? "none";
        var endArrow = style.Get("endArrow") ?? "classic";
        var brush = ResolveStroke(style, this) ?? Foreground ?? Brushes.Black;

        if (!string.Equals(startArrow, "none", StringComparison.OrdinalIgnoreCase))
        {
            DrawArrowHead(dc, points[0], points[1], brush, filled: startArrow != "open");
        }

        if (!string.Equals(endArrow, "none", StringComparison.OrdinalIgnoreCase))
        {
            var last = points.Count - 1;
            DrawArrowHead(dc, points[last], points[last - 1], brush, filled: endArrow != "open");
        }
    }

    private static void DrawArrowHead(
        DrawingContext dc, Point tip, Point from, IBrush brush, bool filled)
    {
        const double size = 9;
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
        const double spread = 0.45;

        var left = new Point(tip.X - size * (dx * Math.Cos(spread) - perpX * Math.Sin(spread)),
                             tip.Y - size * (dy * Math.Cos(spread) - perpY * Math.Sin(spread)));
        var right = new Point(tip.X - size * (dx * Math.Cos(spread) + perpX * Math.Sin(spread)),
                              tip.Y - size * (dy * Math.Cos(spread) + perpY * Math.Sin(spread)));

        if (filled)
        {
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
        else
        {
            var pen = new Pen(brush, 1);
            dc.DrawLine(pen, left, tip);
            dc.DrawLine(pen, tip, right);
        }
    }

    /// <summary>Draws the edge's own value at the edge midpoint.</summary>
    private void DrawEdgeValueLabel(
        DrawingContext dc, DrawIoCell cell, DrawIoStyle style, List<Point> points)
    {
        var mid = EdgeMidpoint(points);
        var offset = cell.Geometry?.Offset ?? new DrawIoPoint(0, 0);
        DrawLabelText(dc, cell, style, new Point(mid.X + offset.X, mid.Y + offset.Y));
    }

    /// <summary>
    /// Draws an <c>edgeLabel</c> child cell. Its geometry stores a relative
    /// position along the edge (x) plus a vertical pixel offset (y), and an
    /// optional <c>offset</c> point that shifts the text away from the anchor.
    /// </summary>
    private void DrawEdgeLabelChild(
        DrawingContext dc, DrawIoCell label, DrawIoStyle edgeStyle, List<Point> points)
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
        DrawLabelText(dc, label, style, new Point(centerX, centerY));
    }

    private void DrawLabelText(
        DrawingContext dc, DrawIoCell cell, DrawIoStyle style, Point center)
    {
        var (text, _) = DrawIoLabelParser.Parse(cell.Value, cell.Attributes);
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

    private static Point? ResolveEndpoint(
        DrawIoDiagram diagram, DrawIoCell edge, string? endpointId, bool isSource)
    {
        if (endpointId is null || !diagram.Cells.TryGetValue(endpointId, out var cell))
        {
            return null;
        }

        var width = cell.Width;
        var height = cell.Height;
        return new Point(cell.AbsoluteX + width / 2, cell.AbsoluteY + height / 2);
    }

    private IBrush ResolveFill(DrawIoStyle style)
    {
        var fill = style.Get("fillColor");
        if (fill is null)
        {
            return Brushes.Transparent;
        }

        if (string.Equals(fill, "none", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.Transparent;
        }

        if (string.Equals(fill, "default", StringComparison.OrdinalIgnoreCase))
        {
            return Brushes.Transparent;
        }

        return TryBrush(fill) ?? Brushes.Transparent;
    }

    private static IBrush? ResolveStroke(DrawIoStyle style, DrawIoPresenter presenter)
    {
        var stroke = style.Get("strokeColor");
        if (stroke is null)
        {
            return null;
        }

        if (string.Equals(stroke, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (string.Equals(stroke, "default", StringComparison.OrdinalIgnoreCase))
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

    private static IBrush? TryBrush(string color)
    {
        try
        {
            return new SolidColorBrush(Color.Parse(color));
        }
        catch
        {
            return null;
        }
    }
}
