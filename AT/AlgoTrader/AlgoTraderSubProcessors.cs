using AT.AlgoTraderEnums;
using AT.AlgoTraderObjects;
using AT.DataObjects;
using Jil;
using NodaTime;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Security.Authentication;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using WebSocket4Net;

namespace AT.AlgoTraderSubProcessors
{
    /*
    public class SymbolManager
    {
        public string Symbol = "";

        // is shared objects in Order Manager, Trading Manager, and Strategies
        public List<Position> Positions = new List<Position>();
        public List<Order> Orders = new List<Order>();


        public Dictionary<string, Strategy> Strategies = null;


        // read and cleared from TradingManager
        public List<StrategyAction> StrategyActions = null;



        public SymbolManager(WatchItem watchItem)
        {
            Symbol = watchItem.Symbol;
            Strategies = new Dictionary<string, Strategy>(watchItem.Strategies);
            StrategyActions = new List<StrategyAction>();
        }

        public void Run(ThreadControl tc)
        {

            tc.Log.AddLine("Inside Run");

            int rndSleep = UC.GetRandomInteger(3, 15);


            while (tc.CheckNotStopped())
            {
                Thread.Sleep(rndSleep);

                Signal signal = tc.PopSignalForChild();

                if (signal == Signal.Process)
                {
                    tc.Log.AddLine("Received Process signal from parent");


                    tc.AddSignalForParent(Signal.Done);
                    tc.Log.AddLine("Sent Done signal to parent");

                }
                else if (signal == Signal.FormulateActions)
                {
                    tc.Log.AddLine("Received FormulateActions signal from parent");

                    string[] keys = Strategies.Keys.ToArray();


                    for (int n = 0; n < keys.Length; n++)
                    {
                        StrategyAction action = Strategies[keys[n]].ComputeActions();

                        if (action != null)
                        {
                            tc.Log.AddLine("[" + Symbol + "] Got a strategy action from " + Strategies[keys[n]].GetType().Name + "!", Verbosity.Normal);
                            StrategyActions.Add(action);
                        }
                    }

                    tc.AddSignalForParent(Signal.Done);
                    tc.Log.AddLine("Sent Done signal to parent");

                }

            }



        }


    }

    */
    public class StocksDataUpdater
    {
        private static bool OnBucketA1 = true;

        public static Dictionary<string, List<Trade>> PrevMinuteTrades = new Dictionary<string, List<Trade>>();
        public static Dictionary<string, List<Quote>> PrevMinuteQuotes = new Dictionary<string, List<Quote>>();

        public static Dictionary<string, List<Trade>> NextMinuteTrades = new Dictionary<string, List<Trade>>();
        public static Dictionary<string, List<Quote>> NextMinuteQuotes = new Dictionary<string, List<Quote>>();

        public static Dictionary<string, List<Trade>> Trades = null;
        public static Dictionary<string, List<Quote>> Quotes = null;


        // this is the main buffer
        private static LinkedList<string> BucketA1 = new LinkedList<string>();
        private static LinkedList<string> BucketA2 = new LinkedList<string>();

        private static WebSocket WS = null;

        public static bool IsConnected = false;
        public static bool IsOpeningConnection = false;

        private static List<string> CurrentSubscription = new List<string>();


        public static void ResetDayData()
        {
            OnBucketA1 = true;
            PrevMinuteTrades.Clear();
            PrevMinuteQuotes.Clear();

            NextMinuteTrades.Clear();
            NextMinuteQuotes.Clear();
            
            if(Trades != null)
            {
                Trades.Clear();
                Quotes.Clear();
                Trades = null;
                Quotes = null;
            }
            

            BucketA1.Clear();
            BucketA2.Clear();
            WS = null;
            IsConnected = false;
            IsOpeningConnection = false;
            CurrentSubscription.Clear();
        }


