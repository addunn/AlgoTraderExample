using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AT.Tools.DataTrackerObjects;
using AT.Tools.SchedulerObjects;
namespace AT.Global
{
    public class Constants
    {

        public static string DBPath = "";

        public static string StockTicksDBFolder = "stock-ticks";
        public static string StockTicksDBPath = DBPath + StockTicksDBFolder + "\\";

        public static string TradingStatsDBFolder = "trading-stats";
        public static string TradingStatsDBPath = "";

        public static string DayDateTimeStringFormat = "yyyy/MM/dd";

        public static string NodesPath = "";
        // public static string NodesPath = "D:\\Backup\\nodes\\";

        public static string StrategiesFolder = "";
        public static string BaseStrategyPath = "";
    }
    public class State
    {

        public static ApplicationStats ApplicationStats = new ApplicationStats();

        public static object ThreadControlTreeLock = new object();

        public static AlgoTraderProcess AlgoTrader = new AlgoTraderProcess();

        public static List<string> AllSymbols = null;
        

        public static bool SendLog = false;
        public static bool LastCanBeShutDown = true;
        public static List<string> Commands = new List<string>();
        public static string ExecutablePath = "";


        public static Scheduler Scheduler = null;
        public static ThreadControl SchedulerTC = null;

        // public static List<FD> DaysThatHaveData = null;

        public static DataTracker DataTracker = null;

        public static Dictionary<string, Subscription> Subscriptions = null;

        public static Dictionary<string, bool> AlgoTraderSubscriptions = null;



        // public static Dictionary<string, bool> Subscriptions = null;
        //public static Dictionary<string, bool> ForceUpdate = null;
        //public static Dictionary<string, object> SubscriptionStates = null;




        public static int SelectedThreadControlId = -1;




        public static List<ThreadControl> ThreadControlTree = null;

        public static long ThreadControlTreeChange = 0;


        public static bool ThreadControlIdChanged = false;


        /// <summary>
        /// Set on app init
        /// </summary>
        public static Dictionary<string, string> UsableStrategies = null;

    }
}
