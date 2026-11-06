"""斗笠（青衣剑仙的斗笠）的像素画法：斜戴的竹编斗笠，青带、带尾飘在脑后，帽檐垂一圈淡青纱。

不是对称的几何图形：
- 帽身是略内凹的锥面，帽檐往外翻，整顶向前（人物朝右）压低约 12°；
- 光从左上来，四档明暗，竹篾的放射纹和一圈圈编纹都按明暗走，背光面才深；
- 青带在帽身中段，带尾两缕顺着重力和走路的摆动飘在脑后；
- 纱挂在帽檐下沿，按重力垂直下落（不跟着帽子斜），褶子有疏有密，下摆起伏，前长后短，只露一点下巴。

draw(...) 返回一张 RGBA 图；make_boss_items.py 用它拼出头部 20 帧和物品图标。
"""
import math

from PIL import Image

OUT = (46, 32, 22)
STRAW = [(240, 220, 166), (214, 182, 118), (176, 136, 80), (126, 90, 52)]  # 亮 → 暗
UNDER = (96, 66, 40)
UNDER_RIM = (150, 112, 66)
TEAL, TEAL_D, TEAL_HI = (96, 176, 164), (50, 116, 110), (160, 222, 208)
VEIL, VEIL_HI = (206, 236, 232), (236, 252, 248)


