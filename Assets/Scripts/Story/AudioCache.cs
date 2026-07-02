using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.Networking;

namespace VN
{
    /// <summary>
    /// 从 StreamingAssets/voice/ 加载配音（wav/ogg/mp3），带缓存。
    /// 找不到文件时回调 null（当作没有配音处理）。
    /// </summary>
    public static class AudioCache
    {
        private static readonly Dictionary<string, AudioClip> _cache = new Dictionary<string, AudioClip>();

        public static IEnumerator Load(string name, Action<AudioClip> onLoaded)
        {
            if (string.IsNullOrEmpty(name))
            {
                onLoaded?.Invoke(null);
                yield break;
            }

            if (_cache.TryGetValue(name, out var cached))
            {
                onLoaded?.Invoke(cached);
                yield break;
            }

            // 优先从加密包取语音（wav 直接内存解码；其它格式落临时文件再读）。
            if (Pak.TryGet("voice/" + name + ".wav", out var wavBytes))
            {
                var clip = WavUtility.ToAudioClip(wavBytes, name);
                if (clip != null)
                {
                    _cache[name] = clip;
                    onLoaded?.Invoke(clip);
                    yield break;
                }
            }
            foreach (var packExt in new[] { ".ogg", ".mp3" })
            {
                if (!Pak.TryGet("voice/" + name + packExt, out var enc)) continue;
                var clip = LoadFromBytesViaTempFile(name, packExt, enc);
                foreach (var step in clip) yield return step;
                if (_cache.TryGetValue(name, out var got))
                {
                    onLoaded?.Invoke(got);
                    yield break;
                }
            }

            string dir = Path.Combine(Application.streamingAssetsPath, "voice");
            string[] exts = { ".wav", ".ogg", ".mp3" };
            foreach (var ext in exts)
            {
                string path = Path.Combine(dir, name + ext);
                if (!File.Exists(path)) continue;

                AudioType type = ext == ".wav" ? AudioType.WAV
                    : ext == ".ogg" ? AudioType.OGGVORBIS
                    : AudioType.MPEG;

                string url = "file:///" + path.Replace("\\", "/");
                using (var req = UnityWebRequestMultimedia.GetAudioClip(url, type))
                {
                    yield return req.SendWebRequest();
                    if (req.result == UnityWebRequest.Result.Success)
                    {
                        var clip = DownloadHandlerAudioClip.GetContent(req);
                        _cache[name] = clip;
                        onLoaded?.Invoke(clip);
                        yield break;
                    }
                    Debug.LogWarning("[VN Voice] 加载失败: " + path + " -> " + req.error);
                }
            }

            onLoaded?.Invoke(null);
        }

        // ogg/mp3 无法直接内存解码，落一个临时文件用 UnityWebRequest 读，读完即删。
        private static IEnumerable LoadFromBytesViaTempFile(string name, string ext, byte[] bytes)
        {
            string temp = Path.Combine(Application.temporaryCachePath, "v_" + name + ext);
            AudioType type = ext == ".ogg" ? AudioType.OGGVORBIS : AudioType.MPEG;
            UnityWebRequest req = null;
            try
            {
                File.WriteAllBytes(temp, bytes);
                string url = "file:///" + temp.Replace("\\", "/");
                req = UnityWebRequestMultimedia.GetAudioClip(url, type);
            }
            catch (Exception e)
            {
                Debug.LogWarning("[VN Voice] 临时文件写入失败: " + e.Message);
                yield break;
            }

            using (req)
            {
                yield return req.SendWebRequest();
                if (req.result == UnityWebRequest.Result.Success)
                {
                    var clip = DownloadHandlerAudioClip.GetContent(req);
                    _cache[name] = clip;
                }
            }
            try { if (File.Exists(temp)) File.Delete(temp); } catch { }
        }
    }
}
