using System;
using System.Collections.Generic;
using StockNotice;

namespace StockNotice.Tests
{
    internal static class MarketDataLiveCheck
    {
        private static int Main(string[] args)
        {
            AppLogger.Initialize();
            JsonStateStore store = new JsonStateStore();
            StartupCheckResult startupCheck = StartupCheck.Run(store);
            Console.WriteLine("Startup: " + startupCheck.Summary);
            Console.WriteLine("Log: " + AppLogger.LogPath);
            AppLogger.Info("Live market-data check started for "
                + (args.Length > 0 ? args[0] : "600519"));

            string code = args.Length > 0 ? args[0] : "600519";

            StockConfig stock = new StockConfig();
            stock.Code = code;
            stock.Market = MarketDataService.InferMarket(code);
            stock.Name = code;

            List<StockConfig> stocks = new List<StockConfig>();
            stocks.Add(stock);

            MarketDataService service = new MarketDataService();
            ProviderResult result = service.GetQuotesAsync(stocks).GetAwaiter().GetResult();

            Console.WriteLine("Provider: " + result.ProviderName);
            Console.WriteLine("Error: " + result.Error);

            bool hasValidQuote = false;
            for (int i = 0; i < result.Quotes.Count; i++)
            {
                Quote quote = result.Quotes[i];
                Console.WriteLine(
                    "Quote: " + quote.Code
                    + " " + quote.Name
                    + " price=" + quote.Price.ToString("0.###")
                    + " previous=" + quote.PreviousClose.ToString("0.###")
                    + " change=" + quote.ChangePercent.ToString("0.###")
                    + "% valid=" + quote.IsValid);
                if (quote.IsValid) hasValidQuote = true;
            }

            // The header index strip is display-only, but it must still come from
            // a real source; verify all three benchmarks resolve.
            List<Quote> indexes = service.GetIndexQuotesAsync().GetAwaiter().GetResult();
            Console.WriteLine("Index count: " + indexes.Count);
            bool hasAllIndexes = indexes.Count == 3;
            for (int i = 0; i < indexes.Count; i++)
            {
                Quote quote = indexes[i];
                Console.WriteLine(
                    "Index: " + quote.Code
                    + " " + quote.Name
                    + " price=" + quote.Price.ToString("0.###")
                    + " change=" + quote.ChangePercent.ToString("0.###")
                    + "% valid=" + quote.IsValid);
                if (!quote.IsValid) hasAllIndexes = false;
            }

            return hasValidQuote && hasAllIndexes ? 0 : 1;
        }
    }
}
