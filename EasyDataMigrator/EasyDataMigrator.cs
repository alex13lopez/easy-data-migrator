using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using EasyDataMigrator.modules;

namespace EasyDataMigrator
{
    class EasyDataMigrator
    {
        static void Main(string[] args)
        {
            Mapper mapper = new();
            DbConnector origConnection = new("OriginConnection"), destConnection = new("DestinationConnection");

            string OriginPattern = ConfigurationManager.AppSettings["SearchOriginPattern"];
            string DestinationPattern = ConfigurationManager.AppSettings["SearchDestPattern"];
            bool excludePatternFromMatch = ConfigurationManager.AppSettings["excludePatternFromMatch"] == "True";
            bool BeforeEachInsertQuery = !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["BeforeEachInsertQuery"]);
            bool AfterEachInsertQuery = !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["AfterEachInsertQuery"]);

            origConnection.Open();
            destConnection.Open();
            mapper.TryAutoMapping(origConnection, destConnection, OriginPattern, DestinationPattern, excludePatternFromMatch);
            origConnection.Close();
            destConnection.Close();

            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["BeforeInsertQuery"]))
            {
                origConnection.Open();
                origConnection.ModifyDB(ConfigurationManager.AppSettings["BeforeInsertQuery"]);
                origConnection.Close();
            }

            // We open connection to begin insert data
            destConnection.Open();

            BeginMigration(mapper, origConnection, destConnection, BeforeEachInsertQuery, AfterEachInsertQuery);

            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["AfterInsertQuery"]))
            {
                string sql = ConfigurationManager.AppSettings["AfterInsertQuery"].Replace("$TIMESTAMP", "'" + DateTime.Now.ToString("yyyyMMdd HHmmss") + "'");
                destConnection.ModifyDB(sql);
            }

            // When we've finished all operations, we finally close the destination connection
            destConnection.Close();

#if DEBUG
            Console.WriteLine("Migration process ended");
            Console.ReadKey();
#endif
        }

        private static void BeginMigration(Mapper mapper, DbConnector origConnection, DbConnector destConnection, bool BeforeEachInsertQuery, bool AfterEachInsertQuery)
        {
            List<TableMap> failedMigrations = new();

            // We try to migrate the tables
            foreach (TableMap tableMap in mapper.TableMaps)
            {
                if (tableMap.DestinationTableBusy)
                {
                    Console.WriteLine($"Skipping table ${tableMap.ToTable} because it is currently busy");
                    failedMigrations.Add(tableMap);
                    continue;
                }

                bool migFailed = MigrateTable(tableMap, origConnection, destConnection, BeforeEachInsertQuery, AfterEachInsertQuery);

                if (migFailed)
                    failedMigrations.Add(tableMap);                                                
            }

            // We retry (if any) failed migrations
            int maxRetries = Convert.ToInt32(ConfigurationManager.AppSettings["RetryFailedMigrations"]);

            foreach (TableMap failedMig in failedMigrations)
            {
                int retries = maxRetries;
                while (maxRetries > 0)
                {
                    maxRetries--;

                    bool migFailed = MigrateTable(failedMig, origConnection, destConnection, BeforeEachInsertQuery, AfterEachInsertQuery);

                    if (!migFailed)
                        break;
                }
            }
        }

        private static bool MigrateTable(TableMap tableMap, DbConnector origConnection, DbConnector destConnection, bool BeforeEachInsertQuery, bool AfterEachInsertQuery) 
        {            

            bool migrationFailed = false;

            Console.WriteLine($"Inserting records from {tableMap.FromTable} to {tableMap.ToTable}.");

            // We execute (if any) the BeforeEachInsertQuery 
            if (BeforeEachInsertQuery)
            {
                origConnection.Open();
                origConnection.ModifyDB(ConfigurationManager.AppSettings["BeforeEachInsertQuery"]);
                origConnection.Close();
            }

            if (!tableMap.UseBulkCopy)
            {
                try
                {
                    string sqlDelete = QueryBuilder.Delete(tableMap);
                    string sqlInsert = QueryBuilder.Insert(tableMap);

                    destConnection.BeginTransaction();
                    destConnection.ModifyDB(sqlDelete, true);
                    int affectedRows = destConnection.ModifyDB(sqlInsert, true);

                    if (affectedRows > 0)
                        destConnection.CommitTransaction();
                    else
                        destConnection.RollBackTransaction();

                }
                catch (SqlException ex) when (ex.Number == -2) // The migration exceeded timeout
                {
                    // First we mark the UseBulkCopy property to true
                    tableMap.UseBulkCopy = true;

                    // Secondly we rollback the changes
                    destConnection.RollBackTransaction();
                }
                catch (Exception ex)
                {                    
                    migrationFailed = true;
                    Console.WriteLine(ex.Message);
                }

            }

            if (tableMap.UseBulkCopy)
            {
                DataTable data = new();
                string sql = QueryBuilder.Select(tableMap);

                origConnection.Open();
                data.Load(origConnection.ReadDB(sql));
                origConnection.Close();

                destConnection.BeginTransaction();
                string sqlDelete = QueryBuilder.Delete(tableMap);
                destConnection.ModifyDB(sqlDelete, true);
                destConnection.BulkCopy(data, tableMap);
                destConnection.CommitTransaction();

                // We free used resources since we don't need them anymore
                data.Dispose();
            }

            // We execute (if any) the AfterEachInsertQuery 
            if (AfterEachInsertQuery && !migrationFailed)
            {
                origConnection.Open();
                origConnection.ModifyDB(ConfigurationManager.AppSettings["AfterEachInsertQuery"]);
                origConnection.Close();
            }

            if (!migrationFailed)
                Console.WriteLine($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");
            else
                Console.WriteLine($"ERROR: Migration of {tableMap.FromTable} to {tableMap.ToTable} has failed!");

            return migrationFailed;
        }
    }
}
