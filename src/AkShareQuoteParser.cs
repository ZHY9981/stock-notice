using System;
using System.Globalization;
using System.Web.Script.Serialization;

namespace StockNotice
{
    // Parses the single-stock response used by AKShare's stock_bid_ask_em
    // interface. Keeping this separate makes the field mapping testable without
    // making a network request.
    internal static class AkShareQuoteParser
    {
        public static Quote Parse(
            string requestedCode,
            string requestedMarket,
            string requestedName,
            string json)
        {
            Quote quote = new Quote();
            quote.Code = string.IsNullOrEmpty(requestedCode) ? string.Empty : requestedCode.Trim();
            quote.Market = requestedMarket;
            quote.Name = string.IsNullOrEmpty(requestedName) ? quote.Code : requestedName;

            if (string.IsNullOrEmpty(json))
            {
                quote.Error = "AKShare 接口未返回数据";
                return quote;
            }

            AkShareResponse payload;
            try
            {
                JavaScriptSerializer serializer = new JavaScriptSerializer();
                payload = serializer.Deserialize<AkShareResponse>(json);
            }
            catch (Exception ex)
            {
                quote.Error = "AKShare 数据解析失败：" + ex.Message;
                return quote;
            }

            if (payload == null || payload.data == null)
            {
                quote.Error = "AKShare 接口没有有效行情";
                return quote;
            }

            string code = ToText(payload.data.f57);
            if (!string.IsNullOrEmpty(code)) quote.Code = code;

            string name = ToText(payload.data.f58);
            if (!string.IsNullOrEmpty(name)) quote.Name = name;

            quote.Price = ToDecimal(payload.data.f43);
            quote.PreviousClose = ToDecimal(payload.data.f60);
            quote.ChangePercent = ToDecimal(payload.data.f170);
            quote.Timestamp = FromUnixSeconds(ToLong(payload.data.f86));
            quote.IsValid = quote.Price > 0m && quote.PreviousClose > 0m;
            if (!quote.IsValid)
            {
                quote.Error = "停牌或无有效成交价";
            }

            return quote;
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
            if (decimal.TryParse(text, NumberStyles.Any, CultureInfo.InvariantCulture, out result))
            {
                return result;
            }
            return 0m;
        }

        private static long ToLong(object value)
        {
            if (value == null) return 0L;
            if (value is long) return (long)value;
            if (value is int) return (int)value;
            if (value is decimal) return Convert.ToInt64((decimal)value, CultureInfo.InvariantCulture);

            long result;
            if (long.TryParse(
                Convert.ToString(value, CultureInfo.InvariantCulture),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out result))
            {
                return result;
            }
            return 0L;
        }

        private static DateTime FromUnixSeconds(long seconds)
        {
            if (seconds <= 0L) return DateTime.MinValue;
            DateTime utc = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc).AddSeconds(seconds);
            return utc.ToLocalTime();
        }

        private class AkShareResponse
        {
            public AkShareQuoteData data { get; set; }
        }

        private class AkShareQuoteData
        {
            public object f43 { get; set; }
            public object f57 { get; set; }
            public object f58 { get; set; }
            public object f60 { get; set; }
            public object f86 { get; set; }
            public object f170 { get; set; }
        }
    }
}
