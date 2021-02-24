using AT.DataObjects;
using Jil;
using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.Linq;
using System.Net;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AT.StockAPI
{
    public class Methods
    {


        public static bool DateHasData(int year, int month, int day, bool tryReallyHard)
        {

            string urlQuotes = "https://api.polygon.io/v2/ticks/stocks/nbbo/AAPL/{0}-{1}-{2}?limit=1&apiKey={3}";
            string urlTrades = "https://api.polygon.io/v2/ticks/stocks/trades/AAPL/{0}-{1}-{2}?limit=1&apiKey={3}";

            bool result = true;

            try
            {
                string s1 = new WebClient().DownloadString(String.Format(urlQuotes, year, month.ToString("D2"), day.ToString("D2"), Config.Key));
                string s2 = new WebClient().DownloadString(String.Format(urlTrades, year, month.ToString("D2"), day.ToString("D2"), Config.Key));
            }
            catch (Exception)
            {
                result = false;
            }

            if (!result && tryReallyHard)
            {
                for (int n = 0; n < 10; n++)
                {
                    Thread.Sleep(500);
                    if (DateHasData(year, month, day, false))
                    {
                        result = true;
                        break;
                    }
                }
            }

            return result;
        }

        public static List<Trade> GetHistoricTradesFull(string symbol, int year, int month, int day, long startTimestamp = -1)
        {
            List<Trade> result = new List<Trade>();

            List<Trade> items;

            items = GetHistoricTrades(symbol, year, month, day, -1, startTimestamp);

            result.AddRange(items);

            while (items.Count != 0)
            {

                items = GetHistoricTrades(symbol, year, month, day, -1, items[items.Count - 1].Timestamp + 1);
                result.AddRange(items);
            }

            result = result.OrderBy(t => t.Timestamp).ToList();

            DataMethods.DeDupeTrades(result);

            return result;
        }

        /// <summary>
        /// Gets the historic trades for a symbol.
        /// </summary>
        /// <param name="startTimeStamp">If using the last timestamp, that timestamp will be returned</param>
        public static List<Trade> GetHistoricTrades(string symbol, int year, int month, int day, int limit = -1, long startTimeStamp = -1, long endTimeStampLimit = -1)
        {
            List<Trade> result = new List<Trade>();

            // symbol is case sensitive
            symbol = symbol.ToUpper();

            NameValueCollection qs = new NameValueCollection();

            qs.Add("apiKey", Config.Key);

            if (limit != -1)
            {
                qs.Add("limit", limit.ToString());
            }
            if (startTimeStamp != -1)
            {
                qs.Add("timestamp", startTimeStamp.ToString());
            }
            if (endTimeStampLimit != -1)
            {
                qs.Add("timestampLimit", endTimeStampLimit.ToString());
            }

            string url = String.Format("https://api.polygon.io/v2/ticks/stocks/trades/{0}/{1}-{2}-{3}?{4}", symbol, year, month.ToString("D2"), day.ToString("D2"), UC.NameValueCollectionToString(qs));

            string payload = UC.DownloadStringAsync(url);

            var obj = JSON.DeserializeDynamic(payload);

            for (int n = 0; n < obj["results"].Count; n++)
            {
                Trade t = new Trade();

                var resultItem = obj["results"][n];

                if (resultItem.ContainsKey("c"))
                {

                    for (int m = 0; m < resultItem["c"].Count; m++)
                    {
                        t.Conditions += resultItem["c"][m].ToString() + (m == resultItem["c"].Count - 1 ? "" : ",");
                    }
                }
                else
                {
                    t.Conditions = "";
                }

                t.ExchangeId = (int)resultItem["x"];

                t.Price = (decimal)resultItem["p"];

                if (resultItem.ContainsKey("z"))
                {
                    t.Tape = (int)resultItem["z"];
                }

                t.Timestamp = (long)resultItem["t"];

                t.Volume = (int)obj["results"][n]["s"];

                result.Add(t);

            }

            result = result.OrderBy(t => t.Timestamp).ToList();

            return result;
        }


        /// <summary>
        /// startTimeStamp is inclusive.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="month"></param>
        /// <param name="day"></param>
        /// <param name="year"></param>
        /// <param name="startTimestamp">Inclusive</param>
        /// <returns></returns>
        public static List<Quote> GetHistoricQuotesFull(string symbol, int year, int month, int day, long startTimestamp = -1)
        {
            List<Quote> result = new List<Quote>();

            List<Quote> items;

            items = GetHistoricQuotes(symbol, year, month, day, -1, startTimestamp);

            result.AddRange(items);

            while (items.Count != 0)
            {
                items = GetHistoricQuotes(symbol, year, month, day, -1, items[items.Count - 1].Timestamp + 1);
                result.AddRange(items);
            }

            result = result.OrderBy(t => t.Timestamp).ToList();


            DataMethods.DeDupeQuotes(result);

            return result;
        }

        /// <summary>
        /// Get historic quotes. Each item has 1 bid and 1 ask. Good for looking at the spread.
        /// </summary>
        /// <param name="startTimeStamp">Inclusive</param>
        public static List<Quote> GetHistoricQuotes(string symbol, int year, int month, int day, int limit = -1, long startTimeStamp = -1, long endTimeStampLimit = -1)
        {
            List<Quote> result = new List<Quote>();

            // symbol is case sensitive
            symbol = symbol.ToUpper();

            NameValueCollection qs = new NameValueCollection();

            qs.Add("apiKey", Config.Key);

            if (limit != -1)
            {
                qs.Add("limit", limit.ToString());
            }
            if (startTimeStamp != -1)
            {
                qs.Add("timestamp", startTimeStamp.ToString());
            }
            if (endTimeStampLimit != -1)
            {
                qs.Add("timestampLimit", endTimeStampLimit.ToString());
            }

            string url = String.Format("https://api.polygon.io/v2/ticks/stocks/nbbo/{0}/{1}-{2}-{3}?{4}", symbol, year, month.ToString("D2"), day.ToString("D2"), UC.NameValueCollectionToString(qs));

            string payload = UC.DownloadStringAsync(url);

            var obj = JSON.DeserializeDynamic(payload); ;

            for (int n = 0; n < obj["results"].Count; n++)
            {
                Quote q = new Quote();

                var resultItem = obj["results"][n];

                if (resultItem.ContainsKey("c"))
                {

                    for (int m = 0; m < resultItem["c"].Count; m++)
                    {
                        q.Conditions += resultItem["c"][m].ToString() + (m == resultItem["c"].Count - 1 ? "" : ",");
                    }
                }
                else
                {
                    q.Conditions = "";
                }

                q.Timestamp = (long)resultItem["t"];

                q.BidPrice = (decimal)resultItem["p"];

                q.BidExchangeId = (int)resultItem["x"];

                q.BidSize = (int)resultItem["s"];

                q.AskPrice = (decimal)resultItem["P"];

                q.AskExchangeId = (int)resultItem["X"];

                q.AskSize = (int)resultItem["S"];

                if (resultItem.ContainsKey("z"))
                {
                    q.Tape = (int)resultItem["z"];
                }

                result.Add(q);

            }

            result = result.OrderBy(t => t.Timestamp).ToList();

            return result;
        }
    }
}
