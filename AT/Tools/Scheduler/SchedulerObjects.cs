using Jil;
using System;
using System.Collections.Generic;
using System.Linq;

using NodaTime;
using System.Threading;
using AT.Tools.SchedulerEnums;
using AT.Tools.LogObjects;
using System.Threading.Tasks;

namespace AT.Tools.SchedulerObjects
{
    

    public class SchedulerItemTime
    {
        public int H = 0;
        public int M = 0;
        public int S = 0;

        public SchedulerItemTime(string time)
        {

            string[] s = time.Split(':');

            H = int.Parse(s[0]);
            M = int.Parse(s[1]);
            S = int.Parse(s[2]);
        }
    }

    public class SchedulerItem
    {


        //public SchedulerItemType Type = SchedulerItemType.Unknown;
        //public SchedulerItemEvery Every = SchedulerItemEvery.Unknown;
        public SchedulerItemTime At = null;
        //public SchedulerItemTime Until = null;
        public SchedulerItemTime EndAt = null;
        public int Estimate = 0;

        public string ObjectId = null;

        public string Call = "";
        public string Class = "";
        public string Assembly = "";

        public string Id = "";

        public ThreadControl TC = null;

        // if this is required... every time the app starts past this scheduleritem, and if it's not marked in DB, it runs it
        //public bool Required = false;

        // SchedulerItem Ids that must be ran before running this, regardless if it's marked as complete in the DB
        //public List<string> MustComplete = new List<string>();

        // value from the database
        public bool MarkedComplete = false;

        // this gets set after the item has been run... it's not persistant... mustComplete checks this value, not MarkedComplete
        // public bool Complete = false;

    }
    
    public class Scheduler
    {
        private List<SchedulerItem> SchedulerItems;

        private ThreadControl PTC = null;

        private string KeyPrefix = "";

        private string ScheduleJSON = "";

        private List<int> PurposedlyStoppedIds = new List<int>();

        public Scheduler(string json, string keyPrefix)
        {
            ScheduleJSON = UC.RemoveJSLineComments(json);
            KeyPrefix = keyPrefix;
        }

        private string GetKey()
        {
            ZonedDateTime dt = UCDT.GetCurrentEastCoastTime();
            return KeyPrefix + "_" + dt.Year + "-" + dt.Month + "-" + dt.Day;
        }

        /// <summary>
        /// Returned value is [0] = Assembly name, [1] = Fully named Class with namespace, [2] = Method name
        /// </summary>
        /// <param name="call"></param>
        /// <returns></returns>
        private string[] ParseCall(string call)
        {

            string[] result = new string[3];

            // call: "SU.App.DeleteStreamedDBs"

            // result: [0] = Assembly name, [1] = Fully named Class with namespace, [2] = Method name

            string[] s1 = call.Split('.');

            for (int n = 0; n < s1.Length; n++)
            {
                // if first slot
                if(n == 0)
                {
                    result[0] = s1[n]; // assign assembly name
                    result[1] = s1[n]; // start with namespace for fully name class
                }  // if last slot
                else if (n == s1.Length - 1)
                {
                    result[2] = s1[n]; // method name
                }// if not first or last
                else
                {
                    result[1] += "." + s1[n]; // append to class name
                }
            }

            return result;
        }

        

