"""Boss 标配物品的贴图：宝藏袋、纪念杯（墙挂 3×3）、遗物（落地 3×3）、斗笠（头部 20 帧）、清流剑符，以及模组图标和创意工坊封面。

素材来自 design_image/定稿（PixelLab 挑出的图）和玩家生成的法相图。
usage: python make_boss_items.py
"""
import math
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent.parent
ART = ROOT / "design_image" / "定稿"
MOD = ROOT / "QingyiJianxian"
ITEMS = MOD / "Content" / "Items"
TILES = MOD / "Content" / "Tiles"
TILES.mkdir(parents=True, exist_ok=True)


def x2(img):
    return img.resize((img.width * 2, img.height * 2), Image.Resampling.NEAREST)


def tile_sheet(img, cols, rows):
    """把一张 (cols*16)×(rows*16) 的图切成物块帧：每块 16×16，块与块之间留 2 像素（原版物块贴图的排布）。"""
    canvas = Image.new("RGBA", (cols * 16, rows * 16), (0, 0, 0, 0))
    canvas.alpha_composite(img, ((canvas.width - img.width) // 2, canvas.height - img.height))
    sheet = Image.new("RGBA", (cols * 18, rows * 18), (0, 0, 0, 0))
    for r in range(rows):
        for c in range(cols):
            sheet.alpha_composite(canvas.crop((c * 16, r * 16, c * 16 + 16, r * 16 + 16)), (c * 18, r * 18))
    return sheet


# 宝藏袋
bag = Image.open(ART / "宝藏袋.png").convert("RGBA")
bag.save(ITEMS / "SwordImmortalBag.png")  # 原尺寸，理由同 draw_icons.py

# 纪念杯：48×48 的画框，1:1 铺成 3×3 墙挂物块；物品图标就是这幅画
trophy = Image.open(ART / "纪念杯.png").convert("RGBA").resize((48, 48), Image.Resampling.NEAREST)
trophy.save(ITEMS / "SwordImmortalTrophy.png")
tile_sheet(trophy, 3, 3).save(TILES / "SwordImmortalTrophyTile.png")

# 遗物：落地 3×3
relic = Image.open(ART / "遗物.png").convert("RGBA")
relic.thumbnail((48, 48), Image.Resampling.NEAREST)
relic.save(ITEMS / "SwordImmortalRelic.png")
tile_sheet(relic, 3, 3).save(TILES / "SwordImmortalRelicTile.png")

# ── 斗笠：竹编斗笠，帽檐垂一圈淡青纱，遮住大半张脸，只露下巴。玩家自己的头照常画，斗笠盖在上面 ──
# 头部位置量自 ExampleMod 的兜帽模板：普通帧头在 x 8-30、y 10-30；第 7-9、14-16 帧上移 2 像素
STRAW, STRAW_D, STRAW_HI, OUT = (196, 166, 108), (138, 106, 66), (228, 206, 152), (40, 30, 22)
TEAL, TEAL_D = (108, 171, 152), (44, 102, 97)
VEIL = (200, 236, 228)
BOB = {7, 8, 9, 14, 15, 16}


def draw_hat(px, cx, top, w, h, veil_rows, sway=0):
    """在 px 上画一顶斗笠：顶点 (cx, top)，帽檐半宽 w、高 h；veil_rows 行纱。"""
    brim = top + h
    for y in range(top, brim + 1):
        k = (y - top) / h
        half = max(0, round(w * k ** 0.85))
        for x in range(cx - half, cx + half + 1):
            # 竹篾：从顶点放射的细纹，越往外越密
            rad = (x - cx) / max(half, 1)
            stripe = round(rad * 5) % 2 == 0
            c = STRAW_HI if (y - top) < 2 and x <= cx else (STRAW if stripe else STRAW_D)
            px[x, y] = c + (255,)
    # 帽檐边一道深色，帽顶一圈青带
    for x in range(cx - w, cx + w + 1):
        px[x, brim] = STRAW_D + (255,)
    band = top + max(2, h // 3)
    bh = round(w * ((band - top) / h) ** 0.85)
    for x in range(cx - bh, cx + bh + 1):
        px[x, band] = TEAL + (255,)
    # 淡青纱：从帽檐内侧垂下，略向外张；下摆起伏不齐，越往下越淡；走路时末梢顺着一侧轻飘
    for i in range(veil_rows):
        y = brim + 1 + i
        flare = round(i * 0.12)
        drift = round(sway * i / veil_rows)
        for xx in range(cx - w + 2 - flare + drift, cx + w - 1 + flare + drift):
            x = xx - drift
            u = (x - cx) / w
            hem = veil_rows - round(1.5 * abs(u)) + round(math.sin(x * 1.7) * 1.2)
            if i >= hem:
                continue
            fade = 1 - i / (veil_rows + 2)
            a = int((150 if (x % 3) else 190) * (0.45 + 0.55 * fade))
            if 0 <= xx < len_x(px) and 0 <= y < len_y(px) and px[xx, y][3] < a:
                px[xx, y] = VEIL + (a,)


SIZES = {}


def len_x(px):
    return SIZES[id(px)][0]


def len_y(px):
    return SIZES[id(px)][1]


def outline(img):
    src = img.copy(); sp = src.load(); px = img.load()
    for y in range(img.height):
        for x in range(img.width):
            if sp[x, y][3] == 0 and any(0 <= x + dx < img.width and 0 <= y + dy < img.height and sp[x + dx, y + dy][3] >= 255
                                        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                px[x, y] = OUT + (255,)


hat = Image.new("RGBA", (40, 56 * 20), (0, 0, 0, 0))
for f in range(20):
    frame = Image.new("RGBA", (40, 56), (0, 0, 0, 0))
    top = 10 - (2 if f in BOB else 0)
    sway = round(math.sin(f * 1.1)) if f >= 6 else 0
    # 头顶在 top 附近，斗笠顶点高出 6 像素，帽檐压到眉上；纱垂到鼻下，下巴露出来
    fp = frame.load(); SIZES[id(fp)] = frame.size
    draw_hat(fp, 20, top - 7, 15, 8, 14, sway)
    outline(frame)
    hat.alpha_composite(frame, (0, 56 * f))
hat.save(ITEMS / "Armor" / "SwordImmortalHat_Head.png")
# 图标：同样的斗笠，画大一号
icon_hat = Image.new("RGBA", (32, 28), (0, 0, 0, 0))
ip = icon_hat.load(); SIZES[id(ip)] = icon_hat.size
draw_hat(ip, 16, 1, 14, 9, 15)
outline(icon_hat)
icon_hat.save(ITEMS / "Armor" / "SwordImmortalHat.png")

# 清流剑符：PixelLab 生成，原尺寸
Image.open(ART / "清流剑符.png").convert("RGBA").save(ITEMS / "LingeringStreamTalisman.png")

# ── 模组图标（80×80）和创意工坊封面（512×512）：取自法相图的脸和剑 ──
src = Image.open(ROOT / "design_image" / "Stoic Swordsman Amidst Pixel Clouds.png").convert("RGB")
icon = src.crop((740, 150, 1080, 490)).resize((80, 80), Image.Resampling.LANCZOS)
icon.save(MOD / "icon.png")
cover = src.crop((476, 0, 1417, 941)).resize((512, 512), Image.Resampling.LANCZOS)
cover.save(MOD / "icon_workshop.png")
print("boss items, hat, talisman, icons written")
