using System.Diagnostics;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEngine;
using Debug = UnityEngine.Debug;

namespace VN.EditorTools
{
    /// <summary>
    /// 打包脚本。两种用法：
    /// 1) 编辑器菜单：顶部菜单 "打包" -> "打包 Windows(64位)"，点一下即可。
    /// 2) 命令行 batchmode：
    ///    Unity.exe -quit -batchmode -projectPath &lt;proj&gt; -executeMethod VN.EditorTools.BuildScript.BuildWindows
    /// 输出到项目根目录 Build/cgjpetgame/。
    /// 注意：exe 命名为 game.exe，因此 Unity 的运行数据夹需要叫 game_Data/game_data。
    /// </summary>
    public static class BuildScript
    {
        private const string OutputDir = "Build/cgjpetgame";
        private const string ExeName = "game.exe";

        [MenuItem("打包/打包 Windows(64位)")]
        public static void BuildWindowsMenu()
        {
            string exe = DoBuild();
            if (exe != null)
            {
                // 打包成功后自动打开输出目录
                EditorUtility.RevealInFinder(exe);
                if (EditorUtility.DisplayDialog("打包完成",
                        "已生成:\n" + exe + "\n\n是否直接运行试试桌宠？", "运行", "关闭"))
                {
                    try { Process.Start(exe); }
                    catch (System.Exception e) { Debug.LogError("[Build] 启动失败: " + e.Message); }
                }
            }
            else
            {
                EditorUtility.DisplayDialog("打包失败", "构建失败，详情看 Console。", "好");
            }
        }

        /// <summary>命令行 batchmode 入口。</summary>
        public static void BuildWindows()
        {
            string exe = DoBuild();
            EditorApplication.Exit(exe != null ? 0 : 2);
        }

        /// <summary>执行构建，成功返回 exe 路径，失败返回 null。</summary>
        private static string DoBuild()
        {
            string[] scenes = EditorBuildSettings.scenes
                .Where(s => s.enabled)
                .Select(s => s.path)
                .ToArray();

            if (scenes.Length == 0)
            {
                var guids = AssetDatabase.FindAssets("t:Scene", new[] { "Assets" });
                scenes = guids.Select(AssetDatabase.GUIDToAssetPath)
                              .Where(p => p.StartsWith("Assets/"))
                              .ToArray();
            }

            if (scenes.Length == 0)
            {
                Debug.LogError("[Build] 找不到任何场景，无法打包。");
                return null;
            }

            string projectRoot = Directory.GetParent(Application.dataPath).FullName;
            string outDir = Path.Combine(projectRoot, OutputDir);
            if (Directory.Exists(outDir))
                Directory.Delete(outDir, true);
            Directory.CreateDirectory(outDir);
            string outExe = Path.Combine(outDir, ExeName);

            var options = new BuildPlayerOptions
            {
                scenes = scenes,
                locationPathName = outExe,
                target = BuildTarget.StandaloneWindows64,
                options = BuildOptions.None,
            };

            Debug.Log("[Build] 场景: " + string.Join(", ", scenes));
            Debug.Log("[Build] 输出: " + outExe);

            var report = BuildPipeline.BuildPlayer(options);
            var summary = report.summary;

            if (summary.result == UnityEditor.Build.Reporting.BuildResult.Succeeded)
            {
                Debug.Log("[Build] 成功: " + summary.totalSize + " bytes -> " + outExe);
                return outExe;
            }

            Debug.LogError("[Build] 失败: " + summary.result + " (errors=" + summary.totalErrors + ")");
            return null;
        }
    }
}
