using NodaTime;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Numerics;
using System.IO;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Formatters.Binary;
using System.Xml.Serialization;
using System.Net;
using System.Threading;
using System.Collections.Specialized;
using System.Security.Cryptography;
using AT.Tools.LogObjects;
using Jil;
using System.Reflection;
using System.Text.RegularExpressions;
using System.Diagnostics;

namespace AT
{

    [Serializable]
    public class ApplicationStats {

        [JilDirective(Ignore = true)]
        private static PerformanceCounter CPUCounter = null;

        [JilDirective(Ignore = true)]
        private static PerformanceCounter RAMCounter = null;

        [JilDirective(Name = "cpuUsage")]
        public string CPUUsage {
            get {
                return Math.Round((CPUCounter.NextValue() / (float)10), 1) + "%";
            }
        }

        [JilDirective(Name = "totalThreads")]
        public string TotalThreads
        {
            get
            {
                return Process.GetCurrentProcess().Threads.Count.ToString();
            }
        }

        /*
        [JilDirective(Name = "availableThreadPoolWorkerThreads")]
        public string AvailableThreadPoolWorkerThreads
        {
            get
            {
                int workerThreads;
                int completionPortThreads;
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
                return workerThreads.ToString();
            }
        }

        [JilDirective(Name = "availableThreadPoolAsyncIOThreads")]
        public string AvailableThreadPoolAsyncIOThreads
        {
            get
            {
                int workerThreads;
                int completionPortThreads;
                ThreadPool.GetAvailableThreads(out workerThreads, out completionPortThreads);
                return completionPortThreads.ToString();
            }
        }
        */
        [JilDirective(Name = "ramUsage")]
        public string RamUsage
        {
            get
            {
                return Math.Round((decimal)RAMCounter.NextValue() / 1000000M) + " MB";
            }
        }

        public ApplicationStats()
        {
            Process p = Process.GetCurrentProcess();
            CPUCounter = new PerformanceCounter("Process", "% Processor Time", p.ProcessName);
            RAMCounter = new PerformanceCounter("Process", "Working Set", p.ProcessName);
        }
    }

    public class Subscription
    {
        public bool Subscribed = false;

        public object State;
        // lower is faster
        public int Interval = 1;

        private int Count = 1;

        public bool IsReady()
        {
            Count++;
            if(Count >= Interval)
            {
                Count = 0;
                return true;
            }
            else
            {
                return false;
            }
        }
    }

    [Serializable]
    public class ThreadControl
    {
        [JilDirective(Name = "id")]
        public int Id = 0;

        [JilDirective(Name = "name")]
        public string Name = "";

        [JilDirective(Name = "children")]
        public List<ThreadControl> Children = null;

        [JilDirective(Ignore = true)]
        public Log Log = null;

        [JilDirective(Ignore = true)]
        public Queue<Signal> SignalsForChild = null;

        [JilDirective(Ignore = true)]
        public Queue<Signal> SignalsForParent = null;

        [JilDirective(Ignore = true)]
        public object LockObj = new object();

        [JilDirective(Name = "startTime")]
        public long StartTime = 0;

        [JilDirective(Name = "endTime")]
        public long EndTime = 0;

        [JilDirective(Name = "state")]
        public ThreadControlState State = ThreadControlState.Waiting;

        [JilDirective(Ignore = true)]
        public bool StateChanged = false;

        [JilDirective(Name = "objectId")]
        public string ObjectId = null;

        public ThreadControl(string name, string objectId = null)
        {
            Id = UC.GetRandomInteger(1, int.MaxValue);
            ObjectId = objectId;
            Log = new Log();
            Name = name;
            Children = new List<ThreadControl>();
            
            SignalsForChild = new Queue<Signal>();
            SignalsForParent = new Queue<Signal>();
            
            ResetDefault();
        }

        public static void SignalChildAndWaitForParentSignal(ThreadControl childTC, ThreadControl parentTC, Signal childSignal, Signal parentSignal)
        {
            childTC.AddSignalForChild(childSignal);
            while(childTC.PopSignalForParent() != parentSignal && parentTC.CheckNotStopped())
            {

            }
        }

        private void ResetDefault()
        {
            lock (LockObj)
            {
                SignalsForChild.Clear();
                SignalsForParent.Clear();
            }
            State = ThreadControlState.Waiting;
            StartTime = 0;
            EndTime = 0;
            Children.Clear();
            StateChanged = true;
        }

        public bool IsOver()
        {
            return State == ThreadControlState.Done || State == ThreadControlState.Complete;
        }

        public bool ReadStateChanged()
        {
            lock (LockObj)
            {
                return StateChanged;
            }
        }
        public void SetStateChanged(bool value)
        {
            lock (LockObj)
            {
                StateChanged = value;
            }
        }

        public void BeforeExecute()
        {
            ResetDefault();

            State = ThreadControlState.Running;

            StartTime = UCDT.DateTimeToUnixTimeStamp(DateTime.Now);

            SetStateChanged(true);
        }
        public Signal PopSignalForChild()
        {
            lock (LockObj)
            {
                // don't pop if it's a stop signal, let the child call CheckNotStopped
                if (SignalsForChild.Count > 0 && SignalsForChild.Peek() != Signal.Stop)
                {
                    return SignalsForChild.Dequeue();
                }
                else
                {
                    return Signal.Empty;
                }
            }
        }

        public int GetChildrenSignalCountForParent(Signal targetSignal)
        {
            int result = 0;

            for(int n = 0; n < Children.Count; n++)
            {
                Signal signal = Children[n].PopSignalForParent();
                if(signal == targetSignal)
                {
                    result++;
                }
            }

            return result;
        }

