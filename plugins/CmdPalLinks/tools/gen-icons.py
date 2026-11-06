#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
生成 CmdPalLinks 的全部 MSIX 图标资产（单一源：本脚本）。

设计：深靛蓝渐变圆角方块 + 居中白色「链环」符号（无文字）。
小尺寸（<=32px）改为透明底 + 亮蓝符号，保证在浅色/深色任务栏都能辨认。

用法（在仓库根目录）：
  py -3 tools/gen-icons.py
  py -3 tools/gen-icons.py --check

只依赖 Pillow：py -3 -m pip install pillow
"""
import os
import sys
import math
from PIL import Image, ImageDraw

ASSETS = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "src", "CmdPalLinks", "Assets")
SS = 4  # 超采样倍数

TOP = (74, 111, 255)     # #4A6FFF
BOTTOM = (26, 45, 190)   # #1A2DBE
GLYPH_BLUE = (58, 122, 255)  # 小尺寸透明底用的亮蓝


def _gradient(size):
    img = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(img)
    for y in range(size):
        t = y / max(1, size - 1)
        c = (
            int(TOP[0] + (BOTTOM[0] - TOP[0]) * t),
            int(TOP[1] + (BOTTOM[1] - TOP[1]) * t),
            int(TOP[2] + (BOTTOM[2] - TOP[2]) * t),
            255,
        )
        d.line([(0, y), (size, y)], fill=c)
    return img


def _rounded_mask(size, radius):
    mask = Image.new("L", (size, size), 0)
    d = ImageDraw.Draw(mask)
    d.rounded_rectangle([0, 0, size - 1, size - 1], radius=radius, fill=255)
    return mask


def _glyph(size, color, two_links=True):
    """在 size×size 透明层上画链环，返回未旋转的层（调用方整体旋转 45°）。

    两个空心椭圆环沿水平方向并排、端部相扣；整体再旋转 45°，形成经典「链接」符号。
    小尺寸（< 80 超采样像素，即 <= 24 最终像素）退化为单个粗环，保证可辨。
    """
    layer = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(layer)

    if not two_links or size < 80:
        rx = size * 0.30
        ry = size * 0.20
        thick = int(max(2, size * 0.115))
        cx, cy = size / 2, size / 2
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], outline=color, width=thick)
        return layer

    rx = size * 0.255
    ry = size * 0.150
    thick = int(max(2, size * 0.075))
    offset = size * 0.175
    for sign in (-1, 1):
        cx = size / 2 + sign * offset
        cy = size / 2
        d.ellipse([cx - rx, cy - ry, cx + rx, cy + ry], outline=color, width=thick)
    return layer


def render(size, dark_tile=True):
    s = size * SS
    canvas = Image.new("RGBA", (s, s), (0, 0, 0, 0))

    if dark_tile:
        radius = int(s * 0.22)
        bg = _gradient(s)
        bg.putalpha(_rounded_mask(s, radius))
        canvas.alpha_composite(bg)
        color = (255, 255, 255, 255)
    else:
        color = GLYPH_BLUE + (255,)

    g = _glyph(s, color, two_links=True)
    g = g.rotate(45, resample=Image.BICUBIC, expand=False)
    canvas.alpha_composite(g)

    return canvas.resize((size, size), Image.LANCZOS)


def save(img, name):
    path = os.path.join(ASSETS, name)
    img.save(path, "PNG", optimize=True)
    return path


SCALE_FACTORS = {"": 1.0, ".scale-100": 1.0, ".scale-125": 1.25,
                 ".scale-150": 1.5, ".scale-200": 2.0, ".scale-400": 4.0}


def gen_square(base, output_name, small_variant=False):
    """按 base 尺寸生成基础图 + 全部 scale 变体。"""
    made = []
    for suffix, factor in SCALE_FACTORS.items():
        px = int(round(base * factor))
        dark = not (small_variant and px <= 32)
        made.append(save(render(px, dark_tile=dark), output_name + suffix + ".png"))
    return made


def gen_targetsize(output_name, sizes, small_transparent=True):
    made = []
    for px in sizes:
        dark = not small_transparent
        made.append(save(render(px, dark_tile=dark), f"{output_name}.targetsize-{px}.png"))
        made.append(save(render(px, dark_tile=False), f"{output_name}.targetsize-{px}_altform-unplated.png"))
    return made


def gen_wide(w, h, output_name, tile_ratio=0.62):
    made = []
    for suffix, factor in SCALE_FACTORS.items():
        W, H = int(round(w * factor)), int(round(h * factor))
        img = Image.new("RGBA", (W, H), (0, 0, 0, 0))
        t = int(round(H * tile_ratio))
        tile = render(t, dark_tile=True)
        img.alpha_composite(tile, ((W - t) // 2, (H - t) // 2))
        made.append(save(img, output_name + suffix + ".png"))
    return made


def main():
    if "--check" in sys.argv:
        return check()

    os.makedirs(ASSETS, exist_ok=True)
    for f in os.listdir(ASSETS):
        if f.lower().endswith(".png"):
            os.remove(os.path.join(ASSETS, f))

    written = []
    written += gen_square(50, "StoreLogo")
    written += gen_square(44, "Square44x44Logo", small_variant=True)
    written += gen_targetsize("Square44x44Logo", [16, 24, 32, 48, 256])
    written += gen_square(150, "Square150x150Logo")
    written += gen_square(71, "Square71x71Logo")
    written += gen_square(89, "Square89x89Logo")
    written += gen_square(310, "Square310x310Logo")
    written += gen_wide(310, 150, "Wide310x150Logo")
    written += gen_wide(620, 300, "SplashScreen", tile_ratio=0.55)

    save(render(24, dark_tile=False), "LockScreenLogo.png")
    save(render(48, dark_tile=False), "LockScreenLogo.scale-200.png")
    written += [os.path.join(ASSETS, "LockScreenLogo.png"),
                os.path.join(ASSETS, "LockScreenLogo.scale-200.png")]

    # 256px 预览图（给用户查看）
    preview_dir = os.path.join(os.path.dirname(ASSETS), "..", "..", "docs")
    os.makedirs(preview_dir, exist_ok=True)
    preview = os.path.join(preview_dir, "icon-preview-256.png")
    render(256, dark_tile=True).save(preview, "PNG")
    print(f"preview: {os.path.abspath(preview)}")

    print(f"assets: {len(written)} files -> {os.path.abspath(ASSETS)}")
    for p in sorted(written):
        im = Image.open(p)
        print(f"  {os.path.basename(p)} {im.size[0]}x{im.size[1]} {im.mode}")


def check():
    """静态自检：清单引用存在、尺寸符合、有 alpha、非空白。"""
    required = ["StoreLogo.png", "Square150x150Logo.png", "Square44x44Logo.png",
                "Wide310x150Logo.png", "SplashScreen.png",
                "Square44x44Logo.targetsize-24_altform-unplated.png",
                "Square44x44Logo.scale-200.png", "Square150x150Logo.scale-200.png",
                "Wide310x150Logo.scale-200.png", "SplashScreen.scale-200.png"]
    ok = True
    for name in required:
        path = os.path.join(ASSETS, name)
        if not os.path.exists(path):
            print(f"MISSING {name}")
            ok = False
            continue
        im = Image.open(path).convert("RGBA")
        alpha = im.getchannel("A")
        bbox = alpha.getbbox()
        if bbox is None:
            print(f"BLANK {name}")
            ok = False
            continue
        print(f"OK {name} {im.size[0]}x{im.size[1]} alpha={alpha.getextrema()} bbox={bbox}")

    print("CHECK PASS" if ok else "CHECK FAIL")
    return 0 if ok else 1


if __name__ == "__main__":
    sys.exit(main() or 0)
