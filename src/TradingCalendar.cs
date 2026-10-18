using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Reflection;
using System.Web.Script.Serialization;

namespace StockNotice
{
    public static class TradingCalendar
    {
        private const string ResourceName = "StockNotice.trading-calendar.json";
        private static readonly HashSet<string> TradingDates = LoadTradingDates();
        private static readonly DateTime LatestTradingDate = FindLatestDate(TradingDates);

        public static bool IsAvailable
        {
            get { return TradingDates.Count > 0; }
        }

        public static DateTime LatestDate
        {
            get { return LatestTradingDate; }
        }

        public static bool IsTradingDay(DateTime date)
        {
            DateTime day = date.Date;
            if (!IsAvailable || day > LatestTradingDate)
            {
                return day.DayOfWeek != DayOfWeek.Saturday
                    && day.DayOfWeek != DayOfWeek.Sunday;
            }

            return TradingDates.Contains(day.ToString("yyyyMMdd", CultureInfo.InvariantCulture));
        }

        public static DateTime GetNextTradingDay(DateTime afterDate)
        {
            return FindTradingDay(afterDate.Date, 1);
        }

        public static DateTime GetNextTradingDayOnOrAfter(DateTime date)
        {
            return FindTradingDay(date.Date, 0);
        }

        private static DateTime FindTradingDay(DateTime start, int firstOffset)
        {
            for (int offset = firstOffset; offset <= 370; offset++)
            {
                DateTime candidate = start.AddDays(offset);
                if (IsTradingDay(candidate)) return candidate;
            }
            return DateTime.MinValue;
        }

        private static HashSet<string> LoadTradingDates()
        {
            HashSet<string> dates = new HashSet<string>(StringComparer.Ordinal);

            try
            {
                Assembly assembly = typeof(TradingCalendar).Assembly;
                using (Stream stream = assembly.GetManifestResourceStream(ResourceName))
                {
                    if (stream == null) return dates;

                    using (StreamReader reader = new StreamReader(stream))
                    {
                        JavaScriptSerializer serializer = new JavaScriptSerializer();
                        List<string> values = serializer.Deserialize<List<string>>(reader.ReadToEnd());
                        if (values == null) return dates;

                        for (int i = 0; i < values.Count; i++)
                        {
                            DateTime parsed;
                            if (DateTime.TryParseExact(
                                values[i],
                                "yyyyMMdd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out parsed))
                            {
                                dates.Add(values[i]);
                            }
                        }
                    }
                }
            }
            catch
            {
                dates.Clear();
            }

            return dates;
        }

        private static DateTime FindLatestDate(HashSet<string> dates)
        {
            DateTime latest = DateTime.MinValue;
            foreach (string value in dates)
            {
                DateTime parsed;
                if (DateTime.TryParseExact(
                    value,
                    "yyyyMMdd",
                    CultureInfo.InvariantCulture,
                    DateTimeStyles.None,
                    out parsed)
                    && parsed > latest)
                {
                    latest = parsed;
                }
            }
            return latest;
        }
    }
}
