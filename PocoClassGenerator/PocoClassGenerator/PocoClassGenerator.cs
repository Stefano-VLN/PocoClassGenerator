using System;
using System.Collections.Generic;
using System.Data;
using System.Linq;
using System.Text;

namespace PocoClassGenerator
{
    [Flags]
    public enum GeneratorBehavior
    {
        Default = 0x0,
        View = 0x1,
        DapperContrib = 0x2,
        Comment = 0x4
    }

    public static partial class PocoClassGenerator
    {
        #region Property
        private static readonly Dictionary<Type, string> TypeAliases = new()
        {
            { typeof(int), "int" },
            { typeof(short), "short" },
            { typeof(byte), "byte" },
            { typeof(byte[]), "byte[]" },
            { typeof(long), "long" },
            { typeof(double), "double" },
            { typeof(decimal), "decimal" },
            { typeof(float), "float" },
            { typeof(bool), "bool" },
            { typeof(string), "string" }
        };

        private static readonly Dictionary<string, string> QuerySqls = new()
        {
            {"sqlconnection", "select * from [{0}] where 1=2" },
            {"sqlceserver", "select * from [{0}] where 1=2" },
            {"sqliteconnection", "select * from [{0}] where 1=2" },
            {"oracleconnection", "select * from \"{0}\" where 1=2" },
            {"mysqlconnection", "select * from `{0}` where 1=2" },
            {"npgsqlconnection", "select * from \"{0}\" where 1=2" }
        };

        private static readonly Dictionary<string, string> TableSchemaSqls = new()
        {
            {"sqlconnection", "select TABLE_NAME from INFORMATION_SCHEMA.TABLES where TABLE_TYPE = 'BASE TABLE'" },
            {"sqlceserver", "select TABLE_NAME from INFORMATION_SCHEMA.TABLES where TABLE_TYPE = 'BASE TABLE'" },
            {"sqliteconnection", "SELECT name FROM sqlite_master where type = 'table'" },
            {"oracleconnection", "select TABLE_NAME from USER_TABLES where table_name not in (select View_name from user_views)" },
            {"mysqlconnection", "select TABLE_NAME from information_schema.tables where table_type = 'BASE TABLE'" },
            {"npgsqlconnection", "select table_name from information_schema.tables where table_type = 'BASE TABLE'" }
        };

        private static readonly HashSet<Type> NullableTypes =
        [
            typeof(int),
            typeof(short),
            typeof(long),
            typeof(double),
            typeof(decimal),
            typeof(float),
            typeof(bool),
            typeof(DateTime)
        ];

        private static readonly string[] sourceArray = ["DataTypeName", "IsUnique", "IsKey", "IsAutoIncrement", "IsReadOnly"];
        #endregion

        public static string GenerateAllTables(this System.Data.Common.DbConnection connection, GeneratorBehavior generatorBehavior = GeneratorBehavior.Default)
        {
            if (connection.State != ConnectionState.Open) connection.Open();

            var connectionName = connection.GetType().Name.ToLower();
            var tables = new List<string>();
            var sql = generatorBehavior.HasFlag(GeneratorBehavior.View) ? TableSchemaSqls[connectionName].Split("where")[0] : TableSchemaSqls[connectionName];
            using (var command = connection.CreateCommand(sql))
            using (var reader = command.ExecuteReader())
            {
                while (reader.Read())
                    tables.Add(reader.GetString(0));
            }

            var sb = new StringBuilder();
            sb.AppendLine("namespace Models { ");
            tables.ForEach(table => sb.Append(connection.GenerateClass(
            string.Format(QuerySqls[connectionName], table), table, generatorBehavior: generatorBehavior
            )));
            sb.AppendLine("}");
            return sb.ToString();
        }

        public static string GenerateClass(this IDbConnection connection, string sql, GeneratorBehavior generatorBehavior)
        => connection.GenerateClass(sql, null, generatorBehavior);

