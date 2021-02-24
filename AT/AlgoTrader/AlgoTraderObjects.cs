using System;
using System.Collections.Generic;
using System.Threading;
using AT.DataObjects;
using AT.AlgoTraderEnums;
using AT.AlgoTraderStrategies;
using NodaTime;
using System.Globalization;

namespace AT.AlgoTraderObjects
{

    public class DataTableSort
    {
        public string Direction = "";
        public string Column = "";
    }

    /*
    public class SymbolData
    {
        public List<StockNode> StockNodes = new List<StockNode>();



        
            - significant negative correlated stocks
            - significant positive correlated stocks
                - we would need historical correlation values from the last 30 days or so
            - trend lines
            - support/resistance
            - indicators (ema, vwap, volitility, etc)
            - momentum
            - oscillators
            - divergance from some other symbol

         
    }
    */

    public class CurrentTime {

        private ZonedDateTime Time;

        private bool IsSim = true;

        private static readonly object LockObj = new object();

        private int IncrementMinutes = 1;

        private int CurrentMinute = 0;

        private int LastMinute = 0;

        public void RefreshTime()
        {
            // lock is set in wrapping calling code (AlgoTraderProcess.TimerCallback)
            lock (LockObj)
            {
                if (!AlgoTraderState.PauseSim)
                {
                    if (IsSim)
                    {
                        if (AlgoTraderState.TradingManagerRunning)
                        {
                            AlgoTraderMethods.Pause();
                        }
                        Time = Time.PlusMinutes(IncrementMinutes);
                    }
                    else
                    {
                        Time = UCDT.GetCurrentEastCoastTime();
                    }

                    CurrentMinute = Time.Minute;                    
                }
            }

        }

        public bool IsNewMinute()
        {
            lock (LockObj)
            {
                if (CurrentMinute != LastMinute)
                {
                    AlgoTraderMethods.Pause();
                    LastMinute = CurrentMinute;
                    return true;
                }
                else
                {
                    AlgoTraderMethods.UnPause();
                    return false;
                }
            }
        }

        public FD GetFD()
        {
            return new FD(Time.Year, Time.Month, Time.Day);
        }

        public ZonedDateTime GetTime()
        {
            lock (LockObj)
            {
                return Time;
            }
        }

        public long GetDateTimeNano()
        {
            long nano = 0;

            lock (LockObj)
            {
                nano = UCDT.DateTimeToNanoUnix(Time.ToDateTimeUtc());
            }

            return nano;
        }

        public int GetMinute()
        {
            lock (LockObj)
            {
                return Time.Minute;
            }
        }

        public int GetHour()
        {
            lock (LockObj)
            {
                return Time.Hour;
            }
        }

        public bool IsBetween(int startHour, int startMinute, int endHour, int endMinuteExclusive)
        {
            bool result = false;

            lock (LockObj)
            {
                result = 
                    ((Time.Hour == startHour && Time.Minute >= startMinute) || (Time.Hour > startHour)) 
                    &&
                    ((Time.Hour == endHour && Time.Minute < endMinuteExclusive) || (Time.Hour < endHour));
            }

            return result;
        }

        public bool IsOn(int hour, int minute)
        {
            bool result = false;

            lock (LockObj)
            {
                result = (Time.Hour == hour && Time.Minute == minute);
            }

            return result;
        }

        public bool IsOnOrPassed(int hour, int minute)
        {
            bool result = false;

            lock (LockObj)
            {
                result = (Time.Hour == hour && Time.Minute >= minute) || (Time.Hour > hour);
            }

            return result;
        }

        public override string ToString()
        {
            
            return Time.ToString("yyyy/MM/dd HH:mm:ss", CultureInfo.InvariantCulture);
        }

        public CurrentTime(bool isSim, ZonedDateTime initialValue)
        {
            IsSim = isSim;
            Time = initialValue;
        }
    }





