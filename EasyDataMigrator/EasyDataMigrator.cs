using System;
using EasyDataMigrator.Modules;
using EasyDataMigrator.Modules.Core;
using EasyDataMigrator.Modules.Configuration;

namespace EasyDataMigrator
{
    class EasyDataMigrator
    {        
        static void Main(string[] args)
        {
            Logger logger = new();
            Commander commander = new(logger);

            try
            {
                commander.TryAutoMapping();
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.WARNING)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.WARNING);
#if DEBUG
                Console.ReadKey();
#endif
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.ERROR)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.ERROR);
#if DEBUG
                Console.ReadKey();
#endif
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.CRITICAL)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
#if DEBUG
                Console.ReadKey();
#endif
            }
            catch (Exception ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
#if DEBUG
                Console.ReadKey();
#endif
                return;
            }

            try
            {
                logger.PrintNLog("Migration process started.");
                logger.PrintNLog($"Mapping precision -> TABLES: {commander.Mapper.TableMapPrecision} | FIELDS: {commander.Mapper.FieldMapPrecision}");

                // First, we always execute the "Read" type queries because we might need their results for execute queries
                commander.ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionTime.BeforeMigration);
                commander.ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionTime.BeforeMigration);

                commander.BeginMigration();

                commander.ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionTime.AfterMigration);
                commander.ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionTime.AfterMigration);


            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.CRITICAL)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.PrintNLog("Migration process ended with errors.");
#if DEBUG
                Console.ReadKey();

                return; // A critical exception is fatal so the program cannot continue
#endif
            }
            catch (Exception ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.PrintNLog("Migration process ended with errors.");
#if DEBUG
                Console.ReadKey();
#endif
                return; // An uncaught exception is an unknown severity kind of exception so we are uncertain if we can continue with the execution or not
            }

            logger.PrintNLog("Migration process ended with success.");

#if DEBUG
            Console.ReadKey();
#endif            
        }                                            
    }
}
