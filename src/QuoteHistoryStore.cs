using System;
using System.Collections.Generic;
using System.IO;
using System.Web.Script.Serialization;

namespace StockNotice
{
    // Keeps the intraday price samples that feed the detail sparkline so the
    // chart survives a restart. Stored next to state.json, one day per file.
    public class QuoteHistoryStore
    {
        private readonly string _path;
        private readonly JavaScriptSerializer _serializer;

        public QuoteHistoryStore(string directory)
        {
            _path = System.IO.Path.Combine(
                string.IsNullOrEmpty(directory) ? "." : directory,
                "quote-history.json");
            _serializer = new JavaScriptSerializer();
        }

        public string Path
        {
            get { return _path; }
        }

        public void Save(string dateKey, IDictionary<string, List<Quote>> history)
        {
            if (string.IsNullOrEmpty(dateKey) || history == null) return;

            Dictionary<string, List<QuoteSample>> samples = new Dictionary<string, List<QuoteSample>>();
            foreach (KeyValuePair<string, List<Quote>> pair in history)
            {
                if (string.IsNullOrEmpty(pair.Key) || pair.Value == null || pair.Value.Count == 0) continue;

                List<QuoteSample> list = new List<QuoteSample>();
                for (int i = 0; i < pair.Value.Count; i++)
                {
                    Quote quote = pair.Value[i];
                    if (quote == null || quote.Price <= 0m || quote.Timestamp == DateTime.MinValue) continue;

                    QuoteSample sample = new QuoteSample();
                    sample.P = quote.Price;
                    sample.T = quote.Timestamp;
                    list.Add(sample);
                }
                if (list.Count > 0) samples[pair.Key] = list;
            }

            QuoteHistoryFile file = new QuoteHistoryFile();
            file.Date = dateKey;
            file.Samples = samples;

            string directory = System.IO.Path.GetDirectoryName(_path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            string temp = _path + ".tmp";
            File.WriteAllText(temp, _serializer.Serialize(file));
            if (File.Exists(_path)) File.Delete(_path);
            File.Move(temp, _path);
        }

        public bool TryLoad(string todayKey, out Dictionary<string, List<Quote>> history)
        {
            history = new Dictionary<string, List<Quote>>();
            if (string.IsNullOrEmpty(todayKey) || !File.Exists(_path)) return false;

            try
            {
                QuoteHistoryFile file = _serializer.Deserialize<QuoteHistoryFile>(File.ReadAllText(_path));
                if (file == null || !string.Equals(file.Date, todayKey, StringComparison.Ordinal)) return false;
                if (file.Samples == null) return false;

                foreach (KeyValuePair<string, List<QuoteSample>> pair in file.Samples)
                {
                    if (pair.Value == null || pair.Value.Count == 0) continue;

                    List<Quote> quotes = new List<Quote>();
                    for (int i = 0; i < pair.Value.Count; i++)
                    {
                        QuoteSample sample = pair.Value[i];
                        if (sample == null || sample.P <= 0m) continue;

                        Quote quote = new Quote();
                        quote.Price = sample.P;
                        quote.Timestamp = sample.T;
                        quote.IsValid = true;
                        quotes.Add(quote);
                    }
                    if (quotes.Count > 0) history[pair.Key] = quotes;
                }
                return history.Count > 0;
            }
            catch
            {
                history = new Dictionary<string, List<Quote>>();
                return false;
            }
        }

        public void Delete()
        {
            try
            {
                if (File.Exists(_path)) File.Delete(_path);
            }
            catch
            {
                // A stubborn file must not break shutdown.
            }
        }

        public class QuoteSample
        {
            public decimal P { get; set; }
            public DateTime T { get; set; }
        }

        private class QuoteHistoryFile
        {
            public string Date { get; set; }
            public Dictionary<string, List<QuoteSample>> Samples { get; set; }
        }
    }
}
