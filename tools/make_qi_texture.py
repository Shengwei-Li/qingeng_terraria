"""QiStrip.png：剑气外焰和剑河用的流动气纹。256×64，横向无缝平铺。

一缕缕横向的丝，粗细、亮度、上下位置各不相同，沿横向起伏；上下边缘柔和淡出。
所有横向频率都是整数，所以首尾相接，配合 Trail 的 uScroll 往后流不会看到接缝。
usage: python make_qi_texture.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "QingyiJianxian" / "Assets" / "Effects" / "QiStrip.png"
W, H = 256, 64
rng = np.random.default_rng(7)
x = np.arange(W) / W * 2 * np.pi
y = np.linspace(-1, 1, H)[:, None]
img = np.zeros((H, W))
for _ in range(22):
    f1, f2 = rng.integers(1, 4), rng.integers(3, 9)
    p1, p2 = rng.uniform(0, 2 * np.pi, 2)
    center = rng.uniform(-0.65, 0.65) + 0.18 * np.sin(f1 * x + p1) + 0.06 * np.sin(f2 * x + p2)
    width = rng.uniform(0.03, 0.12)
    # 每一缕的亮度沿横向时明时暗，像气在烧
    bright = np.clip(0.5 + 0.5 * np.sin(rng.integers(1, 5) * x + rng.uniform(0, 6.28)), 0, 1) ** 1.5
    img += rng.uniform(0.35, 1.0) * bright * np.exp(-((y - center) / width) ** 2)
img = img / img.max()
img *= np.exp(-(y / 0.75) ** 4)  # 上下边缘淡出
img = np.clip(img * 1.25, 0, 1)
a = (img * 255).astype(np.uint8)
rgba = np.dstack([np.full_like(a, 255)] * 3 + [a])
# 预乘：颜色也随亮度走，additive 叠加时更干净
rgba[..., 0] = rgba[..., 1] = rgba[..., 2] = a
Image.fromarray(rgba, "RGBA").save(OUT)
print("wrote", OUT)
