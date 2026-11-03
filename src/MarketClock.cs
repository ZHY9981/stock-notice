using System;

namespace StockNotice
{
    public static class MarketClock
    {
        private static readonly TimeZoneInfo ChinaTimeZone = ResolveChinaTimeZone();

        public static readonly TimeSpan MorningOpen = new TimeSpan(9, 30, 0);
        public static readonly TimeSpan MorningClose = new TimeSpan(11, 30, 0);
        public static readonly TimeSpan AfternoonOpen = new TimeSpan(13, 0, 0);
        public static readonly TimeSpan AfternoonClose = new TimeSpan(15, 0, 0);

        private static TimeZoneInfo ResolveChinaTimeZone()
        {
            try
            {
                return TimeZoneInfo.FindSystemTimeZoneById("China Standard Time");
            }
            catch
            {
                return TimeZoneInfo.Local;
            }
        }

        public static DateTime Now()
        {
            return TimeZoneInfo.ConvertTime(DateTime.Now, TimeZoneInfo.Local, ChinaTimeZone);
        }

        public static string TodayKey()
        {
            return Now().ToString("yyyy-MM-dd");
        }

        public static bool IsTradingSession(DateTime chinaNow)
        {
            if (!TradingCalendar.IsTradingDay(chinaNow.Date))
            {
                return false;
            }

            TimeSpan time = chinaNow.TimeOfDay;
            bool morning = time >= MorningOpen && time <= MorningClose;
            bool afternoon = time >= AfternoonOpen && time < AfternoonClose;
            return morning || afternoon;
        }

        // The exchange stops pushing quotes at 15:00 sharp, so the closing
        // print needs one extra poll right after the bell.
        public static bool IsCloseCaptureWindow(DateTime chinaNow)
        {
            if (!TradingCalendar.IsTradingDay(chinaNow.Date)) return false;
            TimeSpan time = chinaNow.TimeOfDay;
            return time >= AfternoonClose && time < AfternoonClose.Add(TimeSpan.FromMinutes(2));
        }

        public static bool IsAfterClose(DateTime chinaNow)
        {
            return TradingCalendar.IsTradingDay(chinaNow.Date) && chinaNow.TimeOfDay >= AfternoonClose;
        }

        public static bool IsTradingSessionNow()
        {
            return IsTradingSession(Now());
        }

        public static string GetSessionText(DateTime chinaNow)
        {
            if (!TradingCalendar.IsTradingDay(chinaNow.Date))
            {
                if (chinaNow.DayOfWeek == DayOfWeek.Saturday || chinaNow.DayOfWeek == DayOfWeek.Sunday)
                {
                    return "周末休市";
                }
                return "节假日休市";
            }

            TimeSpan time = chinaNow.TimeOfDay;
            if (time < new TimeSpan(9, 15, 0)) return "等待开盘";
            if (time < MorningOpen) return "集合竞价";
            if (time <= MorningClose) return "上午交易中";
            if (time < AfternoonOpen) return "午间休市";
            if (time < AfternoonClose) return "下午交易中";
            return "已收盘";
        }

        public static string GetNextSessionText(DateTime chinaNow)
        {
            DateTime nextSessionDate;
            string nextSessionTime;

            if (TradingCalendar.IsTradingDay(chinaNow.Date))
            {
                TimeSpan time = chinaNow.TimeOfDay;
                if (time < MorningOpen)
                {
                    nextSessionDate = chinaNow.Date;
                    nextSessionTime = "09:30";
                }
                else if (time < AfternoonOpen)
                {
                    nextSessionDate = chinaNow.Date;
                    nextSessionTime = "13:00";
                }
                else
                {
                    nextSessionDate = TradingCalendar.GetNextTradingDay(chinaNow.Date);
                    nextSessionTime = "09:30";
                }
            }
            else
            {
                nextSessionDate = TradingCalendar.GetNextTradingDayOnOrAfter(chinaNow.Date);
                nextSessionTime = "09:30";
            }

            if (nextSessionDate == DateTime.MinValue) return "交易日历已过期";

            string dayText;
            if (nextSessionDate == chinaNow.Date) dayText = "今日";
            else if (nextSessionDate == chinaNow.Date.AddDays(1)) dayText = "明日";
            else dayText = nextSessionDate.ToString("MM-dd");

            return dayText + " " + nextSessionTime;
        }

        public static bool IsFreshTradingDate(Quote quote, DateTime chinaNow)
        {
            if (quote == null || !quote.IsValid) return false;
            if (quote.Timestamp == DateTime.MinValue) return false;

            DateTime quoteTime = TimeZoneInfo.ConvertTime(quote.Timestamp, ChinaTimeZone);
            return quoteTime.Date == chinaNow.Date;
        }

        public static bool IsQuoteFresh(Quote quote, DateTime chinaNow, TimeSpan maxAge)
        {
            if (quote == null || !quote.IsValid) return false;
            if (quote.Timestamp == DateTime.MinValue) return false;
            if (maxAge <= TimeSpan.Zero) return false;

            DateTime quoteTime = quote.Timestamp.Kind == DateTimeKind.Unspecified
                ? quote.Timestamp
                : TimeZoneInfo.ConvertTime(quote.Timestamp, ChinaTimeZone);
            DateTime now = chinaNow.Kind == DateTimeKind.Unspecified
                ? chinaNow
                : TimeZoneInfo.ConvertTime(chinaNow, ChinaTimeZone);

            TimeSpan age = now - quoteTime;
            return age >= TimeSpan.FromSeconds(-30) && age <= maxAge;
        }

        public static bool IsLikelyUnchangedOutsideSession(DateTime timestamp)
        {
            if (timestamp == DateTime.MinValue) return false;
            DateTime china = TimeZoneInfo.ConvertTime(timestamp, ChinaTimeZone);
            return !IsTradingSession(china);
        }
    }
}
