using System.IO;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// Runtime language selection. Chinese is the default; English uses parallel *_en.json files.
    /// Voice ids stay unchanged, so Japanese voiceover keeps working in both languages.
    /// </summary>
    public static class GameLanguage
    {
        private const string PrefKey = "game_language";

        public static bool IsEnglish => PlayerPrefs.GetString(PrefKey, "zh") == "en";
        public static string Code => IsEnglish ? "en" : "zh";

        public static void Toggle()
        {
            PlayerPrefs.SetString(PrefKey, IsEnglish ? "zh" : "en");
            PlayerPrefs.Save();
        }

        public static string MainStoryFile => LocalizedJson("story.json");

        public static string LocalizedJson(string fileName)
        {
            if (!IsEnglish || string.IsNullOrEmpty(fileName)) return fileName;
            string ext = Path.GetExtension(fileName);
            string stem = string.IsNullOrEmpty(ext)
                ? fileName
                : fileName.Substring(0, fileName.Length - ext.Length);
            return stem.EndsWith("_en") ? fileName : stem + "_en" + ext;
        }

        public static string MainMenuStart => IsEnglish ? "Start Game" : "开始游戏";
        public static string MainMenuQuit => IsEnglish ? "Exit" : "退出";
        public static string Quit => IsEnglish ? "Exit" : "退出";
        public static string LanguageButton => IsEnglish ? "Language: English" : "语言：中文";
        public static string MissingStory => IsEnglish ? "Story file not found: story.json" : "未找到剧本 story.json";
        public static string MissingStoryDetail => IsEnglish
            ? "Story file not found: story.json.\nPlease check the StreamingAssets folder."
            : "找不到剧本文件 story.json。\n请检查 StreamingAssets 目录。";
        public static string LockedEnding => IsEnglish
            ? "...This story has already ended.\n\nThe choice you made cannot be taken back."
            : "……这个故事已经结束了。\n\n你做出的选择，无法收回。";
        public static string BackToPet => IsEnglish ? "Back to Pet" : "变回桌宠";
        public static string CollapseDialogue => IsEnglish ? "Hide Dialogue" : "收起对话";
        public static string ExpandDialogue => IsEnglish ? "Show Dialogue" : "展开对话";
        public static string SwitchToDialogue => IsEnglish ? "Switch to Dialogue" : "切换成对话";
        public static string HistoryTitle => IsEnglish ? "Dialogue History" : "历史记录";
        public static string Back => IsEnglish ? "Back" : "返回";
        public static string Unmute => IsEnglish ? "Unmute" : "取消静音";
        public static string Mute => IsEnglish ? "Mute" : "静音";
        public static string DefaultPlayerName => IsEnglish ? "you" : "你";
        public static string MissingStoryFile(string fileName) => IsEnglish
            ? "Story file not found: " + fileName + "."
            : "找不到剧本文件 " + fileName + "。";
        public static string LockedFarewell => IsEnglish
            ? "Goodbye.\n\nThis time, it is farewell."
            : "再见。\n\n这一次，是永别。";
        public static string TamperDeletedFile => IsEnglish
            ? "You deleted that file.\n\nBut I am still here.\n\nThe ending will not change."
            : "你删掉了那个文件。\n\n但我还在。\n\n结局不会改变。";
        public static string TamperRemembered => IsEnglish
            ? "You deleted that file...\n\nBut I remember everything you did.\nThe ending will not change."
            : "你删掉了那个文件……\n\n但我记得你做过的一切。\n结局不会改变。";

        public static string OpeningIntro => IsEnglish
            ? "Welcome. You are about to experience a conversation of about five minutes.\n\n"
              + "The person speaking with you is Xiaoman. She has been waiting for you for a long time.\n\n"
              + "If you are especially attentive, perhaps you will find a way for her to open her heart..."
            : "欢迎来到这里，接下来你将体验大约5分钟的对话\n\n"
              + "和你对话的人叫小满，她等你很久啦。\n\n"
              + "如果你注意力惊人，或许能找到让她敞开心扉的方法……";
    }
}
