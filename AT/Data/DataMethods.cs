
using System;
using System.Collections.Generic;
using System.Data;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AT.DataObjects;
using AT.Geometry.Objects;
using MessagePack;

namespace AT
{



    public class DataMethods
    {
        /// <summary>
        /// Builds a NodesData object.
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="quotes"></param>
        /// <param name="startNanoSecond">This should be on :00.000. Inclusive</param>
        /// <param name="endNanoSecond">This should be on :00.000. Exclusive.</param>
        public static NodesData BuildNodes(List<Trade> trades, List<Quote> quotes, long startNanoSecond, long endNanoSecond, Interval interval)
        {
            int capacity = (int)((endNanoSecond - startNanoSecond) / (long)interval);

            NodesData result = new NodesData()
            {
                Start = startNanoSecond,
                End = endNanoSecond,
                NodeInterval = (long)interval,
                Nodes = new List<Node>(capacity)
            };

            long nanoSecondSpread = (long)interval;

            int currentTradesIndex = 0;
            int currentQuotesIndex = 0;

            //int lastIndexTradePriceNotEstimated = -1;
            //int lastIndexAskPriceNotEstimated = -1;
            //int lastIndexBidPriceNotEstimated = -1;


            int firstNonEstimatedTradePrice = -1;
            int lastNonEstimatedTradePrice = -1;


            int firstNonEstimatedBidPrice = -1;
            int lastNonEstimatedBidPrice = -1;

            int firstNonEstimatedAskPrice = -1;
            int lastNonEstimatedAskPrice = -1;


            int prevNonEstimatedTradePrice = -1;
            int prevNonEstimatedAskPrice = -1;
            int prevNonEstimatedBidPrice = -1;


            // pretty confident this should hit all the ranges properly
            for (long n = startNanoSecond; n < endNanoSecond; n += nanoSecondSpread)
            {
                Node node = new Node();

                #region QUOTES

                decimal askPriceSum = 0M;
                decimal bidPriceSum = 0M;

                int askPriceCount = 0;
                int bidPriceCount = 0;

                // traverse quotes while in the range of [n,n + nanoSecondSpread)
                while (currentQuotesIndex < quotes.Count)
                {

                    // window of time for this node
                    if (quotes[currentQuotesIndex].Timestamp >= n && quotes[currentQuotesIndex].Timestamp < n + nanoSecondSpread)
                    {
                        Quote quote = quotes[currentQuotesIndex];

                        if (quote.AskPrice != 0M)
                        {
                            askPriceSum += quote.AskPrice;
                            askPriceCount++;
                        }

                        if (quote.BidPrice != 0M)
                        {
                            bidPriceSum += quote.BidPrice;
                            bidPriceCount++;
                        }

                        currentQuotesIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (bidPriceCount > 0)
                {
                    node.BidPrice = bidPriceSum / bidPriceCount;
                    node.BidPriceEstimated = false;
                    prevNonEstimatedBidPrice = lastNonEstimatedBidPrice;
                    lastNonEstimatedBidPrice = result.Nodes.Count;
                    if (firstNonEstimatedBidPrice == -1)
                    {
                        firstNonEstimatedBidPrice = result.Nodes.Count;
                    }
                }
                if (askPriceCount > 0)
                {
                    node.AskPrice = askPriceSum / askPriceCount;
                    node.AskPriceEstimated = false;
                    prevNonEstimatedAskPrice = lastNonEstimatedAskPrice;
                    lastNonEstimatedAskPrice = result.Nodes.Count;
                    if (firstNonEstimatedAskPrice == -1)
                    {
                        firstNonEstimatedAskPrice = result.Nodes.Count;
                    }
                }


                #endregion

                #region TRADES

                decimal tradePriceSum = 0M;

                int totalVolume = 0;

                int tradePriceCount = 0;

                // traverse trades while in the range of [n,n + nanoSecondSpread)
                while (currentTradesIndex < trades.Count)
                {

                    // window of time for this node
                    if (trades[currentTradesIndex].Timestamp >= n && trades[currentTradesIndex].Timestamp < n + nanoSecondSpread)
                    {
                        Trade trade = trades[currentTradesIndex];

                        tradePriceSum += trade.Price;
                        tradePriceCount++;
                        totalVolume += trade.Volume;

                        currentTradesIndex++;
                    }
                    else
                    {
                        break;
                    }
                }

                if (tradePriceCount > 0)
                {
                    node.TradePrice = tradePriceSum / tradePriceCount;
                    node.TradeVolume = totalVolume;
                    node.TradePriceEstimated = false;
                    prevNonEstimatedTradePrice = lastNonEstimatedTradePrice;
                    lastNonEstimatedTradePrice = result.Nodes.Count;
                    if (firstNonEstimatedTradePrice == -1)
                    {
                        firstNonEstimatedTradePrice = result.Nodes.Count;
                    }
                }

                #endregion

                if (!node.TradePriceEstimated)
                {
                    if (prevNonEstimatedTradePrice == -1)
                    {
                        result.Nodes.ForEach(o => o.TradePrice = node.TradePrice);
                    }
                    else if (result.Nodes.Count - prevNonEstimatedTradePrice > 1)
                    {
                        Node prevNotEstimated = result.Nodes[prevNonEstimatedTradePrice];
                        decimal slope = (prevNotEstimated.TradePrice - node.TradePrice) / (prevNonEstimatedTradePrice - result.Nodes.Count);
                        decimal yIntercept = prevNotEstimated.TradePrice - (slope * prevNonEstimatedTradePrice);
                        for (int m = prevNonEstimatedTradePrice + 1; m < result.Nodes.Count; m++)
                        {
                            result.Nodes[m].TradePrice = (slope * m) + yIntercept;
                        }
                    }

                }
                if (!node.BidPriceEstimated)
                {
                    if (prevNonEstimatedBidPrice == -1)
                    {
                        result.Nodes.ForEach(o => o.BidPrice = node.BidPrice);
                    }
                    else if (result.Nodes.Count - prevNonEstimatedBidPrice > 1)
                    {
                        Node lastNotEstimated = result.Nodes[prevNonEstimatedBidPrice];
                        decimal slope = (lastNotEstimated.BidPrice - node.BidPrice) / (prevNonEstimatedBidPrice - result.Nodes.Count);
                        decimal yIntercept = lastNotEstimated.BidPrice - (slope * prevNonEstimatedBidPrice);
                        for (int m = prevNonEstimatedBidPrice + 1; m < result.Nodes.Count; m++)
                        {
                            result.Nodes[m].BidPrice = (slope * m) + yIntercept;
                        }
                    }

                }
                if (!node.AskPriceEstimated)
                {
                    if (prevNonEstimatedAskPrice == -1)
                    {
                        result.Nodes.ForEach(o => o.AskPrice = node.AskPrice);
                    }
                    else if (result.Nodes.Count - prevNonEstimatedAskPrice > 1)
                    {
                        Node lastNotEstimated = result.Nodes[prevNonEstimatedAskPrice];
                        decimal slope = (lastNotEstimated.AskPrice - node.AskPrice) / (prevNonEstimatedAskPrice - result.Nodes.Count);
                        decimal yIntercept = lastNotEstimated.AskPrice - (slope * prevNonEstimatedAskPrice);
                        for (int m = prevNonEstimatedAskPrice + 1; m < result.Nodes.Count; m++)
                        {
                            result.Nodes[m].AskPrice = (slope * m) + yIntercept;
                        }
                    }

                }

                result.Nodes.Add(node);
            }


            if (prevNonEstimatedTradePrice != -1)
            {
                for (int n = prevNonEstimatedTradePrice + 1; n < result.Nodes.Count; n++)
                {
                    result.Nodes[n].TradePrice = result.Nodes[prevNonEstimatedTradePrice].TradePrice;
                }
            }
            if (prevNonEstimatedBidPrice != -1)
            {
                for (int n = prevNonEstimatedBidPrice + 1; n < result.Nodes.Count; n++)
                {
                    result.Nodes[n].BidPrice = result.Nodes[prevNonEstimatedBidPrice].BidPrice;
                }
            }
            if (prevNonEstimatedAskPrice != -1)
            {
                for (int n = prevNonEstimatedAskPrice + 1; n < result.Nodes.Count; n++)
                {
                    result.Nodes[n].AskPrice = result.Nodes[prevNonEstimatedAskPrice].AskPrice;
                }
            }

            result.FirstNonEstimatedAskPrice = firstNonEstimatedAskPrice;
            result.FirstNonEstimatedBidPrice = firstNonEstimatedBidPrice;
            result.FirstNonEstimatedTradePrice = firstNonEstimatedTradePrice;

            result.LastNonEstimatedAskPrice = lastNonEstimatedAskPrice;
            result.LastNonEstimatedBidPrice = lastNonEstimatedBidPrice;
            result.LastNonEstimatedTradePrice = lastNonEstimatedTradePrice;

            return result;
        }

        public static void AttachNodesData(NodesData source, NodesData target)
        {
            bool append = source.Start >= target.End;
            bool prepend = source.End <= target.Start;

            if ((append || prepend) && source.NodeInterval == target.NodeInterval)
            {

                if (append) // appending to target
                {
                    // maybe we just traverse once starting at the deepest point?

                    int targetLastNonEstimatedTradePrice = target.LastNonEstimatedTradePrice;
                    int sourceFirstNonEstimatedTradePrice = source.FirstNonEstimatedTradePrice;
                    int targetLastNonEstimatedBidPrice = target.LastNonEstimatedBidPrice;
                    int sourceFirstNonEstimatedBidPrice = source.FirstNonEstimatedBidPrice;
                    int targetLastNonEstimatedAskPrice = target.LastNonEstimatedAskPrice;
                    int sourceFirstNonEstimatedAskPrice = source.FirstNonEstimatedAskPrice;

                    if (targetLastNonEstimatedTradePrice == -1 && sourceFirstNonEstimatedTradePrice != -1)
                    {
                        target.Nodes.ForEach(n => n.TradePrice = source.Nodes[sourceFirstNonEstimatedTradePrice].TradePrice);
                    }
                    else if (targetLastNonEstimatedTradePrice != -1 && sourceFirstNonEstimatedTradePrice == -1)
                    {
                        source.Nodes.ForEach(n => n.TradePrice = target.Nodes[targetLastNonEstimatedTradePrice].TradePrice);
                    }

                    if (targetLastNonEstimatedBidPrice == -1 && sourceFirstNonEstimatedBidPrice != -1)
                    {
                        target.Nodes.ForEach(n => n.BidPrice = source.Nodes[sourceFirstNonEstimatedBidPrice].BidPrice);
                    }
                    else if (targetLastNonEstimatedBidPrice != -1 && sourceFirstNonEstimatedBidPrice == -1)
                    {
                        source.Nodes.ForEach(n => n.BidPrice = target.Nodes[targetLastNonEstimatedBidPrice].BidPrice);
                    }

                    if (targetLastNonEstimatedAskPrice == -1 && sourceFirstNonEstimatedAskPrice != -1)
                    {
                        target.Nodes.ForEach(n => n.AskPrice = source.Nodes[sourceFirstNonEstimatedAskPrice].AskPrice);
                    }
                    else if (targetLastNonEstimatedAskPrice != -1 && sourceFirstNonEstimatedAskPrice == -1)
                    {
                        source.Nodes.ForEach(n => n.AskPrice = target.Nodes[targetLastNonEstimatedAskPrice].AskPrice);
                    }

                    int count = target.Nodes.Count;
                    target.Nodes.AddRange(source.Nodes);

                    target.LastNonEstimatedAskPrice =
                    (
                        source.LastNonEstimatedAskPrice != -1 ?
                        source.LastNonEstimatedAskPrice + count :
                        target.LastNonEstimatedAskPrice
                    );
                    target.LastNonEstimatedBidPrice =
                    (
                        source.LastNonEstimatedBidPrice != -1 ?
                        source.LastNonEstimatedBidPrice + count :
                        target.LastNonEstimatedBidPrice
                    );
                    target.LastNonEstimatedTradePrice =
                    (
                        source.LastNonEstimatedTradePrice != -1 ?
                        source.LastNonEstimatedTradePrice + count :
                        target.LastNonEstimatedTradePrice
                    );

                    if (targetLastNonEstimatedTradePrice != -1 && sourceFirstNonEstimatedTradePrice != -1)
                    {
                        sourceFirstNonEstimatedTradePrice = count + sourceFirstNonEstimatedTradePrice;
                        Node lastNotEstimated = target.Nodes[targetLastNonEstimatedTradePrice];
                        decimal slope = (lastNotEstimated.TradePrice - target.Nodes[sourceFirstNonEstimatedTradePrice].TradePrice) / (targetLastNonEstimatedTradePrice - sourceFirstNonEstimatedTradePrice);
                        decimal yIntercept = lastNotEstimated.TradePrice - (slope * targetLastNonEstimatedTradePrice);
                        for (int m = targetLastNonEstimatedTradePrice + 1; m < sourceFirstNonEstimatedTradePrice; m++)
                        {
                            target.Nodes[m].TradePrice = (slope * m) + yIntercept;
                        }
                    }

                    if (targetLastNonEstimatedBidPrice != -1 && sourceFirstNonEstimatedBidPrice != -1)
                    {
                        sourceFirstNonEstimatedBidPrice = count + sourceFirstNonEstimatedBidPrice;
                        Node lastNotEstimated = target.Nodes[targetLastNonEstimatedBidPrice];
                        decimal slope = (lastNotEstimated.BidPrice - target.Nodes[sourceFirstNonEstimatedBidPrice].BidPrice) / (targetLastNonEstimatedBidPrice - sourceFirstNonEstimatedBidPrice);
                        decimal yIntercept = lastNotEstimated.BidPrice - (slope * targetLastNonEstimatedBidPrice);
                        for (int m = targetLastNonEstimatedBidPrice + 1; m < sourceFirstNonEstimatedBidPrice; m++)
                        {
                            target.Nodes[m].BidPrice = (slope * m) + yIntercept;
                        }
                    }

                    if (targetLastNonEstimatedAskPrice != -1 && sourceFirstNonEstimatedAskPrice != -1)
                    {
                        sourceFirstNonEstimatedAskPrice = count + sourceFirstNonEstimatedAskPrice;
                        Node lastNotEstimated = target.Nodes[targetLastNonEstimatedAskPrice];
                        decimal slope = (lastNotEstimated.AskPrice - target.Nodes[sourceFirstNonEstimatedAskPrice].AskPrice) / (targetLastNonEstimatedAskPrice - sourceFirstNonEstimatedAskPrice);
                        decimal yIntercept = lastNotEstimated.AskPrice - (slope * targetLastNonEstimatedAskPrice);
                        for (int m = targetLastNonEstimatedAskPrice + 1; m < sourceFirstNonEstimatedAskPrice; m++)
                        {
                            target.Nodes[m].AskPrice = (slope * m) + yIntercept;
                        }
                    }

                    target.End = source.End;

                    //if (source.Nodes.Count % 120 != 0 || target.Nodes.Count % 120 != 0)
                    //{
                    //    throw new Exception("hey yo");
                    //}
                }
                else
                {

                    int targetFirstNonEstimatedTradePrice = target.FirstNonEstimatedTradePrice;
                    int sourceLastNonEstimatedTradePrice = source.LastNonEstimatedTradePrice;
                    int targetFirstNonEstimatedBidPrice = target.FirstNonEstimatedBidPrice;
                    int sourceLastNonEstimatedBidPrice = source.LastNonEstimatedBidPrice;
                    int targetFirstNonEstimatedAskPrice = target.FirstNonEstimatedAskPrice;
                    int sourceLastNonEstimatedAskPrice = source.LastNonEstimatedAskPrice;

                    if (targetFirstNonEstimatedTradePrice == -1 && sourceLastNonEstimatedTradePrice != -1)
                    {
                        target.Nodes.ForEach(n => n.TradePrice = source.Nodes[sourceLastNonEstimatedTradePrice].TradePrice);
                    }
                    else if (targetFirstNonEstimatedTradePrice != -1 && sourceLastNonEstimatedTradePrice == -1)
                    {
                        source.Nodes.ForEach(n => n.TradePrice = target.Nodes[targetFirstNonEstimatedTradePrice].TradePrice);
                    }

                    if (targetFirstNonEstimatedBidPrice == -1 && sourceLastNonEstimatedBidPrice != -1)
                    {
                        target.Nodes.ForEach(n => n.BidPrice = source.Nodes[sourceLastNonEstimatedBidPrice].BidPrice);
                    }
                    else if (targetFirstNonEstimatedBidPrice != -1 && sourceLastNonEstimatedBidPrice == -1)
                    {
                        source.Nodes.ForEach(n => n.BidPrice = target.Nodes[targetFirstNonEstimatedBidPrice].BidPrice);
                    }

                    if (targetFirstNonEstimatedAskPrice == -1 && sourceLastNonEstimatedAskPrice != -1)
                    {
                        target.Nodes.ForEach(n => n.AskPrice = source.Nodes[sourceLastNonEstimatedAskPrice].AskPrice);
                    }
                    else if (targetFirstNonEstimatedAskPrice != -1 && sourceLastNonEstimatedAskPrice == -1)
                    {
                        source.Nodes.ForEach(n => n.AskPrice = target.Nodes[targetFirstNonEstimatedAskPrice].AskPrice);
                    }

                    int count = source.Nodes.Count;
                    source.Nodes.AddRange(target.Nodes);
                    target.Nodes = source.Nodes;

                    target.FirstNonEstimatedAskPrice =
                    (
                        source.FirstNonEstimatedAskPrice != -1 ?
                        source.FirstNonEstimatedAskPrice :
                        target.FirstNonEstimatedAskPrice
                    );
                    target.FirstNonEstimatedBidPrice =
                    (
                        source.FirstNonEstimatedBidPrice != -1 ?
                        source.FirstNonEstimatedBidPrice :
                        target.FirstNonEstimatedBidPrice
                    );
                    target.FirstNonEstimatedTradePrice =
                    (
                        source.FirstNonEstimatedTradePrice != -1 ?
                        source.FirstNonEstimatedTradePrice :
                        target.FirstNonEstimatedTradePrice
                    );
                    target.LastNonEstimatedTradePrice =
                    (
                        target.LastNonEstimatedTradePrice != -1 ?
                        target.LastNonEstimatedTradePrice + count :
                        source.LastNonEstimatedTradePrice
                    );
                    target.LastNonEstimatedBidPrice =
                    (
                        target.LastNonEstimatedBidPrice != -1 ?
                        target.LastNonEstimatedBidPrice + count :
                        source.LastNonEstimatedBidPrice
                    );
                    target.LastNonEstimatedAskPrice =
                    (
                        target.LastNonEstimatedAskPrice != -1 ?
                        target.LastNonEstimatedAskPrice + count :
                        source.LastNonEstimatedAskPrice
                    );

                    if (targetFirstNonEstimatedTradePrice != -1 && sourceLastNonEstimatedTradePrice != -1)
                    {
                        targetFirstNonEstimatedTradePrice = count + targetFirstNonEstimatedTradePrice;
                        Node lastNotEstimated = target.Nodes[sourceLastNonEstimatedTradePrice];
                        decimal slope = (lastNotEstimated.TradePrice - target.Nodes[targetFirstNonEstimatedTradePrice].TradePrice) / (sourceLastNonEstimatedTradePrice - targetFirstNonEstimatedTradePrice);
                        decimal yIntercept = lastNotEstimated.TradePrice - (slope * sourceLastNonEstimatedTradePrice);
                        for (int m = sourceLastNonEstimatedTradePrice + 1; m < targetFirstNonEstimatedTradePrice; m++)
                        {
                            target.Nodes[m].TradePrice = (slope * m) + yIntercept;
                        }
                    }

                    if (targetFirstNonEstimatedBidPrice != -1 && sourceLastNonEstimatedBidPrice != -1)
                    {
                        targetFirstNonEstimatedBidPrice = count + targetFirstNonEstimatedBidPrice;
                        Node lastNotEstimated = target.Nodes[sourceLastNonEstimatedBidPrice];
                        decimal slope = (lastNotEstimated.BidPrice - target.Nodes[targetFirstNonEstimatedBidPrice].BidPrice) / (sourceLastNonEstimatedBidPrice - targetFirstNonEstimatedBidPrice);
                        decimal yIntercept = lastNotEstimated.BidPrice - (slope * sourceLastNonEstimatedBidPrice);
                        for (int m = sourceLastNonEstimatedBidPrice + 1; m < targetFirstNonEstimatedBidPrice; m++)
                        {
                            target.Nodes[m].BidPrice = (slope * m) + yIntercept;
                        }
                    }

                    if (targetFirstNonEstimatedAskPrice != -1 && sourceLastNonEstimatedAskPrice != -1)
                    {
                        targetFirstNonEstimatedAskPrice = count + targetFirstNonEstimatedAskPrice;
                        Node lastNotEstimated = target.Nodes[sourceLastNonEstimatedAskPrice];
                        decimal slope = (lastNotEstimated.AskPrice - target.Nodes[targetFirstNonEstimatedAskPrice].AskPrice) / (sourceLastNonEstimatedAskPrice - targetFirstNonEstimatedAskPrice);
                        decimal yIntercept = lastNotEstimated.AskPrice - (slope * sourceLastNonEstimatedAskPrice);
                        for (int m = sourceLastNonEstimatedAskPrice + 1; m < targetFirstNonEstimatedAskPrice; m++)
                        {
                            target.Nodes[m].AskPrice = (slope * m) + yIntercept;
                        }
                    }

                    target.Start = source.Start;
                }

            }
            else
            {
                throw new Exception("Can't attach nodes data.");
            }
        }




        public static List<FD> ListOfDataDaysToListOfFD(List<string> days)
        {
            List<FD> result = new List<FD>();

            for (int n = 0; n < days.Count; n++)
            {
                string[] s = days[n].Split('-');
                int year = int.Parse(s[0]);
                int month = int.Parse(s[1]);
                int day = int.Parse(s[2]);
                result.Add(new FD(year, month, day));
            }

            result = result.OrderBy(d => d.StartNano).ToList();

            return result;
        }

        public static bool DayNodesDataCacheable(string symbol, FD day)
        {
            bool cacheable = false;

            if (Global.State.DataTracker.NoDataDays.BinarySearch(day) >= 0)
            {
                cacheable = false;
            }
            else if (
                Global.State.DataTracker.DataDays.BinarySearch(day) >= 0 &&
                Global.State.DataTracker.DataDaysBySymbol[symbol].BinarySearch(day) >= 0)
            {
                cacheable = true;
            }

            return cacheable;
        }
        public static NodesData GetCachedDayNodesData(string symbol, FD day, Interval interval, bool computeNodes = true, bool justCache = false)
        {

            NodesData result = null;

            string intervalString = Methods.GetIntervalFileString(interval);

            string daysPath = Global.Constants.NodesPath + "days";

            string dayPath = daysPath + "\\" + day.ToString();
            string filePath = dayPath + "\\" + symbol + "-" + intervalString + ".dat";

            if (File.Exists(filePath))
            {
                if (justCache)
                {
                    return null;
                }
                else
                {
                    result = LZ4MessagePackSerializer.Deserialize<NodesData>(File.ReadAllBytes(filePath));
                }

            }
            else
            {

                List<Trade> trades = DBMethods.GetTradesBySymbol(symbol, day.StartNano, day.EndNano);
                List<Quote> quotes = DBMethods.GetQuotesBySymbol(symbol, day.StartNano, day.EndNano);
                result = BuildNodes(trades, quotes, day.StartNano, day.EndNano, interval);

                if (DayNodesDataCacheable(symbol, day))
                {
                    // create dirs
                    if (!Directory.Exists(daysPath))
                    {
                        Directory.CreateDirectory(daysPath);
                    }
                    if (!Directory.Exists(dayPath))
                    {
                        Directory.CreateDirectory(dayPath);
                    }

                    byte[] bytes;

                    bytes = LZ4MessagePackSerializer.Serialize(result);

                    File.WriteAllBytes(filePath, bytes);
                }

            }

            // the nanos aren't serialized to 
            // save space, but can be computed here.
            if (computeNodes)
            {
                result.ComputeNodesNanos();
            }

            return result;
        }

        public static NodesData GetDayNodesDataSeries(string symbol, FD startDay, FD endDay, Interval interval)
        {
            //int totalDays = (int)Math.Round((endDay.DT - startDay.DT).TotalDays) + 1;

            // don't ever cache one day in series paths
            //bool cacheSeries = startDay.StartNano != endDay.StartNano;

            NodesData result = null;

            FD fd = new FD(startDay.DT.Year, startDay.DT.Month, startDay.DT.Day);



            //string intervalString = Methods.GetIntervalFileString(interval);

            //string seriesPath = Global.Constants.NodesPath + "series";

            //string seriesTotalDaysPath = seriesPath + "\\" + totalDays.ToString();
            //string seriesSpanPath = seriesTotalDaysPath + "\\" + startDay.ToString() + "_" + endDay.ToString();
            //string filePath = seriesSpanPath + "\\" + symbol + "-" + intervalString + ".dat";

            // E:\nodes\series\5\start_end\acn-hfsec.dat

            // if (File.Exists(filePath))
            //{
            //    result = LZ4MessagePackSerializer.Deserialize<NodesData>(File.ReadAllBytes(filePath));
            //}
            //else
            //{

            while (fd.StartNano <= endDay.StartNano)
            {

                //   if (!DayNodesDataCacheable(symbol, fd))
                //       {
                //           cacheSeries = false;
                //        }

                NodesData nodes = GetCachedDayNodesData(symbol, fd, interval, computeNodes: false, justCache: false);

                if (result == null)
                {
                    result = nodes;
                }
                else
                {
                    result.AttachNodesData(nodes);
                }

                fd.AddDays(1);
            }

            /*
            if (cacheSeries)
            {
                // create dirs
                if (!Directory.Exists(seriesPath))
                {
                    Directory.CreateDirectory(seriesPath);
                }
                if (!Directory.Exists(seriesTotalDaysPath))
                {
                    Directory.CreateDirectory(seriesTotalDaysPath);
                }
                if (!Directory.Exists(seriesSpanPath))
                {
                    Directory.CreateDirectory(seriesSpanPath);
                }

                byte[] bytes;

                bytes = LZ4MessagePackSerializer.Serialize(result);

                File.WriteAllBytes(filePath, bytes);
            }
            */
            //}

            result.ComputeNodesNanos();

            return result;
        }
        /*
        public static List<StockNode> GetCachedDayStockNodes(string symbol, List<FD> days, Interval interval, bool dontCache = true)
        {

            List<StockNode> result = new List<StockNode>();

            string intervalString = "";

            if(interval == Interval.OneMinute)
            {
                intervalString = "01min";
            }
            else if (interval == Interval.FiveMinutes)
            {
                intervalString = "05min";
            }
            else if (interval == Interval.FifteenMinutes)
            {
                intervalString = "15min";
            }
            else if (interval == Interval.HalfHour)
            {
                intervalString = "30min";
            }
            else if (interval == Interval.OneHour)
            {
                intervalString = "60min";
            }
            else
            {
                throw new Exception("What are you doing?");
            }

            string daysPath = Global.Constants.NodesPath + "days";


            for(int n = 0; n < days.Count; n++)
            {

            


                string dayPath = daysPath + "\\" + days[n].ToString();
                string filePath = dayPath + "\\" + symbol + "-" + intervalString + ".dat";

                if (File.Exists(filePath))
                {
                    return LZ4MessagePackSerializer.Deserialize<List<StockNode>>(File.ReadAllBytes(filePath));
                }
                else
                {

                    List<StockNode> nodes = GetStockNodes(symbol, days[n].StartNano, days[n].EndNano, interval);

                    if (!dontCache)
                    {
                        // create dirs
                        if (!Directory.Exists(daysPath))
                        {
                            Directory.CreateDirectory(daysPath);
                        }
                        if (!Directory.Exists(dayPath))
                        {
                            Directory.CreateDirectory(dayPath);
                        }

                        byte[] bytes;

                        bytes = LZ4MessagePackSerializer.Serialize(nodes);

                        File.WriteAllBytes(filePath, bytes);
                    }

                    result.AddRange(nodes);
                    
                }

            }

            return result;

        }
        */

        /*
    public static NodesData GetNodes(string symbol, long startNanoSecond, long endNanoSecond, Interval interval)
    {
        List<Trade> trades = DBMethods.GetTradesBySymbol(symbol, startNanoSecond, endNanoSecond);
        List<Quote> quotes = DBMethods.GetQuotesBySymbol(symbol, startNanoSecond, endNanoSecond);
        NodesData nodes = BuildNodes(trades, quotes, startNanoSecond, endNanoSecond, interval);

        return nodes;
    }
    */


        public static string GetSequencesDataKey(IEnumerable<SequenceKey> sequenceKeys)
        {
            return String.Join("_", sequenceKeys.OrderBy(sk => sk).ToArray());
        }


        

        /*
        public static List<StockNode> GetStockNodes(string symbol, long startNanoSecond, long endNanoSecond, Interval interval)
        {
            List<Trade> trades = DBMethods.GetTradesBySymbol(symbol, startNanoSecond, endNanoSecond);
            List<Quote> quotes = DBMethods.GetQuotesBySymbol(symbol, startNanoSecond, endNanoSecond);
            List<StockNode> nodes = BuildStockNodes(trades, quotes, startNanoSecond, endNanoSecond, interval);

            return nodes;
        }
        */


        #region StockNode Builder
        public static void ComputeWithSizeBuilder(long timestamp, decimal price, int size, StockNodeDataSizeObjectBuilder builder, StockNodeDataSizeObject data, long startNano, bool isFirst, bool isLast)
        {
            
            if(price != 0M && size != 0)
            {
                if (isFirst)
                {
                    data.Open = size;
                }
                if (isLast)
                {
                    data.Close = size;
                }
                if (size > data.High)
                {
                    data.High = size;
                }
                if (size < data.Low)
                {
                    data.Low = size;
                }

                data.Total += size;

                builder._Points.Add(new Point { X = UC.NanoToSec(timestamp - startNano), Y = size });
            }
        }
        private static void ComputeWithPriceBuilder(long timestamp, decimal price, int size, StockNodeDataPriceObjectBuilder builder, StockNodeDataPriceObject data, long startNano, bool isFirst, bool isLast)
        {
            if (price != 0M && size != 0)
            {
                if (isFirst)
                {
                    data.Open = price;
                }
                if (isLast)
                {
                    data.Close = price;
                }
                if(price > data.High)
                {
                    data.High = price;
                }
                if (price < data.Low)
                {
                    data.Low = price;
                }

                builder._Points.Add(new Point { X = UC.NanoToSec(timestamp - startNano), Y = price });

                builder._VWPrices.Add(size * price);

            }
        }

        
        private static void ComputeQuoteWithPriceSizeBuilder(Quote quote, bool isAsk, StockNodeDataPriceSizeObjectBuilder builder, StockNodeDataPriceSizeObject data, long startNano, bool isFirstQuote, bool isLastQuote)
        {
            decimal price = (isAsk ? quote.AskPrice : quote.BidPrice);
            int size = (isAsk ? quote.AskSize : quote.BidSize);

            ComputeWithPriceBuilder(quote.Timestamp, price, size, builder.PriceBuilder, data.Price, startNano, isFirstQuote, isLastQuote);
            ComputeWithSizeBuilder(quote.Timestamp, price, size, builder.SizeBuilder, data.Size, startNano, isFirstQuote, isLastQuote);
        }

        private static void ComputeQuoteWithBuilder(Quote quote, StockNodeDataQuotesBuilder builder, StockNodeDataQuotes data, long startNano, bool isFirstQuote, bool isLastQuote, Interval interval)
        {
            // this is used for backtesting
            data.DebugQuotes.Add(new QuoteLite()
            {
                AskPrice = quote.AskPrice,
                BidPrice = quote.BidPrice,
                AskSize = quote.AskSize,
                BidSize = quote.BidSize,
                Timestamp = quote.Timestamp
            });

            data.TotalTicks++;

            data.DistributionOfTicks.Add(startNano, interval, quote.Timestamp);

            // asks
            ComputeQuoteWithPriceSizeBuilder(quote, true, builder.AskBuilder, data.Ask, startNano, isFirstQuote, isLastQuote);
            // bids
            ComputeQuoteWithPriceSizeBuilder(quote, false, builder.BidBuilder, data.Bid, startNano, isFirstQuote, isLastQuote);

        }
        private static void ComputeTradeWithBuilder(Trade trade, StockNodeDataTradesBuilder builder, StockNodeDataTrades data, long startNano, bool isFirstTrade, bool isLastTrade, Interval interval)
        {
            decimal price = trade.Price;
            int size = trade.Volume;

            data.TotalTicks++;

            data.DistributionOfTicks.Add(startNano, interval, trade.Timestamp);

            ComputeWithPriceBuilder(trade.Timestamp, price, size, builder.PriceBuilder, data.Price, startNano, isFirstTrade, isLastTrade);
            ComputeWithSizeBuilder(trade.Timestamp, price, size, builder.SizeBuilder, data.Size, startNano, isFirstTrade, isLastTrade);
        }

        private static void FinalizePrice(StockNodeDataPriceObject price, StockNodeDataPriceObjectBuilder priceBuilder, long sizeTotal)
        {
            if (priceBuilder._Points.Count > 0)
            {
                decimal totalAskPrice = UC.GetTotalYFromPoints(priceBuilder._Points);
                decimal totalVWAskPrice = UC.GetTotalFromDecimals(priceBuilder._VWPrices);
                price.Average = totalAskPrice / priceBuilder._Points.Count;
                price.AverageChange = UC.CalcAverageYChangeFromPoints(priceBuilder._Points);
                price.AverageVW = totalVWAskPrice / sizeTotal;
                price.BestFitLine = Geometry.Utils.GetBestFitLine(priceBuilder._Points);
                price.Variance = UC.ComputeVariance(priceBuilder._Points, price.Average);
            }
            else
            {
                price.High = -1;
                price.Low = -1;
            }
        }

        private static void FinalizeSize(StockNodeDataSizeObject size, StockNodeDataSizeObjectBuilder sizeBuilder)
        {
            if (sizeBuilder._Points.Count > 0)
            {
                decimal totalAskSize = UC.GetTotalYFromPoints(sizeBuilder._Points);
                size.Average = totalAskSize / sizeBuilder._Points.Count;
                size.AverageChange = UC.CalcAverageYChangeFromPoints(sizeBuilder._Points);
                size.BestFitLine = Geometry.Utils.GetBestFitLine(sizeBuilder._Points);
                size.Variance = UC.ComputeVariance(sizeBuilder._Points, size.Average);
            }
            else
            {
                size.High = -1;
                size.Low = -1;
            }
        }

        public static void FinalizeQuotesBuilders(StockNodeDataQuotesBuilder dqb, StockNodeDataQuotes dq)
        {
            dq.DistributionOfTicks.CalculatePercentages();

            FinalizePrice(dq.Ask.Price, dqb.AskBuilder.PriceBuilder, dq.Ask.Size.Total);

            FinalizeSize(dq.Ask.Size, dqb.AskBuilder.SizeBuilder);

            FinalizePrice(dq.Bid.Price, dqb.BidBuilder.PriceBuilder, dq.Bid.Size.Total);

            FinalizeSize(dq.Bid.Size, dqb.BidBuilder.SizeBuilder);
        }


        public static void FinalizeTradesBuilders(StockNodeDataTradesBuilder dqb, StockNodeDataTrades dq)
        {
            dq.DistributionOfTicks.CalculatePercentages();

            FinalizePrice(dq.Price, dqb.PriceBuilder, dq.Size.Total);

            FinalizeSize(dq.Size, dqb.SizeBuilder);
        }

        

        /// <summary>
        /// Builds a list of StockDataNode (super candles).
        /// The trades and quotes list should always fully fill the StockDataNode (meaning, the last StockDataNode shouldn't be missing data)
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="quotes"></param>
        /// <param name="startNanoSecond">This should be on an 0 hour and 0 minute. Inclusive</param>
        /// <param name="endNanoSecond">This should be on an 0 hour and 0 minute. Exclusive.</param>
        /// <param name="interval"></param>
        /*
        public static List<StockNode> BuildStockNodes(List<Trade> trades, List<Quote> quotes, long startNanoSecond, long endNanoSecond, Interval interval)
        {

            List<StockNode> result = new List<StockNode>();

            long currentNanoSecond = startNanoSecond;

            long nanoSecondSpread = (long)interval;

            int currentTradesIndex = 0;
            int currentQuotesIndex = 0;


            // pretty confident this should hit all the ranges properly
            for (long n = startNanoSecond; n < endNanoSecond; n += nanoSecondSpread)
            {
                // construct an empty StockDataNode here
                StockNode node = new StockNode();
                node.StartNanoSecond = n;
                node.Interval = interval;

                // these builders help with keeping track of totals, etc
                StockNodeDataTradesBuilder stockNodeDataTradesBuilder = new StockNodeDataTradesBuilder();
                // same thing for quotes
                StockNodeDataQuotesBuilder stockNodeDataQuotesBuilder = new StockNodeDataQuotesBuilder();

                // these are to be added to "node" at the end
                StockNodeDataTrades stockNodeDataTrades = new StockNodeDataTrades(interval);
                // same thing for quotes
                StockNodeDataQuotes stockNodeDataQuotes = new StockNodeDataQuotes(interval);

                #region QUOTES

                bool computeQuotes = true;
                bool isFirstQuoteInNode = true;

                // traverse quotes while in the range of [n,n + nanoSecondSpread)
                while (computeQuotes && currentQuotesIndex < quotes.Count)
                {

                    // window of time for this node
                    if (quotes[currentQuotesIndex].Timestamp >= n && quotes[currentQuotesIndex].Timestamp < n + nanoSecondSpread)
                    {
                        Quote quote = quotes[currentQuotesIndex];

                        bool isLastQuoteInNode = (quotes.Count == currentQuotesIndex + 1) || !(quotes[currentQuotesIndex + 1].Timestamp >= n && quotes[currentQuotesIndex + 1].Timestamp < n + nanoSecondSpread);

                        ComputeQuoteWithBuilder(quote, stockNodeDataQuotesBuilder, stockNodeDataQuotes, node.StartNanoSecond, isFirstQuoteInNode, isLastQuoteInNode, interval);

                        currentQuotesIndex++;

                        isFirstQuoteInNode = false;
                    }
                    else
                    {
                        computeQuotes = false;
                    }
                }

                FinalizeQuotesBuilders(stockNodeDataQuotesBuilder, stockNodeDataQuotes);
                node.QuotesData = stockNodeDataQuotes;

                #endregion





                #region TRADES

                bool computeTrades = true;
                bool isFirstTradeInNode = true;
                int totalTradeTicks = 0;

                // traverse trades while in the range of [n,n + nanoSecondSpread)
                while (computeTrades && currentTradesIndex < trades.Count)
                {

                    // window of time for this node
                    if (trades[currentTradesIndex].Timestamp >= n && trades[currentTradesIndex].Timestamp < n + nanoSecondSpread)
                    {
                        Trade trade = trades[currentTradesIndex];

                        bool isLastTradeInNode = (trades.Count == currentTradesIndex + 1) || !(trades[currentTradesIndex + 1].Timestamp >= n && trades[currentTradesIndex + 1].Timestamp < n + nanoSecondSpread);

                        ComputeTradeWithBuilder(trade, stockNodeDataTradesBuilder, stockNodeDataTrades, node.StartNanoSecond, isFirstTradeInNode, isLastTradeInNode, interval);

                        totalTradeTicks++;

                        currentTradesIndex++;

                        isFirstTradeInNode = false;
                    }
                    else
                    {
                        computeTrades = false;
                    }
                }

                FinalizeTradesBuilders(stockNodeDataTradesBuilder, stockNodeDataTrades);
                node.TradesData = stockNodeDataTrades;

                #endregion



                result.Add(node);
            }


            return result;

            // bool allDataConsumed

            // traverse through the trades creating StockNodeData every secondsSpread

            // if there are gaps, create empty StockNodeData items


        }
        */
        #endregion

        public static void DeDupeQuotes(List<Quote> quotes, int parallelism = 17)
        {

            List<IGrouping<long, Quote>> dups = quotes.GroupBy(a => a.Timestamp).Where(a => a.Count() > 1).ToList();

            if (dups.Count > 0)
            {

                Parallel.For(0, dups.Count, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, n => {

                    long val = dups[n].Key;

                    List<Quote> matches = new List<Quote>();

                    for (int m = 0; m < quotes.Count; m++)
                    {
                        if (quotes[m].Timestamp == val)
                        {
                            matches.Add(quotes[m]);
                        }
                    }

                    long middle = (long)Math.Round((double)(matches.Count - 1) / 2);

                    for (int m = 0; m < matches.Count; m++)
                    {
                        matches[m].Timestamp = matches[m].Timestamp + (middle - m);
                    }
                });

                // check for dups again:
                List<IGrouping<long, Quote>> dups2 = quotes.GroupBy(a => a.Timestamp).Where(a => a.Count() > 1).ToList();

                if (dups2.Count > 0)
                {
                    throw new Exception("More dups!");
                }
            }
        }

        public static void DeDupeTrades(List<Trade> trades, int parallelism = 10)
        {
            List<IGrouping<long, Trade>> dups = trades.GroupBy(a => a.Timestamp).Where(a => a.Count() > 1).ToList();

            if (dups.Count > 0)
            {

                Parallel.For(0, dups.Count, new ParallelOptions { MaxDegreeOfParallelism = parallelism }, n => {

                    long val = dups[n].Key;

                    List<Trade> matches = new List<Trade>();

                    

                    for (int m = 0; m < trades.Count; m++)
                    {
                        if (trades[m].Timestamp == val)
                        {
                            matches.Add(trades[m]);
                        }
                    }

                    long middle = (long)Math.Round((double)(matches.Count - 1) / 2);

                    for (int m = 0; m < matches.Count; m++)
                    {
                        matches[m].Timestamp = matches[m].Timestamp + (middle - m);
                    }
                });

                // check for dups again:
                List<IGrouping<long, Trade>> dups2 = trades.GroupBy(a => a.Timestamp).Where(a => a.Count() > 1).ToList();

                if (dups2.Count > 0)
                {
                    throw new Exception("More dups!");
                }
            }
        }
        /*
        /// <summary>
        /// This takes into account overlapping of trades/quotes with stockDataNodes. stockDataNodes are always completely filled with tick data (no missing tick data)
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="stockDataNodes">stockDataNodes are always completely filled with tick data (no missing tick data)</param>
        /// <param name="trades"></param>
        /// <param name="quotes"></param>
        /// <returns>Left over "trades" and "quotes" that were rounded off the last minute</returns>
        public static Dictionary<string, object> AppendTicksToStockDataNodes(string symbol, List<StockNode> stockNodes, List<Trade> trades, List<Quote> quotes)
        {

            List<Trade> leftOverTrades = new List<Trade>();
            List<Quote> leftOverQuotes = new List<Quote>();

            if (quotes.Count > 0 || trades.Count > 0) {

                StockNode lastStockDataNode = stockNodes.Last();

                // resumeTimestamp is rounded up to the next minute (:00) of the lastStockDataNode
                long resumeTimestamp = lastStockDataNode.StartNanoSecond + ((long)lastStockDataNode.SecondsSpread * (long)1000000000);

                // lastEvenMinuteTimestamp is a rounded down to the minute (:00)
                long lastEvenMinuteTimestamp = GetLastEvenMinuteStartTimestamp(trades, quotes);

                // only use the ticks from [resumeTimestamp, lastEvenMinuteTimestamp)
                List<StockNode> chunk = BuildStockNodes(trades, quotes, resumeTimestamp, lastEvenMinuteTimestamp, 60);

                stockNodes.AddRange(chunk);


                int tradeChopIndex = -1;

                for (int n = trades.Count - 1; n >= 0; n--)
                {
                    if (trades[n].Timestamp >= lastEvenMinuteTimestamp)
                    {
                        tradeChopIndex = n;
                    }
                    else
                    {
                        break;
                    }
                }

                if (tradeChopIndex != -1)
                {
                    leftOverTrades.AddRange(trades.GetRange(tradeChopIndex, trades.Count - tradeChopIndex));
                }

                int quoteChopIndex = -1;

                for (int n = quotes.Count - 1; n >= 0; n--)
                {
                    if (quotes[n].Timestamp >= lastEvenMinuteTimestamp)
                    {
                        quoteChopIndex = n;
                    }
                    else
                    {
                        break;
                    }
                }

                if (quoteChopIndex != -1)
                {
                    leftOverQuotes.AddRange(quotes.GetRange(quoteChopIndex, quotes.Count - quoteChopIndex));
                }

            }


            Dictionary<string, object> leftOverTicks = new Dictionary<string, object>();

            leftOverTicks.Add("trades", leftOverTrades);
            leftOverTicks.Add("quotes", leftOverQuotes);

            // return the ticks that we left over after lastEvenMinuteTimestamp
            return leftOverTicks;

        }
        */


            
            /*
        public static void UpdateStockNodesToLatest(string symbol, List<StockNode> stockNodes, bool streamed, long ticksEndNano = -1)
        {
            StockNode lastStockNode = stockNodes.Last();

            List<Trade> trades = DB.Methods.GetTradesBySymbol(symbol, lastStockNode.EndNanoSecond, ticksEndNano, streamed);
            List<Quote> quotes = DB.Methods.GetQuotesBySymbol(symbol, lastStockNode.EndNanoSecond, ticksEndNano, streamed);

            if (ticksEndNano == -1)
            {
                ticksEndNano = GetLastEvenMinuteStartTimestamp(trades, quotes);
            }

            List<StockNode> freshDataNodes = DataLayer.BuildStockNodes(trades, quotes, lastStockNode.EndNanoSecond, ticksEndNano, 60);

            stockNodes.AddRange(freshDataNodes);

        }
        */

        /*
        /// <summary>
        /// Chops off the last minute. This is done because the last minute data could be partial data for that minute.
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="quotes"></param>
        public static void ChopEndTicksAtEvenMinute(List<Trade> trades, List<Quote> quotes, long evenMinuteTimestamp)
        {

            
            int quoteChopIndex = -1;

            for (int n = quotes.Count - 1; n >= 0; n--)
            {
                if (quotes[n].Timestamp >= evenMinuteTimestamp)
                {
                    quoteChopIndex = n;
                }
                else
                {
                    break;
                }
            }

            quotes.RemoveRange(quoteChopIndex, quotes.Count - quoteChopIndex);

            int tradeChopIndex = -1;

            for (int n = trades.Count - 1; n >= 0; n--)
            {
                if (trades[n].Timestamp >= evenMinuteTimestamp)
                {
                    tradeChopIndex = n;
                }
                else
                {
                    break;
                }
            }

            trades.RemoveRange(tradeChopIndex, trades.Count - tradeChopIndex);
            
        }
        */

        /// <summary>
        /// Get's the most recent time stamp between trades and quotes.
        /// Rounds down to the nearest minute.
        /// So 1.6246246 minutes would return 1 minute in nanoseconds.
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="quotes"></param>
        /// <returns></returns>
        public static long GetLastEvenMinuteStartTimestamp(List<Trade> trades, List<Quote> quotes)
        {
            long lastTradeTimestamp = trades.Count > 0 ? trades.Last().Timestamp : 0;
            long lastQuoteTimestamp = quotes.Count > 0 ? quotes.Last().Timestamp : 0;

            long latestTimestamp = (lastTradeTimestamp > lastQuoteTimestamp ? lastTradeTimestamp : lastQuoteTimestamp);

            long lastEvenMinuteStartTimestamp = latestTimestamp - (latestTimestamp % 60000000000);

            return lastEvenMinuteStartTimestamp;
        }

        /// <summary>
        /// Get's the most recent time stamp between trades and quotes.
        /// Rounds up to the nearest minute.
        /// So 1.6246246 minutes would return 2 minutes in nanoseconds.
        /// </summary>
        /// <param name="trades"></param>
        /// <param name="quotes"></param>
        /// <returns></returns>
        public static long GetLastEvenMinuteEndTimestamp(List<Trade> trades, List<Quote> quotes)
        {
            long lastTradeTimestamp = trades.Count > 0 ? trades.Last().Timestamp : 0;
            long lastQuoteTimestamp = quotes.Count > 0 ? quotes.Last().Timestamp : 0;

            long latestTimestamp = (lastTradeTimestamp > lastQuoteTimestamp ? lastTradeTimestamp : lastQuoteTimestamp);

            long lastEvenMinuteEndTimestamp = (latestTimestamp - (latestTimestamp % 60000000000L)) + 60000000000L;

            return lastEvenMinuteEndTimestamp;
        }

        /*
        public static int TradesFindFirstIndexByTimestamp(List<Trade> trades, long timestamp, int start, int end)
        {
            int checkIndex = Math.Floor((double)(end - start) / 2);
        }
        */
    }

    
}
