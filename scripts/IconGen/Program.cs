using System;
using System.IO;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Collections.Generic;

namespace IconGen;

public static class Program
{
    public static void Main(string[] args)
    {
        // 自动向上寻找包含 FloatTodo.slnx 的根目录
        string current = AppContext.BaseDirectory;
        while (!string.IsNullOrEmpty(current) && !File.Exists(Path.Combine(current, "FloatTodo.slnx")))
        {
            var parent = Directory.GetParent(current);
            if (parent == null) break;
            current = parent.FullName;
        }

        string assetsDir = Path.Combine(current, "src", "FloatTodo.WinUI", "Assets");
        Console.WriteLine($"Target Assets Directory: {assetsDir}");

        if (!Directory.Exists(assetsDir))
        {
            Directory.CreateDirectory(assetsDir);
        }

        // 1. 生成各标准尺寸 PNG 图标
        SavePng(RenderIcon(256), Path.Combine(assetsDir, "AppIcon.png"));
        SavePng(RenderIcon(300), Path.Combine(assetsDir, "Square150x150Logo.scale-200.png"));
        SavePng(RenderIcon(88), Path.Combine(assetsDir, "Square44x44Logo.scale-200.png"));
        SavePng(RenderIcon(24), Path.Combine(assetsDir, "Square44x44Logo.targetsize-24_altform-unplated.png"));
        SavePng(RenderIcon(50), Path.Combine(assetsDir, "StoreLogo.png"));
        SavePng(RenderIcon(48), Path.Combine(assetsDir, "LockScreenLogo.scale-200.png"));

        // 宽磁贴 620x300（居中放置 200x200 图标）
        SavePng(RenderCanvasWithCenteredIcon(620, 300, 180), Path.Combine(assetsDir, "Wide310x150Logo.scale-200.png"));

        // 启动屏幕 1240x600（居中放置 240x240 图标）
        SavePng(RenderCanvasWithCenteredIcon(1240, 600, 240), Path.Combine(assetsDir, "SplashScreen.scale-200.png"));

        // 2. 生成多尺寸全规格 app.ico (16, 24, 32, 48, 64, 128, 256)
        int[] icoSizes = new[] { 16, 24, 32, 48, 64, 128, 256 };
        List<byte[]> pngBuffers = new List<byte[]>();
        foreach (int sz in icoSizes)
        {
            using var bmp = RenderIcon(sz);
            using var ms = new MemoryStream();
            bmp.Save(ms, ImageFormat.Png);
            pngBuffers.Add(ms.ToArray());
        }

        string icoPath = Path.Combine(assetsDir, "app.ico");
        SaveMultiResolutionIco(pngBuffers, icoSizes, icoPath);
        Console.WriteLine($"Generated app.ico with sizes: {string.Join(", ", icoSizes)}");

        Console.WriteLine("All assets generated successfully!");
    }

    private static void SavePng(Bitmap bmp, string filePath)
    {
        bmp.Save(filePath, ImageFormat.Png);
        Console.WriteLine($"Saved PNG: {Path.GetFileName(filePath)} ({bmp.Width}x{bmp.Height})");
        bmp.Dispose();
    }

