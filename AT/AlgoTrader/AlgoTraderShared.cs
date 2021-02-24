using AT.AlgoTraderEnums;
using AT.AlgoTraderObjects;
using AT.AlgoTraderSubProcessors;
using AT.DataObjects;
using NodaTime;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT
{
    public class AlgoTraderShared
    {
        public static List<Position> Positions = new List<Position>();

        public static List<Order> Orders = new List<Order>();

        public static List<WatchItem> WatchList = new List<WatchItem>();

        public static List<StrategyOrderActions> StrategyActions = new List<StrategyOrderActions>();

        // public static List<StrategyOrderActions> CuratedStrategyActions = new List<StrategyOrderActions>();

        public static Dictionary<string, NodesData> NodesData = new Dictionary<string, NodesData>();
        // public static Dictionary<string, SequencesData> SequencesData = new Dictionary<string, SequencesData>();

        public static Dictionary<string, NodesData> WatchListNodesData = new Dictionary<string, NodesData>();

        public static Dictionary<string, NodesData> SimDayNodes = new Dictionary<string, NodesData>();
    }
}
