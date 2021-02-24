using System;
using System.Collections.Generic;
using AT.DataObjects;

namespace AT.AlgoTraderObjects
{

    [Serializable]
    public abstract class Strategy
    {
        public string Symbol = "";

        // can only have one open or closed position at a time.
        // the list is for keeping track of previous positions,
        // the position on the top of the list is where you would find an open/pending position
        //public List<Position> Positions = new List<Position>();

        // Order is referenced in other lists as well
        //public List<Order> Orders = new List<Order>();

        /// <summary>
        /// used only when the strategy is paired with a symbol... e.g., WatchItem.
        /// probability of how much this strategy worked for the symbol.
        /// </summary>
        public decimal Probability = 0;

        /// <summary>
        /// Mostly used only when the strategy is paired with a symbol... e.g., WatchItem.
        /// </summary>
        public string Id = "";

        // this strategy might depend on other symbols
        // if it does, compute actions can read other symbols in AlgoTraderState.SymbolDatas
        public abstract StrategyOrderActions ComputeActions();

        public string Description = "";

        public Strategy(string symbol, decimal probability, string id)
        {
            Probability = probability;
            Symbol = symbol;
            Id = id;
        }
    }
}
