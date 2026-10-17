"""Generate the 'reclining on a cloud, drinking' sprite from the concept, matching the boss sprite's palette.

usage: python gen_lounge.py <out_dir> <strength> <seed> [<seed> ...]   (one generation per seed)
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import plab

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
strength = int(sys.argv[2]); seeds = [int(s) for s in sys.argv[3:]]
N = 72  # 比站姿画布宽一点，倚着的腿才放得下

# 与剑仙主立绘相同的手选调色板（见 gen_idle64.py）
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

src = Image.open(Path(__file__).parent / "lounge" / "src_full.png").convert("RGBA")
h = N - 4
w = round(src.width * h / src.height)
if w > N - 2:
    w = N - 2
    h = round(src.height * w / src.width)
init = Image.new("RGBA", (N, N), (0, 0, 0, 0))
init.alpha_composite(src.resize((w, h), Image.Resampling.LANCZOS), ((N - w) // 2, N - h - 2))
init.save(out / "init.png")

palette = Image.new("RGB", (len(cols), 1)); palette.putdata(cols)
DESC = ("pixel art sprite of a chinese xianxia sword immortal, handsome young man with long black hair in a high bun and teal ribbon, "
        "white hanfu robe with teal inner layer, reclining lazily on a fluffy white cloud, one knee raised, "
        "drinking from a brown wine gourd held to his lips, relaxed and lazy, no weapon in hand, crisp clean pixels")
for seed in seeds:
    r = plab.request("POST", "/create-image-bitforge", {
        "description": DESC,
        "negative_description": "blurry, noisy, messy, extra limbs, text, background, holding sword, standing",
        "image_size": {"width": N, "height": N},
        "text_guidance_scale": 8,
        "outline": "single color black outline",
        "shading": "medium shading",
        "detail": "highly detailed",
        "view": "side",
        "no_background": True,
        "init_image": plab.b64img(init),
        "init_image_strength": strength,
        "color_image": plab.b64img(palette),
        "seed": seed,
    })
    plab.decode(r["image"]).save(out / f"lounge_st{strength}_s{seed}.png")
    print("ok", strength, seed, r.get("usage"))
