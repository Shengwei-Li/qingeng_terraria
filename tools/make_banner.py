"""创意工坊封面（512×512，QingyiJianxian/icon_workshop.png）和宣传横幅（1920×600，design_image/banner.png）。

构图：深色水墨底（在 Steam 一排缩略图里醒目）→ 法相原画（去白底，白袍和剑在深底上跳出来）→
游戏里的像素剑仙站在像素祥云上（一眼看出是泰拉模组，也对应三阶段"法相在天、剑仙在前"）→ 行楷标题和朱印。
不用渐变光晕、不写口号，只有名字。
usage: python make_banner.py
"""
from pathlib import Path

import numpy as np
from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parent.parent
MOD = ROOT / "QingyiJianxian"
SRC = ROOT / "design_image" / "Stoic Swordsman Amidst Pixel Clouds.png"
FONT_TITLE = "C:/Windows/Fonts/STXINGKA.TTF"   # 华文行楷
FONT_SEAL = "C:/Windows/Fonts/STLITI.TTF"      # 华文隶书
FONT_EN = "C:/Windows/Fonts/georgiab.ttf"

INK = (14, 18, 22)
JADE = (236, 250, 238)
SEAL = (178, 46, 38)
rng = np.random.default_rng(3)


def ink_ground(w, h):
    """深色水墨底：上浅下深的墨青，叠几层大小不一的墨云（模糊噪声），不是均匀渐变。"""
    y = np.linspace(0, 1, h)[:, None]
    base = np.zeros((h, w, 3))
    top, bottom = np.array([26, 38, 42]), np.array([9, 13, 16])
    base[:] = top * (1 - y[..., None]) + bottom * y[..., None]
    for scale, amp in ((w // 3, 18), (w // 8, 10), (w // 20, 5)):
        n = rng.random((max(2, h // scale + 2), max(2, w // scale + 2)))
        n = np.asarray(Image.fromarray((n * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)) / 255.0
        base += (n[..., None] - 0.5) * amp * np.array([0.8, 1.1, 1.1])
    return Image.fromarray(np.clip(base, 0, 255).astype(np.uint8), "RGB").convert("RGBA")


def painting():
    """法相原画，白底去掉（和 make_colossus.py 同一种去法，但不预乘，方便在 PIL 里合成）。"""
    a = np.asarray(Image.open(SRC).convert("RGB")).astype(float)
    alpha = np.clip((248 - a.min(axis=2)) / 16, 0, 1)
    out = np.dstack([a, alpha * 255]).astype(np.uint8)
    return Image.fromarray(out, "RGBA")


def fade_edges(img, left=0, right=0, top=0, bottom=0):
    """边缘化进墨里：用带噪声的渐隐，边是毛的，不是一刀切的直线。"""
    w, h = img.size
    xs, ys = np.meshgrid(np.arange(w), np.arange(h))
    m = np.ones((h, w))
    noise = np.asarray(Image.fromarray((rng.random((h // 8 + 1, w // 8 + 1)) * 255).astype(np.uint8)).resize((w, h), Image.BICUBIC)) / 255.0
    for d, size in ((xs, left), (w - 1 - xs, right), (ys, top), (h - 1 - ys, bottom)):
        if size > 0:
            t = np.clip(d / size + (noise - 0.5) * 0.5, 0, 1)
            m *= t * t * (3 - 2 * t)
    arr = np.asarray(img).copy()
    arr[..., 3] = (arr[..., 3] * m).astype(np.uint8)
    return Image.fromarray(arr, "RGBA")


def pixel(img, k):
    return img.resize((img.width * k, img.height * k), Image.NEAREST)


def tint(img, color, alpha):
    arr = np.asarray(img).astype(float)
    arr[..., :3] = arr[..., :3] * 0.35 + np.array(color) * 0.65
    arr[..., 3] *= alpha
    return Image.fromarray(arr.astype(np.uint8), "RGBA")


def boss_sprite():
    sheet = Image.open(MOD / "Content" / "NPCs" / "SwordImmortal" / "SwordImmortal.png").convert("RGBA")
    f = sheet.crop((0, 0, 128, 128))
    return f.crop(f.getbbox())


def clouds():
    return [Image.open(MOD / "Assets" / "Sky" / f"Cloud{i}.png").convert("RGBA") for i in (0, 1)]


def unpremultiply(img):
    arr = np.asarray(img).astype(float)
    a = arr[..., 3:4] / 255.0
    arr[..., :3] = np.where(a > 0, arr[..., :3] / np.maximum(a, 1e-3), 0)
    return Image.fromarray(np.clip(arr, 0, 255).astype(np.uint8), "RGBA")


def text_layer(size, draw_fn):
    layer = Image.new("RGBA", size, (0, 0, 0, 0))
    draw_fn(ImageDraw.Draw(layer))
    return layer


def title_with_shadow(canvas, chars, font, pos, vertical, gap):
    """标题：白玉色行楷，后面压一道墨影（偏移 + 模糊），让字从画上浮出来。"""
    def draw(d, color, off):
        x, y = pos
        for ch in chars:
            d.text((x + off, y + off), ch, font=font, fill=color)
            if vertical:
                y += font.size + gap
            else:
                x += font.size + gap
    shadow = text_layer(canvas.size, lambda d: draw(d, INK + (230,), 5)).filter(ImageFilter.GaussianBlur(6))
    canvas.alpha_composite(shadow)
    canvas.alpha_composite(text_layer(canvas.size, lambda d: draw(d, JADE + (255,), 0)))


def seal(size, chars):
    """朱印：红底白字（阴刻），边缘略毛。"""
    s = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    d = ImageDraw.Draw(s)
    d.rounded_rectangle((0, 0, size - 1, size - 1), radius=size // 10, fill=SEAL + (255,))
    font = ImageFont.truetype(FONT_SEAL, int(size * 0.42))
    for i, ch in enumerate(chars):
        bbox = d.textbbox((0, 0), ch, font=font)
        cw, chh = bbox[2] - bbox[0], bbox[3] - bbox[1]
        d.text(((size - cw) / 2 - bbox[0], size * (0.06 + 0.46 * i) - bbox[1] + (size * 0.44 - chh) / 2), ch, font=font, fill=(250, 238, 226, 255))
    # 印泥不匀：随机挖掉几点
    arr = np.asarray(s).copy()
    holes = rng.random(arr.shape[:2]) < 0.035
    arr[..., 3] = np.where(holes & (arr[..., 3] > 0), 120, arr[..., 3])
    return Image.fromarray(arr, "RGBA")


def spaced(d, xy, text, font, fill, spacing):
    x, y = xy
    for ch in text:
        d.text((x, y), ch, font=font, fill=fill)
        x += d.textlength(ch, font=font) + spacing


def cover():
    W = H = 512
    c = ink_ground(W, H)
    # 法相：脸和剑占右侧大半
    p = painting().crop((540, 20, 1260, 740)).resize((560, 560), Image.LANCZOS)
    p = fade_edges(p, left=170, bottom=200, right=30)
    c.alpha_composite(p, (W - 560 + 40, -10))
    # 像素祥云和剑仙：左下，在法相前面
    cl = clouds()
    c.alpha_composite(tint(unpremultiply(cl[1]), (205, 218, 218), 0.7), (300, 430))
    c.alpha_composite(tint(unpremultiply(cl[0]), (190, 205, 206), 0.45), (-90, 455))
    # 剑仙站在右下、白袍前面，不挡脸和眼睛
    b = pixel(boss_sprite(), 2)
    c.alpha_composite(b, (W - b.width - 22, H - b.height - 20))
    # 标题：竖排行楷，左上
    title_with_shadow(c, "青衣剑仙", ImageFont.truetype(FONT_TITLE, 92), (28, 18), vertical=True, gap=-6)
    c.alpha_composite(seal(46, "问剑"), (122, 300))
    d = ImageDraw.Draw(c)
    spaced(d, (24, H - 28), "THE AZURE-CLAD SWORD IMMORTAL", ImageFont.truetype(FONT_EN, 13), JADE + (220,), 1.4)
    return c.convert("RGB")


def banner():
    W, H = 1920, 600
    c = ink_ground(W, H)
    p = painting()
    scale = 760 / 941 * 1.05
    p = p.resize((int(p.width * scale), int(p.height * scale)), Image.LANCZOS)
    p = fade_edges(p, left=380, right=160, bottom=220)
    c.alpha_composite(p, (W - p.width + 60, -40))
    cl = clouds()
    # 像素祥云按原图的像素大小，不放大（放大成大方块就不像一幅画了）
    for (i, x, y, a) in ((0, 40, 460, 0.55), (1, 640, 470, 0.5), (0, 1040, 490, 0.35)):
        c.alpha_composite(tint(unpremultiply(cl[i]), (200, 214, 214), a), (x, y))
    # 剑仙站在标题和法相之间，脚下一朵云
    b = pixel(boss_sprite(), 3)
    c.alpha_composite(b, (930, H - b.height - 36))
    title_with_shadow(c, "青衣剑仙", ImageFont.truetype(FONT_TITLE, 170), (90, 110), vertical=False, gap=-14)
    c.alpha_composite(seal(84, "问剑"), (724, 130))
    d = ImageDraw.Draw(c)
    spaced(d, (98, 330), "THE AZURE-CLAD SWORD IMMORTAL", ImageFont.truetype(FONT_EN, 30), JADE + (230,), 5)
    spaced(d, (100, 380), "A tModLoader boss mod", ImageFont.truetype("C:/Windows/Fonts/georgiai.ttf", 22), (170, 200, 196, 220), 1.5)
    return c.convert("RGB")


cover().save(MOD / "icon_workshop.png")
banner().save(ROOT / "design_image" / "banner.png")
print("cover and banner written")
