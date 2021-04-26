using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data;
using System.Data.SqlClient;
using System.Threading;
using EasyDataMigrator.Modules.Core;
using EasyDataMigrator.Modules.Configuration;
using System.Linq;

namespace EasyDataMigrator
{
    class EasyDataMigrator
    {
        static void Main(string[] args)
        {
            Mapper mapper = new();
            Logger logger = new();

            Variables variables = CustomVariablesConfig.GetConfig().Variables;
            DbConnector origConnection = new("OriginConnection"), destConnection = new("DestinationConnection");

            string OriginPattern = ConfigurationManager.AppSettings["SearchOriginPattern"];
            string DestinationPattern = ConfigurationManager.AppSettings["SearchDestPattern"];
            bool excludePatternFromMatch = ConfigurationManager.AppSettings["excludePatternFromMatch"] == "True";
            decimal precisionThreshold;

            origConnection.Open();
            destConnection.Open();
            mapper.TryAutoMapping(origConnection, destConnection, OriginPattern, DestinationPattern, excludePatternFromMatch);
            origConnection.Close();
            destConnection.Close();

            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MapPrecisionThreshold"]))
                precisionThreshold = Convert.ToDecimal(ConfigurationManager.AppSettings["MapPrecisionThreshold"]);
            else
                precisionThreshold = 50; // By default, we want at least to be 50% succesful

            decimal totalPrecision = mapper.FieldMapPrecision > 0 ? (mapper.TableMapPrecision + mapper.FieldMapPrecision) / 2 : 0;

            if (totalPrecision < precisionThreshold)
            {
                logger.PrintNLog($"Precision threshold of {precisionThreshold}% has not been reached! Aborting migration.", Logger.LogType.CRITICAL);
#if DEBUG
                Console.ReadKey();
#endif
                return;
            }

            try
            {
                logger.PrintNLog("Migration process started.");
                logger.PrintNLog($"Mapping precision -> TABLES: {mapper.TableMapPrecision} | FIELDS: {mapper.FieldMapPrecision}");

                // Before Migration queries in Origin connection
                if (origConnection.Queries.Count > 0)
                {
                    origConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(ref variables, origConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeMigration);
                    ExecuteQueries(ref variables, origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeMigration);

                    origConnection.Close();

                }

                // Before Migration queries in Destination connection
                if (destConnection.Queries.Count > 0)
                {
                    destConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(ref variables, destConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeMigration);
                    ExecuteQueries(ref variables, destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeMigration);

                    destConnection.Close();

                }

                // We open connection to begin insert data
                destConnection.Open();

                BeginMigration(mapper, origConnection, destConnection, logger);

                //After Migration queries in Origin connection
                if (origConnection.Queries.Count > 0)
                {
                    origConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(ref variables, origConnection, Query.QueryType.Read, Query.QueryExecutionTime.AfterMigration);
                    ExecuteQueries(ref variables, origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.AfterMigration);

                    origConnection.Close();

                }

                // After Migration queries in Destination connection
                if (destConnection.Queries.Count > 0)
                {
                    destConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(ref variables, destConnection, Query.QueryType.Read, Query.QueryExecutionTime.AfterMigration);
                    ExecuteQueries(ref variables, destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.AfterMigration);

                    destConnection.Close();

                }

            }
            catch (SqlException ex) when (ex.Number == 208) // SQ_BADOBJECT --> The specified object cannot be found.
            {
                logger.PrintNLog($"No se encuentra el objeto especificado. No se puede ejecutar la consulta. Error de Base de Datos.", Logger.LogType.CRITICAL);
            }

            finally
            {
                // When we've finished all operations, we finally close the destination connection
                destConnection.Close();
                logger.PrintNLog("Migration process ended.");
            }

#if DEBUG
            Console.ReadKey();
#endif
        }

