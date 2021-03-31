using System;
using System.Data;
using System.Data.SqlClient;
using ConectorSLIM4.modules;

namespace ConectorSLIM4
{
    class ConectorSLIM
    {
        static void Main(string[] args)
        {
            Mapper mapper = new();
            DbConnector origConnection = new("DataDecConnectionString"), destConnection = new("SLIMConnectionString");

            origConnection.Open();
            destConnection.Open();
            mapper.TryAutoMapping(origConnection, destConnection, "SLIM_");
            origConnection.Close();
            destConnection.Close();

            // We open connection to begin insert dataA
            destConnection.Open();
            foreach (TableMap tableMap in mapper.TableMaps)
            {
                Console.WriteLine($"Inserting records from {tableMap.FromTable} to {tableMap.ToTable}.");                
                if (!tableMap.UseBulkCopy)
                {
                    string sqlDelete = QueryBuilder.Delete(tableMap);
                    string sqlInsert = QueryBuilder.Insert(tableMap);
                
                    destConnection.BeginTransaction();
                    destConnection.ModifyDB(sqlDelete);
                    destConnection.ModifyDB(sqlInsert, true);                    
                    destConnection.CommitTransaction();
                }
                else
                {
                    DataTable data = new();
                    string sql = QueryBuilder.Select(tableMap);

                    origConnection.Open();
                    data.Load(origConnection.ReadDB(sql));
                    origConnection.Close();

                    destConnection.BeginTransaction();
                    destConnection.BulkCopy(data, tableMap);
                    destConnection.CommitTransaction();
                }
                Console.WriteLine($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");
            }
            destConnection.Close();
        }
    }
}
