"""开发自检：找出模组里缺失的贴图，免得游戏加载时报 MissingResourceException。

检查：
  - 每个 ModItem / ModNPC / ModProjectile / ModBuff / ModMount 类，没有重写 Texture 的，同目录要有 <类名>.png
  - [AutoloadEquip(EquipType.Head/Body/Legs)] 要有 <类名>_Head/_Body/_Legs.png
  - [AutoloadBossHead] 要有 <类名>_Head_Boss.png
  - ModTile 要有 <类名>.png；模组根目录要有 icon.png
  - ModMount 要有 <类名>_Back.png（或 _Front.png）
  - 代码里所有 "QingyiJianxian/..." 路径的贴图要存在
usage: python check_textures.py   （有缺失时退出码为 1）
"""
import re
import sys
from pathlib import Path

MOD = Path(__file__).resolve().parent.parent / "QingyiJianxian"
problems = []

for cs in MOD.rglob("*.cs"):
    if "obj" in cs.parts or "bin" in cs.parts:
        continue
    src = cs.read_text(encoding="utf-8")
    for m in re.finditer(r"((?:\[[^\]]*\]\s*)*)public\s+class\s+(\w+)\s*:\s*(ModItem|ModNPC|ModProjectile|ModBuff|ModMount|ModTile)\b", src):
        attrs, name, kind = m.group(1), m.group(2), m.group(3)
        folder = cs.parent
        overrides_texture = re.search(r"override\s+string\s+Texture\s*=>", src[m.end():m.end() + 4000]) is not None
        if kind == "ModTile":
            if not (folder / f"{name}.png").exists():
                problems.append(f"{cs.relative_to(MOD)}: {name} 缺 {name}.png")
        elif kind == "ModMount":
            if not ((folder / f"{name}_Back.png").exists() or (folder / f"{name}_Front.png").exists()):
                problems.append(f"{cs.relative_to(MOD)}: {name} 缺 {name}_Back.png / _Front.png")
        elif kind != "ModTile" and not overrides_texture and not (folder / f"{name}.png").exists():
            problems.append(f"{cs.relative_to(MOD)}: {name} 缺 {name}.png")
        for slot in re.findall(r"EquipType\.(Head|Body|Legs)", attrs):
            if not (folder / f"{name}_{slot}.png").exists():
                problems.append(f"{cs.relative_to(MOD)}: {name} 缺 {name}_{slot}.png")
        if "AutoloadBossHead" in attrs and not (folder / f"{name}_Head_Boss.png").exists():
            problems.append(f"{cs.relative_to(MOD)}: {name} 缺 {name}_Head_Boss.png")

    for path in re.findall(r'"QingyiJianxian/([^"]+)"', src):
        # 只看真正的贴图路径：Assets/ 或 Content/ 开头、不是拼接用的前缀（以 / 结尾）
        if "{" in path or path.endswith("/") or not path.startswith(("Assets/", "Content/")):
            continue
        if not (MOD / (path + ".png")).exists():
            problems.append(f"{cs.relative_to(MOD)}: 引用的贴图不存在 {path}.png")

if not (MOD / "icon.png").exists():
    problems.append("缺模组图标 icon.png")

if problems:
    print("缺失的贴图：")
    for p in sorted(set(problems)):
        print("  -", p)
    sys.exit(1)
print("贴图齐全")
