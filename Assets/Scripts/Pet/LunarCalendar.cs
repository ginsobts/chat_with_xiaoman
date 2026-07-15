using System;

namespace VN
{
    /// <summary>
    /// 农历 ↔ 公历换算（1900-2100）以及清明节气推算。
    /// 数据表为通行的 lunarInfo 编码：低 4 位=闰月月份(0 无闰)，0x10000 位=闰月天数(30/29)，
    /// 0x8000..0x10 位分别对应正月..腊月的大小月(30/29)。基准：农历 1900-01-01 = 公历 1900-01-31。
    /// </summary>
    public static class LunarCalendar
    {
        private static readonly int[] lunarInfo =
        {
            0x04bd8,0x04ae0,0x0a570,0x054d5,0x0d260,0x0d950,0x16554,0x056a0,0x09ad0,0x055d2, //1900-1909
            0x04ae0,0x0a5b6,0x0a4d0,0x0d250,0x1d255,0x0b540,0x0d6a0,0x0ada2,0x095b0,0x14977, //1910-1919
            0x04970,0x0a4b0,0x0b4b5,0x06a50,0x06d40,0x1ab54,0x02b60,0x09570,0x052f2,0x04970, //1920-1929
            0x06566,0x0d4a0,0x0ea50,0x06e95,0x05ad0,0x02b60,0x186e3,0x092e0,0x1c8d7,0x0c950, //1930-1939
            0x0d4a0,0x1d8a6,0x0b550,0x056a0,0x1a5b4,0x025d0,0x092d0,0x0d2b2,0x0a950,0x0b557, //1940-1949
            0x06ca0,0x0b550,0x15355,0x04da0,0x0a5b0,0x14573,0x052b0,0x0a9a8,0x0e950,0x06aa0, //1950-1959
            0x0aea6,0x0ab50,0x04b60,0x0aae4,0x0a570,0x05260,0x0f263,0x0d950,0x05b57,0x056a0, //1960-1969
            0x096d0,0x04dd5,0x04ad0,0x0a4d0,0x0d4d4,0x0d250,0x0d558,0x0b540,0x0b5a0,0x195a6, //1970-1979
            0x095b0,0x049b0,0x0a974,0x0a4b0,0x0b27a,0x06a50,0x06d40,0x0af46,0x0ab60,0x09570, //1980-1989
            0x04af5,0x04970,0x064b0,0x074a3,0x0ea50,0x06b58,0x05ac0,0x0ab60,0x096d5,0x092e0, //1990-1999
            0x0c960,0x0d954,0x0d4a0,0x0da50,0x07552,0x056a0,0x0abb7,0x025d0,0x092d0,0x0cab5, //2000-2009
            0x0a950,0x0b4a0,0x0baa4,0x0ad50,0x055d9,0x04ba0,0x0a5b0,0x15176,0x052b0,0x0a930, //2010-2019
            0x07954,0x06aa0,0x0ad50,0x05b52,0x04b60,0x0a6e6,0x0a4e0,0x0d260,0x0ea65,0x0d530, //2020-2029
            0x05aa0,0x076a3,0x096d0,0x04afb,0x04ad0,0x0a4d0,0x1d0b6,0x0d250,0x0d520,0x0dd45, //2030-2039
            0x0b5a0,0x056d0,0x055b2,0x049b0,0x0a577,0x0a4b0,0x0aa50,0x1b255,0x06d20,0x0ada0, //2040-2049
            0x14b63,0x09370,0x049f8,0x04970,0x064b0,0x168a6,0x0ea50,0x06b20,0x1a6c4,0x0aae0, //2050-2059
            0x0a2e0,0x0d2e3,0x0c960,0x0d557,0x0d4a0,0x0da50,0x05d55,0x056a0,0x0a6d0,0x055d4, //2060-2069
            0x052d0,0x0a9b8,0x0a950,0x0b4a0,0x0b6a6,0x0ad50,0x055a0,0x0aba4,0x0a5b0,0x052b0, //2070-2079
            0x0b273,0x06930,0x07337,0x06aa0,0x0ad50,0x14b55,0x04b60,0x0a570,0x054e4,0x0d160, //2080-2089
            0x0e968,0x0d520,0x0daa0,0x16aa6,0x056d0,0x04ae0,0x0a9d4,0x0a2d0,0x0d150,0x0f252, //2090-2099
            0x0d520 //2100
        };

        private const int MinYear = 1900;
        private const int MaxYear = 2100;
        private static readonly DateTime Base = new DateTime(1900, 1, 31);

        private static int LeapMonth(int y) => lunarInfo[y - MinYear] & 0xf;

        private static int LeapDays(int y)
        {
            if (LeapMonth(y) == 0) return 0;
            return (lunarInfo[y - MinYear] & 0x10000) != 0 ? 30 : 29;
        }

        /// <summary>农历 y 年第 m 个（非闰）月的天数。</summary>
        private static int MonthDays(int y, int m)
        {
            return (lunarInfo[y - MinYear] & (0x10000 >> m)) != 0 ? 30 : 29;
        }

        private static int YearDays(int y)
        {
            int sum = 0;
            for (int m = 1; m <= 12; m++) sum += MonthDays(y, m);
            return sum + LeapDays(y);
        }

        /// <summary>
        /// 把农历日期转换为公历。lunarDay==0 表示该月最后一天（用于除夕：传 month=12, day=0）。
        /// 只处理常规（非闰）月的节日，闰月不参与。超出范围返回 DateTime.MinValue。
        /// </summary>
        public static DateTime LunarToSolar(int lunarYear, int lunarMonth, int lunarDay)
        {
            if (lunarYear < MinYear || lunarYear > MaxYear) return DateTime.MinValue;
            if (lunarMonth < 1 || lunarMonth > 12) return DateTime.MinValue;

            int offset = 0;
            for (int y = MinYear; y < lunarYear; y++) offset += YearDays(y);

            int leap = LeapMonth(lunarYear);
            for (int m = 1; m < lunarMonth; m++)
            {
                offset += MonthDays(lunarYear, m);
                if (leap != 0 && m == leap) offset += LeapDays(lunarYear); // 闰月排在该月之后
            }

            int dim = MonthDays(lunarYear, lunarMonth);
            int day = lunarDay <= 0 ? dim : Math.Min(lunarDay, dim);
            offset += day - 1;

            return Base.AddDays(offset);
        }

        /// <summary>清明（公历），适用于 2000-2099；其余年份用近似公式。</summary>
        public static DateTime Qingming(int year)
        {
            int y = year % 100;
            double c = year >= 2000 ? 4.81 : 5.59; // 20 世纪用 5.59
            int day = (int)(y * 0.2422 + c) - y / 4;
            if (day < 1) day = 4;
            if (day > 6) day = 5;
            return new DateTime(year, 4, day);
        }
    }
}
