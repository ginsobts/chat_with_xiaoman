# 桌宠立绘图集（换皮）

把桌宠的多张差分立绘拼进一张 `pet_atlas.png`，方便整套替换贴图。

## 文件

- `Assets/StreamingAssets/images/pet_atlas.png`：图集大图（4 列 × 3 行网格）。
- `Assets/StreamingAssets/images/pet_atlas.json`：清单，描述每个格子对应哪个差分名。

游戏运行时 `AssetCache` 会**优先**读图集，按名字裁出对应格子的子图；清单里没有的名字，
仍然按原来的单文件 `images/<名字>.png` 加载（回退，二者可共存）。

## 网格布局（从上到下、从左到右）

| 行\列 | 0 | 1 | 2 | 3 |
|---|---|---|---|---|
| 0 | pet | pet_blink | pet_wink | pet_wave |
| 1 | pet_happy | pet_click | pet_drag | pet_headpat |
| 2 | pet_walk | pet_sleep | （空） | （空） |

- 每张差分都是同样大小的格子（默认 768×512，即源图 1536×1024 的一半）。
- 所有差分保持同样的画布与站位，桌宠切换差分时不会跳动。

## 怎么换皮

1. 用同样的 4×3 网格、同样的格子顺序，做一张新的 `pet_atlas.png`（保持每格构图一致）。
2. 覆盖 `Assets/StreamingAssets/images/pet_atlas.png` 即可，全部差分一起换掉。
   清单不用改（除非你调整了格子数量或顺序）。

## 工具

需要 Pillow：`pip install pillow`

```bash
# 单文件 -> 图集（默认每格缩放到 768×512）
python tools/atlas/pack_pet_atlas.py pack

# 不缩放，保留原始分辨率
python tools/atlas/pack_pet_atlas.py pack --no-scale

# 自定义每格宽高
python tools/atlas/pack_pet_atlas.py pack --cell 1024 683

# 图集 -> 拆回单文件（输出到 images/unpacked/，按格子分辨率）
python tools/atlas/pack_pet_atlas.py unpack
```

## 备注

- 图集做好、确认没问题后，可以删掉 `images/pet_*.png` 这些单文件以减小打包体积
  （它们只是回退用；删掉后就完全走图集）。
- 打包时 `pet_atlas.png` / `pet_atlas.json` 会随 StreamingAssets 一起进加密包，无需额外配置。
