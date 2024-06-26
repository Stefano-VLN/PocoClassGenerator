﻿using System;
using System.Collections.Generic;
using System.Data;
using System.Text;

namespace PocoClassGenerator
{
    public static partial class DataTablePocoClassGenerator
    {
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

        public static string GetTypeAliases(Type type)
        {
            if (TypeAliases.TryGetValue(type, out string typename))
                return typename;
            else
                return type.FullName;
        }

        public static string GenerateClass(this DataTable dt)
        {
            //var datas = new List<Dictionary<string, Type>>();
            var sb = new StringBuilder();
            sb.AppendLine($"public class {dt.TableName}");
            sb.AppendLine("{");
            foreach (DataColumn dc in dt.Columns)
                sb.AppendLine($"    public {GetTypeAliases(dc.DataType)} {dc.ColumnName} {{ get; set; }}");
            sb.Append('}');
            return sb.ToString();
        }
    }
}