using System;
using System.Collections.Generic;

namespace StockNotice
{
    public static class MarketNames
    {
        public const string Shanghai = "SH";
        public const string Shenzhen = "SZ";
        public const string Beijing = "BJ";

        public static string Display(string market)
        {
            if (market == Shanghai) return "沪市";
            if (market == Shenzhen) return "深市";
            if (market == Beijing) return "北交所";
            return "未知";
        }
    }

    public class StockConfig
    {
        public string Id { get; set; }
        public string Code { get; set; }
        public string Market { get; set; }
        public string Name { get; set; }
        public bool Enabled { get; set; }
        public decimal UpThreshold { get; set; }
        public decimal DownThreshold { get; set; }
        public decimal ResetMargin { get; set; }
        public bool CustomBaseEnabled { get; set; }
        public decimal CustomBasePrice { get; set; }
        public string CustomBaseDate { get; set; }
        public DateTime CreatedAt { get; set; }

        public StockConfig()
        {
            Id = Guid.NewGuid().ToString("N");
            Code = string.Empty;
            Market = MarketNames.Shanghai;
            Name = string.Empty;
            Enabled = true;
            UpThreshold = 3.0m;
            DownThreshold = 3.0m;
            ResetMargin = 0.5m;
            CustomBaseEnabled = false;
            CustomBasePrice = 0m;
            CustomBaseDate = string.Empty;
            CreatedAt = DateTime.Now;
        }

        public string Symbol
        {
            get
            {
                if (Market == MarketNames.Shanghai) return "SH" + Code;
                if (Market == MarketNames.Shenzhen) return "SZ" + Code;
                if (Market == MarketNames.Beijing) return "BJ" + Code;
                return Code;
            }
        }
    }

    public class Quote
    {
        public string Code { get; set; }
        public string Market { get; set; }
        public string Name { get; set; }
        public decimal Price { get; set; }
        public decimal PreviousClose { get; set; }
        public decimal ChangePercent { get; set; }
        public DateTime Timestamp { get; set; }
        public bool IsValid { get; set; }
        public string Error { get; set; }

        public Quote()
        {
            Code = string.Empty;
            Market = MarketNames.Shanghai;
            Name = string.Empty;
            Timestamp = DateTime.MinValue;
            IsValid = false;
            Error = string.Empty;
        }
    }

    public class DirectionRuntimeState
    {
        public bool Armed { get; set; }
        public int LevelCursor { get; set; }
        public int NotificationCount { get; set; }
        public DateTime LastNotifiedAt { get; set; }
        public decimal LastNotifiedPercent { get; set; }
        public bool StartupNotifiedThisRun { get; set; }

        public DirectionRuntimeState()
        {
            Armed = true;
            LevelCursor = -1;
            NotificationCount = 0;
            LastNotifiedAt = DateTime.MinValue;
            LastNotifiedPercent = 0m;
            StartupNotifiedThisRun = false;
        }

        public void ResetCycle()
        {
            Armed = true;
            LevelCursor = -1;
            NotificationCount = 0;
            LastNotifiedAt = DateTime.MinValue;
            LastNotifiedPercent = 0m;
        }
    }

    public class StockRuntimeState
    {
        public string TradingDate { get; set; }
        public DirectionRuntimeState Up { get; set; }
        public DirectionRuntimeState Down { get; set; }

        public StockRuntimeState()
        {
            TradingDate = string.Empty;
            Up = new DirectionRuntimeState();
            Down = new DirectionRuntimeState();
        }
    }

    public class TriggerRecord
    {
        public string Id { get; set; }
        public string StockId { get; set; }
        public string StockCode { get; set; }
        public string StockName { get; set; }
        public string Direction { get; set; }
        public decimal ChangePercent { get; set; }
        public decimal Price { get; set; }
        public decimal BasePrice { get; set; }
        public int LevelIndex { get; set; }
        public int NotificationNumber { get; set; }
        public string Reason { get; set; }
        public DateTime TriggeredAt { get; set; }

        public TriggerRecord()
        {
            Id = Guid.NewGuid().ToString("N");
            StockId = string.Empty;
            StockCode = string.Empty;
            StockName = string.Empty;
            Direction = "up";
            Reason = string.Empty;
            TriggeredAt = DateTime.Now;
        }
    }

    public class AppSettings
    {
        public int PollIntervalSeconds { get; set; }
        public bool MonitoringEnabled { get; set; }
        public bool SoundEnabled { get; set; }
        public bool StartWithWindows { get; set; }

        public AppSettings()
        {
            PollIntervalSeconds = 5;
            MonitoringEnabled = true;
            SoundEnabled = true;
            StartWithWindows = false;
        }
    }

    public class AppState
    {
        public int Version { get; set; }
        public AppSettings Settings { get; set; }
        public List<StockConfig> Stocks { get; set; }
        public Dictionary<string, StockRuntimeState> Runtime { get; set; }
        public List<TriggerRecord> History { get; set; }
        public DateTime UpdatedAt { get; set; }

        public AppState()
        {
            Version = 1;
            Settings = new AppSettings();
            Stocks = new List<StockConfig>();
            Runtime = new Dictionary<string, StockRuntimeState>();
            History = new List<TriggerRecord>();
            UpdatedAt = DateTime.Now;
        }

        public void Normalize()
        {
            if (Settings == null) Settings = new AppSettings();
            if (Settings.PollIntervalSeconds <= 0) Settings.PollIntervalSeconds = 5;
            if (Stocks == null) Stocks = new List<StockConfig>();
            if (Runtime == null) Runtime = new Dictionary<string, StockRuntimeState>();
            if (History == null) History = new List<TriggerRecord>();
        }

        public StockRuntimeState GetRuntime(string stockId, string tradingDate)
        {
            Normalize();

            StockRuntimeState runtime;
            if (!Runtime.TryGetValue(stockId, out runtime) || runtime == null)
            {
                runtime = new StockRuntimeState();
                Runtime[stockId] = runtime;
            }

            if (runtime.Up == null) runtime.Up = new DirectionRuntimeState();
            if (runtime.Down == null) runtime.Down = new DirectionRuntimeState();

            if (!string.Equals(runtime.TradingDate, tradingDate, StringComparison.Ordinal))
            {
                runtime.TradingDate = tradingDate;
                runtime.Up.ResetCycle();
                runtime.Down.ResetCycle();
            }

            return runtime;
        }
    }

    public class RuleTrigger
    {
        public bool ShouldNotify { get; set; }
        public string Direction { get; set; }
        public int LevelIndex { get; set; }
        public int NotificationNumber { get; set; }
        public string Reason { get; set; }
        public decimal ChangePercent { get; set; }
        public decimal Price { get; set; }
        public decimal BasePrice { get; set; }
        public DateTime TriggeredAt { get; set; }

        public RuleTrigger()
        {
            Direction = "up";
            LevelIndex = -1;
            NotificationNumber = 0;
            Reason = string.Empty;
            TriggeredAt = DateTime.Now;
        }
    }

    public class RuleEvaluation
    {
        public decimal BasePrice { get; set; }
        public decimal ChangePercent { get; set; }
        public RuleTrigger UpTrigger { get; set; }
        public RuleTrigger DownTrigger { get; set; }
    }
}
