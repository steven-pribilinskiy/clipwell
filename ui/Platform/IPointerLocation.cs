using Avalonia;

namespace Clipwell.Ui.Platform;

/// <summary>
/// Reports the current mouse cursor position in physical screen pixels. Used to
/// open the picker at the cursor. Platform-specific (Windows done); when no
/// implementation is available the picker falls back to centering.
/// </summary>
public interface IPointerLocation
{
    bool TryGetCursor(out PixelPoint point);
}
