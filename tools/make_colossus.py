"""法天相地：把玩家生成的法相图（design_image/Stoic Swordsman Amidst Pixel Clouds.png）做成天幕贴图。

输出两层（都缩小一半，像素块和游戏尺度接近）：
  Colossus.png      整幅，白底去掉；游戏里按水平条带波动，长发、发带、衣袖、云随风飘
  ColossusCore.png  只有中间的脸、剑、握剑的手（边缘柔和过渡），静止地盖在上面
usage: python make_colossus.py
"""
from pathlib import Path

import numpy as np
from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
SRC = ROOT / "design_image" / "Stoic Swordsman Amidst Pixel Clouds.png"
OUT = ROOT / "QingyiJianxian" / "Assets" / "Sky"

img = Image.open(SRC).convert("RGB")
img = img.resize((img.width // 2, img.height // 2), Image.Resampling.BOX)
a = np.asarray(img).astype(float)
h, w = a.shape[:2]

# 白底去掉：越接近纯白越透明（白袍本身略带灰蓝，仍然保留；纸也是白的，看不出差别）
whiteness = a.min(axis=2)
alpha = np.clip((255 - whiteness) / 40, 0, 1)


def smooth(t):
    t = np.clip(t, 0, 1)
    return t * t * (3 - 2 * t)


ys = np.arange(h)[:, None] / h
xs = np.arange(w)[None, :] / w
# 四边化进纸里，不留硬边
alpha *= smooth(xs / 0.08) * smooth((1 - xs) / 0.08) * smooth((1 - ys) / 0.2)

rgba = np.zeros((h, w, 4), np.uint8)
rgba[..., :3] = a.astype(np.uint8)
rgba[..., 3] = (alpha * 255).astype(np.uint8)
Image.fromarray(rgba, "RGBA").save(OUT / "Colossus.png")

# 静止核心：脸、剑和握剑的手，一条竖带（原图 x≈800-1010），左右各 20 像素柔和过渡
x0, x1, feather = 800 / 2, 1010 / 2, 20
px = np.arange(w)
core = np.clip(np.minimum(px - (x0 - feather), (x1 + feather) - px) / feather, 0, 1)[None, :]
core_rgba = rgba.copy()
core_rgba[..., 3] = (alpha * core * 255).astype(np.uint8)
Image.fromarray(core_rgba, "RGBA").save(OUT / "ColossusCore.png")
print("colossus layers written", (w, h))
