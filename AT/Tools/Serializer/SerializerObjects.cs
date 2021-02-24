
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Web;

/*
namespace AT
{
    public class Serializer
    {


        public static string JSONSerializeStockNode(StockNode stockNode)
        {



            StringBuilder sb = new StringBuilder();
            sb.Append("{");
                sb.Append("\"symbol\":\"" + stockNode.Symbol + "\",");
                sb.Append("\"log\":\"" + HttpUtility.JavaScriptStringEncode(stockNode.Log.ToString()) + "\",");
                sb.Append("\"stockData\":{");
                    foreach (KeyValuePair<string, StockData> kv in stockNode.StockData)
                    {
                        sb.Append("\"" + kv.Key + "\":{");
                            sb.Append("\"data\":[");
                            for(int n = 0; n < kv.Value.Data.Count; n++)
                            {
                                sb.Append("{");
                                sb.Append("\"o\":" + kv.Value.Data[n].Open + ",");
                                sb.Append("\"h\":" + kv.Value.Data[n].High + ",");
                                sb.Append("\"l\":" + kv.Value.Data[n].Low + ",");
                                sb.Append("\"v\":" + kv.Value.Data[n].Volume);
                                sb.Append("},");
                            }
                            sb.Remove(sb.Length - 1, 1);
                        sb.Append("]");
                        sb.Append("},");
                    }
                    // remove the last comma
                    sb.Remove(sb.Length - 1, 1);
                sb.Append("}");
            sb.Append("}");
            return sb.ToString();
        }
        public static string JSONSerializePricesTimeZoned(PricesByIntervalData pricesTimeZoned)
        {
            StringBuilder sb = new StringBuilder();

            sb.Append("{");

            sb.Append("\"min\":" + pricesTimeZoned.yMin + ",");
            sb.Append("\"max\":" + pricesTimeZoned.yMax + ",");

            sb.Append("\"data\":[");

            for(int n = 0; n < pricesTimeZoned.PricesTimeZoned.Count; n++)
            {
                PriceTimeZoned p = pricesTimeZoned.PricesTimeZoned[n];
                sb.Append("{");
                sb.Append("\"sd\":\"" + p.StartDateTime.ToString("g") + "\",");
                sb.Append("\"ed\":\"" + p.EndDateTime.ToString("g") + "\",");



                if (!p.HasNoData)
                {
                    sb.Append("\"open\":" + p.Open + ",");
                    sb.Append("\"close\":" + p.Close + ",");
                    sb.Append("\"high\":" + p.High + ",");
                    sb.Append("\"low\":" + p.Low + ",");
                    sb.Append("\"volume\":" + p.Volume);
                }
                else
                {
                    sb.Append("\"noData\":true");
                }

                sb.Append("}");

                if(n < pricesTimeZoned.PricesTimeZoned.Count - 1)
                {
                    sb.Append(",");
                }
            }

            sb.Append("]}");
            
            return sb.ToString();
        }

    }
}
*/