using System;
using System.Drawing;
using System.Threading.Tasks;

namespace ShareX.ScreenCaptureLib;

// Keep hit testing and selection independent of potentially slow UIA providers.
internal sealed class ScrollingCaptureHoverSession
{
    private readonly Func<Point, ScrollingCaptureTarget> nativeResolver;
    private readonly Func<Point, int, Task<ScrollingCaptureTarget>> automationResolver;
    private Task<ScrollingCaptureTarget> discovery;
    private ScrollingCaptureTarget refinedTarget;
    private int discoveryParent;
    private int refinedParent;

    public ScrollingCaptureHoverSession(Func<Point, ScrollingCaptureTarget> nativeResolver,
        Func<Point, int, Task<ScrollingCaptureTarget>> automationResolver)
    {
        this.nativeResolver = nativeResolver;
        this.automationResolver = automationResolver;
    }

    public ScrollingCaptureTarget Update(Point point, int parentIndex)
    {
        ScrollingCaptureTarget native = nativeResolver(point);
        if (discovery?.IsCompleted == true)
        {
            // Observe faults without stopping selection of native controls.
            refinedTarget = discovery.Status == TaskStatus.RanToCompletion ? discovery.Result : null;
            _ = discovery.Exception;
            refinedParent = discoveryParent;
            discovery = null;
        }

        if (native == null) return null;
        bool sameControl = refinedTarget != null && refinedTarget.Handle == native.Handle;
        ScrollingCaptureTarget target;
        if (sameControl && refinedParent == parentIndex && refinedTarget.Bounds.Contains(point))
            target = refinedTarget.AtPoint(point);
        // UIA already described this control, so its native bounds are coarser than a
        // panel (Chromium uses one HWND per window). Show nothing until the new panel resolves.
        else if (sameControl) target = null;
        else target = native;

        if (discovery == null)
        {
            discoveryParent = parentIndex;
            discovery = automationResolver(point, parentIndex);
        }
        return target;
    }
}
