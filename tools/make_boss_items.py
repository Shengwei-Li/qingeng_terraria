"""Boss 标配物品的贴图：宝藏袋、纪念杯（墙挂 3×3）、遗物（落地 3×3）、面具（头部 20 帧），以及模组图标和创意工坊封面。

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
x2(bag).save(ITEMS / "SwordImmortalBag.png")

# 纪念杯：48×48 的画框，1:1 铺成 3×3 墙挂物块；物品图标就是这幅画
trophy = Image.open(ART / "纪念杯.png").convert("RGBA").resize((48, 48), Image.Resampling.NEAREST)
trophy.save(ITEMS / "SwordImmortalTrophy.png")
tile_sheet(trophy, 3, 3).save(TILES / "SwordImmortalTrophyTile.png")

# 遗物：落地 3×3
relic = Image.open(ART / "遗物.png").convert("RGBA")
relic.thumbnail((48, 48), Image.Resampling.NEAREST)
relic.save(ITEMS / "SwordImmortalRelic.png")
tile_sheet(relic, 3, 3).save(TILES / "SwordImmortalRelicTile.png")

# ── 面具：剑仙的发型（黑发高髻、青发带、脑后长发），20 帧，人物朝右 ──
# 头部位置量自 ExampleMod 的兜帽模板：普通帧头在 x 8-30、y 10-30；第 7-9、14-16 帧上移 2 像素
hood = Image.open(ROOT / "tools" / "armor_templates" / "ExampleHood_Head.png").convert("RGBA")
HAIR, HAIR_HI, OUT = (30, 32, 40), (64, 70, 86), (14, 14, 18)
TEAL, TEAL_D = (108, 171, 152), (44, 102, 97)
BOB = {7, 8, 9, 14, 15, 16}
mask = Image.new("RGBA", (40, 56 * 20), (0, 0, 0, 0))
for f in range(20):
    frame = Image.new("RGBA", (40, 56), (0, 0, 0, 0))
    px = frame.load()
    hp = hood.crop((0, 56 * f, 40, 56 * f + 56)).load()
    top = 10 - (2 if f in BOB else 0)
    # 头发：兜帽轮廓里，头顶（前几行）和脑后（偏左的部分）涂成黑发，脸（右下）留空
    for y in range(56):
        for x in range(40):
            if hp[x, y][3] == 0:
                continue
            on_top = y < top + 5
            on_back = x < 22
            if on_top or on_back:
                px[x, y] = (HAIR_HI if (x + y) % 7 == 0 else HAIR) + (255,)
    # 脑后长发垂到肩下，末梢随走路轻摆
    sway = round(math.sin(f * 1.1)) if f >= 6 else 0
    for i in range(14):
        y = top + 16 + i
        x0 = 8 - (i // 5) + (sway if i > 8 else 0)
        for x in range(x0, x0 + 5):
            if 0 <= x < 40 and 0 <= y < 56:
                px[x, y] = HAIR + (255,)
    # 高髻和青发带
    cx = 19
    for y in range(top - 6, top):
        w = 3 if y < top - 4 else 4
        for x in range(cx - w + 1, cx + w):
            px[x, y] = HAIR + (255,)
    for x in range(cx - 4, cx + 4):
        px[x, top - 1] = TEAL + (255,)
    for i in range(8):  # 发带垂在脑后
        x, y = cx - 4 - i // 2, top - 1 + i
        px[x, y] = (TEAL if i % 3 else TEAL_D) + (255,)
    # 外轮廓描边
    src = frame.copy(); sp = src.load()
    for y in range(56):
        for x in range(40):
            if sp[x, y][3] == 0 and any(0 <= x + dx < 40 and 0 <= y + dy < 56 and sp[x + dx, y + dy][3]
                                        for dx, dy in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                if sp[x - 1 if x > 0 else x, y][:3] != (0, 0, 0) or True:
                    px[x, y] = OUT + (255,)
    mask.alpha_composite(frame, (0, 56 * f))
mask.save(ITEMS / "Armor" / "SwordImmortalMask_Head.png")
# 面具图标：用 Boss 头像
head = Image.open(MOD / "Content" / "NPCs" / "SwordImmortal" / "SwordImmortal_Head_Boss.png").convert("RGBA")
head.save(ITEMS / "Armor" / "SwordImmortalMask.png")

# ── 模组图标（80×80）和创意工坊封面（512×512）：取自法相图的脸和剑 ──
src = Image.open(ROOT / "design_image" / "Stoic Swordsman Amidst Pixel Clouds.png").convert("RGB")
icon = src.crop((740, 150, 1080, 490)).resize((80, 80), Image.Resampling.LANCZOS)
icon.save(MOD / "icon.png")
cover = src.crop((476, 0, 1417, 941)).resize((512, 512), Image.Resampling.LANCZOS)
cover.save(MOD / "icon_workshop.png")
print("boss items, mask, icons written")
