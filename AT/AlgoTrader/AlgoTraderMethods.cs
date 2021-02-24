using AT.AlgoTraderObjects;
using System;
using System.Collections.Generic;
using AT.AlgoTraderStrategies;
using System.Reflection;
using System.IO;
using NodaTime;
using System.Linq;
using AT.AlgoTraderEnums;
using System.Threading.Tasks;
using System.Threading;
using System.Diagnostics;

namespace AT
{
    // any decently sized calculations should go in here... 
    // process should be lean and not have large calculations
    public class AlgoTraderMethods
    {
        public static void SetSpeed(AlgoTraderSimulatingSpeed speed)
        {
            AlgoTraderState.SimSpeed = speed;

            if(speed == AlgoTraderSimulatingSpeed.Paused)
            {
                AlgoTraderState.SuperPauseSim = !AlgoTraderState.SuperPauseSim;
            }
            else if(speed == AlgoTraderSimulatingSpeed.RealTime)
            {
                Global.State.AlgoTrader.ChangeSimulationTimerInterval(60000);
                AlgoTraderUI.Frequency = 1;
            }
            else if (speed == AlgoTraderSimulatingSpeed.Fast)
            {
                Global.State.AlgoTrader.ChangeSimulationTimerInterval(1000);
                AlgoTraderUI.Frequency = 1;
            }
            else if (speed == AlgoTraderSimulatingSpeed.Faster)
            {
                Global.State.AlgoTrader.ChangeSimulationTimerInterval(100);
                AlgoTraderUI.Frequency = 1;
            }
            else if (speed == AlgoTraderSimulatingSpeed.EvenFaster)
            {
                Global.State.AlgoTrader.ChangeSimulationTimerInterval(1);
                AlgoTraderUI.Frequency = 3;
            }
            else if (speed == AlgoTraderSimulatingSpeed.Fastest)
            {
                Global.State.AlgoTrader.ChangeSimulationTimerInterval(1);
                AlgoTraderUI.Frequency = 60;
            }
        }


        public static long GetRealTimeCurrentNano()
        {
            long elapsedNanoSeconds = 0; // (AlgoTraderState.MinuteStopWatch.ElapsedTicks * (1000000000L / Stopwatch.Frequency));
            return AlgoTraderState.CurrentTime.GetDateTimeNano() + elapsedNanoSeconds + AlgoTraderState.LiveEstimatedNanoOffset;
        }

        /*
        private bool ModeNeedsSystemClock()
        {
            return (
                AlgoTraderState.Mode == AlgoTraderModes.Live ||
                AlgoTraderState.Mode == AlgoTraderModes.Paper ||
                AlgoTraderState.Mode == AlgoTraderModes.FakeLive
            );
        }
        */
        public static void WaitFor(int hour, int minute, ThreadControl tc)
        {
            int lastSecond = 0;
            int currentSecond = 0;

            while(!AlgoTraderState.CurrentTime.IsOnOrPassed(hour, minute) && tc.CheckNotStopped())
            {
                currentSecond = DateTime.UtcNow.Second;
                if(currentSecond != lastSecond)
                {
                    lastSecond = currentSecond;
                    tc.Log.AddLine("Waiting for " + hour.ToString("D2") + ":" + minute.ToString("D2") + ". It's currently " + AlgoTraderState.CurrentTime.GetTime().ToString(), Verbosity.Normal);
                }
            }
        }


        /// <summary>
        /// This is executed at the start of the application.
        /// It getts all Strategies in AlgoTraderStrategies namespace.
        /// It will hash the code and store it in the DB and return the usable strategies that can be used in trading
        /// </summary>
        /// <returns></returns>
        public static Dictionary<string, string> RefreshStrategyNamesAndIds()
        {

            Dictionary<string, string> result = new Dictionary<string, string>();

            Type[] types = UC.GetTypesInNamespace(Assembly.GetExecutingAssembly(), "AT.AlgoTraderStrategies");


            string baseStrategyText = UC.NormalizeSourceCode(File.ReadAllText(Global.Constants.BaseStrategyPath));
            string baseStrategyMD5 = UC.MD5Hash(baseStrategyText);
            string baseStrategyId = baseStrategyMD5.Substring(baseStrategyMD5.Length - 6);

            for (int n = 0; n < types.Length; n++)
            {
                string strategyText = UC.NormalizeSourceCode(File.ReadAllText(Global.Constants.StrategiesFolder + types[n].Name + ".cs"));
                string strategyMD5 = UC.MD5Hash(strategyText);
                string strategyId = strategyMD5.Substring(strategyMD5.Length - 6) + baseStrategyId;
                result.Add(strategyId, types[n].Name);

                DBMethods.InsertStrategyName(strategyId, types[n].Name, UCDT.GetCurrentNanoUnix());
            }



            return result;



        }


