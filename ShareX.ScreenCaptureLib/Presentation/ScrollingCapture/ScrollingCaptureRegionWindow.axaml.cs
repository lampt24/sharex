#region License Information (GPL v3)

/*
    ShareX - A program that allows you to take screenshots and share any file type
    Copyright (c) 2007-2026 ShareX Team

    This program is free software; you can redistribute it and/or
    modify it under the terms of the GNU General Public License
    as published by the Free Software Foundation; either version 2
    of the License, or (at your option) any later version.

    This program is distributed in the hope that it will be useful,
    but WITHOUT ANY WARRANTY; without even the implied warranty of
    MERCHANTABILITY or FITNESS FOR A PARTICULAR PURPOSE.  See the
    GNU General Public License for more details.

    You should have received a copy of the GNU General Public License
    along with this program; if not, write to the Free Software
    Foundation, Inc., 51 Franklin Street, Fifth Floor, Boston, MA  02110-1301, USA.
*/

#endregion License Information (GPL v3)

#nullable enable

using Avalonia;
using Avalonia.Controls;
using Avalonia.Markup.Xaml;
using Avalonia.Threading;
using ShareX.HelpersLib;
using System;
using System.Runtime.InteropServices;
using DrawingRectangle = System.Drawing.Rectangle;

namespace ShareX.ScreenCaptureLib;

public partial class ScrollingCaptureRegionWindow : Window
{
    private const int BorderPixels = 1;
    private const int RegionDiff = 4;

    private readonly DrawingRectangle _regionRectangle;
    private readonly int _frameWidth;
    private readonly int _frameHeight;
    private double _windowScaling = 1;

    public ScrollingCaptureRegionWindow()
        : this(new DrawingRectangle(0, 0, 640, 420))
    {
    }

    public ScrollingCaptureRegionWindow(DrawingRectangle regionRectangle)
    {
        _regionRectangle = regionRectangle;
        _frameWidth = regionRectangle.Width + BorderPixels * 2;
        _frameHeight = regionRectangle.Height + BorderPixels * 2;

        InitializeComponent();
        Position = new PixelPoint(
            regionRectangle.X - BorderPixels,
            regionRectangle.Y - BorderPixels);
        ConfigureGeometry(1);

        Opened += OnOpened;
    }

    private void InitializeComponent()
    {
        AvaloniaXamlLoader.Load(this);
    }

    private void OnOpened(object? sender, EventArgs e)
    {
        ConfigureGeometry(GetScreenScaling());
        ApplyClickThroughToolWindowStyle();
        ApplyNativeFrameRegion();
        Dispatcher.UIThread.Post(ApplyNativeFrameRegion, DispatcherPriority.Loaded);
    }

    private void ConfigureGeometry(double scaling)
    {
        _windowScaling = Math.Max(0.5, scaling);
        Width = _frameWidth / _windowScaling;
        Height = _frameHeight / _windowScaling;
    }

    private double GetScreenScaling()
    {
        // Position is one pixel outside the region. When the region touches a
        // monitor edge it belongs to the neighbouring monitor and its scaling
        // would size the window incorrectly, clipping the right/bottom border.
        PixelPoint center = new(
            _regionRectangle.X + _regionRectangle.Width / 2,
            _regionRectangle.Y + _regionRectangle.Height / 2);
        return Screens.ScreenFromPoint(center)?.Scaling ?? Screens.Primary?.Scaling ?? 1;
    }

    private void ApplyClickThroughToolWindowStyle()
    {
        IntPtr handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        WindowInfo info = new(handle);
        info.ExStyle |= WindowStyles.WS_EX_TRANSPARENT | WindowStyles.WS_EX_TOOLWINDOW;
    }

    private void ApplyNativeFrameRegion()
    {
        IntPtr handle = TryGetPlatformHandle()?.Handle ?? IntPtr.Zero;
        if (handle == IntPtr.Zero)
        {
            return;
        }

        IntPtr frameRegion = CreateRectRgn(0, 0, _frameWidth, _frameHeight);
        IntPtr apertureRegion = CreateRectRgn(
            BorderPixels,
            BorderPixels,
            _frameWidth - BorderPixels,
            _frameHeight - BorderPixels);

        if (frameRegion == IntPtr.Zero || apertureRegion == IntPtr.Zero)
        {
            DeleteRegion(frameRegion);
            DeleteRegion(apertureRegion);
            return;
        }

        CombineRgn(frameRegion, frameRegion, apertureRegion, RegionDiff);
        DeleteObject(apertureRegion);

        if (SetWindowRgn(handle, frameRegion, true) == 0)
        {
            DeleteObject(frameRegion);
        }
    }

    private static void DeleteRegion(IntPtr region)
    {
        if (region != IntPtr.Zero)
        {
            DeleteObject(region);
        }
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateRectRgn(int left, int top, int right, int bottom);

    [DllImport("gdi32.dll")]
    private static extern int CombineRgn(IntPtr destination, IntPtr source1, IntPtr source2, int combineMode);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);

    [DllImport("user32.dll")]
    private static extern int SetWindowRgn(IntPtr windowHandle, IntPtr regionHandle, bool redraw);
}