        /// <summary>
        /// Called only once during the constructor
        /// </summary>
        /// <param name="jsonPayload"></param>
        private void BuildSchedule(string jsonPayload)
        {
            PTC.Log.AddLine("Building schedule", Verbosity.Normal);

            var o = JSON.DeserializeDynamic(jsonPayload);

            SchedulerItems = new List<SchedulerItem>();

            for (int n = 0; n < o.Count; n++)
            {
                SchedulerItem si = new SchedulerItem();
                //si.Type = (SchedulerItemType)Enum.ToObject(typeof(SchedulerItemType), (int)o[n]["type"]);
                si.At = o[n].ContainsKey("at") ? new SchedulerItemTime((string)o[n]["at"]) : null;


                string[] parsedCalls = ParseCall((string)o[n]["call"]);

                si.Assembly = parsedCalls[0];
                si.Class += parsedCalls[1];
                si.Call = parsedCalls[2];


                //if (o[n].ContainsKey("afterEndItCall"))
                //{
                //    string[] parsedafterEndItCalls = ParseCall((string)o[n]["afterEndItCall"]);
                //    si.AfterEndItAssembly = parsedafterEndItCalls[0];
                //    si.AfterEndItClass += parsedafterEndItCalls[1];
                //    si.AfterEndItCall = parsedafterEndItCalls[2];
                //}

                si.Id = (string)o[n]["id"];
                si.EndAt = o[n].ContainsKey("endAt") ? new SchedulerItemTime((string)o[n]["endAt"]) : null;
                si.ObjectId = o[n].ContainsKey("objectId") ? (string)o[n]["objectId"] : null;
                si.Estimate = o[n].ContainsKey("estimate") ? (int)o[n]["estimate"] : -1;
                //si.Every = o[n].ContainsKey("every") ? (SchedulerItemEvery)Enum.ToObject(typeof(SchedulerItemEvery), (int)o[n]["every"]) : SchedulerItemEvery.Unknown;
                //si.Until = o[n].ContainsKey("until") ? new SchedulerItemTime((string)o[n]["until"]) : null;
                //si.Required = o[n].ContainsKey("required") ? (bool)o[n]["required"] : false;
                //si.MustComplete = o[n].ContainsKey("mustComplete") ? ((string)o[n]["mustComplete"]).Split(',').ToList() : null;
                si.MarkedComplete = false;

                SchedulerItems.Add(si);

                PTC.Log.AddLine("SchedulerItem item added. id: " + si.Id, Verbosity.Verbose);
            }
        }

        private void NewDay()
        {
            List<string> idsMarkedCompleted = DBMethods.GetSchedulerItems(GetKey());
            PurposedlyStoppedIds.Clear();
            for (int n = 0; n < SchedulerItems.Count; n++)
            {
                SchedulerItems[n].MarkedComplete = idsMarkedCompleted.Contains(SchedulerItems[n].Id);
                SchedulerItems[n].TC = null;
            }
            
        }

        public bool ItemIsWithinCurrentTime(ZonedDateTime zdt, SchedulerItem si)
        {
            bool result = false;

            int currentMs = (zdt.Hour * 3600000) + (zdt.Minute * 60000) + (zdt.Second * 1000);
            int startMs = (si.At.H * 3600000) + (si.At.M * 60000) + (si.At.S * 1000);

            int estimate = 0;

            if(si.EndAt == null)
            {
                estimate = si.Estimate;
            }
            else
            {
                estimate = ((si.EndAt.H * 3600000) + (si.EndAt.M * 60000) + (si.EndAt.S * 1000)) - startMs;
            }

            int endMs = startMs + estimate;

            return currentMs >= startMs && currentMs <= endMs;
        }


        public bool CurrentTimeAfterOrEqualSchedulerItemTime(ZonedDateTime zdt, SchedulerItemTime st)
        {
            int n1 = (zdt.Hour * 3600) + (zdt.Minute * 60) + (zdt.Second);
            int n2 = (st.H * 3600) + (st.M * 60) + (st.S);
            return n1 >= n2;
        }


