using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT
{

    public enum SequenceKey
    {
        // (average_ask, avarege_trade, volume_weighted_average_trade, average_bid, total_volume, open_trade, close_trade, high_trade, low_trade)
        Unknown = 0,
        AskPriceAvg = 1,
        BidPriceAvg = 2,
        TradePriceAvg = 3,
        TradePriceVolumeWeightedAvg = 4,
        TradePriceOpen = 5,
        TradePriceClose = 6,
        TradePriceHigh = 7,
        TradePriceLow = 8,

        TradeVolumeTotal = 9,

        // computed sequence, should this be here?
        EMA10Day = 50,
        VolumePerOneCent = 60
    }

    /*
    public enum SequenceGroup
    {
        Unknown = 0,
        Main = 1,
        MainVolume = 2,
        OscillatorZeroToOne = 3
    }
    */

    public enum NodeFlag
    {
        TradePriceEstimated = 0,
        AskPriceEstimated = 1,
        BidPriceEstimated = 2
    }


    /// <summary>
    /// Span of time in nanoseconds
    /// </summary>
    public enum Interval: long
    {
        HalfSecond = 500000000,
        OneSecond = 1000000000,
        OneMinute = 60000000000,
        FiveMinutes = 300000000000,
        FifteenMinutes = 900000000000,
        HalfHour = 1800000000000,
        OneHour = 3600000000000
    }

    public enum ThreadControlState
    {
        Waiting = 0,
        Running = 1,
        Stopping = 2,
        Stopped = 3,
        Complete = 4, // if it wasn't stopped and ran all the way through
        Done = 5 // if it was stopped
    }

    public enum Signal
    {
        Empty = 0,
        Stop = 1,
        Done = 2,
        Process = 3,
        FormulateActions = 4,
        AllowSignalingManagers = 5,
        EOTD = 6,
        UpdateOrders = 7,
        ProcessStrategyActions = 8
    }
    public enum Verbosity
    {
        Minimal = 1,
        Normal = 2,
        Verbose = 3
    }
}
