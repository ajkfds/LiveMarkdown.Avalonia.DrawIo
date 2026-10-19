using Avalonia.Controls;

namespace LiveMarkdown.Avalonia;

/// <summary>
/// Markdown block node that renders a <c>```drawio</c> fence as a draw.io
/// (mxGraph) diagram, or as plain text while the fence is still open.
/// </summary>
public class DrawIoBlockNode : BlockNode<DrawIoCodeBlock>
{
    /// <inheritdoc/>
    public override Control Control { get; }

    private readonly MarkdownTextBlock _textBlock;
    private readonly DrawIoPresenter _presenter;

    /// <summary>Initializes a new instance of the <see cref="DrawIoBlockNode"/> class.</summary>
    public DrawIoBlockNode()
    {
        Control = new Panel
        {
            Classes = { "DrawIoBlock" },
            Children =
            {
                (_textBlock = new MarkdownTextBlock
                {
                    Classes = { "DrawIoBlock" }
                }),
                (_presenter = new DrawIoPresenter
                {
                    Classes = { "DrawIoBlock" }
                })
            }
        };
    }

    /// <inheritdoc/>
    protected override bool UpdateCore(
        DocumentNode documentNode,
        DrawIoCodeBlock drawIo,
        in ObservableStringBuilderChangedEventArgs change,
        CancellationToken cancellationToken)
    {
        if (drawIo.IsOpen)
        {
            _presenter.IsVisible = false;
            _textBlock.Text = drawIo.Lines.ToString();
            _textBlock.IsVisible = true;
        }
        else
        {
            _textBlock.IsVisible = false;
            _presenter.Text = drawIo.Lines.ToString();
            _presenter.IsVisible = true;
        }

        return true;
    }
}