        public static string GenerateClass(this IDbConnection connection, string sql, string className = null, GeneratorBehavior generatorBehavior = GeneratorBehavior.Default)
        {
            if (connection.State != ConnectionState.Open) connection.Open();

            var builder = new StringBuilder();

            //Get Table Name
            //Fix : [When View using CommandBehavior.KeyInfo will get duplicate columns �P Issue #8 �P shps951023/PocoClassGenerator](https://github.com/shps951023/PocoClassGenerator/issues/8 )
            var isFromMutiTables = false;
            using (var command = connection.CreateCommand(sql))
            using (var reader = command.ExecuteReader(CommandBehavior.KeyInfo | CommandBehavior.SingleRow))
            {
                var tables = reader.GetSchemaTable().Select().Select(s => s["BaseTableName"] as string).Distinct();
                var tableName = string.IsNullOrWhiteSpace(className) ? tables.First() ?? "Info" : className;

                isFromMutiTables = tables.Count() > 1;

                if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContrib) && !isFromMutiTables)
                    builder.AppendFormat(" [Dapper.Contrib.Extensions.Table(\"{0}\")]{1}", tableName, Environment.NewLine);
                builder.AppendFormat(" public class {0}{1}", tableName.Replace(" ", ""), Environment.NewLine);
                builder.AppendLine(" {");
            }

            //Get Columns
            var behavior = isFromMutiTables ? CommandBehavior.SchemaOnly | CommandBehavior.SingleRow : CommandBehavior.KeyInfo | CommandBehavior.SingleRow;

            using (var command = connection.CreateCommand(sql))
            using (var reader = command.ExecuteReader(behavior))
            {
                do
                {
                    var schema = reader.GetSchemaTable();
                    foreach (DataRow row in schema.Rows)
                    {
                        var type = (Type)row["DataType"];
                        var name = TypeAliases.TryGetValue(type, out string value) ? value : type.FullName;
                        var isNullable = (bool)row["AllowDBNull"] && NullableTypes.Contains(type);
                        var columnName = (string)row["ColumnName"];

                        if (generatorBehavior.HasFlag(GeneratorBehavior.Comment) && !isFromMutiTables)
                        {
                            var comments = sourceArray.Select(s =>
                            {
                                if (row[s] is bool rowBool && rowBool)
                                    return s;
                                if (row[s] is string rowString && !string.IsNullOrWhiteSpace(rowString))
                                    return string.Format(" {0} : {1} ", s, row[s]);
                                return null;
                            }).Where(w => w != null).ToArray();
                            var sComment = string.Join(" , ", comments);

                            builder.AppendFormat(" /// <summary>{0}</summary>{1}", sComment, Environment.NewLine);
                        }

                        if (generatorBehavior.HasFlag(GeneratorBehavior.DapperContrib) && !isFromMutiTables)
                        {
                            var isKey = (bool)row["IsKey"];
                            var isAutoIncrement = (bool)row["IsAutoIncrement"];
                            if (isKey && isAutoIncrement)
                                builder.AppendLine(" [Dapper.Contrib.Extensions.Key]");
                            if (isKey && !isAutoIncrement)
                                builder.AppendLine(" [Dapper.Contrib.Extensions.ExplicitKey]");
                            if (!isKey && isAutoIncrement)
                                builder.AppendLine(" [Dapper.Contrib.Extensions.Computed]");
                        }

                        builder.AppendLine(string.Format("    public {0}{1} {2} {{ get; set; }}{3}", name, isNullable ? "?" : string.Empty, columnName, name == "string" && !isNullable ? " = string.Empty;" : string.Empty));
                    }

                    builder.AppendLine(" }");
                    builder.AppendLine();
                } while (reader.NextResult());

                return builder.ToString();
            }
        }

        #region Private
        //private static string[] Split(this string text, string splitText) => text.Split(new[] { splitText }, StringSplitOptions.None);
        private static IDbCommand CreateCommand(this IDbConnection connection, string sql)
        {
            var cmd = connection.CreateCommand();
            cmd.CommandText = sql;
            return cmd;
        }
        #endregion
    }
}