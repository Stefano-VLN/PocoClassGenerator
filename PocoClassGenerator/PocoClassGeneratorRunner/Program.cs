using Microsoft.Data.SqlClient;
using PocoClassGenerator;

namespace PocoClassGeneratorRunner
{
    internal class Program
    {
        static void Main()
        {
            using var conn = new SqlConnection(@"Server = ABV-DB-03;Database = DATAOPS;Trusted_Connection=True;Encrypt=False");
            var Pocos = conn.GenerateAllTables(GeneratorBehavior.View);
            File.WriteAllText("Pocos.cs", Pocos);
        }
    }
}