"""Turn the finished art (design_image/定稿, 1 art pixel = 1 px) into game textures (2x, like vanilla).

Also generates a few effect textures procedurally. Re-run after changing any source art.
usage: python make_textures.py
"""
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "design_image" / "定稿"
MOD = ROOT / "QingyiJianxian"
BOSS = MOD / "Content" / "NPCs" / "SwordImmortal"
ITEMS = MOD / "Content" / "Items"
FX = MOD / "Assets" / "Effects"


def x2(src: Path, dst: Path):
    img = Image.open(src).convert("RGBA")
    dst.parent.mkdir(parents=True, exist_ok=True)
    img.resize((img.width * 2, img.height * 2), Image.Resampling.NEAREST).save(dst)
    return img


body = Image.open(ART / "剑仙_待机_64.png").convert("RGBA")

# Boss sheet, frames stacked vertically (Terraria convention), 2x:
#   0-7 idle loop, 8-15 cast (剑指), 16-21 lunge. SwordImmortal.cs relies on this order.
frames = []
for folder in ("剑仙_待机动画", "剑仙_施法", "剑仙_冲刺"):
    frames += sorted((ART / folder).glob("*.png"), key=lambda p: int(p.stem))
sheet = Image.new("RGBA", (64, 64 * len(frames)))
for i, f in enumerate(frames):
    sheet.alpha_composite(Image.open(f).convert("RGBA"), (0, 64 * i))
sheet.resize((sheet.width * 2, sheet.height * 2), Image.Resampling.NEAREST).save(BOSS / "SwordImmortal.png")
print("boss frames", len(frames))

# 初遇：倚云饮酒。0-7 饮酒循环，8-15 懒懒挥手。WanderingImmortal.cs 依赖这个顺序
lounge = []
for folder in ("剑仙_饮酒循环", "剑仙_挥手"):
    lounge += sorted((ART / folder).glob("*.png"), key=lambda p: int(p.stem))
LW, LH = Image.open(lounge[0]).size  # 倚云图是 72×72，比站姿大
lsheet = Image.new("RGBA", (LW, LH * len(lounge)))
for i, f in enumerate(lounge):
    lsheet.alpha_composite(Image.open(f).convert("RGBA"), (0, LH * i))
lsheet.resize((lsheet.width * 2, lsheet.height * 2), Image.Resampling.NEAREST).save(BOSS / "WanderingImmortal.png")
print("lounge frames", len(lounge))
x2(ART / "青锋_32.png", BOSS / "Azurebrand.png")
x2(ART / "清月剑_32.png", ITEMS / "Clearmoon.png")
x2(ART / "水墨巨剑.png", BOSS / "FinalSword.png")  # 最后一剑，剑尖朝下

# Report the figure's bounding box so the hitbox can match it.
bbox = body.getbbox()
print("boss art bbox", bbox, "-> on screen", [v * 2 for v in bbox])

# Falling leaf (bamboo leaf), 2x.
LEAF = [
    "..KKK....",
    ".KLCCKK..",
    "KLCCCccKK",
    ".KCcccK..",
    "..KKK....",
]
PAL = {"K": (24, 52, 56), "L": (170, 226, 206), "C": (96, 180, 160), "c": (52, 120, 118)}
leaf = Image.new("RGBA", (len(LEAF[0]), len(LEAF)), (0, 0, 0, 0))
for y, row in enumerate(LEAF):
    for x, ch in enumerate(row):
        if ch != ".":
            leaf.putpixel((x, y), PAL[ch] + (255,))
leaf.resize((leaf.width * 2, leaf.height * 2), Image.Resampling.NEAREST).save(BOSS / "FallingLeaf.png")

