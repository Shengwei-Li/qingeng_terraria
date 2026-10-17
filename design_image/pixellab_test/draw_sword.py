"""Procedurally draw 青锋, a straight jian, on a 45° diagonal (hilt bottom-left, tip top-right).

Terraria item sprites point up-right like this. The same sprite is held by the boss.
usage: python draw_sword.py <out.png> [size]
"""
import math
import sys
from PIL import Image

N = int(sys.argv[2]) if len(sys.argv) > 2 else 32
img = Image.new("RGBA", (N, N), (0, 0, 0, 0))
px = img.load()

OUT = (22, 28, 38, 255)
STEEL_HI = (236, 246, 248, 255)
STEEL = (184, 204, 212, 255)
STEEL_LO = (120, 146, 158, 255)
RIDGE = (148, 218, 210, 255)      # faint teal glint along the ridge
BRASS = (214, 190, 128, 255)
BRASS_LO = (150, 124, 74, 255)
GRIP = (34, 52, 60, 255)
GRIP_HI = (74, 167, 166, 255)
TEAL = (74, 167, 166, 255)
TEAL_LO = (42, 111, 120, 255)
RED = (190, 56, 46, 255)

s2 = math.sqrt(2)
ox, oy = 3.5, N - 6.5   # pommel end; room below for the tassel


def uv(x, y):
    dx, dy = x + 0.5 - ox, y + 0.5 - oy
    return (dx - dy) / s2, (dx + dy) / s2   # u: along the sword, v: across it


L = (N - 8) * s2            # total length along the diagonal
pommel_end = 2.2
grip_end = L * 0.22
guard_end = grip_end + 2.1
tip_start = L - 4.5

for y in range(N):
    for x in range(N):
        u, v = uv(x, y)
        a = abs(v)
        c = None
        if 0 <= u < pommel_end and a < 1.6:
            c = BRASS if v < 0.3 else BRASS_LO
        elif pommel_end <= u < grip_end and a < 1.0:
            c = GRIP_HI if int(u * 1.4) % 2 == 0 and v < 0 else GRIP
        elif grip_end <= u < guard_end and a < 3.6:
            c = BRASS if v < 0.6 else BRASS_LO
        elif guard_end <= u < L:
            half = 1.45 if u < tip_start else 1.45 * (L - u) / (L - tip_start)
            if a < half:
                if a < 0.45:
                    c = RIDGE if u > guard_end + 2 else STEEL
                elif v < 0:
                    c = STEEL_HI
                else:
                    c = STEEL_LO
        if c:
            px[x, y] = c

# 1-px dark outline around everything drawn so far
src = img.copy(); sp = src.load()
for y in range(N):
    for x in range(N):
        if sp[x, y][3]:
            continue
        if any(0 <= x + dx < N and 0 <= y + dy < N and sp[x + dx, y + dy][3]
               for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
            px[x, y] = OUT

# tassel hanging from the pommel: cord, red bead, teal fringe
tx, ty = 3, N - 6
for dx, dy, c in [(0, 1, TEAL_LO), (0, 2, RED), (-1, 3, TEAL), (0, 3, TEAL), (1, 3, TEAL_LO),
                  (-1, 4, TEAL_LO), (0, 4, TEAL), (1, 4, TEAL), (0, 5, TEAL_LO)]:
    x, y = tx + dx, ty + dy
    if 0 <= x < N and 0 <= y < N:
        px[x, y] = c

img.save(sys.argv[1])
