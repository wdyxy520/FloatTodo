using System;
using System.Collections.Generic;
using FloatTodo.Core.Models;
using FloatTodo.Core.Windowing;
using Xunit;

namespace FloatTodo.Core.Tests;

public class DockGeometryTests
{
    private static MonitorDescriptor CreateMonitor(
        int left, int top, int right, int bottom,
        int workLeft, int workTop, int workRight, int workBottom,
        double dpi = 1.0, bool isPrimary = true, int id = 1)
    {
        return new MonitorDescriptor
        {
            Handle = new IntPtr(id),
            MonitorArea = new PixelRect(left, top, right, bottom),
            WorkArea = new PixelRect(workLeft, workTop, workRight, workBottom),
            DpiScaleX = dpi,
            DpiScaleY = dpi,
            IsPrimary = isPrimary
        };
    }

    [Fact]
    public void SelectTargetMonitor_PointInMonitor_ReturnsThatMonitor()
    {
        var m1 = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        var m2 = CreateMonitor(1920, 0, 3840, 1080, 1920, 0, 3840, 1040, 1.0, false, 2);
        var list = new List<MonitorDescriptor> { m1, m2 };

        var selected = DockGeometry.SelectTargetMonitor(new PixelPoint(2000, 500), new PixelRect(1900, 100, 2220, 660), list);
        Assert.Equal(m2.Handle, selected.Handle);
    }

    [Fact]
    public void CalculateDockPlacement_SnapToRight_WhenWithinThreshold()
    {
        var m = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        int width = 320;
        int height = 560;

        // Cursor places natural right at 1910 (10px from 1920, threshold is 16px)
        var cursor = new PixelPoint(1910, 200);
        var anchor = new PixelPoint(width, 0); // grabbed at top-right

        var result = DockGeometry.CalculateDockPlacement(cursor, anchor, width, height, m, DockSide.None);

        Assert.Equal(DockSide.Right, result.TargetDockSide);
        Assert.Equal(1920, result.TargetRect.Right);
        Assert.Equal(1600, result.TargetRect.Left);
    }

    [Fact]
    public void CalculateDockPlacement_DoesNotSnap_WhenBeyondThreshold()
    {
        var m = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        int width = 320;
        int height = 560;

        // Cursor places natural right at 1900 (20px from 1920, threshold is 16px)
        var cursor = new PixelPoint(1900, 200);
        var anchor = new PixelPoint(width, 0);

        var result = DockGeometry.CalculateDockPlacement(cursor, anchor, width, height, m, DockSide.None);

        Assert.Equal(DockSide.None, result.TargetDockSide);
        Assert.Equal(1900, result.TargetRect.Right);
        Assert.Equal(1580, result.TargetRect.Left);
    }

    [Fact]
    public void CalculateDockPlacement_Hysteresis_RetainsRightSnap_UntilUnsnapThreshold()
    {
        var m = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        int width = 320;
        int height = 560;

        // Currently DockSide.Right. User pulled to naturalRight = 1900 (20px away, unsnap threshold is 28px)
        var cursor = new PixelPoint(1900, 200);
        var anchor = new PixelPoint(width, 0);

        var result = DockGeometry.CalculateDockPlacement(cursor, anchor, width, height, m, DockSide.Right);

        // Should STILL be snapped because 20px < 28px unsnap threshold!
        Assert.Equal(DockSide.Right, result.TargetDockSide);
        Assert.Equal(1920, result.TargetRect.Right);

        // Pull further: naturalRight = 1880 (40px away > 28px)
        var cursorFurther = new PixelPoint(1880, 200);
        var resultUnsnap = DockGeometry.CalculateDockPlacement(cursorFurther, anchor, width, height, m, DockSide.Right);

        Assert.Equal(DockSide.None, resultUnsnap.TargetDockSide);
        Assert.Equal(1880, resultUnsnap.TargetRect.Right);
    }

    [Fact]
    public void CalculateDockPlacement_BoundedTop_PreventsTitleBarLeavingWorkArea()
    {
        var m = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        int width = 320;
        int height = 560;

        // Cursor dragged up to Y = -50 (anchor Y = 10 -> naturalTop = -60)
        var cursor = new PixelPoint(500, -50);
        var anchor = new PixelPoint(50, 10);

        var result = DockGeometry.CalculateDockPlacement(cursor, anchor, width, height, m, DockSide.None);

        Assert.Equal(m.WorkArea.Top, result.TargetRect.Top);
        Assert.Equal(m.WorkArea.Top + height, result.TargetRect.Bottom);
    }

    [Fact]
    public void IsSafeToHide_SingleMonitor_ReturnsTrue()
    {
        var m = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        var list = new List<MonitorDescriptor> { m };
        var expanded = new PixelRect(1600, 100, 1920, 660);

        bool safe = DockGeometry.IsSafeToHide(DockSide.Right, expanded, m, list, 6);
        Assert.True(safe);
    }

