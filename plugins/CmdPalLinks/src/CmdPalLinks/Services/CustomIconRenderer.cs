using System;
using System.Globalization;
using System.Runtime.InteropServices;
using CmdPalLinks.Models;

namespace CmdPalLinks.Services;

/// <summary>
/// 自绘图标：圆角色块 / 透明底 + 单色 emoji（GDI 字形掩膜 + 人工着色）。
/// 说明：GDI/GDI+ 无法渲染 Segoe UI Emoji 的彩色字形，这里只取字形轮廓做单色掩膜，
/// 因此 emoji 会丢失原色与渐变（这是当前版本的已知视觉上限）。
/// 注意：GDI 的 W 版本 API 必须按 Unicode 编组（见下方 DllImport 的 CharSet），
/// 否则非 ASCII 文本（emoji、中文标题）会先被转成 ANSI 再按 UTF-16 解码，渲染成乱码字形。
/// </summary>
internal static class CustomIconRenderer
{
    private const int Size = 64;

    private const int Scale = 4;

    private const string EmojiFace = "Segoe UI Emoji";

    private const string TextFace = "Segoe UI";

    public static byte[] Render(LinkItem item, bool dark)
    {
        var canvas = Size * Scale;
        var label = ResolveLabel(item);
        var emoji = !string.IsNullOrWhiteSpace(item.IconEmoji);

        var mask = RenderGlyphMask(label, canvas, emoji ? EmojiFace : TextFace);
        if (HasInk(mask, canvas) == 0 && emoji)
        {
            // ZWJ 组合序列在 GDI 下可能没有单一字形，退回首字。
            mask = RenderGlyphMask(ResolveLabel(item, forceLetter: true), canvas, TextFace);
        }

        var kind = IconKinds.Normalize(item.IconKind);
        var color = ParseColor(item.IconColor, out var hasColor);
        var hasBlock = kind switch
        {
            IconKinds.Block => true,
            IconKinds.Transparent => false,
            IconKinds.Emoji => hasColor,
            _ => hasColor,
        };

        var background = hasBlock ? color : (0, 0, 0);
        var foreground = hasBlock || !dark ? (15, 23, 42) : (241, 245, 249);
        if (hasBlock)
        {
            // 色块上统一用近白色前景，保证深浅主题都有对比。
            foreground = (255, 255, 255);
        }

        var canvasPixels = Composite(mask, canvas, hasBlock, background, foreground);
        var rgba = Downsample(canvasPixels, canvas, Size);
        return ShellIconExtractor.EncodePng(rgba, Size, Size);
    }

    private static string ResolveLabel(LinkItem item, bool forceLetter = false)
    {
        if (!forceLetter && !string.IsNullOrWhiteSpace(item.IconEmoji))
        {
            return StringInfo.GetNextTextElement(item.IconEmoji.Trim());
        }

        var title = (item.Title ?? string.Empty).Trim();
        if (title.Length > 0)
        {
            return StringInfo.GetNextTextElement(title);
        }

        return item.Kind == LinkKinds.Url ? "W" : "?";
    }

    private static (int R, int G, int B) ParseColor(string? value, out bool valid)
    {
        valid = false;
        var text = (value ?? string.Empty).Trim();
        if (text.Length == 7 && text[0] == '#')
        {
            var r = Hex(text.Substring(1, 2));
            var g = Hex(text.Substring(3, 2));
            var b = Hex(text.Substring(5, 2));
            if (r >= 0 && g >= 0 && b >= 0)
            {
                valid = true;
                return (r, g, b);
            }
        }

        return (37, 99, 235);
    }

    private static int Hex(string text)
    {
        try
        {
            return int.Parse(text, NumberStyles.HexNumber, CultureInfo.InvariantCulture);
        }
        catch (Exception)
        {
            return -1;
        }
    }

    private static byte[] RenderGlyphMask(string text, int canvas, string face)
    {
        var mask = new byte[canvas * canvas * 4];
        IntPtr dc = IntPtr.Zero;
        IntPtr dib = IntPtr.Zero;
        IntPtr font = IntPtr.Zero;
        IntPtr oldFont = IntPtr.Zero;

        try
        {
            var header = new BitmapInfoHeader
            {
                Size = Marshal.SizeOf<BitmapInfoHeader>(),
                Width = canvas,
                Height = -canvas,
                Planes = 1,
                BitCount = 32,
                Compression = 0,
            };
            var info = new BitmapInfo { Header = header };
            dc = CreateCompatibleDC(IntPtr.Zero);
            dib = CreateDIBSection(dc, ref info, 0, out var bits, IntPtr.Zero, 0);
            if (dib == IntPtr.Zero || bits == IntPtr.Zero)
            {
                return mask;
            }

            // 白底。
            var white = new byte[canvas * canvas * 4];
            for (var i = 0; i < white.Length; i += 4)
            {
                white[i] = 255;
                white[i + 1] = 255;
                white[i + 2] = 255;
                white[i + 3] = 255;
            }

            Marshal.Copy(white, 0, bits, white.Length);

            SelectObject(dc, dib);
            var height = -(int)(canvas * 0.72);
            font = CreateFontW(height, 0, 0, 0, 400, false, false, false, 1, 0, 0, 4, 0, face);
            oldFont = SelectObject(dc, font);
            SetBkMode(dc, 1);
            SetTextColor(dc, 0);

            var rect = new Rect { Left = 0, Top = 0, Right = canvas, Bottom = canvas };
            DrawTextW(dc, text, text.Length, ref rect, 0x00000001 | 0x00000004 | 0x00000020);

            Marshal.Copy(bits, mask, 0, mask.Length);
        }
        catch (Exception ex)
        {
            HostLog.Error($"custom icon glyph failed: {ex.GetType().Name}: {ex.Message}");
        }
        finally
        {
            if (oldFont != IntPtr.Zero)
            {
                SelectObject(dc, oldFont);
            }

            if (font != IntPtr.Zero)
            {
                DeleteObject(font);
            }

            if (dib != IntPtr.Zero)
            {
                DeleteObject(dib);
            }

            if (dc != IntPtr.Zero)
            {
                DeleteDC(dc);
            }
        }

        return mask;
    }

