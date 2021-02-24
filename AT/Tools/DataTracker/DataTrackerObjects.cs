using System;
using System.Collections.Generic;
using System.Text;

namespace AT.Tools.DataTrackerObjects
{
    public class DataTracker
    {
        public Dictionary<string, List<FD>> DataDaysBySymbol;
        public List<FD> DataDays;
        public List<FD> Holidays;
        public List<FD> NoDataDays;

        public bool SymbolHasDayData(string symbol, FD fd)
        {
            return DataDaysBySymbol[symbol].BinarySearch(fd) >= 0;
        }
        public bool DayHasData(FD fd)
        {
            return DataDays.BinarySearch(fd) >= 0;
        }

        public bool IsSymbolCacheableForDay(string symbol, FD fd)
        {
            // this could be a problem if API has an error and it looks like there's no data for that day
            // should periodically refresh nodatadays and if there are nodatadays that turn out to be data days, delete those day cache files
            return NoDataDays.BinarySearch(fd) >= 0 || DataDaysBySymbol[symbol].BinarySearch(fd) >= 0;
        }
    }
}