    public static Bitmap RenderIcon(int size)
    {
        Bitmap bmp = new Bitmap(size, size, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(bmp))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            float scale = size / 512.0f;

            // 1. 底板: 432x432 Squircle, rx=108
            RectangleF bodyRect = new RectangleF(40 * scale, 40 * scale, 432 * scale, 432 * scale);
            float bodyRadius = 108 * scale;
            using (GraphicsPath bodyPath = CreateRoundedRectangle(bodyRect, bodyRadius))
            {
                using (LinearGradientBrush bodyBrush = new LinearGradientBrush(
                    new PointF(0, bodyRect.Top),
                    new PointF(0, bodyRect.Bottom),
                    Color.FromArgb(246, 247, 249),
                    Color.FromArgb(229, 232, 236)))
                {
                    g.FillPath(bodyBrush, bodyPath);
                }

                // 微描边 (防浅色背景融合，增强边缘锐利度)
                float strokeWidth = Math.Max(1.0f, 2.0f * scale);
                using (Pen borderPen = new Pen(Color.FromArgb(208, 212, 220), strokeWidth))
                {
                    g.DrawPath(borderPen, bodyPath);
                }
            }

            // 2. 工业微凹槽 (在 >= 32px 时渲染微妙槽体层次；小尺寸下省略以保持绝对纯净)
            if (size >= 32)
            {
                RectangleF slotRect = new RectangleF(128 * scale, 210 * scale, 256 * scale, 92 * scale);
                float slotRadius = 46 * scale;
                using (GraphicsPath slotPath = CreateRoundedRectangle(slotRect, slotRadius))
                {
                    using (LinearGradientBrush slotBrush = new LinearGradientBrush(
                        new PointF(0, slotRect.Top),
                        new PointF(0, slotRect.Bottom),
                        Color.FromArgb(217, 220, 225),
                        Color.FromArgb(239, 241, 244)))
                    {
                        g.FillPath(slotBrush, slotPath);
                    }
                }
            }

            // 3. 亮橙磁吸胶囊横条: 240x80, rx=40
            RectangleF pillRect;
            if (size <= 24)
            {
                // 超小尺寸 (16/24px) 下微调充满度与纵横比，确保像素密度饱满、一眼可辨
                pillRect = new RectangleF(116 * scale, 206 * scale, 280 * scale, 100 * scale);
            }
            else
            {
                pillRect = new RectangleF(136 * scale, 216 * scale, 240 * scale, 80 * scale);
            }

            float pillRadius = pillRect.Height / 2.0f;
            using (GraphicsPath pillPath = CreateRoundedRectangle(pillRect, pillRadius))
            {
                using (LinearGradientBrush pillBrush = new LinearGradientBrush(
                    new PointF(0, pillRect.Top),
                    new PointF(0, pillRect.Bottom),
                    Color.FromArgb(255, 99, 38),   // #FF6326
                    Color.FromArgb(244, 71, 0)))    // #F44700
                {
                    g.FillPath(pillBrush, pillPath);
                }
            }
        }
        return bmp;
    }

    public static Bitmap RenderCanvasWithCenteredIcon(int canvasWidth, int canvasHeight, int iconSize)
    {
        Bitmap canvas = new Bitmap(canvasWidth, canvasHeight, PixelFormat.Format32bppArgb);
        using (Graphics g = Graphics.FromImage(canvas))
        {
            g.SmoothingMode = SmoothingMode.AntiAlias;
            g.InterpolationMode = InterpolationMode.HighQualityBicubic;
            g.PixelOffsetMode = PixelOffsetMode.HighQuality;
            g.Clear(Color.Transparent);

            using Bitmap icon = RenderIcon(iconSize);
            int x = (canvasWidth - iconSize) / 2;
            int y = (canvasHeight - iconSize) / 2;
            g.DrawImage(icon, x, y, iconSize, iconSize);
        }
        return canvas;
    }

    public static GraphicsPath CreateRoundedRectangle(RectangleF rect, float radius)
    {
        GraphicsPath path = new GraphicsPath();
        float diameter = radius * 2.0f;
        if (diameter > rect.Width) diameter = rect.Width;
        if (diameter > rect.Height) diameter = rect.Height;

        RectangleF arc = new RectangleF(rect.X, rect.Y, diameter, diameter);
        path.AddArc(arc, 180, 90);
        arc.X = rect.Right - diameter;
        path.AddArc(arc, 270, 90);
        arc.Y = rect.Bottom - diameter;
        path.AddArc(arc, 0, 90);
        arc.X = rect.Left;
        path.AddArc(arc, 90, 90);
        path.CloseFigure();
        return path;
    }

    private static void SaveMultiResolutionIco(List<byte[]> pngBuffers, int[] sizes, string outputPath)
    {
        using var fs = new FileStream(outputPath, FileMode.Create, FileAccess.Write);
        using var bw = new BinaryWriter(fs);

        int count = sizes.Length;

        // ICONDIR header
        bw.Write((ushort)0);      // idReserved
        bw.Write((ushort)1);      // idType: 1 = ICO
        bw.Write((ushort)count);  // idCount

        int headerSize = 6 + (16 * count);
        int currentOffset = headerSize;

        // ICONDIRENTRY array
        for (int i = 0; i < count; i++)
        {
            int sz = sizes[i];
            byte bWidth = (byte)(sz >= 256 ? 0 : sz);
            byte bHeight = (byte)(sz >= 256 ? 0 : sz);
            byte bColorCount = 0;
            byte bReserved = 0;
            ushort wPlanes = 1;
            ushort wBitCount = 32;
            int dwBytesInRes = pngBuffers[i].Length;
            int dwImageOffset = currentOffset;

            bw.Write(bWidth);
            bw.Write(bHeight);
            bw.Write(bColorCount);
            bw.Write(bReserved);
            bw.Write(wPlanes);
            bw.Write(wBitCount);
            bw.Write(dwBytesInRes);
            bw.Write(dwImageOffset);

            currentOffset += dwBytesInRes;
        }

        // Write image payloads
        for (int i = 0; i < count; i++)
        {
            bw.Write(pngBuffers[i]);
        }
    }
}
