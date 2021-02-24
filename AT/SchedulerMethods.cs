using AT.DataObjects;
using AT.Tools.LogObjects;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace AT
{
    public class SchedulerMethods
    {

        public static void CreateTradingStatsDBs(ThreadControl tc)
        {

            List<string> symbols = Global.State.AllSymbols;


            for (int n = 0; n < symbols.Count; n++)
            {
                if (DBMethods.TradingStatsDBExists(symbols[n]))
                {
                    tc.Log.AddLine("[" + symbols[n] + "] Already has a trading stats DB");

                }
                else
                {
                    DBMethods.CreateTradingStatsDB(symbols[n]);

                    tc.Log.AddLine("[" + symbols[n] + "] Created trading stats DB.");
                }
            }
        }
        public static void RefreshNoDataDaysAndSlapCache(ThreadControl tc)
        {
            ZonedDateTime ie = SystemClock.Instance.GetCurrentInstant().InZone(UCDT.TimeZones.Eastern);

            // set the current date to two days ago via eastern timezone
            DateTime dt = new DateTime(ie.Year, ie.Month, ie.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(-2);

            // ZonedDateTime zdt = UCDT.ZonedDateTimetoZonedDateTime(currentDate, UCDT.TimeZones.Eastern);
            List<FD> noDataDays = new List<FD>();


            while(dt.Year > 2018 && tc.CheckNotStopped())
            {
                tc.Log.AddLine("Checking: " + dt.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture));

                if (!StockAPI.Methods.DateHasData(dt.Year, dt.Month, dt.Day, false))
                {
                    FD fd = new FD(dt);
                    tc.Log.AddLine("Found a day with no data: " + fd.ToStringLong());
                    noDataDays.Add(fd);
                }

                dt = dt.AddDays(-1);
            }


            

            if (tc.CheckNotStopped())
            {

                DBMethods.RefreshDaysTracker(noDataDays, "NoData");

                noDataDays.Sort();

                for (int n = 0; n < Global.State.DataTracker.NoDataDays.Count; n++)
                {
                    // check if we didn't find one that was found before
                    if(noDataDays.BinarySearch(Global.State.DataTracker.NoDataDays[n]) < 0)
                    {

                        tc.Log.AddLine("FOUND A DAY IN THE OLD DATA THAT'S NOT IN THE NEW DATA: " + Global.State.DataTracker.NoDataDays[n].ToStringLong());
                        // This means that the scan before got a false nodataday.
                        // Could be API error or something.
                        // DELETE ANY CACHED DAY FOR THIS DAY BECAUSE WE MAY HAVE CACHED EMPTY NODES
                    }
                }

                App.InitDataTracker();
            }
            
        }

        public static void BuildMinuteDayNodes(ThreadControl tc)
        {

            // this should be called AFTER updated yesterday's ticks AND data tracker no data days updated


            List<string> symbols = new List<string>(Global.State.AllSymbols);

            symbols.Shuffle();

            symbols.Remove("spy");
            symbols.Remove("aapl");
            symbols.Add("spy");
            symbols.Add("aapl");
            symbols.Reverse();





            ZonedDateTime ie = SystemClock.Instance.GetCurrentInstant().InZone(UCDT.TimeZones.Eastern);

            // set the current date to yesterday via eastern timezone just to be safe
            DateTime dt = new DateTime(ie.Year, ie.Month, ie.Day, 0, 0, 0, DateTimeKind.Unspecified).AddDays(-20);


            DateTime firstDataDay = Global.State.DataTracker.DataDays[0].DT;

            while (dt > firstDataDay && tc.CheckNotStopped())
            {
                FD fd = new FD(dt);

                Parallel.For(0, symbols.Count, new ParallelOptions { MaxDegreeOfParallelism = 20 }, n =>
                {
                    if (DataMethods.DayNodesDataCacheable(symbols[n], fd))
                    {
                        Stopwatch sw = new Stopwatch();
                        sw.Start();
                        NodesData nodes = DataMethods.GetCachedDayNodesData(symbols[n], fd, Interval.HalfSecond, computeNodes: false, justCache: true);
                        sw.Stop();
                        tc.Log.AddLine("[" + symbols[n] + "] " + fd.ToString() + " Done. Took " + UC.MillisecondsToSeconds(sw.ElapsedMilliseconds, 2) + " sec(s)");
                    }
                    else
                    {
                        tc.Log.AddLine("[" + symbols[n] + "] " + fd.ToString() + ". Not cacheable.");
                    }

                });

                dt = dt.AddDays(-1);
            }

        }

        /*
        public static void BuildMinuteDayNodes(ThreadControl tc)
        {
            
            List<string> symbols = new List<string>(Global.State.AllSymbols);

            symbols.Remove("spy");
            symbols.Remove("aapl");
            symbols.Add("spy");
            symbols.Add("aapl");
            symbols.Reverse();

            Dictionary<string, List<FD>> symbolsDataDays = new Dictionary<string, List<FD>>();

            for(int n = 0; n < symbols.Count; n++)
            {
                symbolsDataDays.Add(symbols[n], DataMethods.ListOfDataDaysToListOfFD(DBMethods.GetDaysThatHaveDataBySymbol(symbols[n])));
            }

            for (int n = Global.State.DaysThatHaveData.Count - 1; n > 0; n--)
            {
                tc.Log.AddLine("Starting symbols for new day");

                Parallel.For(0, symbols.Count, new ParallelOptions { MaxDegreeOfParallelism = 15 }, m =>
                {
                    // if the symbol has day data for this date
                    if (symbolsDataDays[symbols[m]].Exists(d => d.IsEqual(Global.State.DaysThatHaveData[n])))
                    {
                        List<StockNode> nodes = DataMethods.GetCachedDayStockNodes(symbols[m], new List<FD>() { Global.State.DaysThatHaveData[n] }, Interval.OneMinute, false);
                        tc.Log.AddLine("[" + symbols[m] + "] " + Global.State.DaysThatHaveData[n].ToString() + " Done.");
                    }
                    else
                    {
                        tc.Log.AddLine("[" + symbols[m] + "] DOES NOT HAVE DAY DATA FOR THIS SYMBOL");
                    }
                });

                if (!tc.CheckNotStopped())
                {
                    tc.Log.AddLine("Breaking!");
                    break;
                }
            }
            

        }
        */


        public static void AddSymbols(ThreadControl tc)
        {

            List<string> symbols = new List<string>() { "adp", "azo", "avb", "avy", "bhge", "bll", "bac", "bk", "bax", "bbt", "bdx", "bby", "biib", "blk", "hrb", "ba", "bkng", "bwa", "bxp", "bsx", "bhf", "bmy", "avgo", "chrw", "ca", "cog", "cdns", "cpb", "cof", "cah", "kmx" };

            int count = 0;

            for(int n = 0; n < symbols.Count; n++)
            {
                if (DBMethods.QuotesDBExists(symbols[n]) || DBMethods.TradesDBExists(symbols[n]))
                {
                    tc.Log.AddLine("[" + symbols[n] + "] Already have this symbol");
                    
                }
                else 
                { 
                    DBMethods.CreateQuotesDB(symbols[n]);
                    DBMethods.CreateTradesDB(symbols[n]);
                    DBMethods.InsertSymbol(symbols[n]);
                    tc.Log.AddLine("[" + symbols[n] + "] Created Quotes and Trades tables and inserting into symbols.db");

                    count++;
                }
            }

            tc.Log.AddLine("All done. Added " + count + " new symbols.");

            
        }

        public static void TestSerializer(ThreadControl tc)
        {
            /*
            tc.Log.AddLine("Entered TestSerializer");

            List<string> symbols = new List<string>(Global.State.AllSymbols);

            Stopwatch lz4StopWatch = new Stopwatch();

            lz4StopWatch.Start();

            int count = 0;

            for (int n = Global.State.DaysThatHaveData.Count - 1; n > Global.State.DaysThatHaveData.Count - 5; n--)
            {
                tc.Log.AddLine("Starting symbols for new day");

                Parallel.For(0, symbols.Count, m =>
                {
                    List<StockNode> nodes2 = DataMethods.GetCachedDayStockNodes(symbols[m], Global.State.DaysThatHaveData[n], Interval.OneMinute);
                    tc.Log.AddLine("[" + symbols[m] + "] " + Global.State.DaysThatHaveData[n].ToString() + " Done.");
                });

                count++;
            }

            lz4StopWatch.Stop();

            tc.Log.AddLine("ALL WITH LZ4 DONE! For all symbols over " + count + " days, it took " + UC.MillisecondsToSeconds(lz4StopWatch.ElapsedMilliseconds, 2) + " sec(s).");
            */
        }



        public static void RunAlgoTraderGatherStats(ThreadControl tc)
        {

            List<FD> days = new List<FD>();
            days.AddRange(Global.State.DataTracker.DataDays);

            Global.State.AlgoTrader.Run(AlgoTraderEnums.AlgoTraderModes.GatherStats, days, tc);
        }


        
        public static void UpdateTestingDaysDB(ThreadControl tc)
        {
            List<FD> dataDays = Global.State.DataTracker.DataDays;


            tc.Log.AddLine("TOTAL DATA DAYS: " + dataDays.Count);

            List<string> testingList = new List<string>();

            for (int n = 0; n < dataDays.Count; n++)
            {
                int year = dataDays[n].DT.Year;
                int month = dataDays[n].DT.Month;
                int day = dataDays[n].DT.Day;

                int total = year + month + day;

                if (total % 13 == 0 || total % 5 == 0 || total % 17 == 0)
                {
                    testingList.Add(year + "-" + month.ToString("D2") + "-" + day.ToString("D2"));
                }

            }

            tc.Log.AddLine("TOTAL TESTING DAYS: " + testingList.Count);

            for (int n = 0; n < testingList.Count; n++)
            {
                int m = DBMethods.InsertTestingDay(testingList[n]);
                tc.Log.AddLine("(" + m + ") Inserted test day " + testingList[n]);
            }
        }
        

        public static void PrepareSocketStreamForWatchSymbols(ThreadControl tc)
        {
            /*
            Log.AddLine("Preparing socket stream for watch symbols", Log.Verbosity.Minimal);

            List<string> watchSymbols = CL.DB.Methods.GetAllWatchedSymbols();

            CurrentSymbols.Clear();
            CurrentSymbols.AddRange(watchSymbols);

            List<string> unsubscribeList = AllSymbols.FindAll(s => !watchSymbols.Contains(s));

            SocketManager.Unsubscribe(unsubscribeList);

            ts.TrySignalEndedIt();
            */
        }

        


        
        public static void VacuumAllDBs(ThreadControl tc)
        {
            
            tc.Log.AddLine("Starting Vacuuming all databases", Verbosity.Minimal);

            List<string> keys = DBMethods.GetConnectionKeys();

            long startSize = UC.GetFolderSize(Global.Constants.StockTicksDBPath);

            for (int m = 0; m < keys.Count; m += 60)
            {
                Parallel.For(0, 60, new ParallelOptions { MaxDegreeOfParallelism = 20 }, n =>
                {
                    if(m + n < keys.Count)
                    {
                        tc.Log.AddLine("Vacuuming " + keys[m + n], Verbosity.Verbose);
                        DBMethods.Vacuum(keys[m + n]);
                        tc.Log.AddLine("Done Vacuuming " + keys[m + n], Verbosity.Verbose);
                    }
                });
                if (!tc.CheckNotStopped())
                {
                    tc.Log.AddLine("Breaking VacuumAllDBs!");
                    break;
                }
            }

            Thread.Sleep(500);

            long endSize = UC.GetFolderSize(Global.Constants.StockTicksDBPath);

            tc.Log.AddLine("Vacuuming Complete. Start size: " + startSize + ", End size: " + endSize, Verbosity.Minimal);

        }

        public static void SerializeHistoricNodes(ThreadControl tc)
        {
            // starting yesterday (Eastern), and working backwards:
            //      - for each symbol:
            //          - get all ticks from [00:00, end)
            //          - get 1min nodes by calling BuildStockDataNodes()
            //          - get 5min nodes by calling BuildStockDataNodes()
            //          - get 15min nodes by calling BuildStockDataNodes()
            //          - get 30min nodes by calling BuildStockDataNodes()
            //          - get 1hour nodes by calling BuildStockDataNodes()
            //          - get 1day node(s) by calling BuildStockDataNodes()
            //          - serialize all the lists in this folder/format: bars/historical/symbol/date-1min.dat
        }


        public static void UpdateHistoricalTicksByDay(List<string> symbols, FD fd, bool force, ThreadControl tc)
        {
            
            tc.Log.AddLine("Starting update histocial ticks by day", Verbosity.Minimal);

            ZonedDateTime ie = SystemClock.Instance.GetCurrentInstant().InZone(UCDT.TimeZones.Eastern);
            
            // safety precaution
            if (ie.Year == fd.DT.Year && ie.Month == fd.DT.Month && ie.Day == fd.DT.Day)
            {
                throw new Exception("What are you doing???");
            }

            int parallelAPICalls = 15;

            if (!fd.IsOnWeekend())
            {

            

                bool hasDataForDay = StockAPI.Methods.DateHasData(fd.DT.Year, fd.DT.Month, fd.DT.Day, force);

                if (hasDataForDay)
                {
                    if (force)
                    {
                        tc.Log.AddLine("Forcing a historical update", Verbosity.Verbose);
                    }
                    else
                    {
                        tc.Log.AddLine("Date has data", Verbosity.Verbose);
                    }

                    tc.Log.AddLine("About to start API calls as fast as we can go", Verbosity.Minimal);

                    symbols.Shuffle();

                    for (int s = 0; s < symbols.Count; s += parallelAPICalls)
                    {
                        //string[] syms = new string[parallelAPICalls];
                        lock (Global.State.ThreadControlTreeLock)
                        {
                            for (int m = 0; m < parallelAPICalls; m++)
                            {
                                if (m + s < symbols.Count)
                                {
                                    string sym = symbols[m + s];
                                    ThreadControl singleQuoteTC = new ThreadControl(fd.ToString() + " : " + sym);
                                    tc.Children.Add(singleQuoteTC);
                                    Task.Factory.StartNew(() => Methods.ThreadRun("AT.SchedulerMethods, AT", "UpdateSingleDayAndSingleSymbolTicks", singleQuoteTC, null, new object[] { sym, fd }), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                                }
                            }
                        }

                        while (tc.Children.Exists(p => p.State != ThreadControlState.Complete && p.State != ThreadControlState.Done)) { }
                    
                        tc.ClearChildren();


                        tc.Log.AddLine("Done with small batch for " + fd.ToString() + ". About %" + Math.Round(((double)s / (double)symbols.Count) * (double)100, 2) + " done.");
                    
                        if (!tc.CheckNotStopped())
                        {
                            tc.Log.AddLine("Breaking UpdateHistoricalTicksByDay!");
                            break;
                        }
                    }

                }
                else
                {
                    tc.Log.AddLine("Not forcing and date does NOT have data for " + fd.ToString());
                }

            }
            else
            {
                tc.Log.AddLine("Date is on weekend " + fd.ToString());
            }



        }

        public static void UpdateSingleDayAndSingleSymbolTicks(string symbol, FD fd, ThreadControl tc)
        {
            if (DBMethods.TradesDBExists(symbol) && DBMethods.QuotesDBExists(symbol))
            {
                if (!Global.State.DataTracker.SymbolHasDayData(symbol, fd))
                {

                    tc.Log.AddLine("[" + fd.ToString() + "]", Verbosity.Verbose);

                    tc.Log.AddLine("[" + symbol + "] Making trades API calls", Verbosity.Verbose);

                    List<Trade> trades = StockAPI.Methods.GetHistoricTradesFull(symbol, fd.DT.Year, fd.DT.Month, fd.DT.Day);

                    tc.Log.AddLine("[" + symbol + "] Bulk inserting " + trades.Count + " trade(s)", Verbosity.Verbose);

                    int tradesInserted = DBMethods.BulkInsertTrades(trades, symbol);

                    tc.Log.AddLine("[" + symbol + "] Inserted " + tradesInserted + " trade(s)", Verbosity.Normal);

                    tc.Log.AddLine("[" + symbol + "] Making quotes API calls", Verbosity.Verbose);

                    List<Quote> quotes = StockAPI.Methods.GetHistoricQuotesFull(symbol, fd.DT.Year, fd.DT.Month, fd.DT.Day);

                    tc.Log.AddLine("[" + symbol + "] Bulk inserting " + quotes.Count + " quote(s)", Verbosity.Verbose);

                    int quotesInserted = DBMethods.BulkInsertQuotes(quotes, symbol);

                    tc.Log.AddLine("[" + symbol + "] Inserted " + quotesInserted + " quote(s)", Verbosity.Normal);

                    int p = DBMethods.MarkSymbolHasDayData(symbol, fd.DT.Month, fd.DT.Day, fd.DT.Year);

                    tc.Log.AddLine("[" + symbol + "] Marked symbol has data (" + p + ")", Verbosity.Minimal);

                }
                else
                {
                    tc.Log.AddLine("[" + symbol + "] Already has data for this date.");
                }
            }
            else
            {
                tc.Log.AddLine("[" + symbol + "] DBs not found");
            }
        }
        public static void UpdateHistoricalTicks(ThreadControl tc)
        {

            tc.Log.AddLine("Starting update historical ticks", Verbosity.Minimal);

            ZonedDateTime ie = SystemClock.Instance.GetCurrentInstant().InZone(UCDT.TimeZones.Eastern);

            // set the current date to yesterday via eastern timezone just to be safe
            DateTime currentDate = new DateTime(ie.Year, ie.Month, ie.Day, 0, 0, 0, DateTimeKind.Utc).AddDays(-335);


            while (tc.CheckNotStopped())
            {
                FD fd = new FD(currentDate);
                UpdateHistoricalTicksByDay(Global.State.AllSymbols, fd, false, tc);

                currentDate = currentDate.AddDays(-1);
            }

            
            
        }

        
        public static void StreamInsertSymbols(ThreadControl tc)
        {
            /*
            Stopwatch sw = new Stopwatch();

            sw.Start();

            Log.AddLine("Starting stream insert symbols", CL.Log.Verbosity.Minimal);

            // wait for deserializing to be finished if it's still going
            while (SocketManager.CurrentlyDeserializing) { }

            if (callCount == 0)
            {
                // remove all trades and quotes from SocketManager that we aren't using
                string[] tradeKeys = SocketManager.Trades.Keys.ToArray();
                string[] quoteKeys = SocketManager.Quotes.Keys.ToArray();

                for (int n = 0; n < tradeKeys.Length; n++)
                {
                    if (!CurrentSymbols.Contains(tradeKeys[n]))
                    {
                        SocketManager.Trades.Remove(tradeKeys[n]);
                    }
                }
                for (int n = 0; n < quoteKeys.Length; n++)
                {
                    if (!CurrentSymbols.Contains(quoteKeys[n]))
                    {
                        SocketManager.Quotes.Remove(quoteKeys[n]);
                    }
                }
            }

            string[] tKeys = SocketManager.Trades.Keys.ToArray();
            string[] qKeys = SocketManager.Quotes.Keys.ToArray();

            Parallel.For(0, qKeys.Length, new ParallelOptions { MaxDegreeOfParallelism = 10 }, n =>
            {
                DataLayer.DeDupeQuotes(SocketManager.Quotes[qKeys[n]], 2);

                CL.DB.Methods.BulkInsertQuotes(SocketManager.Quotes[qKeys[n]], qKeys[n], true);

                // clear it because we just inserted them, no need to save them
                SocketManager.Quotes[qKeys[n]].Clear();
            });

            Parallel.For(0, tKeys.Length, new ParallelOptions { MaxDegreeOfParallelism = 10 }, n =>
            {
                DataLayer.DeDupeTrades(SocketManager.Trades[tKeys[n]], 2);

                CL.DB.Methods.BulkInsertTrades(SocketManager.Trades[tKeys[n]], tKeys[n], true);

                // clear it because we just inserted them, no need to save them
                SocketManager.Trades[tKeys[n]].Clear();
            });

            sw.Stop();

            Log.AddLine("Done inserting for this minute. Time taken: " + UC.MillisecondsToSeconds(sw.ElapsedMilliseconds, 3) + " secs", CL.Log.Verbosity.Normal);
            */
        }

        /*
        public static void StartAutoUpdate()
        {

            Log.AddLine("App.StartAutoUpdate() called");

            StartEnabled = false;

            CurrentSymbols = new List<string>();

            CurrentSymbols.AddRange(AllSymbols);

            StopEnabled = true;

            Scheduler.Start();
        }
        */


        public static void RestartApp(ThreadControl tc)
        {
            /*
            log.AddLine("Restarting app", Verbosity.Minimal);

            Task.Run(() => RestartAppWait());

            Thread.Sleep(2000);

            ts.TrySignalEndedIt();
            */
        }

        public static void RestartAppWait()
        {
            // execute bat file to start StockUpdater with autostart params
            /*
            Process proc = new Process();
            proc.StartInfo.FileName = Data.ExecutablePath + "\\start-exe.bat";
            proc.StartInfo.WorkingDirectory = Data.ExecutablePath;
            proc.Start();

            StopAutoUpdate();

            Log.AddLine("Calling Application.Exit() in 5 seconds", Log.Verbosity.Minimal);

            Thread.Sleep(5000);
            Application.Exit();
            */
        }

        /*
        public static void StopAutoUpdate()
        {
            StopEnabled = false;
            StartEnabled = false;

            Scheduler.Stop();

            // wait for it to stop running
            while (!Scheduler.IsStopped()) { }

            StartEnabled = true;

            Log.AddLine("AutoUpdate Stopped!", CL.Log.Verbosity.Normal);
        }
        */
    }
}
