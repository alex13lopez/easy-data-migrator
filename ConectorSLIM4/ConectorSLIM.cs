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

            foreach (TableMap tableMap in mapper.TableMaps)
            {
                destConnection.OpenConnection();
                string sql = QueryBuilder.InsertQuery(tableMap);
                destConnection.ModifyDB(sql, true);
            }

            Console.ReadKey();
        }
    }
}
