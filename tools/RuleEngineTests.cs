using System;

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

            // 4. At most three notifications per cycle.
            RuleTrigger t5 = Evaluate(engine, stock, runtime, day, 6.5m, false).UpTrigger;
            Check("fourth level is capped", t5.ShouldNotify, false);

            // 5. Dropping clearly below the threshold re-arms the cycle.
            RuleTrigger t6 = Evaluate(engine, stock, runtime, day, 6.5m, false).UpTrigger;
            Check("still capped above threshold", t6.ShouldNotify, false);

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
    }
}