        public Signal PopSignalForParent()
        {
            lock (LockObj)
            {
                if (SignalsForParent.Count > 0)
                {
                    return SignalsForParent.Dequeue();
                }
                else
                {
                    return Signal.Empty;
                }
            }
        }

        /// <summary>
        /// This is the function you should call to check if the parent has stopped this or not...
        /// ... if it has, the thread should just exit asap
        /// </summary>
        /// <returns></returns>
        public bool CheckNotStopped()
        {

            // this is here because some times child threads might call this method twice
            if(State == ThreadControlState.Stopped)
            {
                return false;
            }

            bool stop = false;

            lock (LockObj)
            {
                if(SignalsForChild.Count > 0)
                {
                    Signal topSignal = SignalsForChild.Peek();
                    if(topSignal == Signal.Stop)
                    {
                        SignalsForChild.Dequeue();
                        stop = true;
                    }
                }
            }

            if (stop) // if it was stopped by parent
            {

                State = ThreadControlState.Stopping;
                SetStateChanged(true);

                AddSignalToAllChildren(Signal.Stop);

                while (Children.Exists(t => t.State != ThreadControlState.Complete && t.State != ThreadControlState.Done)) { } // wait for all children to stop

                State = ThreadControlState.Stopped;
                SetStateChanged(true);

                return false;
            }
            else
            {
                return true;
            }
        }

        public void StopAllChildrenAndWait()
        {
            AddSignalToAllChildren(Signal.Stop);

            while (Children.Exists(t => t.State != ThreadControlState.Complete && t.State != ThreadControlState.Done)) { } // wait for all children to stop
        }

        public void AddSignalForParent(Signal signal)
        {
            lock (LockObj)
            {
                SignalsForParent.Enqueue(signal);
            }
        }

        public void AddSignalForChild(Signal signal)
        {
            lock (LockObj)
            {
                SignalsForChild.Enqueue(signal);
            }
        }

        public void AddSignalToAllChildren(Signal signal)
        {
            Children.ForEach(a => a.AddSignalForChild(signal));
        }

        public bool CanBeStopped()
        {
            return State == ThreadControlState.Running;
        }

        public bool CanBeStarted()
        {
            return State == ThreadControlState.Done || State == ThreadControlState.Complete || State == ThreadControlState.Waiting;
        }

        public void ClearChildren()
        {
            lock (Global.State.ThreadControlTreeLock)
            {
                if (Children.Exists(t => t.State != ThreadControlState.Complete && t.State != ThreadControlState.Done))
                {
                    throw new Exception("THERE ARE CHILDREN THAT AREN'T DONE OR COMPLETE!");
                }

                Children.Clear();
            }
        }




        public void WaitForAllChildrenBySignal(Signal signal)
        {
            int childrenDone = 0;
            do
            {
                Thread.Sleep(1);
                childrenDone += GetChildrenSignalCountForParent(signal);
            } while (childrenDone < Children.Count);
        }

        public void AfterExecute()
        {
            EndTime = UCDT.DateTimeToUnixTimeStamp(DateTime.Now);

            ClearChildren();

            if (State == ThreadControlState.Stopped)
            {
                State = ThreadControlState.Done;
            }
            else
            {
                State = ThreadControlState.Complete;
            }

            StateChanged = true;
        }

    }


    

    /// <summary>
    /// Flattened datetime. Only stores year, month, and day.
    /// </summary>
    public class FD : IComparable<FD>
    {

        public DateTime DT;

        public DateTimeZone Zone;

        /// <summary>
        /// 4am of this day.
        /// </summary>
        public long StartNano;

        /// <summary>
        /// 8pm of this day.
        /// </summary>
        public long EndNano;

        /// <summary>
        /// 12am of this day
        /// </summary>
        //public long StartNanoDay;

        /// <summary>
        /// 12am of the next day
        /// </summary>
        //public long EndNanoDay;

        public bool IsEqual(FD fd)
        {
            //return (fd.Zone == Zone && DT.Year == fd.DT.Year && DT.Month == fd.DT.Month && DT.Day == fd.DT.Day);
            return (fd.StartNano == StartNano && fd.EndNano == EndNano);
        }
        

        public FD(DateTime dt, DateTimeZone zone = null)
        {
            DT = new DateTime(dt.Year, dt.Month, dt.Day, 0, 0, 0, DateTimeKind.Unspecified);
            Zone = zone == null ? UCDT.TimeZones.Eastern : zone;
            ComputeNanos();
        }

        public FD(int year, int month, int day, DateTimeZone zone = null)
        {
            DT = new DateTime(year, month, day, 0, 0, 0, DateTimeKind.Unspecified);
            Zone = zone == null ? UCDT.TimeZones.Eastern : zone;
            ComputeNanos();
        }

        private void ComputeNanos()
        {
            DateTime DT4am = new DateTime(DT.Year, DT.Month, DT.Day, 4, 0, 0, DateTimeKind.Unspecified);
            DateTime DT8pm = new DateTime(DT.Year, DT.Month, DT.Day, 20, 0, 0, DateTimeKind.Unspecified);

            DateTime startUTC = UCDT.ZonedDateTimetoUTCDateTime(DT4am, Zone);
            DateTime endUTC = UCDT.ZonedDateTimetoUTCDateTime(DT8pm, Zone);

            DateTime startDayUTC = UCDT.ZonedDateTimetoUTCDateTime(DT, Zone);
            DateTime endDayUTC = UCDT.ZonedDateTimetoUTCDateTime(DT.AddDays(1), Zone);


            StartNano = UCDT.DateTimeToNanoUnix(startUTC);
            EndNano = UCDT.DateTimeToNanoUnix(endUTC);

            //StartNanoDay = UCDT.DateTimeToNanoUnix(startDayUTC);
            //EndNanoDay = UCDT.DateTimeToNanoUnix(endDayUTC);
        }

