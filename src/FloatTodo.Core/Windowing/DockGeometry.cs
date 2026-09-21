using System;
using System.Collections.Generic;
using FloatTodo.Core.Models;

namespace FloatTodo.Core.Windowing;

public readonly record struct PixelPoint(int X, int Y);

public readonly record struct PixelRect(int Left, int Top, int Right, int Bottom)
{
    public int Width => Right - Left;
    public int Height => Bottom - Top;

    public bool Contains(PixelPoint pt) =>
        pt.X >= Left && pt.X <= Right && pt.Y >= Top && pt.Y <= Bottom;

    public bool IntersectsWith(PixelRect other) =>
        Left < other.Right && Right > other.Left && Top < other.Bottom && Bottom > other.Top;

    public static PixelRect FromXYWH(int x, int y, int width, int height) =>
        new(x, y, x + width, y + height);
}

public sealed class MonitorDescriptor
{
    public IntPtr Handle { get; init; }
    public PixelRect MonitorArea { get; init; }
    public PixelRect WorkArea { get; init; }
    public double DpiScaleX { get; init; } = 1.0;
    public double DpiScaleY { get; init; } = 1.0;
    public bool IsPrimary { get; init; }
}

public readonly record struct DockPlacementResult(
    PixelRect TargetRect,
    DockSide TargetDockSide,
    MonitorDescriptor TargetMonitor
);

public static class DockGeometry
{
    public const double DefaultSnapThresholdDip = 16.0;
    public const double DefaultUnsnapThresholdDip = 28.0;

    /// <summary>
    /// Select the best monitor based on cursor position or window intersection.
    /// </summary>
    public static MonitorDescriptor SelectTargetMonitor(
        PixelPoint cursor,
        PixelRect windowRect,
        IReadOnlyList<MonitorDescriptor> monitors)
    {
        if (monitors == null || monitors.Count == 0)
        {
            throw new ArgumentException("Monitors collection must not be empty.", nameof(monitors));
        }

        // 1. Point hit
        foreach (var m in monitors)
        {
            if (m.MonitorArea.Contains(cursor))
            {
                return m;
            }
        }

        // 2. Intersect area with window candidate
        MonitorDescriptor? bestMonitor = null;
        long bestArea = -1;

        foreach (var m in monitors)
        {
            int intersectLeft = Math.Max(windowRect.Left, m.MonitorArea.Left);
            int intersectTop = Math.Max(windowRect.Top, m.MonitorArea.Top);
            int intersectRight = Math.Min(windowRect.Right, m.MonitorArea.Right);
            int intersectBottom = Math.Min(windowRect.Bottom, m.MonitorArea.Bottom);

            if (intersectRight > intersectLeft && intersectBottom > intersectTop)
            {
                long area = (long)(intersectRight - intersectLeft) * (intersectBottom - intersectTop);
                if (area > bestArea)
                {
                    bestArea = area;
                    bestMonitor = m;
                }
            }
        }

        if (bestMonitor != null)
        {
            return bestMonitor;
        }

        // 3. Fallback to primary or first
        foreach (var m in monitors)
        {
            if (m.IsPrimary) return m;
        }
        return monitors[0];
    }

