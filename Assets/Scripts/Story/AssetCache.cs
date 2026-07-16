using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// 从 StreamingAssets/images/ 加载 PNG/JPG，找不到时生成彩色占位图。
    /// 若存在 images/pet_atlas.png + pet_atlas.json 图集清单，清单里列出的差分名会优先
    /// 从这张共享大图里裁子矩形取用（换皮只需替换图集这一张图）；未列入的名字仍按单文件加载。
    /// </summary>
    public static class AssetCache
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        // 图集：名字 -> 在图集纹理中的子矩形（Unity 纹理坐标，原点左下）。
        private static readonly Dictionary<string, Rect> _atlasRects = new Dictionary<string, Rect>();
        private static Texture2D _atlasTex;
        private static Vector2 _atlasPivot = new Vector2(0.5f, 0.5f);
        private static float _atlasPpu = 100f;
        private static bool _atlasTried;

        public static Sprite GetSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var cached)) return cached;

            Sprite sprite = LoadFromAtlas(name) ?? LoadFromDisk(name) ?? MakePlaceholder(name);
            _cache[name] = sprite;
            return sprite;
        }

        // 从 Pak（加密包）或明文文件读取 images/ 下的资源字节。
        private static byte[] ReadAsset(string relativeInImages)
        {
            string rel = "images/" + relativeInImages;
            if (Pak.TryGet(rel, out var bytes) && bytes != null) return bytes;

            string path = Path.Combine(Application.streamingAssetsPath, "images", relativeInImages);
            if (File.Exists(path))
            {
                try { return File.ReadAllBytes(path); }
                catch { return null; }
            }
            return null;
        }

        // ---------------- 图集 ----------------

        [Serializable]
        private class AtlasManifest
        {
            public string texture = "pet_atlas";
            public int cols;
            public int rows;
            public float[] pivot;
            public float ppu = 100f;
            public string[] names;
        }

        private static void EnsureAtlasLoaded()
        {
            if (_atlasTried) return;
            _atlasTried = true;

            byte[] manifestBytes = ReadAsset("pet_atlas.json");
            if (manifestBytes == null) return;

            AtlasManifest m;
            try { m = JsonUtility.FromJson<AtlasManifest>(System.Text.Encoding.UTF8.GetString(manifestBytes)); }
            catch { return; }
            if (m == null || m.names == null || m.cols <= 0 || m.rows <= 0) return;

            string texName = string.IsNullOrEmpty(m.texture) ? "pet_atlas" : m.texture;
            byte[] texBytes = ReadAsset(texName + ".png") ?? ReadAsset(texName + ".jpg");
            if (texBytes == null) return;

            var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
            if (!tex.LoadImage(texBytes)) return;
            tex.wrapMode = TextureWrapMode.Clamp;
            _atlasTex = tex;

            if (m.pivot != null && m.pivot.Length == 2) _atlasPivot = new Vector2(m.pivot[0], m.pivot[1]);
            if (m.ppu > 0f) _atlasPpu = m.ppu;

            float cellW = (float)tex.width / m.cols;
            float cellH = (float)tex.height / m.rows;
            for (int i = 0; i < m.names.Length; i++)
            {
                string n = m.names[i];
                if (string.IsNullOrEmpty(n)) continue;
                int col = i % m.cols;
                int row = i / m.cols;               // 清单从上到下、从左到右
                if (row >= m.rows) break;
                float x = col * cellW;
                float y = (m.rows - 1 - row) * cellH; // 纹理原点在左下，需翻转行
                _atlasRects[n] = new Rect(x, y, cellW, cellH);
            }
        }

        private static Sprite LoadFromAtlas(string name)
        {
            EnsureAtlasLoaded();
            if (_atlasTex == null) return null;
            if (!_atlasRects.TryGetValue(name, out var rect)) return null;
            return Sprite.Create(_atlasTex, rect, _atlasPivot, _atlasPpu);
        }

        // ---------------- 单文件 ----------------

        private static Sprite LoadFromDisk(string name)
        {
            string[] exts = { ".png", ".jpg", ".jpeg" };
            foreach (var ext in exts)
            {
                byte[] bytes = ReadAsset(name + ext);
                if (bytes == null) continue;
                try
                {
                    var tex = new Texture2D(2, 2, TextureFormat.RGBA32, false);
                    if (tex.LoadImage(bytes))
                    {
                        tex.wrapMode = TextureWrapMode.Clamp;
                        return Sprite.Create(tex,
                            new Rect(0, 0, tex.width, tex.height),
                            new Vector2(0.5f, 0.5f), 100f);
                    }
                }
                catch { }
            }
            return null;
        }

        // 用名字 hash 生成稳定颜色的占位图，方便区分不同素材。
        private static Sprite MakePlaceholder(string name)
        {
            int hash = name.GetHashCode();
            Color c = Color.HSVToRGB((Mathf.Abs(hash) % 360) / 360f, 0.45f, 0.85f);

            bool isBackground = name.StartsWith("bg");
            int w = isBackground ? 320 : 160;
            int h = isBackground ? 180 : 280;

            var tex = new Texture2D(w, h, TextureFormat.RGBA32, false);
            var px = new Color[w * h];
            for (int y = 0; y < h; y++)
            {
                float t = (float)y / h;
                Color bgCol = Color.Lerp(c, c * 0.6f, t); // 背景：竖向渐变
                for (int x = 0; x < w; x++)
                {
                    px[y * w + x] = isBackground
                        ? bgCol
                        : ColorForCharPixel(c, x, y, w, h); // 角色：简易剪影
                }
            }
            tex.SetPixels(px);
            tex.Apply();
            tex.filterMode = FilterMode.Bilinear;
            return Sprite.Create(tex, new Rect(0, 0, w, h), new Vector2(0.5f, 0.5f), 100f);
        }

        // 角色占位：中间一个圆头+身体的纯色剪影，其余透明。
        private static Color ColorForCharPixel(Color body, int x, int y, int w, int h)
        {
            float cx = w * 0.5f;
            // 头
            float headCx = cx, headCy = h * 0.82f, headR = w * 0.22f;
            if ((x - headCx) * (x - headCx) + (y - headCy) * (y - headCy) < headR * headR)
                return body;
            // 身体（梯形）
            float bodyTop = h * 0.60f;
            if (y < bodyTop)
            {
                float t = y / bodyTop;
                float halfW = Mathf.Lerp(w * 0.40f, w * 0.18f, t);
                if (Mathf.Abs(x - cx) < halfW)
                    return body * 0.92f;
            }
            return Color.clear;
        }
    }
}
