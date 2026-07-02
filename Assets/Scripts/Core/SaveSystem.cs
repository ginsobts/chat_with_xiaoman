using System;
using System.IO;
using UnityEngine;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
using Microsoft.Win32;
#endif

namespace VN
{
    /// <summary>
    /// 玩家进度与结局状态。
    /// </summary>
    [Serializable]
    public class SaveState
    {
        public string currentNode = "";   // 预留：断点续玩
        public bool petMode = false;       // 结局A：已变成桌面宠物
        public bool locked = false;        // 结局B：已锁死
        public bool avgUnlocked = false;   // true 线结局：桌宠可与 AVG 对话框互相切换
        public int signature = 0;          // 简单校验，用来判断可见存档是否被改坏
    }

    /// <summary>
    /// 双标记存档系统：
    ///  - 可见存档文件（放在 exe 同级目录，玩家能找到、能删）。
    ///  - 隐藏标记（Windows 注册表 + LocalAppData 隐藏文件）作为“真相”。
    /// 当隐藏标记说已锁死，但可见存档丢失/被改坏时，判定为“被篡改”，
    /// 触发特殊彩蛋剧情，但进度依旧锁死、不回退。
    /// </summary>
    public static class SaveSystem
    {
        private const string RegPath = @"Software\\CGJPetGame";
        private const string HiddenFileName = ".cgjpet_marker";

        /// <summary>本次启动是否检测到“可见存档被删/被篡改”。</summary>
        public static bool Tampered { get; private set; }

        private static string VisibleSavePath
        {
            get
            {
                // 编辑器里 dataPath 指向 Assets，用工程根目录做开发用存档。
                string dir = Application.isEditor
                    ? Directory.GetParent(Application.dataPath).FullName
                    : Directory.GetParent(Application.dataPath).FullName; // exe 同级
                return Path.Combine(dir, "存档.sav");
            }
        }

        private static string HiddenFilePath
        {
            get
            {
                string baseDir = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
                if (string.IsNullOrEmpty(baseDir)) baseDir = Application.persistentDataPath;
                return Path.Combine(baseDir, HiddenFileName);
            }
        }

        private static int ComputeSignature(SaveState s)
        {
            // 故意简单：用于区分“正常存档”和“被随意改坏的存档”。
            unchecked
            {
                int h = 17;
                h = h * 31 + (s.petMode ? 1 : 0);
                h = h * 31 + (s.locked ? 1 : 0);
                h = h * 31 + (s.avgUnlocked ? 1 : 0);
                h = h * 31 + (s.currentNode ?? "").GetHashCode();
                h ^= 0x5A5A5A5A;
                return h;
            }
        }

        public static SaveState Load()
        {
            Tampered = false;

            SaveState visible = LoadVisible();
            bool hiddenLocked = ReadHiddenLocked();
            bool hiddenPet = ReadHiddenPet();
            bool hiddenAvg = ReadHiddenFlag("avg");

            // 隐藏标记是“真相”。
            bool trulyLocked = hiddenLocked || (visible != null && visible.locked);
            bool trulyPet = hiddenPet || (visible != null && visible.petMode);
            bool trulyAvg = hiddenAvg || (visible != null && visible.avgUnlocked);

            if (trulyLocked)
            {
                bool visibleConsistent =
                    visible != null && visible.locked &&
                    visible.signature == ComputeSignature(visible);

                if (!visibleConsistent)
                {
                    // 玩家删了/改坏了可见存档，但隐藏标记还在 → 篡改彩蛋。
                    Tampered = true;
                }
            }

            var state = new SaveState
            {
                petMode = trulyPet,
                locked = trulyLocked,
                avgUnlocked = trulyAvg,
                currentNode = visible != null ? visible.currentNode : ""
            };
            return state;
        }

        public static void Save(SaveState state)
        {
            state.signature = ComputeSignature(state);

            // 可见存档
            try
            {
                File.WriteAllText(VisibleSavePath, JsonUtility.ToJson(state, true));
            }
            catch (Exception e) { Debug.LogWarning("写可见存档失败: " + e.Message); }

            // 隐藏标记
            WriteHidden(state);
        }

        private static SaveState LoadVisible()
        {
            try
            {
                if (!File.Exists(VisibleSavePath)) return null;
                string json = File.ReadAllText(VisibleSavePath);
                return JsonUtility.FromJson<SaveState>(json);
            }
            catch { return null; }
        }

        // ---- 隐藏标记：注册表 ----
        private static bool ReadHiddenLocked()
        {
            bool reg = false, file = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (key != null) reg = (int)(key.GetValue("locked", 0)) == 1;
                }
            }
            catch { }
#endif
            file = ReadHiddenFileFlag("locked");
            return reg || file;
        }

        private static bool ReadHiddenPet()
        {
            bool reg = false, file = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (key != null) reg = (int)(key.GetValue("pet", 0)) == 1;
                }
            }
            catch { }
#endif
            file = ReadHiddenFileFlag("pet");
            return reg || file;
        }

        // 通用：读某个隐藏标记（注册表 DWORD + 隐藏文件行任一为真即真）。
        private static bool ReadHiddenFlag(string flag)
        {
            bool reg = false;
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                using (var key = Registry.CurrentUser.OpenSubKey(RegPath))
                {
                    if (key != null) reg = (int)(key.GetValue(flag, 0)) == 1;
                }
            }
            catch { }
#endif
            return reg || ReadHiddenFileFlag(flag);
        }

        private static void WriteHidden(SaveState state)
        {
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try
            {
                using (var key = Registry.CurrentUser.CreateSubKey(RegPath))
                {
                    if (key != null)
                    {
                        key.SetValue("locked", state.locked ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("pet", state.petMode ? 1 : 0, RegistryValueKind.DWord);
                        key.SetValue("avg", state.avgUnlocked ? 1 : 0, RegistryValueKind.DWord);
                    }
                }
            }
            catch (Exception e) { Debug.LogWarning("写注册表失败: " + e.Message); }
#endif
            try
            {
                string content = (state.locked ? "locked\n" : "")
                    + (state.petMode ? "pet\n" : "")
                    + (state.avgUnlocked ? "avg\n" : "");
                File.WriteAllText(HiddenFilePath, content);
                File.SetAttributes(HiddenFilePath, FileAttributes.Hidden);
            }
            catch (Exception e) { Debug.LogWarning("写隐藏文件失败: " + e.Message); }
        }

        private static bool ReadHiddenFileFlag(string flag)
        {
            try
            {
                if (!File.Exists(HiddenFilePath)) return false;
                foreach (var line in File.ReadAllLines(HiddenFilePath))
                    if (line.Trim() == flag) return true;
            }
            catch { }
            return false;
        }

        /// <summary>调试用：彻底清空所有标记（仅开发期使用）。</summary>
        public static void DevResetAll()
        {
            try { if (File.Exists(VisibleSavePath)) File.Delete(VisibleSavePath); } catch { }
            try { if (File.Exists(HiddenFilePath)) File.Delete(HiddenFilePath); } catch { }
#if UNITY_STANDALONE_WIN || UNITY_EDITOR_WIN
            try { Registry.CurrentUser.DeleteSubKey(RegPath, false); } catch { }
#endif
            Tampered = false;
        }
    }
}