    /// <summary>
    /// Computes candidate placement using hysteresis-based magnetic snapping.
    /// </summary>
    public static DockPlacementResult CalculateDockPlacement(
        PixelPoint cursor,
        PixelPoint cursorDragAnchorOffset,
        int windowWidth,
        int windowHeight,
        MonitorDescriptor monitor,
        DockSide currentDockSide,
        double snapThresholdDip = DefaultSnapThresholdDip,
        double unsnapThresholdDip = DefaultUnsnapThresholdDip)
    {
        int snapPx = (int)Math.Round(snapThresholdDip * monitor.DpiScaleX);
        int unsnapPx = (int)Math.Round(unsnapThresholdDip * monitor.DpiScaleX);

        int naturalLeft = cursor.X - cursorDragAnchorOffset.X;
        int naturalTop = cursor.Y - cursorDragAnchorOffset.Y;
        int naturalRight = naturalLeft + windowWidth;
        int naturalBottom = naturalTop + windowHeight;

        // Bounded top to prevent window title bar from moving out of physical work area
        int boundedTop = Math.Max(monitor.WorkArea.Top, naturalTop);
        int boundedBottom = boundedTop + windowHeight;

        int workLeft = monitor.WorkArea.Left;
        int workRight = monitor.WorkArea.Right;
        int workTop = monitor.WorkArea.Top;

        DockSide newDockSide = currentDockSide;
        int targetLeft = naturalLeft;
        int targetRight = naturalRight;
        int targetTop = boundedTop;
        int targetBottom = boundedBottom;

        if (currentDockSide == DockSide.Right)
        {
            // Already snapped to right: un-snap only if dragged past unsnap threshold
            if (naturalRight < workRight - unsnapPx)
            {
                newDockSide = DockSide.None;
                targetLeft = naturalLeft;
                targetRight = naturalRight;
            }
            else
            {
                targetRight = workRight;
                targetLeft = workRight - windowWidth;
            }
        }
        else if (currentDockSide == DockSide.Left)
        {
            // Already snapped to left: un-snap only if dragged past unsnap threshold
            if (naturalLeft > workLeft + unsnapPx)
            {
                newDockSide = DockSide.None;
                targetLeft = naturalLeft;
                targetRight = naturalRight;
            }
            else
            {
                targetLeft = workLeft;
                targetRight = workLeft + windowWidth;
            }
        }
        else if (currentDockSide == DockSide.Top)
        {
            // Already snapped to top: un-snap only if dragged downwards past unsnap threshold
            if (naturalTop > workTop + unsnapPx)
            {
                newDockSide = DockSide.None;
                targetTop = boundedTop;
                targetBottom = boundedBottom;
            }
            else
            {
                targetTop = workTop;
                targetBottom = workTop + windowHeight;
            }
        }
        else
        {
            // Currently floating: snap if within snap threshold
            bool canSnapTop = Math.Abs(naturalTop - workTop) <= snapPx ||
                              (naturalTop <= workTop && naturalBottom > workTop);
            bool canSnapRight = Math.Abs(naturalRight - workRight) <= snapPx ||
                                (naturalRight >= workRight && naturalLeft < workRight);
            bool canSnapLeft = Math.Abs(naturalLeft - workLeft) <= snapPx ||
                               (naturalLeft <= workLeft && naturalRight > workLeft);

            DockSide chosen = DockSide.None;
            int minDistance = int.MaxValue;

            if (canSnapTop)
            {
                int dist = Math.Abs(naturalTop - workTop);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    chosen = DockSide.Top;
                }
            }
            if (canSnapLeft)
            {
                int dist = Math.Abs(naturalLeft - workLeft);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    chosen = DockSide.Left;
                }
            }
            if (canSnapRight)
            {
                int dist = Math.Abs(naturalRight - workRight);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    chosen = DockSide.Right;
                }
            }

            if (chosen == DockSide.Top)
            {
                newDockSide = DockSide.Top;
                targetTop = workTop;
                targetBottom = workTop + windowHeight;
            }
            else if (chosen == DockSide.Right)
            {
                newDockSide = DockSide.Right;
                targetRight = workRight;
                targetLeft = workRight - windowWidth;
            }
            else if (chosen == DockSide.Left)
            {
                newDockSide = DockSide.Left;
                targetLeft = workLeft;
                targetRight = workLeft + windowWidth;
            }
            else
            {
                newDockSide = DockSide.None;
                targetLeft = naturalLeft;
                targetRight = naturalRight;
            }
        }

        return new DockPlacementResult(
            new PixelRect(targetLeft, targetTop, targetRight, targetBottom),
            newDockSide,
            monitor
        );
    }

    /// <summary>
    /// Determines whether the window can safely slide out to hide at the given edge
    /// without spilling into neighboring displays or covering reserved appbars (side/top taskbars).
    /// </summary>
    public static bool IsSafeToHide(
        DockSide dockSide,
        PixelRect expandedRect,
        MonitorDescriptor hostMonitor,
        IReadOnlyList<MonitorDescriptor> allMonitors,
        int visibleEdgePx)
    {
        if (dockSide == DockSide.None) return false;

        // 1. Guard against docked taskbars / appbars (e.g. WorkArea does not reach MonitorArea)
        if (dockSide == DockSide.Right && hostMonitor.WorkArea.Right != hostMonitor.MonitorArea.Right)
        {
            return false;
        }
        if (dockSide == DockSide.Left && hostMonitor.WorkArea.Left != hostMonitor.MonitorArea.Left)
        {
            return false;
        }
        if (dockSide == DockSide.Top && hostMonitor.WorkArea.Top != hostMonitor.MonitorArea.Top)
        {
            return false;
        }

        // 2. Guard against spilling into neighboring monitor (multi-monitor seam)
        PixelRect offscreenBody;
        if (dockSide == DockSide.Right)
        {
            int hiddenLeft = hostMonitor.WorkArea.Right - visibleEdgePx;
            int hiddenRight = hiddenLeft + expandedRect.Width;
            offscreenBody = new PixelRect(hostMonitor.MonitorArea.Right, expandedRect.Top, hiddenRight, expandedRect.Bottom);
        }
        else if (dockSide == DockSide.Left)
        {
            int hiddenRight = hostMonitor.WorkArea.Left + visibleEdgePx;
            int hiddenLeft = hiddenRight - expandedRect.Width;
            offscreenBody = new PixelRect(hiddenLeft, expandedRect.Top, hostMonitor.MonitorArea.Left, expandedRect.Bottom);
        }
        else // DockSide.Top
        {
            int hiddenBottom = hostMonitor.WorkArea.Top + visibleEdgePx;
            int hiddenTop = hiddenBottom - expandedRect.Height;
            offscreenBody = new PixelRect(expandedRect.Left, hiddenTop, expandedRect.Right, hostMonitor.MonitorArea.Top);
        }

        // Check if offscreenBody intersects any other monitor
        foreach (var m in allMonitors)
        {
            if (m.Handle != hostMonitor.Handle && m.MonitorArea.IntersectsWith(offscreenBody))
            {
                return false;
            }
        }

        return true;
    }
}
