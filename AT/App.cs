using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using AT.Tools.SchedulerObjects;
using AT.Tools;
using System.Threading;
using AT.Tools.DataTrackerObjects;

namespace AT
{
    public class App
    {
        /// <summary>
        /// Executes once when the application starts
        /// </summary>
        public static void Init()
        {
            Global.State.ExecutablePath = AppDomain.CurrentDomain.BaseDirectory;

            string sourcePath = Global.State.ExecutablePath.Replace("\\compiled\\", "");

            Global.Constants.StrategiesFolder = sourcePath + "\\AlgoTrader\\Strategies\\";
            Global.Constants.BaseStrategyPath = sourcePath + "\\AlgoTrader\\AlgoTraderBaseStrategy.cs";
            // D:\\Projects\\AlgoTrader - New\\Source\\AT\\AlgoTrader\\AlgoTraderBaseStrategy.cs

            Global.Constants.NodesPath = Global.State.ExecutablePath + "nodes\\";

            Global.Constants.DBPath = Global.State.ExecutablePath + "dbs\\";
            Global.Constants.StockTicksDBPath = Global.Constants.DBPath + Global.Constants.StockTicksDBFolder + "\\";

            Global.Constants.TradingStatsDBPath = Global.Constants.DBPath + Global.Constants.TradingStatsDBFolder + "\\";

            DBMethods.Init();

            // ThreadPool.SetMinThreads(100, 100);

            string json = File.ReadAllText(Global.State.ExecutablePath + "schedules\\main.js");

            Global.State.SchedulerTC = new ThreadControl("Scheduler");
            Global.State.Scheduler = new Scheduler(json, "app");

            

            Global.State.AllSymbols = DBMethods.GetAllSymbols();
            //Global.State.AllSymbols.Shuffle(5);

            
            Global.State.AllSymbols.Clear();
            
            Global.State.AllSymbols.Add("aapl");
            Global.State.AllSymbols.Add("spy");
            Global.State.AllSymbols.Add("qqq");
            Global.State.AllSymbols.Add("msft");
            Global.State.AllSymbols.Add("gld");

            Global.State.AllSymbols.Add("amd");

            /*
            
                Global.State.AllSymbols.Add("crc");
                Global.State.AllSymbols.Add("cnp");
                Global.State.AllSymbols.Add("meet");
                Global.State.AllSymbols.Add("eros");
                Global.State.AllSymbols.Add("bimi");

            */


            //Global.State.AllSymbols.Add("tsla");
            //Global.State.AllSymbols.Add("amd");

            //Global.State.AllSymbols.Shuffle();
            //Global.State.AllSymbols.RemoveRange(0, 380);
            /*
            Global.State.AllSymbols.Add("spy");
            Global.State.AllSymbols.Add("amd");
            Global.State.AllSymbols.Add("tsla");
            Global.State.AllSymbols.Add("x");
            Global.State.AllSymbols.Add("aapl");
            Global.State.AllSymbols.Add("t");
            Global.State.AllSymbols.Add("xli");
            Global.State.AllSymbols.Add("tsg");
            Global.State.AllSymbols.Add("crc");
            Global.State.AllSymbols.Add("tza");
            Global.State.AllSymbols.Add("carg");
            Global.State.AllSymbols.Add("immu");
            */

            //Global.State.AllSymbols = new List<string>();
            //Global.State.AllSymbols.Add("spy");
            /*
            if (Global.Constants.FastForTesting)
            {
                
            }
            */



            Global.State.ThreadControlTree = new List<ThreadControl>();

            Global.State.ThreadControlTree.Add(Global.State.SchedulerTC);

            

            Global.State.Subscriptions = new Dictionary<string, Subscription>();
            Global.State.AlgoTraderSubscriptions = new Dictionary<string, bool>();



            //Global.State.ForceUpdate = new Dictionary<string, bool>();
            // Global.State.SubscriptionStates = new Dictionary<string, object>();


            Global.State.Subscriptions.Add("schedulerRunning", new Subscription() { State = false, Interval = 2 });

            Global.State.Subscriptions.Add("threadControlTree", new Subscription() { State = long.MaxValue, Interval = 5 });

            Global.State.Subscriptions.Add("threadControlState", new Subscription() { State = "md5hashstring", Interval = 7 });

            Global.State.Subscriptions.Add("threadControlLog", new Subscription() { State = int.MaxValue, Interval = 3 });

            Global.State.Subscriptions.Add("threadControlObject", new Subscription() { State = int.MaxValue, Interval = 2 });

            Global.State.Subscriptions.Add("applicationStats", new Subscription() { State = "some string", Interval = 6 });



            Global.State.AlgoTraderSubscriptions.Add("main", false);
            Global.State.AlgoTraderSubscriptions.Add("overview", false);
            Global.State.AlgoTraderSubscriptions.Add("positions", false);
            Global.State.AlgoTraderSubscriptions.Add("orders", false);
            Global.State.AlgoTraderSubscriptions.Add("actions", false);
            Global.State.AlgoTraderSubscriptions.Add("chart", false);
            Global.State.AlgoTraderSubscriptions.Add("log", false);



            InitDataTracker();


            // this also stores new ids and names in DB
            Global.State.UsableStrategies = AlgoTraderMethods.RefreshStrategyNamesAndIds();




        }


