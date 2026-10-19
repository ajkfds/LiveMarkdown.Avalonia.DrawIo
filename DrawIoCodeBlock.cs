using Markdig;
using Markdig.Parsers;
using Markdig.Renderers;
using Markdig.Syntax;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Markdig fenced code block produced by <see cref="DrawIoBlockParser"/> for
/// <c>```drawio</c> fences.
/// </summary>
public class DrawIoCodeBlock(BlockParser parser) : FencedCodeBlock(parser);

/// <summary>
/// Block parser that recognises <c>```drawio</c> fences and turns them into
/// <see cref="DrawIoCodeBlock"/> instances.
/// </summary>
public class DrawIoBlockParser : FencedBlockParserBase<DrawIoCodeBlock>
{
    /// <summary>Initializes a new instance of the <see cref="DrawIoBlockParser"/> class.</summary>
    public DrawIoBlockParser()
    {
        OpeningCharacters = ['`', '~'];
        InfoPrefix = "drawio";
    }

    /// <inheritdoc/>
    public override BlockState TryOpen(BlockProcessor processor)
    {
        if (!IsDrawIoFenceOpening(processor.Line.AsSpan()))
        {
            return BlockState.None;
        }

        return base.TryOpen(processor);
    }

    /// <inheritdoc/>
    protected override DrawIoCodeBlock CreateFencedBlock(BlockProcessor processor)
    {
        return new DrawIoCodeBlock(this);
    }

    private static bool IsDrawIoFenceOpening(ReadOnlySpan<char> line)
    {
        line = line.TrimStart();
        if (line.Length < 4 || line[0] is not ('`' or '~'))
        {
            return false;
        }

        var fence = line[0];
        var index = 0;
        while (index < line.Length && line[index] == fence)
        {
            index++;
        }

        if (index < 3)
        {
            return false;
        }

        var info = line[index..].TrimStart();
        if (info.Length == 0)
        {
            return false;
        }

        var tokenEnd = 0;
        while (tokenEnd < info.Length && !char.IsWhiteSpace(info[tokenEnd]))
        {
            tokenEnd++;
        }

        return info[..tokenEnd].Equals("drawio", StringComparison.OrdinalIgnoreCase);
    }
}

/// <summary>Markdown extension that enables the <c>drawio</c> fenced code block.</summary>
public class DrawIoExtension : IMarkdownExtension
{
    /// <inheritdoc/>
    public void Setup(MarkdownPipelineBuilder pipeline)
    {
        if (!pipeline.BlockParsers.Contains<DrawIoBlockParser>())
        {
            pipeline.BlockParsers.Insert(0, new DrawIoBlockParser());
        }
    }

    /// <inheritdoc/>
    public void Setup(MarkdownPipeline pipeline, IMarkdownRenderer renderer)
    {
    }
}

/// <summary>Extension methods for registering the draw.io markdown extension.</summary>
public static class DrawIoExtensionBuilder
{
    /// <summary>Enables rendering of <c>```drawio</c> fenced code blocks.</summary>
    public static MarkdownPipelineBuilder UseDrawIo(this MarkdownPipelineBuilder pipeline)
    {
        pipeline.Extensions.AddIfNotAlready<DrawIoExtension>();
        return pipeline;
    }
}
