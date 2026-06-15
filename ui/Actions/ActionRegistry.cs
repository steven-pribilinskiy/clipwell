using System.Collections.Generic;
using System.Linq;
using Clipwell.Protocol;
using Clipwell.Protocol.Plugins;

namespace Clipwell.Ui.Actions;

/// <summary>
/// Holds the available <see cref="IClipAction"/>s (built-ins now; plugin actions
/// merge in later — see the plugin host) and returns those applicable to an item.
/// </summary>
public sealed class ActionRegistry
{
    private readonly List<IClipAction> _actions;

    public ActionRegistry(IEnumerable<IClipAction>? extra = null)
    {
        _actions =
        [
            new OpenUrlAction(),
            new OpenPathAction(),
            new CopyTextAction(),
            new CopyHostAction(),
        ];
        if (extra is not null) _actions.AddRange(extra);
    }

    public IReadOnlyList<IClipAction> ActionsFor(ClipItem item) =>
        _actions.Where(a => a.AppliesTo(item)).ToList();
}