        /// <summary>
        /// Gets the day in the past where the amount of trading days from [day, startDay) is amountOfDays
        /// </summary>
        /// <param name="startDay"></param>
        /// <param name="amountOfDays"></param>
        /// <returns></returns>
        public static FD GetDataDaysStartDay(FD startDay, int amountOfDays)
        {

            int count = 0;

            DateTime currentDateTime = new DateTime(startDay.DT.Year, startDay.DT.Month, startDay.DT.Day, 0, 0, 0, DateTimeKind.Unspecified);
            do
            {
                currentDateTime = currentDateTime.AddDays(-1);
                if (Global.State.DataTracker.DayHasData(new FD(currentDateTime)))
                {
                    count++;
                }
            } while (count < amountOfDays);

            return new FD(currentDateTime);
        }

        /*
        public static List<FD> GetDataDaysFromDay(FD notIncludedStartDay, int amountOfDays)
        {

            List<FD> result = new List<FD>();

            int count = 0;

            DateTime currentDateTime = new DateTime(notIncludedStartDay.DT.Year, notIncludedStartDay.DT.Month, notIncludedStartDay.DT.Day, 0, 0, 0, DateTimeKind.Unspecified);
            do
            {
                currentDateTime = currentDateTime.AddDays(-1);
                if (Global.State.DataTracker.DayHasData(new FD(currentDateTime)))
                {
                    result.Add(new FD(currentDateTime, UCDT.TimeZones.Eastern));
                    count++;
                }
            } while (count < amountOfDays);


            result.Reverse();

            return result;
        }

        */
        public static void Pause()
        {
            AlgoTraderState.PauseSim = true;
        }
        public static void UnPause()
        {
            AlgoTraderState.PauseSim = false;
        }
        public static void SuperPause()
        {
            AlgoTraderState.SuperPauseSim = true;
        }

        public static void SuperUnPause()
        {
            AlgoTraderState.SuperPauseSim = false;
        }

        public static string GetStrategyName(string id)
        {
            return Global.State.UsableStrategies.ContainsKey(id) ? Global.State.UsableStrategies[id] : "";
        }
        public static List<WatchItem> BuildWatchItems()
        {
            List<WatchItem> result = new List<WatchItem>();

            if(AlgoTraderState.Mode == AlgoTraderModes.GatherStats)
            {
                // all symbols with all usable strategies
                string[] keys = Global.State.UsableStrategies.Keys.ToArray();


                List<string> chosenSymbols = new List<string>(Global.State.AllSymbols);




                for (int n = 0; n < chosenSymbols.Count; n++)
                {
                    string symbol = chosenSymbols[n];

                    List<Strategy> allStrategies = new List<Strategy>();

                    for (int m = 0; m < keys.Length; m++)
                    {
                        Type t = Type.GetType("AT.AlgoTraderStrategies." + Global.State.UsableStrategies[keys[m]]);
                        Strategy strat = (Strategy)Activator.CreateInstance(t, symbol, 0M, keys[m]);
                        allStrategies.Add(strat);
                    }

                    result.Add(new WatchItem() { Symbol = symbol, Strategies = allStrategies });
                }
            } 
            else
            {

                // this is here because testing live would cause a race condition in StreamManager
                Thread.Sleep(5000);



                /*
                
                    WatchListPicker algorithm:

	                    - for each symbol:
		                    - for each strategy
			                    - create a List<object> and add these stats for all data days:
				                    - { AverageTimeWeightedNet: , AverageNet: , Symbol: , StratId: , Wins: , Losses: }
				                    - Time weighted is by nanosecond of EndTimeStamp

	                    - sort by gain desc

	                    - keep a running TickSums List<long> where each item is a minute and the value is the sum of each symbol's ticks (quotes+trades) for that minute



	                    for each object in List<object> {
		                    if(gain > 0.1){
			                    if(new symbol){

				
					                    List<long> copy = TicksSums.Copy()
					                    copy.addRange(symbol ticks)
					                    estimatedBytesPerSec = GetEstimatedMaxBytesPerSec(copy);
					                    if(estimatedBytesPerSec <= maxBytesPerSec){
						                    finalObjectList.add(object);
					                    }
				
			                    } else {
				                    finalObjectList.add(object);
			                    }
		                    }
	                    }

	                    - finalObject should contain a gain percentage, so it can be factored in when determining sizing
    
             */
            }



            return result;

        }
    }
}
