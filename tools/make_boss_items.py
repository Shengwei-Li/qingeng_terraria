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

# ── 斗笠：PixelLab 生成的斗笠（斜戴、垂纱），图标原样用；戴在头上的 20 帧就是同一张图按头的位置摆上去 ──
# 头部位置量自 ExampleMod 的兜帽模板：普通帧头顶在 y 10；第 7-9、14-16 帧上移 2 像素（走路时头一起一伏）
# 摆法：斗笠的尖在头顶上方，帽檐压在眉上，纱垂到下巴上方；横向居中在头上（人物朝右）
BOB = {7, 8, 9, 14, 15, 16}
douli = Image.open(ART / "斗笠.png").convert("RGBA")
douli.save(ITEMS / "Armor" / "SwordImmortalHat.png")
hat = Image.new("RGBA", (40, 56 * 20), (0, 0, 0, 0))
for f in range(20):
    top = 10 - (2 if f in BOB else 0)
    frame = Image.new("RGBA", (40, 56), (0, 0, 0, 0))
    oy = top - 13  # 斗笠图上第 5 行是尖，落在头顶上方约 8 像素
    # alpha_composite 不能贴负坐标：负的部分改为从源图里裁掉（那几行本就是空的）
    frame.alpha_composite(douli, (4, max(oy, 0)), (0, max(-oy, 0)))
    hat.alpha_composite(frame, (0, 56 * f))
hat.save(ITEMS / "Armor" / "SwordImmortalHat_Head.png")

# 清流剑符：PixelLab 生成，原尺寸
Image.open(ART / "清流剑符.png").convert("RGBA").save(ITEMS / "LingeringStreamTalisman.png")

# ── 模组图标（80×80）和创意工坊封面（512×512）：取自法相图的脸和剑 ──
src = Image.open(ROOT / "design_image" / "Stoic Swordsman Amidst Pixel Clouds.png").convert("RGB")
icon = src.crop((740, 150, 1080, 490)).resize((80, 80), Image.Resampling.LANCZOS)
icon.save(MOD / "icon.png")
cover = src.crop((476, 0, 1417, 941)).resize((512, 512), Image.Resampling.LANCZOS)
cover.save(MOD / "icon_workshop.png")
print("boss items, hat, talisman, icons written")
