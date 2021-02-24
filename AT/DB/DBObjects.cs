using System;


using AT.DBEnums;

namespace AT.DBObjects
{
    
    public class Column
    {
        public DataTypes Type = DataTypes.UNKNOWN;

        public string Name = null;
        public string DefaultValue = null;
        public int PrimaryKey = 0; // 0 when not pk, 1 when first, 2 when second, etc
        public bool NotNull = true;

        public Column(string name, string type, string notNull, string defaultValue, string primaryKey)
        {
            Name = name;

            Type = (DataTypes)Enum.Parse(typeof(DataTypes), type, true);
            
            NotNull = (notNull == "0" ? false : true);

            DefaultValue = defaultValue;

            PrimaryKey = int.Parse(primaryKey);
        }

    }
    
}
