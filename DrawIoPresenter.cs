using System.Globalization;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Media;
using Avalonia.Media.Imaging;

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

    /// <summary>Defines the <see cref="PageIndex"/> property.</summary>
    public static readonly StyledProperty<int> PageIndexProperty =
        AvaloniaProperty.Register<DrawIoPresenter, int>(nameof(PageIndex), 0);

    /// <summary>
    /// Zero-based index of the diagram page to render when the source is a
    /// multi-page <c>mxfile</c>. Default is 0 (the first page).
    /// </summary>
    public int PageIndex
    {
        get => GetValue(PageIndexProperty);
        set => SetValue(PageIndexProperty, value);
    }

    private const double Padding = 8;

    private DrawIoDiagram? _diagram;
    private Rect _contentBounds;
    private Exception? _error;
    private Size _size;
    private readonly Dictionary<string, Bitmap?> _imageCache = new(StringComparer.Ordinal);

    static DrawIoPresenter()
    {
        AffectsRender<DrawIoPresenter>(
            TextProperty,
            ForegroundProperty,
            BackgroundBrushProperty,
            FontFamilyProperty,
            FontSizeProperty,
            ScaleProperty);
        AffectsMeasure<DrawIoPresenter>(TextProperty, FontSizeProperty, ScaleProperty, PageIndexProperty);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        base.OnPropertyChanged(change);
        if (change.Property == TextProperty || change.Property == PageIndexProperty)
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
            // Register the bundled stencil sets once so common flowchart shapes
            // render through the generic mxStencil engine.
            DrawIoStencilLibrary.RegisterBuiltIns();

            var file = DrawIoParser.Parse(Text);
            var page = Math.Clamp(PageIndex, 0, file.Diagrams.Count - 1);
            _diagram = file.Diagrams[page];
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
            if (!cell.IsVertex || cell.Id is "0" || cell.Id == diagram.LayerId ||
                !IsCellVisible(diagram, cell))
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
            if (!cell.IsEdge || !IsCellVisible(diagram, cell))
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

    /// <summary>
    /// True when a cell should be drawn: it is visible, all its ancestors are
    /// visible, its layer is visible, and no ancestor container is collapsed.
    /// </summary>
    private static bool IsCellVisible(DrawIoDiagram diagram, DrawIoCell cell)
    {
        var current = cell;
        var guard = 0;
        while (current is not null && guard++ < 64)
        {
            if (!current.Visible)
            {
                return false;
            }

            // A collapsed container hides its descendants. Guard against the
            // cell itself being the collapsed container.
            if (!ReferenceEquals(current, cell) &&
                (current.Style.GetBool("collapsible") || current.Style.GetBool("container")) &&
                current.Style.GetBool("collapsed"))
            {
                return false;
            }

            if (current.Parent is not { Length: > 0 } parentId ||
                parentId == diagram.RootId ||
                !diagram.Cells.TryGetValue(parentId, out var parent))
            {
                break;
            }

            current = parent;
        }

        return true;
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
            if (cell.IsEdge && IsCellVisible(diagram, cell))
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
            if (IsEdgeLabel(cell) || !IsCellVisible(diagram, cell))
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

        // Apply the shape's rotation/flip around the cell centre. draw.io keeps
        // the label upright, so only the shape geometry is transformed.
        var rotation = style.GetDouble("rotation", 0);
        var flipH = style.GetBool("flipH");
        var flipV = style.GetBool("flipV");
        var hasTransform = Math.Abs(rotation % 360) > 1e-6 || flipH || flipV;

        // Image cell: draw the bitmap instead of the shape geometry, but still
        // honour the shape outline if one is present.
        if (!isTextOnly && width > 0 && height > 0 && TryDrawImage(dc, cell, bounds, style, hasTransform, rotation, flipH, flipV))
        {
            DrawLabel(dc, diagram, cell, bounds, style, isTextOnly);
            return;
        }

        if (!isTextOnly && width > 0 && height > 0)
        {
            var fill = ResolveFillBrush(style);
            var stroke = ResolveStroke(style, this);
            var thickness = style.GetDouble("strokeWidth", 1);

            // A registered mxStencil definition is used for shapes the built-in
            // preset table does not already handle. Presets stay authoritative
            // so e.g. the flowchart "process"/"document" stencils do not shadow
            // the built-in geometry variants.
            if (!isTextOnly && style.Shape is { Length: > 0 } shapeName &&
                !DrawIoShapeGeometry.IsPreset(shapeName) &&
                DrawIoStencil.Contains(shapeName))
            {
                IDisposable? stencilTransform = null;
                if (hasTransform)
                {
                    stencilTransform = dc.PushTransform(ShapeTransform(bounds, rotation, flipH, flipV));
                }

                DrawIoStencil.TryDraw(
                    dc, shapeName, bounds, style,
                    fill ?? Brushes.Transparent,
                    stroke ?? Brushes.Black,
                    thickness);

                stencilTransform?.Dispose();
                DrawLabel(dc, diagram, cell, bounds, style, isTextOnly);
                return;
            }

            var geometry = DrawIoShapeGeometry.Build(cell, bounds, style);
            if (geometry is not null)
            {
                var pen = stroke is not null && thickness > 0 ? BuildPen(style, stroke, thickness) : null;

                IDisposable? transform = null;
                if (hasTransform)
                {
                    transform = dc.PushTransform(ShapeTransform(bounds, rotation, flipH, flipV));
                }

                // Shadow: a soft offset copy of the geometry behind the shape.
                if (style.GetBool("shadow"))
                {
                    var shadowBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));
                    var offset = new Vector(3, 3);
                    using (dc.PushTransform(Matrix.CreateTranslation(offset.X, offset.Y)))
                    {
                        dc.DrawGeometry(shadowBrush, null, geometry);
                    }
                }

                dc.DrawGeometry(fill, pen, geometry);

                // Glass effect: a translucent highlight over the top half.
                if (style.GetBool("glass") && fill is not null)
                {
                    var glassBounds = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height / 2);
                    var glass = new SolidColorBrush(Color.FromArgb(0x40, 0xFF, 0xFF, 0xFF));
                    dc.DrawGeometry(glass, null, new RectangleGeometry(glassBounds));
                }

                transform?.Dispose();
            }
        }

        DrawLabel(dc, diagram, cell, bounds, style, isTextOnly);
    }

    /// <summary>
    /// Draws an image cell (a <c>shape=image</c> style, an image-valued
    /// <c>value</c>, or an <c>image=data:...</c>/<c>image=https:...</c> style
    /// key). Returns true when an image was drawn (or attempted).
    /// </summary>
    private bool TryDrawImage(
        DrawingContext dc, DrawIoCell cell, Rect bounds, DrawIoStyle style,
        bool hasTransform, double rotation, bool flipH, bool flipV)
    {
        if (!TryResolveImageSource(cell, style, out var source))
        {
            return false;
        }

        var bitmap = GetOrLoadBitmap(source);
        if (bitmap is null)
        {
            // Fall back to the shape geometry if the image cannot be loaded.
            return false;
        }

        IDisposable? transform = null;
        if (hasTransform)
        {
            transform = dc.PushTransform(ShapeTransform(bounds, rotation, flipH, flipV));
        }

        // draw.io treats the bitmap as filling the cell (imageAspect=0), with
        // the origin optionally nudged by imageBorder/spacing. We use the plain
        // cell bounds unless the geometry carries an image offset.
        dc.DrawImage(bitmap, bounds);

        transform?.Dispose();
        return true;
    }

    /// <summary>
    /// Resolves the bitmap source for an image cell. Supports the style keys
    /// (<c>image</c>, <c>imagePath</c>) and an image-valued <c>value</c>.
    /// </summary>
    private static bool TryResolveImageSource(DrawIoCell cell, DrawIoStyle style, out string source)
    {
        source = string.Empty;

        var isImageShape = string.Equals(style.Get("shape"), "image", StringComparison.OrdinalIgnoreCase) ||
                           style.GetBool("image");

        // The image may be in the style or (when shape=image) in the value.
        var candidate = style.Get("image")
                        ?? style.Get("imagePath")
                        ?? (isImageShape ? cell.Value : null);

        if (string.IsNullOrWhiteSpace(candidate))
        {
            return false;
        }

        // Only data URIs and absolute URLs are supported (no file access).
        if (candidate.StartsWith("data:image/", StringComparison.OrdinalIgnoreCase))
        {
            source = candidate;
            return true;
        }

        if (Uri.TryCreate(candidate, UriKind.Absolute, out var uri) &&
            (uri.Scheme == Uri.UriSchemeHttp || uri.Scheme == Uri.UriSchemeHttps))
        {
            source = candidate;
            return true;
        }

        return false;
    }

    private Bitmap? GetOrLoadBitmap(string source)
    {
        if (_imageCache.TryGetValue(source, out var cached))
        {
            return cached;
        }

        Bitmap? bitmap = null;
        try
        {
            if (source.StartsWith("data:", StringComparison.OrdinalIgnoreCase))
            {
                var comma = source.IndexOf(',');
                if (comma >= 0)
                {
                    var payload = source[(comma + 1)..];
                    var bytes = Convert.FromBase64String(payload);
                    using var stream = new MemoryStream(bytes);
                    bitmap = new Bitmap(stream);
                }
            }
            else if (Uri.TryCreate(source, UriKind.Absolute, out var uri))
            {
                // Remote images require a synchronous download, which we avoid;
                // only data URIs are rendered inline.
                _ = uri;
            }
        }
        catch
        {
            bitmap = null;
        }

        _imageCache[source] = bitmap;
        return bitmap;
    }

    /// <summary>
    /// Builds the rotate/flip matrix applied to a shape, mirroring
    /// <c>mxSvgCanvas2D.rotate</c>: rotation is applied around the cell centre,
    /// and flips mirror about the centre (both flips == +180 degrees).
    /// </summary>
    internal static Matrix ShapeTransformForTest(Rect bounds, double rotationDegrees, bool flipH, bool flipV) =>
        ShapeTransform(bounds, rotationDegrees, flipH, flipV);

    internal static IBrush ResolveFillBrushForTest(DrawIoStyle style) => ResolveFillBrush(style);

    internal static double[] ResolveDashPatternForTest(DrawIoStyle style) => ResolveDashPattern(style);

    private static Matrix ShapeTransform(Rect bounds, double rotationDegrees, bool flipH, bool flipV)
    {
        var cx = bounds.Center.X;
        var cy = bounds.Center.Y;

        var matrix = Matrix.CreateTranslation(-cx, -cy);

        if (flipH && flipV)
        {
            rotationDegrees += 180;
        }
        else if (flipH)
        {
            matrix *= Matrix.CreateScale(-1, 1);
        }
        else if (flipV)
        {
            matrix *= Matrix.CreateScale(1, -1);
        }

        matrix *= Matrix.CreateRotation(rotationDegrees * Math.PI / 180.0);
        matrix *= Matrix.CreateTranslation(cx, cy);
        return matrix;
    }

    /// <summary>
    /// Builds the pen for a shape/edge, honouring <c>dashed</c> with an
    /// optional custom <c>dashPattern</c> and stroke opacity.
    /// </summary>
    private static IPen BuildPen(DrawIoStyle style, IBrush stroke, double thickness)
    {
        var pen = new Pen(stroke, thickness);
        if (style.GetBool("dashed"))
        {
            var pattern = ResolveDashPattern(style);
            pen = new Pen(stroke, thickness, new DashStyle(pattern, 0));
        }

        return pen;
    }

    /// <summary>
    /// Resolves the dash pattern. <c>dashPattern</c> (space-separated) wins over
    /// the draw.io default of <c>3 3</c>.
    /// </summary>
    private static double[] ResolveDashPattern(DrawIoStyle style)
    {
        var custom = style.Get("dashPattern");
        if (custom is { Length: > 0 })
        {
            var parts = custom.Split([' '], StringSplitOptions.RemoveEmptyEntries);
            var values = new List<double>();
            foreach (var part in parts)
            {
                if (double.TryParse(part, NumberStyles.Float, CultureInfo.InvariantCulture, out var d) && d >= 0)
                {
                    values.Add(d);
                }
            }

            if (values.Count >= 2)
            {
                return [.. values];
            }
        }

        return [3, 3];
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
        var (text, _) = DrawIoLabelParser.Parse(cell.Value, attributes);
        if (string.IsNullOrWhiteSpace(text))
        {
            return;
        }

        var fontSize = style.GetDouble("fontSize", FontSize);
        var align = style.Get("align") ?? "center";
        var verticalAlign = style.Get("verticalAlign") ?? "middle";
        var verticalLabelPosition = style.Get("verticalLabelPosition");
        var labelPosition = style.Get("labelPosition") ?? "center";
        var spacing = style.GetDouble("spacing", 2) + style.GetDouble("spacingLeft", 0);

        var (bold, italic, underline) = DrawIoTextLayout.ResolveFontStyle(style, false);
        var typeface = BuildTypeface(bold, italic);
        var defaultBrush = ResolveFontColor(style, this);

        // Vertical text (horizontal=0): rotate the whole label 90 degrees.
        var horizontalValue = style.Get("horizontal");
        var vertical = string.Equals(horizontalValue, "0", StringComparison.Ordinal) ||
                       string.Equals(horizontalValue, "false", StringComparison.OrdinalIgnoreCase);

        var lineHeight = fontSize * 1.25;

        // Determine the label box, honoring verticalLabelPosition (top/bottom)
        // and labelPosition (left/right). draw.io places the label adjacent to
        // the shape for left/right positions, outside its bounds.
        double boxX = bounds.X, boxY = bounds.Y, boxW = bounds.Width, boxH = bounds.Height;
        var isExternal = false;

        if (verticalLabelPosition == "top")
        {
            isExternal = true;
        }
        else if (verticalLabelPosition == "bottom")
        {
            isExternal = true;
        }

        var horizontalAnchor = align;
        switch (labelPosition)
        {
            case "left":
                boxW = 0;
                boxX = bounds.X - spacing;
                horizontalAnchor = "right";
                isExternal = true;
                break;
            case "right":
                boxW = 0;
                boxX = bounds.Right + spacing;
                horizontalAnchor = "left";
                isExternal = true;
                break;
        }

        // Word wrap when the label is bounded by the shape.
        var wrap = style.GetBool("whiteSpace") ||
                   string.Equals(style.Get("whiteSpace"), "wrap", StringComparison.OrdinalIgnoreCase);
        var availableWidth = !isExternal && !double.IsInfinity(boxW) && boxW > 0
            ? boxW - 6
            : double.PositiveInfinity;

        var lines = wrap && availableWidth > 0
            ? DrawIoTextLayout.Wrap(text, availableWidth, typeface, fontSize)
            : [.. text.Split('\n')];

        var totalHeight = lines.Count * lineHeight;

        // Re-resolve the external box height now that the line count is known.
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

        // Vertical text: rotate around the label centre and swap the bounds.
        IDisposable? transform = null;
        if (vertical)
        {
            var cx = boxX + (double.IsInfinity(boxW) ? 0 : boxW / 2);
            var cy = boxY + boxH / 2;
            transform = dc.PushTransform(
                Matrix.CreateTranslation(-cx, -cy) *
                Matrix.CreateRotation(-Math.PI / 2) *
                Matrix.CreateTranslation(cx, cy));
            // Swap the effective box for layout purposes.
            (boxW, boxH) = (boxH, boxW);
        }

        var startY = verticalAlign switch
        {
            "top" => boxY,
            "bottom" => boxY + boxH - totalHeight,
            _ => boxY + (boxH - totalHeight) / 2,
        };

        // Label background (drawn behind the text).
        var background = style.Get("labelBackgroundColor");
        if (background is { Length: > 0 } &&
            !string.Equals(background, "none", StringComparison.OrdinalIgnoreCase) &&
            TryBrush(background) is { } bgBrush)
        {
            var bgWidth = double.IsInfinity(boxW) ? 0 : boxW;
            if (bgWidth > 0)
            {
                dc.DrawRectangle(bgBrush, null, new Rect(boxX, startY, bgWidth, totalHeight));
            }
        }

        var y = startY;
        foreach (var line in lines)
        {
            var formatted = new FormattedText(
                line,
                CultureInfo.CurrentCulture,
                FlowDirection.LeftToRight,
                typeface,
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

            var drawY = y + (lineHeight - formatted.Height) / 2;

            // Optional per-cell underline (fontStyle bit 4).
            if (underline)
            {
                var underlineY = drawY + formatted.Height - fontSize * 0.15;
                dc.DrawLine(new Pen(defaultBrush, 1), new Point(x, underlineY), new Point(x + formatted.Width, underlineY));
            }

            dc.DrawText(formatted, new Point(x, drawY));
            y += lineHeight;
        }

        transform?.Dispose();
    }

    /// <summary>Builds a typeface honouring bold/italic font-style bits.</summary>
    private Typeface BuildTypeface(bool bold, bool italic)
    {
        if (!bold && !italic)
        {
            return new Typeface(FontFamily);
        }

        var style = italic ? FontStyle.Italic : FontStyle.Normal;
        var weight = bold ? FontWeight.Bold : FontWeight.Normal;
        return new Typeface(FontFamily, style, weight);
    }

    private void DrawEdge(DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell)
    {
        var style = cell.Style;

        var sourceCell = GetCell(diagram, cell.Source);
        var targetCell = GetCell(diagram, cell.Target);
        var sourceCenter = CenterOf(sourceCell);
        var targetCenter = CenterOf(targetCell);

        var source = ResolveEndpoint(sourceCell, style, isSource: true, toward: targetCenter);
        var target = ResolveEndpoint(targetCell, style, isSource: false, toward: sourceCenter);

        var waypoints = (cell.Geometry?.Waypoints ?? [])
            .Select(p => new Point(p.X, p.Y))
            .ToList();

        // draw.io routes edges through the exit/entry connection points on the
        // source/target perimeters, and inserts orthogonal bends when the edge
        // has no stored waypoints.
        var points = BuildEdgePath(
            diagram, cell, style, sourceCell, targetCell, source, target, waypoints);
        if (points.Count < 2)
        {
            return;
        }

        var stroke = ResolveStroke(style, this) ?? Foreground ?? Brushes.Black;
        var thickness = style.GetDouble("strokeWidth", 1);
        var pen = BuildPen(style, stroke, thickness);

        // Shorten the line at each end so it stops at the marker base rather
        // than poking out past the arrow tip (mxMarker mutates the endpoint pe).
        var linePoints = InsetLineForMarkers(style, points, thickness);

        // Find crossings with other edges so jumpStyle can insert bumps.
        var jumps = ResolveEdgeJumps(diagram, cell, linePoints);
        var geometry = BuildEdgeGeometry(style, linePoints, jumps);
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
    /// Builds the edge geometry from its routed polyline, honouring
    /// <c>rounded=1</c> (rounded orthogonal corners) and <c>curved=1</c>
    /// (smooth quadratic smoothing).
    /// </summary>
    internal static Geometry BuildEdgeGeometry(DrawIoStyle style, IReadOnlyList<Point> points)
    {
        return BuildEdgeGeometry(style, points, []);
    }

    /// <summary>Test hook for the rounded/curved edge geometry.</summary>
    internal static Geometry BuildEdgeGeometryForTest(DrawIoStyle style, IReadOnlyList<Point> points) =>
        BuildEdgeGeometry(style, points, []);

    /// <summary>Test hook for edge routing (ER / orthogonal).</summary>
    internal static List<Point> BuildEdgePathForTest(
        DrawIoCell edge, DrawIoStyle style, DrawIoCell? sourceCell, DrawIoCell? targetCell)
    {
        var source = sourceCell is null
            ? (Point?)null
            : new Point(sourceCell.AbsoluteX + sourceCell.Width / 2, sourceCell.AbsoluteY + sourceCell.Height / 2);
        var target = targetCell is null
            ? (Point?)null
            : new Point(targetCell.AbsoluteX + targetCell.Width / 2, targetCell.AbsoluteY + targetCell.Height / 2);

        return BuildEdgePath(null!, edge, style, sourceCell, targetCell, source, target, []);
    }

    internal static Geometry BuildEdgeGeometry(
        DrawIoStyle style, IReadOnlyList<Point> points, IReadOnlyList<EdgeJump> jumps)
    {
        var geometry = new StreamGeometry();
        if (points.Count < 2)
        {
            return geometry;
        }

        var curved = style.GetBool("curved");
        if (curved)
        {
            using (var ctx = geometry.Open())
            {
                ctx.BeginFigure(points[0], false);
                for (var i = 1; i < points.Count; i++)
                {
                    var mid = new Point((points[i - 1].X + points[i].X) / 2, (points[i - 1].Y + points[i].Y) / 2);
                    ctx.QuadraticBezierTo(points[i - 1], mid);
                    ctx.QuadraticBezierTo(points[i], points[i]);
                }

                ctx.EndFigure(false);
            }

            return geometry;
        }

        var radius = style.GetBool("rounded") ? ResolveCornerRadius(style) : 0;

        using (var ctx = geometry.Open())
        {
            ctx.BeginFigure(points[0], false);

            for (var i = 1; i < points.Count; i++)
            {
                var prev = points[i - 1];
                var cur = points[i];

                // Insert jump bumps that fall on this segment.
                foreach (var jump in jumps)
                {
                    if (jump.SegmentIndex == i - 1)
                    {
                        LineWithJump(ctx, prev, cur, jump.Center, jump.Size);
                    }
                }

                if (radius > 0 && i < points.Count - 1)
                {
                    var corner = cur;
                    var next = points[i + 1];
                    var enter = MoveAlong(corner, prev, Math.Min(radius, Distance(corner, prev) / 2));
                    var exit = MoveAlong(corner, next, Math.Min(radius, Distance(corner, next) / 2));
                    ctx.LineTo(enter);
                    ctx.QuadraticBezierTo(corner, exit);
                }
                else
                {
                    ctx.LineTo(cur);
                }
            }

            ctx.EndFigure(false);
        }

        return geometry;
    }

    private static void LineWithJump(
        StreamGeometryContext ctx, Point a, Point b, Point center, double size)
    {
        // Draw a -> (center - size/2) as a line, then bump to (center + size/2),
        // then continue to b.
        var dx = b.X - a.X;
        var dy = b.Y - a.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-6)
        {
            ctx.LineTo(b);
            return;
        }

        dx /= len;
        dy /= len;
        var half = size / 2;

        var start = new Point(center.X - dx * half, center.Y - dy * half);
        var end = new Point(center.X + dx * half, center.Y + dy * half);
        var perp = new Point(center.X - dy * half, center.Y + dx * half);

        ctx.LineTo(start);
        ctx.QuadraticBezierTo(perp, end);
    }

    /// <summary>Corner radius for rounded edges (draw.io arcSize default 20).</summary>
    private static double ResolveCornerRadius(DrawIoStyle style)
    {
        var arcSize = style.GetDouble("arcSize", 20);
        return Math.Clamp(arcSize, 0, 100) / 2;
    }

    /// <summary>A jump point on a specific edge segment (jumpStyle=arc).</summary>
    internal readonly record struct EdgeJump(int SegmentIndex, Point Center, double Size);

    /// <summary>
    /// Finds where this edge crosses other edges' segments, so jumpStyle can
    /// insert bumps. Only edges with <c>jumpStyle</c> other than <c>none</c>
    /// produce jumps.
    /// </summary>
    private static List<EdgeJump> ResolveEdgeJumps(
        DrawIoDiagram diagram, DrawIoCell edge, IReadOnlyList<Point> points)
    {
        var result = new List<EdgeJump>();

        var jumpStyle = edge.Style.Get("jumpStyle");
        if (string.IsNullOrEmpty(jumpStyle) ||
            string.Equals(jumpStyle, "none", StringComparison.OrdinalIgnoreCase))
        {
            return result;
        }

        var jumpSize = edge.Style.GetDouble("jumpSize", 6);

        foreach (var other in diagram.OrderedCells)
        {
            if (ReferenceEquals(other, edge) || !other.IsEdge)
            {
                continue;
            }

            var otherPoints = (other.Geometry?.Waypoints ?? [])
                .Select(p => new Point(p.X, p.Y))
                .ToList();
            if (otherPoints.Count < 2)
            {
                continue;
            }

            for (var i = 1; i < points.Count; i++)
            {
                for (var j = 1; j < otherPoints.Count; j++)
                {
                    if (TryIntersect(points[i - 1], points[i], otherPoints[j - 1], otherPoints[j], out var hit))
                    {
                        result.Add(new EdgeJump(i - 1, hit, jumpSize));
                    }
                }
            }
        }

        return result;
    }

    /// <summary>Intersection of two line segments, if any.</summary>
    internal static bool TryIntersect(Point a1, Point a2, Point b1, Point b2, out Point result)
    {
        result = default;
        var d1x = a2.X - a1.X;
        var d1y = a2.Y - a1.Y;
        var d2x = b2.X - b1.X;
        var d2y = b2.Y - b1.Y;

        var denom = d1x * d2y - d1y * d2x;
        if (Math.Abs(denom) < 1e-9)
        {
            return false;
        }

        var t = ((b1.X - a1.X) * d2y - (b1.Y - a1.Y) * d2x) / denom;
        var u = ((b1.X - a1.X) * d1y - (b1.Y - a1.Y) * d1x) / denom;

        if (t is < 0 or > 1 || u is < 0 or > 1)
        {
            return false;
        }

        result = new Point(a1.X + t * d1x, a1.Y + t * d1y);
        return true;
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
        DrawIoCell? sourceCell,
        DrawIoCell? targetCell,
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

        if (source is not { } start || target is not { } end)
        {
            return points;
        }

        var edgeStyle = style.Get("edgeStyle") ?? string.Empty;
        var exitX = style.GetDouble("exitX", double.NaN);
        var exitY = style.GetDouble("exitY", double.NaN);
        var entryX = style.GetDouble("entryX", double.NaN);
        var entryY = style.GetDouble("entryY", double.NaN);

        if (string.Equals(edgeStyle, "entityRelationEdgeStyle", StringComparison.OrdinalIgnoreCase))
        {
            return EntityRelationRoute(sourceCell, targetCell, start, end, points);
        }

        // Whether the edge leaves/enters horizontally (side) or vertically.
        var exitHorizontal = IsHorizontalAnchor(exitX, exitY);
        var entryHorizontal = IsHorizontalAnchor(entryX, entryY);

        if (string.Equals(edgeStyle, "orthogonalEdgeStyle", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(edgeStyle, "elbowEdgeStyle", StringComparison.OrdinalIgnoreCase))
        {
            return OrthogonalRoute(start, end, exitX, exitY, entryX, entryY,
                exitHorizontal, entryHorizontal, points);
        }

        return points;
    }

    /// <summary>
    /// Routes an <c>entityRelationEdgeStyle</c> edge, ported from draw.io's
    /// <c>mxEdgeStyle.EntityRelation</c>. The edge leaves the source table's
    /// left/right side (at its vertical centre) and enters the target's, using
    /// a <c>segment</c> (default 40) horizontal stub, with intermediate bends
    /// that depend on whether both endpoints face the same side.
    /// </summary>
    private static List<Point> EntityRelationRoute(
        DrawIoCell? sourceCell, DrawIoCell? targetCell, Point source, Point target, List<Point> fallback)
    {
        if (sourceCell is null || targetCell is null)
        {
            return fallback;
        }

        const double segment = 40;

        var sourceBox = new Rect(sourceCell.AbsoluteX, sourceCell.AbsoluteY, sourceCell.Width, sourceCell.Height);
        var targetBox = new Rect(targetCell.AbsoluteX, targetCell.AbsoluteY, targetCell.Width, targetCell.Height);

        var p0 = new Point(sourceBox.X, sourceBox.Y);
        var pe = new Point(targetBox.Right, targetBox.Y);

        // Which side (left/right) each endpoint attaches to, mirroring
        // mxEdgeStyle.EntityRelation's isLess comparisons. When a table sits
        // entirely to one side, the other connects on its facing edge.
        var isSourceLeft = pe.X < p0.X; // target right edge left of source left edge
        var isTargetLeft = sourceBox.Right < targetBox.X; // source right edge left of target

        var x0 = isSourceLeft ? sourceBox.X : sourceBox.Right;
        var y0 = sourceBox.Center.Y;
        var xe = isTargetLeft ? targetBox.X : targetBox.Right;
        var ye = targetBox.Center.Y;

        var dep = new Point(x0 + (isSourceLeft ? -segment : segment), y0);
        var arr = new Point(xe + (isTargetLeft ? -segment : segment), ye);

        var result = new List<Point> { new(x0, y0) };

        // Case 1: both go out on the same side -> route around via an x lane.
        if (isSourceLeft == isTargetLeft)
        {
            var x = isSourceLeft
                ? Math.Min(x0, xe) - segment
                : Math.Max(x0, xe) + segment;
            result.Add(new Point(x, y0));
            result.Add(new Point(x, ye));
        }
        // Case 2: the depart/arrive stubs are ordered the same way as the
        // sides -> step over at the mid height.
        else if ((dep.X < arr.X) == isSourceLeft)
        {
            var midY = y0 + (ye - y0) / 2;
            result.Add(dep);
            result.Add(new Point(dep.X, midY));
            result.Add(new Point(arr.X, midY));
            result.Add(arr);
        }
        // Case 3: direct diagonal between the two stubs.
        else
        {
            result.Add(dep);
            result.Add(arr);
        }

        result.Add(new Point(xe, ye));
        return result;
    }

    /// <summary>
    /// True when a connection anchor points sideways (an exitX of 0 or 1, or an
    /// entryX of 0 or 1), i.e. the edge leaves/enters the left/right edge.
    /// </summary>
    private static bool IsHorizontalAnchor(double x, double y)
    {
        if (!double.IsNaN(x))
        {
            return x <= 0.001 || x >= 0.999;
        }

        if (!double.IsNaN(y))
        {
            // px anchor: 0 or 1 means top/bottom, which is a vertical exit.
            return false;
        }

        return false;
    }

    /// <summary>
    /// Builds an orthogonal route between two connection points, choosing the
    /// bend axis based on how the edge leaves the source and enters the target.
    /// </summary>
    private static List<Point> OrthogonalRoute(
        Point start, Point end,
        double exitX, double exitY, double entryX, double entryY,
        bool exitHorizontal, bool entryHorizontal, List<Point> fallback)
    {
        // Both sides face horizontally: route via a vertical mid-line.
        if (exitHorizontal && entryHorizontal)
        {
            var midX = (start.X + end.X) / 2;
            return
            [
                start,
                new Point(midX, start.Y),
                new Point(midX, end.Y),
                end,
            ];
        }

        // Both sides face vertically: route via a horizontal mid-line.
        if (!exitHorizontal && !entryHorizontal && (!double.IsNaN(exitY) || !double.IsNaN(entryY)))
        {
            var midY = (start.Y + end.Y) / 2;
            return
            [
                start,
                new Point(start.X, midY),
                new Point(end.X, midY),
                end,
            ];
        }

        // Mixed anchors: leave along the exit axis, then bend once toward the
        // target. Two elbows are enough for a clean orthogonal route.
        if (exitHorizontal)
        {
            return [start, new Point(end.X, start.Y), end];
        }

        return [start, new Point(start.X, end.Y), end];
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

        // Marker size scales with the stroke width and can be overridden with
        // startSize/endSize (draw.io default arrow size is 6).
        var baseSize = Math.Max(6, thickness * 3);

        if (!IsNone(startArrow))
        {
            var startSize = style.GetDouble("startSize", baseSize);
            var startFilled = style.GetBool("startFill", true);
            DrawIoEdgeMarker.Paint(
                dc, startArrow, points[0], Direction(points[0], points[1]),
                startSize, thickness, brush, startFilled);
        }

        if (!IsNone(endArrow))
        {
            var endSize = style.GetDouble("endSize", baseSize);
            var endFilled = style.GetBool("endFill", true);
            var last = points.Count - 1;
            DrawIoEdgeMarker.Paint(
                dc, endArrow, points[last], Direction(points[last], points[last - 1]),
                endSize, thickness, brush, endFilled);
        }
    }

    private static bool IsNone(string kind) =>
        kind.Length == 0 || string.Equals(kind, "none", StringComparison.OrdinalIgnoreCase);

    /// <summary>
    /// Shortens the polyline at each end by the marker's line inset so the line
    /// stops at the marker's base instead of running out past the arrow tip
    /// (draw.io mutates the endpoint <c>pe</c> the same way in mxMarker.js).
    /// </summary>
    private static List<Point> InsetLineForMarkers(
        DrawIoStyle style, List<Point> points, double thickness)
    {
        var baseSize = Math.Max(6, thickness * 3);

        var startArrow = style.Get("startArrow") ?? "none";
        var endArrow = style.Get("endArrow") ?? "classic";

        var startInset = IsNone(startArrow)
            ? 0
            : DrawIoEdgeMarker.LineInset(startArrow, style.GetDouble("startSize", baseSize), thickness);
        var endInset = IsNone(endArrow)
            ? 0
            : DrawIoEdgeMarker.LineInset(endArrow, style.GetDouble("endSize", baseSize), thickness);

        if (startInset <= 0 && endInset <= 0)
        {
            return points;
        }

        var result = new List<Point>(points);

        if (startInset > 0)
        {
            result[0] = MoveAlong(result[0], result[1], startInset);
        }

        if (endInset > 0)
        {
            var last = result.Count - 1;
            result[last] = MoveAlong(result[last], result[last - 1], endInset);
        }

        return result;
    }

    /// <summary>Moves <paramref name="from"/> toward <paramref name="toward"/> by <paramref name="distance"/>.</summary>
    private static Point MoveAlong(Point from, Point toward, double distance)
    {
        var dx = toward.X - from.X;
        var dy = toward.Y - from.Y;
        var len = Math.Sqrt(dx * dx + dy * dy);
        if (len < 1e-9 || distance >= len)
        {
            // Collapse to the midpoint if the segment is shorter than the inset.
            return new Point((from.X + toward.X) / 2, (from.Y + toward.Y) / 2);
        }

        var t = distance / len;
        return new Point(from.X + dx * t, from.Y + dy * t);
    }

    /// <summary>Direction (not normalized) from <paramref name="from"/> to the tip.</summary>
    private static Vector Direction(Point tip, Point from) =>
        new(tip.X - from.X, tip.Y - from.Y);

    /// <summary>Draws the edge's own value at its label anchor.</summary>
    private void DrawEdgeValueLabel(
        DrawingContext dc, DrawIoDiagram diagram, DrawIoCell cell, DrawIoStyle style, List<Point> points)
    {
        var anchor = ComputeEdgeLabelAnchor(points, cell.Geometry);
        DrawLabelText(dc, diagram, cell, style, anchor);
    }

    /// <summary>
    /// Draws an <c>edgeLabel</c> child cell at the position draw.io computes
    /// for a relative edge geometry (see <see cref="ComputeEdgeLabelAnchor"/>).
    /// </summary>
    private void DrawEdgeLabelChild(
        DrawingContext dc, DrawIoDiagram diagram, DrawIoCell label, DrawIoStyle edgeStyle, List<Point> points)
    {
        var anchor = ComputeEdgeLabelAnchor(points, label.Geometry);

        // Prefer the label's own style; fall back to the edge's for font size.
        var style = label.Style;
        DrawLabelText(dc, diagram, label, style, anchor);
    }

    /// <summary>
    /// Computes an edge label anchor, ported from the official
    /// <c>mxGraphView.getPoint</c>. For a relative geometry the label travels
    /// along the polyline to <c>(x / 2 + 0.5) * length</c> measured from the
    /// start, then shifts by <c>y</c> pixels along the segment normal; the
    /// optional <c>offset</c> point is added verbatim.
    /// </summary>
    internal static Point ComputeEdgeLabelAnchor(List<Point> points, DrawIoGeometry? geometry)
    {
        if (points.Count == 0)
        {
            return default;
        }

        if (points.Count == 1 || geometry is null || !geometry.Relative)
        {
            // Absolute geometry: the offset (if any) is relative to the center.
            var center = EdgeMidpoint(points);
            var absOffset = geometry?.Offset;
            return new Point(center.X + (absOffset?.X ?? 0), center.Y + (absOffset?.Y ?? 0));
        }

        // Segment lengths of the routed polyline.
        var segments = new double[points.Count - 1];
        var totalLength = 0.0;
        for (var i = 1; i < points.Count; i++)
        {
            segments[i - 1] = Distance(points[i - 1], points[i]);
            totalLength += segments[i - 1];
        }

        var gx = geometry.X / 2.0;
        var gy = geometry.Y;
        var offsetX = geometry.Offset?.X ?? 0;
        var offsetY = geometry.Offset?.Y ?? 0;

        // Distance along the edge from the start.
        var dist = (gx + 0.5) * totalLength;

        // Find the segment containing dist (mxGraphView.getPoint).
        var index = 1;
        var length = 0.0;
        var segment = segments[0];
        while (dist >= length + segment && index < points.Count - 1)
        {
            length += segment;
            segment = segments[index];
            index++;
        }

        var factor = segment <= 0 ? 0 : (dist - length) / segment;
        var p0 = points[index - 1];
        var pe = points[index];

        var dx = pe.X - p0.X;
        var dy = pe.Y - p0.Y;

        // draw.io's segment normal: nx = dy/len, ny = dx/len.
        var nx = segment <= 0 ? 0 : dy / segment;
        var ny = segment <= 0 ? 0 : dx / segment;

        return new Point(
            p0.X + dx * factor + nx * gy + offsetX,
            p0.Y + dy * factor - (ny * gy - offsetY));
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

    private static DrawIoCell? GetCell(DrawIoDiagram diagram, string? id) =>
        id is not null && diagram.Cells.TryGetValue(id, out var cell) ? cell : null;

    private static Point CenterOf(DrawIoCell? cell) =>
        cell is null
            ? default
            : new Point(cell.AbsoluteX + cell.Width / 2, cell.AbsoluteY + cell.Height / 2);

    /// <summary>
    /// Resolves an edge endpoint. draw.io connects to the source/target vertex
    /// perimeter: when the edge specifies an exit/entry connection point it is
    /// used directly, otherwise the intersection of the line toward the other
    /// terminal with the vertex's bounding box is used (so the arrow tip sits
    /// on the box edge rather than at its centre).
    /// </summary>
    internal static Point? ResolveEndpoint(
        DrawIoCell? cell, DrawIoStyle edgeStyle, bool isSource, Point toward)
    {
        if (cell is null)
        {
            return null;
        }

        var width = cell.Width;
        var height = cell.Height;
        if (width <= 0 || height <= 0)
        {
            // Degenerate geometry (e.g. a port point): use the cell origin.
            return new Point(cell.AbsoluteX, cell.AbsoluteY);
        }

        var prefix = isSource ? "exit" : "entry";
        var fx = edgeStyle.GetDouble(prefix + "X", double.NaN);
        var fy = edgeStyle.GetDouble(prefix + "Y", double.NaN);

        // Explicit connection point: clamp to the shape box and use as-is.
        if (!double.IsNaN(fx) || !double.IsNaN(fy))
        {
            var ox = double.IsNaN(fx) ? 0.5 : Math.Clamp(fx, 0, 1);
            var oy = double.IsNaN(fy) ? 0.5 : Math.Clamp(fy, 0, 1);
            return new Point(cell.AbsoluteX + ox * width, cell.AbsoluteY + oy * height);
        }

        // Otherwise intersect the ray from the centre toward the other terminal
        // with the bounding box (rectangle perimeter).
        var center = CenterOf(cell);
        var dx = toward.X - center.X;
        var dy = toward.Y - center.Y;
        var perimeter = RectanglePerimeter(center, new Size(width, height), dx, dy);
        return perimeter ?? center;
    }

    /// <summary>
    /// Returns the intersection of the ray from <paramref name="center"/> in
    /// direction (<paramref name="dx"/>, <paramref name="dy"/>) with the
    /// rectangle of the given size centred on <paramref name="center"/>.
    /// </summary>
    internal static Point? RectanglePerimeter(Point center, Size size, double dx, double dy)
    {
        if (Math.Abs(dx) < 1e-9 && Math.Abs(dy) < 1e-9)
        {
            return null;
        }

        var hw = size.Width / 2;
        var hh = size.Height / 2;

        var t = double.PositiveInfinity;
        if (Math.Abs(dx) > 1e-9)
        {
            t = Math.Min(t, hw / Math.Abs(dx));
        }

        if (Math.Abs(dy) > 1e-9)
        {
            t = Math.Min(t, hh / Math.Abs(dy));
        }

        if (double.IsInfinity(t))
        {
            return null;
        }

        return new Point(center.X + dx * t, center.Y + dy * t);
    }

    /// <summary>
    /// Resolves a vertex/shape fill as a brush, synthesising a linear gradient
    /// when <c>gradientColor</c> is present. draw.io's default vertex style
    /// uses <c>fillColor=default</c>, which maps to white; an absent value
    /// behaves the same way. <c>none</c> yields no fill. <c>fillOpacity</c> and
    /// <c>opacity</c> (0..100, where 100 is opaque) are applied to the brush.
    /// </summary>
    private static IBrush ResolveFillBrush(DrawIoStyle style)
    {
        var opacity = ResolveOpacity(style, "fillOpacity", "opacity", 1.0);
        var fillColor = ResolveFillColor(style);

        var gradientColor = style.Get("gradientColor");
        if (gradientColor is { Length: > 0 } &&
            !string.Equals(gradientColor, "none", StringComparison.OrdinalIgnoreCase) &&
            TryColor(gradientColor) is { } second)
        {
            // Start with the fill color (or white when unset) at the top.
            var first = fillColor ?? Colors.White;
            first = ApplyOpacity(first, opacity);
            second = ApplyOpacity(second, opacity);

            var brush = new LinearGradientBrush
            {
                StartPoint = new RelativePoint(0, 0, RelativeUnit.Relative),
                EndPoint = new RelativePoint(0, 1, RelativeUnit.Relative),
            };
            brush.GradientStops.Add(new GradientStop(first, 0));
            brush.GradientStops.Add(new GradientStop(second, 1));
            return brush;
        }

        if (fillColor is { } color)
        {
            return new SolidColorBrush(ApplyOpacity(color, opacity));
        }

        return Brushes.Transparent;
    }

    /// <summary>
    /// Resolves the raw <c>fillColor</c> as a color, or <c>null</c> when the
    /// fill is <c>none</c>.
    /// </summary>
    private static Color? ResolveFillColor(DrawIoStyle style)
    {
        var fill = style.Get("fillColor");
        if (string.Equals(fill, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        if (fill is null || string.Equals(fill, "default", StringComparison.OrdinalIgnoreCase))
        {
            return Colors.White;
        }

        return TryColor(fill) ?? Colors.White;
    }

    /// <summary>
    /// Resolves a stroke color. draw.io's default vertex/edge style uses
    /// <c>strokeColor=default</c>, which maps to black; an absent value behaves
    /// the same way. <c>none</c> yields no stroke. <c>strokeOpacity</c> and
    /// <c>opacity</c> are applied.
    /// </summary>
    private static IBrush? ResolveStroke(DrawIoStyle style, DrawIoPresenter presenter)
    {
        var stroke = style.Get("strokeColor");
        if (string.Equals(stroke, "none", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        var color = stroke is null || string.Equals(stroke, "default", StringComparison.OrdinalIgnoreCase)
            ? ColorOf(presenter.Foreground) ?? Colors.Black
            : TryColor(stroke) ?? ColorOf(presenter.Foreground) ?? Colors.Black;

        var opacity = ResolveOpacity(style, "strokeOpacity", "opacity", 1.0);
        return new SolidColorBrush(ApplyOpacity(color, opacity));
    }

    private static Color? ColorOf(IBrush? brush) =>
        brush is ISolidColorBrush solid ? solid.Color : null;

    /// <summary>
    /// Reads an opacity from the style. draw.io stores opacity as a percentage
    /// (0..100); missing values fall back to <paramref name="fallback"/>.
    /// </summary>
    private static double ResolveOpacity(DrawIoStyle style, string key, string generalKey, double fallback)
    {
        var raw = style.Get(key) ?? style.Get(generalKey);
        if (raw is null)
        {
            return fallback;
        }

        if (double.TryParse(raw, NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
        {
            // Accept both 0..1 and 0..100 forms.
            var fraction = value > 1 ? value / 100.0 : value;
            return Math.Clamp(fraction, 0, 1);
        }

        return fallback;
    }

    private static Color ApplyOpacity(Color color, double opacity) =>
        opacity >= 1 ? color : Color.FromArgb((byte)Math.Round(color.A * opacity), color.R, color.G, color.B);

    private static Color? TryColor(string color)
    {
        var normalized = NormalizeColor(color);
        if (normalized is null)
        {
            return null;
        }

        try
        {
            return Color.Parse(normalized);
        }
        catch
        {
            return null;
        }
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
