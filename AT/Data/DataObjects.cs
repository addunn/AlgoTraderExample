using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AT.Geometry.Objects;
using MessagePack;
using AT.AlgoTraderEnums;
using System.Collections;

namespace AT.DataObjects
{


    public class SequencesDataBuildOptions
    {
        // what to build
        public HashSet<SequenceKey> SequenceKeys = new HashSet<SequenceKey>();

        public long Interval = 0;
    }

    [MessagePackObject]
    public class GapObject
    {
        [Key(1)]
        public long NanoGreaterThanOrEqual = 0L;

        [Key(2)]
        public long NanosMissing = 0L;
    }



    [MessagePackObject]
    public class NodesData
    {
        [Key(1)]
        public List<Node> Nodes;

        [Key(2)]
        public long NodeInterval; // = 500000000L; // half second

        [Key(3)]
        public long Start;

        [Key(4)]
        public long End;

        [Key(5)]
        public int FirstNonEstimatedTradePrice = -1;
        [Key(6)]
        public int LastNonEstimatedTradePrice = -1;

        [Key(7)]
        public int FirstNonEstimatedBidPrice = -1;
        [Key(8)]
        public int LastNonEstimatedBidPrice = -1;

        [Key(9)]
        public int FirstNonEstimatedAskPrice = -1;
        [Key(10)]
        public int LastNonEstimatedAskPrice = -1;

        [Key(11)]
        List<GapObject> Gaps = new List<GapObject>();

        public Node FindNodeForNano(long nanoSecond)
        {
            long nodesNanoSpan = NodeInterval * (long)Nodes.Count;

            int index = (int)Math.Floor((decimal)(((nanoSecond - Start) / nodesNanoSpan) * (long)Nodes.Count));

            return Nodes[index];
        }

        public void ComputeNodesNanos()
        {
            for (int n = 0; n < Nodes.Count; n++)
            {
                Nodes[n].StartNano = (n * NodeInterval) + Start;
                Nodes[n].EndNano = Nodes[n].StartNano + NodeInterval;
            }
        }


        /// <summary>
        /// Returns a new NodesData with the nodes inside the range [start to end). See GetNodesWithNanos().
        /// </summary>
        /// <param name="start"></param>
        /// <param name="end"></param>
        /// <returns></returns>
        public NodesData SliceSpawnWithNanos(long start, long end)
        {
            NodesData result = new NodesData();

            List<Node> nodes = GetNodesWithNanos(start, end);

            if (nodes.Count == 0)
            {
                throw new Exception("Returned zero nodes. Is that expected?");
            }


            if ((nodes[nodes.Count - 1].EndNano - nodes[0].StartNano) % NodeInterval != 0)
            {
                throw new Exception("NodeInterval won't divide cleanly into the difference between start and end.");
            }

            result.Start = nodes[0].StartNano;

            result.End = nodes[nodes.Count - 1].EndNano;

            result.NodeInterval = NodeInterval;

            result.Nodes = nodes;


            int checkCount = 0;
            for (int n = 0; n < nodes.Count; n++)
            {
                if (!nodes[n].TradePriceEstimated && result.FirstNonEstimatedTradePrice == -1)
                {
                    result.FirstNonEstimatedTradePrice = n;
                    checkCount++;
                }
                if (!nodes[n].BidPriceEstimated && result.FirstNonEstimatedBidPrice == -1)
                {
                    result.FirstNonEstimatedBidPrice = n;
                    checkCount++;
                }
                if (!nodes[n].AskPriceEstimated && result.FirstNonEstimatedAskPrice == -1)
                {
                    result.FirstNonEstimatedAskPrice = n;
                    checkCount++;
                }
                if (checkCount == 3)
                {
                    break;
                }
            }

            checkCount = 0;


            for (int n = nodes.Count - 1; n >= 0; n--)
            {
                if (!nodes[n].TradePriceEstimated && result.LastNonEstimatedTradePrice == -1)
                {
                    result.LastNonEstimatedTradePrice = n;
                    checkCount++;
                }
                if (!nodes[n].BidPriceEstimated && result.LastNonEstimatedBidPrice == -1)
                {
                    result.LastNonEstimatedBidPrice = n;
                    checkCount++;
                }
                if (!nodes[n].AskPriceEstimated && result.LastNonEstimatedAskPrice == -1)
                {
                    result.LastNonEstimatedAskPrice = n;
                    checkCount++;
                }
                if (checkCount == 3)
                {
                    break;
                }
            }



            return result;
        }

