"""Clean up the chosen idle sprite: remove the held sword, add the wine gourd, tweak colours.

usage: python edit_idle.py <in.png> <out.png>
The sword is removed because the boss's sword is drawn separately in code,
so it can rotate and swing freely.
"""
import sys
from PIL import Image

img = Image.open(sys.argv[1]).convert("RGBA")
px = img.load()
W, H = img.size


def lum(c):
    return 0.299 * c[0] + 0.587 * c[1] + 0.114 * c[2]


# 1. Sword: the white blade curve left of the robe, then the guard, grip and tassel.
for y in range(39, 49):
    for x in range(0, 25):
        if px[x, y][3] and lum(px[x, y]) > 140:
            px[x, y] = (0, 0, 0, 0)
for y in range(44, 59):
    for x in range(0, 17):
        px[x, y] = (0, 0, 0, 0)
# leftover blade outline pixels
for p in [(23, 41), (22, 42), (21, 43), (22, 43), (20, 44)]:
    px[p] = (0, 0, 0, 0)

OUTLINE = (24, 28, 34, 255)
ROBE_LIGHT = (200, 214, 212, 255)
# grip stub under the hand becomes robe; close the robe edge where the blade crossed it
px[25, 39] = OUTLINE
px[25, 40] = ROBE_LIGHT
px[24, 40] = OUTLINE
# the blade also crossed the robe diagonally: pure-white glints and the guard become robe cloth
for y in range(30, 41):
    for x in range(26, 39):
        if px[x, y][3] and min(px[x, y][:3]) >= 245:
            px[x, y] = (222, 234, 232, 255)
px[37, 32] = (200, 214, 212, 255)
px[37, 33] = (150, 170, 172, 255)

# 2. Bring out the 青 (teal): cool grey-blue mid tones are pushed onto a teal ramp.
TEAL = [(24, 70, 80), (42, 111, 120), (74, 167, 166), (148, 218, 210)]
for y in range(H):
    for x in range(W):
        r, g, b, a = px[x, y]
        if not a:
            continue
        L = lum((r, g, b))
        coolish = b >= r + 12 and g >= r + 6
        if coolish and 55 <= L <= 165 and max(r, g, b) - min(r, g, b) < 80:
            t = TEAL[min(3, int((L - 55) / 110 * 4))]
            px[x, y] = t + (255,)

# 3. Wine gourd with a red cord, hung at the front of the waist.
G = {
    "K": (52, 32, 20, 255),     # gourd outline
    "G": (214, 168, 98, 255),   # gourd
    "H": (236, 200, 136, 255),  # highlight
    "g": (150, 102, 54, 255),   # shade
    "R": (190, 56, 46, 255),    # red cord / tassel
    "r": (120, 34, 30, 255),
}
GOURD = [
    "..R...",
    "..KK..",
    ".KHgK.",
    ".KGgK.",
    "..KK..",
    ".KHGgK",
    "KHGGgK",
    "KGGGgK",
    "KgGggK",
    ".KggKR",
    "..KK.R",
    ".....r",
]
gx, gy = 38, 30
for j, row in enumerate(GOURD):
    for i, ch in enumerate(row):
        if ch != ".":
            px[gx + i, gy + j] = G[ch]

img.save(sys.argv[2])
