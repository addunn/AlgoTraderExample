using Jil;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace AT.Tools
{
    public class SerializerMethods
    {
        
        public static string SerializeApplicationStats()
        {
            using (var output = new StringWriter())
            {
                JSON.Serialize(Global.State.ApplicationStats, output, Options.PrettyPrint);
                return output.ToString();
            }
        }

        public static string SerializeThreadControlTree()
        {
            using (var output = new StringWriter())
            {
                
                JSON.Serialize(Global.State.ThreadControlTree, output, Options.PrettyPrint);
                
                return output.ToString();
            }
        }
        

        public static string SerializeThreadControlState(ThreadControl tc)
        {
            string result = "{";
            result += "\"id\":" + tc.Id + ",";
            result += "\"state\":\"" + (tc.State.ToString()) + "\"";
            result += "}";
            return result;
        }


        public static string DictionarySerializedValuesToJSON(Dictionary<string, string> d)
        { 
            StringBuilder sb = new StringBuilder();

            sb.Append("{");

            string[] ks = d.Keys.ToArray();

            for(int n = 0; n < ks.Length; n++)
            {
                sb.Append("\"" + ks[n] + "\":" + d[ks[n]] + (n == ks.Length - 1 ? "" : ","));
            }

            sb.Append("}");

            return d.Count == 0 ? "" : sb.ToString();
        }
    }
}
