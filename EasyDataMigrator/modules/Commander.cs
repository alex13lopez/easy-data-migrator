using System;
using System.Configuration;
using EasyDataMigrator.Modules.Core;
using EasyDataMigrator.Modules.Configuration;
using System.Collections.Generic;
using System.Linq;
using System.Data;
using System.Data.SqlClient;
using System.Threading;

namespace EasyDataMigrator.Modules
{
    class Commander
    {
        private readonly Mapper mapper;
        private readonly Variables userVariables;
        private readonly Variables systemVariables;
        private readonly DbConnector origConnection;
        private readonly DbConnector destConnection;
        private readonly Logger logger;
        private readonly string OriginPattern;
        private readonly string DestinationPattern;
        private readonly bool excludePatternFromMatch;
        private decimal precisionThreshold;
        private readonly bool UseControlMech;

        public Mapper Mapper { get => mapper; }
        public DbConnector OrigConnection { get => origConnection; }
        public DbConnector DestConnection { get => destConnection; }

        /// <summary>
        /// Commander is the main central class that will "command" and lead all migration operations, from Mapping to actually migrate the data.
        /// </summary>
        /// <param name="_logger">We pass an object of type logger so we can print messages to console and write important messages to logs.</param>
        public Commander(Logger _logger)
        {
            mapper = new();
            userVariables = CustomVariablesConfig.GetConfig().Variables;
            systemVariables = new();
            origConnection = new("OriginConnection");
            destConnection = new("DestinationConnection");
            OriginPattern = ConfigurationManager.AppSettings["SearchOriginPattern"];
            DestinationPattern = ConfigurationManager.AppSettings["SearchDestPattern"];
            excludePatternFromMatch = ConfigurationManager.AppSettings["excludePatternFromMatch"] == "True";
            UseControlMech = ConfigurationManager.AppSettings["UseTableControlMechanism"] == "True";
            logger = _logger;
            InitSystemVariables();
        }

        /// <summary>
        /// Function where we will initialize/create system vars that will be used to alter logic an flow of the program.
        /// These variables may be refilled be CustomQueries using the %VariableName instead of $VariableName so we may diferentiate between System Variables and User Variables.
        /// </summary>
        private void InitSystemVariables()
        {
            systemVariables["DestTableName"] = new Variable("DestTableName")
            {
                Type = typeof(string),
            };

            systemVariables["DestTableIsBusy"] = new Variable("DestTableIsBusy")
            {
                Type = typeof(bool),
            };
        }

        /// <summary>
        /// We command Mapper() to try to AutoMap originConnection and destConnection.
        /// </summary>
        public void TryAutoMapping()
        {
            logger.Print($"Trying to AutoMap Origin DataBase '{origConnection.ServerName}.{origConnection.DataBaseName}' to Destination DataBase '{destConnection.ServerName}.{destConnection.DataBaseName}'");

            origConnection.Open();
            destConnection.Open();
            mapper.AutoMap(origConnection, destConnection, OriginPattern, DestinationPattern, excludePatternFromMatch);
            origConnection.Close();
            destConnection.Close();

            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["MapPrecisionThreshold"]))
                precisionThreshold = Convert.ToDecimal(ConfigurationManager.AppSettings["MapPrecisionThreshold"]);
            else
                precisionThreshold = 50; // By default, we want at least to be 50% succesful

            decimal totalPrecision = mapper.FieldMapPrecision > 0 ? (mapper.TableMapPrecision + mapper.FieldMapPrecision) / 2 : 0;

