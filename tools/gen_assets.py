import os, struct, zlib

OUT = r"C:\Users\wandou\WorkBuddy\2026-10-25-23-07-56\EverythingCmdPal\src\EverythingCmdPal\Assets"
BG = (28, 30, 34)
FG = (122, 196, 255)

def chunk(tag, data):
    return struct.pack(">I", len(data)) + tag + data + struct.pack(">I", zlib.crc32(tag + data) & 0xFFFFFFFF)

def write_png(path, w, h):
    raw = bytearray()
    x0, x1 = int(w * 0.30), int(w * 0.38)
    y0, y1 = int(h * 0.22), int(h * 0.78)
    bars = [
        (int(h * 0.22), int(h * 0.32), int(w * 0.30), int(w * 0.72)),
        (int(h * 0.45), int(h * 0.55), int(w * 0.30), int(w * 0.64)),
        (int(h * 0.68), int(h * 0.78), int(w * 0.30), int(w * 0.72)),
    ]
    for y in range(h):
        raw.append(0)
        for x in range(w):
            on = (x0 <= x <= x1 and y0 <= y <= y1)
            if not on:
                for by0, by1, bx0, bx1 in bars:
                    if by0 <= y <= by1 and bx0 <= x <= bx1:
                        on = True
                        break
            r, g, b = FG if on else BG
            raw += bytes((r, g, b))
    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", w, h, 8, 2, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(bytes(raw), 9))
    png += chunk(b"IEND", b"")
    with open(os.path.join(OUT, path), "wb") as f:
        f.write(png)

specs = [
    ("Square44x44Logo.scale-200.png", 88, 88),
    ("Square44x44Logo.targetsize-24_altform-unplated.png", 24, 24),
    ("Square150x150Logo.scale-200.png", 300, 300),
    ("Wide310x150Logo.scale-200.png", 620, 300),
    ("SplashScreen.scale-200.png", 1240, 600),
    ("LockScreenLogo.scale-200.png", 48, 48),
    ("StoreLogo.png", 50, 50),
]
for s in specs:
    write_png(*s)
print("generated", len(specs))
