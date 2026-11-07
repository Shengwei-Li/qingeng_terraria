"""法天相地：把玩家生成的法相图（design_image/Stoic Swordsman Amidst Pixel Clouds.png）做成天幕贴图。

输出（原图全分辨率，游戏里只放大约 1.15 倍，不会糊成色块）：
  Colossus.png        整幅一层。只去掉白底，人物本身不透明——法相要有分量，不是一层半透明的影子
  ColossusClosed.png  闭眼版，眨眼时换上

不再拆成"外层随风错动 + 静止核心"两层：条带错动会把长发撕成锯齿，两层的接缝在脸旁边留下一道折痕。
法相的动改由游戏里整体的呼吸、浮沉、眨眼、剑光和身前身后的流云来表现。

半透明像素预乘：游戏按预乘透明来画，不预乘，淡的边缘会发白发亮。
usage: python make_colossus.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "design_image" / "Stoic Swordsman Amidst Pixel Clouds.png"
OUT = ROOT / "QingyiJianxian" / "Assets" / "Sky"

img = Image.open(SRC).convert("RGB")
a = np.asarray(img).astype(float)
h, w = a.shape[:2]

# 只去掉接近纯白的底（纸也是白的）；白袍略带灰蓝，保留不透明
whiteness = a.min(axis=2)
alpha = np.clip((248 - whiteness) / 16, 0, 1)


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


ys = np.arange(h)[:, None] / h
xs = np.arange(w)[None, :] / w
# 四边化进纸里，不留硬边；下缘化得更长，像从云里升起来
alpha *= smooth(xs / 0.07) * smooth((1 - xs) / 0.07) * smooth((1 - ys) / 0.28)


def save(rgb, path):
    out = np.zeros((h, w, 4), np.uint8)
    out[..., :3] = (rgb * alpha[..., None]).astype(np.uint8)  # 预乘
    out[..., 3] = (alpha * 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(path)


save(a, OUT / "Colossus.png")

# 闭眼版：眼睛在原图 x 846-888、y 351-360（上眼线在 348-351）。
# 眼眶里填上眼睑（肤色取自眼下方，上深下浅），沿杏仁形收边；中间一道下弯的闭眼线，两头往上收
closed = a.copy()
EYE_X0, EYE_X1, TOP, BOTTOM = 846, 888, 351, 361
skin = a[363:366, EYE_X0:EYE_X1 + 1].reshape(-1, 3).mean(axis=0)
cx = (EYE_X0 + EYE_X1) / 2
half = (EYE_X1 - EYE_X0) / 2
for x in range(EYE_X0, EYE_X1 + 1):
    t = (x - cx) / half
    bottom = BOTTOM - round(3 * t * t)            # 杏仁形：两头收窄
    for y in range(TOP, bottom):
        k = (y - TOP) / max(bottom - TOP, 1)
        shade = 0.86 + 0.14 * k                   # 上深下浅
        closed[y, x] = skin * shade
    lash = TOP + 4 + round(3 * (1 - t * t))       # 闭眼线：中间低、两头高
    if lash < bottom + 1:
        for y in range(lash, min(lash + 2, bottom + 1)):
            closed[y, x] = (62, 44, 44)
save(closed, OUT / "ColossusClosed.png")

# ── 流云：直接从原图里裁下它自己的祥云（和法相同一种画风），游戏里在法相身前身后横飘 ──
# 底色是纯白：从四边往里泛洪，连着边的近白像素才去掉；云肚子里的浅灰白保留不透明
from collections import deque


def cut_cloud(box, name):
    c = a[box[1]:box[3], box[0]:box[2]].copy()
    ch, cw = c.shape[:2]
    bg = np.zeros((ch, cw), bool)
    near_white = c.min(axis=2) >= 246
    q = deque([(y, x) for y in range(ch) for x in (0, cw - 1)] + [(y, x) for x in range(cw) for y in (0, ch - 1)])
    while q:
        y, x = q.popleft()
        if 0 <= y < ch and 0 <= x < cw and not bg[y, x] and near_white[y, x]:
            bg[y, x] = True
            q.extend(((y + 1, x), (y - 1, x), (y, x + 1), (y, x - 1)))
    al = (~bg).astype(float)
    # 青色发带的残片去掉（只要灰白的云）
    teal = (c[..., 2] - c[..., 0] > 40) & (c[..., 1] - c[..., 0] > 20)
    al[teal] = 0
    # 裁切边上被切断的云：靠边 14 像素渐隐，不留一刀切的直边
    yy, xx = np.mgrid[0:ch, 0:cw]
    edge = np.minimum.reduce([xx, cw - 1 - xx, yy, ch - 1 - yy]) / 14.0
    al *= smooth(edge)
    out = np.zeros((ch, cw, 4), np.uint8)
    out[..., :3] = (c * al[..., None]).astype(np.uint8)
    out[..., 3] = (al * 255).astype(np.uint8)
    Image.fromarray(out, "RGBA").save(OUT / name)


cut_cloud((1340, 150, 1620, 310), "Cloud0.png")
cut_cloud((20, 290, 235, 430), "Cloud1.png")

for old in ("ColossusCore.png", "ColossusCoreClosed.png"):
    p = OUT / old
    if p.exists():
        p.unlink()
print("colossus written", (w, h))