        private static int ExecuteQueries(ref Variables variables, DbConnector connection, Query.QueryType queryType, Query.QueryExecutionTime executionTime)
        {
            int totalAffectedRows = 0;

            List<Query> queries = connection.Queries.FindAll(query => query.ExecutionTime == executionTime && query.Type == queryType);

            if (queries.Count == 0)
                return 0;

            // We order by ExecutionOrder to avoid needed variables beeing empty
            queries = (from q in queries
                      orderby q.ExecutionOrder ascending
                      select q).ToList();

            foreach (Query query in queries)
            {
                int affectedRows = 0;

                if (query.Sql.Contains("$")) // We determine if it is a parametrized query or not
                {
                    // We inject user-defined variables                        
                    variables.ForEach(
                        v =>
                        {
                            if (v.Type == typeof(string))
                            {
                                query.Sql = query.Sql.Remove('$').Replace(v.Name, "'" + v.Value + "'");
                            }
                            else
                            {
                                query.Sql = query.Sql.Remove('$').Replace(v.Name, v.TrueValue);
                            }
                        }
                        );
                }

                if (!string.IsNullOrWhiteSpace(query.StoreIn) && query.Type == Query.QueryType.Read)
                {
                    Variable result = variables.Find(v => v.Name == query.StoreIn.Replace("$", ""));

                    if (connection.SqlConnection.State != ConnectionState.Open)
                        connection.Open();

                    result.Value = connection.GetFirst(query.Sql);
                    result.TrueValue = Convert.ChangeType(result.Value, result.Type);
                }
                else if (query.Type == Query.QueryType.Execute)
                {
                    if (connection.SqlConnection.State != ConnectionState.Open)
                        connection.Open();

                    affectedRows = connection.ModifyDB(query.Sql);
                    totalAffectedRows += affectedRows;
                }
            }

            return totalAffectedRows;
        }

        private static void BeginMigration(Mapper mapper, DbConnector origConnection, DbConnector destConnection, Logger logger)
        {
            List<TableMap> failedMigrations = new();

            // We try to migrate the tables
            foreach (TableMap tableMap in mapper.TableMaps)
            {
                if (tableMap.DestinationTableBusy)
                {
                    logger.PrintNLog($"Skipping table {tableMap.ToTable} because it is currently busy.", Logger.LogType.WARNING);
                    failedMigrations.Add(tableMap);
                    continue;
                }

                bool migFailed = MigrateTable(tableMap, origConnection, destConnection, logger);

                if (migFailed)
                    failedMigrations.Add(tableMap);                                                
            }

            // We retry (if any) failed migrations
            int maxRetries = Convert.ToInt32(ConfigurationManager.AppSettings["RetryFailedMigrations"]);
            TimeSpan busyTablesWaitTime = new(0, 0, Convert.ToInt32(ConfigurationManager.AppSettings["WaitTimeBusyTables"]));

            foreach (TableMap failedMig in failedMigrations)
            {
                bool retrySucceed = false;

                logger.PrintNLog($"Retrying migration {failedMig.MapId}...");

                int retryCount = 1;
                while (retryCount <= maxRetries)
                {
                    logger.Print($"Retry number: {retryCount}.");
                 
                    if (failedMig.UpdateStatus())
                    {
                        bool migFailed = MigrateTable(failedMig, origConnection, destConnection, logger);
                        
                        if (!migFailed)
                        {
                            logger.PrintNLog($"Failed migration {failedMig.MapId} has been succesfully migrated on retry number {retryCount}!");
                            retrySucceed = true;
                            break;
                        }
                    }
                    else
                    {
                        logger.PrintNLog($"Table is still busy, waiting {busyTablesWaitTime} seconds before next retry.", Logger.LogType.WARNING);
                        Thread.Sleep(busyTablesWaitTime);
                    }

                    retryCount++;
                }

                if (!retrySucceed)
                    logger.PrintNLog($"Could not migrate {failedMig.MapId} fatal error!", Logger.LogType.CRITICAL);

            }
        }

        private static bool MigrateTable(TableMap tableMap, DbConnector origConnection, DbConnector destConnection, Logger logger) 
        {            

            bool migrationFailed = false;

            logger.PrintNLog($"Inserting records from {tableMap.FromTable} to {tableMap.ToTable}.");

            // We execute (if any) the BeforeEachInsertQuery 
            //if (BeforeEachInsertQuery)
            //{
            //    origConnection.Open();
            //    origConnection.ModifyDB(ConfigurationManager.AppSettings["BeforeEachInsertQuery"]);
            //    origConnection.Close();
            //}

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

                    // Lastly we inform of what happened
                    logger.PrintNLog($"Migration {tableMap.MapId} exceeded timeout so changing to Bulk Mode. You should consider adding {tableMap.FromTable} to 'UseBulkCopyTables' list.", Logger.LogType.WARNING);
                }
                catch (Exception ex)
                {                    
                    migrationFailed = true;
                    logger.PrintNLog($"Ooops! Something went wrong for migration {tableMap.MapId}, skipping for now! See details below: {Environment.NewLine}" + ex.Message, Logger.LogType.ERROR);
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
            //if (AfterEachInsertQuery && !migrationFailed)
            //{
            //    origConnection.Open();
            //    origConnection.ModifyDB(ConfigurationManager.AppSettings["AfterEachInsertQuery"]);
            //    origConnection.Close();
            //}

            if (!migrationFailed)
                logger.PrintNLog($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");

            return migrationFailed;
        }
    }
}
