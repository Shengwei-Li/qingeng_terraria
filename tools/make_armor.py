"""Repaint the ExampleMod armor templates (tools/armor_templates) into the Azure Wayfarer set.

Keeps every frame's silhouette and layout; only swaps colors:
  robe  -> white hanfu with teal trim (gold trim of the template becomes teal), teal band on the hem
  boots -> black cloth boots with teal wraps
usage: python make_armor.py
"""
from pathlib import Path

from PIL import Image

ROOT = Path(__file__).resolve().parent
TPL = ROOT / "armor_templates"
OUT = ROOT.parent / "QingyiJianxian" / "Content" / "Items" / "Armor"
OUT.mkdir(parents=True, exist_ok=True)

# 配色取自玩家选定的剑袍图标（design_image/定稿/青衫剑袍.png）：米白衣身、松石青衣缘和腰带
OUTLINE = (19, 17, 16)
ROBE = {
    (50, 50, 50): OUTLINE,
    (52, 52, 35): (39, 95, 90),       # 模板里金边的描边 → 深青
    (110, 96, 79): (162, 148, 133),   # 布料暗部
    (149, 134, 116): (190, 184, 165), # 布料中间调
    (179, 160, 138): (221, 217, 201), # 布料亮部
    (197, 183, 166): (254, 254, 238), # 高光
    (184, 151, 52): (44, 102, 97),    # 金边 → 青边
    (201, 191, 80): (108, 171, 152),
    (215, 208, 125): (150, 204, 186),
}
BOOTS = {
    (68, 22, 22): (22, 24, 30),
    (63, 20, 20): (22, 24, 30),
    (100, 33, 33): (31, 36, 46),
    (147, 49, 49): (46, 52, 64),
    (198, 85, 85): (70, 78, 94),
    (199, 87, 87): (70, 78, 94),
    (236, 197, 197): (74, 167, 166),  # 高光处做成青色绑带
}
TEAL_HEM = [(44, 102, 97), (108, 171, 152)]


def recolor(src, mapping):
    img = Image.open(src).convert("RGBA")
    px = img.load()
    missing = set()
    for y in range(img.height):
        for x in range(img.width):
            r, g, b, a = px[x, y]
            if a == 0:
                continue
            if (r, g, b) in mapping:
                px[x, y] = mapping[(r, g, b)] + (a,)
            else:
                missing.add((r, g, b))
    if missing:
        print("unmapped colors in", src.name, sorted(missing))
    return img


def hem(img, frame_h=56):
    """长袍下摆：每一帧最下面两行布料染成青色缘边。"""
    px = img.load()
    for f in range(img.height // frame_h):
        top = f * frame_h
        for x in range(img.width):
            cloth = [y for y in range(top, top + frame_h) if px[x, y][3] and px[x, y][:3] != OUTLINE]
            if cloth:
                bottom = max(cloth)
                for i, y in enumerate((bottom, bottom - 1)):
                    if y in cloth:
                        px[x, y] = TEAL_HEM[1 - i] + (255,) if i == 0 else TEAL_HEM[1] + (255,)
    return img


recolor(TPL / "ExampleRobe_Body.png", ROBE).save(OUT / "AzureWayfarerRobe_Body.png")
hem(recolor(TPL / "ExampleRobe_Legs.png", ROBE)).save(OUT / "AzureWayfarerRobe_Legs.png")
recolor(TPL / "ExampleLeggings_Legs.png", BOOTS).save(OUT / "AzureWayfarerBoots_Legs.png")
print("armor sheets written to", OUT)
