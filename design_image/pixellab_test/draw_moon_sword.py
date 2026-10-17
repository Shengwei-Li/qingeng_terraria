"""清月剑 / Clearmoon: the player's sword. Slender, sharp, minimal; crescent-moon guard, moonstone pommel.

Drawn on a 45° diagonal, hilt bottom-left, tip top-right (Terraria item convention).
usage: python draw_moon_sword.py <out.png> [size]
"""
import math
import sys
from PIL import Image

N = int(sys.argv[2]) if len(sys.argv) > 2 else 32
img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
px = img.load()

OUT = (20, 26, 40, 255)
EDGE = (250, 254, 255, 255)        # bright cutting edge
STEEL = (196, 214, 232, 255)
STEEL_LO = (122, 144, 176, 255)
RIDGE = (168, 236, 244, 255)       # pale moonlight along the ridge
SILVER = (226, 232, 240, 255)
SILVER_LO = (140, 152, 172, 255)
GRIP = (30, 40, 64, 255)
GRIP_HI = (70, 92, 132, 255)
MOON = (200, 248, 255, 255)        # moonstone
MOON_LO = (110, 190, 210, 255)

s2 = math.sqrt(2)
ox, oy = 2.5, N - 2.5


def uv(x, y):
    dx, dy = x + 0.5 - ox, y + 0.5 - oy
    return (dx - dy) / s2, (dx + dy) / s2


L = (N - 4) * s2
pommel_end = 1.8
grip_end = L * 0.2
g = grip_end                      # guard base
blade_start = g + 1.4
tip_start = L - 7.0               # long, sharp taper

for y in range(N):
    for x in range(N):
        u, v = uv(x, y)
        a = abs(v)
        c = None
        if 0 <= u < pommel_end and a < 1.3:
            c = MOON if v < 0.2 else MOON_LO
        elif pommel_end <= u < grip_end and a < 0.85:
            c = GRIP_HI if int(u * 1.3) % 2 == 0 and v < 0 else GRIP
        elif blade_start <= u < L:
            half = 1.25 if u < tip_start else 1.25 * (L - u) / (L - tip_start)
            if a < half:
                if a < 0.4:
                    c = RIDGE
                elif v < 0:
                    c = EDGE
                else:
                    c = STEEL if a < 0.9 else STEEL_LO
        if c:
            px[x, y] = c

# crescent guard, hand-placed: a bar across the blade whose tips curl toward the tip, moonstone in the middle
for p in [(5, 21), (6, 22), (8, 24), (9, 25)]:
    px[p] = SILVER
for p in [(4, 20), (4, 19), (10, 26), (11, 26)]:
    px[p] = SILVER_LO
px[7, 23] = MOON

# 1-px dark outline
src = img.copy(); sp = src.load()
for y in range(N):
    for x in range(N):
        if not sp[x, y][3] and any(
            0 <= x + dx < N and 0 <= y + dy < N and sp[x + dx, y + dy][3]
            for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))
        ):
            px[x, y] = OUT

img.save(sys.argv[1])
