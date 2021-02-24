using System;
using System.Collections.Generic;
using AT.AlgoTraderObjects;
using AT.DataObjects;

namespace AT.AlgoTraderStrategies
{
    [Serializable]
    public class SkinflintStrategy : Strategy
    {

        public SkinflintStrategy(string symbol, decimal probability, string id): base(symbol, probability, id)
        {
            Description = "Never trades.";
        }

        public override StrategyOrderActions ComputeActions()
        {
            return null;
        }
    }
}
