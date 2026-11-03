using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace StockNotice
{
    public class TriggerEventArgs : EventArgs
    {
        public TriggerRecord Record { get; private set; }

        public TriggerEventArgs(TriggerRecord record)
        {
            Record = record;
        }
    }

    public class MonitorSnapshotEventArgs : EventArgs
    {
        public string StatusMessage { get; private set; }
        public string ProviderName { get; private set; }
        public DateTime LastFetchAt { get; private set; }
        public bool IsFetching { get; private set; }
        public int ConsecutiveFailures { get; private set; }
        public int DataAgeSeconds { get; private set; }
        public bool IsDataStale { get; private set; }

        public MonitorSnapshotEventArgs(
            string statusMessage,
            string providerName,
            DateTime lastFetchAt,
            bool isFetching,
            int consecutiveFailures,
            int dataAgeSeconds,
            bool isDataStale)
        {
            StatusMessage = statusMessage;
            ProviderName = providerName;
            LastFetchAt = lastFetchAt;
            IsFetching = isFetching;
            ConsecutiveFailures = consecutiveFailures;
            DataAgeSeconds = dataAgeSeconds;
            IsDataStale = isDataStale;
        }
    }

    public class MonitorService
    {
        private readonly AppState _state;
        private readonly JsonStateStore _store;
        private readonly MarketDataService _marketData;
        private readonly RuleEngine _ruleEngine;
        private readonly object _sync = new object();
        private readonly Dictionary<string, Quote> _quotes;
        private readonly Dictionary<string, List<Quote>> _quoteHistory;
        private readonly Dictionary<string, StockConfig> _stocksById;

        private Timer _timer;
        private int _busy;
        private bool _initialDisplayDone;
        private bool _firstEvaluationForSession;
        private string _statusMessage;
        private string _providerName;
        private DateTime _lastFetchAt;
        private DateTime _lastAttemptAt;
        private DateTime _lastTransportResetAt;
        private int _consecutiveFailures;

        public event EventHandler<TriggerEventArgs> TriggerRaised;
        public event EventHandler<MonitorSnapshotEventArgs> SnapshotUpdated;

        public MonitorService(AppState state, JsonStateStore store, MarketDataService marketData, RuleEngine ruleEngine)
        {
            _state = state;
            _store = store;
            _marketData = marketData;
            _ruleEngine = ruleEngine;
            _quotes = new Dictionary<string, Quote>();
            _quoteHistory = new Dictionary<string, List<Quote>>();
            _stocksById = new Dictionary<string, StockConfig>();
            _statusMessage = "等待首次检查";
            _providerName = "免费公开行情源";
            _lastFetchAt = DateTime.MinValue;
        }

        public string StatusMessage
        {
            get { lock (_sync) return _statusMessage; }
        }

        public string ProviderName
        {
            get { lock (_sync) return _providerName; }
        }

        public DateTime LastFetchAt
        {
            get { lock (_sync) return _lastFetchAt; }
        }

        public DateTime LastAttemptAt
        {
            get { lock (_sync) return _lastAttemptAt; }
        }

        public int ConsecutiveFailures
        {
            get { lock (_sync) return _consecutiveFailures; }
        }

        public int DataAgeSeconds
        {
            get { return CalculateDataAgeSeconds(); }
        }

        public bool IsDataStale
        {
            get
            {
                lock (_sync)
                {
                    if (_lastFetchAt == DateTime.MinValue) return false;
                    double age = (MarketClock.Now() - _lastFetchAt).TotalSeconds;
                    return age > _state.Settings.StaleQuoteSeconds;
                }
            }
        }

        public void Start()
        {
            if (_timer != null) return;
            RebuildStockIndex();
            _firstEvaluationForSession = true;
            _timer = new Timer(OnTimer, null, 100, _state.Settings.PollIntervalSeconds * 1000);
        }

        public void Stop()
        {
            Timer timer = _timer;
            _timer = null;
            if (timer != null) timer.Dispose();
        }

        public void RebuildStockIndex()
        {
            lock (_sync)
            {
                _stocksById.Clear();
                for (int i = 0; i < _state.Stocks.Count; i++)
                {
                    StockConfig stock = _state.Stocks[i];
                    if (stock != null && !string.IsNullOrEmpty(stock.Id))
                    {
                        _stocksById[stock.Id] = stock;
                    }
                }
            }
        }

        public Quote GetQuote(string stockId)
        {
            lock (_sync)
            {
                Quote quote;
                if (_quotes.TryGetValue(stockId, out quote)) return quote;
                return null;
            }
        }

        public List<Quote> GetQuoteHistory(string stockId)
        {
            lock (_sync)
            {
                List<Quote> history;
                if (!_quoteHistory.TryGetValue(stockId, out history) || history == null)
                {
                    return new List<Quote>();
                }
                return new List<Quote>(history);
            }
        }

        public void ClearQuoteHistory(string stockId)
        {
            lock (_sync)
            {
                _quoteHistory.Remove(stockId);
                _quotes.Remove(stockId);
            }
        }

        public void LoadDemoQuotes(Dictionary<string, Quote> quotes, string status)
        {
            lock (_sync)
            {
                _quotes.Clear();
                if (quotes != null)
                {
                    foreach (KeyValuePair<string, Quote> pair in quotes)
                    {
                        _quotes[pair.Key] = pair.Value;
                    AddQuoteHistory(pair.Key, pair.Value);
                    }
                }
                _providerName = "演示数据";
                _statusMessage = status;
                _lastFetchAt = MarketClock.Now();
                _lastAttemptAt = _lastFetchAt;
                _consecutiveFailures = 0;
            }
        }

        public void ResetRuntimeForStock(string stockId)
        {
            string today = MarketClock.TodayKey();
            StockRuntimeState runtime = _state.GetRuntime(stockId, today);
            runtime.Up.ResetCycle();
            runtime.Down.ResetCycle();
            runtime.Up.StartupNotifiedThisRun = false;
            runtime.Down.StartupNotifiedThisRun = false;
        }

        public Task RefreshNowAsync()
        {
            DateTime now = MarketClock.Now();
            bool evaluate = MarketClock.IsTradingSession(now) && _state.Settings.MonitoringEnabled;
            return PollCoreAsync(evaluate, now, true);
        }

        private async void OnTimer(object ignored)
        {
            if (Interlocked.CompareExchange(ref _busy, 1, 0) != 0) return;

            try
            {
                DateTime now = MarketClock.Now();
                bool session = MarketClock.IsTradingSession(now);
                bool evaluate = session && _state.Settings.MonitoringEnabled;

                lock (_sync)
                {
                    if (!session && _initialDisplayDone)
                    {
                        _statusMessage = MarketClock.GetSessionText(now);
                        return;
                    }
                }

                await PollCoreAsync(evaluate, now, false);
                _initialDisplayDone = true;
            }
            catch (Exception ex)
            {
                AppLogger.Error("Monitor timer failed.", ex);
                lock (_sync)
                {
                    _statusMessage = "检查失败：" + ex.Message;
                }
                RaiseSnapshot(false);
            }
            finally
            {
                Interlocked.Exchange(ref _busy, 0);
            }
        }

        private async Task PollCoreAsync(bool evaluateTriggers, DateTime chinaNow, bool manual)
        {
            List<StockConfig> snapshotStocks;
            lock (_sync)
            {
                snapshotStocks = _state.Stocks == null
                    ? new List<StockConfig>()
                    : new List<StockConfig>(_state.Stocks);
                _statusMessage = "正在获取行情";
                _lastAttemptAt = chinaNow;
            }
            RaiseSnapshot(true);

            if (snapshotStocks.Count == 0)
            {
                lock (_sync)
                {
                    _statusMessage = "暂无监控股票";
                    _lastFetchAt = chinaNow;
                    _consecutiveFailures = 0;
                }
                RaiseSnapshot(false);
                return;
            }

            ProviderResult result = await _marketData.GetQuotesAsync(snapshotStocks);
            Dictionary<string, Quote> received = new Dictionary<string, Quote>();
            for (int i = 0; i < result.Quotes.Count; i++)
            {
                Quote quote = result.Quotes[i];
                if (quote == null || !quote.IsValid) continue;
                StockConfig stock = FindStock(snapshotStocks, quote.Code, quote.Market);
                if (stock != null) received[stock.Id] = quote;
            }

            bool stateChanged = false;
            int failuresBefore = 0;
            List<TriggerRecord> newTriggers = new List<TriggerRecord>();
            List<TriggerRecord> deliveredTriggers = new List<TriggerRecord>();

            lock (_sync)
            {
                foreach (KeyValuePair<string, Quote> pair in received)
                {
                    _quotes[pair.Key] = pair.Value;
                    AddQuoteHistory(pair.Key, pair.Value);
                    StockConfig stock = _stocksById.ContainsKey(pair.Key) ? _stocksById[pair.Key] : null;
                    if (stock != null && !string.IsNullOrEmpty(pair.Value.Name))
                    {
                        stock.Name = pair.Value.Name;
                        stateChanged = true;
                    }
                }

                failuresBefore = _consecutiveFailures;
                if (received.Count > 0)
                {
                    _consecutiveFailures = 0;
                    _providerName = result.ProviderName;
                    _lastFetchAt = chinaNow;
                    _statusMessage = string.IsNullOrEmpty(result.Error)
                        ? result.ProviderName + " 更新成功"
                        : result.ProviderName + " 部分更新：" + result.Error;
                }
                else
                {
                    _consecutiveFailures++;
                    string error = string.IsNullOrEmpty(result.Error) ? "未获取到行情" : result.Error;
                    if (_lastFetchAt == DateTime.MinValue)
                    {
                        _statusMessage = "行情获取失败（连续 "
                            + _consecutiveFailures.ToString() + " 次）· 暂无可用缓存：" + error;
                    }
                    else
                    {
                        int ageSeconds = Math.Max(0, (int)(chinaNow - _lastFetchAt).TotalSeconds);
                        _statusMessage = "行情获取失败（连续 "
                            + _consecutiveFailures.ToString() + " 次）· 显示 "
                            + _lastFetchAt.ToString("HH:mm:ss") + " 缓存价格，已 "
                            + ageSeconds.ToString() + " 秒：" + error;
                    }
                }
            }

            if (received.Count > 0 && failuresBefore > 0)
            {
                AppLogger.Info("Market data recovered after "
                    + failuresBefore.ToString() + " failure(s).");
            }
            else if (received.Count == 0)
            {
                AppLogger.Warn("Market data unavailable. Consecutive failures: "
                    + ConsecutiveFailures.ToString() + ". " + result.Error);
            }

            if (received.Count == 0)
            {
                bool recoverTransport = false;
                lock (_sync)
                {
                    if (_consecutiveFailures >= 3
                        && (_lastTransportResetAt == DateTime.MinValue
                            || chinaNow - _lastTransportResetAt >= TimeSpan.FromMinutes(1)))
                    {
                        _lastTransportResetAt = chinaNow;
                        recoverTransport = true;
                    }
                }

                if (recoverTransport)
                {
                    try
                    {
                        _marketData.RecoverTransport();
                        lock (_sync)
                        {
                            _statusMessage += "；已重置行情连接";
                        }
                        AppLogger.Warn("Market data transport reset after repeated failures.");
                    }
                    catch (Exception ex)
                    {
                        AppLogger.Error("Market data transport reset failed.", ex);
                    }
                }
            }

            int staleQuotes = 0;
            if (evaluateTriggers && received.Count > 0)
            {
                if (_firstEvaluationForSession)
                {
                    ResetStartupFlags(snapshotStocks, chinaNow);
                }

                for (int i = 0; i < snapshotStocks.Count; i++)
                {
                    StockConfig stock = snapshotStocks[i];
                    Quote quote;
                    if (!stock.Enabled || !received.TryGetValue(stock.Id, out quote)) continue;
                    if (!quote.IsValid) continue;
                    if (!MarketClock.IsFreshTradingDate(quote, chinaNow)) continue;
                    if (!MarketClock.IsQuoteFresh(
                        quote,
                        chinaNow,
                        TimeSpan.FromSeconds(_state.Settings.StaleQuoteSeconds)))
                    {
                        staleQuotes++;
                        continue;
                    }

                    StockRuntimeState runtime = _state.GetRuntime(stock.Id, chinaNow.ToString("yyyy-MM-dd"));
                    DateTime previousUpNotifiedAt = runtime.Up.LastNotifiedAt;
                    DateTime previousDownNotifiedAt = runtime.Down.LastNotifiedAt;
                    RuleEvaluation evaluation = _ruleEngine.Evaluate(
                        stock,
                        quote,
                        runtime,
                        chinaNow,
                        _firstEvaluationForSession);

                    if (evaluation.UpTrigger != null && evaluation.UpTrigger.ShouldNotify)
                    {
                        AddTriggerRecord(
                            stock,
                            quote,
                            evaluation.UpTrigger,
                            previousUpNotifiedAt,
                            chinaNow,
                            newTriggers,
                            deliveredTriggers);
                        stateChanged = true;
                    }

                    if (evaluation.DownTrigger != null && evaluation.DownTrigger.ShouldNotify)
                    {
                        AddTriggerRecord(
                            stock,
                            quote,
                            evaluation.DownTrigger,
                            previousDownNotifiedAt,
                            chinaNow,
                            newTriggers,
                            deliveredTriggers);
                        stateChanged = true;
                    }
                }

                _firstEvaluationForSession = false;
            }

            if (staleQuotes > 0)
            {
                lock (_sync)
                {
                    _statusMessage += "；" + staleQuotes.ToString() + " 只股票行情未更新";
                }
                AppLogger.Warn(staleQuotes.ToString()
                    + " quote(s) were stale and skipped for alert evaluation.");
            }

            if (newTriggers.Count > 0)
            {
                for (int i = 0; i < newTriggers.Count; i++)
                {
                    _state.History.Insert(0, newTriggers[i]);
                }
                while (_state.History.Count > 1000)
                {
                    _state.History.RemoveAt(_state.History.Count - 1);
                }
            }

            if (stateChanged || newTriggers.Count > 0 || manual)
            {
                try
                {
                    _store.Save(_state);
                }
                catch
                {
                    // The UI remains usable if a transient disk write fails.
                }
            }

            RaiseSnapshot(false);
            for (int i = 0; i < deliveredTriggers.Count; i++)
            {
                EventHandler<TriggerEventArgs> handler = TriggerRaised;
                if (handler != null) handler(this, new TriggerEventArgs(deliveredTriggers[i]));
            }
        }

        private void AddTriggerRecord(
            StockConfig stock,
            Quote quote,
            RuleTrigger trigger,
            DateTime previousNotifiedAt,
            DateTime chinaNow,
            List<TriggerRecord> allTriggers,
            List<TriggerRecord> deliveredTriggers)
        {
            TriggerRecord record = BuildTrigger(stock, quote, trigger, chinaNow);
            AlertDeliveryDecision decision = AlertPolicy.Evaluate(
                record,
                _state.Settings,
                previousNotifiedAt,
                chinaNow);

            if (!decision.ShouldNotify)
            {
                record.SuppressedReason = decision.SuppressionReason;
                AppLogger.Info("Alert delivery suppressed for "
                    + record.StockCode + " " + record.Direction
                    + ": " + decision.SuppressionReason);
            }
            else
            {
                deliveredTriggers.Add(record);
            }

            allTriggers.Add(record);
        }

        private void ResetStartupFlags(List<StockConfig> stocks, DateTime chinaNow)
        {
            string today = chinaNow.ToString("yyyy-MM-dd");
            for (int i = 0; i < stocks.Count; i++)
            {
                StockRuntimeState runtime = _state.GetRuntime(stocks[i].Id, today);
                runtime.Up.StartupNotifiedThisRun = false;
                runtime.Down.StartupNotifiedThisRun = false;
            }
        }

        private static TriggerRecord BuildTrigger(StockConfig stock, Quote quote, RuleTrigger trigger, DateTime chinaNow)
        {
            TriggerRecord record = new TriggerRecord();
            record.StockId = stock.Id;
            record.StockCode = stock.Code;
            record.StockName = stock.Name;
            record.Direction = trigger.Direction;
            record.ChangePercent = trigger.ChangePercent;
            record.Price = quote.Price;
            record.BasePrice = trigger.BasePrice;
            record.LevelIndex = trigger.LevelIndex;
            record.NotificationNumber = trigger.NotificationNumber;
            record.Reason = trigger.Reason;
            record.TriggeredAt = chinaNow;
            return record;
        }

        private static StockConfig FindStock(List<StockConfig> stocks, string code, string market)
        {
            for (int i = 0; i < stocks.Count; i++)
            {
                if (string.Equals(stocks[i].Code, code, StringComparison.Ordinal)
                    && (string.IsNullOrEmpty(market) || string.Equals(stocks[i].Market, market, StringComparison.Ordinal)))
                {
                    return stocks[i];
                }
            }

            for (int i = 0; i < stocks.Count; i++)
            {
                if (string.Equals(stocks[i].Code, code, StringComparison.Ordinal)) return stocks[i];
            }
            return null;
        }

        private void AddQuoteHistory(string stockId, Quote quote)
        {
            if (string.IsNullOrEmpty(stockId) || quote == null || !quote.IsValid) return;

            List<Quote> history;
            if (!_quoteHistory.TryGetValue(stockId, out history) || history == null)
            {
                history = new List<Quote>();
                _quoteHistory[stockId] = history;
            }

            history.Add(quote);
            while (history.Count > 40)
            {
                history.RemoveAt(0);
            }
        }

        private int CalculateDataAgeSeconds()
        {
            lock (_sync)
            {
                if (_lastFetchAt == DateTime.MinValue) return -1;
                double age = (MarketClock.Now() - _lastFetchAt).TotalSeconds;
                if (age < 0d) return 0;
                if (age > int.MaxValue) return int.MaxValue;
                return (int)age;
            }
        }

        private void RaiseSnapshot(bool fetching)
        {
            string status;
            string provider;
            DateTime lastFetch;
            int consecutiveFailures;
            bool stale;
            lock (_sync)
            {
                status = _statusMessage;
                provider = _providerName;
                lastFetch = _lastFetchAt;
                consecutiveFailures = _consecutiveFailures;
                if (lastFetch == DateTime.MinValue)
                {
                    stale = false;
                }
                else
                {
                    double age = (MarketClock.Now() - lastFetch).TotalSeconds;
                    stale = age > _state.Settings.StaleQuoteSeconds;
                }
            }

            int dataAgeSeconds = CalculateDataAgeSeconds();
            EventHandler<MonitorSnapshotEventArgs> handler = SnapshotUpdated;
            if (handler != null)
            {
                handler(this, new MonitorSnapshotEventArgs(
                    status,
                    provider,
                    lastFetch,
                    fetching,
                    consecutiveFailures,
                    dataAgeSeconds,
                    stale));
            }
        }
    }
}
