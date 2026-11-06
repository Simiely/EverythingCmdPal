using System;
using System.IO;
using System.IO.Compression;
using System.Runtime.InteropServices;

namespace CmdPalLinks.Services;

internal readonly record struct ShellIconResult(byte[] Png, int Width, int Height, string Source);

/// <summary>
/// 用 SHGetFileInfo 取 Shell 图标（jumbo → 大图标 → 小图标），把 HICON 转成 PNG 字节。
/// 不依赖 System.Drawing.Common（NuGet 包未携带该程序集，ThumbnailHelper 因此返回 null）。
/// </summary>
internal static class ShellIconExtractor
{
    private const uint ShgfiIcon = 0x000000100;
    private const uint ShgfiSmallIcon = 0x000000001;
    private const uint ShgfiLargeIcon = 0x000000000;
    private const uint ShgfiSysIconIndex = 0x000004000;
    private const int ShilJumbo = 4;
    private const int IldTransparent = 1;
    private const uint DibRgbColors = 0;

    private static readonly Guid IidIImageList = new("46EB5926-582E-4017-9FDF-E8998DAA0950");

    public static ShellIconResult? Extract(string path)
    {
        var jumbo = TryFromImageList(path);
        if (jumbo is not null)
        {
            return jumbo;
        }

        var large = TryFromShellFileInfo(path, ShgfiIcon | ShgfiLargeIcon, "large");
        if (large is not null)
        {
            return large;
        }

        return TryFromShellFileInfo(path, ShgfiIcon | ShgfiSmallIcon, "small");
    }

    private static ShellIconResult? TryFromImageList(string path)
    {
        IntPtr list = IntPtr.Zero;
        try
        {
            var info = default(SHFILEINFO);
            if (SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), ShgfiSysIconIndex) == IntPtr.Zero)
            {
                return null;
            }

            var iid = IidIImageList;
            if (SHGetImageList(ShilJumbo, ref iid, out list) != 0 || list == IntPtr.Zero)
            {
                return null;
            }

