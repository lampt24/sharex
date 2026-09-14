using ShareX.ScreenCaptureLib;
using System;
using System.Drawing;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Threading;
using WpfPoint = System.Windows.Point;

internal static class ScrollingCaptureTargetTests
{
    // Opt-in desktop integration test. UI Automation runs on the caller's MTA,
    // while the real controls run on a separate STA with a live message loop.
    public static void Run()
    {
        var ready = new TaskCompletionSource<(Window Window, ScrollViewer Left, ScrollViewer Right, ScrollViewer Inner)>();
        Thread thread = new(() =>
        {
            try
            {
                ScrollViewer left = CreateScroller();
                ScrollViewer right = CreateScroller();
                ScrollViewer inner = CreateScroller();
                inner.Width = 210;
                inner.Height = 160;
                inner.HorizontalAlignment = HorizontalAlignment.Left;
                inner.VerticalAlignment = VerticalAlignment.Top;
                var outerContent = new Grid { Width = 1200, Height = 1600 };
                outerContent.Children.Add(inner);
                right.Content = outerContent;
                Grid grid = new();
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.ColumnDefinitions.Add(new ColumnDefinition());
                grid.Children.Add(left);
                Grid.SetColumn(right, 1);
                grid.Children.Add(right);
                Window window = new()
                {
                    Title = "ShareX scroll target integration fixture",
                    Width = 640, Height = 440, Left = 100, Top = 100,
                    Content = grid, Topmost = true
                };
                window.ContentRendered += (_, _) => ready.TrySetResult((window, left, right, inner));
                window.Show();
                Dispatcher.Run();
            }
            catch (Exception ex) { ready.TrySetException(ex); }
        });
        thread.SetApartmentState(ApartmentState.STA);
        thread.IsBackground = true;
        thread.Start();
        var fixture = ready.Task.WaitAsync(TimeSpan.FromSeconds(10)).GetAwaiter().GetResult();
        try
        {
            Thread.Sleep(250);
            var geometry = fixture.Window.Dispatcher.Invoke(() =>
            {
                WpfPoint point = fixture.Inner.PointToScreen(new WpfPoint(70, 70));
                WpfPoint origin = fixture.Window.PointToScreen(new WpfPoint(0, 0));
                return (Point: new System.Drawing.Point((int)point.X, (int)point.Y),
                    Bounds: new Rectangle((int)origin.X, (int)origin.Y, 640, 440));
            });
            var target = ScrollingCaptureTarget.Resolve(geometry.Point, geometry.Bounds, ScrollingCaptureDirection.Vertical);
            var liveTarget = ScrollingCaptureTarget.ResolveAtPoint(geometry.Point, ScrollingCaptureDirection.Vertical);
            Check(liveTarget.SupportsAutomation && liveTarget.Bounds.Contains(geometry.Point),
                "Live selection must detect a scroll panel without a preselected rectangle.");
            var nativeBounds = ScrollingCaptureTarget.ResolveNativeAtPoint(geometry.Point, ScrollingCaptureDirection.Horizontal).Bounds;
            Check(System.Windows.Forms.Screen.FromPoint(geometry.Point).Bounds.Contains(nativeBounds),
                "Hover bounds must stay on the pointer's monitor so the frame uses a single DPI scale.");
            var parentTarget = ScrollingCaptureTarget.ResolveAtPoint(geometry.Point, ScrollingCaptureDirection.Vertical, 1);
            // An unresponsive UIA provider must not block hover feedback or click
            // selection. A small pointer movement must still accept panel bounds.
            var blockedProvider = new TaskCompletionSource<ScrollingCaptureTarget>();
            var nativeTarget = ScrollingCaptureTarget.ResolveNativeAtPoint(geometry.Point, ScrollingCaptureDirection.Vertical);
            var hover = new ScrollingCaptureHoverSession(_ => nativeTarget, (_, _) => blockedProvider.Task);
            Check(hover.Update(geometry.Point, 0) != null,
                "Hover must return a selectable native preview while UIA is blocked.");
            var movedPoint = new System.Drawing.Point(geometry.Point.X + 1, geometry.Point.Y + 1);
            blockedProvider.SetResult(liveTarget);
            Check(hover.Update(movedPoint, 0).Bounds == liveTarget.Bounds,
                "A UIA result must survive pointer movement inside the same panel.");
            var otherPanelPoint = new System.Drawing.Point(geometry.Bounds.Right - 40, geometry.Bounds.Bottom - 40);
            Check(hover.Update(otherPanelPoint, 0) == null,
                "Moving to another panel of a resolved control must not frame the whole window.");
            var failedHover = new ScrollingCaptureHoverSession(_ => nativeTarget,
                (_, _) => Task.FromException<ScrollingCaptureTarget>(new InvalidOperationException("Provider unavailable")));
            failedHover.Update(geometry.Point, 0);
            Check(failedHover.Update(geometry.Point, 0) != null,
                "A provider failure must leave native hover selection available.");
            Check(parentTarget.Bounds.Width > liveTarget.Bounds.Width,
                "Parent selection must highlight the outer scroll panel.");
            parentTarget.Scroll(1);
            fixture.Window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            fixture.Window.Dispatcher.Invoke(() =>
            {
                Check(fixture.Right.VerticalOffset > 0 && fixture.Inner.VerticalOffset == 0,
                    $"Selecting the parent must scroll the outer panel only (outer={fixture.Right.VerticalOffset}, inner={fixture.Inner.VerticalOffset}).");
            });
            parentTarget.ScrollToStart();
            fixture.Window.Dispatcher.Invoke(() => { }, DispatcherPriority.ApplicationIdle);
            Check(target.SupportsAutomation, "Nested panel must expose ScrollPattern.");
            Check(target.Bounds.Width < geometry.Bounds.Width / 2, "Capture must clip to the chosen nested panel.");
            target.Scroll(2);
            fixture.Window.Dispatcher.Invoke(() =>
            {
                Check(fixture.Inner.VerticalOffset > 0, "Chosen nested panel must scroll.");
                Check(fixture.Left.VerticalOffset == 0 && fixture.Right.VerticalOffset == 0,
                    "Sibling and outer panel must remain stationary.");
            });
            target.ScrollToStart();
            fixture.Window.Dispatcher.Invoke(() => Check(fixture.Inner.VerticalOffset == 0, "Scroll-to-start must target the nested panel."));
            var horizontal = ScrollingCaptureTarget.Resolve(geometry.Point, geometry.Bounds, ScrollingCaptureDirection.Horizontal);
            Check(horizontal.SupportsAutomation, "Horizontal scroll target must resolve.");
            horizontal.Scroll(2);
            fixture.Window.Dispatcher.Invoke(() =>
            {
                Check(fixture.Inner.HorizontalOffset > 0, "Chosen nested panel must scroll horizontally.");
                Check(fixture.Right.HorizontalOffset == 0 && fixture.Left.HorizontalOffset == 0,
                    "Horizontal scrolling must not affect adjacent or outer panels.");
                fixture.Inner.ScrollToBottom();
            });
            target.Scroll(2);
            fixture.Window.Dispatcher.Invoke(() => Check(fixture.Right.VerticalOffset == 0,
                "Reaching the inner panel end must not scroll its parent."));
            // The capture region intentionally contains only uniform content. Even
            // if a scrollbar moves, unchanged captured pixels must not be uploaded
            // as a successful scrolling screenshot.
            var unchangedTarget = ScrollingCaptureTarget.Resolve(geometry.Point,
                new Rectangle(geometry.Point.X - 10, geometry.Point.Y - 10, 20, 20),
                ScrollingCaptureDirection.Vertical);
            using (var manager = new ScrollingCaptureManager(new ScrollingCaptureOptions
            {
                ShowRegion = false, StartDelay = 50, ScrollDelay = 50, AutoUpload = true
            }))
            {
                manager.SetTarget(unchangedTarget);
                bool failed = false;
                try { manager.StartCapture().GetAwaiter().GetResult(); }
                catch (InvalidOperationException) { failed = true; }
                Check(failed, "An unchanged capture must report a failure.");
                Check(manager.Result == null && !manager.IsCapturing,
                    "Failed preflight must clear the result and capture state.");
            }
            RunNativeFallback(fixture.Window.Dispatcher);
            Console.WriteLine("Scrolling target desktop integration tests passed (siblings, nested, horizontal, end, scroll-to-start).");
        }
        finally
        {
            fixture.Window.Dispatcher.Invoke(() =>
            {
                fixture.Window.Close();
                Dispatcher.CurrentDispatcher.BeginInvokeShutdown(DispatcherPriority.Normal);
            });
            thread.Join(5000);
        }
    }

