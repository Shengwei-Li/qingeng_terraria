"""Generate 64x64 side-view idle variants of the boss with a palette taken from the concept art.

usage: python gen_idle64.py <out_dir> <strength> <seed> [<seed> ...]
Each seed costs one PixelLab generation.
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import plab
from imgutil import crop_figure, strip_bg

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
strength = int(sys.argv[2]); seeds = [int(s) for s in sys.argv[3:]]
SRC = r"D:\terraria_mod\sword_master\design_image\person.png"
N = 64

fig = strip_bg(crop_figure(SRC, 800, 1448))
h = N - 4
w = round(fig.width * h / fig.height)
init = Image.new("RGBA", (N, N), (0, 0, 0, 0))
init.alpha_composite(fig.resize((w, h), Image.Resampling.LANCZOS), ((N - w) // 2, N - h - 2))

# Forced palette, hand-picked so small accents (skin, gourd, red tassel) survive.
# A palette quantized from the concept loses them to the large white/teal areas.
cols = [
    (22, 28, 38), (38, 42, 53), (70, 80, 100),                     # outline, hair
    (242, 212, 184), (215, 166, 138), (168, 118, 96),              # skin
    (250, 252, 250), (226, 236, 234), (190, 210, 210), (140, 170, 174),  # white robe ramp
    (148, 218, 210), (74, 167, 166), (42, 111, 120), (24, 70, 80), # teal ramp
    (31, 47, 56), (58, 63, 75),                                    # belt, boots
    (212, 170, 100), (176, 124, 66), (112, 74, 40),                # gourd ramp
    (190, 64, 52), (111, 36, 32),                                  # red tassel
    (212, 192, 138), (190, 198, 206), (120, 130, 140),             # brass, steel
]
palette = Image.new("RGB", (len(cols), 1))
palette.putdata(cols)

DESC = ("pixel art sprite of a chinese xianxia sword immortal, handsome young man, full body side view facing right, "
        "long black hair in a high bun with a flowing teal ribbon, layered white hanfu robe with teal inner robe and teal collar, "
        "wide flowing sleeves, sword sheathed diagonally on his back with a teal tassel, round brown wine gourd hanging at his belt, "
        "black boots, calm elegant standing pose, crisp clean pixels, readable silhouette")
for seed in seeds:
    r = plab.request("POST", "/create-image-bitforge", {
        "description": DESC,
        "negative_description": "blurry, noisy, messy, front view, extra limbs, text, background",
        "image_size": {"width": N, "height": N},
        "text_guidance_scale": 8,
        "outline": "single color black outline",
        "shading": "medium shading",
        "detail": "highly detailed",
        "view": "side",
        "direction": "east",
        "no_background": True,
        "init_image": plab.b64img(init),
        "init_image_strength": strength,
        "color_image": plab.b64img(palette),
        "seed": seed,
    })
    img = plab.decode(r["image"])
    img.save(out / f"idle64_st{strength}_s{seed}.png")
    print("ok", strength, seed, r.get("usage"))
