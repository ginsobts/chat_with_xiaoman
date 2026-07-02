using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build;
using UnityEditor.Build.Reporting;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VN.EditorTools
{
    /// <summary>
    /// 打包完成后：
    /// 1) 把 StreamingAssets 素材（story/图片/语音等）合并加密成 game.pak，并删除原明文文件。
    /// 2) 把神秘指导文档移到 build 根目录下的 game_data 文件夹，让该文件夹只放提示 txt。
    /// 3) 把 Unity 真正运行需要的数据目录改名成 essential_data。
    /// </summary>
    public class PakBuildPostprocess : IPostprocessBuildWithReport
    {
        public int callbackOrder => 100;

        public void OnPostprocessBuild(BuildReport report)
        {
            var platform = report.summary.platform;
            if (platform != BuildTarget.StandaloneWindows64 && platform != BuildTarget.StandaloneWindows)
                return;

            string exe = report.summary.outputPath;
            if (string.IsNullOrEmpty(exe)) return;

            string buildDir = Path.GetDirectoryName(exe);
            string dataDir = Path.Combine(buildDir, Path.GetFileNameWithoutExtension(exe) + "_Data");
            string saDir = Path.Combine(dataDir, "StreamingAssets");

            if (!Directory.Exists(saDir))
            {
                Debug.LogWarning("[Pak] build 里没有 StreamingAssets，跳过打包。");
                return;
            }

            try
            {
                PackFolder(saDir);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Pak] 打包失败: " + e.Message);
            }

            try
            {
                ReorganizeBuild(buildDir, dataDir, saDir);
            }
            catch (System.Exception e)
            {
                Debug.LogError("[Build] 目录整理失败: " + e.Message);
            }
        }

        /// <summary>
        /// 整理打包目录，让玩家看到的根目录更干净：
        ///  - 删掉崩溃处理器、Burst 调试等发布不需要的散件；
        ///  - 把神秘指导文档移到 game_data（这个文件夹只放提示 txt）；
        ///  - 把 Unity 运行数据夹 &lt;exe&gt;_Data 改名成 essential_data。
        /// UnityPlayer.dll 必须和 exe 同级，无法移动，予以保留。
        /// </summary>
        private void ReorganizeBuild(string buildDir, string dataDir, string saDir)
        {
            // 1) 删掉根目录里发布用不到的散件
            foreach (var name in new[] { "UnityCrashHandler64.exe", "UnityCrashHandler32.exe" })
            {
                string p = Path.Combine(buildDir, name);
                try { if (File.Exists(p)) File.Delete(p); } catch { }
            }
            // Burst 调试信息目录（*_BurstDebugInformation_DoNotShip）不用发给玩家
            foreach (var d in Directory.GetDirectories(buildDir, "*_DoNotShip", SearchOption.TopDirectoryOnly))
            {
                try { Directory.Delete(d, true); } catch { }
            }

            // 2) 玩家可见提示文件夹：game_data 只放神秘指导 txt。
            string guideSrc = Path.Combine(saDir, PakCrypto.GuideFileName);
            string guideDir = Path.Combine(buildDir, "game_data");
            if (Directory.Exists(guideDir)) Directory.Delete(guideDir, true);
            Directory.CreateDirectory(guideDir);
            if (File.Exists(guideSrc))
            {
                string guideDst = Path.Combine(guideDir, PakCrypto.GuideFileName);
                File.Move(guideSrc, guideDst);
                Debug.Log("[Build] 神秘指导文档已移到 game_data。");
            }
            else
            {
                Debug.LogWarning("[Build] 未找到神秘指导文档，game_data 将为空。");
            }

            // 3) Unity 运行数据夹改名成 essential_data（大小写差异需两步 Move 以确保真正改成小写）
            if (Directory.Exists(dataDir))
            {
                string finalData = Path.Combine(buildDir, "essential_data");
                if (!string.Equals(dataDir, finalData, System.StringComparison.Ordinal))
                {
                    string tmp = Path.Combine(buildDir, "__essential_data_tmp__");
                    if (Directory.Exists(tmp)) { try { Directory.Delete(tmp, true); } catch { } }
                    Directory.Move(dataDir, tmp);
                    Directory.Move(tmp, finalData);
                    Debug.Log("[Build] Unity 数据夹已改名为 essential_data。");
                }
            }
        }

        private void PackFolder(string saDir)
        {
            var allFiles = Directory.GetFiles(saDir, "*", SearchOption.AllDirectories);
            var toPack = new List<string>();
            foreach (var f in allFiles)
            {
                string rel = f.Substring(saDir.Length + 1).Replace('\\', '/');
                if (PakCrypto.KeepPlaintext(rel)) continue;
                toPack.Add(f);
            }

            if (toPack.Count == 0)
            {
                Debug.LogWarning("[Pak] 没有可打包的文件。");
                return;
            }

            var index = new PakIndex();
            using (var payload = new MemoryStream())
            {
                foreach (var f in toPack)
                {
                    string rel = f.Substring(saDir.Length + 1).Replace('\\', '/');
                    byte[] b = File.ReadAllBytes(f);
                    index.entries.Add(new PakEntry
                    {
                        path = rel,
                        offset = (int)payload.Position,
                        length = b.Length
                    });
                    payload.Write(b, 0, b.Length);
                }

                byte[] indexBytes = System.Text.Encoding.UTF8.GetBytes(JsonUtility.ToJson(index));
                byte[] payloadBytes = payload.ToArray();

                byte[] plain;
                using (var all = new MemoryStream())
                {
                    all.Write(System.BitConverter.GetBytes(indexBytes.Length), 0, 4);
                    all.Write(indexBytes, 0, indexBytes.Length);
                    all.Write(payloadBytes, 0, payloadBytes.Length);
                    plain = all.ToArray();
                }

                byte[] enc = PakCrypto.Encrypt(plain);
                string pakPath = Path.Combine(saDir, PakCrypto.PakFileName);
                using (var fs = new FileStream(pakPath, FileMode.Create))
                {
                    fs.Write(PakCrypto.Magic, 0, PakCrypto.Magic.Length);
                    fs.Write(enc, 0, enc.Length);
                }
            }

            // 删除原明文文件
            foreach (var f in toPack)
            {
                try { File.Delete(f); } catch { }
            }

            // 删掉打包后变空的子目录（images / voice 等），先删深层。
            foreach (var d in Directory.GetDirectories(saDir, "*", SearchOption.AllDirectories)
                         .OrderByDescending(p => p.Length))
            {
                try
                {
                    if (Directory.Exists(d) && !Directory.EnumerateFileSystemEntries(d).Any())
                        Directory.Delete(d);
                }
                catch { }
            }

            Debug.Log($"[Pak] 已打包并加密 {toPack.Count} 个文件 -> {Path.Combine(saDir, PakCrypto.PakFileName)}（原明文已删除）");
        }
    }
}
