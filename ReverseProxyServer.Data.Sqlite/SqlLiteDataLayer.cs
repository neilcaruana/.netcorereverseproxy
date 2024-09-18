using Microsoft.Data.Sqlite;
using System.Reflection;

namespace ReverseProxyServer.Data.Sqlite
{
    public class SqlLiteDataLayer
    {
        private string connectionString = "Data Source=[[databasePath]];";
        public SqlLiteDataLayer(string databasePath)
        {
            if (string.IsNullOrWhiteSpace(databasePath))
                throw new Exception("Database path cannot be blank");

            connectionString = connectionString.Replace("[[databasePath]]", databasePath);
        }
        public async Task ExecuteScript(string script)
        {
            if (string.IsNullOrWhiteSpace(script))
                throw new Exception("Script cannot be blank");

            using SqliteConnection connection = new(connectionString);
            await connection.OpenAsync();

            using SqliteCommand command = connection.CreateCommand();
            command.CommandText = script;
            command.ExecuteNonQuery();
        }
        public async Task<SqliteConnection> GetOpenConnection()
        {
            var connection = new SqliteConnection(connectionString);
            await connection.OpenAsync();
            return connection;
        }
        public virtual SqliteCommand InjectCommandWithParameters(SqliteCommand command, string query, List<PropertyInfo> properties, object? entity)
        {
            if (entity == null)
                throw new Exception("Entity cannot be null when updating DB");

            foreach (var property in properties)
            {
                command.Parameters.AddWithValue("@" + property.Name, property.GetValue(entity) ?? DBNull.Value);
            }
            return command;
        }
        public virtual T MapSqliteReaderToEntity<T>(SqliteDataReader reader, List<PropertyInfo> props) where T : new()
        {
            var entity = new T();
            foreach (var property in props)
            {
                if (property.PropertyType == typeof(DateTime))
                    property.SetValue(entity, Convert.ToDateTime(reader[property.Name]));
                else if (property.PropertyType == typeof(DateTime?))
                {
                    object nullableDateTime = reader[property.Name];
                    if (nullableDateTime != DBNull.Value)
                        property.SetValue(entity, Convert.ToDateTime(nullableDateTime));
                }
                else if (!reader.IsDBNull(reader.GetOrdinal(property.Name)))
                    property.SetValue(entity, reader[property.Name]);
            }

            return entity;
        }
    }
}
