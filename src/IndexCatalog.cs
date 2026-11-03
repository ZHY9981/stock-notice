using System;
using System.Collections.Generic;

namespace StockNotice
{
    // The three benchmark indices shown in the top bar. They are display-only:
    // they are never added to AppState.Stocks and never take part in alert
    // evaluation, so the monitor list stays limited to real A-share holdings.
    public sealed class IndexDefinition
    {
        public IndexDefinition(string code, string market, string displayName, string fullName)
        {
            Code = code;
            Market = market;
            DisplayName = displayName;
            FullName = fullName;
        }

        public string Code { get; private set; }
        public string Market { get; private set; }
        public string DisplayName { get; private set; }
        public string FullName { get; private set; }

        public string Symbol
        {
            get { return Market + Code; }
        }

        public StockConfig CreateStock()
        {
            StockConfig stock = new StockConfig();
            stock.Id = "index-" + Symbol;
            stock.Code = Code;
            stock.Market = Market;
            stock.Name = FullName;
            stock.Enabled = false;
            return stock;
        }
    }

    public static class IndexCatalog
    {
        private static readonly IndexDefinition[] Items = new IndexDefinition[]
        {
            new IndexDefinition("000001", MarketNames.Shanghai, "上证", "上证指数"),
            new IndexDefinition("399001", MarketNames.Shenzhen, "深成", "深证成指"),
            new IndexDefinition("399006", MarketNames.Shenzhen, "创业板", "创业板指")
        };

        public static IList<IndexDefinition> All
        {
            get { return Items; }
        }

        public static List<StockConfig> CreateStocks()
        {
            List<StockConfig> stocks = new List<StockConfig>();
            for (int i = 0; i < Items.Length; i++)
            {
                stocks.Add(Items[i].CreateStock());
            }
            return stocks;
        }

        public static string KeyOf(string market, string code)
        {
            return (market ?? string.Empty) + (code ?? string.Empty);
        }

        public static IndexDefinition Find(string market, string code)
        {
            string key = KeyOf(market, code);
            for (int i = 0; i < Items.Length; i++)
            {
                if (string.Equals(Items[i].Symbol, key, StringComparison.Ordinal)) return Items[i];
            }
            return null;
        }

        // Some providers report an index under the exchange inferred from the
        // numeric code, which maps 000001 to Shenzhen. Falling back to the code
        // alone keeps the benchmark strip aligned with the requested catalog.
        public static IndexDefinition FindByCode(string code)
        {
            for (int i = 0; i < Items.Length; i++)
            {
                if (string.Equals(Items[i].Code, code, StringComparison.Ordinal)) return Items[i];
            }
            return null;
        }

        public static List<Quote> CreateDemoQuotes(DateTime timestamp)
        {
            List<Quote> quotes = new List<Quote>();
            quotes.Add(DemoQuote(Items[0], 3298.12m, 3284.32m, timestamp));
            quotes.Add(DemoQuote(Items[1], 10512.30m, 10438.19m, timestamp));
            quotes.Add(DemoQuote(Items[2], 2187.55m, 2190.18m, timestamp));
            return quotes;
        }

        private static Quote DemoQuote(
            IndexDefinition definition,
            decimal price,
            decimal previousClose,
            DateTime timestamp)
        {
            Quote quote = new Quote();
            quote.Code = definition.Code;
            quote.Market = definition.Market;
            quote.Name = definition.FullName;
            quote.Price = price;
            quote.PreviousClose = previousClose;
            quote.ChangePercent = decimal.Round((price / previousClose - 1m) * 100m, 2);
            quote.Timestamp = timestamp;
            quote.IsValid = true;
            return quote;
        }
    }
}