    private static void RunNativeFallback(Dispatcher dispatcher)
    {
        var fixture = dispatcher.Invoke(() =>
        {
            var form = new System.Windows.Forms.Form
            {
                Text = "ShareX native wheel routing fixture", TopMost = true,
                StartPosition = System.Windows.Forms.FormStartPosition.Manual,
                Bounds = new Rectangle(150, 150, 500, 300)
            };
            var left = new WheelControl { Bounds = new Rectangle(0, 0, 220, 240) };
            var right = new WheelControl { Bounds = new Rectangle(230, 0, 220, 240) };
            form.Controls.Add(left);
            form.Controls.Add(right);
            form.Show();
            right.Focus();
            return (Form: form, Left: left, Right: right,
                Point: left.PointToScreen(new System.Drawing.Point(50, 50)));
        });
        try
        {
            var bounds = new Rectangle(fixture.Point.X - 20, fixture.Point.Y - 20, 40, 40);
            var target = ScrollingCaptureTarget.Resolve(fixture.Point, bounds, ScrollingCaptureDirection.Vertical);
            Check(!target.SupportsAutomation, "Custom native fixture must exercise the non-UIA fallback.");
            target.PositionPointer();
            target.ScrollVerticalWheel(2);
            target.ScrollHorizontalWheel();
            dispatcher.Invoke(() =>
            {
                Check(fixture.Left.VerticalDelta == -240 && fixture.Left.HorizontalDelta == 120,
                    "Fallback must send wheel deltas to the selected native control.");
                Check(fixture.Right.VerticalDelta == 0 && fixture.Right.HorizontalDelta == 0,
                    "Focused sibling must not receive the selected control's wheel input.");
            });
        }
        finally { dispatcher.Invoke(fixture.Form.Dispose); }
    }

    private sealed class WheelControl : System.Windows.Forms.Control
    {
        public int VerticalDelta;
        public int HorizontalDelta;

        protected override void WndProc(ref System.Windows.Forms.Message message)
        {
            int delta = unchecked((short)(message.WParam.ToInt64() >> 16));
            if (message.Msg == 0x020A) { VerticalDelta += delta; return; }
            if (message.Msg == 0x020E) { HorizontalDelta += delta; return; }
            base.WndProc(ref message);
        }
    }

    private static ScrollViewer CreateScroller() => new()
    {
        HorizontalScrollBarVisibility = ScrollBarVisibility.Visible,
        VerticalScrollBarVisibility = ScrollBarVisibility.Visible,
        Content = new System.Windows.Controls.Border
        {
            Width = 1200, Height = 1600,
            Background = System.Windows.Media.Brushes.LightBlue
        }
    };

    private static void Check(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }
}
