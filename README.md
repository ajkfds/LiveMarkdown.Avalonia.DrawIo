# LiveMarkdown.Avalonia.DrawIo

An extension package for `LiveMarkdown.Avalonia` that natively renders
[draw.io](https://www.drawio.com/) (mxGraph) diagrams, written in Markdown code
fences, as Avalonia controls **without a JavaScript runtime or WebView**.

The draw.io XML is parsed in C# and drawn directly via `DrawingContext`, using
the geometry and style information embedded in the diagram.

## Features

- 🔌 **Markdown integration** — Detects ` ```drawio ` fences and renders the embedded draw.io XML.
- 🧩 **Native rendering** — No JS / WebView required. Runs on pure C# + Avalonia only.
- 📄 **Multi-format input** — Supports both plain `<mxGraphModel>` XML and the default `<mxfile>` wrapper, including the compressed (base64 + deflate) `<diagram>` payload.
- 🎨 **Shape support** — Rectangles, rounded rectangles, ellipses, triangles, and generic polygons (`mxgraph.basic.polygon` / `polyCoords`, with optional quadratic curves).
- 🏷️ **Rich labels** — HTML labels with `<b>`/`<i>`/`<span style="color:…">`/`<br>` markup and `%Placeholder%` attribute substitution.
- ➡️ **Edges** — Orthogonal/straight routing through waypoints, with classic/open arrowheads and edge labels.
- 🔄 **Streaming support** — Shows the source text while the fence is open, and switches to the rendered diagram once it is closed.

## Installation

```bash
dotnet add package LiveMarkdown.Avalonia.DrawIo
```

The `LiveMarkdown.Avalonia` core package is required (this package depends on it).

## Adding to LiveMarkdown

To enable draw.io, perform the following **three** steps.

### 1. Register the extension in the pipeline

```csharp
using LiveMarkdown.Avalonia;

MarkdownRenderer.ConfigurePipeline += pipeline => pipeline
    .UseDrawIo();
```

### 2. Register the node factory

```csharp
MarkdownNode.Edit(builder => builder
    .Register<DrawIoBlockNode>()
);
```

### 3. Load the styles

```xml
<Application.Styles>
    <StyleInclude Source="avares://LiveMarkdown.Avalonia/Styles.axaml"/>
    <StyleInclude Source="avares://LiveMarkdown.Avalonia.DrawIo/Styles.axaml"/>
</Application.Styles>
```

## Usage

Paste the draw.io XML (via **Extras → Edit Diagram…** in draw.io) inside a
` ```drawio ` fence.

````markdown
```drawio
<mxfile>
  <diagram name="Page-1" id="abc">
    <mxGraphModel dx="1422" dy="798" grid="1" gridSize="10" ...>
      <root>
        <mxCell id="0" />
        <mxCell id="1" parent="0" />
        <mxCell id="2" value="Hello" style="rounded=1;fillColor=#dae8fc;strokeColor=#6c8ebf;" vertex="1" parent="1">
          <mxGeometry x="120" y="80" width="120" height="60" as="geometry" />
        </mxCell>
      </root>
    </mxGraphModel>
  </diagram>
</mxfile>
```
````

## Customization

`DrawIoPresenter` exposes styleable properties through the selector
`md|DrawIoPresenter`.

| Property | Description |
| --- | --- |
| `Text` | The draw.io XML string to render. |
| `Foreground` | Default color for labels and outlines that do not specify a color. |
| `FontFamily` / `FontSize` | Font for text in the diagram. |
| `BackgroundBrush` | Background of the whole diagram (transparent by default). |
| `Scale` | Zoom/scale applied when rendering the diagram. |

## Limitations

- Only the **first** `<diagram>` page of an `<mxfile>` is rendered.
- Only the core draw.io shape vocabulary is supported (rectangles, rounded
  rectangles, ellipses, triangles, polygons). Exotic stencil shapes fall back to
  a plain rectangle.
- Edge routing uses the stored waypoints; automatic orthogonal/curved routing is
  not re-computed.
- Images, gradients, shadows and rotation on shapes are not rendered.

## License

Apache-2.0

## References

- Repository: https://github.com/DearVa/LiveMarkdown.Avalonia
- Official draw.io: https://www.drawio.com/
