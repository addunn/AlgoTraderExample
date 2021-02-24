using AT.DBObjects;
using System.Collections.Generic;
using System.Data;
using System.Linq;

namespace AT
{
    public class DBUtils
    {

        public static string GetColumnNamesCommaSeperated(List<Column> columns)
        {
            List<string> columnNames = columns.Select(c => c.Name).ToList();

            return UC.ListToString(columnNames, ", ");
        }

        /// <summary>
        /// Unique constraints don't work
        /// </summary>
        /// <param name="columns"></param>
        /// <param name="tableName"></param>
        /// <returns></returns>
        public static string GetTableCreationSQL(List<Column> columns, string tableName)
        {
            /*
                CREATE TABLE IF NOT EXISTS "Trades" 
                (
	                "Timestamp" INTEGER NOT NULL UNIQUE,
	                "Price" REAL NOT NULL,
	                "Volume" INTEGER NOT NULL,
	                "ExchangeId" INTEGER NOT NULL,
	                "Conditions" TEXT,
	                "Tape" INTEGER NOT NULL,
	                PRIMARY KEY("Timestamp") // PRIMARY KEY("col1","col2")
                );
            */

            string result = "CREATE TABLE IF NOT EXISTS \"" + tableName + "\" ";

            result += "(";

            List<string> primaryKeysNames = columns.Where(c => c.PrimaryKey != 0).OrderBy(c => c.PrimaryKey).Select(p => p.Name).ToList();

            // columns
            for (int n = 0; n < columns.Count; n++)
            {
                result += "\"" + columns[n].Name + "\" " + columns[n].Type.ToString();
                
                if(primaryKeysNames.Count == 1 && primaryKeysNames[0] == columns[n].Name)
                {
                    result += " PRIMARY KEY, ";
                }
                else
                {
                    result += (columns[n].NotNull ? " NOT NULL" : "") + ", ";
                }
            }

            if(primaryKeysNames.Count == 1)
            {
                result = result.Substring(0, result.Length - 2);
            }

            //primary key
            if (primaryKeysNames.Count > 1)
            {
                result += "PRIMARY KEY (\"" + UC.ListToString(primaryKeysNames, "\",\"") + "\")";
            }

            result += ");";

            return result;
        }
    }
}
