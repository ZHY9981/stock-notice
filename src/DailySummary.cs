using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;

namespace StockNotice
{
    public class DailySummaryItem
    {
        public string StockId { get; set; }
        public string Name { get; set; }
        public string Code { get; set; }
        public decimal Price { get; set; }
        public decimal ChangePercent { get; set; }
        public bool HasQuote { get; set; }
        public int TriggerCount { get; set; }
    }

    public class DailySummaryReport
    {
        public string DateKey { get; set; }
        public string Title { get; set; }
        public string Body { get; set; }
        public string ShortText { get; set; }
        public string FullText { get; set; }
        public int TriggerCount { get; set; }
        public int MissingQuoteCount { get; set; }
        public List<DailySummaryItem> Items { get; set; }

        public DailySummaryReport()
        {
            DateKey = string.Empty;
            Title = string.Empty;
            Body = string.Empty;
            ShortText = string.Empty;
            FullText = string.Empty;
            Items = new List<DailySummaryItem>();
        }
    }

    public class DailySummaryEventArgs : EventArgs
    {
        public DailySummaryReport Report { get; private set; }

        public DailySummaryEventArgs(DailySummaryReport report)
        {
            Report = report;
        }
    }

    // Builds the after-close recap. Pure logic so it can be tested without a
    // window, a timer or a network connection.
    public static class DailySummary
    {
        public static readonly TimeSpan WindowStart = new TimeSpan(15, 5, 0);
        public static readonly TimeSpan WindowEnd = new TimeSpan(17, 0, 0);

        public const int MaxBodyItems = 4;

        public static bool IsSummaryWindow(DateTime chinaNow)
        {
            if (!TradingCalendar.IsTradingDay(chinaNow.Date)) return false;
            TimeSpan time = chinaNow.TimeOfDay;
            return time >= WindowStart && time < WindowEnd;
        }

        public static DailySummaryReport Build(
            DateTime chinaNow,
            IList<StockConfig> stocks,
            IDictionary<string, Quote> quotes,
            IList<TriggerRecord> history)
        {
            if (stocks == null) throw new ArgumentNullException("stocks");
            if (quotes == null) throw new ArgumentNullException("quotes");

            DailySummaryReport report = new DailySummaryReport();
            report.DateKey = chinaNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            report.Title = "收盘汇总 · " + chinaNow.ToString("MM-dd", CultureInfo.InvariantCulture);

            Dictionary<string, int> triggersByStock = CountTriggers(chinaNow, history);
            foreach (KeyValuePair<string, int> pair in triggersByStock)
            {
                report.TriggerCount += pair.Value;
            }

            for (int i = 0; i < stocks.Count; i++)
            {
                StockConfig stock = stocks[i];
                if (stock == null || !stock.Enabled || string.IsNullOrEmpty(stock.Id)) continue;

                DailySummaryItem item = new DailySummaryItem();
                item.StockId = stock.Id;
                item.Code = stock.Code;
                item.Name = string.IsNullOrEmpty(stock.Name) ? stock.Code : stock.Name;

                int triggerCount;
                if (triggersByStock.TryGetValue(stock.Id, out triggerCount)) item.TriggerCount = triggerCount;

                Quote quote;
                if (quotes.TryGetValue(stock.Id, out quote)
                    && quote != null
                    && quote.IsValid
                    && quote.Price > 0m)
                {
                    decimal basePrice = ResolveBasePrice(stock, quote, chinaNow);
                    item.HasQuote = true;
                    item.Price = quote.Price;
                    item.ChangePercent = basePrice > 0m
                        ? (quote.Price / basePrice - 1m) * 100m
                        : 0m;
                }
                else
                {
                    report.MissingQuoteCount++;
                }

                report.Items.Add(item);
            }

            if (report.Items.Count == 0 || CountQuoted(report.Items) == 0) return null;

            report.Items.Sort(CompareItems);
            report.Body = BuildBody(report);
            report.FullText = BuildFullText(report, chinaNow);
            report.ShortText = BuildShortText(report);
            return report;
        }

        public static string FormatPercent(decimal value)
        {
            string text = value.ToString("0.00", CultureInfo.InvariantCulture);
            return value > 0m ? "+" + text : text;
        }

