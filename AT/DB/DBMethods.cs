using AT.DBObjects;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.SQLite;
using System.IO;
using AT.DBEnums;
using AT.DataObjects;

namespace AT
{
    public class DBMethods
    {
        
        public static string cs = "Data Source={0};Version=3;Synchronous=OFF;Journal Mode=OFF;Page Size=16384;Temp Store=Memory;Locking Mode=Exclusive;"; //

        /// <summary>
        /// Keys are the folder (if it's in a folder) plus the filename without ".db".
        /// </summary>
        public static Dictionary<string, SQLiteConnection> cons = new Dictionary<string, SQLiteConnection>();


        public static void Init()
        {

            // normal dbs
            DirectoryInfo stockDir = new DirectoryInfo(Global.Constants.DBPath);

            FileInfo[] stockFiles = stockDir.GetFiles("*.db");
            foreach (FileInfo file in stockFiles)
            {
                string key = file.Name.Substring(0, file.Name.Length - 3); // cuts of ".db"
                SQLiteConnection con = new SQLiteConnection(String.Format(cs, Global.Constants.DBPath + file.Name));
                con.Open();
                cons.Add(key, con);
            }


            // trading stats
            DirectoryInfo tradingStatsDir = new DirectoryInfo(Global.Constants.TradingStatsDBPath);
            FileInfo[] tradingStatsFiles = tradingStatsDir.GetFiles("*.db");
            foreach (FileInfo file in tradingStatsFiles)
            {
                string key = Global.Constants.TradingStatsDBFolder + "\\" + file.Name.Substring(0, file.Name.Length - 3); // cuts of ".db"
                SQLiteConnection con = new SQLiteConnection(String.Format(cs, Global.Constants.TradingStatsDBPath + file.Name));
                con.Open();
                cons.Add(key, con);
            }

            // ticks
            DirectoryInfo stockTicksDir = new DirectoryInfo(Global.Constants.StockTicksDBPath);
            FileInfo[] stockTicksFiles = stockTicksDir.GetFiles("*.db");
            foreach (FileInfo file in stockTicksFiles)
            {
                string key = Global.Constants.StockTicksDBFolder + "\\" + file.Name.Substring(0, file.Name.Length - 3); // cuts of ".db"
                SQLiteConnection con = new SQLiteConnection(String.Format(cs, Global.Constants.StockTicksDBPath + file.Name));
                con.Open();
                cons.Add(key, con);
            }

            
        }

        public static List<string> GetConnectionKeys()
        {
            List<string> result = new List<string>();

            foreach (KeyValuePair<string, SQLiteConnection> kv in cons)
            {
                result.Add(kv.Key);
            }

            return result;
        }

        public static void Vacuum(string conKey)
        {
            string sql = "VACUUM";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
            {
                cmd1.ExecuteNonQuery();
            }
        }

        public static List<string> GetAllWatchedSymbols()
        {
            List<string> result = new List<string>();

            string sql = "";

            sql = "SELECT Symbol FROM Symbols WHERE Watch = 1 ORDER BY Symbol ASC";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons["symbols"]))
            {
                SQLiteDataReader r = cmd1.ExecuteReader();
                while (r.Read())
                {
                    result.Add(r["Symbol"].ToString());
                }
            }

