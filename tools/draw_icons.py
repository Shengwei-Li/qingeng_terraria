"""Hand-placed pixel icons for consumables, buffs and the cloud mount. Writes straight into the mod at 2x.

usage: python draw_icons.py
"""
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
MOD = ROOT / "QingyiJianxian"
ART = ROOT / "design_image" / "定稿"

PAL = {
    "K": (36, 30, 34),                                          # outline
    "W": (238, 240, 236), "w": (196, 208, 206), "v": (150, 166, 168),  # porcelain
    "C": (74, 167, 166), "c": (42, 111, 120), "L": (148, 218, 210),    # teal
    "R": (196, 58, 46), "r": (122, 34, 30),                     # red label / cord
    "Y": (226, 196, 120),                                       # label text gold
    "B": (120, 84, 50), "b": (82, 56, 34),                      # cork
    "S": (232, 242, 250), "s": (150, 170, 196),                 # cup
    "O": (96, 150, 160), "H": (252, 255, 255), "M": (226, 240, 240), "D": (176, 214, 216),  # cloud
}

# 青云药酒：一只青白瓷梅瓶，红纸封口，瓶身一圈青色云纹
WINE = [
    "....KKKK....",
    "...KrRRrK...",
    "....KBBK....",
    "....KbBK....",
    "...KWWwvK...",
    "..KWWWWwvK..",
    ".KWWLCWWwvK.",
    ".KWCLLCWwvK.",
    ".KWWWWWWwvK.",
    ".KWRRRRRwvK.",
    ".KWRYRYRwvK.",
    ".KWRRRRRwvK.",
    ".KWWWWWWwvK.",
    "..KWCLCWvK..",
    "..KWWWWwvK..",
    "...KWWwvK...",
    "...KKKKKK...",
]

# 微醺（增益图标）：一只小酒杯，杯口一缕青气
TIPSY = [
    "................",
    "........L.......",
    ".......L.L......",
    "......L...L.....",
    ".......L........",
    "........L.......",
    "................",
    "...KKKKKKKKKK...",
    "...KSSSSSSSSK...",
    "...KSCCCCCCsK...",
    "....KSSSSSsK....",
    ".....KSSSsK.....",
    "......KSsK......",
    ".......KK.......",
    ".....KKKKKK.....",
    "................",
]


def render(rows):
    w = max(len(r) for r in rows)
    img = Image.new("RGBA", (w, len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), PAL[ch] + (255,))
    return img


def x2(img):
    return img.resize((img.width * 2, img.height * 2), Image.Resampling.NEAREST)


def cloud(width, height, blobs, shift):
    """Pixel-art cloud from overlapping circles; `shift` nudges the puffs for an animation frame."""
    inside = [[any(math.hypot(x + 0.5 - (bx + shift * ((i % 2) * 2 - 1)), y + 0.5 - by) <= r
                   for i, (bx, by, r) in enumerate(blobs)) and y < height - 1
               for x in range(width)] for y in range(height)]
    img = Image.new("RGBA", (width, height), (0, 0, 0, 0))
    for y in range(height):
        for x in range(width):
            if not inside[y][x]:
                continue
            edge = any(not (0 <= x + dx < width and 0 <= y + dy < height and inside[y + dy][x + dx])
                       for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1)))
            top = y == 0 or not inside[y - 1][x] or (y > 1 and not inside[y - 2][x])
            c = PAL["O"] if edge else PAL["D"] if y >= height - 4 else PAL["H"] if top else PAL["M"]
            img.putpixel((x, y), c + (255,))
    return img


items = MOD / "Content" / "Items"
buffs = MOD / "Content" / "Buffs"
mounts = MOD / "Content" / "Mounts"
for d in (items, buffs, mounts):
    d.mkdir(parents=True, exist_ok=True)

# 物品图标：用 PixelLab 生成后挑出的定稿（design_image/定稿）。PixelLab 已按物品尺寸画好（约 30 像素），
# 原尺寸放进模组；再放大 2 倍，掉在地上就和人一样高了
for name, dst in [("青云药酒.png", items / "AzureCloudDraught.png"),
                  ("御剑诀.png", items / "LitanyOfSwordflight.png"),
                  ("剑穗.png", items / "InkwakeTassel.png"),
                  ("一壶酒.png", items / "CloudbrewGourd.png"),
                  ("云纹绡.png", items / "CloudweaveSilk.png"),
                  ("青衫剑袍.png", items / "Armor" / "AzureWayfarerRobe.png"),
                  ("青衫云履.png", items / "Armor" / "AzureWayfarerBoots.png"),
                  ("青衫发冠.png", items / "Armor" / "AzureWayfarerCrown.png")]:
    Image.open(ART / name).convert("RGBA").save(dst)

x2(render(TIPSY)).save(buffs / "Tipsy.png")

WILL = [
    "................",
    ".............LS.",
    "....L.......LSs.",
    "...........LSs..",
    "..........LSs...",
    ".........LSs..L.",
    "........LSs.....",
    ".......LSs......",
    "..L..sLSs.......",
    "......Ys........",
    ".....YKY........",
    "....KcY.....L...",
    "...KcK..........",
    "..Rc............",
    ".RR.....L.......",
    "................",
]
x2(render(WILL)).save(buffs / "SwordWill.png")

# 剑被夺（减益图标）：一柄清月剑，被一道朱砂红叉划过
DISARMED = [
    "................",
    ".............KS.",
    "............KSsK",
    "...........KSsK.",
    "..R.......KSsK.R",
    "...R.....KSsK.R.",
    "....R...KSsK.R..",
    ".....R.KSsK.R...",
    "......RSsKRR....",
    ".....KYRRR......",
    "....KcYKRR......",
    "...KcK.R..R.....",
    "..KcK.R....R....",
    ".KcK.R......R...",
    ".KK.R........R..",
    "................",
]
x2(render(DISARMED)).save(buffs / "Disarmed.png")

# 坐骑：两帧，第二帧整体下沉 1 像素，像云在轻轻呼吸。帧竖着排
c = Image.open(ART / "坐骑_云.png").convert("RGBA")
fw, fh = c.width, c.height + 1
sheet = Image.new("RGBA", (fw, fh * 2))
sheet.alpha_composite(c, (0, 0))
sheet.alpha_composite(c, (0, fh + 1))
x2(sheet).save(mounts / "WanderingCloud_Back.png")
# 前景层：云的下半部分画在人前面，脚陷进云里，人和云才像在同一层
front = sheet.copy()
fp = front.load()
for fy in range(2):
    for y in range(fh * fy, fh * fy + int(fh * 0.45)):
        for x in range(fw):
            fp[x, y] = (0, 0, 0, 0)
x2(front).save(mounts / "WanderingCloud_Front.png")

# 坐骑增益图标：缩小的云
icon = Image.new("RGBA", (16, 16), (0, 0, 0, 0))
small = c.resize((16, max(1, round(c.height * 16 / c.width))), Image.Resampling.NEAREST)
icon.alpha_composite(small, (0, (16 - small.height) // 2))
x2(icon).save(buffs / "WanderingCloudBuff.png")
print("icons written")
