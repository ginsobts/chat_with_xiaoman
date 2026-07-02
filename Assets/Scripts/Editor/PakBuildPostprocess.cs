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
    /// 2) 把 Unity 运行数据目录整理为 game_data（必须与 game.exe 对应）。
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
        ///  - 把 Unity 运行数据夹 &lt;exe&gt;_Data 改名成 game_data。
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

            // 2) Unity 运行数据夹改名成 game_data（大小写差异需两步 Move 以确保真正改成小写）。
            // game.exe 会查找 game_Data/game_data，不能改成 essential_data。
            string finalData = Path.Combine(buildDir, "game_data");
            string tmp = Path.Combine(buildDir, "__game_data_tmp__");

            // 自愈：若上次打包中途失败，数据夹可能残留成 __game_data_tmp__。
            if (!Directory.Exists(dataDir) && Directory.Exists(tmp))
                dataDir = tmp;

            if (Directory.Exists(dataDir) &&
                !string.Equals(dataDir, finalData, System.StringComparison.Ordinal))
            {
                try
                {
                    // 目标已存在（历史遗留的 game_data）会导致 Move 失败，先清掉。
                    if (Directory.Exists(finalData) &&
                        !string.Equals(dataDir, finalData, System.StringComparison.OrdinalIgnoreCase))
                    {
                        Directory.Delete(finalData, true);
                    }

                    // 大小写差异（game_Data -> game_data）在 Windows 上无法直接 Move，需经中转名。
                    // 刚改名后目录句柄可能还被杀软/索引器占用，直接第二次 Move 会 "Access denied"，
                    // 所以两步都用带延时重试的 Move。
                    if (!string.Equals(dataDir, tmp, System.StringComparison.Ordinal))
                    {
                        if (Directory.Exists(tmp)) { try { Directory.Delete(tmp, true); } catch { } }
                        MoveDirWithRetry(dataDir, tmp);
                    }
                    MoveDirWithRetry(tmp, finalData);
                    Debug.Log("[Build] Unity 数据夹已改名为 game_data。");
                }
                catch (System.Exception e)
                {
                    // 关键：改名失败绝不能留下半成品，否则 game.exe 找不到数据夹。
                    // 尽最大努力把数据夹恢复成一个 game.exe 能识别的名字。
                    Debug.LogError("[Build] 数据夹改名失败，尝试回退: " + e.Message);
                    try
                    {
                        if (!Directory.Exists(finalData) && Directory.Exists(tmp))
                            MoveDirWithRetry(tmp, finalData);
                    }
                    catch (System.Exception e2)
                    {
                        Debug.LogError("[Build] 数据夹回退也失败，请手动把 __game_data_tmp__ 改名为 game_data: " + e2.Message);
                    }
                }
            }
        }

        /// <summary>
        /// Windows 上刚被改名/写完的目录常被杀软、Windows 搜索索引、资源管理器短暂占用句柄，
        /// 立刻再对它 Move 会抛 "Access denied"/IOException。这里做带延时的重试直到句柄释放。
        /// </summary>
        private static void MoveDirWithRetry(string from, string to, int attempts = 10, int delayMs = 400)
        {
            for (int i = 0; ; i++)
            {
                try
                {
                    Directory.Move(from, to);
                    return;
                }
                catch (System.Exception) when (i < attempts - 1)
                {
                    // 释放可能残留的托管句柄，给系统一点时间放锁后重试。
                    System.GC.Collect();
                    System.GC.WaitForPendingFinalizers();
                    System.Threading.Thread.Sleep(delayMs);
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