        /// <summary>
        /// Get all the nodes where the time span of the nodes fits completely between start and end.
        /// If start param is in the middle of a node, it won't return that node.
        /// Same for end param.
        /// </summary>
        /// <param name="start">Inclusive</param>
        /// <param name="end">Exclusive</param>
        /// <returns></returns>
        public List<Node> GetNodesWithNanos(long start, long end)
        {

            /*
             ok... i don't know why this was made so fancy..
             ... all we have to do is match start with the start of a node, and end with the end of the node...
                ... and return the range
                ... no need for binary search
                ... just go directly to the index using nanos
                ... assume whole numbers happen... if they don't it will throw an error which is good
            */


            // each node has width of the interval

            // the index of start should be: start - Start


            /*
            int startIndex = (int)((decimal)(start - Start) / (decimal)NodeInterval);
            int endIndex = (int)((decimal)(end - Start) / (decimal)NodeInterval) - 1;
            int count = (endIndex - startIndex) + 1;

            List<Node> result = Nodes.GetRange(startIndex, count);
            */







            // need startindex and endindex from start and end
            //int startIndex = GetNodeIndexByStartNano(start);
            //int endIndex = GetNodeIndexByStartNano(end - NodeInterval);

            //int count = (endIndex - startIndex) + 1;

            long startGapFactor = GetGapFactor(start);
            long endGapFactor = GetGapFactor(end);


            int startIndex = (int)((decimal)((start - Start) - startGapFactor) / (decimal)NodeInterval);
            int endIndex = (int)((decimal)((end - Start) - endGapFactor) / (decimal)NodeInterval) - 1;

            int count = (endIndex - startIndex) + 1;

            List<Node> result = Nodes.GetRange(startIndex, count);


            return result;

            /*
            long nodesNanoSpan = NodeInterval * (long)Nodes.Count;

            

            decimal dA1 = (decimal)(start - Start) / (decimal)nodesNanoSpan;
            decimal dA1BeforeTruncate = dA1;
            if (UC.GetDecimalPlaces(dA1) == 28)
            {
                dA1 = UC.TruncateDecimal(dA1, 27);
                //dA1 = Math.Truncate(dA1 * 10000000000000000000L) / 10000000000000000000L;
            }
            // 1000000000000000000000000000
            decimal dA2 = (decimal)(dA1 * (decimal)Nodes.Count);

            decimal dB1 = (decimal)(end - Start) / (decimal)nodesNanoSpan;

            //decimal dB1BeforeTruncate = dB1;

            //if (UC.GetDecimalPlaces(dB1) == 28)
            //{
             //   dB1 = UC.TruncateDecimal(dB1, 27);
            //    bool breakme = true;
                //dB1 = Math.Truncate(dB1 * 10M) / 10M;
           ///}
            decimal dB2 = (decimal)(dB1 * (decimal)Nodes.Count);

            // ceiling(4) is 4.
            int startIndex = (int)Math.Ceiling(dA2);

            int dB2Int = (int)Math.Floor(dB2);
            if(dB2 - (decimal)dB2Int < 0.99999999M)
            {
                dB2Int--;
            }


            int endIndex = dB2Int;

            int count = (endIndex - startIndex) + 1;

            if (endIndex < startIndex || endIndex >= Nodes.Count || startIndex < 0)
            {
                throw new Exception("Indices are messed up.");
            }

            if((end - start) % (long)NodeInterval != 0)
            {
                throw new Exception("NodeInterval won't divide cleanly into the difference between start and end.");
            }
            */

        }
        public long GetGapFactor(long nano)
        {
            long result = 0L;

            for (int n = 0; n < Gaps.Count; n++)
            {
                if (nano >= Gaps[n].NanoGreaterThanOrEqual)
                {
                    result = Gaps[n].NanosMissing;
                }
                else
                {
                    break;
                }
            }

            return result;
        }
        /*
        public int GetNodeIndexByStartNano(decimal start)
        {
            int index = (int)((start - Start) / NodeInterval); // works perfectly if no gaps

            decimal gapFactor = GetGapFactorByStartNano(start);

            int indexAdjustment = (int)(gapFactor / NodeInterval);

            index -= indexAdjustment;

            return index;

        }

        public decimal GetGapFactorByStartNano(decimal start)
        {
            decimal gapFactor = 0L;

            // this could potentially be reversed?
            for (int n = 0; n < Gaps.Count; n++)
            {
                if (start >= Gaps[n].Start)
                {
                    gapFactor = Gaps[n].Missing;
                }
                else
                {
                    break;
                }
            }

            return gapFactor;
        }
        */

