
namespace AT.AlgoTraderEnums
{

    public enum AlgoTraderSimulatingSpeed
    {
        Unknown = 0,
        Paused = 1,
        RealTime = 2,
        Fast = 3,
        Faster = 4,
        EvenFaster = 5,
        Fastest = 6
    }
    public enum AlgoTraderModes
    {
        /// <summary>
        /// Runs all symbols, all strategies, all days and stores result in TradingStats DB.
        /// This mode's goal is to performance stats on the strategies to be used for selecting strategies in other modes.
        /// Respects the limit of only one position open per strategy/symbol at a time.
        /// OrderManager disregards risk management, sizing calculations and strategy/symbol probability. It just does 1 share trades.
        /// </summary>
        GatherStats = 0,

        /// <summary>
        /// Simulates exactly how live trading would behave (strategically selecting strategies, risk management, etc).
        /// Takes in days. Can be training days for training or testing days for testing.
        /// Stores a final pnl report at the end.
        /// </summary>
        Backtesting = 1,

        /// <summary>
        /// Date range, no testing days, speed can be adjusted, can watch trading in UI.
        /// </summary>
        Simulating = 2,

        /// <summary>
        /// Works on live data realtime, but no order API calls
        /// </summary>
        FakeLive = 3,

        /// <summary>
        /// Works on live data realtime, but API calls go to paper API
        /// </summary>
        Paper = 4,

        /// <summary>
        /// Works on live data realtime, API and money is very real
        /// </summary>
        Live = 5
    }

    public enum OrderType
    {
        Unknown = 0,
        Market = 1,
        Limit = 2,
        Stop = 3,
        StopLimit = 4
    }
    public enum OrderSide
    {
        Unknown = 0,
        Buy = 1,
        Sell = 2
    }
    public enum OrderStatus
    {
        Unknown = 0,

        /// <summary>
        /// Created in application, but not yet sent to API
        /// </summary>
        WaitingToSendToAPI = 1,

        /// <summary>
        /// Sent to API but not yet routed to exchanges
        /// </summary>
        Accepted = 2,

        /// <summary>
        /// Sent to API, routed to exchanges, but has not been accepted for execution.
        /// </summary>
        PendingSubmitted = 3,

        /// <summary>
        /// Sent to API and routed to exchanges for execution
        /// </summary>
        Submitted = 4,

        /// <summary>
        /// The order has been filled, and no further updates will occur for the order.
        /// </summary>
        Filled = 5,

        /// <summary>
        /// The order has been partially filled, it will remain on the market until completely filled.
        /// </summary>
        PartiallyFilled = 6,

        /// <summary>
        /// The order is done executing for the day, and will not receive further updates until the next trading day.
        /// </summary>
        DoneForDay = 7,

        /// <summary>
        /// The order has been canceled, and no further updates will occur for the order. This can be either due to a cancel request by the user, or the order has been canceled by the exchanges due to its time-in-force.
        /// </summary>
        Cancelled = 8,

        /// <summary>
        /// The order has expired, and no further updates will occur for the order.
        /// </summary>
        Expired = 9,

        /// <summary>
        /// The order has been rejected, and no further updates will occur for the order. This state occurs on rare occasions and may occur based on various conditions decided by the exchanges.
        /// </summary>
        Rejected = 10,

        /// <summary>
        /// The order has been suspended, and is not eligible for trading. This state only occurs on rare occasions.
        /// </summary>
        Suspended = 11,

        /// <summary>
        /// When the API responds with a status and it doesn't fall in other categories
        /// </summary>
        UnknownAPIStatus = 12,

        PendingCancel = 13
    }
    public enum OrderActionType
    {
        Unknown = 0,
        Cancel = 1,
        Place = 2
    }

    public enum PositionStatus
    {
        Unknown = 0,
        Pending = 1,
        Open = 2,
        Closed = 3
    }
    public enum PositionSide
    {
        Unknown = 0,
        Long = 1,
        Short = 2
    }
    public enum StrategyOrderActionsStatus
    {
        Unknown = 0,
        PendingDecision = 1,
        Rejected = 2,
        Approved = 3,
        Executed = 4
    }


}
