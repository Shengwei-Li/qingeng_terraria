"""法天相地：生成天上那尊水墨法相（半身像，剑竖在脸前，只露一只眼）。

usage: python gen_colossus.py <out_dir> <seed> [<seed> ...]   (one generation per seed)
生成在白底上；游戏里按"越黑越不透明"转成墨色叠在空白天幕上，所以不需要透明背景。
"""
import sys
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
import plab

DESC = ("traditional chinese ink wash painting (shuimo), black ink on plain white paper, monochrome, "
        "colossal half-body portrait of a cold, aloof and strikingly handsome young chinese xianxia sword immortal, about twenty five, "
        "clean-shaven, no beard, refined sharp features, sword-like eyebrows, narrow piercing eyes with a cold calm stare, "
        "long black hair in a high topknot with long loose strands and a hair ribbon whipping in a strong wind, "
        "wide white robe sleeves billowing in the wind, holding a long straight jian sword vertically in front of his face, "
        "the blade splitting his face so one cold eye glares at the viewer, otherworldly and imposing, "
        "bold confident dry-brush strokes, ink splashes, clouds swirling around his chest, "
        "lots of empty white space, epic movie poster composition, no text, no seal, no calligraphy")

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
for seed in map(int, sys.argv[2:]):
    r = plab.request("POST", "/create-image-pixflux", {
        "description": DESC,
        "image_size": {"width": 320, "height": 400},
        "text_guidance_scale": 8,
        "no_background": False,
        "seed": seed,
    })
    plab.decode(r["image"]).save(out / f"colossus_s{seed}.png")
    print("ok", seed, r.get("usage"))
