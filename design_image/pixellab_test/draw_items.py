"""Hand-placed item icons: Cloudbrew Gourd, Inkwake Tassel, Litany of Swordflight.

usage: python draw_items.py <out_dir>
Sizes are in art pixels; the game shows them at 2x like vanilla items.
"""
import sys
from pathlib import Path
from PIL import Image

PAL = {
    "K": (40, 28, 22),      # warm outline (gourd, bamboo)
    "N": (20, 26, 40),      # cool outline
    "G": (214, 168, 98), "H": (240, 208, 144), "g": (150, 102, 54),
    "w": (120, 84, 50),     # wooden stopper
    "R": (196, 58, 46), "r": (122, 34, 30),
    "C": (74, 167, 166), "c": (42, 111, 120), "L": (148, 218, 210),
    "J": (156, 222, 186), "j": (94, 168, 134), "I": (214, 248, 228),  # jade
    "B": (226, 204, 146), "b": (182, 154, 98), "d": (124, 98, 60),  # bamboo
    "S": (232, 242, 250), "s": (150, 170, 196), "M": (200, 248, 255),  # silver sword glyph, moon glow
}

GOURD = [
    "....KK......",
    "...KwwK.....",
    "...KwwK.....",
    "....KK......",
    "...KHGK.....",
    "..KHGGgK....",
    "..KGGGgK....",
    "...KGgK.....",
    "...KRRKR....",
    "..KHGGGgKR..",
    ".KHGGGGGgKr.",
    ".KHGGGGGgKR.",
    ".KGGGGGGgK..",
    ".KGGGGGggK..",
    ".KgGGGgggK..",
    "..KggggK....",
    "...KKKK.....",
]

TASSEL = [
    "...NN...",
    "..NC.N..",
    "...NCN..",
    "....C...",
    "...NRN..",
    "..NRrRN.",
    "...NRN..",
    "...NCN..",
    "..NIJJN.",
    ".NIJJjjN",
    ".NJJJjjN",
    "..NJjjN.",
    "...NCN..",
    "..NCCcN.",
    ".NCLCCcN",
    ".NCLCCcN",
    ".NCCLCcN",
    ".NcCLCcN",
    ".NcCCCcN",
    "..NcNcN.",
    "...N.N..",
]

LITANY = [
    "...KKKKKKKKKKKKKK..",
    "..KbKBbBbBbBbBbBbK.",
    ".KbBKBbBbBbBMbBbBK.",
    ".KBbKCCCCCCCCCCCCK.",
    ".KbBKBbBbBbBSbBbBK.",
    ".KBbKBbBbBbBSsBbBK.",
    ".KbBKBbBbBbBSsBbBK.",
    ".KBbKBbBbBbBSsBbBK.",
    ".KbBKBbBbBbBSsBbBK.",
    ".KBbKBbBbBsSSSsBbK.",
    ".KbBKBbBbBbBcbBbBK.",
    ".KBbKBbBbBbBcbBbBK.",
    ".KbBKCCCCCCCCCCCCK.",
    ".KBbKBbBbBbBMbBbBK.",
    "..KbKBbBbBbBbBbBbK.",
    "...KKKKKKKKKKKKKK..",
]


def render(rows):
    w = max(len(r) for r in rows)
    img = Image.new("RGBA", (w, len(rows)), (0, 0, 0, 0))
    for y, row in enumerate(rows):
        for x, ch in enumerate(row):
            if ch != ".":
                img.putpixel((x, y), PAL[ch] + (255,))
    return img


out = Path(sys.argv[1]); out.mkdir(parents=True, exist_ok=True)
for name, rows in (("一壶酒", GOURD), ("剑穗", TASSEL), ("御剑诀", LITANY)):
    render(rows).save(out / f"{name}.png")
