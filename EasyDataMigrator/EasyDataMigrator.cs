﻿using System;
using System.Configuration;
using EasyDataMigrator.Modules;
using EasyDataMigrator.Modules.Core;
using EasyDataMigrator.Modules.Configuration;
using CommandLine;
using CommandLine.Text;
using System.Collections.Generic;
using System.IO;

namespace EasyDataMigrator
{
    class EasyDataMigrator
    {   
        /// <summary>
        /// Option class that defines all the available arguments that can be passed to the app
        /// </summary>
        public class Options
        {
            [Option('m', "migrate", HelpText = "Start migration with specified map.", SetName = "saved_migration")]
            public string Migrate { get; set; }

            [Option('A', "automap", HelpText = "Just AutoMap and save mapping.", SetName = "mapping")]
            public string AutoMap { get; set; }          

            [Option('a', "automigrate", HelpText = "AutoMap and start migration with generated map", SetName = "create_migration")]
            public bool AutoMigrate { get; set; }

            [Option('s', "savemap", HelpText = "If AutoMigrate succeeds, this map will be saved for later use.", SetName = "create_migration", Default = null)]
            public string SaveMap { get; set;  }

            [Option('f', "fullpath", HelpText = "Indicates if the path provided for -s|--savemap is a full path or just the name of the map that it will load from the MapsLocation setting in App.config.", Default = false)]
            public bool FullPath { get; set; }

            [Option('l', "listmaps", HelpText = "Gets a lists of TableMaps in default MapsLocation setting in App.config.")]
            public bool ListMaps { get; set; }
        }

        static void Main(string[] args)
        {            
            Logger logger = new();
            Commander commander = new(logger);
            ParserResult<Options> options = Parser.Default.ParseArguments<Options>(args);

            try
            {
                options.WithParsed<Options>(o =>
                {
                    if (o.AutoMigrate)
                        AutoMigrate(logger, commander, o.SaveMap, o.FullPath);
               
                    if (!string.IsNullOrWhiteSpace(o.Migrate))
                        Migrate(logger, commander, o.Migrate, o.FullPath);

                    if (!string.IsNullOrWhiteSpace(o.AutoMap))
                        Map(logger, commander, true, o.AutoMap, o.FullPath);

                    if (o.ListMaps)
                        ListMaps();
                });
            }
            catch (ArgumentNullException ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.Print(HelpText.AutoBuild(options, null, null));
#if DEBUG
                Console.ReadKey();
#endif
            }

             
        }

        /// <summary>
        /// Function that prints to console the list of saved maps in MapsLocation
        /// </summary>
        private static void ListMaps()
        {
            string mapsLocation = ConfigurationManager.AppSettings["MapsLocation"];

            string[] TableMaps = Directory.GetFiles(mapsLocation, "*.tablemaps", SearchOption.TopDirectoryOnly);

            string message = $"Available TableMaps in '{mapsLocation}':{Environment.NewLine}";
            TableMaps.ForEach(file => 
                        {
                            string t = file.Replace(mapsLocation, ""); // We remove the path
                            t = t.Replace(".tablemaps", ""); // We remove the extension
                            message += $"\t- {t}{Environment.NewLine}";
                        });

            Console.WriteLine(message);
        }

        /// <summary>
        /// Function that tries to AutoMap and allows us to save the map if specified so.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="commander"></param>
        /// <param name="save"></param>
        /// <param name="SaveToMap"></param>
        private static void Map(Logger logger, Commander commander, bool save = false, string SaveToMap = null, bool fullPath = false)
        {
            try
            {
                commander.TryAutoMapping();
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.WARNING)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.WARNING);
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.ERROR)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.ERROR);

                return;
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.CRITICAL)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);

                return;
            }
            catch (Exception ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);

                return;
            }

            // We save the map if user wants us to do so
            if (save)
                commander.Mapper.SaveMaps(SaveToMap, fullPath);
        }

        /// <summary>
        /// Interface function that executes Map() and then Migrate() so to provide the "AutoMigrate" feature.
        /// </summary>
        /// <param name="logger"></param>
        /// <param name="commander"></param>
        /// <param name="SaveToMap"></param>
        private static void AutoMigrate(Logger logger, Commander commander, string SaveToMap = null, bool fullPath = false)
        {
            if (string.IsNullOrWhiteSpace(SaveToMap))
                Map(logger, commander);
            else
                Map(logger, commander, true, SaveToMap, fullPath);

            Migrate(logger, commander);
        }

        /// <summary>
        /// Function that starts the mechanism to migrate data.
        /// </summary>
        /// <param name="mapToUse"></param>
        /// <param name="logger"></param>
        /// <param name="commander"></param>
        private static void Migrate(Logger logger, Commander commander, string mapToUse = null, bool fullPath = false)
        {
            static void StartMigration(Logger logger, Commander commander)
            {
                logger.PrintNLog("Migration process started.");
                logger.PrintNLog($"Mapping precision -> TABLES: {commander.Mapper.TableMapPrecision} | FIELDS: {commander.Mapper.FieldMapPrecision}");

                // First, we always execute the "Read" type queries because we might need their results for execute queries
                commander.ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionContext.BeforeMigration);
                commander.ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionContext.BeforeMigration);

                commander.BeginMigration();

                commander.ExecuteQueries(Query.QueryType.Read, Query.QueryExecutionContext.AfterMigration);
                commander.ExecuteQueries(Query.QueryType.Execute, Query.QueryExecutionContext.AfterMigration);

                logger.PrintNLog("Migration process ended with success.");

            }

            try
            {
                // If no map is specified but we have TableMaps, it is most likely that we are "AutoMigrating" so we start the migration with what we have.
                if (string.IsNullOrEmpty(mapToUse) && commander.Mapper.TableMaps.Count > 0)
                {
                    StartMigration(logger, commander);

                }
                // If we specified that map we load that map and then start migration.
                else if (!string.IsNullOrEmpty(mapToUse))
                {
                    commander.Mapper.LoadMaps(mapToUse, fullPath);
                    StartMigration(logger, commander);
                }
                else
                {
                    throw new MigrationException("A saved map has not been provided and AutoMap has failed, aborting migration.", MigrationException.ExceptionSeverityLevel.CRITICAL);
                }


            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.CRITICAL)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.PrintNLog("Migration process ended with errors.");

                if (commander.OrigConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.OrigConnection.Close();

                if (commander.DestConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.DestConnection.Close();

                return; // A critical exception is fatal so the program cannot continue
            }
            catch (Exception ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.PrintNLog("Migration process ended with errors.");

                if (commander.OrigConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.OrigConnection.Close();

                if (commander.DestConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.DestConnection.Close();

                return; // An uncaught exception is an unknown severity kind of exception so we are uncertain if we can continue with the execution or not
            }
        }
    }
}
