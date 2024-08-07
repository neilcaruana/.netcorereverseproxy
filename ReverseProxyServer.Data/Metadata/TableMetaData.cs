﻿using System.Reflection;

public class TableMetaData(string tableName, string primaryKey, PropertyInfo primaryKeyProp, bool isPrimaryKeyIdentity, 
                           string insertQuery, string updateQuery, string selectQuery, string deleteQuery, 
                           List<PropertyInfo> columns, Dictionary<PropertyInfo, bool> databaseAutoGeneratedColumns)
{
    public string TableName { get; set; } = tableName;
    public string PrimaryKey { get; set; } = primaryKey;
    public PropertyInfo PrimaryKeyProp { get; set; } = primaryKeyProp;
    public bool IsPrimaryKeyIdentity { get; set; } = isPrimaryKeyIdentity;
    public List<PropertyInfo> Columns { get; set; } = columns;
    public Dictionary<PropertyInfo, bool> DatabaseAutoGeneratedColumns { get; set; } = databaseAutoGeneratedColumns;
    public string InsertQuery { get; set; } = insertQuery;
    public string UpdateQuery { get; set; } = updateQuery;
    public string SelectQuery { get; set; } = selectQuery;
    public string DeleteQuery { get; set; } = deleteQuery;
}
