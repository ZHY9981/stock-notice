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

        public MonitorSnapshotEventArgs(string statusMessage, string providerName, DateTime lastFetchAt, bool isFetching)
        {
            StatusMessage = statusMessage;
            ProviderName = providerName;
            LastFetchAt = lastFetchAt;
            IsFetching = isFetching;
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
        private readonly Dictionary<string, StockConfig> _stocksById;

        private Timer _timer;
        private int _busy;
        private bool _initialDisplayDone;
        private bool _firstEvaluationForSession;
        private string _statusMessage;
        private string _providerName;
        private DateTime _lastFetchAt;

        public event EventHandler<TriggerEventArgs> TriggerRaised;
        public event EventHandler<MonitorSnapshotEventArgs> SnapshotUpdated;

        public MonitorService(AppState state, JsonStateStore store, MarketDataService marketData, RuleEngine ruleEngine)
        {
            _state = state;
            _store = store;
            _marketData = marketData;
            _ruleEngine = ruleEngine;
            _quotes = new Dictionary<string, Quote>();
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
                    }
                }
                _providerName = "演示数据";
                _statusMessage = status;
                _lastFetchAt = DateTime.Now;
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
            }
            RaiseSnapshot(true);

            if (snapshotStocks.Count == 0)
            {
                lock (_sync)
                {
                    _statusMessage = "暂无监控股票";
                    _lastFetchAt = DateTime.Now;
                }
                RaiseSnapshot(false);
                return;
            }

            ProviderResult result = await _marketData.GetQuotesAsync(snapshotStocks);
            Dictionary<string, Quote> received = new Dictionary<string, Quote>();
            for (int i = 0; i < result.Quotes.Count; i++)
            {
                Quote quote = result.Quotes[i];
                StockConfig stock = FindStock(snapshotStocks, quote.Code, quote.Market);
                if (stock != null) received[stock.Id] = quote;
            }

            bool stateChanged = false;
            List<TriggerRecord> newTriggers = new List<TriggerRecord>();

            lock (_sync)
            {
                foreach (KeyValuePair<string, Quote> pair in received)
                {
                    _quotes[pair.Key] = pair.Value;
                    StockConfig stock = _stocksById.ContainsKey(pair.Key) ? _stocksById[pair.Key] : null;
                    if (stock != null && !string.IsNullOrEmpty(pair.Value.Name))
                    {
                        stock.Name = pair.Value.Name;
                        stateChanged = true;
                    }
                }

                if (result.Quotes.Count > 0)
                {
                    _providerName = result.ProviderName;
                    _lastFetchAt = DateTime.Now;
                    _statusMessage = string.IsNullOrEmpty(result.Error)
                        ? result.ProviderName + " 更新成功"
                        : result.ProviderName + "：" + result.Error;
                }
                else
                {
                    _statusMessage = string.IsNullOrEmpty(result.Error) ? "未获取到行情" : result.Error;
                }
            }

            if (evaluateTriggers && result.Quotes.Count > 0)
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

                    StockRuntimeState runtime = _state.GetRuntime(stock.Id, chinaNow.ToString("yyyy-MM-dd"));
                    RuleEvaluation evaluation = _ruleEngine.Evaluate(
                        stock,
                        quote,
                        runtime,
                        chinaNow,
                        _firstEvaluationForSession);

                    if (evaluation.UpTrigger != null && evaluation.UpTrigger.ShouldNotify)
                    {
                        TriggerRecord record = BuildTrigger(stock, quote, evaluation.UpTrigger, chinaNow);
                        newTriggers.Add(record);
                        stateChanged = true;
                    }

                    if (evaluation.DownTrigger != null && evaluation.DownTrigger.ShouldNotify)
                    {
                        TriggerRecord record = BuildTrigger(stock, quote, evaluation.DownTrigger, chinaNow);
                        newTriggers.Add(record);
                        stateChanged = true;
                    }
                }

                _firstEvaluationForSession = false;
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
            for (int i = 0; i < newTriggers.Count; i++)
            {
                EventHandler<TriggerEventArgs> handler = TriggerRaised;
                if (handler != null) handler(this, new TriggerEventArgs(newTriggers[i]));
            }
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

        private void RaiseSnapshot(bool fetching)
        {
            string status;
            string provider;
            DateTime lastFetch;
            lock (_sync)
            {
                status = _statusMessage;
                provider = _providerName;
                lastFetch = _lastFetchAt;
            }

            EventHandler<MonitorSnapshotEventArgs> handler = SnapshotUpdated;
            if (handler != null)
            {
                handler(this, new MonitorSnapshotEventArgs(status, provider, lastFetch, fetching));
            }
        }
    }
}
