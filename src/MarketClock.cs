using System;

namespace StockNotice
{
    public static class MarketClock
    {
        private static readonly TimeZoneInfo ChinaTimeZone = ResolveChinaTimeZone();

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
            if (chinaNow.DayOfWeek == DayOfWeek.Saturday || chinaNow.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            int minutes = chinaNow.Hour * 60 + chinaNow.Minute;
            bool morning = minutes >= 9 * 60 + 30 && minutes <= 11 * 60 + 30;
            bool afternoon = minutes >= 13 * 60 && minutes <= 15 * 60;
            return morning || afternoon;
        }

        public static bool IsTradingSessionNow()
        {
            return IsTradingSession(Now());
        }

        public static string GetSessionText(DateTime chinaNow)
        {
            if (chinaNow.DayOfWeek == DayOfWeek.Saturday || chinaNow.DayOfWeek == DayOfWeek.Sunday)
            {
                return "周末休市";
            }

            int minutes = chinaNow.Hour * 60 + chinaNow.Minute;
            if (minutes < 9 * 60 + 15) return "等待开盘";
            if (minutes < 9 * 60 + 30) return "集合竞价";
            if (minutes <= 11 * 60 + 30) return "上午交易中";
            if (minutes < 13 * 60) return "午间休市";
            if (minutes <= 15 * 60) return "下午交易中";
            return "已收盘";
        }

        public static string GetNextSessionText(DateTime chinaNow)
        {
            if (chinaNow.DayOfWeek == DayOfWeek.Saturday)
            {
                return "下周一 09:30";
            }
            if (chinaNow.DayOfWeek == DayOfWeek.Sunday)
            {
                return "明日 09:30";
            }

            int minutes = chinaNow.Hour * 60 + chinaNow.Minute;
            if (minutes < 9 * 60 + 30) return "今日 09:30";
            if (minutes <= 11 * 60 + 30) return "今日 13:00";
            if (minutes < 13 * 60) return "今日 13:00";
            return "下一交易日 09:30";
        }

        public static bool IsFreshTradingDate(Quote quote, DateTime chinaNow)
        {
            if (quote == null || !quote.IsValid) return false;
            if (quote.Timestamp == DateTime.MinValue) return true;

            DateTime quoteTime = TimeZoneInfo.ConvertTime(quote.Timestamp, ChinaTimeZone);
            return quoteTime.Date == chinaNow.Date;
        }

        public static bool IsLikelyUnchangedOutsideSession(DateTime timestamp)
        {
            if (timestamp == DateTime.MinValue) return false;
            DateTime china = TimeZoneInfo.ConvertTime(timestamp, ChinaTimeZone);
            return !IsTradingSession(china);
        }
    }
}
