using System;
using System.Collections.Generic;
using System.Threading;
using System.Windows.Forms;

namespace StockNotice
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);

            bool demoMode = false;
            for (int i = 0; i < args.Length; i++)
            {
                if (string.Equals(args[i], "--demo", StringComparison.OrdinalIgnoreCase))
                {
                    demoMode = true;
                }
            }

            bool createdNew;
            using (Mutex mutex = new Mutex(true, "StockNotice.SingleInstance", out createdNew))
            {
                if (!createdNew)
                {
                    MessageBox.Show(
                        "股票涨跌提醒已经在运行，请在任务栏托盘查看。",
                        "股票涨跌提醒",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Information);
                    return;
                }

                try
                {
                    Application.Run(CreateMainForm(demoMode));
                }
                catch (Exception ex)
                {
                    MessageBox.Show(
                        "程序启动失败：" + ex.Message,
                        "股票涨跌提醒",
                        MessageBoxButtons.OK,
                        MessageBoxIcon.Error);
                }
            }
        }

        private static MainForm CreateMainForm(bool demoMode)
        {
            JsonStateStore store = new JsonStateStore();
            AppState state = store.Load();
            MarketDataService marketData = new MarketDataService();
            RuleEngine ruleEngine = new RuleEngine();
            MonitorService monitor = new MonitorService(state, store, marketData, ruleEngine);

            if (demoMode)
            {
                SeedDemo(state, store, monitor);
            }

            return new MainForm(state, store, marketData, monitor, demoMode);
        }

        private static void SeedDemo(AppState state, JsonStateStore store, MonitorService monitor)
        {
            if (state.Stocks.Count == 0)
            {
                state.Stocks.Add(CreateStock("600519", MarketNames.Shanghai, "贵州茅台", 2.0m, 2.0m, 0.5m));
                state.Stocks.Add(CreateStock("000001", MarketNames.Shenzhen, "平安银行", 1.5m, 1.5m, 0.4m));
                state.Stocks.Add(CreateStock("300750", MarketNames.Shenzhen, "宁德时代", 3.0m, 3.0m, 1.0m));
                store.Save(state);
            }

            Dictionary<string, Quote> quotes = new Dictionary<string, Quote>();
            for (int i = 0; i < state.Stocks.Count; i++)
            {
                StockConfig stock = state.Stocks[i];
                decimal previousClose = 100m + i * 10m;
                decimal price = previousClose * (1m + (i - 1) * 0.017m);

                Quote quote = new Quote();
                quote.Code = stock.Code;
                quote.Market = stock.Market;
                quote.Name = stock.Name;
                quote.Price = decimal.Round(price, 2);
                quote.PreviousClose = previousClose;
                quote.ChangePercent = decimal.Round((quote.Price / previousClose - 1m) * 100m, 2);
                quote.Timestamp = DateTime.Now;
                quote.IsValid = true;
                quotes[stock.Id] = quote;
            }

            monitor.LoadDemoQuotes(quotes, "演示数据（未连接行情源）");
        }

        private static StockConfig CreateStock(
            string code,
            string market,
            string name,
            decimal upThreshold,
            decimal downThreshold,
            decimal resetMargin)
        {
            StockConfig stock = new StockConfig();
            stock.Code = code;
            stock.Market = market;
            stock.Name = name;
            stock.UpThreshold = upThreshold;
            stock.DownThreshold = downThreshold;
            stock.ResetMargin = resetMargin;
            return stock;
        }
    }
}
