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

        // ---------- 日历 / 提醒 ----------
        public static string Calendar => IsEnglish ? "Calendar" : "日历";
        public static string CalendarTitle => IsEnglish ? "Calendar" : "日历提醒";
        public static string NewReminder => IsEnglish ? "New Reminder" : "新建提醒";
        public static string EditReminder => IsEnglish ? "Edit Reminder" : "编辑提醒";
        public static string ReminderName => IsEnglish ? "Name" : "名称";
        public static string ReminderNamePlaceholder => IsEnglish ? "e.g. Birthday" : "如：生日";
        public static string ReminderType => IsEnglish ? "Type" : "类型";
        public static string TypeYearly => IsEnglish ? "Every year" : "每年";
        public static string TypeOneoff => IsEnglish ? "Specific date" : "指定日期";
        public static string TypeCountdown => IsEnglish ? "Days from a date" : "从某天起算";
        public static string TypeInterval => IsEnglish ? "Every N days" : "每隔几天";
        public static string TypeLunar => IsEnglish ? "Lunar" : "农历";
        public static string TypeSolarTerm => IsEnglish ? "Solar term" : "节气";
        public static string LunarFestivalNote => IsEnglish
            ? "Lunar / solar-term festival — repeats yearly on the traditional date."
            : "农历 / 节气节日，每年按传统日期自动提醒。";
        public static string DateLabel => IsEnglish ? "Date" : "日期";
        public static string MonthDayLabel => IsEnglish ? "Month / Day" : "月 / 日";
        public static string BaseDateLabel => IsEnglish ? "Base date" : "基准日期";
        public static string OffsetDaysLabel => IsEnglish ? "Days after" : "之后第几天";
        public static string IntervalDaysLabel => IsEnglish ? "Repeat every (days)" : "每隔几天提醒";
        public static string LeadDaysLabel => IsEnglish ? "Remind days ahead" : "提前几天提醒";
        public static string SaveLabel => IsEnglish ? "Save" : "保存";
        public static string DeleteLabel => IsEnglish ? "Delete" : "删除";
        public static string CancelLabel => IsEnglish ? "Cancel" : "取消";
        public static string CloseLabel => IsEnglish ? "Close" : "关闭";
        public static string ConfirmGotIt => IsEnglish ? "Got it" : "知道了";
        public static string NoReminders => IsEnglish
            ? "No reminders yet. Tap New Reminder to add one."
            : "还没有提醒，点“新建提醒”添加一个吧。";
        public static string YearUnit => IsEnglish ? "Y" : "年";
        public static string MonthUnit => IsEnglish ? "M" : "月";
        public static string DayUnit => IsEnglish ? "D" : "日";
        public static string DaysUnit => IsEnglish ? "days" : "天";

        // ---------- 预览 / 节日 / 导入导出 ----------
        public static string NextRemindLabel => IsEnglish ? "Next reminder" : "下次提醒";
        public static string NextRemindNone => IsEnglish ? "no upcoming date" : "暂无即将到来的日期";
        public static string PickFestival => IsEnglish ? "Festivals" : "选择节日";
        public static string PickFestivalTitle => IsEnglish ? "Pick a festival (yearly)" : "选择节日（每年提醒）";
        public static string ExportLabel => IsEnglish ? "Export" : "导出";
        public static string ImportLabel => IsEnglish ? "Import" : "导入";
        public static string ExportedTip(string path) => IsEnglish
            ? "Exported to:\n" + path + "\n(also copied to clipboard)"
            : "已导出到：\n" + path + "\n（同时已复制到剪贴板）";
        public static string ImportedTip(int n) => IsEnglish
            ? "Imported " + n + " reminder" + (n == 1 ? "" : "s") + "."
            : "已导入 " + n + " 条提醒。";
        public static string ImportNothingTip => IsEnglish
            ? "Nothing to import. Export/copy on the other machine first."
            : "没有找到可导入的内容，请先在另一台电脑导出或复制。";

        public static string NextRemindPreview(string dateText) =>
            NextRemindLabel + (IsEnglish ? ": " : "：") + dateText;

        public static string[] WeekdayShort => IsEnglish
            ? new[] { "Su", "Mo", "Tu", "We", "Th", "Fr", "Sa" }
            : new[] { "日", "一", "二", "三", "四", "五", "六" };

        public static string MonthTitle(int year, int month)
        {
            if (month < 1) month = 1; if (month > 12) month = 12;
            if (IsEnglish)
            {
                string[] names = { "January", "February", "March", "April", "May", "June",
                    "July", "August", "September", "October", "November", "December" };
                return names[month - 1] + " " + year;
            }
            return year + "年" + month + "月";
        }

        public static string RemindDaysLeft(string title, int days) => IsEnglish
            ? days + " day" + (days == 1 ? "" : "s") + " until \"" + title + "\"."
            : "距离【" + title + "】还有 " + days + " 天哦。";
        public static string RemindToday(string title) => IsEnglish
            ? "Today is \"" + title + "\"!"
            : "今天就是【" + title + "】啦！";

        public static string OpeningIntro => IsEnglish
            ? "Welcome. You are about to experience a conversation of about five minutes.\n\n"
              + "The person speaking with you is Xiaoman. She has been waiting for you for a long time.\n\n"
              + "If you are especially attentive, perhaps you will find a way for her to open her heart..."
            : "欢迎来到这里，接下来你将体验大约5分钟的对话\n\n"
              + "和你对话的人叫小满，她等你很久啦。\n\n"
              + "如果你注意力惊人，或许能找到让她敞开心扉的方法……";
    }
}
