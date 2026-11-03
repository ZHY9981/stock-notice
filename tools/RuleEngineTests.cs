using System;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace StockNotice.Tests
{
    // Console checks for the notification state machine.
    // Build: see tools\run-tests.ps1
    internal static class RuleEngineTests
    {
        private static int _failures;

        private static void Main()
        {
            RuleEngine engine = new RuleEngine();
            DateTime day = new DateTime(2026, 10, 13, 10, 0, 0);

            StockConfig stock = new StockConfig();
            stock.Code = "600519";
            stock.Name = "测试股票";
            stock.UpThreshold = 3.0m;
            stock.DownThreshold = 3.0m;
            stock.ResetMargin = 0.5m;

            StockRuntimeState runtime = new StockRuntimeState();

            // 1. First crossing of the up threshold notifies once.
            RuleTrigger t1 = Evaluate(engine, stock, runtime, day, 3.2m, false).UpTrigger;
            Check("first crossing notifies", t1.ShouldNotify, true);
            Check("first crossing reason", t1.Reason, "threshold");
            Check("first crossing count", t1.NotificationNumber, 1);

            // 2. Same level on the next poll stays quiet.
            RuleTrigger t2 = Evaluate(engine, stock, runtime, day, 3.2m, false).UpTrigger;
            Check("same level is quiet", t2.ShouldNotify, false);

            // 3. Each further full 1% notifies again.
            RuleTrigger t3 = Evaluate(engine, stock, runtime, day, 4.3m, false).UpTrigger;
            Check("+1% step notifies", t3.ShouldNotify, true);
            Check("+1% step count", t3.NotificationNumber, 2);

            RuleTrigger t4 = Evaluate(engine, stock, runtime, day, 5.4m, false).UpTrigger;
            Check("second step notifies", t4.ShouldNotify, true);
            Check("second step count", t4.NotificationNumber, 3);

            // 4. A new 1% level refreshes the burst allowance, so a stock that
            //    keeps climbing keeps notifying instead of going silent.
            RuleTrigger t5 = Evaluate(engine, stock, runtime, day, 6.5m, false).UpTrigger;
            Check("fourth level refreshes allowance", t5.ShouldNotify, true);
            Check("fourth level keeps counting", t5.NotificationNumber, 4);

            // 5. Repeating the same level stays quiet, the next level notifies again.
            RuleTrigger t6 = Evaluate(engine, stock, runtime, day, 6.5m, false).UpTrigger;
            Check("same level stays quiet", t6.ShouldNotify, false);

            RuleTrigger t6b = Evaluate(engine, stock, runtime, day, 7.6m, false).UpTrigger;
            Check("fifth level notifies", t6b.ShouldNotify, true);
            Check("fifth level count continues", t6b.NotificationNumber, 5);

            // 6. Dropping clearly below the threshold re-arms the cycle.

            RuleTrigger idle = Evaluate(engine, stock, runtime, day, 2.0m, false).UpTrigger;
            Check("drop below reset margin is quiet", idle.ShouldNotify, false);
            Check("cycle re-armed after drop", runtime.Up.Armed, true);
            Check("counter cleared after drop", runtime.Up.NotificationCount, 0);

            RuleTrigger t7 = Evaluate(engine, stock, runtime, day, 3.2m, false).UpTrigger;
            Check("re-trigger after reset", t7.ShouldNotify, true);
            Check("re-trigger count resets to 1", t7.NotificationNumber, 1);

            // 6. Down moves use the same rules on the opposite side.
            StockRuntimeState downRuntime = new StockRuntimeState();
            RuleTrigger d1 = Evaluate(engine, stock, downRuntime, day, -3.4m, false).DownTrigger;
            Check("down crossing notifies", d1.ShouldNotify, true);
            Check("down direction label", d1.Direction, "down");
            Check("down percent sign", d1.ChangePercent < 0m, true);
            Check("up side stays quiet", Evaluate(engine, stock, downRuntime, day, -3.4m, false).UpTrigger.ShouldNotify, false);

            // 7. Startup evaluation reports an already-open threshold once.
            StockRuntimeState startupRuntime = new StockRuntimeState();
            startupRuntime.Up.Armed = false;
            startupRuntime.Up.LevelCursor = 2;
            startupRuntime.Up.NotificationCount = 3;
            RuleTrigger s1 = Evaluate(engine, stock, startupRuntime, day, 5.5m, true).UpTrigger;
            Check("startup notifies once", s1.ShouldNotify, true);
            Check("startup reason", s1.Reason, "startup");
            RuleTrigger s2 = Evaluate(engine, stock, startupRuntime, day, 5.6m, true).UpTrigger;
            Check("startup does not repeat", s2.ShouldNotify, false);

            // 8. A custom base price replaces yesterday's close for the day.
            StockConfig custom = new StockConfig();
            custom.Code = "000001";
            custom.UpThreshold = 3.0m;
            custom.DownThreshold = 3.0m;
            custom.ResetMargin = 0.5m;
            custom.CustomBaseEnabled = true;
            custom.CustomBasePrice = 10.0m;
            custom.CustomBaseDate = day.ToString("yyyy-MM-dd");
            StockRuntimeState customRuntime = new StockRuntimeState();
            RuleEvaluation customEval = EvaluateAtPrice(engine, custom, customRuntime, day, 10.4m, 11.0m, false);
            Check("custom base price used", customEval.BasePrice, 10.0m);
            Check("custom base percent", customEval.ChangePercent, 4.0m);
            Check("custom base notifies", customEval.UpTrigger.ShouldNotify, true);

            // 9. AKShare single-stock fields map to the shared quote model.
            string akShareJson = "{\"rc\":0,\"data\":{\"f43\":1329.0,\"f57\":\"600519\","
                + "\"f58\":\"贵州茅台\",\"f60\":1312.97,\"f86\":1792138307,\"f170\":1.22}}";
            Quote akShareQuote = AkShareQuoteParser.Parse(
                "600519",
                MarketNames.Shanghai,
                "600519",
                akShareJson);
            Check("akshare quote valid", akShareQuote.IsValid, true);
            Check("akshare quote code", akShareQuote.Code, "600519");
            Check("akshare quote name", akShareQuote.Name, "贵州茅台");
            Check("akshare quote price", akShareQuote.Price, 1329.0m);
            Check("akshare quote previous close", akShareQuote.PreviousClose, 1312.97m);
            Check("akshare quote percent", akShareQuote.ChangePercent, 1.22m);
            Check("akshare quote timestamp", akShareQuote.Timestamp != DateTime.MinValue, true);

            Quote emptyAkShareQuote = AkShareQuoteParser.Parse(
                "000001",
                MarketNames.Shenzhen,
                "平安银行",
                "{\"rc\":0,\"data\":null}");
            Check("akshare empty data invalid", emptyAkShareQuote.IsValid, false);
            Check("akshare empty data error", emptyAkShareQuote.Error, "AKShare 接口没有有效行情");

            // 10. The embedded A-share calendar excludes exchange holidays.
            Check("trading calendar loaded", TradingCalendar.IsAvailable, true);
            Check(
                "trading calendar covers 2026",
                TradingCalendar.LatestDate >= new DateTime(2026, 12, 31),
                true);
            Check(
                "national day is closed",
                TradingCalendar.IsTradingDay(new DateTime(2026, 10, 1)),
                false);
            Check(
                "first day after national day is open",
                TradingCalendar.IsTradingDay(new DateTime(2026, 10, 8)),
                true);
            Check(
                "weekend is closed",
                TradingCalendar.IsTradingDay(new DateTime(2026, 10, 18)),
                false);
            Check(
                "expired calendar falls back to weekdays",
                TradingCalendar.IsTradingDay(TradingCalendar.LatestDate.AddDays(1)),
                true);
            Check(
                "next trading day skips holiday",
                TradingCalendar.GetNextTradingDay(new DateTime(2026, 9, 30, 16, 0, 0)),
                new DateTime(2026, 10, 8));
            Check(
                "holiday is not a trading session",
                MarketClock.IsTradingSession(new DateTime(2026, 10, 1, 10, 0, 0)),
                false);

            Quote freshQuote = new Quote();
            freshQuote.IsValid = true;
            freshQuote.Timestamp = new DateTime(2026, 10, 13, 10, 0, 0).AddSeconds(-10);
            Check(
                "recent quote is fresh",
                MarketClock.IsQuoteFresh(
                    freshQuote,
                    new DateTime(2026, 10, 13, 10, 0, 0),
                    TimeSpan.FromSeconds(30)),
                true);
            freshQuote.Timestamp = new DateTime(2026, 10, 13, 10, 0, 0).AddSeconds(-40);
            Check(
                "old quote is stale",
                MarketClock.IsQuoteFresh(
                    freshQuote,
                    new DateTime(2026, 10, 13, 10, 0, 0),
                    TimeSpan.FromSeconds(30)),
                false);
            freshQuote.Timestamp = DateTime.MinValue;
            Check(
                "quote without timestamp is not fresh",
                MarketClock.IsQuoteFresh(
                    freshQuote,
                    new DateTime(2026, 10, 13, 10, 0, 0),
                    TimeSpan.FromSeconds(30)),
                false);

            // 11. Alert delivery obeys cooldown and quiet hours.
            AppSettings deliverySettings = new AppSettings();
            deliverySettings.AlertCooldownSeconds = 120;
            deliverySettings.QuietHoursEnabled = false;

            TriggerRecord deliveryRecord = new TriggerRecord();
            deliveryRecord.StockId = "stock-1";
            deliveryRecord.Direction = "up";

            AlertDeliveryDecision cooldownDecision = AlertPolicy.Evaluate(
                deliveryRecord,
                deliverySettings,
                new DateTime(2026, 10, 13, 10, 0, 30),
                new DateTime(2026, 10, 13, 10, 1, 0));
            Check("cooldown suppresses repeat", cooldownDecision.ShouldNotify, false);
            Check("cooldown reason", cooldownDecision.SuppressionReason, "cooldown");

            AlertDeliveryDecision cooledDownDecision = AlertPolicy.Evaluate(
                deliveryRecord,
                deliverySettings,
                new DateTime(2026, 10, 13, 9, 58, 59),
                new DateTime(2026, 10, 13, 10, 1, 0));
            Check("expired cooldown allows notification", cooledDownDecision.ShouldNotify, true);

            deliverySettings.QuietHoursEnabled = true;
            deliverySettings.QuietHoursStart = "22:00";
            deliverySettings.QuietHoursEnd = "08:00";
            Check(
                "quiet hours suppress late night",
                AlertPolicy.IsQuietTime(deliverySettings, new DateTime(2026, 10, 13, 23, 0, 0)),
                true);
            Check(
                "quiet hours suppress early morning",
                AlertPolicy.IsQuietTime(deliverySettings, new DateTime(2026, 10, 13, 7, 0, 0)),
                true);
            Check(
                "quiet hours allow daytime",
                AlertPolicy.IsQuietTime(deliverySettings, new DateTime(2026, 10, 13, 12, 0, 0)),
                false);

            deliverySettings.QuietHoursStart = "11:30";
            deliverySettings.QuietHoursEnd = "13:00";
            Check(
                "same-day quiet hours suppress lunch",
                AlertPolicy.IsQuietTime(deliverySettings, new DateTime(2026, 10, 13, 12, 0, 0)),
                true);
            Check(
                "same-day quiet hours end at boundary",
                AlertPolicy.IsQuietTime(deliverySettings, new DateTime(2026, 10, 13, 13, 1, 0)),
                false);

            // 12. The quote router falls through failing and invalid providers.
            StockConfig routedStock = new StockConfig();
            routedStock.Code = "600519";
            routedStock.Market = MarketNames.Shanghai;
            List<StockConfig> routedStocks = new List<StockConfig>();
            routedStocks.Add(routedStock);

            FakeQuoteProvider failedProvider = new FakeQuoteProvider();
            failedProvider.Name = "failed";
            failedProvider.Result = new ProviderResult();
            failedProvider.Result.ProviderName = "failed";
            failedProvider.Result.Error = "network down";

            FakeQuoteProvider invalidProvider = new FakeQuoteProvider();
            invalidProvider.Name = "invalid";
            invalidProvider.Result = new ProviderResult();
            invalidProvider.Result.ProviderName = "invalid";
            Quote invalidQuote = new Quote();
            invalidQuote.Code = routedStock.Code;
            invalidQuote.Market = routedStock.Market;
            invalidQuote.IsValid = false;
            invalidQuote.Error = "no trade";
            invalidProvider.Result.Quotes.Add(invalidQuote);

            FakeQuoteProvider workingProvider = new FakeQuoteProvider();
            workingProvider.Name = "working";
            workingProvider.Result = new ProviderResult();
            workingProvider.Result.ProviderName = "working";
            Quote workingQuote = new Quote();
            workingQuote.Code = routedStock.Code;
            workingQuote.Market = routedStock.Market;
            workingQuote.Name = "贵州茅台";
            workingQuote.Price = 1329m;
            workingQuote.PreviousClose = 1312.97m;
            workingQuote.ChangePercent = 1.22m;
            workingQuote.Timestamp = day;
            workingQuote.IsValid = true;
            workingProvider.Result.Quotes.Add(workingQuote);

            List<IMarketQuoteProvider> providers = new List<IMarketQuoteProvider>();
            providers.Add(failedProvider);
            providers.Add(invalidProvider);
            providers.Add(workingProvider);
            MarketDataService routedService = new MarketDataService(providers);
            ProviderResult routedResult = routedService.GetQuotesAsync(routedStocks)
                .GetAwaiter()
                .GetResult();
            Check("quote router uses next provider", routedResult.ProviderName, "working");
            Check("quote router returns one quote", routedResult.Quotes.Count, 1);
            Check("quote router records provider", routedService.LastProviderName, "working");

            // 13. Legacy settings and history normalize without losing compatibility.
            AppSettings legacySettings = new AppSettings();
            legacySettings.AlertCooldownSeconds = -1;
            legacySettings.StaleQuoteSeconds = 1;
            legacySettings.QuietHoursStart = "bad";
            legacySettings.QuietHoursEnd = "25:00";
            legacySettings.Normalize();
            Check("cooldown normalizes", legacySettings.AlertCooldownSeconds, 0);
            Check("stale threshold normalizes", legacySettings.StaleQuoteSeconds, 5);
            Check("quiet start falls back", legacySettings.QuietHoursStart, "22:00");
            Check("quiet end falls back", legacySettings.QuietHoursEnd, "08:00");

            AppState legacyState = new AppState();
            TriggerRecord legacyRecord = new TriggerRecord();
            legacyRecord.SuppressedReason = null;
            legacyState.History.Add(legacyRecord);
            legacyState.Normalize();
            Check("legacy history normalizes", legacyState.History[0].SuppressedReason, string.Empty);

            RunSessionChecks();
            RunDailySummaryChecks();
            RunQuoteHistoryChecks();

            Console.WriteLine();
            if (_failures == 0)
            {
                Console.WriteLine("All rule-engine checks passed.");
            }
            else
            {
                Console.WriteLine(_failures + " check(s) failed.");
                Environment.ExitCode = 1;
            }
        }

        private static RuleEvaluation Evaluate(
            RuleEngine engine,
            StockConfig stock,
            StockRuntimeState runtime,
            DateTime day,
            decimal changePercent,
            bool startup)
        {
            return Evaluate(engine, stock, runtime, day, changePercent, startup, 100m);
        }

        private static RuleEvaluation Evaluate(
            RuleEngine engine,
            StockConfig stock,
            StockRuntimeState runtime,
            DateTime day,
            decimal changePercent,
            bool startup,
            decimal previousClose)
        {
            decimal price = decimal.Round(previousClose * (1m + changePercent / 100m), 4);
            return EvaluateAtPrice(engine, stock, runtime, day, price, previousClose, startup);
        }

        private static RuleEvaluation EvaluateAtPrice(
            RuleEngine engine,
            StockConfig stock,
            StockRuntimeState runtime,
            DateTime day,
            decimal price,
            decimal previousClose,
            bool startup)
        {
            Quote quote = new Quote();
            quote.Code = stock.Code;
            quote.Market = stock.Market;
            quote.Name = stock.Name;
            quote.PreviousClose = previousClose;
            quote.Price = price;
            quote.ChangePercent = previousClose > 0m ? (price / previousClose - 1m) * 100m : 0m;
            quote.IsValid = true;
            quote.Timestamp = day;
            return engine.Evaluate(stock, quote, runtime, day, startup);
        }

        private static void RunSessionChecks()
        {
            DateTime day = new DateTime(2026, 11, 3);
            Check("session opens at 09:30", MarketClock.IsTradingSession(day.AddHours(9).AddMinutes(30)), true);
            Check("session closed at 09:29", MarketClock.IsTradingSession(day.AddHours(9).AddMinutes(29)), false);
            Check("morning close print counted", MarketClock.IsTradingSession(day.AddHours(11).AddMinutes(30)), true);
            Check("after morning close excluded", MarketClock.IsTradingSession(day.AddHours(11).AddMinutes(30).AddSeconds(1)), false);
            Check("lunch break excluded", MarketClock.IsTradingSession(day.AddHours(12)), false);
            Check("afternoon opens at 13:00", MarketClock.IsTradingSession(day.AddHours(13)), true);
            Check("14:59:59 still trading", MarketClock.IsTradingSession(day.AddHours(15).AddSeconds(-1)), true);
            Check("15:00 excluded from session", MarketClock.IsTradingSession(day.AddHours(15)), false);
            Check("close capture at 15:00:30", MarketClock.IsCloseCaptureWindow(day.AddHours(15).AddSeconds(30)), true);
            Check("close capture ends at 15:02", MarketClock.IsCloseCaptureWindow(day.AddHours(15).AddMinutes(2)), false);
            Check("session text after close", MarketClock.GetSessionText(day.AddHours(15).AddSeconds(1)), "已收盘");
            Check("session text at 14:59", MarketClock.GetSessionText(day.AddHours(15).AddSeconds(-1)), "下午交易中");
            Check("weekend is not a session", MarketClock.IsTradingSession(new DateTime(2026, 11, 7, 10, 0, 0)), false);
        }

        private static void RunDailySummaryChecks()
        {
            DateTime day = new DateTime(2026, 11, 3, 15, 10, 0);

            Check("summary window at 15:04", DailySummary.IsSummaryWindow(new DateTime(2026, 11, 3, 15, 4, 59)), false);
            Check("summary window at 15:05", DailySummary.IsSummaryWindow(new DateTime(2026, 11, 3, 15, 5, 0)), true);
            Check("summary window at 17:00", DailySummary.IsSummaryWindow(new DateTime(2026, 11, 3, 17, 0, 0)), false);
            Check("summary window on weekend", DailySummary.IsSummaryWindow(new DateTime(2026, 11, 7, 15, 10, 0)), false);

            List<StockConfig> stocks = new List<StockConfig>();
            stocks.Add(SummaryStock("a", "600150", "中国船舶"));
            stocks.Add(SummaryStock("b", "600276", "恒瑞医药"));
            stocks.Add(SummaryStock("c", "002276", "万马股份"));

            Dictionary<string, Quote> quotes = new Dictionary<string, Quote>();
            quotes["a"] = SummaryQuote("600150", 103m, 100m);
            quotes["b"] = SummaryQuote("600276", 97m, 100m);

            List<TriggerRecord> history = new List<TriggerRecord>();
            history.Add(SummaryTrigger("a", day.AddHours(-2)));
            history.Add(SummaryTrigger("a", day.AddHours(-1)));
            history.Add(SummaryTrigger("c", day.AddDays(-1)));

            DailySummaryReport report = DailySummary.Build(day, stocks, quotes, history);
            Check("summary is built", report != null, true);
            Check("summary counts today triggers only", report.TriggerCount, 2);
            Check("summary lists enabled stocks", report.Items.Count, 3);
            Check("summary reports missing quote", report.MissingQuoteCount, 1);
            Check("summary sorts strongest first", report.Items[0].Code, "600150");
            Check("summary keeps missing quote last", report.Items[2].Code, "002276");
            Check("summary title has date", report.Title, "收盘汇总 · 11-03");
            Check("summary body mentions triggers", report.Body.Contains("今日触发 2 次"), true);
            Check("summary body lists gainer", report.Body.Contains("中国船舶 +3.00%"), true);
            Check("summary body lists loser", report.Body.Contains("恒瑞医药 -3.00%"), true);
            Check("summary full text keeps price", report.FullText.Contains("中国船舶 +3.00% 103.00"), true);
            Check("summary tags triggered stock", report.FullText.Contains("中国船舶 +3.00% 103.00 · 触发 2 次"), true);
            Check("summary short text has leader", report.ShortText.Contains("中国船舶 +3.00%"), true);

            // More than four quoted stocks collapse into "another N" line.
            List<StockConfig> manyStocks = new List<StockConfig>();
            Dictionary<string, Quote> manyQuotes = new Dictionary<string, Quote>();
            for (int i = 0; i < 6; i++)
            {
                string id = "s" + i.ToString();
                manyStocks.Add(SummaryStock(id, "60000" + i.ToString(), "股票" + i.ToString()));
                manyQuotes[id] = SummaryQuote("60000" + i.ToString(), 100m + i, 100m);
            }
            DailySummaryReport manyReport = DailySummary.Build(day, manyStocks, manyQuotes, new List<TriggerRecord>());
            Check("summary caps body list", manyReport.Body.Contains("…另 2 只"), true);

            // No quote at all means there is nothing worth notifying.
            DailySummaryReport emptyReport = DailySummary.Build(day, manyStocks, new Dictionary<string, Quote>(), new List<TriggerRecord>());
            Check("summary needs at least one quote", emptyReport == null, true);
        }

        private static void RunQuoteHistoryChecks()
        {
            string directory = System.IO.Path.Combine(
                System.IO.Path.GetTempPath(),
                "stocknotice-history-" + Guid.NewGuid().ToString("N"));
            System.IO.Directory.CreateDirectory(directory);
            try
            {
                QuoteHistoryStore store = new QuoteHistoryStore(directory);

                Dictionary<string, List<Quote>> history = new Dictionary<string, List<Quote>>();
                List<Quote> samples = new List<Quote>();
                Quote quote = new Quote();
                quote.Price = 10.5m;
                quote.Timestamp = new DateTime(2026, 11, 4, 10, 0, 0);
                quote.IsValid = true;
                samples.Add(quote);
                history["a"] = samples;

                store.Save("2026-11-04", history);

                Dictionary<string, List<Quote>> loaded;
                Check("history round trip", store.TryLoad("2026-11-04", out loaded), true);
                Check("history keeps sample", loaded["a"][0].Price, 10.5m);
                Check("history keeps one series", loaded.Count, 1);
                Check("history rejects another day", store.TryLoad("2026-11-05", out loaded), false);

                System.IO.File.WriteAllText(store.Path, "not json");
                Check("history ignores corrupt file", store.TryLoad("2026-11-04", out loaded), false);
            }
            finally
            {
                try { System.IO.Directory.Delete(directory, true); } catch { }
            }
        }

        private static StockConfig SummaryStock(string id, string code, string name)
        {
            StockConfig stock = new StockConfig();
            stock.Id = id;
            stock.Code = code;
            stock.Name = name;
            stock.Enabled = true;
            return stock;
        }

        private static Quote SummaryQuote(string code, decimal price, decimal previousClose)
        {
            Quote quote = new Quote();
            quote.Code = code;
            quote.Market = MarketNames.Shanghai;
            quote.Price = price;
            quote.PreviousClose = previousClose;
            quote.IsValid = true;
            quote.Timestamp = DateTime.Now;
            return quote;
        }

        private static TriggerRecord SummaryTrigger(string stockId, DateTime triggeredAt)
        {
            TriggerRecord record = new TriggerRecord();
            record.StockId = stockId;
            record.TriggeredAt = triggeredAt;
            return record;
        }

        private static void Check(string label, bool actual, bool expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("PASS  " + label);
            }
            else
            {
                _failures++;
                Console.WriteLine("FAIL  " + label + " (expected " + expected + ", got " + actual + ")");
            }
        }

        private static void Check(string label, int actual, int expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("PASS  " + label);
            }
            else
            {
                _failures++;
                Console.WriteLine("FAIL  " + label + " (expected " + expected + ", got " + actual + ")");
            }
        }

        private static void Check(string label, string actual, string expected)
        {
            if (string.Equals(actual, expected, StringComparison.Ordinal))
            {
                Console.WriteLine("PASS  " + label);
            }
            else
            {
                _failures++;
                Console.WriteLine("FAIL  " + label + " (expected '" + expected + "', got '" + actual + "')");
            }
        }

        private static void Check(string label, decimal actual, decimal expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("PASS  " + label);
            }
            else
            {
                _failures++;
                Console.WriteLine("FAIL  " + label + " (expected " + expected + ", got " + actual + ")");
            }
        }

        private static void Check(string label, DateTime actual, DateTime expected)
        {
            if (actual == expected)
            {
                Console.WriteLine("PASS  " + label);
            }
            else
            {
                _failures++;
                Console.WriteLine(
                    "FAIL  " + label
                    + " (expected " + expected.ToString("yyyy-MM-dd HH:mm:ss")
                    + ", got " + actual.ToString("yyyy-MM-dd HH:mm:ss") + ")");
            }
        }

        private sealed class FakeQuoteProvider : IMarketQuoteProvider
        {
            public string Name { get; set; }
            public ProviderResult Result { get; set; }

            public Task<ProviderResult> GetQuotesAsync(List<StockConfig> stocks)
            {
                return Task.FromResult(Result);
            }
        }
    }
}
