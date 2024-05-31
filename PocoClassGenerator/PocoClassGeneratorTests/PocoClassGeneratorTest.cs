using MartinCostello.SqlLocalDb;
using Microsoft.Data.SqlClient;
using Microsoft.SqlServer.Management.Common;
using Microsoft.SqlServer.Management.Smo;
using PocoClassGenerator;
using System;
using System.Data;
using System.IO;
using Xunit;

namespace PocoClassGeneratorTests
{
    public class DatabaseFixture : IDisposable
    {
        public SqlConnection Db { get; private set; }

        public DatabaseFixture()
        {
            var localDB = new SqlLocalDbApi();

            var instance = localDB.CreateTemporaryInstance(deleteFiles: true);

            Db = new SqlConnection(instance.ConnectionString);

            // Apri connessione
            Db.Open();

            // Setup DB
            string script = File.ReadAllText("UnitTest_DDL_SQLScript.sql");
            ServerConnection svrConnection = new(Db);
            Server server = new(svrConnection);
            server.ConnectionContext.ExecuteNonQuery(script);
        }

        public void Dispose()
        {
            // ... clean up test data from the database ...
        }
    }

    public class DataTablePocoClassGeneratorTest
    {
        [Fact]
        public void DataTablePocoClassTest()
        {
            var dt = new DataTable();
            dt.TableName = "TestTable";
            dt.Columns.Add(new DataColumn() { ColumnName = "ID", DataType = typeof(string) });

            var result = dt.GenerateClass();
            var expect =
@"public class TestTable
{
    public string ID { get; set; }
}";
            Assert.Equal(expect, result);
        }
    }

    public class PocoClassGeneratorTest : IClassFixture<DatabaseFixture>
    {
        DatabaseFixture fixture;

        public PocoClassGeneratorTest(DatabaseFixture fixture)
        {
            this.fixture = fixture;
        }

        [Fact]
        public void GenerateClassTest()
        {
            var result1 = fixture.Db.GenerateClass("select * from table1");
            Console.WriteLine(result1);
            Assert.Contains("public class table1", result1);

            var result2 = fixture.Db.GenerateClass("select * from table1");
            Console.WriteLine(result2);
            Assert.Contains("public class table1", result2);

            var result3 = fixture.Db.GenerateClass("with cte as (select 1 id , 'weihan' name) select * from cte;");
            Console.WriteLine(result3);
            Assert.Contains("public class Info", result3);

            var result4 = fixture.Db.GenerateClass("with cte as (select 1 id , 'weihan' name) select * from cte;", "CteModel");
            Console.WriteLine(result4);
            Assert.Contains("public class CteModel", result4);
        }

        [Fact]
        public void GenerateAllTables()
        {
            var result = fixture.Db.GenerateAllTables();
            Console.WriteLine(result);

            Assert.Contains("public class table1", result);
            Assert.Contains("public class table2", result);
        }

        [Fact]
        public void DapperContrib_GenerateAllTables_Test()
        {
            var result = fixture.Db.GenerateAllTables(GeneratorBehavior.DapperContrib);
            Console.WriteLine(result);

            Assert.Contains("[Dapper.Contrib.Extensions.ExplicitKey]", result);
            Assert.Contains("public int ID { get; set; }", result);
            Assert.Contains("[Dapper.Contrib.Extensions.Computed]", result);
            Assert.Contains("public int AutoIncrementColumn { get; set; }", result);
            Assert.Contains("[Dapper.Contrib.Extensions.Table(\"table1\")]", result);
            Assert.Contains("[Dapper.Contrib.Extensions.Key]", result);
            Assert.Contains("public int ID { get; set; }", result);
        }
    }
}
