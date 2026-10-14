using System;

namespace StockNotice
{
    public class RuleEngine
    {
        private const decimal LevelStep = 1.0m;
        private const decimal ComparisonEpsilon = 0.000001m;

        public RuleEvaluation Evaluate(StockConfig stock, Quote quote, StockRuntimeState runtime, DateTime chinaNow, bool startupEvaluation)
        {
            if (stock == null) throw new ArgumentNullException("stock");
            if (quote == null) throw new ArgumentNullException("quote");
            if (runtime == null) throw new ArgumentNullException("runtime");

            decimal basePrice = ResolveBasePrice(stock, quote, chinaNow);
            decimal changePercent = 0m;
            if (basePrice > 0m && quote.Price > 0m)
            {
                changePercent = (quote.Price / basePrice - 1.0m) * 100.0m;
            }

            RuleEvaluation evaluation = new RuleEvaluation();
            evaluation.BasePrice = basePrice;
            evaluation.ChangePercent = changePercent;
            evaluation.UpTrigger = EvaluateDirection(
                runtime.Up,
                changePercent,
                stock.UpThreshold,
                stock.ResetMargin,
                "up",
                startupEvaluation,
                quote.Price,
                basePrice,
                chinaNow);
            evaluation.DownTrigger = EvaluateDirection(
                runtime.Down,
                -changePercent,
                stock.DownThreshold,
                stock.ResetMargin,
                "down",
                startupEvaluation,
                quote.Price,
                basePrice,
                chinaNow);
            return evaluation;
        }

        private static decimal ResolveBasePrice(StockConfig stock, Quote quote, DateTime chinaNow)
        {
            string today = chinaNow.ToString("yyyy-MM-dd");
            bool customToday = stock.CustomBaseEnabled
                && stock.CustomBasePrice > 0m
                && string.Equals(stock.CustomBaseDate, today, StringComparison.Ordinal);

            if (customToday) return stock.CustomBasePrice;
            if (quote.PreviousClose > 0m) return quote.PreviousClose;
            return quote.Price;
        }

        private static RuleTrigger EvaluateDirection(
            DirectionRuntimeState state,
            decimal metric,
            decimal threshold,
            decimal resetMargin,
            string direction,
            bool startupEvaluation,
            decimal price,
            decimal basePrice,
            DateTime chinaNow)
        {
            RuleTrigger result = new RuleTrigger();
            result.Direction = direction;
            result.Price = price;
            result.BasePrice = basePrice;
            result.TriggeredAt = chinaNow;
            result.ChangePercent = direction == "up" ? metric : -metric;
            result.NotificationNumber = state.NotificationCount;
            result.LevelIndex = state.LevelCursor;

            if (threshold <= 0m)
            {
                return result;
            }

            if (!state.Armed && metric <= threshold - Math.Max(0.01m, resetMargin))
            {
                state.ResetCycle();
            }

            bool aboveThreshold = metric > threshold + ComparisonEpsilon;
            if (!aboveThreshold)
            {
                return result;
            }

            decimal rawLevel = (metric - threshold) / LevelStep;
            int levelIndex = (int)Math.Floor((double)(rawLevel + ComparisonEpsilon));
            if (levelIndex < 0) levelIndex = 0;

            bool shouldNotify = false;
            string reason = string.Empty;

            if (state.Armed)
            {
                shouldNotify = true;
                reason = "threshold";
            }
            else if (levelIndex > state.LevelCursor && state.NotificationCount < 3)
            {
                shouldNotify = true;
                reason = "level";
            }

            if (startupEvaluation && !state.StartupNotifiedThisRun)
            {
                shouldNotify = true;
                reason = "startup";
            }

            if (shouldNotify)
            {
                state.StartupNotifiedThisRun = true;
                state.Armed = false;
                if (levelIndex > state.LevelCursor) state.LevelCursor = levelIndex;
                if (state.NotificationCount < 3) state.NotificationCount++;
                state.LastNotifiedAt = chinaNow;
                state.LastNotifiedPercent = result.ChangePercent;

                result.ShouldNotify = true;
                result.Reason = reason;
                result.LevelIndex = levelIndex;
                result.NotificationNumber = state.NotificationCount;
            }

            return result;
        }
    }
}