    [Fact]
    public void IsSafeToHide_MultiMonitorSeam_ReturnsFalse()
    {
        var m1 = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1920, 1040, 1.0, true, 1);
        var m2 = CreateMonitor(1920, 0, 3840, 1080, 1920, 0, 3840, 1040, 1.0, false, 2);
        var list = new List<MonitorDescriptor> { m1, m2 };

        // Window on m1, docked to right (which is adjacent to m2!)
        var expanded = new PixelRect(1600, 100, 1920, 660);

        bool safe = DockGeometry.IsSafeToHide(DockSide.Right, expanded, m1, list, 6);
        // Offscreen body would spill into m2 ([1920, 0, 3840, 1080])! Must be false!
        Assert.False(safe);

        // But docking to m2's right side is safe
        var expandedM2 = new PixelRect(3520, 100, 3840, 660);
        bool safeM2 = DockGeometry.IsSafeToHide(DockSide.Right, expandedM2, m2, list, 6);
        Assert.True(safeM2);
    }

    [Fact]
    public void IsSafeToHide_SideTaskbar_ReturnsFalse()
    {
        // Right side taskbar: WorkArea right is 1840, but MonitorArea right is 1920
        var m = CreateMonitor(0, 0, 1920, 1080, 0, 0, 1840, 1080, 1.0, true, 1);
        var list = new List<MonitorDescriptor> { m };
        var expanded = new PixelRect(1520, 100, 1840, 660);

        bool safe = DockGeometry.IsSafeToHide(DockSide.Right, expanded, m, list, 6);
        Assert.False(safe);
    }
}

public class StorageServiceTests : IDisposable
{
    private readonly string _tempFile;

    public StorageServiceTests()
    {
        _tempFile = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"floattodo_test_{Guid.NewGuid():N}.json");
    }

    public void Dispose()
    {
        if (System.IO.File.Exists(_tempFile))
        {
            try { System.IO.File.Delete(_tempFile); } catch { }
        }
    }

    [Fact]
    public void LoadSettings_WhenFileDoesNotExist_ReturnsDefaults()
    {
        using var storage = new FloatTodo.Core.Services.StorageService(_tempFile);
        var settings = storage.LoadSettings();

        Assert.NotNull(settings);
        Assert.NotNull(settings.Window);
        Assert.NotNull(settings.Appearance);
        Assert.Equal(BackdropType.Mica, settings.Appearance.Backdrop);
        Assert.True(settings.Window.Topmost);
        Assert.True(settings.Window.AutoHide);
    }

    [Theory]
    [InlineData(BackdropType.Mica)]
    [InlineData(BackdropType.MicaAlt)]
    [InlineData(BackdropType.Acrylic)]
    [InlineData(BackdropType.Solid)]
    public void SaveAndLoad_AllBackdropTypes_PersistedAccurately(BackdropType backdrop)
    {
        using (var storage = new FloatTodo.Core.Services.StorageService(_tempFile))
        {
            var settings = new AppSettings
            {
                Window = new WindowSettings
                {
                    Left = 450,
                    Top = 200,
                    Width = 380,
                    Height = 600,
                    Topmost = false,
                    AutoHide = true,
                    DockSide = DockSide.Right
                },
                Appearance = new AppearanceSettings
                {
                    Backdrop = backdrop
                }
            };
            storage.SaveSettingsImmediately(settings);
        }

        using (var storage = new FloatTodo.Core.Services.StorageService(_tempFile))
        {
            var loaded = storage.LoadSettings();
            Assert.Equal(450, loaded.Window.Left);
            Assert.Equal(200, loaded.Window.Top);
            Assert.Equal(380, loaded.Window.Width);
            Assert.Equal(600, loaded.Window.Height);
            Assert.False(loaded.Window.Topmost);
            Assert.True(loaded.Window.AutoHide);
            Assert.Equal(DockSide.Right, loaded.Window.DockSide);
            Assert.Equal(backdrop, loaded.Appearance.Backdrop);
        }
    }

    [Fact]
    public void SaveSettingsDebounced_FlushesOnDispose()
    {
        using (var storage = new FloatTodo.Core.Services.StorageService(_tempFile))
        {
            var settings = new AppSettings
            {
                Window = new WindowSettings { Left = 777, Top = 888 },
                Appearance = new AppearanceSettings { Backdrop = BackdropType.Acrylic }
            };
            storage.SaveSettingsDebounced(settings, delayMs: 5000);
        }

        using (var storage = new FloatTodo.Core.Services.StorageService(_tempFile))
        {
            var loaded = storage.LoadSettings();
            Assert.Equal(777, loaded.Window.Left);
            Assert.Equal(888, loaded.Window.Top);
            Assert.Equal(BackdropType.Acrylic, loaded.Appearance.Backdrop);
        }
    }
}