        public void AttachNodesData(NodesData attach)
        {
            DataMethods.AttachNodesData(attach, this);

            ComputeGaps();

        }
        public void ComputeGaps()
        {
            Gaps.Clear();


            long totalMissingNanoSeconds = 0L;

            for(int n = 1; n < Nodes.Count; n++)
            {
                if(Nodes[n].StartNano - Nodes[n - 1].EndNano < 0)
                {
                    //throw new Exception("Seems some nodes overlap.");
                }

                // if the difference between the two nodes isn't the time span of node interval... we have some missing nanoseconds
                if(Nodes[n].StartNano - Nodes[n - 1].EndNano > 0)
                {
                    long diff = Nodes[n].StartNano - Nodes[n - 1].EndNano;

                    if(diff % NodeInterval != 0)
                    {
                        throw new Exception("Difference in time between nodes isn't divisible evenly by NodeInterval.");
                    }
                    else
                    {
                        totalMissingNanoSeconds += diff;
                        Gaps.Add(new GapObject() { NanoGreaterThanOrEqual = Nodes[n].StartNano, NanosMissing = totalMissingNanoSeconds });
                    }
                }
            }

        }

    }



    [MessagePackObject]
    public class SequenceGap
    {
        /// <summary>
        /// The index that the gap starts on
        /// </summary>
        [Key(1)]
        public int StartIndex;

        /// <summary>
        /// The start nano second (or price amount) that the gap starts on
        /// </summary>
        [Key(2)]
        public decimal Start;

        /// <summary>
        /// amount of nano seconds (or price amount) that need to be adjusted
        /// </summary>
        [Key(3)]
        public decimal Missing;
    }

    [MessagePackObject]
    public class Node
    {
        [Key(1)]
        public decimal TradePrice;

        [Key(2)]
        public int TradeVolume;

        [Key(3)]
        public decimal AskPrice;

        [Key(4)]
        public decimal BidPrice;

        [Key(5)]
        public bool TradePriceEstimated = true;

        [Key(6)]
        public bool AskPriceEstimated = true;

        [Key(7)]
        public bool BidPriceEstimated = true;

        // these are computed in NodesData
        [IgnoreMember]
        public long StartNano;
        [IgnoreMember]
        public long EndNano;
    }


    [MessagePackObject]
    public class OldNode
    {
        [Key(1)]
        public decimal TradePrice;

        [Key(2)]
        public int TradeVolume;

        [Key(3)]
        public decimal AskPrice;

        [Key(4)]
        public decimal BidPrice;
        
        [Key(5)]
        public bool TradePriceEstimated = true;

        [Key(6)]
        public bool AskPriceEstimated = true;

        [Key(7)]
        public bool BidPriceEstimated = true;

        // these are computed in NodesData
        [Key(8)]
        public long StartNano;

        [Key(9)]
        public long EndNano;
    }
    


    public class TradingStat
    {
        public string StrategyId = null;
        public string Symbol = null;
        public long OpenTimestamp = -1;
        public long CloseTimestamp = -1;

        public PositionSide PositionSide = PositionSide.Unknown;

        public decimal EntryPrice = -1;
        public decimal ExitPrice = -1;

        // includes OpenTimestamp interval and CloseTimestamp interval
        public int SpanAverageTradeVolume = -1;

    }
    public class Trade
    {
        // t
        // nanosecond unix stamp of when this trade occured
        public long Timestamp;

        // x
        public int ExchangeId;

        // s
        public int Volume;

        // c
        public string Conditions;

        private string[] _conditionsArray;

        public string[] ConditionsArray
        {
            get
            {
                if (_conditionsArray == null)
                {
                    _conditionsArray = Conditions.Split(',');
                    if (_conditionsArray.Length == 1 && String.IsNullOrWhiteSpace(_conditionsArray[0]))
                    {
                        _conditionsArray = new string[0];
                    }
                }
                return _conditionsArray;
            }
        }

