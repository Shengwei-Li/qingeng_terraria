"""Generate item icons and the mount cloud with PixelLab pixflux.

usage: python gen_icons.py <out_dir> <key> <seed> [<seed> ...]   (one generation per seed)
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import plab

STYLE = ("terraria item sprite, pixel art, clean dark outline, limited palette, chinese xianxia style, "
         "single object centered, transparent background")
ITEMS = {
    # key: (width, height, description)
    "litany": (32, 32, "an ancient chinese bamboo-slip scroll half unrolled, pale jade-green bamboo strips bound with a teal silk cord, "
                       "a small silver flying sword and a swirl of white cloud painted on it, faint cyan glow, " + STYLE),
    "tassel": (32, 32, "a chinese sword tassel ornament: a carved round pale jade bead with a small red knot above it, "
                       "a long flowing teal silk fringe hanging below, " + STYLE),
    "gourd": (32, 32, "a chinese calabash wine gourd, two rounded bulbs, warm tan and brown with highlight, wooden stopper, "
                      "tied at the waist with a red cord and a small red tassel, " + STYLE),
    "draught": (32, 32, "a small celadon porcelain chinese wine bottle (meiping), pale green-white glaze with a teal cloud pattern, "
                        "sealed with red paper and string at the neck, a red paper label, " + STYLE),
    "robe": (32, 32, "a neatly folded white chinese hanfu robe with teal collar and teal sleeve trim, a teal sash on top, "
                     "subtle cloud embroidery, " + STYLE),
    "boots": (32, 32, "a pair of black chinese cloth boots with teal wraps around the ankles and pale soles, " + STYLE),
    "silk": (32, 32, "a small bolt of shimmering translucent white silk cloth with faint teal cloud patterns, wisps of mist "
                     "drifting off it, glowing softly, magical fabric material, " + STYLE),
    "crown": (32, 32, "a small chinese jade hair crown (guan) for a topknot, pale jade green, a white jade hairpin through it, "
                      "two long flowing teal silk ribbons hanging from the back, " + STYLE),
    "crown2": (32, 32, "side view of a small chinese scholar topknot crown (guan) carved from green jade, sitting on nothing, "
                       "a long white jade hairpin piercing through it horizontally, two thin teal silk ribbons trailing down to one side, "
                       "elegant hair accessory, " + STYLE),
    "cloud": (64, 24, "a small fluffy white auspicious chinese cloud (xiangyun) seen from the side, soft teal shading underneath, "
                      "swirl curls at the ends, flat bottom you could stand on, terraria mount sprite, pixel art, transparent background"),
}

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
key = sys.argv[2]
w, h, desc = ITEMS[key]
for seed in map(int, sys.argv[3:]):
    r = plab.request("POST", "/create-image-pixflux", {
        "description": desc,
        "image_size": {"width": w, "height": h},
        "text_guidance_scale": 8,
        "no_background": True,
        "background_removal_task": "remove_complex_background",
        "seed": seed,
    })
    plab.decode(r["image"]).save(out / f"{key}_s{seed}.png")
    print("ok", key, seed, r.get("usage"))
