"""Generate item icons styled after our hand-drawn Clearmoon sword.

usage: python gen_items.py <out_dir> <item_key> <seed> [<seed> ...]
Each seed costs one PixelLab generation.
"""
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import plab

ITEMS = {
    "gourd": "pixel art item icon, a round calabash wine gourd, two bulbs, warm tan and brown, "
             "tied at the waist with a red cord and a small red tassel, wooden stopper, single object centered",
    "tassel": "pixel art item icon, a chinese sword tassel ornament, a carved pale jade bead on a teal silk cord, "
              "long flowing teal silk fringe, a small red knot above the bead, single object centered",
    "litany": "pixel art item icon, an ancient chinese bamboo slip scroll half unrolled, pale bamboo strips bound by teal cord, "
              "a small glowing silver sword emblem on it, faint cyan magical glow, single object centered",
}

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
key = sys.argv[2]; seeds = [int(s) for s in sys.argv[3:]]
style = Image.open(Path(__file__).parent.parent / "定稿" / "清月剑_32.png").convert("RGBA")

for seed in seeds:
    r = plab.request("POST", "/create-image-bitforge", {
        "description": ITEMS[key],
        "negative_description": "blurry, noisy, text, background, multiple objects",
        "image_size": {"width": 32, "height": 32},
        "text_guidance_scale": 8,
        "outline": "single color black outline",
        "shading": "medium shading",
        "detail": "medium detail",
        "no_background": True,
        "style_image": plab.b64img(style),
        "style_strength": 35,
        "seed": seed,
    })
    plab.decode(r["image"]).save(out / f"{key}_s{seed}.png")
    print("ok", key, seed, r.get("usage"))
