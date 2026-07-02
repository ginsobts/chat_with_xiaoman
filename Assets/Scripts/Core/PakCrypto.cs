using System.Security.Cryptography;
using System.Text;

namespace VN
{
    /// <summary>
    /// game.pak 的加密与打包规则（运行时与编辑器打包脚本共用）。
    /// 注意：这是“防随手翻文件”的混淆，不是加密安全——密钥在程序里，
    /// 反编译可提取。对彩蛋/防剧透足够，别用来存真正的机密。
    /// </summary>
    public static class PakCrypto
    {
        public const string PakFileName = "game.pak";

        // 打包文件保持明文、不进 pak 的文件名（相对 StreamingAssets 根，全小写比较）。
        // True story hint document stays plaintext so players can discover it by browsing files.
        public const string GuideFileName = "the_true_story.txt";

        public static readonly byte[] Magic = Encoding.ASCII.GetBytes("CGJPAK01");

        private static readonly byte[] Key =
        {
            0x93, 0x1c, 0x5f, 0xa8, 0x27, 0xe4, 0x6b, 0xd0,
            0x4a, 0xf1, 0x38, 0x9c, 0x72, 0x05, 0xbe, 0x61,
            0x8d, 0x2f, 0xc3, 0x50, 0x19, 0xa6, 0x7e, 0xdb,
            0x44, 0x90, 0x3b, 0xef, 0x66, 0x11, 0xc5, 0x88
        };

        private static readonly byte[] IV =
        {
            0x5e, 0x21, 0xb7, 0x0c, 0x9a, 0x43, 0xf8, 0x36,
            0x6d, 0xc1, 0x08, 0x94, 0x2b, 0xd7, 0x4f, 0xa2
        };

        private static Aes CreateAes()
        {
            var aes = Aes.Create();
            aes.Key = Key;
            aes.IV = IV;
            aes.Mode = CipherMode.CBC;
            aes.Padding = PaddingMode.PKCS7;
            return aes;
        }

        public static byte[] Encrypt(byte[] plain)
        {
            using (var aes = CreateAes())
            using (var enc = aes.CreateEncryptor())
                return enc.TransformFinalBlock(plain, 0, plain.Length);
        }

        public static byte[] Decrypt(byte[] cipher, int offset, int count)
        {
            using (var aes = CreateAes())
            using (var dec = aes.CreateDecryptor())
                return dec.TransformFinalBlock(cipher, offset, count);
        }

        /// <summary>该相对路径是否保持明文（不打包、不删除）。</summary>
        public static bool KeepPlaintext(string relPath)
        {
            if (string.IsNullOrEmpty(relPath)) return true;
            string p = relPath.Replace('\\', '/').ToLowerInvariant();
            if (p.EndsWith(".meta")) return true;
            if (p == PakFileName.ToLowerInvariant()) return true;
            if (p == GuideFileName.ToLowerInvariant()) return true;
            return false;
        }
    }

    [System.Serializable]
    public class PakEntry
    {
        public string path;
        public int offset;
        public int length;
    }

    [System.Serializable]
    public class PakIndex
    {
        public System.Collections.Generic.List<PakEntry> entries =
            new System.Collections.Generic.List<PakEntry>();
    }
}
