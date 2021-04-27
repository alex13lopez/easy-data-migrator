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

            try
            {
                origConnection.Open();
                destConnection.Open();
                mapper.TryAutoMapping(origConnection, destConnection, OriginPattern, DestinationPattern, excludePatternFromMatch);
                origConnection.Close();
                destConnection.Close();
            }
            catch (Exception ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
#if DEBUG
                Console.ReadKey();
#endif
                return;
            }

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
                    ExecuteQueries(origConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeMigration, logger, variables);
                    ExecuteQueries(origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeMigration, logger, variables);

                    origConnection.Close();

                }

                // Before Migration queries in Destination connection
                if (destConnection.Queries.Count > 0)
                {
                    destConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(destConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeMigration, logger, variables);
                    ExecuteQueries(destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeMigration, logger, variables);

                    destConnection.Close();

                }

                // We open Dest Connection here to avoid open() and close() operations every each table insert
                destConnection.Open();

                BeginMigration(mapper, origConnection, destConnection, logger, ref variables);

                //After Migration queries in Origin connection
                if (origConnection.Queries.Count > 0)
                {
                    origConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(origConnection, Query.QueryType.Read, Query.QueryExecutionTime.AfterMigration, logger, variables);
                    ExecuteQueries(origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.AfterMigration, logger, variables);

                    origConnection.Close();

                }

                // After Migration queries in Destination connection
                if (destConnection.Queries.Count > 0)
                {
                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(destConnection, Query.QueryType.Read, Query.QueryExecutionTime.AfterMigration, logger, variables);
                    ExecuteQueries(destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.AfterMigration, logger, variables);
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

        private static void BeginMigration(Mapper mapper, DbConnector origConnection, DbConnector destConnection, Logger logger, ref Variables variables)
        {
            List<TableMap> failedMigrations = new();
            Variables systemVariables = new();
            bool UseControlMech = ConfigurationManager.AppSettings["UseTableControlMechanism"] == "True";

            // We try to migrate the tables
            foreach (TableMap tableMap in mapper.TableMaps)
            {
                systemVariables["DestTableName"] = new Variable("DestTableName")
                {
                    Type = typeof(string),
                    Value = tableMap.ToTableName,
                    TrueValue = tableMap.ToTableName,
                };

                systemVariables["DestTableIsBusy"] = new Variable("DestTableIsBusy")
                {
                    Type = typeof(bool),
                };

                // Before Table Migration queries in Origin connection
                if (origConnection.Queries.Count > 0)
                {
                    origConnection.Open();

                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(origConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeTableMigration, logger, variables, systemVariables);
                    ExecuteQueries(origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeTableMigration, logger, variables, systemVariables);

                    origConnection.Close();

                }

                // Before Table Migration queries in Destination connection
                if (destConnection.Queries.Count > 0)
                {
                    // First we obtain the "Read" type queries because we might need their data for execution queries later
                    ExecuteQueries(destConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeTableMigration, logger, variables, systemVariables);
                    ExecuteQueries(destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeTableMigration, logger, variables, systemVariables);
                }                

                if (systemVariables["DestTableIsBusy"].TrueValue && UseControlMech)
                {
                    logger.PrintNLog($"Skipping table {tableMap.ToTable} because it is currently busy.", Logger.LogType.WARNING);
                    failedMigrations.Add(tableMap);
                    continue;
                }                

                bool migFailed = MigrateTable(tableMap, origConnection, destConnection, logger);

                if (migFailed)                
                    failedMigrations.Add(tableMap);
                else
                {
                    // After Table Migration queries in Origin connection
                    if (origConnection.Queries.Count > 0)
                    {
                        origConnection.Open();

                        // First we obtain the "Read" type queries because we might need their data for execution queries later
                        ExecuteQueries(origConnection, Query.QueryType.Read, Query.QueryExecutionTime.AfterTableMigration, logger, variables, systemVariables);
                        ExecuteQueries(origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.AfterTableMigration, logger, variables, systemVariables);

                        origConnection.Close();

                    }

                    // After Table Migration queries in Destination connection
                    if (destConnection.Queries.Count > 0)
                    {
                        // First we obtain the "Read" type queries because we might need their data for execution queries later
                        ExecuteQueries(destConnection, Query.QueryType.Read, Query.QueryExecutionTime.AfterTableMigration, logger, variables, systemVariables);
                        ExecuteQueries(destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.AfterTableMigration, logger, variables, systemVariables);
                    }
                }
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

                    if (UseControlMech)
                    {
                        UpdateTableStatus(origConnection, destConnection, systemVariables);

                        if (!systemVariables["DestTableIsBusy"].TrueValue)
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
                    }
                    else
                    {
                        bool migFailed = MigrateTable(failedMig, origConnection, destConnection, logger);

                        if (!migFailed)
                        {
                            logger.PrintNLog($"Failed migration {failedMig.MapId} has been succesfully migrated on retry number {retryCount}!");
                            retrySucceed = true;
                            break;
                        }
                        else
                        {
                            logger.PrintNLog($"Failed migration {failedMig.MapId} still could not be migrated. Waiting {busyTablesWaitTime}s before next retry!", Logger.LogType.ERROR);
                            Thread.Sleep(busyTablesWaitTime);
                        }
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

            if (!migrationFailed)
                logger.PrintNLog($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");            

            return migrationFailed;
        }

        private static void ExecuteQueries(DbConnector connection, Query.QueryType queryType, Query.QueryExecutionTime executionTime, Logger logger, Variables userVariables = null, Variables systemVariables = null)
        {
            logger.AlternateColors = true;

            List<Query> queries = connection.Queries.FindAll(query => query.ExecutionTime == executionTime && query.Type == queryType);

            if (queries.Count == 0)
            {
                logger.AlternateColors = false;
                return;
            }

            // We order by ExecutionOrder to avoid needed variables beeing empty
            queries = (from q in queries
                       orderby q.ExecutionOrder ascending
                       select q).ToList();

            foreach (Query query in queries)
            {
                int affectedRows = 0;

                Query opQuery = query.Clone();
                
                if (userVariables != null || systemVariables != null)
                    ParametrizeQuery(opQuery, userVariables, systemVariables);

                if (!string.IsNullOrWhiteSpace(opQuery.StoreIn) && opQuery.Type == Query.QueryType.Read)
                {
                    Variable userVar = null, sysVar = null;

                    if (userVariables != null)
                    {
                        userVar = userVariables.Find(v => v.Name == opQuery.StoreIn.Replace("$", ""));
                    }

                    if (systemVariables != null)
                    {
                        sysVar = systemVariables.Find(v => v.Name == opQuery.StoreIn.Replace("%", ""));
                    }
                    
                    if (connection.SqlConnection.State != ConnectionState.Open)
                        connection.Open();

                    if (userVar != null)
                    {
                        userVar.Value = Convert.ToString(connection.GetFirst(opQuery.Sql));
                        userVar.TrueValue = Convert.ChangeType(userVar.Value, userVar.Type);
                        logger.PrintNLog($"User-defined read query: {opQuery.OriginalID} which is stored in user-defined variable {userVar.Name} has now the value: {userVar.Value}", Logger.LogType.INFO, "querylog");
                        
                    }
                    else if (sysVar != null)
                    {
                        sysVar.Value = Convert.ToString(connection.GetFirst(opQuery.Sql));
                        sysVar.TrueValue = Convert.ChangeType(sysVar.Value, sysVar.Type);
                        logger.PrintNLog($"User-defined read query: {opQuery.OriginalID} which is stored in system-defined variable {sysVar.Name} has now the value: {sysVar.Value}", Logger.LogType.INFO, "querylog");
                    }
                }
                else if (opQuery.Type == Query.QueryType.Execute)
                {
                    if (connection.SqlConnection.State != ConnectionState.Open)
                        connection.Open();

                    affectedRows = connection.ModifyDB(opQuery.Sql);

                    logger.PrintNLog($"User-defined execute query: {opQuery.OriginalID} has affected {affectedRows} rows", Logger.LogType.INFO, "querylog");
                }
            }

            logger.AlternateColors = false;
        }

        private static void ParametrizeQuery(Query query, Variables userVariables, Variables systemVariables)
        {            
            if (query.Sql.Contains("$") && userVariables != null) // We determine if it is a user-parametrized query or not
            {
                // We inject user-defined variables                        
                userVariables.ForEach(
                    v =>
                    {
                        if (v.Type == typeof(string))
                        {
                            query.Sql = query.Sql.Replace("$" + v.Name, "'" + v.Value + "'");
                        }
                        else
                        {
                            query.Sql = query.Sql.Replace("$" + v.Name, v.Value); // We use Value property instead of TrueValue because Replace() needs strings
                        }
                    }
                    );
            }

            if (query.Sql.Contains("%") && systemVariables != null)
            {
                systemVariables.ForEach(
                    v =>
                    {
                        if (v.Type == typeof(string))
                        {
                            query.Sql = query.Sql.Replace("%" + v.Name, "'" + v.Value + "'");
                        }
                        else
                        {
                            query.Sql = query.Sql.Replace("%" + v.Name, v.Value);
                        }
                    }
                    );
            }
        }

        private static void UpdateTableStatus(DbConnector origConnection, DbConnector destConnection, Variables systemVariables)
        {
            if (origConnection.Queries.Count > 0)
            {
                origConnection.Open();

                // First we obtain the "Read" type queries because we might need their data for execution queries later
                ExecuteQueries(origConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeTableMigration, null, systemVariables);
                ExecuteQueries(origConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeTableMigration, null, systemVariables);

                origConnection.Close();

            }

            // Before Table Migration queries in Destination connection
            if (destConnection.Queries.Count > 0)
            {
                // First we obtain the "Read" type queries because we might need their data for execution queries later
                ExecuteQueries(destConnection, Query.QueryType.Read, Query.QueryExecutionTime.BeforeTableMigration, null, systemVariables);
                ExecuteQueries(destConnection, Query.QueryType.Execute, Query.QueryExecutionTime.BeforeTableMigration, null, systemVariables);
            }
        }
    }
}
