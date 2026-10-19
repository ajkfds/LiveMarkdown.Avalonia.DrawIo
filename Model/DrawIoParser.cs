using System.IO.Compression;
using System.Text;
using System.Xml.Linq;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Parses draw.io XML (<c>mxfile</c> / <c>mxGraphModel</c>) into a
/// <see cref="DrawIoFile"/> model. Supports both plain XML diagrams and the
/// default compressed (base64 + deflate) <c>diagram</c> payload.
/// </summary>
public static class DrawIoParser
{
    private static readonly XNamespace None = XNamespace.None;

    /// <summary>Parses a draw.io document, throwing <see cref="FormatException"/> on invalid input.</summary>
    public static DrawIoFile Parse(string source)
    {
        if (string.IsNullOrWhiteSpace(source))
        {
            throw new FormatException("draw.io source is empty.");
        }

        XDocument document;
        try
        {
            document = XDocument.Parse(source, LoadOptions.PreserveWhitespace);
        }
        catch (Exception ex)
        {
            throw new FormatException("draw.io source is not valid XML.", ex);
        }

        var root = document.Root ?? throw new FormatException("draw.io source has no root element.");

        if (root.Name.LocalName == "mxGraphModel")
        {
            return new DrawIoFile { Diagrams = [ParseGraphModel(root)] };
        }

        if (root.Name.LocalName != "mxfile")
        {
            // Some tools wrap the model differently; try to find one anywhere.
            var model = root.Descendants().FirstOrDefault(e => e.Name.LocalName == "mxGraphModel");
            if (model is null)
            {
                throw new FormatException($"Unexpected draw.io root element '{root.Name.LocalName}'.");
            }

            return new DrawIoFile { Diagrams = [ParseGraphModel(model)] };
        }

        var diagrams = new List<DrawIoDiagram>();
        foreach (var diagramElement in root.Elements().Where(e => e.Name.LocalName == "diagram"))
        {
            var model = ResolveDiagramModel(diagramElement);
            if (model is null)
            {
                continue;
            }

            var parsed = ParseGraphModel(model);
            parsed.Name = diagramElement.Attribute("name")?.Value;
            parsed.Id = diagramElement.Attribute("id")?.Value;
            diagrams.Add(parsed);
        }

        if (diagrams.Count == 0)
        {
            throw new FormatException("draw.io document contains no valid diagram.");
        }

        return new DrawIoFile { Diagrams = diagrams };
    }

    private static XElement? ResolveDiagramModel(XElement diagram)
    {
        var direct = diagram.Elements().FirstOrDefault(e => e.Name.LocalName == "mxGraphModel");
        if (direct is not null)
        {
            return direct;
        }

        // Compressed payload: base64( deflate( uri-encoded xml ) ).
        var payload = diagram.Value;
        if (string.IsNullOrWhiteSpace(payload))
        {
            return null;
        }

        try
        {
            var xml = Inflate(payload.Trim());
            var inner = XDocument.Parse(xml, LoadOptions.PreserveWhitespace);
            return inner.Root;
        }
        catch
        {
            return null;
        }
    }

    private static string Inflate(string base64)
    {
        var bytes = Convert.FromBase64String(base64);
        using var input = new MemoryStream(bytes);
        using var deflate = new DeflateStream(input, CompressionMode.Decompress);
        using var output = new MemoryStream();
        deflate.CopyTo(output);
        var raw = Encoding.UTF8.GetString(output.ToArray());
        // The inner XML is URI-encoded (percent-encoded) by draw.io.
        return Uri.UnescapeDataString(raw);
    }

    private static DrawIoDiagram ParseGraphModel(XElement model)
    {
        var cells = new Dictionary<string, DrawIoCell>(StringComparer.Ordinal);
        var ordered = new List<DrawIoCell>();
        var background = model.Attribute("background")?.Value;
        var pageWidth = ParseDouble(model.Attribute("pageWidth")?.Value, 0);
        var pageHeight = ParseDouble(model.Attribute("pageHeight")?.Value, 0);

        var root = model.Descendants().FirstOrDefault(e => e.Name.LocalName == "root")
            ?? throw new FormatException("mxGraphModel is missing a <root> element.");

        string? rootId = null;
        string? layerId = null;

        foreach (var node in root.Elements())
        {
            switch (node.Name.LocalName)
            {
                case "mxCell":
                {
                    var cell = ParseCell(node, null);
                    if (cell is null)
                    {
                        continue;
                    }

                    Register(cells, ordered, cell);
                    if (cell.Id == "0")
                    {
                        rootId = cell.Id;
                    }
                    else if (cell.Id == "1" && layerId is null)
                    {
                        layerId = cell.Id;
                    }

                    break;
                }
                case "object":
                case "UserObject":
                {
                    var cell = ParseCell(node, node);
                    if (cell is null)
                    {
                        continue;
                    }

                    Register(cells, ordered, cell);
                    break;
                }
            }
        }

        if (rootId is null && cells.ContainsKey("0"))
        {
            rootId = "0";
        }

        rootId ??= ordered.FirstOrDefault()?.Id ?? "0";
        layerId ??= ordered.FirstOrDefault(c => c.Id != rootId)?.Id ?? rootId;

        // Resolve parent/child relationships.
        foreach (var cell in ordered)
        {
            if (cell.Parent is { Length: > 0 } parentId &&
                cells.TryGetValue(parentId, out var parentCell))
            {
                parentCell.Children.Add(cell.Id);
            }
        }

        ResolveAbsoluteGeometry(cells, rootId);

        return new DrawIoDiagram
        {
            Cells = cells,
            RootId = rootId,
            LayerId = layerId,
            OrderedCells = ordered,
            Background = string.IsNullOrEmpty(background) || background == "none" ? null : background,
            PageWidth = pageWidth,
            PageHeight = pageHeight
        };
    }

