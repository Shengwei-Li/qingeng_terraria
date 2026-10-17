"""Try image-to-pixelart on the side-view concept. usage: python test_i2p.py <out_dir>"""
import sys
from collections import deque
from pathlib import Path

import numpy as np
from PIL import Image

sys.path.insert(0, str(Path(__file__).parent))
import plab

out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
SRC = r"D:\terraria_mod\sword_master\design_image\person.png"


def crop_figure(path, x0, x1):
    img = Image.open(path).convert("RGB").crop((x0, 0, x1, Image.open(path).height))
    a = np.asarray(img).astype(int)
    nw = (a.min(2) > 232) & ((a.max(2) - a.min(2)) < 18)
    ys, xs = np.where(~nw)
    pad = 12
    box = (max(xs.min() - pad, 0), max(ys.min() - pad, 0), min(xs.max() + pad, img.width), min(ys.max() + pad, img.height))
    return img.crop(box)


def strip_bg(img):
    """Flood-fill near-white from the borders to transparent."""
    a = np.asarray(img.convert("RGBA")).copy()
    h, w = a.shape[:2]
    rgb = a[..., :3].astype(int)
    nw = ((rgb.min(2) > 225) & ((rgb.max(2) - rgb.min(2)) < 22)) | (a[..., 3] == 0)
    seen = np.zeros((h, w), bool); q = deque()
    for y in range(h):
        for x in (0, w - 1):
            if nw[y, x]: seen[y, x] = True; q.append((y, x))
    for x in range(w):
        for y in (0, h - 1):
            if nw[y, x] and not seen[y, x]: seen[y, x] = True; q.append((y, x))
    while q:
        y, x = q.popleft()
        for dy, dx in ((1, 0), (-1, 0), (0, 1), (0, -1)):
            ny, nx = y + dy, x + dx
            if 0 <= ny < h and 0 <= nx < w and nw[ny, nx] and not seen[ny, nx]:
                seen[ny, nx] = True; q.append((ny, nx))
    a[seen, 3] = 0
    return Image.fromarray(a, "RGBA")


fig = crop_figure(SRC, 800, 1448)
OUT_H = int(sys.argv[2]) if len(sys.argv) > 2 else 46
out_w = round(fig.width * OUT_H / fig.height)
inp = fig.resize((out_w * 4, OUT_H * 4), Image.Resampling.LANCZOS)
inp.save(out / "input.png")

variants = {
    "reinterpret": dict(fixer=False, init_image_strength=0),
    "faithful": dict(fixer=True, init_image_strength=250),
}
for name, extra in variants.items():
    r = plab.request("POST", "/image-to-pixelart", {
        "image": plab.b64img(inp),
        "image_size": {"width": inp.width, "height": inp.height},
        "output_size": {"width": out_w, "height": OUT_H},
        "seed": 7,
        **extra,
    })
    img = strip_bg(plab.decode(r["image"]))
    img.save(out / f"{name}_{OUT_H}.png")
    print(name, img.size, "usage:", r.get("usage"))