        public void AddDays(int days)
        {
            DT = DT.AddDays(days);
            ComputeNanos();
        }

        public FD DeepCopy()
        {
            return new FD(DT, Zone);
        }
        public bool IsOnWeekend()
        {
            return UCDT.ZonedDateTimeIsOnWeekend(DT, Zone);
        }

        public override string ToString()
        {
            return DT.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        }
        public string ToStringLong()
        {
            return UCDT.ZonedDateTimetoZonedDateTime(DT, Zone).ToString();
        }

        // mostly used for binarysearch
        public int CompareTo(FD that)
        {
            return this.StartNano.CompareTo(that.StartNano);
        }

        public int GetHashCode(FD fd)
        {
            unchecked
            {
                int a = UC.NanoToFloorMin(StartNano);
                int b = UC.NanoToFloorMin(EndNano);
                return  a + b;
            }
        }
        
    }



    /// <summary>
    /// Arbitrary precision decimal.
    /// All operations are exact, except for division. Division never determines more digits than the given precision.
    /// Source: https://gist.github.com/JcBernack/0b4eef59ca97ee931a2f45542b9ff06d
    /// Based on https://stackoverflow.com/a/4524254
    /// Author: Jan Christoph Bernack (contact: jc.bernack at gmail.com)
    /// License: public domain
    /// </summary>
    public struct BigDecimal
        : IComparable
        , IComparable<BigDecimal>
    {
        /// <summary>
        /// Specifies whether the significant digits should be truncated to the given precision after each operation.
        /// </summary>
        public static bool AlwaysTruncate = false;

        /// <summary>
        /// Sets the maximum precision of division operations.
        /// If AlwaysTruncate is set to true all operations are affected.
        /// </summary>
        // public static int Precision = 28;
        public static int Precision = 27;

        public BigInteger Mantissa { get; set; }
        public int Exponent { get; set; }

        public BigDecimal(BigInteger mantissa, int exponent)
            : this()
        {
            Mantissa = mantissa;
            Exponent = exponent;
            Normalize();
            if (AlwaysTruncate)
            {
                Truncate();
            }
        }

        /// <summary>
        /// Removes trailing zeros on the mantissa
        /// </summary>
        public void Normalize()
        {
            if (Mantissa.IsZero)
            {
                Exponent = 0;
            }
            else
            {
                BigInteger remainder = 0;
                while (remainder == 0)
                {
                    var shortened = BigInteger.DivRem(Mantissa, 10, out remainder);
                    if (remainder == 0)
                    {
                        Mantissa = shortened;
                        Exponent++;
                    }
                }
            }
        }

        /// <summary>
        /// Truncate the number to the given precision by removing the least significant digits.
        /// </summary>
        /// <returns>The truncated number</returns>
        public BigDecimal Truncate(int precision)
        {
            // copy this instance (remember it's a struct)
            var shortened = this;
            // save some time because the number of digits is not needed to remove trailing zeros
            shortened.Normalize();
            // remove the least significant digits, as long as the number of digits is higher than the given Precision
            while (NumberOfDigits(shortened.Mantissa) > precision)
            {
                shortened.Mantissa /= 10;
                shortened.Exponent++;
            }
            // normalize again to make sure there are no trailing zeros left
            shortened.Normalize();
            return shortened;
        }

        public BigDecimal Truncate()
        {
            return Truncate(Precision);
        }

        public BigDecimal Floor()
        {
            return Truncate(BigDecimal.NumberOfDigits(Mantissa) + Exponent);
        }

        public static int NumberOfDigits(BigInteger value)
        {
            // do not count the sign
            //return (value * value.Sign).ToString().Length;
            // faster version
            return (int)Math.Ceiling(BigInteger.Log10(value * value.Sign));
        }

        #region Conversions

        public static implicit operator BigDecimal(int value)
        {
            return new BigDecimal(value, 0);
        }

        public static implicit operator BigDecimal(double value)
        {
            var mantissa = (BigInteger)value;
            var exponent = 0;
            double scaleFactor = 1;
            while (Math.Abs(value * scaleFactor - (double)mantissa) > 0)
            {
                exponent -= 1;
                scaleFactor *= 10;
                mantissa = (BigInteger)(value * scaleFactor);
            }
            return new BigDecimal(mantissa, exponent);
        }

        public static implicit operator BigDecimal(decimal value)
        {
            var mantissa = (BigInteger)value;
            var exponent = 0;
            decimal scaleFactor = 1;
            while ((decimal)mantissa != value * scaleFactor)
            {
                exponent -= 1;
                scaleFactor *= 10;
                mantissa = (BigInteger)(value * scaleFactor);
            }
            return new BigDecimal(mantissa, exponent);
        }

        public static explicit operator double(BigDecimal value)
        {
            return (double)value.Mantissa * Math.Pow(10, value.Exponent);
        }

        public static explicit operator float(BigDecimal value)
        {
            return Convert.ToSingle((double)value);
        }

        public static explicit operator decimal(BigDecimal value)
        {
            decimal d1 = (decimal)value.Mantissa;
            decimal d2 = (decimal)Math.Pow(10, value.Exponent);

            return d1 * d2;
            // return (decimal)value.Mantissa * (decimal)Math.Pow(10, value.Exponent);
        }

        public static explicit operator int(BigDecimal value)
        {
            return (int)(value.Mantissa * BigInteger.Pow(10, value.Exponent));
        }

        public static explicit operator uint(BigDecimal value)
        {
            return (uint)(value.Mantissa * BigInteger.Pow(10, value.Exponent));
        }

        #endregion

        #region Operators

        public static BigDecimal operator +(BigDecimal value)
        {
            return value;
        }

        public static BigDecimal operator -(BigDecimal value)
        {
            value.Mantissa *= -1;
            return value;
        }

        public static BigDecimal operator ++(BigDecimal value)
        {
            return value + 1;
        }

        public static BigDecimal operator --(BigDecimal value)
        {
            return value - 1;
        }

        public static BigDecimal operator +(BigDecimal left, BigDecimal right)
        {
            return Add(left, right);
        }

        public static BigDecimal operator -(BigDecimal left, BigDecimal right)
        {
            return Add(left, -right);
        }

        private static BigDecimal Add(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent
                ? new BigDecimal(AlignExponent(left, right) + right.Mantissa, right.Exponent)
                : new BigDecimal(AlignExponent(right, left) + left.Mantissa, left.Exponent);
        }

        public static BigDecimal operator *(BigDecimal left, BigDecimal right)
        {
            return new BigDecimal(left.Mantissa * right.Mantissa, left.Exponent + right.Exponent);
        }

        public static BigDecimal operator /(BigDecimal dividend, BigDecimal divisor)
        {
            var exponentChange = Precision - (NumberOfDigits(dividend.Mantissa) - NumberOfDigits(divisor.Mantissa));
            if (exponentChange < 0)
            {
                exponentChange = 0;
            }
            dividend.Mantissa *= BigInteger.Pow(10, exponentChange);
            return new BigDecimal(dividend.Mantissa / divisor.Mantissa, dividend.Exponent - divisor.Exponent - exponentChange);
        }

        public static BigDecimal operator %(BigDecimal left, BigDecimal right)
        {
            return left - right * (left / right).Floor();
        }

        public static bool operator ==(BigDecimal left, BigDecimal right)
        {
            return left.Exponent == right.Exponent && left.Mantissa == right.Mantissa;
        }

        public static bool operator !=(BigDecimal left, BigDecimal right)
        {
            return left.Exponent != right.Exponent || left.Mantissa != right.Mantissa;
        }

        public static bool operator <(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) < right.Mantissa : left.Mantissa < AlignExponent(right, left);
        }

