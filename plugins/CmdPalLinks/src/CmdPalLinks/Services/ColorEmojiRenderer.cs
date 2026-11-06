using System;
using System.Numerics;
using CmdPalLinks.Models;
using Vortice.DCommon;
using Vortice.Direct2D1;
using Vortice.DirectWrite;
using Vortice.Mathematics;
using Vortice.WIC;
using WicPixelFormat = Vortice.WIC.PixelFormat;

namespace CmdPalLinks.Services;

/// <summary>
/// 彩色 emoji：用 DirectWrite 取彩色字形（COLR/CPAL + DrawTextOptions.EnableColorFont），
/// 由 Direct2D 画到 WIC 位图，再合成底色/降采样成 64px PNG。
/// 这里的 D2D/DWrite 调用全部走 Vortice（SharpGen 生成的成熟 COM 封装），不自己维护 vtable 下标；
/// 并且只在独立子进程里执行（见 <see cref="EmojiRenderHost"/>），任何原生失败都不会带走扩展宿主进程。
/// 返回 null 表示字体没有给出彩色字形（判定见 <see cref="HasColorInk"/>），调用方降级为 GDI 单色轮廓。
/// </summary>
internal static class ColorEmojiRenderer
{
    private const int Canvas = 256;

    private const int Size = 64;

    private const string EmojiFace = "Segoe UI Emoji";

    /// <summary>彩色字形判定阈值：单像素 RGB 通道最大差。</summary>
    private const int ColorSpread = 24;

    /// <summary>彩色字形判定阈值：至少这么多像素满足 spread。</summary>
    private const int ColorPixelCount = 8;

    public static byte[]? Render(string emoji, string? color, bool hasBlock)
    {
        var text = (emoji ?? string.Empty).Trim();
        if (text.Length == 0)
        {
            return null;
        }

        var canvasPixels = RenderCanvas(text, out var colored, out var ink);
        if (!colored || ink == 0)
        {
            return null;
        }

        var background = CustomIconRenderer.ParseColor(color, out _);
        var composed = Composite(canvasPixels, Canvas, hasBlock, background);
        var rgba = CustomIconRenderer.Downsample(composed, Canvas, Size);
        return ShellIconExtractor.EncodePng(rgba, Size, Size);
    }

    /// <summary>渲染 256px 画布，返回预乘 BGRA 像素；colored 表示取到了彩色字形。</summary>
    private static byte[] RenderCanvas(string text, out bool colored, out int ink)
    {
        using var wic = new IWICImagingFactory();
        using var bitmap = wic.CreateBitmap(
            Canvas,
            Canvas,
            WicPixelFormat.Format32bppPBGRA,
            BitmapCreateCacheOption.CacheOnLoad);

        using var d2d = D2D1.D2D1CreateFactory<ID2D1Factory>(Vortice.Direct2D1.FactoryType.SingleThreaded);
        using var dwrite = DWrite.DWriteCreateFactory<IDWriteFactory>(Vortice.DirectWrite.FactoryType.Shared);

        var props = new RenderTargetProperties(
            new Vortice.DCommon.PixelFormat(Vortice.DXGI.Format.B8G8R8A8_UNorm, AlphaMode.Premultiplied));
        using var target = d2d.CreateWicBitmapRenderTarget(bitmap, props);

        target.BeginDraw();
        target.Clear(new Color4(0f, 0f, 0f, 0f));

        using var format = dwrite.CreateTextFormat(
            EmojiFace,
            null,
            FontWeight.Normal,
            FontStyle.Normal,
            FontStretch.Normal,
            Canvas * 0.62f,
            "en-us");
        format.TextAlignment = TextAlignment.Center;
        format.ParagraphAlignment = ParagraphAlignment.Center;
        format.WordWrapping = WordWrapping.NoWrap;

        using var layout = dwrite.CreateTextLayout(text, format, Canvas, Canvas);
        using var brush = target.CreateSolidColorBrush(new Color4(1f, 1f, 1f, 1f));
        target.DrawTextLayout(
            new Vector2(0f, 0f),
            layout,
            brush,
            DrawTextOptions.EnableColorFont | DrawTextOptions.NoSnap);
        target.EndDraw();

        var pixels = new byte[Canvas * Canvas * 4];
        unsafe
        {
            fixed (byte* buffer = pixels)
            {
                bitmap.CopyPixels(
                    new RectI(0, 0, Canvas, Canvas),
                    (uint)(Canvas * 4),
                    (uint)pixels.Length,
                    (nint)buffer);
            }
        }

        colored = HasColorInk(pixels, out ink);
        return pixels;
    }

    /// <summary>
    /// 判定是否取到彩色字形：统计不透明像素里 RGB 通道差 ≥ <see cref="ColorSpread"/> 的数量。
    /// 取不到彩色表（COLR/CPAL 缺失、字体回退）时字形只有一种颜色，spread 为 0。
    /// </summary>
    private static bool HasColorInk(byte[] bgra, out int ink)
    {
        var colored = 0;
        ink = 0;

        for (var i = 0; i < bgra.Length; i += 4)
        {
            var a = bgra[i + 3];
            if (a < 32)
            {
                continue;
            }

            ink++;

            // 预乘像素先反解出直通值再比较，避免低 alpha 边缘把差异压小。
            var b = Math.Min(255, bgra[i] * 255 / a);
            var g = Math.Min(255, bgra[i + 1] * 255 / a);
            var r = Math.Min(255, bgra[i + 2] * 255 / a);
            var max = Math.Max(r, Math.Max(g, b));
            var min = Math.Min(r, Math.Min(g, b));
            if (max - min >= ColorSpread)
            {
                colored++;
            }
        }

        return colored >= ColorPixelCount;
    }

    /// <summary>把预乘 BGRA 的 emoji 叠到（可选的）圆角色块底上，输出直通 RGBA。</summary>
    private static byte[] Composite(byte[] source, int canvas, bool hasBlock, (int R, int G, int B) background)
    {
        var pixels = new byte[canvas * canvas * 4];
        var radius = canvas * 0.22;
        var inner = canvas - radius;

        for (var y = 0; y < canvas; y++)
        {
            for (var x = 0; x < canvas; x++)
            {
                var index = (y * canvas + x) * 4;
                var a = source[index + 3];
                var inside = hasBlock && CustomIconRenderer.InsideRoundedRect(x, y, canvas, radius, inner);

                var outB = source[index];
                var outG = source[index + 1];
                var outR = source[index + 2];
                var outA = a;

                if (inside)
                {
                    // source-over：底色不透明，前景按 alpha 叠上去。
                    var rest = 255 - a;
                    outB = (byte)Math.Min(255, outB + (background.B * rest) / 255);
                    outG = (byte)Math.Min(255, outG + (background.G * rest) / 255);
                    outR = (byte)Math.Min(255, outR + (background.R * rest) / 255);
                    outA = 255;
                }

                if (outA > 0 && outA < 255)
                {
                    // 预乘 -> 直通，PNG 编码器按直通 RGBA 处理。
                    outB = (byte)Math.Min(255, outB * 255 / outA);
                    outG = (byte)Math.Min(255, outG * 255 / outA);
                    outR = (byte)Math.Min(255, outR * 255 / outA);
                }

                // WIC 位图是 BGRA 内存序，PNG 编码器（CustomIconRenderer.Downsample）按 RGBA 读取。
                pixels[index] = outR;
                pixels[index + 1] = outG;
                pixels[index + 2] = outB;
                pixels[index + 3] = (byte)outA;
            }
        }

        return pixels;
    }
}
