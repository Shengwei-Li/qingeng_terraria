"""Bitforge generation guided by the concept art. usage: python test_bitforge.py <out_dir> <canvas> [seed]"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import plab
from imgutil import crop_figure, strip_bg

out = Path(sys.argv[1]); N = int(sys.argv[2]); seed = int(sys.argv[3]) if len(sys.argv) > 3 else 11
SRC = r"D:\terraria_mod\sword_master\design_image\person.png"

fig = strip_bg(crop_figure(SRC, 800, 1448))
h = N - 2
w = round(fig.width * h / fig.height)
init = Image.new("RGBA", (N, N), (0, 0, 0, 0))
init.alpha_composite(fig.resize((w, h), Image.Resampling.LANCZOS), ((N - w) // 2, N - h - 1))

DESC = ("pixel art game sprite of a chinese xianxia sword immortal, young man, side view facing right, "
        "long black hair in a high bun tied with a teal ribbon, white hanfu robe with teal inner layer and teal sleeves trim, "
        "sword sheathed on his back with a teal tassel, brown wine gourd with red tassel hanging at the belt, black boots, "
        "elegant and handsome, clean readable silhouette, terraria style")
r = plab.request("POST", "/create-image-bitforge", {
    "description": DESC,
    "negative_description": "blurry, noisy, messy, extra limbs, text, background",
    "image_size": {"width": N, "height": N},
    "text_guidance_scale": 8,
    "outline": "single color black outline",
    "shading": "medium shading",
    "detail": "highly detailed",
    "view": "side",
    "direction": "east",
    "no_background": True,
    "init_image": plab.b64img(init),
    "init_image_strength": 300,
    "seed": seed,
})
img = plab.decode(r["image"])
img.save(out / f"bitforge_{N}_s{seed}.png")
print("bitforge", N, img.size, r.get("usage"))
