"""Generates the Sci-fi theme's two textures as plain RGBA PNGs with no external libraries.

    python Tools~/make_textures.py

scanlines.png     : 4x4 tile, three clear rows and one faint dark row, tiled over the whole window (Sci-fi).
glow-frame.png    : 48x48 nine-slice frame, a crisp 1px cyan line with a soft glow and a translucent fill (Sci-fi).
ember-row-glow.png: 64x1 amber gradient, strongest at the left, stretched across the selected row (Ember).
All are original, procedural art. Unity keeps the import settings in the .meta files next to them.
"""
import os
import struct
import zlib

OUT = os.path.join(os.path.dirname(os.path.abspath(__file__)), "..", "Editor", "UI", "Themes")


def write_png(path, width, height, rows):
    raw = b"".join(b"\x00" + bytes(row) for row in rows)

    def chunk(tag, data):
        body = tag + data
        return struct.pack(">I", len(data)) + body + struct.pack(">I", zlib.crc32(body) & 0xFFFFFFFF)

    png = b"\x89PNG\r\n\x1a\n"
    png += chunk(b"IHDR", struct.pack(">IIBBBBB", width, height, 8, 6, 0, 0, 0))
    png += chunk(b"IDAT", zlib.compress(raw, 9))
    png += chunk(b"IEND", b"")
    with open(path, "wb") as f:
        f.write(png)


def scanlines():
    rows = []
    for y in range(4):
        row = []
        for x in range(4):
            alpha = 46 if y == 3 else 0
            row += [0, 0, 0, alpha]
        rows.append(row)
    write_png(os.path.join(OUT, "scanlines.png"), 4, 4, rows)


def glow_frame():
    """48x48 nine-slice: a crisp 1px cyan line 6px in from the edge, a soft glow either side of it and a
    translucent navy fill inside, so the element itself can stay transparent. Slice at 14px."""
    size = 48
    line = 6           # distance from the edge to the crisp line
    glow = 5           # how far the glow spreads on each side of the line
    r, g, b = 34, 211, 238
    fill = (11, 18, 32, 235)
    rows = []
    for y in range(size):
        row = []
        for x in range(size):
            d = min(x, y, size - 1 - x, size - 1 - y)   # distance from the nearest edge
            if d == line:
                glow_alpha = 255
            elif abs(d - line) <= glow:
                t = 1.0 - abs(d - line) / (glow + 1.0)
                glow_alpha = int(150 * t * t)
            else:
                glow_alpha = 0

            if d > line:
                # inside the line: cyan glow composited over the fill
                ga = glow_alpha / 255.0
                fa = fill[3] / 255.0
                out_a = ga + fa * (1.0 - ga)

                def mix(c, f):
                    return int(round((c * ga + f * fa * (1.0 - ga)) / out_a)) if out_a > 0 else 0

                row += [mix(r, fill[0]), mix(g, fill[1]), mix(b, fill[2]), int(round(out_a * 255))]
            else:
                row += [r, g, b, glow_alpha]
        rows.append(row)
    write_png(os.path.join(OUT, "glow-frame.png"), size, size, rows)


def ember_row_glow():
    """64x1 strip: amber that fades out towards the right with an ease-out curve, so the selected row
    glows from its left edge."""
    width = 64
    r, g, b = 255, 159, 67
    row = []
    for x in range(width):
        t = x / (width - 1.0)
        alpha = int(round(70 * (1.0 - t) ** 2))
        row += [r, g, b, alpha]
    write_png(os.path.join(OUT, "ember-row-glow.png"), width, 1, [row])


scanlines()
glow_frame()
ember_row_glow()
for name in ("scanlines.png", "glow-frame.png", "ember-row-glow.png"):
    print(name, os.path.getsize(os.path.join(OUT, name)), "bytes")
