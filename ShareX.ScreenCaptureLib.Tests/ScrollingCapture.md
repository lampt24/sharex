# Scrolling capture target checks

Run the regular tests with:

```powershell
dotnet run --project ShareX.ScreenCaptureLib.Tests/ShareX.ScreenCaptureLib.Tests.csproj
```

Run the desktop integration fixture on an unlocked Windows desktop with:

```powershell
dotnet run --project ShareX.ScreenCaptureLib.Tests/ShareX.ScreenCaptureLib.Tests.csproj -- --scroll-target
```

The fixture opens a temporary WPF window with adjacent and nested scroll viewers.
It verifies live point-based resolution without a capture rectangle, parent panel
selection, clipping to the selected panel, vertical and
horizontal scrolling, returning to the start, isolation at the end of the inner
panel, and clearing the capture result when no captured pixels move. The window
closes automatically. A native control fixture also verifies wheel routing when
an adjacent control has keyboard focus and no ScrollPattern is available. Do not
cover these windows while the test runs.

Hover regression checks inject a blocked or failed UIA provider and verify that
the native preview remains selectable. They also verify that moving within a
panel does not discard the provider's result. These checks exercise the hover
session used by the live picker; UIA discovery must never gate drawing a frame.

Manual checks for the selection overlay and application-specific fallbacks:

1. Start a scrolling capture and move the pointer over the application. A live
   border follows the detected scroll panel; there is no frozen screenshot or
   rectangle drawing step. Content should keep animating while selecting.
2. Click inside the panel to capture. The selection click must not activate an
   underlying link or button. Enter selects at the pointer; Esc/right click cancels.
3. With two adjacent panels, select each separately. Only that panel should move.
4. With nested panels, hover inner content to select the inner panel. Tab moves to
   a parent scroll panel; Shift+Tab moves back. The border previews what will be
   captured. For custom controls without ScrollPattern, the native control bounds
   are used; these may be less precise than a supported scroll container.
5. Repeat for horizontal capture, mixed monitor scaling, and a monitor left of the
   primary monitor. Check that the point and capture border match physical pixels.
6. If already at the end, expect an explanatory no-movement message and no upload.
7. Cover the target with another window during capture; expect an error and no
   upload. Escape should stop capture without sending a selection click.

Custom application controls may ignore wheel/window messages. In that case the
capture reports no movement; it does not guess a different panel. UI Automation
providers can also be slow or unavailable. Excel retains its button fallback,
restricted to a visible horizontal scrollbar below the selected point.
