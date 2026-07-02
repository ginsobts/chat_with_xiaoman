using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// 从 StreamingAssets/images/ 加载 PNG/JPG，找不到时生成彩色占位图。
    /// </summary>
    public static class AssetCache
    {
        private static readonly Dictionary<string, Sprite> _cache = new Dictionary<string, Sprite>();

        public static Sprite GetSprite(string name)
        {
            if (string.IsNullOrEmpty(name)) return null;
            if (_cache.TryGetValue(name, out var cached)) return cached;

            Sprite sprite = LoadFromDisk(name) ?? MakePlaceholder(name);
            _cache[name] = sprite;
            return sprite;
        }

        private static Sprite LoadFromDisk(string name)
        {
            string dir = Path.Combine(Application.streamingAssetsPath, "images");
            string[] exts = { ".png", ".jpg", ".jpeg" };
            foreach (var ext in exts)
            {
                byte[] bytes = null;

                // 优先从加密包取图；没有再读明文文件。
                if (!Pak.TryGet("images/" + name + ext, out bytes))
                {
                    string path = Path.Combine(dir, name + ext);
                    if (File.Exists(path))
                    {
                        try { bytes = File.ReadAllBytes(path); }
                        catch { bytes = null; }
                    }
                }

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
