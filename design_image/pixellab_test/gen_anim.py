"""Animate the boss's idle frame with PixelLab animate-with-text-v3.

usage: python gen_anim.py <out_dir> <name> <frames> <seed> "<action text>" [loop]
"loop" passes the first frame as last_frame too, so the animation returns to its start.
Writes <name>_s<seed>_<i>.png per frame plus a horizontal strip. One call per run.
"""
import json
import sys
from pathlib import Path

from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import plab

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
name, frames, seed, action = sys.argv[2], int(sys.argv[3]), int(sys.argv[4]), sys.argv[5]
# 第 7 个参数之后可以给首帧图片路径；默认是剑仙站立待机图
src = next((a for a in sys.argv[6:] if a.endswith(".png")), None)
first = Image.open(src or Path(__file__).parent.parent / "定稿" / "剑仙_待机_64.png").convert("RGBA")

before = plab.balance()["subscription"]["generations"]
r = plab.request("POST", "/animate-with-text-v3", {
    "first_frame": plab.b64img(first),
    "action": action,
    "frame_count": frames,
    "seed": seed,
    "no_background": True,
    **({"last_frame": plab.b64img(first)} if "loop" in sys.argv[6:] else {}),
})
job = plab.wait_job(r["background_job_id"])
if job["status"] != "completed":
    print(json.dumps(job)[:1500]); sys.exit(1)

imgs = [plab.decode(i) for i in job["last_response"]["images"]]
strip = Image.new("RGBA", (sum(i.width for i in imgs), max(i.height for i in imgs)))
x = 0
for k, img in enumerate(imgs):
    img.save(out / f"{name}_s{seed}_{k}.png")
    strip.alpha_composite(img, (x, 0)); x += img.width
strip.save(out / f"{name}_s{seed}_strip.png")
after = plab.balance()["subscription"]["generations"]
print(name, len(imgs), "frames", imgs[0].size, "cost", before - after, "left", after)
