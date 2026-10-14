using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web.Script.Serialization;

namespace StockNotice
{
    public class ProviderResult
    {
        public List<Quote> Quotes { get; set; }
        public string ProviderName { get; set; }
        public string Error { get; set; }
        public DateTime FetchedAt { get; set; }

        public ProviderResult()
        {
            Quotes = new List<Quote>();
            ProviderName = string.Empty;
            Error = string.Empty;
            FetchedAt = DateTime.Now;
        }

        public bool IsSuccess
        {
            get { return Quotes.Count > 0 && string.IsNullOrEmpty(Error); }
        }
    }

    public class MarketDataService
    {
        private static readonly Regex CodeRegex = new Regex(@"^(?<market>SH|SZ|BJ)?(?<code>\d{6})(?:\.(?<suffix>SH|SZ|BJ))?$", RegexOptions.IgnoreCase | RegexOptions.Compiled);
        private readonly HttpClient _http;
        private readonly JavaScriptSerializer _serializer;

        public string LastProviderName { get; private set; }
        public string LastError { get; private set; }

        public MarketDataService()
        {
            try
            {
                ServicePointManager.SecurityProtocol = SecurityProtocolType.Tls12;
            }
            catch
            {
                // Older Windows installations may not expose the TLS 1.2 enum value.
            }

            HttpClientHandler handler = new HttpClientHandler();
            handler.AutomaticDecompression = DecompressionMethods.GZip | DecompressionMethods.Deflate;
            handler.UseProxy = true;
            handler.UseDefaultCredentials = true;

            _http = new HttpClient(handler);
            _http.Timeout = TimeSpan.FromSeconds(10);
            _http.DefaultRequestHeaders.UserAgent.ParseAdd(
                "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 " +
                "(KHTML, like Gecko) Chrome/125.0.0.0 Safari/537.36");

            _serializer = new JavaScriptSerializer();
            _serializer.MaxJsonLength = int.MaxValue;
            LastProviderName = string.Empty;
            LastError = string.Empty;
        }

        public async Task<ProviderResult> GetQuotesAsync(List<StockConfig> stocks)
        {
            if (stocks == null || stocks.Count == 0)
            {
                return new ProviderResult();
            }

            List<string> errors = new List<string>();

            try
            {
                ProviderResult eastMoney = await GetEastMoneyAsync(stocks);
                if (eastMoney.Quotes.Count > 0)
                {
                    LastProviderName = eastMoney.ProviderName;
                    LastError = eastMoney.Error;
                    return eastMoney;
                }
                errors.Add("东方财富：" + eastMoney.Error);
            }
            catch (Exception ex)
            {
                errors.Add("东方财富：" + ex.Message);
            }

            try
            {
                ProviderResult tencent = await GetTencentAsync(stocks);
                if (tencent.Quotes.Count > 0)
                {
                    LastProviderName = tencent.ProviderName;
                    LastError = tencent.Error;
                    return tencent;
                }
                errors.Add("腾讯行情：" + tencent.Error);
            }
            catch (Exception ex)
            {
                errors.Add("腾讯行情：" + ex.Message);
            }

            ProviderResult failed = new ProviderResult();
            failed.ProviderName = "免费公开行情源";
            failed.Error = string.Join("；", errors.ToArray());
            LastProviderName = failed.ProviderName;
            LastError = failed.Error;
            return failed;
        }

        public async Task<Quote> LookupAsync(string rawCode)
        {
            StockConfig stock;
            string error;
            if (!TryNormalizeStock(rawCode, out stock, out error))
            {
                Quote invalid = new Quote();
                invalid.IsValid = false;
                invalid.Error = error;
                return invalid;
            }

            List<StockConfig> list = new List<StockConfig>();
            list.Add(stock);
            ProviderResult result = await GetQuotesAsync(list);
            if (result.Quotes.Count > 0) return result.Quotes[0];

            Quote missing = new Quote();
            missing.Code = stock.Code;
            missing.Market = stock.Market;
            missing.Error = result.Error;
            return missing;
        }