        public static void Run(List<string> subscription, ThreadControl tc)
        {
            CurrentSubscription = subscription;

            tc.Log.AddLine("Starting Run(). ThreadId: " + Thread.CurrentThread.ManagedThreadId + ", IsBackground: " + Thread.CurrentThread.IsBackground + ", Priority: " + Thread.CurrentThread.Priority.ToString());

            if (!AlgoTraderState.IsSim)
            {
                ThreadControl streamTC = new ThreadControl("Stream");
                tc.Children.Add(streamTC);
                Task.Factory.StartNew(() => Methods.ThreadRun("AT.AlgoTraderSubProcessors.StocksDataUpdater, AT", "RunStream", streamTC, null, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            bool isFirstProcess = true;
            int processedCount = 0;

            long lastEndNano = 0L;

            while (tc.CheckNotStopped())
            {
                // could there be a threading issue by reading this?
                //currentMinute = AlgoTraderState.CurrentTime.GetMinute();


                Thread.Sleep(5);

                if (tc.PopSignalForChild() == Signal.Process)
                {
                    tc.Log.AddLine("Received a Process signal!", Verbosity.Normal);

                    Stopwatch sw = new Stopwatch();

                    sw.Start();

                    // this is necessary when not sim.
                    // this makes sure that we receive all ticks for the previous minute (thinking there chould be at most a 1 second delay)
                    if (!AlgoTraderState.IsSim)
                    {
                        Thread.Sleep(1);
                    }

                    // swap the bucket that LoopWatchSocket uses
                    OnBucketA1 = !OnBucketA1;




                    // just to make sure that LoopWatchSocket is done touching the old bucket
                    if (!AlgoTraderState.IsSim)
                    {
                        Thread.Sleep(5);
                    }

                    string payload = "[" + String.Join(",", OnBucketA1 ? BucketA2 : BucketA1) + "]";

                    if (OnBucketA1)
                    {
                        BucketA2.Clear();
                    }
                    else
                    {
                        BucketA1.Clear();
                    }

                    // payload looks like: [[{"ev":"Q",...},{"ev":"Q",...}],[{"ev":"T",...}]]




                    // THIS IS CORRECT BECAUSE WE ARE JUST GETTING 1 MINUTE FROM THE CURRENT TIME
                    //long currentNano = AlgoTraderMethods.GetRealTimeCurrentNano(); // AlgoTraderState.CurrentTime.GetDateTimeNano();

                    //CurrentTime ct = AlgoTraderState.CurrentTime;

                    // THIS IS CORRECT BECAUSE WE ARE JUST GETTING 1 MINUTE FROM THE CURRENT TIME
                    // long currentNano = AlgoTraderState.CurrentTime.GetDateTimeNano();
                    long currentNano = AlgoTraderMethods.GetRealTimeCurrentNano();
                    long startNano = (currentNano - (currentNano % 60000000000L)) - 60000000000L;
                    long endNano = startNano + 60000000000L;



                    tc.Log.AddLine("Moving " + NextMinuteTrades.Count + " symbol's next minute trades to current minute trades", Verbosity.Normal);
                    tc.Log.AddLine("Moving " + NextMinuteQuotes.Count + " symbol's next minute quotes to current minute quotes", Verbosity.Normal);

                    // move NextMinute bags to CurrentMinute bags
                    Trades = new Dictionary<string, List<Trade>>(NextMinuteTrades);
                    Quotes = new Dictionary<string, List<Quote>>(NextMinuteQuotes);

                    NextMinuteTrades.Clear();
                    NextMinuteQuotes.Clear();

                    if (!AlgoTraderState.IsSim)
                    {


                        var arrays = JSON.DeserializeDynamic(payload);

                        for (int n = 0; n < arrays.Count; n++)
                        {
                            var obj = arrays[n];

                            for (int m = 0; m < obj.Count; m++)
                            {
                                if (obj[m]["ev"] != "status")
                                {
                                    int z = 0;

                                    if (obj[m].ContainsKey("z"))
                                    {
                                        z = (int)obj[m]["z"]; // 2
                                    }

                                    string c = ""; // 0, [13,14]
                                    long t = (long)obj[m]["t"]; // it comes in as milliseconds (1571339927631)




                                    string symbol = ((string)obj[m]["sym"].ToString().ToLower()).Replace("\"", string.Empty);

                                    if (t < 3000000000000)
                                    {
                                        // assume it's in milliseconds
                                        t = t * 1000000;
                                    }





                                    if (obj[m]["c"] is IList)
                                    {
                                        for (int p = 0; p < obj[m]["c"].Count; p++)
                                        {
                                            c += obj[m]["c"][p].ToString() + (p == obj[m]["c"].Count - 1 ? "" : ",");
                                        }
                                    }
                                    else
                                    {
                                        c = obj[m]["c"].ToString();
                                    }

                                    if (obj[m]["ev"] == "T")
                                    {
                                        Trade trade = new Trade();
                                        trade.Conditions = c;
                                        trade.Tape = z;
                                        trade.Timestamp = t;
                                        trade.ExchangeId = (int)obj[m]["x"];
                                        trade.Price = (decimal)obj[m]["p"];
                                        trade.Volume = (int)obj[m]["s"];

                                        Dictionary<string, List<Trade>> tradeBag = null;
                                        if (t >= endNano)
                                        {
                                            tradeBag = NextMinuteTrades;
                                            //tc.Log.AddLine("next minute trade timestamp: " + UCDT.UTCDateTimeToZonedDateTime(UCDT.NanoUnixToDateTime(t), UCDT.TimeZones.Eastern).ToString());
                                        }
                                        else if (t < startNano)
                                        {
                                            tradeBag = PrevMinuteTrades;
                                            //tc.Log.AddLine("prev minute trade timestamp: " + UCDT.UTCDateTimeToZonedDateTime(UCDT.NanoUnixToDateTime(t), UCDT.TimeZones.Eastern).ToString());
                                        }
                                        else
                                        {
                                            tradeBag = Trades;
                                        }

                                        if (!tradeBag.ContainsKey(symbol))
                                        {
                                            tradeBag.Add(symbol, new List<Trade>());
                                        }
                                        tradeBag[symbol].Add(trade);
                                    }
                                    else if (obj[m]["ev"] == "Q")
                                    {
                                        Quote quote = new Quote();
                                        quote.Conditions = c;
                                        quote.Tape = z;
                                        quote.Timestamp = t;
                                        quote.AskExchangeId = (int)obj[m]["ax"];
                                        quote.AskPrice = (decimal)obj[m]["ap"];
                                        quote.AskSize = (int)obj[m]["as"];
                                        quote.BidExchangeId = (int)obj[m]["bx"];
                                        quote.BidPrice = (decimal)obj[m]["bp"];
                                        quote.BidSize = (int)obj[m]["bs"];

                                        Dictionary<string, List<Quote>> quoteBag = null;
                                        if (t >= endNano)
                                        {
                                            quoteBag = NextMinuteQuotes;
                                            //tc.Log.AddLine("next minute quote timestamp: " + UCDT.UTCDateTimeToZonedDateTime(UCDT.NanoUnixToDateTime(t), UCDT.TimeZones.Eastern).ToString());
                                        }
                                        else if (t < startNano)
                                        {
                                            quoteBag = PrevMinuteQuotes;
                                            //tc.Log.AddLine("prev minute quote timestamp: " + UCDT.UTCDateTimeToZonedDateTime(UCDT.NanoUnixToDateTime(t), UCDT.TimeZones.Eastern).ToString());
                                        }
                                        else
                                        {
                                            quoteBag = Quotes;
                                        }

                                        if (!quoteBag.ContainsKey(symbol))
                                        {
                                            quoteBag.Add(symbol, new List<Quote>());
                                        }
                                        quoteBag[symbol].Add(quote);
                                    }
                                }
                                else
                                {
                                    if (obj[m].ContainsKey("status") && obj[m].ContainsKey("message"))
                                    {
                                        tc.Log.AddLine("[Socket] Status: " + obj[m]["status"].ToString() + ", Message: " + obj[m]["message"].ToString());
                                    }
                                }
                            }
                        }



                        // now get startnano and endnano
                        // if there's any ticks past endnano, log about it and temporarily remove them and put them back after process
                        // if there's any ticks before startnano, log loudly about it because they will never be put into a node

                        if (NextMinuteQuotes.Count > 0)
                        {
                            tc.Log.AddLine("WE HAVE NEXT MINUTE QUOTES: " + UC.ListToString(NextMinuteQuotes.Keys.ToList(), ","), Verbosity.Normal);
                        }
                        if (NextMinuteTrades.Count > 0)
                        {
                            tc.Log.AddLine("WE HAVE NEXT MINUTE TRADES: " + UC.ListToString(NextMinuteTrades.Keys.ToList(), ","), Verbosity.Normal);
                        }

                        if (PrevMinuteQuotes.Count > 0)
                        {
                            tc.Log.AddLine("WE HAVE PREVIOUS MINUTE QUOTES: " + UC.ListToString(PrevMinuteQuotes.Keys.ToList(), ","), Verbosity.Normal);
                            PrevMinuteQuotes.Clear();
                        }
                        if (PrevMinuteTrades.Count > 0)
                        {
                            tc.Log.AddLine("WE HAVE PREVIOUS MINUTE TRADES: " + UC.ListToString(PrevMinuteTrades.Keys.ToList(), ","), Verbosity.Normal);
                            PrevMinuteTrades.Clear();
                        }

                    }


                    long totalElapsedAttachingNodes = 0;

                    for (int n = 0; n < CurrentSubscription.Count; n++)
                    {

                        // if first process of the day
                        if (isFirstProcess)
                        {
                            // SHOULD WE DO SOMETHING BETTER THAN THIS?
                            isFirstProcess = false;

                            FD day = AlgoTraderState.CurrentTime.GetFD();

                            DateTime dt = new DateTime(day.DT.Year, day.DT.Month, day.DT.Day, 4, 0, 0, DateTimeKind.Unspecified);

                            startNano = UCDT.DateTimeToNanoUnix(UCDT.ZonedDateTimetoUTCDateTime(dt, UCDT.TimeZones.Eastern));

                            // startNano = (startNano - (startNano % (60000000000L * 1440L)));
                        }


                        NodesData nodes = null;

                        if (AlgoTraderState.IsSim)
                        {
                            nodes = AlgoTraderShared.SimDayNodes[CurrentSubscription[n]].SliceSpawnWithNanos(startNano, endNano);
                        }
                        else
                        {


                            if (Quotes.ContainsKey(CurrentSubscription[n]))
                            {
                                // dedupe them
                                DataMethods.DeDupeQuotes(Quotes[CurrentSubscription[n]]);
                            }
                            if (Trades.ContainsKey(CurrentSubscription[n]))
                            {
                                // dedupe them
                                DataMethods.DeDupeTrades(Trades[CurrentSubscription[n]]);
                            }


                            nodes = DataMethods.BuildNodes(Trades.ContainsKey(CurrentSubscription[n]) ? Trades[CurrentSubscription[n]] : new List<Trade>(), Quotes.ContainsKey(CurrentSubscription[n]) ? Quotes[CurrentSubscription[n]] : new List<Quote>(), startNano, endNano, Interval.HalfSecond);
                            nodes.ComputeNodesNanos();

                        }
                        //if (nodes.Nodes.Count != 120)
                       // {
                        //    tc.Log.AddLine("[" + CurrentSubscription[n] + "] BuildStockNodes RETURNED " + nodes.Nodes.Count + " NODES INSTEAD OF 120");
                        //}

                        int dataNodesCount = 0;

                        if (AlgoTraderShared.NodesData[CurrentSubscription[n]] == null)
                        {

                            AlgoTraderShared.NodesData[CurrentSubscription[n]] = nodes;
                        }
                        else
                        {
                            dataNodesCount = AlgoTraderShared.NodesData[CurrentSubscription[n]].Nodes.Count;

                            Stopwatch sw2 = new Stopwatch();
                            sw2.Start();

                            AlgoTraderShared.NodesData[CurrentSubscription[n]].AttachNodesData(nodes);
                            sw2.Stop();

                            totalElapsedAttachingNodes += sw2.ElapsedMilliseconds;
                        }

                        // AlgoTraderState.NodesData[CurrentSubscription[n]].StockNodes = AlgoTraderState.SymbolDatas[CurrentSubscription[n]].StockNodes.OrderBy(o => o.StartNanoSecond).ToList();

                        int dataNodesCountAfter = AlgoTraderShared.NodesData[CurrentSubscription[n]].Nodes.Count;


                        tc.Log.AddLine("[" + CurrentSubscription[n] + "] Updated stock data nodes from " + dataNodesCount + " to " + dataNodesCountAfter, Verbosity.Minimal);


                        //AlgoTraderState.SymbolDatas[CurrentSubscription[n]].StockNodes.AddRange(AlgoTraderState.FreshNodes[CurrentSubscription[n]]);






                        //tc.Log.AddLine("[" + CurrentSubscription[n] + "] Fresh Nodes Count: " + AlgoTraderState.FreshNodes[CurrentSubscription[n]].Count);
                    }

                    // clear these
                    Trades.Clear();
                    Quotes.Clear();

                    Trades = null;
                    Quotes = null;

                    sw.Stop();

                    tc.Log.AddLine("Done deserializing bucket. Took: " + UC.MillisecondsToSeconds(sw.ElapsedMilliseconds, 4) + " sec(s)", Verbosity.Verbose);
                    tc.Log.AddLine("Attaching nodes total elapsed took: " + UC.MillisecondsToSeconds(totalElapsedAttachingNodes, 4) + " sec(s)", Verbosity.Verbose);

                    tc.AddSignalForParent(Signal.Done);

                    processedCount++;

                }



            }
        }

        public static void SetSubscription(List<string> symbols)
        {

            // intersect: tickers in symbols AND CurrentSubscription
            // except: CurrentSubscription.Except(symbols) is tickers that we need to unsubscribe to
            // except: symbols.Except(CurrentSubscription) is tickers that we need to subscribe to

            List<string> unsubscribeList = CurrentSubscription.Except(symbols).ToList();
            List<string> subscribeList = symbols.Except(CurrentSubscription).ToList();

            CurrentSubscription = symbols;

            if (!AlgoTraderState.IsSim)
            {
                if (subscribeList.Count > 0)
                {
                    string subscribeCommandPart = "";
                    for (int n = 0; n < subscribeList.Count; n++)
                    {
                        subscribeCommandPart += "Q." + subscribeList[n].ToUpper() + "," + "T." + subscribeList[n].ToUpper() + (n == subscribeList.Count - 1 ? "" : ",");
                    }
                    if (IsConnected)
                    {
                        WS.Send("{\"action\":\"subscribe\",\"params\":\"" + subscribeCommandPart + "\"}");
                    }
                }

                if (unsubscribeList.Count > 0)
                {
                    string unsubscribeCommandPart = "";
                    for (int n = 0; n < unsubscribeList.Count; n++)
                    {
                        unsubscribeCommandPart += "Q." + unsubscribeList[n].ToUpper() + "," + "T." + unsubscribeList[n].ToUpper() + (n == unsubscribeList.Count - 1 ? "" : ",");
                    }
                    if (IsConnected)
                    {
                        WS.Send("{\"action\":\"unsubscribe\",\"params\":\"" + unsubscribeCommandPart + "\"}");
                    }
                }
            }
        }

        public static void RunStream(ThreadControl tc)
        {
            int bucketA1Count = 0;
            int bucketA2Count = 0;

            tc.Log.AddLine("Starting RunStream(). ThreadId: " + Thread.CurrentThread.ManagedThreadId + ", IsBackground: " + Thread.CurrentThread.IsBackground + ", Priority: " + Thread.CurrentThread.Priority.ToString());

            WS = new WebSocket("wss://socket.polygon.io/stocks", sslProtocols: SslProtocols.Tls12 | SslProtocols.Tls11 | SslProtocols.Tls);

            WS.Opened += delegate (object sender, EventArgs e)
            {

                tc.Log.AddLine("Connected to WebSocket! Sending Auth");
                WS.Send("{\"action\":\"auth\",\"params\":\"" + StockAPI.Config.Key + "\"}");

                string subscribeCommandPart = "";
                for (int n = 0; n < CurrentSubscription.Count; n++)
                {
                    subscribeCommandPart += "Q." + CurrentSubscription[n].ToUpper() + "," + "T." + CurrentSubscription[n].ToUpper() + (n == CurrentSubscription.Count - 1 ? "" : ",");
                }
                if (!String.IsNullOrWhiteSpace(subscribeCommandPart))
                {
                    WS.Send("{\"action\":\"subscribe\",\"params\":\"" + subscribeCommandPart + "\"}");
                }

                IsConnected = true;
                IsOpeningConnection = false;
            };
            WS.Error += delegate (object sender, SuperSocket.ClientEngine.ErrorEventArgs e)
            {
                tc.Log.AddLine("WebSocket error: " + e.Exception.Message);
            };
            WS.Closed += delegate (object sender, EventArgs e)
            {
                IsConnected = false;
                tc.Log.AddLine("WebSocket connected closed");
            };

            WS.MessageReceived += delegate (object sender, MessageReceivedEventArgs e)
            {
                if (OnBucketA1)
                {
                    bucketA2Count = 0;
                    bucketA1Count++;
                    BucketA1.AddLast(e.Message);
                }
                else
                {
                    bucketA1Count = 0;
                    bucketA2Count++;
                    BucketA2.AddLast(e.Message);
                }
            };

            IsOpeningConnection = true;

            WS.Open();

            int currentSecond = DateTime.UtcNow.Second;
            int lastSecond = currentSecond;

            while (tc.CheckNotStopped())
            {

                currentSecond = DateTime.UtcNow.Second;

                if (currentSecond != lastSecond)
                {
                    lastSecond = currentSecond;
                    tc.Log.AddLine(OnBucketA1 ? "On bucket A1: " + bucketA1Count : "On bucket A2: " + bucketA2Count);
                }
            }

            WS.Close();
            WS.Dispose();

            WS = null;
            
        }
    }

    public class OrdersDataUpdater
    {
        private static bool OnBucketA1 = true;

        public static List<Order> OrdersBucketA1 = new List<Order>();
        public static List<Order> OrdersBucketA2 = new List<Order>();


        public static void ResetDayData()
        {
            OnBucketA1 = true;
            OrdersBucketA1.Clear();
            OrdersBucketA2.Clear();
        }

        public static void Run(ThreadControl tc)
        {
            tc.Log.AddLine("Starting Run(). ThreadId: " + Thread.CurrentThread.ManagedThreadId + ", IsBackground: " + Thread.CurrentThread.IsBackground + ", Priority: " + Thread.CurrentThread.Priority.ToString());

            if (!AlgoTraderState.IsSim)
            {
                ThreadControl streamTC = new ThreadControl("Stream");
                tc.Children.Add(streamTC);
                Task.Factory.StartNew(() => Methods.ThreadRun("AT.AlgoTraderSubProcessors.OrdersDataUpdater, AT", "RunStream", streamTC, null, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            while (tc.CheckNotStopped())
            {
                Thread.Sleep(3);

                Signal signal = tc.PopSignalForChild();

                if (signal == Signal.Process)
                {
                    Stopwatch sw = new Stopwatch();

                    sw.Start();

                    OnBucketA1 = !OnBucketA1;

                    if (AlgoTraderState.UseSimOrdering)
                    {
                        FillNonActiveChangedOrders();
                    }

                    ApplyChangedOrdersToItems();

                    int totalOrdersCount = AlgoTraderShared.Orders.Count;
                    int submittedOrdersCount = AlgoTraderShared.Orders.FindAll(o => o.Status == OrderStatus.Submitted).Count;
                    int filledOrdersCount = AlgoTraderShared.Orders.FindAll(o => o.Status == OrderStatus.Filled).Count;

                    int totalPositionsCount = AlgoTraderShared.Positions.Count;
                    int openPositionsCount = AlgoTraderShared.Positions.FindAll(o => o.Status == PositionStatus.Open).Count;
                    int closedPositionsCount = AlgoTraderShared.Positions.FindAll(o => o.Status == PositionStatus.Closed).Count;

                    List<Position> closedPositions = AlgoTraderShared.Positions.FindAll(o => o.Status == PositionStatus.Closed);

                    decimal net = 0M;

                    for(int n = 0; n < closedPositions.Count; n++)
                    {
                        net += closedPositions[n].SoldAt - closedPositions[n].BoughtAt;
                    }

                    tc.Log.AddLine("Orders: " + totalOrdersCount + ", Open Orders: " + submittedOrdersCount + ", Filled Orders: " + filledOrdersCount);
                    tc.Log.AddLine("Positions: " + totalPositionsCount + ", Open Positions: " + openPositionsCount + ", Closed Positions: " + closedPositionsCount);
                    tc.Log.AddLine("Profit: " + UC.DecimalToUSD(net));

                    sw.Stop();

                    tc.Log.AddLine("UpdateOrders took " + UC.MillisecondsToSeconds(sw.ElapsedMilliseconds, 4) + " sec(s)");
                    
                    tc.AddSignalForParent(Signal.Done);
                }
            }
        }


        private static void FillNonActiveChangedOrders()
        {
            List<Order> target = OnBucketA1 ? OrdersBucketA1 : OrdersBucketA2;

            // want to get active orders where events can occur (submitted, WaitingForAPI, partially filled, pending, etc)
            List<Order> activeOrders = UC.Clone(AlgoTraderShared.Orders.FindAll(o => o.IsActive()));

            List<string> changedOrderIds = new List<string>();

            // just go straight to submitted
            activeOrders.FindAll(o => o.Status == OrderStatus.WaitingToSendToAPI).ForEach(o =>
            {
                o.Status = OrderStatus.Submitted;
                o.APIId = UC.RandomString(36);
                o.CreatedAt = o.InstanceCreatedAt + 1;
                o.UpdatedAt = o.InstanceCreatedAt + 3;
                o.SubmittedAt = o.InstanceCreatedAt + 3;
                changedOrderIds.Add(o.Id);
            });


            // i think this will cause problems... 
            // these pending canceled orders could have
            // been filled between start of the minute to when 
            // the action to cancel it was executed
            // FIX THIS
            activeOrders.FindAll(o => o.Status == OrderStatus.PendingCancel).ForEach(o =>
            {
                o.Status = OrderStatus.Cancelled;
                o.CanceledAt = o.PendingCancelAt + 1;
                o.UpdatedAt = o.CanceledAt;
                changedOrderIds.Add(o.Id);
            });



            List<Order> ordersToCheck = activeOrders.FindAll(o => o.Status == OrderStatus.Submitted || o.Status == OrderStatus.PartiallyFilled);

            long currentNano = AlgoTraderMethods.GetRealTimeCurrentNano();
            long endNano = (currentNano - (currentNano % 60000000000L)); // last minute
            long startNano = endNano - 60000000000L; // last minute

            // this could be optimized to group ordersToCheck by symbol
            // or add parallelism
            for (int n = 0; n < ordersToCheck.Count; n++)
            {
                Order order = ordersToCheck[n];

                List<Node> nodes = AlgoTraderShared.NodesData[order.Symbol].GetNodesWithNanos(startNano, endNano);

                //if (nodes.Count != 120)
                //{
                //    throw new Exception("Nodes should be a full minute");
                //}

                for (int m = 0; m < nodes.Count; m++)
                {
                    Node node = nodes[m];

                    // make sure order submitted is on or after quote timestamp
                    if (order.SubmittedAt >= node.StartNano)
                    {

                        // if stop limit or stop (code is better when grouped)
                        if (order.Type == OrderType.StopLimit || order.Type == OrderType.Stop)
                        {
                            // if buy and askprice fell below stop price
                            if (order.Side == OrderSide.Buy && ((order.PriceAtCreation >= order.StopPrice && node.AskPrice <= order.StopPrice) || (order.PriceAtCreation < order.StopPrice && node.AskPrice >= order.StopPrice)))
                            {
                                // convert to limit or market
                                order.Type = (order.Type == OrderType.StopLimit ? OrderType.Limit : OrderType.Market);
                                order.StopTriggeredAt = node.StartNano;
                                order.UpdatedAt = node.StartNano;
                                changedOrderIds.Add(order.Id);
                            }
                            else if (order.Side == OrderSide.Sell && ((order.PriceAtCreation >= order.StopPrice && node.BidPrice <= order.StopPrice) || (order.PriceAtCreation < order.StopPrice && node.BidPrice >= order.StopPrice)))
                            {
                                // convert to limit
                                order.Type = (order.Type == OrderType.StopLimit ? OrderType.Limit : OrderType.Market);
                                order.StopTriggeredAt = node.StartNano;
                                order.UpdatedAt = node.EndNano;
                                changedOrderIds.Add(order.Id);
                            }
                        }

                        if (order.Type == OrderType.Market)
                        {
                            decimal price = GetMarketPrice(order, node);
                            order.AverageFilledPrice = price;
                            order.FilledAt = node.StartNano;
                            order.FilledQuantity = order.Quantity;
                            order.UpdatedAt = node.StartNano;
                            order.Status = OrderStatus.Filled;
                            changedOrderIds.Add(order.Id);
                            break;
                        }
                        else if (order.Type == OrderType.Limit)
                        {
                            if (order.Side == OrderSide.Buy && node.AskPrice <= order.LimitPrice)
                            {
                                // TODO: implement a partial fill estimator

                                int amountFilled = order.Quantity - order.FilledQuantity;

                                // fully filled
                                order.FilledAt = node.StartNano;
                                order.AverageFilledPrice = ((order.AverageFilledPrice * (decimal)order.FilledQuantity) + (node.AskPrice * (decimal)amountFilled)) / (decimal)order.Quantity;
                                order.UpdatedAt = node.StartNano;
                                order.Status = OrderStatus.Filled;
                                order.FilledQuantity = order.Quantity;
                                changedOrderIds.Add(order.Id);
                                break;
                            }
                            else if (order.Side == OrderSide.Sell && node.BidPrice >= order.LimitPrice)
                            {
                                // TODO: implement a partial fill estimator

                                int amountFilled = order.Quantity - order.FilledQuantity;

                                // fully filled
                                order.FilledAt = node.StartNano;
                                order.AverageFilledPrice = ((order.AverageFilledPrice * (decimal)order.FilledQuantity) + (node.BidPrice * (decimal)amountFilled)) / (decimal)order.Quantity;
                                order.UpdatedAt = node.StartNano;
                                order.Status = OrderStatus.Filled;
                                order.FilledQuantity = order.Quantity;
                                changedOrderIds.Add(order.Id);
                                break;
                            }
                        }

                    }
                }

            }


            // now add changed order to target

            // remove dupes
            changedOrderIds = changedOrderIds.Distinct().ToList();

            for (int n = 0; n < changedOrderIds.Count; n++)
            {
                target.Add(activeOrders.Find(o => o.Id == changedOrderIds[n]));
            }

        }

        private static void ApplyChangedOrdersToItems()
        {
            List<Order> ordersChanged = OnBucketA1 ? OrdersBucketA2 : OrdersBucketA1;


            // THERE COULD BE CANCELLED ITEMS ALONG WITH 
            // FILLED ITEMS WITH THE SAME ORDER ID HERE
            // NEED TO RECONCILE THESE HERE


            // replace orders in Orders with ordersChanged that match Id
            for (int n = 0; n < ordersChanged.Count; n++)
            {
                Order order = ordersChanged[n];

                // replace in ordermanager orders list
                // replace in tradingmanager orders list
                // replace in symbol watcher orders list
                // replace in strategy orders list

                AlgoTraderShared.Orders[AlgoTraderShared.Orders.FindIndex(o => o.Id == order.Id)] = order;
                //AlgoTraderState.TradingManager.Orders[AlgoTraderState.TradingManager.Orders.FindIndex(o => o.Id == order.Id)] = order;
                //AlgoTraderState.TradingManager.SymbolManagers[order.Symbol].Orders[AlgoTraderState.TradingManager.SymbolManagers[order.Symbol].Orders.FindIndex(o => o.Id == order.Id)] = order;
                //AlgoTraderState.TradingManager.SymbolManagers[order.Symbol].Strategies[order.StrategyId].Orders[AlgoTraderState.TradingManager.SymbolManagers[order.Symbol].Strategies[order.StrategyId].Orders.FindIndex(o => o.Id == order.Id)] = order;
            }

            List<Order> filledOrders = ordersChanged.FindAll(o => o.Status == OrderStatus.Filled);

            // update the positions on the newly filled orders
            for (int n = 0; n < filledOrders.Count; n++)
            {
                Order order = filledOrders[n];

                Position position = AlgoTraderShared.Positions.Find(p => p.Id == order.PositionId);

                if (order.Side == OrderSide.Buy)
                {
                    position.BoughtAt = order.AverageFilledPrice;
                    position.BoughtQuantity = order.Quantity;
                }
                else if (order.Side == OrderSide.Sell)
                {
                    position.SoldAt = order.AverageFilledPrice;
                    position.SoldQuantity = order.Quantity;
                }

                if (position.Status == PositionStatus.Pending)
                {
                    position.Status = PositionStatus.Open;
                    position.OpenedAt = order.FilledAt;
                }
                else if (position.Status == PositionStatus.Open)
                {
                    position.Status = PositionStatus.Closed;
                    position.ClosedAt = order.FilledAt;
                }
            }

            // clear it fresh for streamer on next minute
            ordersChanged.Clear();
        }

        private static decimal GetMarketPrice(Order order, Node node)
        {

            decimal result = 0M;

            if (order.Side == OrderSide.Buy)
            {
                result = node.AskPrice;
            }
            else if (order.Side == OrderSide.Sell)
            {
                result = node.BidPrice;
            }

            return result;
        }


        public static void RunStream(ThreadControl tc)
        {
            int currentSecond = DateTime.UtcNow.Second;
            int lastSecond = currentSecond;

            while (tc.CheckNotStopped())
            {
                Thread.Sleep(10);

                currentSecond = DateTime.UtcNow.Second;

                if (currentSecond != lastSecond)
                {
                    lastSecond = currentSecond;
                    tc.Log.AddLine(OnBucketA1 ? "On bucket A1" : "On bucket A2");
                }
            }
        }
    }


    /// <summary>
    /// - Gets actions and possibly executes them
    /// - Keeps track of positions
    /// - Risk management
    /// </summary>
    public class TradingManager
    {



        public static void PrepareWatchList(ThreadControl tc)
        {
            tc.Log.AddLine("Building WatchList", Verbosity.Normal);

            AlgoTraderShared.WatchList.AddRange(AlgoTraderMethods.BuildWatchItems());

            // assuming today is a data day


            


            //FD lastDataDay = AlgoTraderState.CurrentDay.DeepCopy();
            //lastDataDay.AddDays(-1);

            FD startDay = AlgoTraderState.CurrentDay.DeepCopy();
            FD endDay = startDay.DeepCopy();

            startDay.AddDays(-2);
            endDay.AddDays(-1);

            //startDay.AddDays(-2);
            //endDay.AddDays(-1);



            // fill the watchlist nodes
            Parallel.For(0, AlgoTraderShared.WatchList.Count, new ParallelOptions { MaxDegreeOfParallelism = 30 }, n =>
            {
                //WatchList[n].Nodes = DataMethods.GetDayNodesDataSeries(WatchList[n].Symbol, startDay, endDay, Interval.HalfSecond);
                AlgoTraderShared.WatchListNodesData.Add(AlgoTraderShared.WatchList[(int)n].Symbol, DataMethods.GetDayNodesDataSeries(AlgoTraderShared.WatchList[(int)n].Symbol, startDay, endDay, Interval.HalfSecond));
                tc.Log.AddLine("WatchList nodes built for " + AlgoTraderShared.WatchList[(int)n].Symbol, Verbosity.Verbose);

            });



            
            tc.Log.AddLine("WatchList Built.", Verbosity.Normal);

            // tc.AddSignalForParent(Signal.Done);

            AlgoTraderState.WatchListBuilt = true;
        }

        private static void PrepareOrMergeOrWaitForWatchList(ThreadControl tc)
        {
            if (!AlgoTraderState.WatchListBuilt && !AlgoTraderState.WatchListBuilding && AlgoTraderState.CurrentTime.IsOnOrPassed(8, 0))
            {
                // prepare
                AlgoTraderState.WatchListBuilding = true;
                ThreadControl watchListTC = new ThreadControl("Watch List Builder");
                tc.Children.Add(watchListTC);
                Task.Factory.StartNew(() => Methods.ThreadRun("AT.AlgoTraderSubProcessors.TradingManager, AT", "PrepareWatchList", watchListTC, null, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);
            }

            if (!AlgoTraderState.WatchListBuilt && AlgoTraderState.WatchListBuilding && AlgoTraderState.CurrentTime.IsOnOrPassed(9, 30))
            {
                tc.Log.AddLine("Waiting for watch list to be built...");
                // wait
                while (tc.CheckNotStopped() && !AlgoTraderState.WatchListBuilt)
                {
                    // wait for it to be built
                }
            }

            if (AlgoTraderState.WatchListBuilt && AlgoTraderState.WatchListBuilding)
            {
                tc.Log.AddLine("Watch list built. Will AttachNodesData WatchListNodesData.");
                // merge
                AlgoTraderState.WatchListBuilding = false;

                List<string> watchListSymbols = new List<string>();

                foreach (string key in AlgoTraderShared.WatchListNodesData.Keys)
                {
                    watchListSymbols.Add(key);
                    // add (prepend) the node data to the existing data we got from the stream updates
                    AlgoTraderShared.NodesData[key].AttachNodesData(AlgoTraderShared.WatchListNodesData[key]);
                }

                StocksDataUpdater.SetSubscription(watchListSymbols);
            }
        }
        public static void Run(ThreadControl tc)
        {
            //int lastMinute = AlgoTraderState.CurrentTime.GetMinute();
            //int currentMinute;

            AlgoTraderState.TradingManagerRunning = true;

            // probably necessary
            // Thread.Sleep(500);

            while (tc.CheckNotStopped())
            {

                //currentMinute = AlgoTraderState.CurrentTime.GetMinute();

                // if (currentMinute != lastMinute)
                if (AlgoTraderState.CurrentTime.IsNewMinute())
                {
                    // pause happens in CurrentTime

                    tc.Log.AddLine("New minute hit: " + AlgoTraderState.CurrentTime.GetTime().ToString());

                    AlgoTraderState.MinuteStopWatch.Start();

                    //lastMinute = currentMinute;

                    PrepareOrMergeOrWaitForWatchList(tc);

                    // only between 4:01am and signal stocks data updater to process, then wait for done
                    if (AlgoTraderState.CurrentTime.IsBetween(4, 1, 16, 1))
                    {
                        ThreadControl.SignalChildAndWaitForParentSignal(AlgoTraderState.StocksDataUpdaterTC, tc, Signal.Process, Signal.Done);
                    }


                    if (AlgoTraderState.CurrentTime.IsBetween(9, 31, 16, 1))
                    {
                        // signal orders data updater to process, then wait for done
                        ThreadControl.SignalChildAndWaitForParentSignal(AlgoTraderState.OrdersDataUpdaterTC, tc, Signal.Process, Signal.Done);
                    }

                    //AlgoTraderShared.CuratedStrategyActions.Clear();
                    //AlgoTraderShared.StrategyActions.Clear();

                    int actionsCount = 0;

                    // if time [9:30:00-3:59:00)
                    if (AlgoTraderState.CurrentTime.IsBetween(9, 30, 15, 59))
                    {

                        // first compute all actions

                        object lockObj = new object();

                        Parallel.For(0, AlgoTraderShared.WatchList.Count, n =>
                        {
                            for (int m = 0; m < AlgoTraderShared.WatchList[n].Strategies.Count; m++)
                            {
                                StrategyOrderActions sa = AlgoTraderShared.WatchList[n].Strategies[m].ComputeActions();
                                if(sa != null)
                                {
                                    lock (lockObj)
                                    {
                                        AlgoTraderShared.StrategyActions.Add(sa);
                                    }
                                }
                            }
                        });

                        // now curate actions by determining risk and calculate sizing
                        List<StrategyOrderActions> strategyActions = AlgoTraderShared.StrategyActions.FindAll(sa => sa.Status == StrategyOrderActionsStatus.PendingDecision);

                        Parallel.For(0, strategyActions.Count, n =>
                        {

                            // TODO: determine risk and strategically calculate sizing

                            for (int m = 0; m < strategyActions[n].OrderActions.Count; m++)
                            {
                                strategyActions[n].OrderActions[m].Quantity = 1;
                            }

                            lock (lockObj)
                            {
                                int rndNum = UC.GetRandomInteger(1, 20);
                                if(rndNum == 3)
                                {
                                    strategyActions[n].Status = StrategyOrderActionsStatus.Rejected;
                                }
                                else
                                {
                                    strategyActions[n].Status = StrategyOrderActionsStatus.Approved;
                                    actionsCount++;
                                }
                            }

                        });

                    }
                    else if (AlgoTraderState.CurrentTime.IsOn(15, 59))
                    {
                        // cancel all open orders

                        List<Order> cancelableOrders = AlgoTraderShared.Orders.FindAll(o => o.IsCancelable());
                        for (int n = 0; n < cancelableOrders.Count; n++)
                        {
                            Order order = cancelableOrders[n];
                            OrderAction oa = new OrderAction();
                            oa.Type = OrderActionType.Cancel;
                            oa.OrderId = order.Id;
                            oa.Quantity = 1;

                            StrategyOrderActions sa = new StrategyOrderActions();
                            sa.Status = StrategyOrderActionsStatus.Approved;
                            sa.OrderActions = new List<OrderAction>() { oa };
                            sa.PositionId = order.PositionId;
                            sa.StrategyId = order.StrategyId;
                            sa.StrategyName = order.StrategyName;
                            sa.Symbol = order.Symbol;

                            AlgoTraderShared.StrategyActions.Add(sa);
                            actionsCount++;
                        }

                        tc.Log.AddLine("It's 3:59pm. Created " + cancelableOrders.Count + " actions to cancel orders.");

                    }

                    // if time [9:30:00-4:00:00)
                    if (AlgoTraderState.CurrentTime.IsBetween(9, 30, 16, 0))
                    {
                        tc.Log.AddLine("Executing " + actionsCount + " action(s)");
                        ExecuteActions();
                    }


                    bool breakLoop = false;
                    if(AlgoTraderState.CurrentTime.IsOn(16, 0))
                    {
                        tc.Log.AddLine("It's 4pm. Breaking.");
                        breakLoop = true;
                    }

                    
                    // if the ui is subscribed to data, this builds the payload sent to the browser        
                    AlgoTraderUI.TryBuildPayload();
                     

                    tc.Log.AddLine("Minute " + AlgoTraderState.CurrentTime.GetMinute() + " processed in " + UC.MillisecondsToSeconds(AlgoTraderState.MinuteStopWatch.ElapsedMilliseconds, 4) + " sec(s)");

                    AlgoTraderState.MinuteStopWatch.Reset();

                    

                    if (breakLoop)
                    {
                        break;
                    }

                    // AlgoTraderMethods.UnPause();
                }


            }

            

        }


        
        /*
        private void DistributePendingItems()
        {
            Positions.AddRange(PendingPositions);
            Orders.AddRange(PendingOrders);

            AlgoTraderState.TradingManager.Positions.AddRange(PendingPositions);
            AlgoTraderState.TradingManager.Orders.AddRange(PendingOrders);

            for (int n = 0; n < PendingPositions.Count; n++)
            {
                Position position = PendingPositions[n];

                AlgoTraderState.TradingManager.SymbolManagers[position.Symbol].Positions.Add(position);
                AlgoTraderState.TradingManager.SymbolManagers[position.Symbol].Strategies[position.StrategyId].Positions.Add(position);
            }

            for (int n = 0; n < PendingOrders.Count; n++)
            {
                Order order = PendingOrders[n];

                AlgoTraderState.TradingManager.SymbolManagers[order.Symbol].Orders.Add(order);
                AlgoTraderState.TradingManager.SymbolManagers[order.Symbol].Strategies[order.StrategyId].Orders.Add(order);
            }
        }
        */

        private static void ExecuteActions()
        {
            List<StrategyOrderActions> approvedStrategyActions = AlgoTraderShared.StrategyActions.FindAll(sa => sa.Status == StrategyOrderActionsStatus.Approved);

            for (int n = 0; n < approvedStrategyActions.Count; n++)
            {
                StrategyOrderActions sa = approvedStrategyActions[n];

                Position position = null;

                if (String.IsNullOrWhiteSpace(sa.PositionId))
                {
                    position = new Position();
                    position.Id = sa.Symbol + "_" + sa.StrategyId + "_" + UC.RandomString(10);
                    position.Symbol = sa.Symbol;
                    position.StrategyId = sa.StrategyId;
                    position.StrategyName = sa.StrategyName;
                    position.Status = PositionStatus.Pending;
                    position.Side = sa.PositionSide;
                    position.PendingAt = AlgoTraderMethods.GetRealTimeCurrentNano();

                    AlgoTraderShared.Positions.Add(position);
                }




                for (int m = 0; m < sa.OrderActions.Count; m++)
                {
                    OrderAction oa = sa.OrderActions[m];

                    if (oa.Type == OrderActionType.Cancel)
                    {
                        // check to make sure we can cancel this;

                        Order order = AlgoTraderShared.Orders.Find(o => o.Id == oa.OrderId);

                        if (order.IsCancelable())
                        {
                            order.Status = OrderStatus.PendingCancel;
                            order.PendingCancelAt = AlgoTraderMethods.GetRealTimeCurrentNano();

                            // make API call to cancel

                        }
                        else
                        {
                            throw new Exception("Trying to cancel an uncancelable order eh?");
                        }
                    }
                    else if (oa.Type == OrderActionType.Place)
                    {
                        Order order = new Order();
                        order.Status = OrderStatus.WaitingToSendToAPI;

                        order.PositionId = String.IsNullOrWhiteSpace(sa.PositionId) ? position.Id : sa.PositionId;
                        order.Id = sa.Symbol + "_" + sa.StrategyId + "_" + UC.RandomString(10);
                        order.Symbol = sa.Symbol;
                        order.StrategyId = sa.StrategyId;
                        order.StrategyName = sa.StrategyName;
                        order.InstanceCreatedAt = AlgoTraderMethods.GetRealTimeCurrentNano();
                        order.Quantity = oa.Quantity;
                        order.Type = oa.OrderType;
                        order.Side = oa.OrderSide;
                        order.TimeInForce = oa.TimeInForce;
                        order.LimitPrice = oa.LimitPrice;
                        order.StopPrice = oa.StopPrice;

                        if (order.Type == OrderType.Stop || order.Type == OrderType.StopLimit)
                        {
                            order.PriceAtCreation = AlgoTraderShared.NodesData[order.Symbol].Nodes[AlgoTraderShared.NodesData[order.Symbol].Nodes.Count - 1].TradePrice;
                        }
                        AlgoTraderShared.Orders.Add(order);

                        // make API call to cancel
                    }
                }

                sa.Status = StrategyOrderActionsStatus.Executed;
            }
        }



    }

}
