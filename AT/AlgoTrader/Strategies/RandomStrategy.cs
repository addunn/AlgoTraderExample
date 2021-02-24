using System;
using System.Collections.Generic;
using AT.AlgoTraderObjects;
using AT.AlgoTraderEnums;
using AT;
using AT.DataObjects;

namespace AT.AlgoTraderStrategies
{
    [Serializable]
    public class RandomStrategy : Strategy
    {

        public RandomStrategy(string symbol, decimal probability, string id): base(symbol, probability, id)
        {
            Description = "It's a random strategy.";
        }

        public override StrategyOrderActions ComputeActions()
        {


            Position openPosition = AlgoTraderShared.Positions.Find(p => p.StrategyId == Id && p.Symbol == Symbol && p.Status == PositionStatus.Open);

            Node node = AlgoTraderShared.NodesData[Symbol].Nodes[AlgoTraderShared.NodesData[Symbol].Nodes.Count - 1];

            decimal currentSpread = node.AskPrice - node.BidPrice;

            decimal currentPrice = node.TradePrice;

            if (openPosition != null)
            {

                

                int n = AT.UC.GetRandomInteger(1, 5);
                

                // sell if the price is more than 0.04 cents than what you bought it at, or more than some mins has passed
                if(n == 3 && (currentPrice > openPosition.BoughtAt + 0.05M || (AlgoTraderState.CurrentTime.GetDateTimeNano() - openPosition.OpenedAt > 2400000000000L)))
                {
                    StrategyOrderActions sa = new StrategyOrderActions();

                    sa.PositionId = openPosition.Id;
                    sa.PositionSide = openPosition.Side;
                    sa.StrategyName = this.GetType().Name;
                    sa.StrategyId = Id;
                    sa.Symbol = Symbol;
                    sa.OrderActions = new List<OrderAction>();

                    Order orderToCancel = AlgoTraderShared.Orders.Find(o => o.StrategyId == Id && o.Symbol == Symbol && o.IsCancelable());

                    if (orderToCancel != null)
                    {
                        OrderAction cancelOA = new OrderAction();
                        cancelOA.Type = AlgoTraderEnums.OrderActionType.Cancel;
                        cancelOA.OrderId = orderToCancel.Id;

                        OrderAction marketSell = new OrderAction();
                        marketSell.Type = OrderActionType.Place;
                        marketSell.OrderType = OrderType.Market;
                        marketSell.OrderSide = OrderSide.Sell;
                        marketSell.Quantity = 1;

                        sa.OrderActions.Add(cancelOA);
                        sa.OrderActions.Add(marketSell);
                    }

                    return sa;
                }
            }
            else
            {


                int n = AT.UC.GetRandomInteger(1, 60);

                if(n == 3 && currentSpread < 0.02M)
                {


                    StrategyOrderActions sa = new StrategyOrderActions();


                    sa.PositionSide = PositionSide.Long;
                    sa.StrategyName = this.GetType().Name;
                    sa.OrderActions = new List<OrderAction>();
                    sa.StrategyId = Id;
                    sa.Symbol = Symbol;



                    OrderAction marketBuy = new OrderAction();
                    marketBuy.Type = OrderActionType.Place;
                    marketBuy.OrderType = OrderType.Market;
                    marketBuy.OrderSide = OrderSide.Buy;
                    marketBuy.Quantity = 1;

                    OrderAction stopLossOrder = new OrderAction();
                    stopLossOrder.Type = OrderActionType.Place;
                    stopLossOrder.OrderType = OrderType.Stop;
                    stopLossOrder.OrderSide = OrderSide.Sell;
                   
                    // stopprice is current price minus a little
                    stopLossOrder.StopPrice = currentPrice - 0.15M;
                    stopLossOrder.Quantity = 1;

                    sa.OrderActions.Add(marketBuy);
                    sa.OrderActions.Add(stopLossOrder);


                    return sa;
                }
            }
            

            
            return null;
        }
    }
}