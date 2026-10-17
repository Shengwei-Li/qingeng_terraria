"""Zoomed preview with a coordinate grid, for planning pixel edits.

usage: python grid.py <in.png> <out.png> [scale] [x0 y0 x1 y1]
"""
import sys
from PIL import Image, ImageDraw

src = Image.open(sys.argv[1]).convert("RGBA")
out = sys.argv[2]
s = int(sys.argv[3]) if len(sys.argv) > 3 else 14
if len(sys.argv) > 7:
    x0, y0, x1, y1 = map(int, sys.argv[4:8])
else:
    x0, y0, x1, y1 = 0, 0, src.width, src.height
crop = src.crop((x0, y0, x1, y1))
m = 28  # margin for labels
img = Image.new("RGBA", (crop.width * s + m, crop.height * s + m), (120, 170, 220, 255))
img.alpha_composite(crop.resize((crop.width * s, crop.height * s), Image.Resampling.NEAREST), (m, m))
d = ImageDraw.Draw(img)
for i in range(crop.width + 1):
    x = m + i * s
    d.line([(x, m), (x, img.height)], fill=(0, 0, 0, 90 if (x0 + i) % 4 else 200))
    if (x0 + i) % 4 == 0 and i < crop.width:
        d.text((x + 2, 4), str(x0 + i), fill=(0, 0, 0, 255))
for j in range(crop.height + 1):
    y = m + j * s
    d.line([(m, y), (img.width, y)], fill=(0, 0, 0, 90 if (y0 + j) % 4 else 200))
    if (y0 + j) % 4 == 0 and j < crop.height:
        d.text((2, y + 2), str(y0 + j), fill=(0, 0, 0, 255))
img.save(out)