# Footing cloud (乘云), pixel art built from overlapping circles, 2x.
CW, CH = 44, 14
blobs = [(7, 9, 4.2), (14, 6, 5.0), (22.5, 4.8, 5.6), (30.5, 6.5, 4.8), (37, 9, 3.6), (20, 10.5, 5.5), (29, 10.5, 4.5)]
inside = [[any(math.hypot(x + 0.5 - bx, y + 0.5 - by) <= r for bx, by, r in blobs) and y < CH - 1 for x in range(CW)] for y in range(CH)]
cloud = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
OUTLINE, SHADE, MID, HI = (96, 150, 160), (176, 214, 216), (226, 240, 240), (252, 255, 255)
for y in range(CH):
    for x in range(CW):
        if inside[y][x]:
            edge = any(not (0 <= x + dx < CW and 0 <= y + dy < CH and inside[y + dy][x + dx])
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            above_open = y == 0 or not inside[y - 1][x] or (y > 1 and not inside[y - 2][x])
            if edge:
                c = OUTLINE
            elif y >= CH - 4:
                c = SHADE
            elif above_open:
                c = HI
            else:
                c = MID
            cloud.putpixel((x, y), c + (255,))
# a small 祥云 curl on the left end
for p in [(3, 7), (3, 8), (4, 9), (5, 9), (6, 8), (5, 7)]:
    cloud.putpixel(p, OUTLINE + (255,))
cloud.resize((CW * 2, CH * 2), Image.Resampling.NEAREST).save(BOSS / "FootCloud.png")

# Soft round glow, white, tinted in code.
FX.mkdir(parents=True, exist_ok=True)
N = 64
glow = Image.new("RGBA", (N, N))
for y in range(N):
    for x in range(N):
        d = math.hypot(x + 0.5 - N / 2, y + 0.5 - N / 2) / (N / 2)
        a = max(0.0, 1 - d) ** 2
        glow.putpixel((x, y), (255, 255, 255, int(a * 255)))
glow.save(FX / "SoftGlow.png")

# Hard-edged disc (pixel-art friendly), used for ink bursts and warning circles.
N = 32
disc = Image.new("RGBA", (N, N))
for y in range(N):
    for x in range(N):
        if math.hypot(x + 0.5 - N / 2, y + 0.5 - N / 2) <= N / 2 - 0.5:
            disc.putpixel((x, y), (255, 255, 255, 255))
disc.save(FX / "Disc.png")

# Ring outline for telegraphs.
ring = Image.new("RGBA", (N, N))
for y in range(N):
    for x in range(N):
        d = math.hypot(x + 0.5 - N / 2, y + 0.5 - N / 2)
        if N / 2 - 2.5 <= d <= N / 2 - 0.5:
            ring.putpixel((x, y), (255, 255, 255, 255))
ring.save(FX / "Ring.png")
# Brush stroke for trails (笔触，带飞白). x: 0 = tail, 1 = head; y: across the stroke.
# White with alpha; the trail renderer tints it.
import numpy as np  # noqa: E402

rng = np.random.default_rng(7)
BW, BH = 256, 48
ys = (np.arange(BH) + 0.5) / BH * 2 - 1                       # -1..1 across
edge = np.clip(1 - np.abs(ys) ** 3, 0, 1)                      # soft but full-bodied edges
bristle = np.clip(rng.normal(0.97, 0.12, BH), 0.35, 1.0)        # each row is a bristle
breakup = rng.random(BH)                                       # where each bristle runs dry
xs = (np.arange(BW) + 0.5) / BW                                # 0 tail .. 1 head
alpha = np.zeros((BH, BW))
for y in range(BH):
    # 越往尾部越干：每根毛在自己的位置断开，留下飞白
    dry = np.clip((xs - breakup[y] * 0.55) / 0.12, 0, 1)
    streak = 1 - 0.18 * np.abs(np.sin(xs * (9 + 13 * rng.random()) + rng.random() * 6))
    alpha[y] = edge[y] * bristle[y] * dry * streak
# the head end is pressed down: a fuller, rounder start
head = np.clip((1 - xs) / 0.06, 0, 1)
alpha *= head[None, :] * 0.25 + 0.75
alpha = np.clip(alpha * 1.35, 0, 1)
brush = np.zeros((BH, BW, 4), np.uint8)
brush[..., :3] = 255
brush[..., 3] = (alpha * 255).astype(np.uint8)
Image.fromarray(brush, "RGBA").save(FX / "BrushStroke.png")

# Plain soft strip (for glowing sword trails): bright core fading to the edges and the tail.
strip = np.zeros((32, 128, 4), np.uint8)
yy = (np.arange(32) + 0.5) / 32 * 2 - 1
xx = (np.arange(128) + 0.5) / 128
a = np.clip(1 - np.abs(yy) ** 1.5, 0, 1)[:, None] * xx[None, :] ** 0.8
strip[..., :3] = 255
strip[..., 3] = (a * 255).astype(np.uint8)
Image.fromarray(strip, "RGBA").save(FX / "GlowStrip.png")
# ── 水墨山水背景（二阶段）。山是白色 + alpha，代码里染墨色；横向可无缝平铺 ──
SKY = MOD / "Assets" / "Sky"
SKY.mkdir(parents=True, exist_ok=True)


def mountains(path, w, h, seed, base, amp, harmonics, ridge_dark, streak_amt):
    r = np.random.default_rng(seed)
    x = np.arange(w) / w
    # 脊状噪声：1-|sin| 叠加，得到尖峰而不是圆包；整数频率保证横向无缝
    peaks = np.zeros(w)
    for k in range(1, harmonics + 1):
        a = 1 / k ** 0.9
        peaks += a * (1 - np.abs(np.sin(np.pi * k * x + r.random() * 3.1416))) ** 2
    peaks = (peaks - peaks.min()) / (peaks.max() - peaks.min())
    ridge = base - amp * peaks                  # 越高的峰，山脊越靠上
    ys = np.arange(h)[:, None]
    depth = ys - ridge[None, :] * h             # 山脊以下为正
    body = np.clip(depth / (h * 0.42), 0, 1)
    alpha = np.where(depth >= 0, (1 - body) ** 2.2, 0.0)   # 山脊最浓，往下化进雾里
    edge = np.clip(1 - depth / 5.0, 0, 1) * (depth >= 0)    # 山脊一道墨线
    alpha = np.maximum(alpha * 0.8, edge * ridge_dark)
    # 竖向皴擦：平滑过的随机明暗，像笔扫过
    n = r.random(w)
    kernel = np.array([1, 2, 3, 2, 1], float); kernel /= kernel.sum()
    n = np.convolve(np.concatenate([n[-2:], n, n[:2]]), kernel, "valid")
    alpha = np.clip(alpha * (1 - streak_amt * n[None, :]), 0, 1)
    return alpha, (ridge * h).astype(int)


def save_alpha(path, alpha):
    img = np.zeros(alpha.shape + (4,), np.uint8)
    img[..., :3] = 255
    img[..., 3] = (np.clip(alpha, 0, 1) * 255).astype(np.uint8)
    Image.fromarray(img, "RGBA").save(path)


def stamp(alpha, mask, x0, y0, strength):
    """Paints a silhouette mask (bottom-centered at x0, y0) into an alpha map."""
    mh, mw = mask.shape
    for my in range(mh):
        for mx in range(mw):
            if mask[my, mx]:
                x, y = (x0 - mw // 2 + mx) % alpha.shape[1], y0 - mh + 1 + my
                if 0 <= y < alpha.shape[0]:
                    alpha[y, x] = max(alpha[y, x], strength * mask[my, mx])


def pagoda_mask(tiers=5):
    """塔：逐层收窄，檐角上翘，顶上一根塔刹。"""
    body_h, roof_h = 7, 3
    H = tiers * (body_h + roof_h) + 10
    W = 30
    m = np.zeros((H, W))
    y = H - 1
    for i in range(tiers):
        bw = 14 - 2 * i
        rw = bw + 10
        for yy in range(body_h):                         # 塔身
            m[y - yy, W // 2 - bw // 2: W // 2 + bw // 2] = 1
            if yy in (2, 3):                             # 门窗留白
                m[y - yy, W // 2 - 1: W // 2 + 1] = 0.35
        y -= body_h
        for yy in range(roof_h):                         # 塔檐：下宽上窄
            w2 = rw - yy * 4
            m[y - yy, W // 2 - w2 // 2: W // 2 + w2 // 2] = 1
        m[y - roof_h + 1, W // 2 - rw // 2] = 1          # 檐角上翘
        m[y - roof_h + 1, W // 2 + rw // 2 - 1] = 1
        y -= roof_h
    for yy in range(9):                                  # 塔刹
        m[y - yy, W // 2] = 1
    m[y - 4, W // 2 - 1: W // 2 + 2] = 1
    return m


def pine_mask(height, seed, lean):
    """水墨松：微弯的树干，左右错落几簇扁平的松针。"""
    r = np.random.default_rng(seed)
    W = 70
    m = np.zeros((height, W))
    cx = W / 2
    for yy in range(height):                             # 树干，下粗上细，带一点弯
        t = yy / height
        x = cx + lean * t * t * 14
        thick = 3.2 - 2.2 * t
        m[height - 1 - yy, int(x - thick / 2): int(x + thick / 2) + 1] = 1
    for k, t in enumerate([0.42, 0.58, 0.72, 0.86, 0.98]):
        side = -1 if k % 2 == 0 else 1
        if k == 4:
            side = 0
        yc = height - 1 - int(t * height)
        xc = cx + lean * t * t * 14 + side * (5 + r.random() * 4)
        rx, ry = 7 + r.random() * 4 - k * 0.6, 2.2 + r.random() * 0.8
        for yy in range(int(yc - ry - 1), int(yc + ry + 2)):
            for xx in range(int(xc - rx - 1), int(xc + rx + 2)):
                if 0 <= yy < height and 0 <= xx < W:
                    d = ((xx - xc) / rx) ** 2 + ((yy - yc) / ry) ** 2
                    if d <= 1 and not (yy > yc and r.random() < 0.25):  # 下沿参差
                        m[yy, xx] = 1
        # 一根横枝连到簇上
        x0 = cx + lean * t * t * 14
        for xx in range(int(min(x0, xc)), int(max(x0, xc)) + 1):
            m[yc + 1, xx] = 1
    return m


far, far_ridge = mountains(SKY / "MountainsFar.png", 1024, 320, 11, base=0.62, amp=0.38, harmonics=7, ridge_dark=0.5, streak_amt=0.3)
px_peak = int(np.argmin(far_ridge))
stamp(far, pagoda_mask(), px_peak, far_ridge[px_peak] + 3, 0.7)
save_alpha(SKY / "MountainsFar.png", far)

near, near_ridge = mountains(SKY / "MountainsNear.png", 1024, 320, 23, base=0.72, amp=0.5, harmonics=9, ridge_dark=0.85, streak_amt=0.35)
# 松树种在近山几个较高的山头上
order = np.argsort(near_ridge)
spots = []
for x in order:
    if all(min(abs(x - s), 1024 - abs(x - s)) > 220 for s in spots):
        spots.append(int(x))
    if len(spots) == 3:
        break
for i, x in enumerate(spots):
    stamp(near, pine_mask(34 + i * 5, 40 + i, lean=1 if i % 2 else -1), x, near_ridge[x] + 4, 0.92)
save_alpha(SKY / "MountainsNear.png", near)

# 大雁：两帧，翅膀上扬 / 平展。白色，代码里染墨色
GOOSE = [
    ["X.........X",
     ".X.......X.",
     "..X.....X..",
     "...XXXXX...",
     ".....X.....",],
    ["...........",
     "...........",
     "XXX.....XXX",
     "...XXXXX...",
     ".....X.....",],
]
goose = Image.new("RGBA", (11 * 2, 5), (0, 0, 0, 0))
for f, rows in enumerate(GOOSE):
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch == "X":
                goose.putpixel((f * 11 + x, y), (255, 255, 255, 255))
goose.save(SKY / "Goose.png")

# 宣纸色天空渐变：上淡下略深，烘焙好颜色
g = np.zeros((256, 4, 4), np.uint8)
t = np.linspace(0, 1, 256)[:, None]
top, bottom = np.array([236, 233, 222]), np.array([198, 200, 194])
g[..., :3] = (top * (1 - t) + bottom * t)[:, None, :].repeat(4, 1).astype(np.uint8)
g[..., 3] = 255
Image.fromarray(g, "RGBA").save(SKY / "PaperGradient.png")
print("textures written")
