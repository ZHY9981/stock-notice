using System;
using System.Collections.Generic;
using System.IO;

namespace StockNotice
{
    public class StartupCheckResult
    {
        public bool IsHealthy { get; set; }
        public string Summary { get; set; }
        public List<string> Issues { get; set; }

        public StartupCheckResult()
        {
            IsHealthy = true;
            Summary = string.Empty;
            Issues = new List<string>();
        }
    }

    public static class StartupCheck
    {
        public static StartupCheckResult Run(JsonStateStore store)
        {
            StartupCheckResult result = new StartupCheckResult();

            if (!TradingCalendar.IsAvailable)
            {
                result.Issues.Add("交易日历未加载，已回退为工作日判断");
            }
            else if (TradingCalendar.LatestDate < MarketClock.Now().Date.AddDays(30))
            {
                result.Issues.Add("交易日历即将过期，请更新 calendar.json");
            }

            if (store == null)
            {
                result.Issues.Add("数据存储未初始化");
            }
            else
            {
                CheckStorage(store.StatePath, result);
            }

            result.IsHealthy = result.Issues.Count == 0;
            result.Summary = result.IsHealthy
                ? "自检通过 · 交易日历至 " + TradingCalendar.LatestDate.ToString("yyyy-MM-dd")
                : "自检发现 " + result.Issues.Count.ToString() + " 项问题";
            return result;
        }

        private static void CheckStorage(string statePath, StartupCheckResult result)
        {
            try
            {
                string directory = Path.GetDirectoryName(statePath);
                if (string.IsNullOrEmpty(directory))
                {
                    result.Issues.Add("数据目录无效");
                    return;
                }

                Directory.CreateDirectory(directory);
                string probe = Path.Combine(directory, ".startup-check");
                File.WriteAllText(probe, "ok");
                File.Delete(probe);
            }
            catch
            {
                result.Issues.Add("数据目录不可写");
            }
        }
    }
}
