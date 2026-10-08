using System.Collections.Generic;
using System.Linq;
using Avalonia;
using Avalonia.Automation;
using Avalonia.Automation.Peers;
using Avalonia.Automation.Provider;
using Avalonia.Threading;

namespace GithubMarkdownViewer.Preview;

/// <summary>
/// Describes the preview to screen readers. The control draws everything itself, so without this peer
/// assistive technology would see one empty element. Children are built on demand, so nothing is
/// created unless an assistive technology asks.
/// </summary>
public sealed class MarkdownPreviewAutomationPeer : ControlAutomationPeer
{
    private readonly MarkdownPreviewControl _owner;

    public MarkdownPreviewAutomationPeer(MarkdownPreviewControl owner) : base(owner)
    {
        _owner = owner;
        owner.LayoutUpdated += OnLayoutUpdated;
    }

    // The layout changes during measure, so report the change after it finishes.
    private void OnLayoutUpdated() => Dispatcher.UIThread.Post(InvalidateChildren, DispatcherPriority.Background);

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Document;

    protected override string? GetNameCore()
    {
        var name = AutomationProperties.GetName(_owner);
        return string.IsNullOrWhiteSpace(name) ? "Markdown preview" : name;
    }

    protected override string GetClassNameCore() => nameof(MarkdownPreviewControl);

    protected override IReadOnlyList<AutomationPeer>? GetChildrenCore()
    {
        var layout = _owner.GetLayout();
        if (layout == null) return null;

        return AutomationModel.Build(layout)
            .Select(block => (AutomationPeer)new PreviewBlockPeer(_owner, block))
            .ToList();
    }
}

/// <summary>Shared behaviour for the elements inside the preview. They are snapshots, so they never touch a disposed layout.</summary>
internal abstract class PreviewElementPeer : AutomationPeer
{
    private AutomationPeer? _parent;

    protected PreviewElementPeer(MarkdownPreviewControl owner, Rect contentBounds, string automationId)
    {
        Owner = owner;
        ContentBounds = contentBounds;
        AutomationId = automationId;
    }

    protected MarkdownPreviewControl Owner { get; }
    protected Rect ContentBounds { get; }
    protected string AutomationId { get; }

    internal void AttachParent(AutomationPeer parent) => _parent = parent;

    protected override bool TrySetParent(AutomationPeer? parent)
    {
        _parent = parent;
        return true;
    }

    protected override AutomationPeer? GetParentCore() => _parent;
    protected override string GetAutomationIdCore() => AutomationId;
    protected override Rect GetBoundingRectangleCore() => Owner.ContentRectToRoot(ContentBounds);
    protected override bool IsOffscreenCore() => !Owner.IsContentRectVisible(ContentBounds);
    protected override void BringIntoViewCore() => Owner.BringContentRectIntoView(ContentBounds);

    protected override string? GetAcceleratorKeyCore() => null;
    protected override string? GetAccessKeyCore() => null;
    protected override AutomationPeer? GetLabeledByCore() => null;
    protected override bool HasKeyboardFocusCore() => false;
    protected override bool IsContentElementCore() => true;
    protected override bool IsControlElementCore() => true;
    protected override bool IsEnabledCore() => Owner.IsEffectivelyEnabled;
    protected override bool IsKeyboardFocusableCore() => false;
    protected override void SetFocusCore() => Owner.Focus();
    protected override bool ShowContextMenuCore() => false;
}

/// <summary>A paragraph, heading, list item, code block, or table cell.</summary>
internal sealed class PreviewBlockPeer : PreviewElementPeer
{
    private readonly BlockInfo _block;
    private readonly IReadOnlyList<AutomationPeer> _children;

    public PreviewBlockPeer(MarkdownPreviewControl owner, BlockInfo block)
        : base(owner, block.Bounds, $"block-{block.Index}")
    {
        _block = block;
        _children = block.Links
            .Select((link, i) => new PreviewLinkPeer(owner, link, $"block-{block.Index}-link-{i}", this))
            .Cast<AutomationPeer>()
            .ToList();
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Text;
    protected override string? GetNameCore() => _block.Name;
    protected override int GetHeadingLevelCore() => _block.HeadingLevel;

    protected override string? GetHelpTextCore() => _block.Kind == BlockKind.Code ? "Code block" : null;

    protected override string GetClassNameCore() => _block.Kind switch
    {
        BlockKind.Heading => "PreviewHeading",
        BlockKind.Code => "PreviewCode",
        BlockKind.TableCell => "PreviewTableCell",
        _ => "PreviewText",
    };

    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => _children;
}

/// <summary>A link. Invoking it behaves like clicking it.</summary>
internal sealed class PreviewLinkPeer : PreviewElementPeer, IInvokeProvider
{
    private readonly LinkInfo _link;

    public PreviewLinkPeer(MarkdownPreviewControl owner, LinkInfo link, string automationId, AutomationPeer parent)
        : base(owner, link.Bounds, automationId)
    {
        _link = link;
        AttachParent(parent);
    }

    protected override AutomationControlType GetAutomationControlTypeCore() => AutomationControlType.Hyperlink;
    protected override string? GetNameCore() => _link.Text;
    protected override string? GetHelpTextCore() => _link.Url;
    protected override string GetClassNameCore() => "PreviewLink";
    protected override IReadOnlyList<AutomationPeer> GetOrCreateChildrenCore() => System.Array.Empty<AutomationPeer>();
    protected override bool IsKeyboardFocusableCore() => false;

    public void Invoke() => Dispatcher.UIThread.Post(() => Owner.ActivateLink(_link.Url));
}
