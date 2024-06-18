using PocoClassGenerator;
using System.Data.SqlClient;

namespace PocoClassGeneratorRunner
{
    internal class Program
    {
        static void Main()
        {
            using var conn = new SqlConnection(@"Server = ABV-DB-03;Database = DATAOPS;Trusted_Connection=True;");
            var Pocos = conn.GenerateAllTables();
            File.WriteAllText("Pocos.cs", Pocos);
        }
    }
}