        public void Run(ThreadControl tc)
        {
            PTC = tc;

            BuildSchedule(ScheduleJSON);

            NewDay();

            ZonedDateTime dt = UCDT.GetCurrentEastCoastTime();

            int currentSecond = -1;
            int lastSecond = -1;
            int currentDay = dt.Day;
            int lastDay = dt.Day;

            while (PTC.CheckNotStopped())
            {
                dt = UCDT.GetCurrentEastCoastTime();

                if(dt.Day != lastDay)
                {
                    // reset for new day
                    NewDay();
                }

                lastDay = dt.Day;

                string currentKey = GetKey();

                currentSecond = dt.Second;

                if (currentSecond != lastSecond)
                {
                    lastSecond = currentSecond;

                    // check any items that have finished and mark them complete
                    
                    List<SchedulerItem> CompletedNotMarkedItems = SchedulerItems.FindAll(i => !i.MarkedComplete && i.TC != null && i.TC.State == ThreadControlState.Complete);
                    for(int n = 0; n < CompletedNotMarkedItems.Count; n++)
                    {
                        PTC.Log.AddLine("Marking " + CompletedNotMarkedItems[n].Id + " as completed");
                        CompletedNotMarkedItems[n].MarkedComplete = true;
                        DBMethods.MarkSchedulerItem(currentKey, CompletedNotMarkedItems[n].Id);
                    }
                    
                    // stop any that are running and it's after their "endAt" date
                    
                    List<SchedulerItem> ItemsNeedStopped = SchedulerItems.FindAll(i => !i.MarkedComplete && i.TC != null && i.TC.CanBeStopped() && i.EndAt != null && CurrentTimeAfterOrEqualSchedulerItemTime(dt, i.EndAt));
                    for (int n = 0; n < ItemsNeedStopped.Count; n++)
                    {
                        PTC.Log.AddLine("Stopping " + ItemsNeedStopped[n].Id + " because on or after EndAt. Will wait for stop before marking completed.");

                        PurposedlyStoppedIds.Add(ItemsNeedStopped[n].TC.Id);
                        ItemsNeedStopped[n].TC.AddSignalForChild(Signal.Stop);
                    }
                    
                    // find all recently stopped items BY SCHEDULER
                    
                    List<SchedulerItem> ItemsThatHaveStopped = SchedulerItems.FindAll(i => !i.MarkedComplete && i.TC != null && i.TC.State == ThreadControlState.Done && PurposedlyStoppedIds.Exists(p => p == i.TC.Id));
                    for (int n = 0; n < ItemsThatHaveStopped.Count; n++)
                    {
                        PTC.Log.AddLine("Found a Done. Marking it as complete.");
                        PurposedlyStoppedIds.Remove(ItemsThatHaveStopped[n].TC.Id);
                        ItemsThatHaveStopped[n].MarkedComplete = true;
                        DBMethods.MarkSchedulerItem(currentKey, ItemsThatHaveStopped[n].Id);
                    }
                    
                    // start any that are within the current time and not running
                    List<SchedulerItem> ItemsThatNeedStarted = SchedulerItems.FindAll(i => !i.MarkedComplete && i.TC == null && ItemIsWithinCurrentTime(dt, i));
                    for (int n = 0; n < ItemsThatNeedStarted.Count; n++)
                    {
                        PTC.Log.AddLine("Starting " + ItemsThatNeedStarted[n].Id + " because current time is between [At, EndAt]");

                        string typeName = ItemsThatNeedStarted[n].Class + ", " + ItemsThatNeedStarted[n].Assembly;

                        string methodName = ItemsThatNeedStarted[n].Call;

                        ThreadControl childTc = new ThreadControl(ItemsThatNeedStarted[n].Id, ItemsThatNeedStarted[n].ObjectId);
                        PTC.Children.Add(childTc);
                        ItemsThatNeedStarted[n].TC = childTc;
                        Task.Factory.StartNew(() => Methods.ThreadRun(typeName, methodName, childTc, null, null), CancellationToken.None, TaskCreationOptions.LongRunning, TaskScheduler.Default);

                        
                    }

                }
                else
                {
                    Thread.Sleep(250);
                }
            }
        }

        /*
        private List<int> GetItemsThatNeedToRunFrom(int itemIndex)
        {
            List<int> result = new List<int>();

            List<string> mustComplete = SchedulerItems[itemIndex].MustComplete;

            for (int n = 0; n < itemIndex; n++)
            {
                SchedulerItem item = SchedulerItems[n];
                if ((item.Required && !item.MarkedComplete) || (!item.Complete && mustComplete != null && mustComplete.Contains(item.Id)))
                {
                    result.Add(n);
                }
            }
            return result;
        }
        */

            /*
        private int GetItemIndex(ZonedDateTime zdt)
        {
            int result = -1;

            DateTime target = new DateTime(zdt.Year, zdt.Month, zdt.Day, zdt.Hour, zdt.Minute, zdt.Second, DateTimeKind.Local);

            DateTime blockStart = new DateTime(1990, 1, 1);
            DateTime blockEnd = new DateTime(1990, 1, 1);

            for (int n = 0; n < SchedulerItems.Count; n++)
            {
                SchedulerItem item = SchedulerItems[n];

                if (item.At != null)
                {
                    blockStart = new DateTime(target.Year, target.Month, target.Day, item.At.H, item.At.M, item.At.S, DateTimeKind.Unspecified);
                    blockEnd = new DateTime(target.Year, target.Month, target.Day, item.At.H, item.At.M, item.At.S, DateTimeKind.Unspecified);
                }

                // add the estimate to the blockend
                blockEnd = blockEnd.AddMilliseconds(item.Estimate);

                // check if current time falls between blockstart and blockend
                if (target >= blockStart && target < blockEnd)
                {
                    result = n;
                    break;
                }
            }

            return result;
        }
        */
        /*
        private void TryMarkingItemAsComplete(SchedulerItem item, string key)
        {
            // don't mark a item complete if it was forced to end
            if (TS.IsNotEndIt())
            {
                Log.AddLine("Marking " + item.Id + " as complete", Verbosity.Verbose);
                item.Complete = true;
                item.MarkedComplete = true;
                DBMethods.MarkSchedulerItem(key, item.Id);
            }
            
        }
        */

