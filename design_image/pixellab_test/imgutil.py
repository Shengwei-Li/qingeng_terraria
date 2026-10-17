from collections import deque

import numpy as np
from PIL import Image


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
