using ReverseProxyServer.Data.Metadata;
using System.Collections.Concurrent;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Reflection;

public static class MetadataCache
{
    private static readonly ConcurrentDictionary<Type, TableMetaData> Cache = new();
    public static TableMetaData GetTableMetadata<T>()
    {
        var type = typeof(T);
        if (Cache.TryGetValue(type, out var metadata))
            return metadata;

        List<PropertyInfo> columns = GetColumns(type);
        metadata = new(GetTableName(type), GetPrimaryKey(type, columns), columns);

        Cache[type] = metadata;
        return metadata;
    }

    private static string GetTableName(Type type)
    {
        var tableAttribute = type.GetCustomAttribute<TableAttribute>();
        return tableAttribute != null ? tableAttribute.Name : throw new Exception($"Class {type.Name} must have a declared [Table] attribute");
    }
    private static string GetPrimaryKey(Type type, List<PropertyInfo> props)
    {
        var keyProperty0 = type.GetCustomAttribute<KeyAttribute>();
        var keyProperty = props.FirstOrDefault(p => p.GetCustomAttribute<KeyAttribute>() != null);
        return keyProperty != null ? keyProperty.Name : throw new Exception($"Class {type.Name} must have a declared [Key] attribute");
    }
    private static List<PropertyInfo> GetColumns(Type type)
    {
        return [.. type.GetProperties()];
    }

}


