"""Generate the ink-wash giant sword for the Final Stroke with PixelLab pixflux.

usage: python gen_bigsword.py <out_dir> <seed> [<seed> ...]   (one generation per seed)
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import plab

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
DESC = ("giant chinese straight sword (jian) painted in traditional ink wash style, vertical, tip pointing straight down, "
        "hilt at the top with a simple guard and a long flowing ink tassel, blade made of bold black sumi ink brush strokes "
        "with grey wash gradients, dry-brush texture, ink splashes and drips along the edges, a thin pale teal glint on the edge, "
        "elegant, monochrome ink, single object centered, empty background")
for seed in map(int, sys.argv[2:]):
    r = plab.request("POST", "/create-image-pixflux", {
        "description": DESC,
        "image_size": {"width": 128, "height": 384},
        "text_guidance_scale": 8,
        "no_background": True,
        "background_removal_task": "remove_complex_background",
        "seed": seed,
    })
    plab.decode(r["image"]).save(out / f"bigsword_s{seed}.png")
    print("ok", seed, r.get("usage"))