        // p
        public decimal Price;

        // z (1,2 = CTA, 3 = UTP)
        public int Tape;
    }

    [MessagePackObject]
    public class QuoteLite
    {
        [Key(0)]
        public long Timestamp;

        [Key(1)]
        public decimal AskPrice;

        [Key(2)]
        public int AskSize;

        [Key(3)]
        public decimal BidPrice;

        [Key(4)]
        public int BidSize;
        
    }

    public class Quote
    {
        // t
        // nanosecond unix stamp of when this trade occured
        public long Timestamp;

        private string[] _conditionsArray;

        public string[] ConditionsArray
        {
            get
            {
                if (_conditionsArray == null)
                {
                    _conditionsArray = Conditions.Split(',');
                    if (_conditionsArray.Length == 1 && String.IsNullOrWhiteSpace(_conditionsArray[0]))
                    {
                        _conditionsArray = new string[0];
                    }
                }
                return _conditionsArray;
            }
        }

        // c
        public string Conditions;

        // p
        public decimal BidPrice;

        // x
        public int BidExchangeId;

        // s
        public int BidSize; // in "round lots"

        // P
        public decimal AskPrice;

        // X
        public int AskExchangeId;

        // S
        public int AskSize; // in "round lots"

        // z
        public int Tape;
    }
    /*
    [MessagePackObject]
    public class StockNode
    {
        // There should be a StockNodeData for "ALL", "MAPPED", and each condition

        // maybe add conditions stats here later

        // should add some useful properties here that take quotes and trades data into account.
        [Key(0)]
        public long StartNanoSecond;

        [Key(1)]
        public Interval Interval;

        [Key(2)]
        public StockNodeDataTrades TradesData = null;

        [Key(3)]
        public StockNodeDataQuotes QuotesData = null;

        /// <summary>
        /// Get's the end of this node (exclusive).
        /// This is always the same as the next StartNanoSecond in a list of StockDataNode
        /// </summary>
        [IgnoreMember]
        public long EndNanoSecond
        {
            get { return StartNanoSecond + (long)Interval; }
        }

        public bool IsEmpty()
        {
            return (TradesData == null || TradesData.IsEmpty()) && (QuotesData == null || QuotesData.IsEmpty());
        }

        public StockNode()
        {

        }
    }
    */

    [MessagePackObject]
    public class DistributionOfTicks
    {
        // don't serialize these... this object can't be used if deserialized

        [IgnoreMember]
        private int[] Counts;

        [IgnoreMember]
        private int Total = 0;

        [Key(0)]
        public float[] Percentages;

        /// <summary>
        /// Makes serializer happy
        /// </summary>
        public DistributionOfTicks() { }

        public DistributionOfTicks(Interval interval)
        {
            int secondsSpread = (int)(((long)interval / 500000000L));
            int start = (int)Math.Floor((decimal)(secondsSpread / 10));
            int slots = 0;
            for (int n = start; n > 0; n--)
            {
                if (secondsSpread % n == 0)
                {
                    slots = secondsSpread / n;
                    break;
                }
            }
            Counts = new int[slots];
        }
        public void Add(long startNano, Interval interval, long timestamp)
        {
            int slots = Counts.Length;

            long nanoSpread = (long)interval;

            long nanoSecondsPerSlot = nanoSpread / (long)slots;

            long n1 = timestamp - startNano;

            int slot = (int)Math.Floor((decimal)(n1 / nanoSecondsPerSlot));

            Counts[slot]++;
            Total++;
        }

        /// <summary>
        /// Finalizes this object after calculating percentages
        /// </summary>
        public void CalculatePercentages()
        {
            Percentages = new float[Counts.Length];

            for (int n = 0; n < Counts.Length; n++)
            {
                Percentages[n] = (float)Counts[n] / (float)Total;
            }
        }
    }
    /// <summary>
    /// Keeps a common set of variables for Quotes (asks and bids) and Trades
    /// </summary>
    [MessagePackObject]
    public class StockNodeDataPriceObject
    {
        [Key(0)]
        public decimal Open = -1;

        [Key(1)]
        public decimal Close = -1;

        [Key(2)]
        public decimal Low = decimal.MaxValue;

        [Key(3)]
        public decimal High = decimal.MinValue;

