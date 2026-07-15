using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace VN
{
    /// <summary>
    /// 一条日历提醒。
    /// type：
    ///   "yearly"    每年重复：用 month/day，每年当天提醒。
    ///   "oneoff"    一次性：用 year/month/day，只在那一天（及提前期）提醒一次。
    ///   "countdown" 从基准日算起：用 year/month/day 作为基准，加 offsetDays 天得到目标日（一次性）。
    ///   "interval"  从基准日起每 intervalDays 天重复提醒（如每 10 天一次）。
    /// leadDays：提前多少天开始提醒（默认 5）。
    /// lastAckDate：玩家最近一次点“知道了”确认的日期（yyyy-MM-dd），用于每天只弹一次。
    /// </summary>
    [Serializable]
    public class Reminder
    {
        public string id = "";
        public string title = "";
        public string type = "oneoff";
        public int year;
        public int month = 1;
        public int day = 1;
        public int offsetDays;
        public int intervalDays = 10;
        public int leadDays = 5;
        public string lastAckDate = "";
    }

    [Serializable]
    internal class ReminderFile
    {
        public List<Reminder> reminders = new List<Reminder>();
    }

    /// <summary>
    /// 桌宠日历提醒的读写与到期计算。数据存在 persistentDataPath/pet_reminders.json。
    /// </summary>
    public class ReminderStore
    {
        public const string YEARLY = "yearly";
        public const string ONEOFF = "oneoff";
        public const string COUNTDOWN = "countdown";
        public const string INTERVAL = "interval";
        public const string LUNAR = "lunar";     // 农历每年重复：month/day 为农历月/日，day=0 表示除夕
        public const string QINGMING = "qingming"; // 清明节气，每年重复
        public const int DefaultLeadDays = 5;

        private readonly List<Reminder> _items = new List<Reminder>();

        public IReadOnlyList<Reminder> Items => _items;

        private static string FilePath =>
            Path.Combine(Application.persistentDataPath, "pet_reminders.json");

        public static ReminderStore Load()
        {
            var store = new ReminderStore();
            try
            {
                if (File.Exists(FilePath))
                {
                    string json = File.ReadAllText(FilePath);
                    var file = JsonUtility.FromJson<ReminderFile>(json);
                    if (file != null && file.reminders != null)
                        store._items.AddRange(file.reminders);
                }
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ReminderStore] 读取提醒失败: " + e.Message);
            }
            return store;
        }

        public void Save()
        {
            try
            {
                var file = new ReminderFile { reminders = _items };
                File.WriteAllText(FilePath, JsonUtility.ToJson(file, true));
            }
            catch (Exception e)
            {
                Debug.LogWarning("[ReminderStore] 写入提醒失败: " + e.Message);
            }
        }

        public void AddOrUpdate(Reminder r)
        {
            if (r == null) return;
            if (string.IsNullOrEmpty(r.id))
                r.id = Guid.NewGuid().ToString("N");
            int idx = _items.FindIndex(x => x.id == r.id);
            if (idx >= 0) _items[idx] = r;
            else _items.Add(r);
            Save();
        }

        public void Remove(string id)
        {
            _items.RemoveAll(x => x.id == id);
            Save();
        }

        // 安全地构造日期，日子超出当月天数时夹紧（例如闰年 2/29 → 平年 2/28）。
        private static DateTime SafeDate(int year, int month, int day)
        {
            month = Mathf.Clamp(month, 1, 12);
            if (year < 1) year = DateTime.Now.Year;
            int maxDay = DateTime.DaysInMonth(year, month);
            day = Mathf.Clamp(day, 1, maxDay);
            return new DateTime(year, month, day);
        }

        /// <summary>
        /// 返回这条提醒相对 today 的“下一次目标日期”。
        /// oneoff/countdown 若已彻底过去，返回 null。
        /// </summary>
        public static DateTime? NextOccurrence(Reminder r, DateTime today)
        {
            if (r == null) return null;
            today = today.Date;
            switch (r.type)
            {
                case YEARLY:
                {
                    DateTime thisYear = SafeDate(today.Year, r.month, r.day);
                    if (thisYear.Date >= today) return thisYear;
                    return SafeDate(today.Year + 1, r.month, r.day);
                }
                case LUNAR:
                {
                    DateTime s = LunarCalendar.LunarToSolar(today.Year, r.month, r.day);
                    if (s != DateTime.MinValue && s.Date >= today) return s;
                    DateTime nxt = LunarCalendar.LunarToSolar(today.Year + 1, r.month, r.day);
                    return nxt == DateTime.MinValue ? (DateTime?)null : nxt;
                }
                case QINGMING:
                {
                    DateTime q = LunarCalendar.Qingming(today.Year);
                    return q.Date >= today ? q : LunarCalendar.Qingming(today.Year + 1);
                }
                case COUNTDOWN:
                {
                    DateTime target = SafeDate(r.year, r.month, r.day).AddDays(r.offsetDays);
                    return target.Date >= today ? target : (DateTime?)null;
                }
                case INTERVAL:
                {
                    DateTime baseDate = SafeDate(r.year, r.month, r.day);
                    int step = Mathf.Max(1, r.intervalDays);
                    if (baseDate.Date >= today) return baseDate;
                    int diff = (today - baseDate.Date).Days;
                    int k = (diff + step - 1) / step; // 向上取整到下一个间隔点
                    return baseDate.AddDays((double)k * step);
                }
                default: // ONEOFF
                {
                    DateTime target = SafeDate(r.year, r.month, r.day);
                    return target.Date >= today ? target : (DateTime?)null;
                }
            }
        }

        /// <summary>距离下一次目标还有几天；没有未来目标返回 int.MaxValue。</summary>
        public static int DaysUntil(Reminder r, DateTime today)
        {
            var next = NextOccurrence(r, today);
            if (next == null) return int.MaxValue;
            return (next.Value.Date - today.Date).Days;
        }

        private static int LeadDaysOf(Reminder r)
        {
            return r.leadDays > 0 ? r.leadDays : DefaultLeadDays;
        }

        /// <summary>今天（含提前期内）是否应该提醒，且今天还没被确认过。</summary>
        public bool ShouldRemind(Reminder r, DateTime today)
        {
            int days = DaysUntil(r, today);
            if (days < 0 || days > LeadDaysOf(r)) return false;
            return r.lastAckDate != today.ToString("yyyy-MM-dd");
        }

        /// <summary>取出今天所有到期且未确认的提醒。</summary>
        public List<Reminder> DueReminders(DateTime today)
        {
            var due = new List<Reminder>();
            foreach (var r in _items)
                if (ShouldRemind(r, today)) due.Add(r);
            return due;
        }

        public void Acknowledge(Reminder r, DateTime today)
        {
            if (r == null) return;
            r.lastAckDate = today.ToString("yyyy-MM-dd");
            Save();
        }

        // ---- 导出 / 导入（跨电脑）----

        /// <summary>导出文件放在 exe（编辑器为工程根）同级，方便玩家复制到别的机器。</summary>
        public static string ExportFilePath
        {
            get
            {
                string dir = Directory.GetParent(Application.dataPath).FullName;
                return Path.Combine(dir, "reminders_export.json");
            }
        }

        /// <summary>导出到文件，并同时复制一份到剪贴板。返回导出文件路径。</summary>
        public string Export()
        {
            string json = JsonUtility.ToJson(new ReminderFile { reminders = _items }, true);
            try { File.WriteAllText(ExportFilePath, json); }
            catch (Exception e) { Debug.LogWarning("[ReminderStore] 导出失败: " + e.Message); }
            try { GUIUtility.systemCopyBuffer = json; } catch { }
            return ExportFilePath;
        }

        private static bool LooksLikeReminders(string s)
        {
            return !string.IsNullOrEmpty(s) && s.Contains("\"reminders\"");
        }

        /// <summary>
        /// 从剪贴板（优先）或导出文件导入提醒，按 id 合并（相同 id 覆盖，新 id 追加）。
        /// 返回导入条数，-1 表示没有可导入内容。
        /// </summary>
        public int Import()
        {
            string json = null;
            try
            {
                string cb = GUIUtility.systemCopyBuffer;
                if (LooksLikeReminders(cb)) json = cb;
            }
            catch { }

            if (json == null && File.Exists(ExportFilePath))
            {
                try { json = File.ReadAllText(ExportFilePath); } catch { }
            }
            if (!LooksLikeReminders(json)) return -1;

            ReminderFile file;
            try { file = JsonUtility.FromJson<ReminderFile>(json); }
            catch { return -1; }
            if (file == null || file.reminders == null) return -1;

            int count = 0;
            foreach (var r in file.reminders)
            {
                if (r == null) continue;
                if (string.IsNullOrEmpty(r.id)) r.id = Guid.NewGuid().ToString("N");
                int idx = _items.FindIndex(x => x.id == r.id);
                if (idx >= 0) _items[idx] = r; else _items.Add(r);
                count++;
            }
            Save();
            return count;
        }

        /// <summary>某一天是否有提醒目标（供日历高亮）。</summary>
        public bool HasTargetOn(DateTime date)
        {
            date = date.Date;
            foreach (var r in _items)
            {
                if (r.type == YEARLY)
                {
                    int d = Mathf.Clamp(r.day, 1, DateTime.DaysInMonth(date.Year, Mathf.Clamp(r.month, 1, 12)));
                    if (Mathf.Clamp(r.month, 1, 12) == date.Month && d == date.Day)
                        return true;
                }
                else if (r.type == LUNAR)
                {
                    // 农历节日的公历年可能与农历年错开（如除夕），两年都比对一次。
                    if (LunarCalendar.LunarToSolar(date.Year, r.month, r.day).Date == date) return true;
                    if (LunarCalendar.LunarToSolar(date.Year - 1, r.month, r.day).Date == date) return true;
                }
                else if (r.type == QINGMING)
                {
                    if (LunarCalendar.Qingming(date.Year).Date == date) return true;
                }
                else if (r.type == INTERVAL)
                {
                    DateTime baseDate = SafeDate(r.year, r.month, r.day).Date;
                    int step = Mathf.Max(1, r.intervalDays);
                    if (date >= baseDate && (date - baseDate).Days % step == 0)
                        return true;
                }
                else
                {
                    DateTime t = (r.type == COUNTDOWN)
                        ? SafeDate(r.year, r.month, r.day).AddDays(r.offsetDays)
                        : SafeDate(r.year, r.month, r.day);
                    if (t.Date == date) return true;
                }
            }
            return false;
        }
    }

    /// <summary>
    /// 预置节日。kind：
    ///   YEARLY   固定公历月/日；
    ///   LUNAR    农历月/日（day=0 表示除夕，取当月最后一天）；
    ///   QINGMING 清明节气（month/day 忽略）。
    /// </summary>
    public class Festival
    {
        public string titleZh;
        public string titleEn;
        public string kind = ReminderStore.YEARLY;
        public int month;
        public int day;
        public string Title => GameLanguage.IsEnglish ? titleEn : titleZh;
    }

    public static class Festivals
    {
        public static readonly Festival[] All =
        {
            // ---- 公历固定 ----
            new Festival { titleZh = "元旦",   titleEn = "New Year's Day",  month = 1,  day = 1 },
            new Festival { titleZh = "情人节", titleEn = "Valentine's Day", month = 2,  day = 14 },
            new Festival { titleZh = "妇女节", titleEn = "Women's Day",     month = 3,  day = 8 },
            new Festival { titleZh = "愚人节", titleEn = "April Fools' Day",month = 4,  day = 1 },
            new Festival { titleZh = "劳动节", titleEn = "Labour Day",      month = 5,  day = 1 },
            new Festival { titleZh = "儿童节", titleEn = "Children's Day",  month = 6,  day = 1 },
            new Festival { titleZh = "建党节", titleEn = "CPC Founding Day",month = 7,  day = 1 },
            new Festival { titleZh = "建军节", titleEn = "Army Day",        month = 8,  day = 1 },
            new Festival { titleZh = "教师节", titleEn = "Teachers' Day",   month = 9,  day = 10 },
            new Festival { titleZh = "国庆节", titleEn = "National Day",    month = 10, day = 1 },
            new Festival { titleZh = "万圣夜", titleEn = "Halloween",       month = 10, day = 31 },
            new Festival { titleZh = "光棍节", titleEn = "Singles' Day",    month = 11, day = 11 },
            new Festival { titleZh = "平安夜", titleEn = "Christmas Eve",   month = 12, day = 24 },
            new Festival { titleZh = "圣诞节", titleEn = "Christmas",       month = 12, day = 25 },

            // ---- 农历 ----
            new Festival { titleZh = "除夕",   titleEn = "Lunar New Year's Eve", kind = ReminderStore.LUNAR, month = 12, day = 0 },
            new Festival { titleZh = "春节",   titleEn = "Spring Festival",      kind = ReminderStore.LUNAR, month = 1,  day = 1 },
            new Festival { titleZh = "元宵节", titleEn = "Lantern Festival",     kind = ReminderStore.LUNAR, month = 1,  day = 15 },
            new Festival { titleZh = "龙抬头", titleEn = "Dragon Head Festival", kind = ReminderStore.LUNAR, month = 2,  day = 2 },
            new Festival { titleZh = "端午节", titleEn = "Dragon Boat Festival", kind = ReminderStore.LUNAR, month = 5,  day = 5 },
            new Festival { titleZh = "七夕节", titleEn = "Qixi Festival",        kind = ReminderStore.LUNAR, month = 7,  day = 7 },
            new Festival { titleZh = "中元节", titleEn = "Ghost Festival",       kind = ReminderStore.LUNAR, month = 7,  day = 15 },
            new Festival { titleZh = "中秋节", titleEn = "Mid-Autumn Festival",  kind = ReminderStore.LUNAR, month = 8,  day = 15 },
            new Festival { titleZh = "重阳节", titleEn = "Double Ninth Festival",kind = ReminderStore.LUNAR, month = 9,  day = 9 },
            new Festival { titleZh = "腊八节", titleEn = "Laba Festival",        kind = ReminderStore.LUNAR, month = 12, day = 8 },

            // ---- 节气 ----
            new Festival { titleZh = "清明节", titleEn = "Qingming Festival",    kind = ReminderStore.QINGMING },
        };
    }
}
