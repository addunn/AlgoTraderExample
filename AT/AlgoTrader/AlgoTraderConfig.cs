using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT
{
    public class AlgoTraderConfig
    {
        // static members to config things for algotrader
        public static int IncludePastDataDays = 20;


        //public static int PrepareDayAtHour = 9;
        //public static int PrepareDayAtMinute = 0;


        public static int TicksStreamManagerAtHour = 3;
        public static int TicksStreamManagerAtMinute = 55;

        public static decimal SlippageFactorPerShare = 0.00001M;

        public static decimal AccountRisk = 0.02M;
    }
}