            var icon = ImageList_GetIcon(list, info.iIcon, IldTransparent);
            return icon == IntPtr.Zero ? null : Convert(icon, "jumbo");
        }
        catch
        {
            return null;
        }
        finally
        {
            if (list != IntPtr.Zero)
            {
                _ = Marshal.Release(list);
            }
        }
    }

    private static ShellIconResult? TryFromShellFileInfo(string path, uint flags, string source)
    {
        var info = default(SHFILEINFO);
        var result = SHGetFileInfo(path, 0, ref info, (uint)Marshal.SizeOf<SHFILEINFO>(), flags);
        if (result == IntPtr.Zero || info.hIcon == IntPtr.Zero)
        {
            return null;
        }

        return Convert(info.hIcon, source);
    }

    private static ShellIconResult? Convert(IntPtr hIcon, string source)
    {
        try
        {
            var iconInfo = default(ICONINFO);
            if (!GetIconInfo(hIcon, ref iconInfo))
            {
                return null;
            }

            try
            {
                return Render(iconInfo, source);
            }
            finally
            {
                if (iconInfo.hbmMask != IntPtr.Zero)
                {
                    _ = DeleteObject(iconInfo.hbmMask);
                }

                if (iconInfo.hbmColor != IntPtr.Zero)
                {
                    _ = DeleteObject(iconInfo.hbmColor);
                }
            }
        }
        finally
        {
            _ = DestroyIcon(hIcon);
        }
    }

    private static ShellIconResult? Render(ICONINFO info, string source)
    {
        var color = info.hbmColor;
        var mask = info.hbmMask;

        var width = 0;
        var height = 0;
        var bm = default(BITMAP);
        if (color != IntPtr.Zero && GetObject(color, Marshal.SizeOf<BITMAP>(), out bm) != 0)
        {
            width = bm.bmWidth;
            height = bm.bmHeight;
        }
        else if (mask != IntPtr.Zero && GetObject(mask, Marshal.SizeOf<BITMAP>(), out bm) != 0)
        {
            width = bm.bmWidth;
            height = Math.Abs(bm.bmHeight);
        }

        if (width <= 0 || height <= 0)
        {
            return null;
        }

        var length = width * height * 4;
        var pixels = new byte[length];
        var maskPixels = new byte[length];
        var hdc = CreateCompatibleDC(IntPtr.Zero);
        try
        {
            if (hdc == IntPtr.Zero)
            {
                return null;
            }

            if (color != IntPtr.Zero && !ReadBits(hdc, color, width, height, pixels))
            {
                return null;
            }

            var hasMask = mask != IntPtr.Zero && ReadBits(hdc, mask, width, height, maskPixels);
            ApplyAlpha(pixels, hasMask ? maskPixels : null);
            return new ShellIconResult(EncodePng(pixels, width, height), width, height, source);
        }
        finally
        {
            if (hdc != IntPtr.Zero)
            {
                _ = DeleteDC(hdc);
            }
        }
    }

    private static bool ReadBits(IntPtr hdc, IntPtr bitmap, int width, int height, byte[] target)
    {
        var header = new BITMAPINFOHEADER
        {
            biSize = (uint)Marshal.SizeOf<BITMAPINFOHEADER>(),
            biWidth = width,
            biHeight = -height,
            biPlanes = 1,
            biBitCount = 32,
            biCompression = 0,
        };

        return GetDIBits(hdc, bitmap, 0, (uint)height, target, ref header, DibRgbColors) != 0;
    }

    /// <summary>
    /// 32 位图标通常自带 alpha；若 alpha 全 0（部分旧图标），用 AND 掩码合成 0/255 透明度。
    /// </summary>
    private static void ApplyAlpha(byte[] pixels, byte[]? mask)
    {
        var anyAlpha = false;
        for (var i = 3; i < pixels.Length; i += 4)
        {
            if (pixels[i] != 0)
            {
                anyAlpha = true;
                break;
            }
        }

        for (var i = 0; i < pixels.Length; i += 4)
        {
            var transparent = mask is not null && mask[i] != 0;
            if (mask is not null && transparent)
            {
                pixels[i + 3] = 0;
            }
            else if (!anyAlpha)
            {
                pixels[i + 3] = 255;
            }
        }
    }

    internal static byte[] EncodePng(byte[] rgba, int width, int height)
    {
        var raw = new byte[(width * 4 + 1) * height];
        var source = 0;
        var target = 0;
        for (var y = 0; y < height; y++)
        {
            raw[target++] = 0;
            Buffer.BlockCopy(rgba, source, raw, target, width * 4);
            source += width * 4;
            target += width * 4;
        }

        byte[] compressed;
        using (var output = new MemoryStream())
        {
            using (var deflate = new DeflateStream(output, CompressionLevel.Optimal, leaveOpen: true))
            {
                deflate.Write(raw);
            }

            compressed = output.ToArray();
        }

        using var png = new MemoryStream();
        png.Write(new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A });

        using (var ihdr = new MemoryStream())
        {
            WriteInt(ihdr, width);
            WriteInt(ihdr, height);
            ihdr.WriteByte(8);
            ihdr.WriteByte(6);
            ihdr.WriteByte(0);
            ihdr.WriteByte(0);
            ihdr.WriteByte(0);
            WriteChunk(png, "IHDR", ihdr.ToArray());
        }

        using (var idat = new MemoryStream())
        {
            idat.WriteByte(0x78);
            idat.WriteByte(0x01);
            idat.Write(compressed);
            WriteInt(idat, (int)Adler32(raw));
            WriteChunk(png, "IDAT", idat.ToArray());
        }

        WriteChunk(png, "IEND", Array.Empty<byte>());
        return png.ToArray();
    }

    private static void WriteChunk(Stream stream, string type, byte[] data)
    {
        WriteInt(stream, data.Length);
        var typeBytes = System.Text.Encoding.ASCII.GetBytes(type);
        stream.Write(typeBytes);
        stream.Write(data);

        var crcInput = new byte[typeBytes.Length + data.Length];
        Buffer.BlockCopy(typeBytes, 0, crcInput, 0, typeBytes.Length);
        Buffer.BlockCopy(data, 0, crcInput, typeBytes.Length, data.Length);
        WriteInt(stream, (int)Crc32(crcInput));
    }

    private static void WriteInt(Stream stream, int value)
    {
        stream.WriteByte((byte)(value >> 24));
        stream.WriteByte((byte)(value >> 16));
        stream.WriteByte((byte)(value >> 8));
        stream.WriteByte((byte)value);
    }

    private static uint Adler32(byte[] data)
    {
        uint a = 1;
        uint b = 0;
        foreach (var value in data)
        {
            a = (a + value) % 65521;
            b = (b + a) % 65521;
        }

        return (b << 16) | a;
    }

    private static uint Crc32(byte[] data)
    {
        var crc = 0xFFFFFFFFu;
        foreach (var value in data)
        {
            crc ^= value;
            for (var bit = 0; bit < 8; bit++)
            {
                crc = (crc & 1) != 0 ? 0xEDB88320u ^ (crc >> 1) : crc >> 1;
            }
        }

        return crc ^ 0xFFFFFFFFu;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct ICONINFO
    {
        public bool fIcon;
        public int xHotspot;
        public int yHotspot;
        public IntPtr hbmMask;
        public IntPtr hbmColor;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAP
    {
        public int bmType;
        public int bmWidth;
        public int bmHeight;
        public ushort bmWidthBytes;
        public ushort bmPlanes;
        public ushort bmBitsPixel;
        public IntPtr bmBits;
    }

    [StructLayout(LayoutKind.Sequential)]
    private struct BITMAPINFOHEADER
    {
        public uint biSize;
        public int biWidth;
        public int biHeight;
        public ushort biPlanes;
        public ushort biBitCount;
        public uint biCompression;
        public uint biSizeImage;
        public int biXPelsPerMeter;
        public int biYPelsPerMeter;
        public uint biClrUsed;
        public uint biClrImportant;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct SHFILEINFO
    {
        public IntPtr hIcon;
        public int iIcon;
        public uint dwAttributes;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)]
        public string szDisplayName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 80)]
        public string szTypeName;
    }

    [DllImport("shell32.dll", CharSet = CharSet.Unicode)]
    private static extern IntPtr SHGetFileInfo(string path, uint attributes, ref SHFILEINFO info, uint size, uint flags);

    [DllImport("shell32.dll", EntryPoint = "#727")]
    private static extern int SHGetImageList(int imageList, ref Guid iid, out IntPtr result);

    [DllImport("comctl32.dll")]
    private static extern IntPtr ImageList_GetIcon(IntPtr list, int index, int flags);

    [DllImport("user32.dll")]
    private static extern bool DestroyIcon(IntPtr icon);

    [DllImport("user32.dll")]
    private static extern bool GetIconInfo(IntPtr icon, ref ICONINFO info);

    [DllImport("gdi32.dll")]
    private static extern IntPtr CreateCompatibleDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteDC(IntPtr hdc);

    [DllImport("gdi32.dll")]
    private static extern int GetObject(IntPtr handle, int size, out BITMAP bitmap);

    [DllImport("gdi32.dll", SetLastError = true)]
    private static extern int GetDIBits(IntPtr hdc, IntPtr bitmap, uint start, uint lines, byte[] bits, ref BITMAPINFOHEADER header, uint usage);

    [DllImport("gdi32.dll")]
    private static extern bool DeleteObject(IntPtr handle);
}