        public static bool TryNormalizeStock(string rawCode, out StockConfig stock, out string error)
        {
            stock = null;
            error = string.Empty;

            if (string.IsNullOrEmpty(rawCode))
            {
                error = "请输入 6 位股票代码。";
                return false;
            }

            string value = rawCode.Trim().ToUpperInvariant();
            Match match = CodeRegex.Match(value);
            if (!match.Success)
            {
                error = "股票代码格式无效，请输入例如 600519、SH600519 或 000001。";
                return false;
            }

            string market = match.Groups["market"].Value;
            if (string.IsNullOrEmpty(market)) market = match.Groups["suffix"].Value;

            string code = match.Groups["code"].Value;
            if (string.IsNullOrEmpty(market)) market = InferMarket(code);

            StockConfig item = new StockConfig();
            item.Code = code;
            item.Market = market;
            item.Name = code;
            stock = item;
            return true;
        }

        public static string InferMarket(string code)
        {
            if (string.IsNullOrEmpty(code)) return MarketNames.Shenzhen;

            string first = code.Substring(0, 1);
            string prefix2 = code.Length >= 2 ? code.Substring(0, 2) : code;

            if (first == "6" || first == "5" || first == "9") return MarketNames.Shanghai;
            if (prefix2 == "43" || prefix2 == "83" || prefix2 == "87" || prefix2 == "92") return MarketNames.Beijing;
            return MarketNames.Shenzhen;
        }

        private async Task<ProviderResult> GetEastMoneyAsync(List<StockConfig> stocks)
        {
            List<string> secIds = new List<string>();
            Dictionary<string, StockConfig> lookup = new Dictionary<string, StockConfig>();

            for (int i = 0; i < stocks.Count; i++)
            {
                StockConfig stock = stocks[i];
                string marketId = stock.Market == MarketNames.Shanghai ? "1" : "0";
                secIds.Add(marketId + "." + stock.Code);
                lookup[marketId + "." + stock.Code] = stock;
            }

            string url = "https://push2.eastmoney.com/api/qt/ulist.np/get"
                + "?fltt=2&invt=2&ut=fa5fd1943c7b386f172d6893dbfba10b"
                + "&fields=f12,f14,f2,f3,f4,f18,f15,f16,f17,f124"
                + "&secids=" + string.Join(",", secIds.ToArray());

            string json;
            using (HttpResponseMessage response = await _http.GetAsync(url))
            {
                response.EnsureSuccessStatusCode();
                json = await response.Content.ReadAsStringAsync();
            }

            EastMoneyResponse payload = _serializer.Deserialize<EastMoneyResponse>(json);
            ProviderResult result = new ProviderResult();
            result.ProviderName = "东方财富";

            if (payload == null || payload.data == null || payload.data.diff == null || payload.data.diff.Count == 0)
            {
                result.Error = "接口未返回行情数据。";
                return result;
            }

            for (int i = 0; i < payload.data.diff.Count; i++)
            {
                EastMoneyItem item = payload.data.diff[i];
                string code = ToText(item.f12);
                StockConfig stock;
                string symbol = InferMarket(code) == MarketNames.Shanghai ? "1." + code : "0." + code;
                lookup.TryGetValue(symbol, out stock);

                Quote quote = new Quote();
                quote.Code = code;
                quote.Market = stock != null ? stock.Market : InferMarket(code);
                quote.Name = ToText(item.f14);
                quote.Price = ToDecimal(item.f2);
                quote.PreviousClose = ToDecimal(item.f18);
                quote.ChangePercent = ToDecimal(item.f3);
                quote.Timestamp = FromUnixSeconds(ToLong(item.f124));
                quote.IsValid = quote.Price > 0m && quote.PreviousClose > 0m;
                if (!quote.IsValid) quote.Error = "停牌或无有效成交价";
                result.Quotes.Add(quote);
            }

            return result;
        }

        private async Task<ProviderResult> GetTencentAsync(List<StockConfig> stocks)
        {
            List<string> symbols = new List<string>();
            for (int i = 0; i < stocks.Count; i++)
            {
                StockConfig stock = stocks[i];
                string prefix = "sz";
                if (stock.Market == MarketNames.Shanghai) prefix = "sh";
                if (stock.Market == MarketNames.Beijing) prefix = "bj";
                symbols.Add(prefix + stock.Code);
            }

            string url = "https://qt.gtimg.cn/q=" + string.Join(",", symbols.ToArray());
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Get, url);
            request.Headers.Referrer = new Uri("https://gu.qq.com/");

