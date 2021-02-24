using System;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml.Serialization;

namespace AT
{
    public class Methods
    {

        public static string GetIntervalFileString(Interval interval)
        {
            string intervalString = "";

            if (interval == Interval.HalfSecond)
            {
                intervalString = "hfsec";
            }
            else if (interval == Interval.OneMinute)
            {
                intervalString = "01min";
            }
            else if (interval == Interval.OneSecond)
            {
                intervalString = "01sec";
            }
            else if (interval == Interval.FiveMinutes)
            {
                intervalString = "05min";
            }
            else if (interval == Interval.FifteenMinutes)
            {
                intervalString = "15min";
            }
            else if (interval == Interval.HalfHour)
            {
                intervalString = "30min";
            }
            else if (interval == Interval.OneHour)
            {
                intervalString = "60min";
            }
            else
            {
                throw new Exception("What are you doing?");
            }

            return intervalString;
        }

        public static void ThreadRun(string typeName, string methodName, ThreadControl tc, Object instance = null, object[] parameters = null)
        {
            List<object> p = new List<object>();

            if (parameters != null)
            {
                p.AddRange(parameters);
            }

            p.Add(tc);

            tc.BeforeExecute();
            //try
            //{
                Type.GetType(typeName).GetMethod(methodName).Invoke(instance, p.ToArray());
            //}
            //catch (Exception e)
            //{
            //    Console.WriteLine("EXCEPTION EXCEPTION EXCEPTION EXCEPTION EXCEPTION EXCEPTION");
             //   Console.WriteLine(e.Message);
            //}
            
            tc.AfterExecute();
        }
    }
}
