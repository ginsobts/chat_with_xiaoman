using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// 运行时读取 StreamingAssets/game.pak（加密打包的素材）。
    /// 若不存在（例如编辑器里开发），则 Available 为 false，各加载器回退读明文文件。
    /// </summary>
    public static class Pak
    {
        private static bool _init;
        private static byte[] _decrypted;             // 解密后的整块：[4字节indexLen][indexJson][payload]
        private static int _payloadStart;
        private static Dictionary<string, PakEntry> _index;

        public static bool Available
        {
            get { EnsureInit(); return _index != null; }
        }

        private static void EnsureInit()
        {
            if (_init) return;
            _init = true;

            try
            {
                string path = Path.Combine(Application.streamingAssetsPath, PakCrypto.PakFileName);
                if (!File.Exists(path)) return;

                byte[] raw = File.ReadAllBytes(path);
                int m = PakCrypto.Magic.Length;
                if (raw.Length <= m) { Debug.LogWarning("[Pak] 文件过短"); return; }
                for (int i = 0; i < m; i++)
                {
                    if (raw[i] != PakCrypto.Magic[i]) { Debug.LogWarning("[Pak] 魔数不匹配"); return; }
                }

                _decrypted = PakCrypto.Decrypt(raw, m, raw.Length - m);
                int indexLen = BitConverter.ToInt32(_decrypted, 0);
                string indexJson = System.Text.Encoding.UTF8.GetString(_decrypted, 4, indexLen);
                var idx = JsonUtility.FromJson<PakIndex>(indexJson);
                _payloadStart = 4 + indexLen;

                _index = new Dictionary<string, PakEntry>();
                if (idx != null && idx.entries != null)
                {
                    foreach (var e in idx.entries)
                        _index[e.path.Replace('\\', '/')] = e;
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[Pak] 读取失败，将回退明文: " + e.Message);
                _index = null;
                _decrypted = null;
            }
        }

        public static bool TryGet(string relPath, out byte[] bytes)
        {
            bytes = null;
            EnsureInit();
            if (_index == null) return false;

            var key = relPath.Replace('\\', '/');
            if (!_index.TryGetValue(key, out var e)) return false;

            bytes = new byte[e.length];
            Array.Copy(_decrypted, _payloadStart + e.offset, bytes, 0, e.length);
            return true;
        }

        public static bool TryGetText(string relPath, out string text)
        {
            text = null;
            if (!TryGet(relPath, out var bytes)) return false;
            text = System.Text.Encoding.UTF8.GetString(bytes);
            return true;
        }
    }
}
