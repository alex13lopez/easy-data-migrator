using System;
using ConectorSLIM4.modules;

namespace ConectorSLIM4
{
    class ConectorSLIM
    {
        static void Main(string[] args)
        {
            Mapper mapper = new();
            DbConnector origConnection = new("DataDecConnectionString"), destConnection = new("SLIMConnectionString");

            origConnection.OpenConnection();
            destConnection.OpenConnection();
            mapper.TryAutoMapping(origConnection, destConnection, "SLIM_");
            origConnection.CloseConnection();
            destConnection.CloseConnection();

            // We open connection to begin insert dataA
            destConnection.OpenConnection();
            foreach (TableMap tableMap in mapper.TableMaps)
            {
                Console.WriteLine($"Inserting records from {tableMap.FromTable} to {tableMap.ToTable}.");
                
                string sql = QueryBuilder.InsertQuery(tableMap);
                destConnection.BeginTransaction();
                destConnection.ModifyDB(sql, true);
                destConnection.CommitTransaction();
                Console.WriteLine($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");
                break;
            }
            
            destConnection.CloseConnection();

            Console.ReadKey();
        }
    }
}