        [Key(4)]
        public decimal Average = -1; // need points

        [Key(5)]
        public decimal AverageVW = -1; // need list of VW prices

        [Key(6)]
        public decimal AverageChange = -1; // need points

        [Key(7)]
        public decimal Variance = -1; // need points

        [Key(8)]
        public Line BestFitLine = null; // need points
    }

    // size can be volume or a rounded lot, depends on where this is added
    [MessagePackObject]
    public class StockNodeDataSizeObject
    {
        [Key(0)]
        public int Open = -1;

        [Key(1)]
        public int Close = -1;

        [Key(2)]
        public int High = int.MinValue;

        [Key(3)]
        public int Low = int.MaxValue;

        [Key(4)]
        public long Total = 0;

        [Key(5)]
        public decimal Average = -1; // needs points

        [Key(6)]
        public decimal AverageChange = -1; // needs points

        [Key(7)]
        public decimal Variance = -1; // needs points

        [Key(8)]
        public Line BestFitLine = null; // needs points
    }

    [MessagePackObject]
    public class StockNodeDataPriceSizeObject
    {
        [Key(0)]
        public StockNodeDataPriceObject Price = new StockNodeDataPriceObject();

        [Key(1)]
        public StockNodeDataSizeObject Size = new StockNodeDataSizeObject();

    }

    [MessagePackObject]
    public class StockNodeDataObject
    {
        [Key(0)]
        public int TotalTicks = 0;

        [Key(1)]
        public DistributionOfTicks DistributionOfTicks; // percent of ticks that fall into a 5s interval. [0] is [0, 5) seconds, [1] is [5, 10), etc

        public bool IsEmpty() { return TotalTicks == 0; }

        public StockNodeDataObject(Interval interval)
        {
            DistributionOfTicks = new DistributionOfTicks(interval);
        }
        public StockNodeDataObject()
        {

        }
    }

    [MessagePackObject]
    public class StockNodeDataQuotes : StockNodeDataObject
    {
        [Key(2)]
        public List<QuoteLite> DebugQuotes = new List<QuoteLite>();

        [Key(3)]
        public StockNodeDataPriceSizeObject Ask = new StockNodeDataPriceSizeObject();

        [Key(4)]
        public StockNodeDataPriceSizeObject Bid = new StockNodeDataPriceSizeObject();

        public StockNodeDataQuotes(Interval interval) : base(interval)
        {

        }
        public StockNodeDataQuotes()
        {

        }
    }


    [MessagePackObject]
    public class StockNodeDataTrades : StockNodeDataObject
    {
        [Key(2)]
        public StockNodeDataPriceObject Price = new StockNodeDataPriceObject();

        [Key(3)]
        public StockNodeDataSizeObject Size = new StockNodeDataSizeObject();

        public StockNodeDataTrades(Interval interval) : base(interval)
        {

        }
        public StockNodeDataTrades()
        {

        }
    }

    public class StockNodeDataPriceObjectBuilder
    {
        public List<Point> _Points = new List<Point>();
        public List<decimal> _VWPrices = new List<decimal>();
    }
    public class StockNodeDataSizeObjectBuilder
    {
        public List<Point> _Points = new List<Point>();
    }
    public class StockNodeDataPriceSizeObjectBuilder
    {
        public StockNodeDataPriceObjectBuilder PriceBuilder = new StockNodeDataPriceObjectBuilder();
        public StockNodeDataSizeObjectBuilder SizeBuilder = new StockNodeDataSizeObjectBuilder();
    }

    // these builders are for keeping track of data when creating a StockNode in BuildStockNodes
    public class StockNodeDataQuotesBuilder
    {
        public StockNodeDataPriceSizeObjectBuilder AskBuilder = new StockNodeDataPriceSizeObjectBuilder();
        public StockNodeDataPriceSizeObjectBuilder BidBuilder = new StockNodeDataPriceSizeObjectBuilder();

        public StockNodeDataQuotesBuilder()
        {

        }
    }
    public class StockNodeDataTradesBuilder
    {
        public StockNodeDataPriceObjectBuilder PriceBuilder = new StockNodeDataPriceObjectBuilder();
        public StockNodeDataSizeObjectBuilder SizeBuilder = new StockNodeDataSizeObjectBuilder();

        public StockNodeDataTradesBuilder()
        {

        }
    }
}