        /*
        private void ContinouslyExecuteMethod(SchedulerItem item, TaskSignals ts)
        {

            // continuous calls cannot start on the minute... the code won't work

            ZonedDateTime dt = UCDT.GetCurrentEastCoastTime();

            // since we are running every minute and we start at a second, we make that the target
            int targetSecond = item.At.S;

            int currentSecond = dt.Second;

            int lastSecond = dt.Second;

            int currentMinute = dt.Minute;
            int lastMinute = dt.Minute;

            int callCount = 0;

            // call method immediatly because it should be called on the At (also pass in the current call count)
            Type.GetType(item.Class).GetMethod(item.Call).Invoke(null, new object[] { callCount });

            // increment call count
            callCount++;

            do
            {
                dt = UCDT.GetCurrentEastCoastTime();

                currentMinute = dt.Minute;

                if (currentMinute != lastMinute)
                {
                    Log.AddLine("Continuous task new minute hit: " + currentMinute, Verbosity.Verbose);

                    lastMinute = currentMinute;

                    currentSecond = dt.Second;

                    lastSecond = dt.Second;

                    do
                    {
                        dt = UCDT.GetCurrentEastCoastTime();

                        currentSecond = dt.Second;

                        if (currentSecond != lastSecond)
                        {
                            lastSecond = currentSecond;

                            if (currentSecond == targetSecond)
                            {
                                Log.AddLine("Continuous task target second hit: " + currentSecond, Verbosity.Verbose);

                                // call method (also pass in the current call count)
                                Type.GetType(item.Class).GetMethod(item.Call).Invoke(null, new object[] { callCount });

                                // increment call count
                                callCount++;
                                break;
                            }
                        }
                        else
                        {
                            UC.SleepNewSecondSensitive();
                        }
                    } while (ts.IsNotEndIt());
                }
                else
                {
                    UC.SleepNewMinuteSensitive();
                }
                // until current time is equal to Until time
            } while (ts.IsNotEndIt());

            ts.TrySignalEndedIt();
        }
        */

        /*
        private void ExecuteMethod(SchedulerItem item, TaskSignals ts, bool isAfterEndIt = false)
        {
            string typeName = isAfterEndIt ? item.AfterEndItClass + ", " + item.AfterEndItAssembly : item.Class + ", " + item.Assembly;

            string methodName = isAfterEndIt ? item.AfterEndItCall : item.Call;

            Type.GetType(typeName).GetMethod(methodName).Invoke(null, new object[] { ts });
        }
        */
        /*
        private void RunItem(int index, bool backtrack, string currentKey)
        {

            // the key is based on current time, so get it now instead of after
            string key = currentKey;

            Log.AddLine("RunItem: " + SchedulerItems[index].Id, Verbosity.Normal);

            SchedulerItem item = SchedulerItems[index];

            TaskSignals ts = new TaskSignals();

            bool tryMarkingItemAsComplete = false;

            if (item.Type == SchedulerItemType.Single)
            {
                CurrentTask = item.Id;

                

                // run it
                Task.Run(() => ExecuteMethod(item, ts.Reset()));
                
                WaitForIsNotRunning(ts);

                tryMarkingItemAsComplete = true;
            }
            else if(!backtrack && item.Type == SchedulerItemType.SingleEndless)
            {

                CurrentTask = item.Id;

                // run it
                Task.Run(() => ExecuteMethod(item, ts.Reset()));

                WaitForIsNotRunningEndLess(ts, item);

                tryMarkingItemAsComplete = true;
                
            }
            
            else if (!backtrack && item.Type == SchedulerItemType.Continuous)
            {
                CurrentTask = item.Id;

                Task.Run(() => ContinouslyExecuteMethod(item, ts.Reset()));

                WaitForIsNotRunningContinous(ts, item);

                tryMarkingItemAsComplete = true;


            }
            

            if (tryMarkingItemAsComplete)
            {
                TryMarkingItemAsComplete(item, key);
            }

            if (TS.IsEndIt() && !String.IsNullOrWhiteSpace(item.AfterEndItCall))
            {
                Log.AddLine("Calling AfterEndItCall: " + item.AfterEndItCall, Verbosity.Normal);
                // don't start a new thread because these should be quick calls
                ExecuteMethod(item, new TaskSignals(), true);
            }
        }
        */



    }
}
