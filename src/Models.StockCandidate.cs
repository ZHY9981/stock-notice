namespace StockNotice
{
    // One hit from the code / name search box.
    public class StockCandidate
    {
        public string Code { get; set; }
        public string Name { get; set; }
        public string Market { get; set; }
        public string SecurityTypeName { get; set; }

        public StockCandidate()
        {
            Code = string.Empty;
            Name = string.Empty;
            Market = MarketNames.Shanghai;
            SecurityTypeName = string.Empty;
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

        public string DisplayText
        {
            get { return Name + "  " + Symbol; }
        }

        public StockConfig ToStock()
        {
            StockConfig stock = new StockConfig();
            stock.Code = Code;
            stock.Market = Market;
            stock.Name = Name;
            return stock;
        }
    }
}
