using System;
using System.Configuration;
using System.Data;
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

            // We open connection to begin insert data
            destConnection.Open();
            foreach (TableMap tableMap in mapper.TableMaps)
            {
                if (tableMap.DestinationTableBusy)
                {
                    Console.WriteLine($"Skipping table ${tableMap.ToTable} because it is currently busy");
                    continue;
                }

                Console.WriteLine($"Inserting records from {tableMap.FromTable} to {tableMap.ToTable}.");                
                if (!tableMap.UseBulkCopy)
                {
                    string sqlDelete = QueryBuilder.Delete(tableMap);
                    string sqlInsert = QueryBuilder.Insert(tableMap);
                
                    destConnection.BeginTransaction();
                    destConnection.ModifyDB(sqlDelete, true);
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

                    // We free used resources since we don't need them anymore
                    data.Dispose();
                }
                Console.WriteLine($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");
            }

            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["AfterInsertQuery"]))
                destConnection.ModifyDB(ConfigurationManager.AppSettings["AfterInsertQuery"]);

            destConnection.Close();
        }
    }
}
