"""Side-by-side nearest-neighbour preview. usage: python sheet.py <out.png> <scale> <img1> [img2 ...]"""
import sys
from PIL import Image

out, scale, paths = sys.argv[1], int(sys.argv[2]), sys.argv[3:]
imgs = [Image.open(p).convert("RGBA") for p in paths]
imgs = [i.resize((i.width * scale, i.height * scale), Image.Resampling.NEAREST) for i in imgs]
pad = 20
W = sum(i.width for i in imgs) + pad * (len(imgs) + 1)
H = max(i.height for i in imgs) + pad * 2
sheet = Image.new("RGBA", (W, H), (120, 170, 220, 255))
x = pad
for i in imgs:
    sheet.alpha_composite(i, (x, H - pad - i.height)); x += i.width + pad
sheet.save(out)