    private static void Register(
        Dictionary<string, DrawIoCell> cells, List<DrawIoCell> ordered, DrawIoCell cell)
    {
        cells[cell.Id] = cell;
        ordered.Add(cell);
    }

    private static DrawIoCell? ParseCell(XElement node, XElement? wrapper)
    {
        var isWrapper = wrapper is not null;
        var cellElement = isWrapper
            ? node.Elements().FirstOrDefault(e => e.Name.LocalName == "mxCell")
            : node;

        if (cellElement is null)
        {
            return null;
        }

        // The id may live on the wrapping <object> element rather than the
        // inner <mxCell> (draw.io puts it there when the cell has attributes).
        var id = isWrapper
            ? wrapper!.Attribute("id")?.Value ?? cellElement.Attribute("id")?.Value
            : cellElement.Attribute("id")?.Value;
        if (string.IsNullOrEmpty(id))
        {
            return null;
        }

        var rawStyle = cellElement.Attribute("style")?.Value;
        // For <object label="..."> wrappers the label lives on the wrapper.
        var value = isWrapper
            ? wrapper!.Attribute("label")?.Value ?? cellElement.Attribute("value")?.Value ?? string.Empty
            : cellElement.Attribute("value")?.Value ?? string.Empty;

        var cell = new DrawIoCell
        {
            Id = id,
            Value = value,
            RawStyle = rawStyle,
            Style = DrawIoStyle.Parse(rawStyle),
            Parent = cellElement.Attribute("parent")?.Value,
            IsVertex = cellElement.Attribute("vertex")?.Value == "1",
            IsEdge = cellElement.Attribute("edge")?.Value == "1",
            Source = cellElement.Attribute("source")?.Value,
            Target = cellElement.Attribute("target")?.Value
        };

        if (isWrapper)
        {
            foreach (var attribute in wrapper!.Attributes())
            {
                if (attribute.Name.LocalName is "label" or "id")
                {
                    continue;
                }

                cell.Attributes[attribute.Name.LocalName] = attribute.Value;
            }

            // The wrapper label is the display value; if the inner mxCell also
            // carries a value, prefer the wrapper (draw.io behaviour).
            if (wrapper.Attribute("label") is { } label)
            {
                cell.Value = label.Value;
            }
        }

        var geometry = cellElement.Elements().FirstOrDefault(e => e.Name.LocalName == "mxGeometry");
        if (geometry is not null)
        {
            cell.Geometry = ParseGeometry(geometry);
        }

        return cell;
    }

    private static DrawIoGeometry ParseGeometry(XElement geometry)
    {
        var result = new DrawIoGeometry
        {
            X = ParseDouble(geometry.Attribute("x")?.Value, 0),
            Y = ParseDouble(geometry.Attribute("y")?.Value, 0),
            Width = ParseDouble(geometry.Attribute("width")?.Value, double.NaN),
            Height = ParseDouble(geometry.Attribute("height")?.Value, double.NaN),
            Relative = geometry.Attribute("relative")?.Value == "1"
        };

        var array = geometry.Elements().FirstOrDefault(e => e.Name.LocalName == "Array");
        if (array is not null)
        {
            foreach (var point in array.Elements().Where(e => e.Name.LocalName == "mxPoint"))
            {
                result.Waypoints.Add(new DrawIoPoint(
                    ParseDouble(point.Attribute("x")?.Value, 0),
                    ParseDouble(point.Attribute("y")?.Value, 0)));
            }
        }

        var offset = geometry.Elements().FirstOrDefault(e =>
            e.Name.LocalName == "mxPoint" &&
            string.Equals(e.Attribute("as")?.Value, "offset", StringComparison.Ordinal));
        if (offset is not null)
        {
            result.Offset = new DrawIoPoint(
                ParseDouble(offset.Attribute("x")?.Value, 0),
                ParseDouble(offset.Attribute("y")?.Value, 0));
        }

        return result;
    }

    /// <summary>
    /// Computes absolute coordinates: children with relative geometry are
    /// offset from their parent vertex; absolute children use their raw
    /// geometry but inherit the accumulated parent origin when nested.
    /// </summary>
    private static void ResolveAbsoluteGeometry(Dictionary<string, DrawIoCell> cells, string rootId)
    {
        foreach (var cell in cells.Values)
        {
            if (cell.Geometry is null)
            {
                continue;
            }

            double parentX = 0, parentY = 0;
            if (cell.Parent is { Length: > 0 } parentId &&
                parentId != rootId &&
                cells.TryGetValue(parentId, out var parent) &&
                parent.Geometry is not null)
            {
                parentX = parent.AbsoluteX;
                parentY = parent.AbsoluteY;
            }

            if (cell.Geometry.Relative)
            {
                cell.AbsoluteX = parentX + cell.Geometry.X;
                cell.AbsoluteY = parentY + cell.Geometry.Y;
            }
            else
            {
                cell.AbsoluteX = parentX + cell.Geometry.X;
                cell.AbsoluteY = parentY + cell.Geometry.Y;
            }
        }
    }

    private static double ParseDouble(string? value, double fallback)
    {
        if (value is not null &&
            double.TryParse(value, System.Globalization.NumberStyles.Float,
                System.Globalization.CultureInfo.InvariantCulture, out var d))
        {
            return d;
        }

        return fallback;
    }
}
