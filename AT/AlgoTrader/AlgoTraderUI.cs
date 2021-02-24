using AT.AlgoTraderEnums;
using AT.AlgoTraderObjects;
using AT.UIObjects;
using AT.AlgoTraderSubProcessors;
using AT.DataObjects;
using AT.Tools;
using Jil;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT
{
    public class AlgoTraderUI
    {
        public static long GraphNanoSecondsPerXPixel = 500000000L; // half second
        public static decimal GraphPricePerYPixel = 0.0001M;
        public static int GraphCanvasWidth = 0;
        public static int GraphCanvasHeight = 0;




        public static string SelectedSymbol = "";
        public static string SelectedStrategyId = "";

        // true if UI subscribes to any algo trader subscriptions
        public static bool IsSubscribed = false;

        public static int Frequency = 1; // in minutes
        public static int Current = 0; // in minutes
        public static object LockObj = new object();

        public static string Payload = null;

        public static Dictionary<string, DataTableSort> DataTableSorts = new Dictionary<string, DataTableSort>();

        public static void UnsubscribeToAll()
        {
            lock (LockObj)
            {
                string[] keys = Global.State.AlgoTraderSubscriptions.Keys.ToArray();
                for (int n = 0; n < keys.Length; n++)
                {
                    Global.State.AlgoTraderSubscriptions[keys[n]] = false;
                }
            }

            
            AlgoTraderUI.IsSubscribed = false;
        }
        public static bool IsUnsubscribedToAll()
        {
            lock (LockObj)
            {
                return !Global.State.AlgoTraderSubscriptions.Values.ToList().Exists(s => s == true);
            }
        }



        public static void TryBuildPayload()
        {
            if (IsSubscribed)
            {
                Current++;
                if (Current >= Frequency)
                {
                    Current = 0;
                    lock (LockObj)
                    {


                        




                        Dictionary<string, string> items = new Dictionary<string, string>();

                        if (Global.State.AlgoTraderSubscriptions["main"] == true)
                        {
                            AlgoTraderMain main = new AlgoTraderMain();
                            main.CurrentTime = AlgoTraderState.CurrentTime.ToString();
                            main.SelectedSymbol = SelectedSymbol;
                            main.SelectedStrategyName = AlgoTraderMethods.GetStrategyName(SelectedStrategyId);
                            main.SelectedStrategyId = SelectedStrategyId;

                            using (var output = new StringWriter())
                            {
                                JSON.Serialize(main, output, Options.PrettyPrint);
                                items.Add("algoTraderMain", output.ToString());
                            }
                        }


                        if (Global.State.AlgoTraderSubscriptions["log"] == true)
                        {
                            UIObjects.AlgoTraderLog log = new UIObjects.AlgoTraderLog();
                            log.Log = Global.State.AlgoTrader.TC.Log.Read(Verbosity.Verbose);
                            //log.Log = System.Web.HttpUtility.JavaScriptStringEncode(Global.State.AlgoTrader.TC.Log.Read(Verbosity.Verbose));

                            using (var output = new StringWriter())
                            {
                                JSON.Serialize(log, output, Options.PrettyPrint);
                                items.Add("algoTraderLog", output.ToString());
                            }
                        }

                        if (Global.State.AlgoTraderSubscriptions["overview"] == true)
                        {
                            AlgoTraderOverview overview = buildOverviewObject();

                            using (var output = new StringWriter())
                            {
                                JSON.Serialize(overview, output, Options.PrettyPrint);
                                items.Add("algoTraderOverview", output.ToString());
                            }
                        }

                        if (Global.State.AlgoTraderSubscriptions["chart"] == true)
                        {
                            AlgoTraderChart chart = buildChartObject();

                            using (var output = new StringWriter())
                            {
                                JSON.Serialize(chart, output, Options.Default);
                                items.Add("algoTraderChart", output.ToString());
                            }
                        }


                        Payload = SerializerMethods.DictionarySerializedValuesToJSON(items);
                    }
                }
            }
        }

        public static AlgoTraderChart buildChartObject()
        {
            AlgoTraderChart result = new AlgoTraderChart();

            if(
                AlgoTraderShared.NodesData == null || 
                !AlgoTraderShared.NodesData.ContainsKey(SelectedSymbol) ||
                AlgoTraderShared.NodesData[SelectedSymbol] == null ||
                AlgoTraderShared.NodesData[SelectedSymbol].Nodes == null ||
                AlgoTraderShared.NodesData[SelectedSymbol].Nodes.Count == 0 ||
                AlgoTraderShared.NodesData[SelectedSymbol].Nodes.Count < (GraphCanvasWidth) + 500
            )
            {
                return result;
            }

            int lastNodeIndex = AlgoTraderShared.NodesData[SelectedSymbol].Nodes.Count - 1;
            int startNodeIndex = (lastNodeIndex - (GraphCanvasWidth)) + 1;

            decimal midPrice = AlgoTraderShared.NodesData[SelectedSymbol].Nodes[lastNodeIndex].TradePrice;
            decimal askPrice = AlgoTraderShared.NodesData[SelectedSymbol].Nodes[lastNodeIndex].AskPrice;
            decimal bidPrice = AlgoTraderShared.NodesData[SelectedSymbol].Nodes[lastNodeIndex].BidPrice;

            result.CurrentTradePrice = UC.DecimalToUSD(midPrice, 2);
            result.CurrentAskPrice = UC.DecimalToUSD(askPrice, 2);
            result.CurrentBidPrice = UC.DecimalToUSD(bidPrice, 2);

            //decimal highPrice = midPrice + 0.20M;
            //decimal lowPrice = midPrice - 0.20M;

            decimal highPrice = midPrice + 0.8M;
            decimal lowPrice = midPrice - 0.8M;

            decimal priceSpan = highPrice - lowPrice;
            // high price plot is 0
            // low price plot is GraphCanvasHeight


            result.Data.Add(new List<AlgoTraderChartPoint>()); // tradeprice
            result.Data.Add(new List<AlgoTraderChartPoint>()); // ask price
            result.Data.Add(new List<AlgoTraderChartPoint>()); // bid price

            //int count = 0;

            decimal askPriceYAvgLast = 0M;
            decimal bidPriceYAvgLast = 0M;
            decimal tradePriceYAvgLast = 0M;

            for (int n = startNodeIndex; n <= lastNodeIndex; n+= 1)
            {
                decimal askPriceYAvg = 0M;
                decimal bidPriceYAvg = 0M;
                decimal tradePriceYAvg = 0M;

                int count = 0;

                bool askEstimated = false;
                bool bidEstimated = false;
                bool tradeEstimated = false;

                for (int m = 0; m < 1; m++)
                {
                    if(n + m < AlgoTraderShared.NodesData[SelectedSymbol].Nodes.Count)
                    {
                        Node node = AlgoTraderShared.NodesData[SelectedSymbol].Nodes[n + m];

                        if (node.AskPriceEstimated)
                        {
                            askEstimated = true;
                        }

                        if (node.BidPriceEstimated)
                        {
                            bidEstimated = true;
                        }
                        if (node.TradePriceEstimated)
                        {
                            tradeEstimated = true;
                        }

                        askPriceYAvg += ((highPrice - node.AskPrice) / priceSpan) * GraphCanvasHeight;
                        bidPriceYAvg += ((highPrice - node.BidPrice) / priceSpan) * GraphCanvasHeight;
                        tradePriceYAvg += ((highPrice - node.TradePrice) / priceSpan) * GraphCanvasHeight;
                        count++;
                    }
                    
                }


                askPriceYAvg = askPriceYAvg / count;
                bidPriceYAvg = bidPriceYAvg / count;
                tradePriceYAvg = tradePriceYAvg / count;


                AlgoTraderChartPoint ask_atcp = new AlgoTraderChartPoint();
                AlgoTraderChartPoint bid_atcp = new AlgoTraderChartPoint();
                AlgoTraderChartPoint trade_atcp = new AlgoTraderChartPoint();

                ask_atcp.Y = askPriceYAvg;
                bid_atcp.Y = bidPriceYAvg;
                trade_atcp.Y = tradePriceYAvg;


                if(askPriceYAvg < askPriceYAvgLast)
                {
                    ask_atcp.R = 1D;
                    ask_atcp.G = 0D;
                    ask_atcp.B = 0D;
                }
                else if (askPriceYAvg > askPriceYAvgLast)
                {
                    ask_atcp.R = 1D;
                    ask_atcp.G = 0D;
                    ask_atcp.B = 0D;
                }
                else
                {
                    ask_atcp.R = 1D;
                    ask_atcp.G = 0D;
                    ask_atcp.B = 0D;
                }
                if (askEstimated)
                {
                    //ask_atcp.R = 0.4D;
                    ask_atcp.A = 0.15D;
                }
                else
                {
                    ask_atcp.A = 1D;
                }





                if (bidPriceYAvg < bidPriceYAvgLast)
                {
                    bid_atcp.R = 0D;
                    bid_atcp.G = 0D;
                    bid_atcp.B = 1D;
                }
                else if (bidPriceYAvg > bidPriceYAvgLast)
                {
                    bid_atcp.R = 0D;
                    bid_atcp.G = 0D;
                    bid_atcp.B = 1D;
                }
                else
                {
                    bid_atcp.R = 0D;
                    bid_atcp.G = 0D;
                    bid_atcp.B = 1D;
                }
                if (bidEstimated)
                {
                    //bid_atcp.B = 0.4D;
                    bid_atcp.A = 0.15D;
                }
                else
                {
                    bid_atcp.A = 1D;
                }





                if (tradePriceYAvg < tradePriceYAvgLast)
                {
                    trade_atcp.R = 0D;
                    trade_atcp.G = 1D;
                    trade_atcp.B = 0D;
                }
                else if (tradePriceYAvg > tradePriceYAvgLast)
                {
                    trade_atcp.R = 0D;
                    trade_atcp.G = 1D;
                    trade_atcp.B = 0D;
                }
                else
                {
                    trade_atcp.R = 0D;
                    trade_atcp.G = 1D;
                    trade_atcp.B = 0D;
                }
                if (tradeEstimated)
                {
                    //bid_atcp.G = 0.4D;
                    trade_atcp.A = 0.15D;
                }
                else
                {
                    trade_atcp.A = 1D;
                }




                askPriceYAvgLast = askPriceYAvg;
                bidPriceYAvgLast = bidPriceYAvg;
                tradePriceYAvgLast = tradePriceYAvg;


                result.Data[0].Add(trade_atcp);
                result.Data[1].Add(ask_atcp);
                result.Data[2].Add(bid_atcp);





            }


            return result;
            //long lastEndNano = AlgoTraderShared.NodesData[SelectedSymbol].Nodes[lastNodeIndex].EndNano;

            //long chartTimeSpan = AlgoTraderShared.NodesData[SelectedSymbol].GetNodesWithNanos
            
        }
        public static AlgoTraderOverview buildOverviewObject()
        {
            AlgoTraderOverview result = new AlgoTraderOverview();


            /* ###################### SYMBOLS ###################### */

            AlgoTraderDataTable symbolsDataTable = new AlgoTraderDataTable();

            symbolsDataTable.Name = "overview-symbols";

            if (String.IsNullOrEmpty(SelectedSymbol))
            {
                symbolsDataTable.HideColumn = (!String.IsNullOrEmpty(SelectedStrategyId) ? 1 : -1);
                symbolsDataTable.Show = true;
                symbolsDataTable.Title = "Symbols (" + AlgoTraderShared.WatchList.Count.ToString() + ")";

                StringBuilder sb = new StringBuilder();

                // name, strategies, positions, orders, actions, realized, unrealized

                List<DataTableItem> dataTableItems = new List<DataTableItem>();
                for(int n = 0; n < AlgoTraderShared.WatchList.Count; n++)
                {
                    WatchItem wi = AlgoTraderShared.WatchList[n];

                    string symbol = wi.Symbol;

                    DataTableItem dataTableItem = new DataTableItem();

                    dataTableItem.ColumnValues.Add("symbol", wi.Symbol);
                    dataTableItem.ColumnValues.Add("strategies", wi.Strategies.Count);



                    //dataTableItem.Name = wi.Symbol;
                    //dataTableItem.SubItems = wi.Strategies.Count;

                    dataTableItem.ColumnValues.Add("orders", AlgoTraderShared.Orders.FindAll(o => o.Symbol == symbol).Count);
                    dataTableItem.ColumnValues.Add("actions", AlgoTraderShared.StrategyActions.FindAll(a => a.Symbol == symbol).Count);

                    List<Position> positions = AlgoTraderShared.Positions.FindAll(p => p.Symbol == symbol);

                    dataTableItem.ColumnValues.Add("positions", positions.Count);

                    // do realized
                    List<Position> closedPositions = positions.FindAll(p => p.Status == PositionStatus.Closed);
                    decimal netRealized = 0M;
                    for(int m = 0; m < closedPositions.Count; m++)
                    {
                        netRealized += closedPositions[m].SoldAt - closedPositions[m].BoughtAt;
                    }
                    dataTableItem.ColumnValues.Add("realized", netRealized);

                    // do unrealized
                    List<Position> openPositions = positions.FindAll(p => p.Status == PositionStatus.Open);
                    decimal latestAskPrice = -1M;
                    decimal latestBidPrice = -1M;
                    if (openPositions.Count > 0)
                    {
                        List<Node> nodes = AlgoTraderShared.NodesData[symbol].Nodes;
                        latestAskPrice = nodes[nodes.Count - 1].AskPrice;
                        latestBidPrice = nodes[nodes.Count - 1].BidPrice;
                    }
                    decimal netUnrealized = 0M;


                    for (int m = 0; m < openPositions.Count; m++)
                    {
                        Position p = openPositions[m];
                        decimal sellPrice = 0M;
                        decimal buyPrice = 0M;

                        if (p.Side == PositionSide.Long)
                        {
                            // if long, find the sell price by getting the latest bid price
                            sellPrice = latestBidPrice;
                            buyPrice = p.BoughtAt;
                        }
                        else if (p.Side == PositionSide.Short)
                        {
                            // if short, find the buy price by getting the latest ask price
                            buyPrice = latestAskPrice;
                            sellPrice = p.SoldAt;
                        }
                        else
                        {
                            throw new Exception("a what?");
                        }
                        netUnrealized += sellPrice - buyPrice;
                    }
                    dataTableItem.ColumnValues.Add("unrealized", netUnrealized);

                    dataTableItems.Add(dataTableItem);


                }

                
                TrySortDataTable(ref dataTableItems, "overview-symbols", "symbol");

                // // name, strategies, positions, orders, actions, realized, unrealized
                // <tr><td></td><td class=\"sub-items-col\"></td><td></td><td></td><td></td><td></td><td></td></tr>
                for(int n = 0; n < dataTableItems.Count; n++)
                {
                    sb.Append("<tr>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["symbol"] + "</td>");
                    sb.Append("<td class=\"sub-items-col\">" + dataTableItems[n].ColumnValues["strategies"] + "</td>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["positions"] + "</td>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["orders"] + "</td>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["actions"] + "</td>");
                    sb.Append("<td data-sort-value=\"" + dataTableItems[n].ColumnValues["realized"] + "\">" + UC.DecimalToUSD((decimal)dataTableItems[n].ColumnValues["realized"], 4) + "</td>");
                    sb.Append("<td data-sort-value=\"" + dataTableItems[n].ColumnValues["unrealized"] + "\">" + UC.DecimalToUSD((decimal)dataTableItems[n].ColumnValues["unrealized"], 4) + "</td>");
                    sb.Append("</tr>");
                }

                symbolsDataTable.TBodyHtml = sb.ToString();

            }

            result.DataTables.Add(symbolsDataTable);

            /* ###################### STRATEGIES ###################### */

            AlgoTraderDataTable strategiesDataTable = new AlgoTraderDataTable();

            strategiesDataTable.Name = "overview-strategies";

            if (String.IsNullOrEmpty(SelectedStrategyId))
            {
                strategiesDataTable.HideColumn = (!String.IsNullOrEmpty(SelectedSymbol) ? 1 : -1);
                strategiesDataTable.Show = true;

                string[] usableStrategiesKeys = Global.State.UsableStrategies.Keys.ToArray();

                strategiesDataTable.Title = "Strategies (" + usableStrategiesKeys.Length.ToString() + ")";

                StringBuilder sb = new StringBuilder();

                // name, strategies, positions, orders, actions, realized, unrealized

                List<DataTableItem> dataTableItems = new List<DataTableItem>();
                if(AlgoTraderShared.WatchList.Count > 0)
                {

                    for (int n = 0; n < usableStrategiesKeys.Length; n++)
                    {
                    
                        string strategy = Global.State.UsableStrategies[usableStrategiesKeys[n]];

                        DataTableItem dataTableItem = new DataTableItem();

                        dataTableItem.ColumnValues.Add("strategy", strategy);

                        // probably super inefficient
                        dataTableItem.ColumnValues.Add("symbols", AlgoTraderShared.WatchList.Count(w => w.Strategies.Exists(s => s.GetType().Name == strategy)));




                        //dataTableItem.Name = wi.Symbol;
                        //dataTableItem.SubItems = wi.Strategies.Count;

                        dataTableItem.ColumnValues.Add("orders", AlgoTraderShared.Orders.FindAll(o => o.StrategyName == strategy && (String.IsNullOrEmpty(SelectedSymbol) || o.Symbol == SelectedSymbol)).Count);
                        dataTableItem.ColumnValues.Add("actions", AlgoTraderShared.StrategyActions.FindAll(a => a.StrategyName == strategy && (String.IsNullOrEmpty(SelectedSymbol) || a.Symbol == SelectedSymbol)).Count);

                        List<Position> positions = AlgoTraderShared.Positions.FindAll(p => p.StrategyName == strategy && (String.IsNullOrEmpty(SelectedSymbol) || p.Symbol == SelectedSymbol));

                        dataTableItem.ColumnValues.Add("positions", positions.Count);

                        // do realized
                        List<Position> closedPositions = positions.FindAll(p => p.Status == PositionStatus.Closed);
                        decimal netRealized = 0M;
                        for (int m = 0; m < closedPositions.Count; m++)
                        {
                            netRealized += closedPositions[m].SoldAt - closedPositions[m].BoughtAt;
                        }
                        dataTableItem.ColumnValues.Add("realized", netRealized);

                        // do unrealized
                        List<Position> openPositions = positions.FindAll(p => p.Status == PositionStatus.Open);

                        decimal netUnrealized = 0M;


                        for (int m = 0; m < openPositions.Count; m++)
                        {
                            Position p = openPositions[m];
                            decimal sellPrice = 0M;
                            decimal buyPrice = 0M;

                            List<Node> nodes = AlgoTraderShared.NodesData[p.Symbol].Nodes;

                            if (p.Side == PositionSide.Long)
                            {
                                // if long, find the sell price by getting the latest bid price
                                sellPrice = nodes[nodes.Count - 1].BidPrice;
                                buyPrice = p.BoughtAt;
                            }
                            else if (p.Side == PositionSide.Short)
                            {
                                // if short, find the buy price by getting the latest ask price
                                buyPrice = nodes[nodes.Count - 1].AskPrice;
                                sellPrice = p.SoldAt;
                            }
                            else
                            {
                                throw new Exception("a what?");
                            }
                            netUnrealized += sellPrice - buyPrice;
                        }
                        dataTableItem.ColumnValues.Add("unrealized", netUnrealized);

                        dataTableItems.Add(dataTableItem);

                    }
                }
                TrySortDataTable(ref dataTableItems, "overview-strategies", "strategy");

                for (int n = 0; n < dataTableItems.Count; n++)
                {
                    sb.Append("<tr>");
                    sb.Append("<td style=\"text-align:left;\">" + dataTableItems[n].ColumnValues["strategy"] + "</td>");
                    sb.Append("<td class=\"sub-items-col\">" + dataTableItems[n].ColumnValues["symbols"] + "</td>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["positions"] + "</td>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["orders"] + "</td>");
                    sb.Append("<td>" + dataTableItems[n].ColumnValues["actions"] + "</td>");
                    sb.Append("<td data-sort-value=\"" + dataTableItems[n].ColumnValues["realized"] + "\">" + UC.DecimalToUSD((decimal)dataTableItems[n].ColumnValues["realized"], 4) + "</td>");
                    sb.Append("<td data-sort-value=\"" + dataTableItems[n].ColumnValues["unrealized"] + "\">" + UC.DecimalToUSD((decimal)dataTableItems[n].ColumnValues["unrealized"], 4) + "</td>");
                    sb.Append("</tr>");
                }

                strategiesDataTable.TBodyHtml = sb.ToString();

            }

            result.DataTables.Add(strategiesDataTable);

            return result;
        }

        private static void TrySortDataTable(ref List<DataTableItem> dataTableItems, string dataTableName, string defaultSortColumn)
        {
            string column = defaultSortColumn;
            string dir = "asc";

            if (DataTableSorts.ContainsKey(dataTableName))
            {
                DataTableSort dts = DataTableSorts[dataTableName];
                column = dts.Column;
                dir = dts.Direction;
            }

            // name, strategies, positions, orders, actions, realized, unrealized

            dataTableItems = dataTableItems.OrderBy(dti => dti.ColumnValues[column]).ToList();

            if(dir == "desc")
            {
                dataTableItems.Reverse();
            }

        }
    }
}
