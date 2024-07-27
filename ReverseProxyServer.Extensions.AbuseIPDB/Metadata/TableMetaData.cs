using System.Reflection;

namespace ReverseProxyServer.Data.Metadata
{
    public class TableMetaData(string tableName, string primaryKey, List<PropertyInfo> columns)
    {
        public string TableName { get; set; } = tableName;
        public string PrimaryKey { get; set; } = primaryKey;
        public List<PropertyInfo> Columns { get; set; } = columns;
    }
}