    private static byte[] Composite(byte[] mask, int canvas, bool hasBlock, (int R, int G, int B) background, (int R, int G, int B) foreground)
    {
        var pixels = new byte[canvas * canvas * 4];
        var radius = canvas * 0.22;
        var inner = canvas - radius;

        for (var y = 0; y < canvas; y++)
        {
            for (var x = 0; x < canvas; x++)
            {
                var index = (y * canvas + x) * 4;
                var b = mask[index];
                var g = mask[index + 1];
                var r = mask[index + 2];
                var luminance = Math.Min(r, Math.Min(g, b));
                var glyphAlpha = 255 - luminance;

                var inside = hasBlock && InsideRoundedRect(x, y, canvas, radius, inner);
                var baseR = 0;
                var baseG = 0;
                var baseB = 0;
                var baseA = 0;

                if (inside)
                {
                    baseR = background.R;
                    baseG = background.G;
                    baseB = background.B;
                    baseA = 255;
                }

                if (glyphAlpha > 0)
                {
                    var a = glyphAlpha;
                    var outR = (foreground.R * a + baseR * (255 - a)) / 255;
                    var outG = (foreground.G * a + baseG * (255 - a)) / 255;
                    var outB = (foreground.B * a + baseB * (255 - a)) / 255;
                    var outA = inside ? 255 : a;
                    pixels[index] = (byte)outR;
                    pixels[index + 1] = (byte)outG;
                    pixels[index + 2] = (byte)outB;
                    pixels[index + 3] = (byte)outA;
                }
                else
                {
                    pixels[index] = (byte)baseR;
                    pixels[index + 1] = (byte)baseG;
                    pixels[index + 2] = (byte)baseB;
                    pixels[index + 3] = (byte)baseA;
                }
            }
        }

        return pixels;
    }

    private static bool InsideRoundedRect(int x, int y, int canvas, double radius, double inner)
    {
        if (x < 0 || y < 0 || x >= canvas || y >= canvas)
        {
            return false;
        }

        var fx = x + 0.5;
        var fy = y + 0.5;
        var cx = fx < radius ? radius : fx > inner ? inner : fx;
        var cy = fy < radius ? radius : fy > inner ? inner : fy;
        var dx = fx - cx;
        var dy = fy - cy;
        return (dx * dx) + (dy * dy) <= radius * radius;
    }

    private static byte[] Downsample(byte[] source, int canvas, int size)
    {
        var result = new byte[size * size * 4];
        var factor = canvas / size;

        for (var y = 0; y < size; y++)
        {
            for (var x = 0; x < size; x++)
            {
                var sumA = 0;
                var sumR = 0;
                var sumG = 0;
                var sumB = 0;

                for (var sy = 0; sy < factor; sy++)
                {
                    for (var sx = 0; sx < factor; sx++)
                    {
                        var index = ((y * factor + sy) * canvas + (x * factor + sx)) * 4;
                        var a = source[index + 3];
                        sumA += a;
                        sumR += source[index] * a;
                        sumG += source[index + 1] * a;
                        sumB += source[index + 2] * a;
                    }
                }

                var target = (y * size + x) * 4;
                if (sumA == 0)
                {
                    continue;
                }

                result[target] = (byte)(sumR / sumA);
                result[target + 1] = (byte)(sumG / sumA);
                result[target + 2] = (byte)(sumB / sumA);
                result[target + 3] = (byte)(sumA / (factor * factor));
            }
        }

        return result;
    }

    private static int HasInk(byte[] mask, int canvas)
    {
        var count = 0;
        for (var i = 0; i < canvas * canvas * 4; i += 4)
        {
            if (mask[i] < 240 || mask[i + 1] < 240 || mask[i + 2] < 240)
            {
                count++;
            }
        }

        return count;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfoHeader
    {
        public int Size;
        public int Width;
        public int Height;
        public short Planes;
        public short BitCount;
        public int Compression;
        public int SizeImage;
        public int XPelsPerMeter;
        public int YPelsPerMeter;
        public int ClrUsed;
        public int ClrImportant;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BitmapInfo
    {
        public BitmapInfoHeader Header;
        public int Colors;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct Rect
    {
        public int Left;
        public int Top;
        public int Right;
        public int Bottom;
    }

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateDIBSection(IntPtr hdc, ref BitmapInfo pbmi, uint usage, out IntPtr bits, IntPtr section, uint offset);

    [DllImport("gdi32.dll")]
    private static extern IntPtr SelectObject(IntPtr hdc, IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr obj);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr CreateFontW(
        int height,
        int width,
        int escapement,
        int orientation,
        int weight,
        bool italic,
        bool underline,
        bool strikeOut,
        uint charSet,
        uint outPrecision,
        uint clipPrecision,
        uint quality,
        uint pitchAndFamily,
        string face);

    [DllImport("gdi32.dll")]
    private static extern int SetBkMode(IntPtr hdc, int mode);

    [DllImport("gdi32.dll")]
    private static extern int SetTextColor(IntPtr hdc, int color);

    [DllImport("user32.dll", CharSet = CharSet.Unicode)]
    private static extern int DrawTextW(IntPtr hdc, string text, int count, ref Rect rect, uint format);
}
