using AT.AlgoTraderEnums;
using AT.AlgoTraderObjects;
using AT.AlgoTraderSubProcessors;
using AT.DataObjects;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT
{
    public class AlgoTraderState
    {

        public static AlgoTraderSimulatingSpeed SimSpeed = AlgoTraderSimulatingSpeed.Unknown;

        public static AlgoTraderModes Mode = AlgoTraderModes.Simulating;

        public static CurrentTime CurrentTime;

        public static bool IsSim = true;

        public static bool UseSimOrdering = true;

        public static FD CurrentDay = null;

        public static bool WatchListBuilt = false;
        public static bool WatchListBuilding = false;

        public static bool PauseSim = false;
        public static bool SuperPauseSim = false;

        public static bool TradingManagerRunning = false;

        public static Stopwatch MinuteStopWatch = new Stopwatch();

        public static ThreadControl StocksDataUpdaterTC = null;

        public static ThreadControl OrdersDataUpdaterTC = null;

        /// <summary>
        /// When simulating, this value increases during processing of a minute.
        /// For example, since live goes through the full process buckets and Sim doesn't,
        /// Sim will add on an estimated value of how long it would have taken if it had to process buckets.
        /// This is mostly used in Order Manager to figure out when orders/positions are filled, created, etc.
        /// </summary>
        public static long LiveEstimatedNanoOffset = 0;

    }
}