    /// <summary>
    /// When live, the strategies combined with a symbol will be strategically picked.
    /// When gathering stats, the strategies will most likely be all strategies
    /// </summary>
    public class WatchItem
    {
        public string Symbol = null;
        public List<Strategy> Strategies = null;
    }



    public class OrderAction
    {
        // cancel, place
        public OrderActionType Type = OrderActionType.Unknown;

        public OrderType OrderType = OrderType.Unknown;

        public OrderSide OrderSide = OrderSide.Unknown;

        public int Quantity = 0;

        public decimal LimitPrice = -1M;

        public decimal StopPrice = -1M;

        // for canceling
        public string OrderId = "";

        public string TimeInForce = "day";
    }


    /// <summary>
    /// This is the action that is passed to the Trading Manager, and then Order Manager
    /// </summary>
    [Serializable]
    public class StrategyOrderActions
    {
        public string Symbol = "";
        public string StrategyId = "";
        public string StrategyName = "";

        // empty if starting a new position
        public string PositionId = "";

        // not unknown if starting a new position
        public PositionSide PositionSide = PositionSide.Unknown;

        public StrategyOrderActionsStatus Status = StrategyOrderActionsStatus.PendingDecision;

        public List<OrderAction> OrderActions = null;
    }

    public class OrderEvent {

        public OrderStatus Event = OrderStatus.Unknown;
        public decimal Price = -1M;
        public long Timestamp = -1L;
        public int Quantity = -1;
        public string OrderAPIId = "";
        public string OrderId = "";
        public Order Order = null;

    }

    [Serializable]
    public class Order
    {
        public string PositionId = "";
        public string Id = ""; // (stratid + "_" + symbol + "_" + rndchars)
        public string APIId = "";

        public string Symbol = "";
        public string StrategyId = "";
        public string StrategyName = "";

        // api dates
        public long CreatedAt;
        public long UpdatedAt;
        public long SubmittedAt;
        public long FilledAt;
        public long ExpiredAt;
        public long CanceledAt;
        public long PendingCancelAt;
        public long FailedAt;


        // application dates
        public long StopTriggeredAt;
        public long InstanceCreatedAt;


        public int Quantity = 0;

        public int FilledQuantity = 0;

        public OrderType Type = OrderType.Unknown;

        public OrderSide Side = OrderSide.Unknown;

        public OrderStatus Status = OrderStatus.Unknown;

        public string TimeInForce = "";

        public decimal LimitPrice = -1M;
        public decimal StopPrice = -1M;


        // this is used to trigger stop orders
        public decimal PriceAtCreation = -1M;

        public decimal AverageFilledPrice = -1M;

        public bool IsCancelable()
        {
            return
                Status != OrderStatus.Cancelled &&
                Status != OrderStatus.Filled &&
                Status != OrderStatus.Expired &&
                Status != OrderStatus.Rejected &&
                Status != OrderStatus.Suspended;
        }

        public bool IsActive()
        {
            return
                Status != OrderStatus.Cancelled &&
                Status != OrderStatus.Filled &&
                Status != OrderStatus.Expired &&
                Status != OrderStatus.Rejected &&
                Status != OrderStatus.Unknown &&
                Status != OrderStatus.UnknownAPIStatus &&
                Status != OrderStatus.Suspended;
        }

    }

    
    [Serializable]
    public class Position
    {
        public string Id = "";
        public string Symbol = "";
        public string StrategyId = "";
        public string StrategyName = "";

        // these should always be the same
        public int BoughtQuantity = 0;
        public int SoldQuantity = 0;

        public PositionStatus Status = PositionStatus.Unknown;
        public PositionSide Side = PositionSide.Unknown;

        public decimal BoughtAt = -1M;
        public decimal SoldAt = -1M;

        public long PendingAt = -1L;

        public long OpenedAt = -1L;
        public long ClosedAt = -1L;

        // public List<Order> Orders = new List<Order>();
    }
}