            return result;
        }


        public static void MarkSchedulerItem(string Key, string Id)
        {
            string sql = "INSERT OR IGNORE INTO Items (Key, Id) VALUES ('" + Key + "','" + Id + "')";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons["completed-scheduled-items"]))
            {
                cmd1.ExecuteNonQuery();
            }
        }


        public static List<FD> GetTestingDays()
        {
            List<FD> result = new List<FD>();

            string sql = "SELECT Day FROM Days";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons["testing-days"]))
            {
                SQLiteDataReader r = cmd1.ExecuteReader();
                while (r.Read())
                {
                    string[] s = r["Day"].ToString().Split('-');

                    int year = int.Parse(s[0]);
                    int month = int.Parse(s[1]);
                    int day = int.Parse(s[2]);

                    result.Add(new FD(year, month, day));
                }
            }

            return result;
        }

        public static List<string> GetSchedulerItems(string Key)
        {
            List<string> result = new List<string>();

            string sql = "";

            sql = "SELECT Id FROM Items WHERE Key = '" + Key + "'";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons["completed-scheduled-items"]))
            {
                SQLiteDataReader r = cmd1.ExecuteReader();
                while (r.Read())
                {
                    result.Add(r["Id"].ToString());
                }
            }

            return result;
        }
        public static List<string> GetAllSymbols()
        {
            List<string> result = new List<string>();

            string sql = "";

            sql = "SELECT Symbol FROM Symbols ORDER BY Symbol ASC";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons["symbols"]))
            {
                SQLiteDataReader r = cmd1.ExecuteReader();
                while (r.Read())
                {
                    result.Add(r["Symbol"].ToString());
                }
            }

            return result;
        }

        public static bool SymbolHasDayData(string symbol, int month, int day, int year)
        {
            bool result = false;

            string date = year + "-" + month + "-" + day;

            symbol = symbol.ToLower().Trim();

            string sql = "SELECT * FROM Tracker WHERE Symbol = '" + symbol + "' AND Date = '" + date + "'";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons["data-tracker"]))
            {
                cmd1.Parameters.Add("Symbol", DbType.String).Value = symbol;
                object o = cmd1.ExecuteScalar();
                result = (o != null);
            }

            return result;
        }

        /// <summary>
        /// Day should be formatted "year-month-day"
        /// These are the days for TESTING the final algo
        /// </summary>
        /// <param name="day"></param>
        /// <returns></returns>
        public static int InsertTestingDay(string day)
        {
            int result = 0;

            string sql = "INSERT OR IGNORE INTO Days (Day) VALUES (@Day)";

            string conKey = "testing-days";

            using (SQLiteTransaction tr = cons[conKey].BeginTransaction())
            {
                using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd.Parameters.Add("Day", DbType.String).Value = day;
                    result = cmd.ExecuteNonQuery();
                }
                tr.Commit();
            }

            return result;
        }

        public static List<string> GetDaysThatHaveDataBySymbol(string symbol)
        {
            List<string> result = new List<string>();

            string sql = "SELECT DISTINCT Date FROM Tracker WHERE Symbol = '" + symbol + "'";

            string conKey = "data-tracker";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
            {
                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    result.Add(r["Date"].ToString());
                }
            }

            return result;
        }



        public static Dictionary<string, List<FD>> GetDaysThatHaveDataEachSymbol()
        {
            Dictionary<string, List<FD>> result = new Dictionary<string, List<FD>>();

            string sql = "SELECT Symbol, Date FROM Tracker";

            string conKey = "data-tracker";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
            {
                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string symbol = r["Symbol"].ToString();
                    string[] s = r["Date"].ToString().Split('-');
                    int year = int.Parse(s[0]);
                    int month = int.Parse(s[1]);
                    int day = int.Parse(s[2]);
                    if (!result.ContainsKey(symbol))
                    {
                        result.Add(symbol, new List<FD>());
                    }
                    result[symbol].Add(new FD(year, month, day));
                }
            }

            foreach (List<FD> item in result.Values)
            {
                item.Sort();
            }


            return result;
        }



        public static void RefreshDaysTracker(List<FD> days, string table = "NoData")
        {

            string conKey = "data-tracker";

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {

                using (SQLiteCommand cmd0 = new SQLiteCommand("DELETE FROM " + table, cons[conKey]))
                {
                    cmd0.ExecuteNonQuery();
                }

                string sql = "INSERT INTO " + table + " (Date) VALUES (@Date)";

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.Parameters.Add("Date", DbType.String).Value = "";
                    
                    for(int n = 0; n < days.Count; n++)
                    {
                        cmd1.Parameters["Date"].Value = days[n].DT.Year + "-" + days[n].DT.Month + "-" + days[n].DT.Day;
                        cmd1.ExecuteNonQuery();
                    }
                    
                }

                t.Commit();
            }
        }

        /// <summary>
        /// NoData or Holidays
        /// </summary>
        /// <param name="table">NoData or Holidays</param>
        /// <returns></returns>
        public static List<FD> GetDaysFromTracker(string table = "NoData")
        {
            List<FD> result = new List<FD>();

            string sql = "SELECT Date FROM " + table;

            string conKey = "data-tracker";

            using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
            {
                SQLiteDataReader r = cmd.ExecuteReader();
                while (r.Read())
                {
                    string[] s = r["Date"].ToString().Split('-');
                    int year = int.Parse(s[0]);
                    int month = int.Parse(s[1]);
                    int day = int.Parse(s[2]);
                    result.Add(new FD(year, month, day));
                }
            }

            result.Sort();

            return result;
        }


        public static int MarkSymbolHasDayData(string symbol, int month, int day, int year)
        {
            string date = year + "-" + month + "-" + day;

            int result = 0;

            string sql = "INSERT OR IGNORE INTO Tracker (Symbol, Date) VALUES (@Symbol, @Date)";

            string conKey = "data-tracker";

            using (SQLiteTransaction tr = cons[conKey].BeginTransaction())
            {
                using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd.Parameters.Add("Symbol", DbType.String).Value = symbol;
                    cmd.Parameters.Add("Date", DbType.String).Value = date;
                    result = cmd.ExecuteNonQuery();
                }
                tr.Commit();
            }

            return result;
        }

        public static bool SymbolExists(string symbol)
        {
            bool result = false;

            symbol = symbol.ToLower().Trim();

            string sql = "SELECT * FROM Symbols WHERE Symbol = @Symbol";

            string conKey = "symbols";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
            {
                cmd1.Parameters.Add("Symbol", DbType.String).Value = symbol;
                object o = cmd1.ExecuteScalar();
                result = (o != null);
            }

            return result;
        }


        /// <summary>
        /// Add a column to a table
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="columnName"></param>
        /// <param name="columnType"></param>
        /// <param name="type">Can be "t", "q", "s_t", "s_q"</param>
        public static void AddColumn(string conKey, string tableName, string columnName, string columnType)
        {

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                string sql = "ALTER TABLE " + tableName + " ADD COLUMN " + columnName + " " + columnType;

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }

        private static List<Column> GetColumns(string conKey, string tableName)
        {
            List<Column> result = new List<Column>();

            using (SQLiteCommand cmd1 = new SQLiteCommand("PRAGMA table_info(" + tableName + ");", cons[conKey]))
            {
                SQLiteDataReader r = cmd1.ExecuteReader();
                while (r.Read())
                {

                    Column col = new Column(r["name"].ToString(), r["type"].ToString(), r["notnull"].ToString(), r["dflt_value"] == null ? null : r["dflt_value"].ToString(), r["pk"].ToString());

                    result.Add(col);
                }
            }

            return result;
        }

        public static void CreateTable(string conKey, string tableName, List<Column> columns)
        {
            string sql = DBUtils.GetTableCreationSQL(columns, tableName);

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }

        public static void ModifyColumn(string conKey, string tableName, string columnName, DataTypes convertTo)
        {
            // get existing columns data
            List<Column> columns = GetColumns(conKey, tableName);

            // create new column data with columnName to the convertTo type
            Column col = columns.Find(c => c.Name == columnName);
            col.Type = convertTo;

            string tempTable = tableName + "_temp";

            // create new temp table with new column data: CREATE TEMPORARY TABLE tableName_temp(NewColumnData);
            CreateTable(conKey, tempTable, columns);

            // insert from newtable select columns from oldtable
            TableToTableCopy(conKey, tableName, tempTable, columns);

            DropTable(conKey, tableName);

            RenameTable(conKey, tempTable, tableName);

            // vacuum it because the big tables can double in size
            Vacuum(conKey);
        }


        public static void TableToTableCopy(string conKey, string tableNameSrc, string tableNameDest, List<Column> columns)
        {
            string columnsPart = DBUtils.GetColumnNamesCommaSeperated(columns);

            string sql = "INSERT OR IGNORE INTO " + tableNameDest + " SELECT " + columnsPart + " FROM " + tableNameSrc;
            
            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
            
        }

        private static void DropTable(string conKey, string tableName)
        {
            string sql = "DROP TABLE " + tableName;

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }

        private static void RenameTable(string conKey, string fromTableName, string toTableName)
        {
            string sql = "ALTER TABLE " + fromTableName + " RENAME TO " + toTableName;

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }

        /// <summary>
        /// Remove a column to a table
        /// </summary>
        /// /*
        public static void RemoveColumn(string conKey, string tableName, string columnName)
        {
            List<Column> oldColumns = GetColumns(conKey, tableName);
            List<Column> newColumns = new List<Column>();

            newColumns.AddRange(oldColumns);
            newColumns.RemoveAll(c => c.Name == columnName);

            string tempTable = tableName + "_temp";

            // create the table without columnName
            CreateTable(conKey, tempTable, newColumns);

            // insert from newtable select columns from oldtable
            TableToTableCopy(conKey, tableName, tempTable, newColumns);

            DropTable(conKey, tableName);

            RenameTable(conKey, tempTable, tableName);

            // vacuum it because the big tables can double in size
            Vacuum(conKey);

        }
            

        /// <summary>
        /// Simple file check of the .db file.
        /// </summary>
        public static bool TradesDBExists(string symbol)
        {
            bool result = false;

            symbol = symbol.ToLower().Trim();

            result = File.Exists(Global.Constants.StockTicksDBPath + "t_" + symbol + ".db");

            return result;
        }

        /// <summary>
        /// Simple file check of the .db file.
        /// </summary>
        public static bool TradingStatsDBExists(string symbol)
        {
            bool result = false;

            symbol = symbol.ToLower().Trim();

            result = File.Exists(Global.Constants.TradingStatsDBPath + symbol + ".db");

            return result;
        }

        /// <summary>
        /// Simple file check of the .db file.
        /// </summary>
        public static bool QuotesDBExists(string symbol)
        {
            bool result = false;

            symbol = symbol.ToLower().Trim();

            result = File.Exists(Global.Constants.StockTicksDBPath + "q_" + symbol + ".db");

            return result;
        }

        /// <summary>
        /// Creates a new quotes DB with table without checking if one already exists. 
        /// </summary>
        public static void CreateQuotesDB(string symbol)
        {
            CreateDataDB(symbol, "q");
        }

        /// <summary>
        /// Generic Data DB creation.
        /// </summary>
        /// <param name="symbol"></param>
        /// <param name="type">Can be "t" or "q"</param>
        /// <param name="streamed"></param>
        public static void CreateDataDB(string symbol, string type)
        {

            string sql = "";
            string prefix = "";

            if(type == "t")
            {
                sql = "CREATE TABLE \"Trades\" (\"Timestamp\" INTEGER NOT NULL UNIQUE,\"Price\" REAL NOT NULL,\"Volume\" INTEGER NOT NULL,\"ExchangeId\" INTEGER NOT NULL,\"Conditions\" TEXT,\"Tape\" INTEGER NOT NULL, PRIMARY KEY(\"Timestamp\"));";
                prefix = "t_";
            }
            else if (type == "q")
            {
                sql = "CREATE TABLE \"Quotes\" (\"Timestamp\" INTEGER NOT NULL UNIQUE,\"Conditions\" TEXT,\"BidPrice\" REAL NOT NULL,\"BidExchangeId\" INTEGER NOT NULL,\"BidSize\" INTEGER NOT NULL,\"AskPrice\" REAL NOT NULL,\"AskExchangeId\" INTEGER NOT NULL,\"AskSize\" INTEGER NOT NULL,\"Tape\" INTEGER NOT NULL, PRIMARY KEY(\"Timestamp\"));";
                prefix = "q_";
            }

            string dbPath = Global.Constants.StockTicksDBPath + prefix + symbol;

            string conKey = Global.Constants.StockTicksDBFolder + "\\" + prefix + symbol;

            SQLiteConnection.CreateFile(dbPath + ".db");

            SQLiteConnection con = new SQLiteConnection(String.Format(cs, dbPath + ".db"));

            con.Open();

            cons.Add(conKey, con);

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }
        public static void CreateTradingStatsDB(string symbol)
        {
            string sql = "CREATE TABLE \"Stats\" (\"StrategyId\" TEXT NOT NULL, \"OpenTimestamp\" INTEGER NOT NULL, \"CloseTimestamp\" INTEGER NOT NULL, \"Data\" BLOB NOT NULL);";

            string dbPath = Global.Constants.TradingStatsDBPath + symbol;

            string conKey = Global.Constants.TradingStatsDBFolder + "\\" + symbol;

            SQLiteConnection.CreateFile(dbPath + ".db");

            SQLiteConnection con = new SQLiteConnection(String.Format(cs, dbPath + ".db"));

            con.Open();

            cons.Add(conKey, con);

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }
        /// <summary>
        /// Creates a new trades DB with table without checking if one already exists. 
        /// </summary>
        public static void CreateTradesDB(string symbol)
        {
            CreateDataDB(symbol, "t");
        }

        /// <summary>
        /// Get list of quotes for a symbol.
        /// </summary>
        /// <param name="start">Unix nanosecond time. Inclusive.</param>
        /// <param name="end">Unix nanosecond time. NOT Inclusive.</param>
        /// <returns>Sorted (timestamp asc) list of trades</returns>
        public static List<Quote> GetQuotesBySymbol(string symbol, long start = -1, long end = -1)
        {
            List<Quote> result = new List<Quote>();

            string sqlPart = " ";

            if (start != -1 && end != -1)
            {
                sqlPart = " WHERE Timestamp >= " + start + " AND Timestamp < " + end + " ";
            }
            else if (start != -1)
            {
                sqlPart = " WHERE Timestamp >= " + start + " ";
            }
            else if (end != -1)
            {
                sqlPart = " WHERE Timestamp < " + end + " ";
            }


            string conKey = Global.Constants.StockTicksDBFolder + "\\q_" + symbol;

            string sql = "SELECT Timestamp, Conditions, BidPrice, BidExchangeId, BidSize, AskPrice, AskExchangeId, AskSize, Tape FROM Quotes" + sqlPart + "ORDER BY Timestamp ASC";

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
            {

                SQLiteDataReader r = cmd1.ExecuteReader();
                while (r.Read())
                {
                    Quote q = new Quote();
                    q.Timestamp = r.GetInt64(0);
                    q.Conditions = (string)r[1];
                    q.BidPrice = r.GetDecimal(2);
                    q.BidExchangeId = r.GetInt32(3);
                    q.BidSize = r.GetInt32(4);
                    q.AskPrice = r.GetDecimal(5);
                    q.AskExchangeId = r.GetInt32(6);
                    q.AskSize = r.GetInt32(7);
                    q.Tape = r.GetInt32(8);
                    result.Add(q);
                }
            }

            return result;
        }


        /// <summary>
        /// Get list of trades for a symbol.
        /// </summary>
        /// <param name="start">Unix nanosecond time. Inclusive.</param>
        /// <param name="end">Unix nanosecond time. NOT Inclusive.</param>
        /// <returns>Sorted (timestamp asc) list of trades</returns>
        public static List<Trade> GetTradesBySymbol(string symbol, long start = -1, long end = -1)
        {
            List<Trade> result = new List<Trade>();

            string sqlPart = " ";

            if (start != -1 && end != -1)
            {
                sqlPart = " WHERE Timestamp >= " + start + " AND Timestamp < " + end + " ";
            }
            else if(start != -1)
            {
                sqlPart = " WHERE Timestamp >= " + start + " ";
            }
            else if (end != -1)
            {
                sqlPart = " WHERE Timestamp < " + end + " ";
            }

            string sql = "SELECT Timestamp, Price, Volume, ExchangeId, Conditions, Tape FROM Trades" + sqlPart + "ORDER BY Timestamp ASC";

            string conKey = Global.Constants.StockTicksDBFolder + "\\t_" + symbol;

            using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
            {
                SQLiteDataReader r = cmd1.ExecuteReader();

                while (r.Read())
                {
                    Trade t = new Trade();

                    t.Timestamp = r.GetInt64(0);
                    t.Price = r.GetDecimal(1);
                    t.Volume = r.GetInt32(2);
                    t.ExchangeId = r.GetInt32(3);
                    t.Conditions = (string)r[4];
                    t.Tape = r.GetInt32(5);

                    result.Add(t);
                }
            }

            return result;
        }


        public static void InsertStrategyName(string id, string strategyName, long timestamp)
        {

            string conKey = "strategies";

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                string sql = "INSERT OR IGNORE INTO Strategies (Id, Name, Timestamp) VALUES ('" + id + "', '" + strategyName + "', " + timestamp + ")";

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }



        /// <summary>
        /// Simple insert of a symbol into symbols.db. Watch is false by default.
        /// </summary>
        /// <param name="symbol"></param>
        public static void InsertSymbol(string symbol)
        {
            symbol = symbol.ToLower().Trim();

            string conKey = "symbols";

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                string sql = "INSERT INTO Symbols (Symbol, Watch) VALUES (@Symbol, @Watch)";

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    cmd1.Parameters.Add("Symbol", DbType.String).Value = symbol;
                    cmd1.Parameters.Add("Watch", DbType.Boolean).Value = false;
                    cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }
        }

        /// <summary>
        /// Insert thousands of trades at a time.
        /// </summary>
        /// <param name="conflictAction">IGNORE or REPLACE</param>
        /// <returns></returns>
        public static int BulkInsertTrades(List<Trade> trades, string symbol, string conflictAction = "IGNORE")
        {
            int result = 0;

            string sql = "INSERT OR " + conflictAction + " INTO Trades (Timestamp, Price, Volume, ExchangeId, Conditions, Tape) VALUES (@Timestamp, @Price, @Volume, @ExchangeId, @Conditions, @Tape)";

            string conKey = Global.Constants.StockTicksDBFolder + "\\t_" + symbol;

            using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
            {
                using (SQLiteTransaction tr = cons[conKey].BeginTransaction())
                {
                    cmd.Parameters.Add("Timestamp", DbType.Int64).Value = 0;
                    cmd.Parameters.Add("Price", DbType.Decimal).Value = 0;
                    cmd.Parameters.Add("Volume", DbType.Int32).Value = 0;
                    cmd.Parameters.Add("ExchangeId", DbType.Int32).Value = 0;
                    cmd.Parameters.Add("Conditions", DbType.String).Value = 0;
                    cmd.Parameters.Add("Tape", DbType.Int32).Value = 0;

                    for (int n = 0; n < trades.Count; n++)
                    {
                        cmd.Parameters["Timestamp"].Value = trades[n].Timestamp;
                        cmd.Parameters["Price"].Value = trades[n].Price;
                        cmd.Parameters["Volume"].Value = trades[n].Volume;
                        cmd.Parameters["ExchangeId"].Value = trades[n].ExchangeId;
                        cmd.Parameters["Conditions"].Value = trades[n].Conditions;
                        cmd.Parameters["Tape"].Value = trades[n].Tape;

                        result += cmd.ExecuteNonQuery();
                    }

                    tr.Commit();
                }
            }

            return result;
        }


        /// <summary>
        /// Insert thousands of quotes at a time.
        /// </summary>
        /// <param name="conflictAction">IGNORE or REPLACE</param>
        public static int BulkInsertQuotes(List<Quote> quotes, string symbol, string conflictAction = "IGNORE")
        {
            int result = 0;

            string sql = "INSERT OR " + conflictAction + " INTO Quotes (Timestamp, Conditions, BidPrice, BidExchangeId, BidSize, AskPrice, AskExchangeId, AskSize, Tape) VALUES (@Timestamp, @Conditions, @BidPrice, @BidExchangeId, @BidSize, @AskPrice, @AskExchangeId, @AskSize, @Tape)";

            string conKey = Global.Constants.StockTicksDBFolder + "\\q_" + symbol;

            using (SQLiteCommand cmd = new SQLiteCommand(sql, cons[conKey]))
            {
                using (SQLiteTransaction tr = cons[conKey].BeginTransaction())
                {
                    cmd.Parameters.Add("Timestamp", DbType.Int64).Value = 0;
                    cmd.Parameters.Add("Conditions", DbType.String).Value = 0;

                    cmd.Parameters.Add("BidPrice", DbType.Decimal).Value = 0;
                    cmd.Parameters.Add("BidExchangeId", DbType.Int32).Value = 0;
                    cmd.Parameters.Add("BidSize", DbType.Int32).Value = 0;

                    cmd.Parameters.Add("AskPrice", DbType.Decimal).Value = 0;
                    cmd.Parameters.Add("AskExchangeId", DbType.Int32).Value = 0;
                    cmd.Parameters.Add("AskSize", DbType.Int32).Value = 0;

                    cmd.Parameters.Add("Tape", DbType.Int32).Value = 0;

                    for (int n = 0; n < quotes.Count; n++)
                    {
                        cmd.Parameters["Timestamp"].Value = quotes[n].Timestamp;
                        cmd.Parameters["Conditions"].Value = quotes[n].Conditions;

                        cmd.Parameters["BidPrice"].Value = quotes[n].BidPrice;
                        cmd.Parameters["BidExchangeId"].Value = quotes[n].BidExchangeId;
                        cmd.Parameters["BidSize"].Value = quotes[n].BidSize;

                        cmd.Parameters["AskPrice"].Value = quotes[n].AskPrice;
                        cmd.Parameters["AskExchangeId"].Value = quotes[n].AskExchangeId;
                        cmd.Parameters["AskSize"].Value = quotes[n].AskSize;

                        cmd.Parameters["Tape"].Value = quotes[n].Tape;

                        result += cmd.ExecuteNonQuery();
                    }

                    tr.Commit();
                }
            }

            return result;
        }


        public static void ReplaceWatchList(List<string> symbols)
        {
            string conKey = "symbols";

            using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Symbols SET Watch = 0", cons[conKey]))
            {
                using (SQLiteTransaction tr = cons[conKey].BeginTransaction())
                {
                    cmd.ExecuteNonQuery();
                    tr.Commit();
                }
            }

            for(int n = 0; n < symbols.Count; n++)
            {
                string symbol = symbols[n].Trim().ToLower();

                using (SQLiteCommand cmd = new SQLiteCommand("UPDATE Symbols SET Watch = 1 WHERE Symbol = '" + symbol + "'", cons[conKey]))
                {
                    using (SQLiteTransaction tr = cons[conKey].BeginTransaction())
                    {
                        cmd.ExecuteNonQuery();
                        tr.Commit();
                    }
                }
            }
        }

        public static int DeleteSymbolFromSymbols(string symbol)
        {
            int result = 0;
                
            symbol = symbol.ToLower().Trim();

            string conKey = "symbols";

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                string sql = "DELETE FROM Symbols WHERE Symbol = '" + symbol + "'";

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    result = cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }

            return result;
        }

        public static int DeleteSymbolFromDataTracker(string symbol)
        {
            int result = 0;

            symbol = symbol.ToLower().Trim();

            string conKey = "data-tracker";

            using (SQLiteTransaction t = cons[conKey].BeginTransaction())
            {
                string sql = "DELETE FROM Tracker WHERE Symbol = '" + symbol + "'";

                using (SQLiteCommand cmd1 = new SQLiteCommand(sql, cons[conKey]))
                {
                    result = cmd1.ExecuteNonQuery();
                }

                t.Commit();
            }

            return result;
        }
    }
}
