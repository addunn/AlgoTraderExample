using AT.AlgoTraderEnums;
using AT.AlgoTraderObjects;
using AT.AlgoTraderSubProcessors;
using AT.DataObjects;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AT
{
    public class AlgoTraderProcess
    {
        private ThreadControl TradingManagerTC = null;

        public ThreadControl TC = null;

        public Timer SimulationTimer = null;

        private List<FD> Days = new List<FD>();

        private int TimerInterval = 1;
        
        public void SetIntervalBasedOnMode()
        {
            switch (AlgoTraderState.Mode)
            {
                case AlgoTraderModes.GatherStats:
                    TimerInterval = 1;
                    AlgoTraderState.SimSpeed = AlgoTraderSimulatingSpeed.Fastest;
                    break;
                case AlgoTraderModes.Backtesting:
                    TimerInterval = 1;
                    AlgoTraderState.SimSpeed = AlgoTraderSimulatingSpeed.Fastest;
                    break;
                case AlgoTraderModes.Simulating:
                    TimerInterval = 250;
                    AlgoTraderState.SimSpeed = AlgoTraderSimulatingSpeed.Paused;
                    break;
                case AlgoTraderModes.FakeLive:
                    TimerInterval = 10;
                    break;
                case AlgoTraderModes.Paper:
                    TimerInterval = 10;
                    break;
                case AlgoTraderModes.Live:
                    TimerInterval = 10;
                    break;
                default:
                    break;
            }
            
        }
        public void Run(AlgoTraderModes mode, List<FD> days, ThreadControl tc)
        {
            TC = tc;

            ResetData();

            AlgoTraderState.Mode = mode;

            SetIntervalBasedOnMode();

            AlgoTraderState.IsSim = 
                AlgoTraderState.Mode == AlgoTraderModes.GatherStats || 
                AlgoTraderState.Mode == AlgoTraderModes.Simulating || 
                AlgoTraderState.Mode == AlgoTraderModes.Backtesting;

            AlgoTraderState.UseSimOrdering = AlgoTraderState.IsSim || AlgoTraderState.Mode == AlgoTraderModes.FakeLive;

            PrepDays(days);


            bool allowToRun = true;

            if (!AlgoTraderState.IsSim)
            {
                ZonedDateTime zdt = UCDT.GetCurrentEastCoastTime();

                if (AlgoTraderState.Mode == AlgoTraderModes.Live && zdt.Hour >= 4)
                {
                    allowToRun = false;
                }
            }


            if (allowToRun)
            {
                // live or paper only has one day
                for (int n = 0; n < Days.Count; n++)
                {

                    ResetDayData();

                    AlgoTraderState.CurrentDay = Days[n];


                    // AlgoTraderUI.SelectedSymbol = Global.State.AllSymbols[UC.GetRandomInteger(0, Global.State.AllSymbols.Count - 1)];

                    // init the list where nodes, trend lines, etc are stored
                    // AlgoTraderShared.NodesData = new Dictionary<string, NodesData>();
                    for (int m = 0; m < Global.State.AllSymbols.Count; m++)
                    {
                        AlgoTraderShared.NodesData.Add(Global.State.AllSymbols[m], null);
                    }

                    CollectGarbage();

                    // this will take a while only when simulating
                    PrepareSimDayNodes();

                    // starts incrementing CurrentTime immediatly when this is called
                    PrepareTimer();

                    // waits for 3:55am
                    AlgoTraderMethods.WaitFor(AlgoTraderConfig.TicksStreamManagerAtHour, AlgoTraderConfig.TicksStreamManagerAtMinute, TC);

                    RunStocksDataUpdater();

                    RunOrdersDataUpdater();

                    RunTradingManager();

                    CollectGarbage();

                    // waits for trading manager to finish
                    WaitToContinue();

                    TC.StopAllChildrenAndWait();

                    if (!TC.CheckNotStopped()) { break; }
                }
            }

            TC.Log.AddLine("AlgoTraderProcess Run() complete", Verbosity.Minimal);

            // it will also set Ended = true
            ResetData();
        }






        #region Timer Methods

        public void ChangeSimulationTimerInterval(int milliseconds = 60000)
        {
            TimerInterval = milliseconds;

            if (SimulationTimer != null)
            {
                SimulationTimer.Change(TimerInterval, TimerInterval);
            }
        }

        private void DisposeTimer()
        {
            if (SimulationTimer != null)
            {
                SimulationTimer.Dispose();
                SimulationTimer = null;
            }
        }

        private void TimerCallback(object state)
        {
            if (!AlgoTraderState.IsSim || (AlgoTraderState.IsSim && !AlgoTraderState.PauseSim && !AlgoTraderState.SuperPauseSim))
            {
                AlgoTraderState.CurrentTime.RefreshTime();
            }
        }

        #endregion

        #region Helper Methods

        public void CollectGarbage()
        {
            TC.Log.AddLine("Garbage collecting starting");
            GC.Collect();
            GC.WaitForPendingFinalizers();
            TC.Log.AddLine("Garbage collecting finished");
        }

        
        private void ResetData()
        {
            AlgoTraderState.Mode = AlgoTraderModes.Simulating;
            AlgoTraderState.CurrentTime = null;
            AlgoTraderState.IsSim = true;
            AlgoTraderState.UseSimOrdering = true;
            Days.Clear();

            TradingManagerTC = null;

            CollectGarbage();
        }
        /// <summary>
        /// Resets all data associated with running a day.
        /// </summary>
        private void ResetDayData()
        {
            DisposeTimer();

            AlgoTraderState.CurrentDay = null;
            AlgoTraderState.WatchListBuilt = false;
            AlgoTraderState.WatchListBuilding = false;

            AlgoTraderState.PauseSim = false;
            AlgoTraderState.MinuteStopWatch.Reset();
            AlgoTraderState.StocksDataUpdaterTC = null;
            AlgoTraderState.OrdersDataUpdaterTC = null;
            AlgoTraderState.LiveEstimatedNanoOffset = 0;

            // not positions, they should carry over to the next days
            //AlgoTraderShared.Positions.Clear();
            //AlgoTraderShared.Orders.Clear();
            //AlgoTraderShared.StrategyActions.Clear();

            AlgoTraderShared.WatchList.Clear();
            
            AlgoTraderShared.NodesData.Clear();
            AlgoTraderShared.WatchListNodesData.Clear();
            AlgoTraderShared.SimDayNodes.Clear();

            AlgoTraderState.TradingManagerRunning = false;

            StocksDataUpdater.ResetDayData();
            OrdersDataUpdater.ResetDayData();

            TC.ClearChildren();
        }

        /// <summary>
        /// Starts ticking the timer.
        /// For simulations, current time will be set to 12am current day.
        /// For non-simulations, current time will be current east coast time.
        /// </summary>
        private void PrepareTimer()
        {
            if (AlgoTraderState.IsSim)
            {
                DateTime dt = new DateTime(AlgoTraderState.CurrentDay.DT.Year, AlgoTraderState.CurrentDay.DT.Month, AlgoTraderState.CurrentDay.DT.Day, 0, 0, 0, DateTimeKind.Unspecified);

                ZonedDateTime initial = UCDT.UTCDateTimeToZonedDateTime(UCDT.ZonedDateTimetoUTCDateTime(dt, UCDT.TimeZones.Eastern), UCDT.TimeZones.Eastern);

                AlgoTraderState.CurrentTime = new CurrentTime(AlgoTraderState.IsSim, initial);
            }
            else
            {
                AlgoTraderState.CurrentTime = new CurrentTime(AlgoTraderState.IsSim, UCDT.GetCurrentEastCoastTime());
            }

            SimulationTimer = new Timer(TimerCallback, null, TimerInterval, TimerInterval);
        }

        /// <summary>
        /// When simulating, this method will load all of the needed nodes for simulating a live feed/stream.
        /// </summary>
        private void PrepareSimDayNodes()
        {
            if (AlgoTraderState.IsSim)
            {
                // load up today's nodes for StreamManager to pretend to add nodes by using today's nodes

                int amount = 100;
                Stopwatch sw = new Stopwatch();
                sw.Start();

                object lockObj = new object();

                for (int m = 0; m < Global.State.AllSymbols.Count; m += amount)
                {

                    Parallel.For(0, amount, new ParallelOptions { MaxDegreeOfParallelism = 30 }, n =>
                    {
                        if (m + n < Global.State.AllSymbols.Count)
                        {
                            // NodesData nodes = null; 
                            NodesData nodes = DataMethods.GetCachedDayNodesData(Global.State.AllSymbols[m + n], AlgoTraderState.CurrentDay, Interval.HalfSecond, justCache: false);

                            // NodesData nodesData = DataMethods.GetCachedDayNodesData(Global.State.AllSymbols[m + n], CurrentDay, Interval.HalfSecond, computeNodes: true, justCache: false);

                            // no need to ComputeGaps because it's 1 day... trusting well formatted data
                            lock (lockObj)
                            {
                                AlgoTraderShared.SimDayNodes.Add(Global.State.AllSymbols[m + n], nodes);

                                //nodes.Add(Global.State.AllSymbols[m + n], DataMethods.GetCachedDayNodesData(Global.State.AllSymbols[m + n], CurrentDay, Interval.HalfSecond, computeNodes: true, justCache: false));
                            }
                            TC.Log.AddLine("Loaded " + Global.State.AllSymbols[m + n] + " stock nodes for stream manager");
                        }
                    });

                    if (!TC.CheckNotStopped())
                    {
                        break;
                    }
                }

                sw.Stop();

                TC.Log.AddLine("Finished loading sim nodes for stream manager in " + UC.MillisecondsToSeconds(sw.ElapsedMilliseconds, 3) + " sec(s). That's " + ((decimal)Global.State.AllSymbols.Count / UC.MillisecondsToSeconds(sw.ElapsedMilliseconds, 3)) + " per sec.");

                
            }
            
        }

        private void RunStocksDataUpdater()
        {
            AlgoTraderState.StocksDataUpdaterTC = new ThreadControl("Stocks Data Updater");

            TC.Children.Add(AlgoTraderState.StocksDataUpdaterTC);

            Task.Factory.StartNew(() => Methods.ThreadRun("AT.AlgoTraderSubProcessors.StocksDataUpdater, AT", "Run", AlgoTraderState.StocksDataUpdaterTC, null, new object[] { Global.State.AllSymbols } ), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void RunOrdersDataUpdater()
        {
            AlgoTraderState.OrdersDataUpdaterTC = new ThreadControl("Orders Data Updater");

            TC.Children.Add(AlgoTraderState.OrdersDataUpdaterTC);

            Task.Factory.StartNew(() => Methods.ThreadRun("AT.AlgoTraderSubProcessors.OrdersDataUpdater, AT", "Run", AlgoTraderState.OrdersDataUpdaterTC, null, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        private void RunTradingManager()
        {
            TradingManagerTC = new ThreadControl("Trading Manager");

            TC.Children.Add(TradingManagerTC);

            Task.Factory.StartNew(() => Methods.ThreadRun("AT.AlgoTraderSubProcessors.TradingManager, AT", "Run", TradingManagerTC, null, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
        }

        /// <summary>
        /// Checks and configures Days.
        /// Will remove any testing days if the mode can't have testing days.
        /// If not simulating, will limit the days to just today.
        /// </summary>
        /// <param name="days"></param>
        private void PrepDays(List<FD> days)
        {
            Days = new List<FD>();
            // prep days (forces today when not sim, removes testing days if sim GatherStats)
            if (AlgoTraderState.IsSim)
            {
                if (CannotHaveTestingDays())
                {
                    List<FD> testingDays = DBMethods.GetTestingDays();
                    int daysCount = days.Count;
                    // remove all testing days from days
                    days.RemoveAll(d => testingDays.Exists(t => t.Equals(d)));
                    TC.Log.AddLine("Removed " + (daysCount - days.Count) + " testing days because we cannot have testing days", Verbosity.Normal);
                }
                else
                {
                    TC.Log.AddLine("Testing days are totally fine. Will add all " + days.Count + " day(s)", Verbosity.Normal);
                }
                Days.AddRange(days);
                
                // make sure it's ordered chronologically for tiddyness sake
                Days = Days.OrderBy(d => d.StartNano).ToList();
            }
            else
            {
                ZonedDateTime zdt = UCDT.GetCurrentEastCoastTime();
                FD fd = new FD(zdt.Year, zdt.Month, zdt.Day);
                TC.Log.AddLine("Added only today because it's not a simulation: " + fd.ToString(), Verbosity.Normal);
                Days.Add(fd);
            }
        }

        /// <summary>
        /// This is for the main algo thread to wait until the trading day is over.
        /// </summary>
        private void WaitToContinue()
        {
            while (TC.CheckNotStopped() && !TradingManagerTC.IsOver())
            {
                Thread.Sleep(300);
            }
        }

        /// <summary>
        /// Returns true if mode cannot run algo trader over test days
        /// </summary>
        /// <returns></returns>
        private bool CannotHaveTestingDays()
        {
            return (AlgoTraderState.Mode != AlgoTraderModes.GatherStats);
        }

        #endregion

    }
}
