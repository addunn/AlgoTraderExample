using System;
using System.Collections.Generic;
using System.Text;

namespace AT.Tools.LogObjects
{
    public class Log
    {


        public Dictionary<Verbosity, long> changes = new Dictionary<Verbosity, long>();

        private Dictionary<Verbosity, long> readLastChanges = new Dictionary<Verbosity, long>();

        private Dictionary<Verbosity, StringBuilder> sb = new Dictionary<Verbosity, StringBuilder>();

        private readonly Object obj = new Object();

        // private bool firstAdd = true;

        // private DateTime startDate;

        private bool useTimestamp = false;

        public void AddLine(string val, Verbosity verbosity = Verbosity.Normal)
        {
            lock (obj)
            {

                int verbInt = (int)verbosity;

                if (useTimestamp && val.Length > 0)
                {
                    val = "[" + String.Format("{0:T}", DateTime.Now) + "] " + val;
                }

                for (int n = verbInt; n < 4; n++)
                {
                    Verbosity v = (Verbosity)Enum.ToObject(typeof(Verbosity), n);

                    sb[v].Append(val + "\r\n");

                    if (changes[v] > 1000)
                    {
                        // chop 20% of the length
                        string s = sb[v].ToString();
                        sb[v] = new StringBuilder();

                        if(s.Length > 35001)
                        {
                            sb[v].Append(s.Substring(s.Length - 35000));
                        }
                        else
                        {
                            sb[v].Append(s);
                        }
                        
                        changes[v] = 0;
                    }

                    changes[v]++;
                }
            }

        }
        
        public string Read(Verbosity verbosity = Verbosity.Normal)
        {
            string value = "";
            lock (obj)
            {
                value = sb[verbosity].ToString();
                readLastChanges[verbosity] = changes[verbosity];
            }
            return value;
        }
        public bool ChangedSinceLastRead(Verbosity verbosity = Verbosity.Normal)
        {
            return readLastChanges[verbosity] != changes[verbosity];
        }
        public Log(bool includeTimestamp = true)
        {
            useTimestamp = includeTimestamp;

            sb.Add(Verbosity.Minimal, new StringBuilder());
            sb.Add(Verbosity.Normal, new StringBuilder());
            sb.Add(Verbosity.Verbose, new StringBuilder());

            changes.Add(Verbosity.Minimal, 0);
            changes.Add(Verbosity.Normal, 0);
            changes.Add(Verbosity.Verbose, 0);

            readLastChanges.Add(Verbosity.Minimal, 0);
            readLastChanges.Add(Verbosity.Normal, 0);
            readLastChanges.Add(Verbosity.Verbose, 0);
        }
    }
}
