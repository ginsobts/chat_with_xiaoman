#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
桌宠立绘图集打包 / 拆包工具。

把 Assets/StreamingAssets/images/ 下的多张单独差分 PNG，按统一网格拼成一张
pet_atlas.png，并生成 pet_atlas.json 清单（列/行/格子对应的差分名）。
游戏运行时 AssetCache 会优先读图集、按名字裁子矩形；换皮时只要替换 pet_atlas.png
（保持同样的网格布局）即可整套替换。

用法：
    python pack_pet_atlas.py pack      # 单文件 -> 图集(默认缩放到 768x512/格)
    python pack_pet_atlas.py pack --no-scale     # 不缩放，保持原始分辨率
    python pack_pet_atlas.py pack --cell 1024 683  # 自定义每格宽高
    python pack_pet_atlas.py unpack    # 图集 -> 拆回单文件(按格子分辨率)

依赖：Pillow  (pip install pillow)
"""
import argparse
import json
import sys
from pathlib import Path

try:
    from PIL import Image
except ImportError:
    print("需要 Pillow：pip install pillow")
    sys.exit(1)

ROOT = Path(__file__).resolve().parents[2]
IMAGES_DIR = ROOT / "Assets" / "StreamingAssets" / "images"
ATLAS_PNG = IMAGES_DIR / "pet_atlas.png"
ATLAS_JSON = IMAGES_DIR / "pet_atlas.json"

# 网格布局：按“从上到下、从左到右”的顺序对应差分名；"" 表示空格子。
COLS = 4
ROWS = 3
NAMES = [
    "pet",       "pet_blink", "pet_wink",    "pet_wave",
    "pet_happy", "pet_click", "pet_drag",    "pet_headpat",
    "pet_walk",  "pet_sleep", "",            "",
]

DEFAULT_CELL = (768, 512)  # 缩放后每格宽高（源图 1536x1024 的一半）


def _load_source(name):
    for ext in (".png", ".jpg", ".jpeg"):
        p = IMAGES_DIR / (name + ext)
        if p.exists():
            return Image.open(p).convert("RGBA")
    return None


def pack(cell, scale):
    # 先确定每格尺寸：缩放时用 cell；不缩放时用第一张源图的原始尺寸。
    sources = {}
    for name in NAMES:
        if not name:
            continue
        img = _load_source(name)
        if img is None:
            print(f"  ! 找不到 {name}.png，将留空该格")
        sources[name] = img

    if scale:
        cw, ch = cell
    else:
        first = next((im for im in sources.values() if im is not None), None)
        if first is None:
            print("没有任何源图，退出")
            sys.exit(1)
        cw, ch = first.size

    atlas = Image.new("RGBA", (cw * COLS, ch * ROWS), (0, 0, 0, 0))
    for idx, name in enumerate(NAMES):
        if not name or sources.get(name) is None:
            continue
        col, row = idx % COLS, idx // COLS
        cell_img = sources[name]
        if cell_img.size != (cw, ch):
            cell_img = cell_img.resize((cw, ch), Image.LANCZOS)
        atlas.paste(cell_img, (col * cw, row * ch), cell_img)

    atlas.save(ATLAS_PNG)
    manifest = {
        "texture": "pet_atlas",
        "cols": COLS,
        "rows": ROWS,
        "cell": [cw, ch],
        "pivot": [0.5, 0.5],
        "ppu": 100,
        "names": NAMES,
    }
    ATLAS_JSON.write_text(
        json.dumps(manifest, ensure_ascii=False, indent=2) + "\n", encoding="utf-8"
    )
    print(f"已生成 {ATLAS_PNG.name} ({atlas.width}x{atlas.height}) 与 {ATLAS_JSON.name}")


def unpack():
    if not ATLAS_PNG.exists() or not ATLAS_JSON.exists():
        print("缺少 pet_atlas.png 或 pet_atlas.json")
        sys.exit(1)
    m = json.loads(ATLAS_JSON.read_text(encoding="utf-8"))
    atlas = Image.open(ATLAS_PNG).convert("RGBA")
    cols, rows = m["cols"], m["rows"]
    cw, ch = atlas.width // cols, atlas.height // rows
    out = IMAGES_DIR / "unpacked"
    out.mkdir(exist_ok=True)
    for idx, name in enumerate(m["names"]):
        if not name:
            continue
        col, row = idx % cols, idx // cols
        box = (col * cw, row * ch, col * cw + cw, row * ch + ch)
        atlas.crop(box).save(out / (name + ".png"))
    print(f"已拆到 {out}")


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["pack", "unpack"])
    ap.add_argument("--no-scale", dest="scale", action="store_false")
    ap.add_argument("--cell", nargs=2, type=int, metavar=("W", "H"))
    ap.set_defaults(scale=True)
    args = ap.parse_args()

    if args.mode == "pack":
        cell = tuple(args.cell) if args.cell else DEFAULT_CELL
        pack(cell, args.scale)
    else:
        unpack()


if __name__ == "__main__":
    main()