        private static Dictionary<string, int> CountTriggers(DateTime chinaNow, IList<TriggerRecord> history)
        {
            Dictionary<string, int> counts = new Dictionary<string, int>(StringComparer.Ordinal);
            if (history == null) return counts;

            for (int i = 0; i < history.Count; i++)
            {
                TriggerRecord record = history[i];
                if (record == null || record.TriggeredAt.Date != chinaNow.Date) continue;
                if (string.IsNullOrEmpty(record.StockId)) continue;

                int current;
                counts.TryGetValue(record.StockId, out current);
                counts[record.StockId] = current + 1;
            }
            return counts;
        }

        private static decimal ResolveBasePrice(StockConfig stock, Quote quote, DateTime chinaNow)
        {
            string today = chinaNow.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
            bool customToday = stock.CustomBaseEnabled
                && stock.CustomBasePrice > 0m
                && string.Equals(stock.CustomBaseDate, today, StringComparison.Ordinal);
            if (customToday) return stock.CustomBasePrice;
            if (quote.PreviousClose > 0m) return quote.PreviousClose;
            return quote.Price;
        }

        private static int CompareItems(DailySummaryItem left, DailySummaryItem right)
        {
            if (left.HasQuote != right.HasQuote) return left.HasQuote ? -1 : 1;
            return right.ChangePercent.CompareTo(left.ChangePercent);
        }

        private static string BuildBody(DailySummaryReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("监控 ").Append(CountText(report.Items.Count))
                .Append(" 只 · 今日触发 ").Append(CountText(report.TriggerCount)).Append(" 次");

            int listed = 0;
            for (int i = 0; i < report.Items.Count && listed < MaxBodyItems; i++)
            {
                DailySummaryItem item = report.Items[i];
                if (!item.HasQuote) continue;
                builder.Append('\n')
                    .Append(item.Name).Append(' ')
                    .Append(FormatPercent(item.ChangePercent)).Append('%');
                listed++;
            }

            int remaining = CountQuoted(report.Items) - listed;
            if (remaining > 0)
            {
                builder.Append("\n…另 ").Append(CountText(remaining)).Append(" 只");
            }
            return builder.ToString();
        }

        private static string BuildShortText(DailySummaryReport report)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append("监控 ").Append(CountText(report.Items.Count))
                .Append(" 只 · 触发 ").Append(CountText(report.TriggerCount)).Append(" 次");

            for (int i = 0; i < report.Items.Count; i++)
            {
                if (!report.Items[i].HasQuote) continue;
                builder.Append(" · ").Append(report.Items[i].Name).Append(' ')
                    .Append(FormatPercent(report.Items[i].ChangePercent)).Append('%');
                break;
            }
            return builder.ToString();
        }

        private static string BuildFullText(DailySummaryReport report, DateTime chinaNow)
        {
            StringBuilder builder = new StringBuilder();
            builder.Append(chinaNow.ToString("MM-dd", CultureInfo.InvariantCulture)).Append(" 收盘汇总")
                .Append("\r\n监控 ").Append(CountText(report.Items.Count))
                .Append(" 只 · 今日触发 ").Append(CountText(report.TriggerCount)).Append(" 次");
            if (report.MissingQuoteCount > 0)
            {
                builder.Append(" · ").Append(CountText(report.MissingQuoteCount)).Append(" 只无行情");
            }

            for (int i = 0; i < report.Items.Count; i++)
            {
                DailySummaryItem item = report.Items[i];
                builder.Append("\r\n").Append(item.Name).Append(' ');
                if (!item.HasQuote)
                {
                    builder.Append("无行情");
                }
                else
                {
                    builder.Append(FormatPercent(item.ChangePercent)).Append('%')
                        .Append(' ').Append(item.Price.ToString("0.00", CultureInfo.InvariantCulture));
                }

                if (item.TriggerCount > 0)
                {
                    builder.Append(" · 触发 ").Append(CountText(item.TriggerCount)).Append(" 次");
                }
            }
            return builder.ToString();
        }

        private static int CountQuoted(List<DailySummaryItem> items)
        {
            int count = 0;
            for (int i = 0; i < items.Count; i++)
            {
                if (items[i].HasQuote) count++;
            }
            return count;
        }

        private static string CountText(int value)
        {
            return value.ToString(CultureInfo.InvariantCulture);
        }
    }
}