            if (totalPrecision < precisionThreshold)
            {
                throw new MigrationException($"Precision threshold of {precisionThreshold}% has not been reached! Aborting migration.", MigrationException.ExceptionSeverityLevel.CRITICAL);
            }
            else
            {
                logger.Print("Automapping successfull!");
            }
        }

        /// <summary>
        /// This function executes queries (if any) on both connections with the QueryType and QueryExecutionContext especified as parameters.
        /// </summary>
        /// <param name="queryType">QueryType to be executed (Read/Execute)</param>
        /// <param name="executionContext">QueryExecutionContext where the query is required to execute (BeforMigration, AfterMigration, BeforeTableMigration or AfterTableMigration)</param>
        public void ExecuteQueries(Query.QueryType queryType, Query.QueryExecutionContext executionContext)
        {
            DbConnector connection = null;
            List<Query> queries = null;

            logger.AlternateColors = true;

            if (origConnection.Queries.Count > 0)
            {
                connection = origConnection;

                if (connection != null)
                    queries = connection.Queries.FindAll(query => query.ExecutionContext == executionContext && query.Type == queryType);

                if (queries != null && queries.Count > 0)
                    PerformQueries(connection, queries);
            }

            if (destConnection.Queries.Count > 0)
            {
                connection = destConnection;

                if (connection != null)
                    queries = connection.Queries.FindAll(query => query.ExecutionContext == executionContext && query.Type == queryType);

                if (queries != null && queries.Count > 0)
                    PerformQueries(connection, queries);
            }

            logger.AlternateColors = false;
        }

        /// <summary>
        /// This function is the one that internally exeuctes queries but it can only be called by its "interface function" which is ExecuteQueries.
        /// </summary>
        /// <param name="connection"></param>
        /// <param name="queries"></param>
        private void PerformQueries(DbConnector connection, List<Query> queries)
        {
            connection.Open();

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

                        Variable userVar = null, sysVar = null;
                try
                {
                    if (!string.IsNullOrWhiteSpace(opQuery.StoreIn) && opQuery.Type == Query.QueryType.Read)
                    {

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
                catch (SqlException ex) when (ex.Number == 208) // SQ_BADOBJECT --> The specified object cannot be found.
                {
                    connection.Close();
                    throw new MigrationException("The specified object cannot be found. DataBase error.", MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
                }
                catch (SqlException ex) when (ex.Number == -2) // Query timeout
                {
                    connection.Close();
                    throw new MigrationException($"Query has reached timeout. {Environment.NewLine}" + ex.Message, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
                }
                catch (InvalidCastException ex)
                {
                    connection.Close();
                    throw new MigrationException(ex.Message, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
                }
                catch (FormatException ex)
                {
                    connection.Close();
                    string errMessage = null;

                    if (sysVar != null)
                        errMessage = $"Input string was not in a correct format. Cannot convert '{sysVar.Value}' to '{sysVar.Type.Name}'. This value is for SystemVariable: '{sysVar.Name}' and is obtained from CustomQuery: '{opQuery.OriginalID}'";
                    else if (userVar != null)
                        errMessage = $"Input string was not in a correct format. Cannot convert '{userVar.Value}' to '{userVar.Type.Name}'. This value is for CustomVariable: '{userVar.Name}' and is obtained from CustomQuery: '{opQuery.OriginalID}'";
                    else
                        errMessage = ex.Message;

                    throw new MigrationException(errMessage, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
                }
                catch (OverflowException ex)
                {
                    connection.Close();
                    throw new MigrationException(ex.Message, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
                }
                catch (ArgumentException ex)
                {
                    connection.Close();
                    throw new MigrationException(ex.Message, MigrationException.ExceptionSeverityLevel.CRITICAL, ex);
                }
            }

            connection.Close();
        }

        /// <summary>
        /// Helper function that will inject system and user-defined variables into the query passed by parameter.
        /// </summary>
        /// <param name="query"></param>
        /// <param name="userVariables"></param>
        /// <param name="systemVariables"></param>
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
                // We inject system-defined variables
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

        /// <summary>
        /// Function that starts data migration.
        /// </summary>
        public void BeginMigration()
        {
            List<TableMap> failedMigrations = new();

            destConnection.Open();

            foreach (TableMap tableMap in Mapper.TableMaps)
            {
                bool migrationSucceded = false;
                systemVariables["DestTableName"].TrueValue = tableMap.ToTableName;

                // Before Table Migration queries
                ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionContext.BeforeTableMigration);
                ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionContext.BeforeTableMigration);

                if (UseControlMech)
                {
                    if (systemVariables["DestTableIsBusy"].TrueValue)
                    {
                        logger.PrintNLog($"Skipping table {tableMap.ToTable} because it is currently busy.", Logger.LogType.WARNING);
                        failedMigrations.Add(tableMap);
                        continue;
                    }
                }
                else
                {
                    logger.PrintNLog($"Control mechanism is disabled, cannot determine if destination table '{tableMap.ToTable}' is busy or not.");
                }
                
                try
                {                    
                    // We try table migration
                    MigrateMap(tableMap);
                    migrationSucceded = true;
                }
                catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.ERROR) 
                {
                    failedMigrations.Add(tableMap);
                }

                
                if(migrationSucceded)
                {
                    // After Table Migration queries
                    ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionContext.AfterTableMigration);
                    ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionContext.AfterTableMigration);
                }
            }

            if (failedMigrations.Count > 0)
                RetryFailedMigrations(failedMigrations);

            destConnection.Close();
        }

        /// <summary>
        /// Private function that migrates a table map.
        /// </summary>
        /// <param name="tableMap"></param>
        private void MigrateMap(TableMap tableMap)
        {
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
                   throw new MigrationException($"Ooops! Something went wrong for migration {tableMap.MapId} in native-insert mode, skipping for now!", MigrationException.ExceptionSeverityLevel.ERROR, ex);
                }

            }

            if (tableMap.UseBulkCopy)
            {
                try
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
                catch (Exception ex)
                {
                    throw new MigrationException($"Ooops! Something went wrong for migration {tableMap.MapId} in bulk-insert mode, skipping for now!", MigrationException.ExceptionSeverityLevel.ERROR, ex);
                }
            }

            logger.PrintNLog($"Inserted records from {tableMap.FromTable} to {tableMap.ToTable} successfully!");
        }

        /// <summary>
        /// Private function to retry failed tablemap migrations.
        /// </summary>
        /// <param name="failedMigrations"></param>
        private void RetryFailedMigrations(List<TableMap> failedMigrations)
        {
            int maxRetries = Convert.ToInt32(ConfigurationManager.AppSettings["FailedMigrationsRetries"]);
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
                        UpdateTableStatus();

                        if (!systemVariables["DestTableIsBusy"].TrueValue)
                        {
                            try
                            {
                                // We retry table migration
                                MigrateMap(failedMig);
                                retrySucceed = true;
                            }
                            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.ERROR)
                            {
                                logger.PrintNLog($"Failed migration {failedMig.MapId} still could not be migrated. Waiting {busyTablesWaitTime}s before next retry!", Logger.LogType.ERROR);
                                Thread.Sleep(busyTablesWaitTime);

                                retryCount++;
                                continue;
                            }

                            if (retrySucceed)
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
                        try
                        {
                            // We retry table migration
                            MigrateMap(failedMig);
                            retrySucceed = true;
                        }
                        catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.ERROR)
                        {
                            logger.PrintNLog($"Failed migration {failedMig.MapId} still could not be migrated. Waiting {busyTablesWaitTime}s before next retry!", Logger.LogType.ERROR);
                            Thread.Sleep(busyTablesWaitTime);

                            retryCount++;
                            continue;
                        }

                        if (retrySucceed)
                        {
                            logger.PrintNLog($"Failed migration {failedMig.MapId} has been succesfully migrated on retry number {retryCount}!");
                            retrySucceed = true;
                            break;
                        }
                    }
                }

                if (!retrySucceed)
                    throw new MigrationException($"Could not migrate {failedMig.MapId}, fatal error!", MigrationException.ExceptionSeverityLevel.CRITICAL);
            }
        }

        /// <summary>
        /// Function that repeats the BeforeTableMigration queries to see if destination table status has changed from busy to not busy.
        /// </summary>
        private void UpdateTableStatus()
        {
            ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionContext.BeforeTableMigration);
            ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionContext.BeforeTableMigration);
        }
    }
}