            string text;
            using (HttpResponseMessage response = await _http.SendAsync(request))
            {
                response.EnsureSuccessStatusCode();
                byte[] bytes = await response.Content.ReadAsByteArrayAsync();
                text = Encoding.GetEncoding(936).GetString(bytes);
            }

            ProviderResult result = new ProviderResult();
            result.ProviderName = "腾讯行情";

            MatchCollection matches = Regex.Matches(
                text,
                "v_(?<symbol>[^=]+)=\"(?<data>[^\"]*)\"",
                RegexOptions.Singleline);

            for (int i = 0; i < matches.Count; i++)
            {
                string[] fields = matches[i].Groups["data"].Value.Split('~');
                if (fields.Length < 35) continue;

                StockConfig requested = FindBySymbol(stocks, matches[i].Groups["symbol"].Value);
                Quote quote = new Quote();
                quote.Code = fields[2];
                quote.Market = requested != null ? requested.Market : InferMarket(quote.Code);
                quote.Name = fields.Length > 1 ? fields[1] : quote.Code;
                quote.Price = ToDecimal(fields[3]);
                quote.PreviousClose = ToDecimal(fields[4]);
                quote.ChangePercent = fields.Length > 32 ? ToDecimal(fields[32]) : 0m;
                quote.Timestamp = ParseTencentTime(fields.Length > 30 ? fields[30] : string.Empty);
                quote.IsValid = quote.Price > 0m && quote.PreviousClose > 0m;
                if (!quote.IsValid) quote.Error = "停牌或无有效成交价";
                result.Quotes.Add(quote);
            }

            if (result.Quotes.Count == 0) result.Error = "接口未返回可识别的行情数据。";
            return result;
        }

        private static StockConfig FindBySymbol(List<StockConfig> stocks, string symbol)
        {
            if (string.IsNullOrEmpty(symbol)) return null;
            string normalized = symbol.Trim().ToLowerInvariant();
            for (int i = 0; i < stocks.Count; i++)
            {
                StockConfig stock = stocks[i];
                string prefix = stock.Market == MarketNames.Shanghai ? "sh" : (stock.Market == MarketNames.Beijing ? "bj" : "sz");
                if (normalized == prefix + stock.Code) return stock;
            }
            return null;
        }

        private static string ToText(object value)
        {
            if (value == null) return string.Empty;
            return Convert.ToString(value, CultureInfo.InvariantCulture);
        }

        private static decimal ToDecimal(object value)
        {
            if (value == null) return 0m;
            if (value is decimal) return (decimal)value;
            if (value is double) return Convert.ToDecimal((double)value, CultureInfo.InvariantCulture);
            if (value is float) return Convert.ToDecimal((float)value, CultureInfo.InvariantCulture);
            if (value is int) return (int)value;
            if (value is long) return (long)value;

            string text = Convert.ToString(value, CultureInfo.InvariantCulture);
            decimal result;
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result)) return result;
            return 0m;
        }

        private static long ToLong(object value)
        {
            if (value == null) return 0L;
            if (value is long) return (long)value;
            if (value is int) return (int)value;
            if (value is decimal) return Convert.ToInt64((decimal)value, CultureInfo.InvariantCulture);

            long result;
            if (long.TryParse(Convert.ToString(value, CultureInfo.InvariantCulture), out result)) return result;
            return 0L;
        }

        private static DateTime FromUnixSeconds(long seconds)
        {
            if (seconds <= 0L) return DateTime.MinValue;
            DateTime utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
            return utc.ToLocalTime();
        }

        private static DateTime ParseTencentTime(string value)
        {
            DateTime result;
            if (DateTime.TryParseExact(
                value,
                "yyyyMMddHHmmss",
                CultureInfo.InvariantCulture,
                DateTimeStyles.None,
                out result))
            {
                return result;
            }
            return DateTime.MinValue;
        }

        private class EastMoneyResponse
        {
            public EastMoneyData data { get; set; }
        }

        private class EastMoneyData
        {
            public List<EastMoneyItem> diff { get; set; }
        }

        private class EastMoneyItem
        {
            public object f12 { get; set; }
            public object f14 { get; set; }
            public object f2 { get; set; }
            public object f3 { get; set; }
            public object f4 { get; set; }
            public object f18 { get; set; }
            public object f15 { get; set; }
            public object f16 { get; set; }
            public object f17 { get; set; }
            public object f124 { get; set; }
        }
    }
}