def draw(size, origin, scale=1.0, tilt=0.21, sway=0.0, veil_front=13, veil_back=9):
    """在 size 大小的画布上画斗笠。origin = 帽檐中心（屏幕坐标），sway = 走路摆动（-1..1）。"""
    W, Hc = 15.5 * scale, 9.0 * scale          # 帽檐半宽、帽身高
    rim = 1.3 * scale                          # 帽檐厚
    ox, oy = origin
    ct, st = math.cos(tilt), math.sin(tilt)
    w, h = size

    hat = Image.new("RGBA", size, (0, 0, 0, 0))
    hp = hat.load()
    solid = [[False] * h for _ in range(w)]
    brim_bottom = {}

    for py in range(h):
        for px in range(w):
            # 像素中心转回帽子自己的坐标（未倾斜时：x 向前，y 向下，帽檐在 y=0，尖在 y=-Hc）
            dx, dy = px + 0.5 - ox, py + 0.5 - oy
            x = dx * ct + dy * st
            y = -dx * st + dy * ct
            color = None
            if -Hc <= y <= 0:
                k = (y + Hc) / Hc                       # 0 = 尖，1 = 帽檐
                half = W * k ** 1.2 + 0.35            # 内凹的锥面，帽檐外翻
                if abs(x) <= half:
                    t = x / half                        # -1 后 .. 1 前
                    # 光从左上：后侧、靠尖处亮，前侧、靠帽檐处暗
                    light = 0.62 - 0.42 * t - 0.22 * k + 0.18 * (1 - k)
                    # 竹篾放射纹：沿同一 t 的线，按明暗决定压一档还是不压
                    spoke = ((t + 1) * 6.0) % 1.0
                    if spoke < 0.16 and k > 0.25:
                        light -= 0.22
                    # 一圈圈编纹：只在帽身中下段，淡淡的
                    ring = (y * 1.0 / scale) % 2.6
                    if ring < 0.45 and 0.35 < k < 0.9 and t > -0.2:
                        light -= 0.08
                    # 帽尖一点高光
                    if k < 0.2 and t < 0.3:
                        light += 0.2
                    # 青带
                    if 0.42 <= k <= 0.58:
                        color = TEAL_HI if (t < -0.35 and k < 0.5) else (TEAL if t < 0.45 else TEAL_D)
                    else:
                        idx = 0 if light > 0.72 else 1 if light > 0.45 else 2 if light > 0.2 else 3
                        color = STRAW[idx]
            elif 0 < y <= rim:
                half = W + 0.35 - y * 0.6
                if abs(x) <= half:
                    # 帽檐下沿：背光，前缘被光扫到一点
                    color = UNDER_RIM if (x > W * 0.55 or y < rim * 0.4) else UNDER
            # 帽尖的小顶
            if color is None and math.hypot(x, y + Hc + 0.6 * scale) <= 1.0 * scale:
                color = STRAW[1]
            if color:
                hp[px, py] = color + (255,)
                solid[px][py] = True
                brim_bottom[px] = max(brim_bottom.get(px, -1), py)

    # 描边：外轮廓一圈深褐
    lined = hat.copy()
    lp = lined.load()
    for py in range(h):
        for px in range(w):
            if solid[px][py]:
                continue
            if any(0 <= px + ax < w and 0 <= py + ay < h and solid[px + ax][py + ay]
                   for ax, ay in ((1, 0), (-1, 0), (0, 1), (0, -1))):
                lp[px, py] = OUT + (255,)

    # 带尾：从青带后侧出发，两缕飘在脑后，随走路摆
    tail = Image.new("RGBA", size, (0, 0, 0, 0))
    tp = tail.load()
    # 带尾从帽檐后端垂下（帽带系在檐下，带尾露在脑后）
    lx, ly = -W + 1.0, rim
    bx = ox + lx * ct - ly * st
    by = oy + lx * st + ly * ct
    for strand, (spread, length) in enumerate(((0.0, 12), (1.0, 9))):
        for i in range(int(length * scale)):
            u = i / (length * scale)
            x = bx - i * (0.35 - 0.2 * spread) - sway * 1.5 * u * u
            y = by + i * (0.9 + 0.15 * spread) + math.sin(u * 3.0 + strand) * 0.8
            xi, yi = int(round(x)), int(round(y))
            if 0 <= xi < w and 0 <= yi < h and tp[xi, yi][3] == 0:
                tp[xi, yi] = (TEAL if i % 3 else TEAL_D) + (255,)

    # 纱：从帽檐下沿垂下，前长后短，下摆起伏；越往下越透，走路时下段往后飘
    veil = Image.new("RGBA", size, (0, 0, 0, 0))
    vp = veil.load()
    cols = sorted(brim_bottom)
    if cols:
        left, right = cols[0] + 4, cols[-1] - 3  # 纱比帽檐窄一圈，不成方块
        for px in range(left, right + 1):
            if px not in brim_bottom:
                continue
            u = (px - left) / max(right - left, 1)          # 0 后 .. 1 前
            length = veil_back + (veil_front - veil_back) * u
            length += math.sin(px * 1.3) * 0.9 + math.sin(px * 0.45 + 1.0) * 0.8
            length *= scale
            if u < 0.08 or u > 0.94:
                length *= 0.8                                # 两侧收一点
            fold = 0.5 + 0.5 * math.sin(px * 1.9)            # 褶子：有疏有密
            for i in range(int(length)):
                y = brim_bottom[px] + 1 + i
                drift = int(round(-sway * 1.6 * (i / max(length, 1)) ** 2))
                x = px + drift
                if not (0 <= x < w and 0 <= y < h):
                    continue
                fade = 1 - 0.55 * i / max(length, 1)
                a = int((40 + 34 * fold + 36 * max(0.0, u - 0.45)) * fade)  # 轻纱：透得见底下的脸
                c = VEIL
                if i >= int(length) - 1:                     # 下摆一线亮
                    c, a = VEIL_HI, min(140, a + 55)
                elif fold > 0.85:
                    c = VEIL_HI
                if vp[x, y][3] < a:
                    vp[x, y] = c + (a,)

    out = Image.new("RGBA", size, (0, 0, 0, 0))
    out.alpha_composite(veil)
    out.alpha_composite(lined)
    out.alpha_composite(tail)  # 带尾压在帽子上，从青带后侧飘出去
    return out


def premultiply(img):
    """把半透明像素的颜色乘上透明度。游戏按预乘透明来画贴图；不预乘，淡淡的纱会画成一块实心的白。"""
    out = img.copy()
    px = out.load()
    for y in range(out.height):
        for x in range(out.width):
            r, g, b, a = px[x, y]
            if 0 < a < 255:
                px[x, y] = (r * a // 255, g * a // 255, b * a // 255, a)
    return out
