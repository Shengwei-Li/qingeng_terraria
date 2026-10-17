"""青衫·发冠的穿戴贴图：20 帧竖排，每帧 40×56，人物朝右。

只画头顶的玉冠、白玉簪和一条飘在脑后的青发带，不遮头发（物品里设了 DrawFullHair）。
头顶位置量自 ExampleMod 的头盔模板：普通帧头顶在 y≈10，第 7-9、14-16 帧（走路颠起）上移 2 像素。
usage: python make_crown.py
"""
import math
from pathlib import Path

from PIL import Image

OUT = Path(__file__).resolve().parent.parent / "QingyiJianxian" / "Content" / "Items" / "Armor" / "AzureWayfarerCrown_Head.png"

PAL = {
    "K": (19, 17, 16),       # 描边
    "J": (150, 204, 186),    # 玉冠亮部
    "j": (108, 171, 152),    # 玉冠
    "d": (44, 102, 97),      # 玉冠暗部 / 冠带
    "W": (254, 254, 238),    # 白玉簪
    "w": (221, 217, 201),
    "R": (108, 171, 152),    # 发带
    "r": (44, 102, 97),
}

# 玉冠和簪子，坐标相对于头顶中心 (19, 头顶 y)
CROWN = [
    (-1, -5, "KKK"),
    (-2, -4, "KJjK"),
    (-3, -3, "KJjjdK"),
    (-7, -2, "KwWWWjjjdWWWWK"),  # 白玉簪横穿玉冠
    (-3, -1, "KdddddK"),
]
BOB_FRAMES = {7, 8, 9, 14, 15, 16}


def ribbon_points(frame):
    """发带：从冠后垂下，贴着脑后往下落，末梢微微往后飘。走路帧里末梢左右摆一点。"""
    sway = math.sin(frame * 1.1) * 1.0 if frame >= 6 else 0.0
    pts = []
    for i in range(11):
        t = i / 10
        x = -4 - 0.25 * i - 2.2 * t * t * t + sway * t * t
        y = -1 + i
        pts.append((round(x), round(y)))
    return pts


def frame_image(frame):
    img = Image.new("RGBA", (40, 56), (0, 0, 0, 0))
    px = img.load()
    cx, top = 19, 10 - (2 if frame in BOB_FRAMES else 0)

    def put(x, y, ch):
        if 0 <= x < 40 and 0 <= y < 56 and ch != ".":
            px[x, y] = PAL[ch] + (255,)

    # 发带先画（在冠后面），两股并行，带描边
    for k, (dx, dy) in enumerate(ribbon_points(frame)):
        for off, ch in ((0, "R"), (-1, "r")):
            put(cx + dx + off, top + dy, ch)
        put(cx + dx + 1, top + dy, "K") if k > 1 else None
        put(cx + dx - 2, top + dy, "K")
    for dx, dy, row in CROWN:
        for i, ch in enumerate(row):
            put(cx + dx + i, top + dy, ch)
    return img


sheet = Image.new("RGBA", (40, 56 * 20), (0, 0, 0, 0))
for f in range(20):
    sheet.alpha_composite(frame_image(f), (0, 56 * f))
OUT.parent.mkdir(parents=True, exist_ok=True)
sheet.save(OUT)
print("crown sheet written", sheet.size)
