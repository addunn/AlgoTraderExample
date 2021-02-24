using AT.AlgoTraderEnums;
using Jil;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT.UIObjects
{
    [Serializable]
    public class AlgoTraderMain
    {
        [JilDirective(Name = "currentTime")]
        public string CurrentTime = "";

        [JilDirective(Name = "selectedSymbol")]
        public string SelectedSymbol = "";

        [JilDirective(Name = "selectedStrategyName")]
        public string SelectedStrategyName = "";

        [JilDirective(Name = "selectedStrategyId")]
        public string SelectedStrategyId = "";

        [JilDirective(Name = "currentSpeed")]
        public AlgoTraderSimulatingSpeed CurrentSpeed = AlgoTraderSimulatingSpeed.Unknown;
    }


    [Serializable]
    public class AlgoTraderLog
    {
        [JilDirective(Name = "log")]
        public string Log = "";
    }



    [Serializable]
    public class AlgoTraderOverview
    {
        [JilDirective(Name = "dataTables")]
        public List<AlgoTraderDataTable> DataTables = new List<AlgoTraderDataTable>();
    }


    [Serializable]
    public class AlgoTraderChart
    {
        /*
            xlabels: [{1,"1am"}, {5, "2am"}]


            each sub chart:
	            - x, y, width, height
	            - chart label
	            - ylabels
	            - plot these dots with this color
	            - plot these lines with this color
        */
        [JilDirective(Name = "currentTradePrice")]
        public string CurrentTradePrice = "";

        [JilDirective(Name = "currentAskPrice")]
        public string CurrentAskPrice = "";

        [JilDirective(Name = "currentBidPrice")]
        public string CurrentBidPrice = "";

        [JilDirective(Name = "data")]
        public List<List<AlgoTraderChartPoint>> Data = new List<List<AlgoTraderChartPoint>>();
    }

    [Serializable]
    public class AlgoTraderChartPoint
    {
        [JilDirective(Name = "y")]
        public decimal Y = 0M;

        [JilDirective(Name = "r")]
        public double R = 0D;

        [JilDirective(Name = "g")]
        public double G = 0D;

        [JilDirective(Name = "b")]
        public double B = 0D;

        [JilDirective(Name = "a")]
        public double A = 0D;
    }



        [Serializable]
    public class AlgoTraderDataTable
    {
        [JilDirective(Name = "name")]
        public string Name = "";

        [JilDirective(Name = "title")]
        public string Title = "";

        [JilDirective(Name = "show")]
        public bool Show = false;

        [JilDirective(Name = "hideColumn")]
        public int HideColumn = -1;

        [JilDirective(Name = "tBodyHtml")]
        public string TBodyHtml = "";


    }



    public class DataTableItem
    {

        public Dictionary<string, object> ColumnValues = new Dictionary<string, object>();

        /*
        public string Name = "";
        public int SubItems = 0;
        public int Positions = 0;
        public int Orders = 0;
        public int Actions = 0;

        public decimal Realized = 0;
        public decimal Unrealized = 0;
        */
    }

}