        public static void InitDataTracker()
        {
            Dictionary<string, List<FD>> dataDaysBySymbol = DBMethods.GetDaysThatHaveDataEachSymbol();

            List<FD> dataDays = new List<FD>();

            foreach (List<FD> item in dataDaysBySymbol.Values)
            {
                // add them all, dedupe later
                dataDays.AddRange(item);
            }

            dataDays.Sort();

            List<FD> newList = new List<FD>(dataDays.Distinct());


            dataDays = dataDays.DistinctBy(p => new { p.StartNano, p.EndNano }).ToList();


            List<FD> noDataDays = DBMethods.GetDaysFromTracker("NoData");

            List<FD> holidays = DBMethods.GetDaysFromTracker("Holidays");

            Global.State.DataTracker = new DataTracker();

            Global.State.DataTracker.DataDays = dataDays;
            Global.State.DataTracker.DataDaysBySymbol = dataDaysBySymbol;
            Global.State.DataTracker.Holidays = holidays;
            Global.State.DataTracker.NoDataDays = noDataDays;
        }

        public static void StartAppScheduler()
        {
            if(Global.State.SchedulerTC.CanBeStarted())
            {
                Task.Factory.StartNew(() => Methods.ThreadRun("AT.Tools.SchedulerObjects.Scheduler, AT", "Run", Global.State.SchedulerTC, Global.State.Scheduler, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }
        }


        public static void StopAppScheduler()
        {
            if (Global.State.SchedulerTC != null && Global.State.SchedulerTC.CanBeStopped())
            {
                Global.State.SchedulerTC.AddSignalForChild(Signal.Stop);
            }
        }

        public static string GetSubscribedStateChanges()
        {

            Dictionary<string, string> items = new Dictionary<string, string>();

            if (Global.State.Subscriptions["schedulerRunning"].Subscribed && Global.State.Subscriptions["schedulerRunning"].IsReady())
            {
                bool update = (Global.State.SchedulerTC.State == ThreadControlState.Running) != (bool)Global.State.Subscriptions["schedulerRunning"].State;
                if (update)
                {
                    bool val = (Global.State.SchedulerTC.State == ThreadControlState.Running);
                    Global.State.Subscriptions["schedulerRunning"].State = val;
                    items.Add("schedulerRunning", UC.BoolToJSBool(val));
                }
            }

            if (Global.State.Subscriptions["applicationStats"].Subscribed && Global.State.Subscriptions["applicationStats"].IsReady())
            {
                items.Add("applicationStats", SerializerMethods.SerializeApplicationStats());
            }




            if (Global.State.Subscriptions["threadControlTree"].Subscribed && Global.State.Subscriptions["threadControlTree"].IsReady())
            {
                lock (Global.State.ThreadControlTreeLock)
                {
                    long change = GetThreadControlTreeChangeValue();
                    if (change != (long)Global.State.Subscriptions["threadControlTree"].State)
                    {
                        Global.State.Subscriptions["threadControlTree"].State = change;
                        items.Add("threadControlTree", SerializerMethods.SerializeThreadControlTree());
                    }
                }
            }

            // don't do this if we already have a threadControlTree
            if (Global.State.Subscriptions["threadControlState"].Subscribed && !items.ContainsKey("threadControlTree") && Global.State.Subscriptions["threadControlState"].IsReady())
            {


                lock (Global.State.ThreadControlTreeLock)
                {
                    string json = GetSerializedThreadControlChangedState();
                    string state = UC.MD5Hash(json);
                    if (!string.IsNullOrWhiteSpace(json) && state != (string)Global.State.Subscriptions["threadControlState"].State)
                    {
                        Global.State.Subscriptions["threadControlState"].State = state;
                        items.Add("threadControlState", json);
                    }
                }
            }

            if (Global.State.Subscriptions["threadControlLog"].Subscribed && Global.State.Subscriptions["threadControlLog"].IsReady())
            {
                Tools.LogObjects.Log log = RecursiveFindLogByThreadControlId(Global.State.SelectedThreadControlId, Global.State.ThreadControlTree);
                if (log != null && (log.ChangedSinceLastRead(Verbosity.Verbose) || Global.State.ThreadControlIdChanged))
                {
                    items.Add("threadControlLog", "\"" + System.Web.HttpUtility.JavaScriptStringEncode(log.Read(Verbosity.Verbose)) + "\"");
                }
            }

            Global.State.ThreadControlIdChanged = false;

            return SerializerMethods.DictionarySerializedValuesToJSON(items);
        }

        public static long GetThreadControlTreeChangeValue()
        {
            long val = 0;
            for(int n = 0; n < Global.State.ThreadControlTree.Count; n++)
            {
                val += RecursiveThreadControlChangeValue(val, Global.State.ThreadControlTree[n]);
            }

            return val;
        }

        public static Tools.LogObjects.Log RecursiveFindLogByThreadControlId(int id, List<ThreadControl> tcList)
        {
            Tools.LogObjects.Log result = null;

            for (int n = 0; n < tcList.Count; n++)
            {
                if(tcList[n].Id == id)
                {
                    result = tcList[n].Log;
                    break;
                }
                if(tcList[n].Children.Count > 0)
                {
                    result = RecursiveFindLogByThreadControlId(id, tcList[n].Children);
                }
            }

            return result;
        }

        public static long RecursiveThreadControlChangeValue(long val, ThreadControl tc)
        {
            for(int n = 0; n < tc.Children.Count; n++)
            {
                val += RecursiveThreadControlChangeValue(val, tc.Children[n]);
            }

            val += tc.Id;

            return val;
        }

        public static string GetSerializedThreadControlChangedState()
        {

            StringBuilder sb = new StringBuilder();

            sb.Append("[");

            List<ThreadControl> allChangedThreadControls = RecursiveFindThreadControlChangedState(Global.State.ThreadControlTree);

            for(int n = 0; n < allChangedThreadControls.Count; n++)
            {
                sb.Append(SerializerMethods.SerializeThreadControlState(allChangedThreadControls[n]));
                if(n != allChangedThreadControls.Count - 1)
                {
                    sb.Append(",");
                }
            }


            sb.Append("]");

            string result = sb.ToString();

            return allChangedThreadControls.Count == 0 ? "" : result;

        }
        public static List<ThreadControl> RecursiveFindThreadControlChangedState(List<ThreadControl> tcList)
        {
            List<ThreadControl> result = new List<ThreadControl>();
            
            
            for(int n = 0; n < tcList.Count; n++)
            {
                if (tcList[n].ReadStateChanged())
                {
                    result.Add(tcList[n]);
                    tcList[n].SetStateChanged(false);
                }

                if(tcList[n].Children.Count > 0)
                {
                    result.AddRange(RecursiveFindThreadControlChangedState(tcList[n].Children));
                }
            }


            return result;
        }
    }
}