        public static bool operator >(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) > right.Mantissa : left.Mantissa > AlignExponent(right, left);
        }

        public static bool operator <=(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) <= right.Mantissa : left.Mantissa <= AlignExponent(right, left);
        }

        public static bool operator >=(BigDecimal left, BigDecimal right)
        {
            return left.Exponent > right.Exponent ? AlignExponent(left, right) >= right.Mantissa : left.Mantissa >= AlignExponent(right, left);
        }

        /// <summary>
        /// Returns the mantissa of value, aligned to the exponent of reference.
        /// Assumes the exponent of value is larger than of reference.
        /// </summary>
        private static BigInteger AlignExponent(BigDecimal value, BigDecimal reference)
        {
            return value.Mantissa * BigInteger.Pow(10, value.Exponent - reference.Exponent);
        }

        #endregion

        #region Additional mathematical functions

        public static BigDecimal Exp(double exponent)
        {
            var tmp = (BigDecimal)1;
            while (Math.Abs(exponent) > 100)
            {
                var diff = exponent > 0 ? 100 : -100;
                tmp *= Math.Exp(diff);
                exponent -= diff;
            }
            return tmp * Math.Exp(exponent);
        }

        public static BigDecimal Pow(double basis, double exponent)
        {
            var tmp = (BigDecimal)1;
            while (Math.Abs(exponent) > 100)
            {
                var diff = exponent > 0 ? 100 : -100;
                tmp *= Math.Pow(basis, diff);
                exponent -= diff;
            }
            return tmp * Math.Pow(basis, exponent);
        }

        #endregion

        public override string ToString()
        {
            return string.Concat(Mantissa.ToString(), "E", Exponent);
        }

        public bool Equals(BigDecimal other)
        {
            return other.Mantissa.Equals(Mantissa) && other.Exponent == Exponent;
        }

        public override bool Equals(object obj)
        {
            if (ReferenceEquals(null, obj))
            {
                return false;
            }
            return obj is BigDecimal && Equals((BigDecimal)obj);
        }

        public override int GetHashCode()
        {
            unchecked
            {
                return (Mantissa.GetHashCode() * 397) ^ Exponent;
            }
        }

        public int CompareTo(object obj)
        {
            if (ReferenceEquals(obj, null) || !(obj is BigDecimal))
            {
                throw new ArgumentException();
            }
            return CompareTo((BigDecimal)obj);
        }

        public int CompareTo(BigDecimal other)
        {
            return this < other ? -1 : (this > other ? 1 : 0);
        }
    }


    public class UCDT
    {


        public class TimeZones
        {
            // public static DateTimeZone Eastern = DateTimeZoneProviders.Tzdb["America/New_York"];
            public static DateTimeZone Eastern = DateTimeZoneProviders.Tzdb["US/Eastern"];
        }

        public static ZonedDateTime GetCurrentEastCoastTime()
        {
            return SystemClock.Instance.GetCurrentInstant().InZone(UCDT.TimeZones.Eastern);
        }

        /// <summary>
        /// This should be tested. The last 2 digits of nanoUnix are rounded down.
        /// </summary>
        /// <param name="nanoUnix"></param>
        /// <returns></returns>
        public static DateTime NanoUnixToDateTime(long nanoUnix)
        {
            long n = (long)Math.Floor((decimal)((decimal)nanoUnix / (decimal)100));
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddTicks(n);
        }

        public static long EasternDateTimeToNanoUnix(DateTime easternDateTime)
        {
            return UCDT.DateTimeToNanoUnix(UCDT.ZonedDateTimetoUTCDateTime(easternDateTime, UCDT.TimeZones.Eastern));
        }

        public static long DateTimeToNanoUnix(DateTime dateTimeUTC)
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (dateTimeUTC - epochStart).Ticks * 100;
        }

        public static long GetCurrentNanoUnix()
        {
            DateTime epochStart = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);

            return (DateTime.UtcNow - epochStart).Ticks * 100;
        }
        public static int DateTimeToUnixTimeStamp(DateTime dateTimeUTC)
        {

            return (int)(dateTimeUTC.Subtract(new DateTime(1970, 1, 1, 0, 0, 0, 0))).TotalSeconds;
        }



        public static DateTime UnixTimeStampToDateTime(double unixTimeStamp)
        {
            return new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc).AddSeconds(unixTimeStamp);
        }


        /*
        public static ZonedDateTime UTCDateTimeToZonedDateTime(DateTime sourceUtc, DateTimeZone timezone)
        {
            // convert to instant from UTC - see http://stackoverflow.com/questions/20807799/using-nodatime-how-to-convert-an-instant-to-the-corresponding-systems-zoneddat
            Instant instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(sourceUtc, DateTimeKind.Utc));
            return instant.InZone(timezone); //.ToDateTimeUnspecified();
        }
        */

        public static ZonedDateTime GetCurrentSystemZonedDateTime()
        {
            Instant now = SystemClock.Instance.GetCurrentInstant();
            DateTimeZone tz = DateTimeZoneProviders.Bcl.GetSystemDefault();
            return now.InZone(tz);
        }


        /// <summary>
        /// The DateTime here must have a "Kind" of Utc.
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="timezone"></param>
        /// <returns></returns>
        public static ZonedDateTime UTCDateTimeToZonedDateTime(DateTime dateTime, DateTimeZone timezone)
        {
            Instant instant = Instant.FromDateTimeUtc(DateTime.SpecifyKind(dateTime, DateTimeKind.Utc));
            return instant.InZone(timezone);
        }

        /// <summary>
        /// The DateTime here should have a "Kind" of Unspecified
        /// </summary>
        /// <param name="dateTime"></param>
        /// <param name="timezone"></param>
        /// <returns></returns>
        public static DateTime ZonedDateTimetoUTCDateTime(DateTime dateTime, DateTimeZone timezone)
        {
            ZonedDateTime zonedDbDateTime = timezone.AtLeniently(LocalDateTime.FromDateTime(dateTime));
            return zonedDbDateTime.ToDateTimeUtc();
        }

        public static ZonedDateTime ZonedDateTimetoZonedDateTime(DateTime dateTime, DateTimeZone timezone)
        {
            return timezone.AtLeniently(LocalDateTime.FromDateTime(dateTime));
        }

        public static bool ZonedDateTimeIsOnWeekend(DateTime dateTime, DateTimeZone timezone)
        {
            ZonedDateTime zonedDbDateTime = timezone.AtLeniently(LocalDateTime.FromDateTime(dateTime));
            return zonedDbDateTime.DayOfWeek == IsoDayOfWeek.Sunday || zonedDbDateTime.DayOfWeek == IsoDayOfWeek.Saturday;
        }
    }


    public class GenericDictionary
    {
        private Dictionary<string, object> _dict = new Dictionary<string, object>();

        public void Add<T>(string key, T value) where T : class
        {
            _dict.Add(key, value);
        }

        public T GetValue<T>(string key) where T : class
        {
            return _dict[key] as T;
        }
    }


    public class UC
    {

        public static string DecimalToUSD(decimal value, int roundDecimals = 2)
        {
            return String.Format("{0:C" + roundDecimals.ToString() + "}", value);
            //return "$" + Math.Round(value, roundDecimals).ToString();
        }

        public static decimal TruncateDecimal(decimal d, int decimals)
        {
            if (decimals < 0)
                throw new ArgumentOutOfRangeException("decimals", "Value must be in range 0-28.");
            else if (decimals > 28)
                throw new ArgumentOutOfRangeException("decimals", "Value must be in range 0-28.");
            else if (decimals == 0)
                return Math.Truncate(d);
            else
            {
                decimal integerPart = Math.Truncate(d);
                decimal scalingFactor = d - integerPart;
                decimal multiplier = (decimal)Math.Pow(10, decimals);

                scalingFactor = Math.Truncate(scalingFactor * multiplier) / multiplier;

                return integerPart + scalingFactor;
            }
        }


        public static int GetDecimalPlaces(decimal n)
        {
            n = Math.Abs(n); //make sure it is positive.
            n -= (int)n;     //remove the integer part of the number.
            var decimalPlaces = 0;
            while (n > 0)
            {
                decimalPlaces++;
                n *= 10;
                n -= (int)n;
            }
            return decimalPlaces;
        }

        public static string NormalizeSourceCode(string sourceCode)
        {
            string result = sourceCode;

            // REMOVE COMMENTS
            var blockComments = @"/\*(.*?)\*/";
            var lineComments = @"//(.*?)\r?\n";
            var strings = @"""((\\[^\n]|[^""\n])*)""";
            var verbatimStrings = @"@(""[^""]*"")+";
            result = Regex.Replace(result, blockComments + "|" + lineComments + "|" + strings + "|" + verbatimStrings, me =>
            {
                if (me.Value.StartsWith("/*") || me.Value.StartsWith("//"))
                {
                    return me.Value.StartsWith("//") ? Environment.NewLine : "";
                }
                // Keep the literal strings
                return me.Value;
            }, RegexOptions.Singleline);

            // REPLACE ANY RUNS OF WHITE SPACE WITH A SINGLE SPACE
            result = Regex.Replace(result, @"\s+", " ");

            return result.Trim();
        }

        public static Type[] GetTypesInNamespace(Assembly assembly, string nameSpace)
        {

            Type[] allTypes = assembly.GetTypes();
            return
              assembly.GetTypes()
                      .Where(t => String.Equals(t.Namespace, nameSpace, StringComparison.Ordinal) && !t.Name.Contains("<"))
                      .ToArray();
        }

        public static string MD5Hash(string input)
        {
            MD5 md5Hasher = MD5.Create();
            byte[] data = md5Hasher.ComputeHash(Encoding.Default.GetBytes(input));

            return GenericBaseConverter.ConvertToString(data, "abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789", 0);
        }

        /// <summary>
        /// Crude way to do this, works but don't rely on it
        /// </summary>
        /// <param name="source"></param>
        /// <returns></returns>
        public static string RemoveJSLineComments(string source)
        {
            string result = "";

            string[] s1 = source.Split('\n');
            for(int n = 0; n < s1.Length; n++)
            {
                string[] s2 = s1[n].Split("//".ToCharArray());
                result += s2[0] + "\n";

            }
            return result;
        }

        public static int GetRandomInteger(double start, double end)
        {
            // not efficient
            return (int)Math.Round(GetRandomDouble(start - 0.5, end + 0.5), MidpointRounding.AwayFromZero);
        }


        // gets very close to minimum but never touches, same for maximum
        public static double GetRandomDouble(double minimum, double maximum)
        {
            // Step 1: fill an array with 8 random bytes
            var rng = new RNGCryptoServiceProvider();
            var bytes = new Byte[8];
            rng.GetBytes(bytes);
            // Step 2: bit-shift 11 and 53 based on double's mantissa bits
            var ul = BitConverter.ToUInt64(bytes, 0) / (1 << 11);
            Double d = ul / (Double)(1UL << 53);
            return d * (maximum - minimum) + minimum;
        }
        public static int[] StringArrayToIntArray(string[] numbers)
        {
            int[] result = new int[numbers.Length];

            for (int n = 0; n < numbers.Length; n++)
            {
                result[n] = int.Parse(numbers[n]);
            }

            return result;
        }
        /// <summary>
        /// Will sleep for a safe period of time to make sure that we don't miss when a new second has occured
        /// </summary>
        public static void SleepNewSecond()
        {
            Thread.Sleep(100);
        }

        /// <summary>
        /// Will sleep for a safe period of time to make sure that we don't miss when a new second has occured
        /// </summary>
        public static void SleepNewSecondSensitive()
        {
            Thread.Sleep(30);
        }
        /// <summary>
        /// Will sleep for a safe period of time to make sure that we don't miss when a new minute has occured
        /// </summary>
        public static void SleepNewMinuteSensitive()
        {
            Thread.Sleep(500);
        }
        /// <summary>
        /// Will sleep for a safe period of time to make sure that we don't miss when a new minute has occured
        /// </summary>
        public static void SleepNewMinute()
        {
            Thread.Sleep(10000);
        }
        public static string BoolToJSBool(bool val)
        {
            return val ? "true" : "false";
        }
        public static long GetFolderSize(string s)
        {
            string[] fileNames = Directory.GetFiles(s, "*.*");

            long size = 0;

            // Calculate total size by looping through files in the folder and totalling their sizes
            foreach (string name in fileNames)
            {
                // length of each file.
                FileInfo details = new FileInfo(name);
                size += details.Length;
            }

            return size;
        }

        public static decimal ComputeVariance(List<decimal> points, decimal mean)
        {
            decimal d1 = 0;

            for (int n = 0; n < points.Count; n++)
            {
                decimal d2 = (points[n] - mean);
                d1 += d2 * d2;
            }

            return d1 / (decimal)points.Count;
        }
        public static decimal ComputeVariance(List<Geometry.Objects.Point> points, decimal mean)
        {
            decimal d1 = 0;

            for (int n = 0; n < points.Count; n++)
            {
                decimal d2 = (points[n].Y - mean);
                d1 += d2 * d2;
            }

            return d1 / (decimal)points.Count;
        }

        public static int SumDictionaryIntInt(Dictionary<int, int> dic)
        {
            int result = 0;

            foreach (KeyValuePair<int, int> kv in dic)
            {
                result += kv.Value;
            }

            return result;
        }
        public static decimal CalcAverageYChangeFromPoints(List<Geometry.Objects.Point> points)
        {
            if (points.Count <= 1)
            {
                return 0M;
            }

            decimal totalChange = 0;
            decimal lastY = -1;

            for (int n = 0; n < points.Count; n++)
            {
                if (lastY != -1)
                {
                    totalChange += Math.Abs(points[n].Y - lastY);
                }

                lastY = points[n].Y;

            }

            return (totalChange / (points.Count - 1));
        }
        public static decimal GetTotalFromDecimals(List<decimal> vals)
        {
            decimal result = 0;

            for (int n = 0; n < vals.Count; n++)
            {
                result += vals[n];
            }

            return result;
        }

        public static decimal GetTotalYFromPoints(List<Geometry.Objects.Point> points)
        {
            decimal result = 0;

            for (int n = 0; n < points.Count; n++)
            {
                result += points[n].Y;
            }

            return result;
        }
        public static Dictionary<int, float> DictionaryIntToPercentage(Dictionary<int, int> source)
        {

            Dictionary<int, float> result = new Dictionary<int, float>();

            int[] keys = source.Keys.ToArray();

            int total = 0;

            for (int n = 0; n < keys.Length; n++)
            {
                total += source[keys[n]];
            }

            for (int n = 0; n < keys.Length; n++)
            {
                result.Add(keys[n], (float)source[keys[n]] / (float)total);
            }

            return result;
        }


        public static string NameValueCollectionToString(NameValueCollection nvc)
        {
            return String.Join("&", nvc.AllKeys.Select(a => a + "=" + nvc[a]));
        }
        public static string DownloadString(string url)
        {
            // make this better
            return new WebClient().DownloadString(url);
        }

        public static decimal MillisecondsToMinutes(long milliseconds, int decimals)
        {
            return Math.Round((decimal)((decimal)((decimal)milliseconds / 1000) / 60), decimals);
        }

        public static decimal MillisecondsToSeconds(long milliseconds, int decimals)
        {
            return Math.Round((decimal)((decimal)((decimal)milliseconds / 1000)), decimals);
        }

        public static long NanoToFloorSec(long nanoSeconds)
        {
            return (long)Math.Floor((decimal)((decimal)nanoSeconds / 1000000000M));
        }

        public static int NanoToFloorMin(long nanoSeconds)
        {
            return (int)Math.Floor((decimal)((decimal)nanoSeconds / 60000000000M));
        }


        public static decimal NanoToSec(long nanoSeconds)
        {
            return nanoSeconds / 1000000000M;
        }


        public static string DownloadStringAsync(string url)
        {
            string result = "";

            bool tryAgain = false;

            try
            {
                Task<string> t = new WebClient().DownloadStringTaskAsync(url);
                result = t.Result;
            }
            catch (Exception)
            {
                tryAgain = true;
            }

            while (tryAgain)
            {

                Thread.Sleep(1500);

                tryAgain = false;

                try
                {
                    Task<string> t = new WebClient().DownloadStringTaskAsync(url);
                    result = t.Result;
                }
                catch (Exception)
                {
                    tryAgain = true;
                }
            }

            return result;
        }
        public static string ListToString(List<string> source, string delimeter)
        {
            return String.Join(delimeter, source);
        }

        public static List<string> MultilineStringToList(string source)
        {
            List<string> result = new List<string>();
            using (StringReader reader = new StringReader(source))
            {

                string line = "";

                while ((line = reader.ReadLine()) != null)
                {
                    if (!String.IsNullOrWhiteSpace(line))
                    {
                        result.Add(line);
                    }
                }
            }
            return result;
        }




        public static int DeleteFoldersAndFiles(string folderPath)
        {
            DirectoryInfo di = new DirectoryInfo(folderPath);

            int itemsDeleted = 0;

            foreach (FileInfo file in di.GetFiles())
            {
                itemsDeleted++;
                file.Delete();
            }
            foreach (DirectoryInfo dir in di.GetDirectories())
            {
                itemsDeleted++;
                dir.Delete(true);
            }

            return itemsDeleted;
        }

        public static string[] ReadFileLinesToArray(string filePath)
        {
            string[] lines = File.ReadAllLines(filePath);
            return lines;
        }
        public static string RandomString(int length)
        {
            Random random = new Random();
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            int min = 0;
            int max = chars.Length - 1;
            return new string(Enumerable.Repeat(chars, length)
              .Select(s => s[GetRandomInteger(min, max)]).ToArray());
        }
        /// <summary>
        /// Writes the given object instance to a binary file.
        /// <para>Object type (and all child types) must be decorated with the [Serializable] attribute.</para>
        /// <para>To prevent a variable from being serialized, decorate it with the [NonSerialized] attribute; cannot be applied to properties.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the binary file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the binary file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToBinaryFile<T>(string filePath, T objectToWrite)
        {
            using (Stream stream = File.Open(filePath, FileMode.Create))
            {
                var binaryFormatter = new System.Runtime.Serialization.Formatters.Binary.BinaryFormatter();
                binaryFormatter.Serialize(stream, objectToWrite);
            }
        }

        /// <summary>
        /// Reads an object instance from a binary file.
        /// </summary>
        /// <typeparam name="T">The type of object to read from the binary file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the binary file.</returns>
        public static T ReadFromBinaryFile<T>(string filePath)
        {
            using (Stream stream = File.Open(filePath, FileMode.Open))
            {
                var binaryFormatter = new BinaryFormatter();
                return (T)binaryFormatter.Deserialize(stream);
            }
        }

        /// <summary>
        /// Writes the given object instance to an XML file.
        /// <para>Only Public properties and variables will be written to the file. These can be any type though, even other classes.</para>
        /// <para>If there are public properties/variables that you do not want written to the file, decorate them with the [XmlIgnore] attribute.</para>
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object being written to the file.</typeparam>
        /// <param name="filePath">The file path to write the object instance to.</param>
        /// <param name="objectToWrite">The object instance to write to the file.</param>
        /// <param name="append">If false the file will be overwritten if it already exists. If true the contents will be appended to the file.</param>
        public static void WriteToXmlFile<T>(string filePath, T objectToWrite, bool append = false) where T : new()
        {
            TextWriter writer = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                writer = new StreamWriter(filePath, append);
                serializer.Serialize(writer, objectToWrite);
            }
            finally
            {
                if (writer != null)
                    writer.Close();
            }
        }

        /// <summary>
        /// Reads an object instance from an XML file.
        /// <para>Object type must have a parameterless constructor.</para>
        /// </summary>
        /// <typeparam name="T">The type of object to read from the file.</typeparam>
        /// <param name="filePath">The file path to read the object instance from.</param>
        /// <returns>Returns a new instance of the object read from the XML file.</returns>
        public static T ReadFromXmlFile<T>(string filePath) where T : new()
        {
            TextReader reader = null;
            try
            {
                var serializer = new XmlSerializer(typeof(T));
                reader = new StreamReader(filePath);
                return (T)serializer.Deserialize(reader);
            }
            finally
            {
                if (reader != null)
                    reader.Close();
            }
        }
        /// <summary>
        /// Reference Article http://www.codeproject.com/KB/tips/SerializedObjectCloner.aspx
        /// Provides a method for performing a deep copy of an object.
        /// Binary Serialization is used to perform the copy.
        /// Perform a deep Copy of the object.
        /// </summary>
        /// <typeparam name="T">The type of object being copied.</typeparam>
        /// <param name="source">The object instance to copy.</param>
        /// <returns>The copied object.</returns>
        public static T Clone<T>(T source)
        {
            if (!typeof(T).IsSerializable)
            {
                throw new ArgumentException("The type must be serializable.", nameof(source));
            }

            // Don't serialize a null object, simply return the default for that object
            if (Object.ReferenceEquals(source, null))
            {
                return default(T);
            }

            IFormatter formatter = new BinaryFormatter();
            Stream stream = new MemoryStream();
            using (stream)
            {
                formatter.Serialize(stream, source);
                stream.Seek(0, SeekOrigin.Begin);
                return (T)formatter.Deserialize(stream);
            }
        }
    }

    public static class GenericBaseConverter
    {
        public static string ConvertToString(byte[] valueAsArray, string digits, int pad)
        {
            if (digits == null)
                throw new ArgumentNullException("digits");
            if (digits.Length < 2)
                throw new ArgumentOutOfRangeException("digits", "Expected string with at least two digits");

            BigInteger value = new BigInteger(valueAsArray);
            bool isNeg = value < 0;
            value = isNeg ? -value : value;

            StringBuilder sb = new StringBuilder(pad + (isNeg ? 1 : 0));

            do
            {
                BigInteger rem;
                value = BigInteger.DivRem(value, digits.Length, out rem);
                sb.Append(digits[(int)rem]);
            } while (value > 0);

            // pad it
            if (sb.Length < pad)
                sb.Append(digits[0], pad - sb.Length);

            // if the number is negative, add the sign.
            if (isNeg)
                sb.Append('-');

            // reverse it
            for (int i = 0, j = sb.Length - 1; i < j; i++, j--)
            {
                char t = sb[i];
                sb[i] = sb[j];
                sb[j] = t;
            }

            return sb.ToString();

        }

        public static BigInteger ConvertFromString(string s, string digits)
        {
            BigInteger result;

            switch (Parse(s, digits, out result))
            {
                case ParseCode.FormatError:
                    throw new FormatException("Input string was not in the correct format.");
                case ParseCode.NullString:
                    throw new ArgumentNullException("s");
                case ParseCode.NullDigits:
                    throw new ArgumentNullException("digits");
                case ParseCode.InsufficientDigits:
                    throw new ArgumentOutOfRangeException("digits", "Expected string with at least two digits");
                case ParseCode.Overflow:
                    throw new OverflowException();
            }

            return result;
        }

        public static bool TryConvertFromString(string s, string digits, out BigInteger result)
        {
            return Parse(s, digits, out result) == ParseCode.Success;
        }

        private enum ParseCode
        {
            Success,
            NullString,
            NullDigits,
            InsufficientDigits,
            Overflow,
            FormatError,
        }

        private static ParseCode Parse(string s, string digits, out BigInteger result)
        {
            result = 0;

            if (s == null)
                return ParseCode.NullString;
            if (digits == null)
                return ParseCode.NullDigits;
            if (digits.Length < 2)
                return ParseCode.InsufficientDigits;

            // skip leading white space
            int i = 0;
            while (i < s.Length && Char.IsWhiteSpace(s[i]))
                ++i;
            if (i >= s.Length)
                return ParseCode.FormatError;

            // get the sign if it's there.
            BigInteger sign = 1;
            if (s[i] == '+')
                ++i;
            else if (s[i] == '-')
            {
                ++i;
                sign = -1;
            }

            // Make sure there's at least one digit
            if (i >= s.Length)
                return ParseCode.FormatError;


            // Parse the digits.
            while (i < s.Length)
            {
                int n = digits.IndexOf(s[i]);
                if (n < 0)
                    return ParseCode.FormatError;
                BigInteger oldResult = result;
                result = unchecked((result * digits.Length) + n);
                if (result < oldResult)
                    return ParseCode.Overflow;

                ++i;
            }

            // skip trailing white space
            while (i < s.Length && Char.IsWhiteSpace(s[i]))
                ++i;

            // and make sure there's nothing else.
            if (i < s.Length)
                return ParseCode.FormatError;

            if (sign < 0)
                result = -result;

            return ParseCode.Success;
        }
    }
}
