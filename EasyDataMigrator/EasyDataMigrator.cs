using System;
using System.Configuration;
using EasyDataMigrator.Modules;
using EasyDataMigrator.Modules.Core;
using EasyDataMigrator.Modules.Configuration;
using CommandLine;
using CommandLine.Text;
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
            public string SaveMap { get; set; }

            [Option('f', "fullpath", HelpText = "Indicates if the path provided for -s|--savemap or for -c|configfile is a full path or not. If you use this option and are using both options (-s, -c) both paths must be supplied as a full path.", Default = false)]
            public bool FullPath { get; set; }

            [Option('l', "listmaps", HelpText = "Gets a lists of available TableMaps in default MapsLocation setting in default location.")]
            public bool ListMaps { get; set; }

            [Option('L', "listconfigs", HelpText = "Gets a list of available Configs in default ConfigsLocation setting in default location.")]
            public bool ListConfs { get; set; }

            [Option('c', "configfile", HelpText = "Specifies which config file to be used. If none specified, default App.config will be used.")]
            public string ConfigFile { get; set; }

            [Option('p', "pause", HelpText = "Pause after program execution.")]
            public bool PauseAfterExecution { get; set; }
        }

        static void Main(string[] args)
        {
            Logger logger = new();
            Commander commander = new(logger);
            ParserResult<Options> options = Parser.Default.ParseArguments<Options>(args);
            bool pauseAfterExecution = false;

            if (args.Length <= 0)
            {
                Console.Write(HelpText.AutoBuild(options, null, null));
                return;
            }

            try
            {
                options.WithParsed<Options>(o =>
                {
                    if (!string.IsNullOrWhiteSpace(o.ConfigFile))
                        LoadConfig(o.ConfigFile, commander, o.FullPath);
                    else
                        LoadConfig(EasyDataMigratorConfig.DefaultConfigFilePath, commander, true);

                    if (o.AutoMigrate)
                        AutoMigrate(logger, commander, o.SaveMap, o.FullPath);
               
                    if (!string.IsNullOrWhiteSpace(o.Migrate))
                        Migrate(logger, commander, o.Migrate, o.FullPath);

                    if (!string.IsNullOrWhiteSpace(o.AutoMap))
                        Map(logger, commander, true, o.AutoMap, o.FullPath);

                    if (o.ListMaps)
                        ListAppFiles("TableMaps", @".\Maps\", ".tablemaps");

                    if (o.ListConfs)
                        ListAppFiles("Configs", @".\Configs\", ".config");

                    if (o.PauseAfterExecution)
                        pauseAfterExecution = true;
                });
            }
            catch (ArgumentNullException ex)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                Console.Write(HelpText.AutoBuild(options, null, null));
            }
            catch (FileNotFoundException ex)
            {
                logger.PrintNLog(ex.Message + ": " + ex.FileName, Logger.LogType.CRITICAL);
            }

            if (pauseAfterExecution)
            {
                Console.Write("Press any key to continue...");
                Console.ReadKey();
            }

        }

        /// <summary>
        /// Function that loads the configuration file for this execution, be it the default config file or user-specified
        /// </summary>
        /// <param name="configFile"></param>
        private static void LoadConfig(string configFile, Commander commander, bool fullPath = false)
        {
            bool filenameIsValidated = Utilities.ValidateFileName(configFile);

            if (filenameIsValidated)
            {
                string defaultDirPath = @".\Configs\";
                
                if (!Directory.Exists(defaultDirPath))
                    Directory.CreateDirectory(defaultDirPath);

                configFile = defaultDirPath + configFile + ".config";
            }

            if (!fullPath && !filenameIsValidated)
                throw new ArgumentOutOfRangeException(nameof(configFile), $"The file name specified is not valid. It can only contain numbers, letters, dashes, underscores and have a maximum length of 200 characters (Extension will be added automatically).{Environment.NewLine}Please if it is a path use -f|--fullpath.");
            else if (!File.Exists(configFile))
                throw new FileNotFoundException("The file especified does not exist", configFile);

            // We open the config file
            ExeConfigurationFileMap map = new() { ExeConfigFilename = configFile };
            Configuration config = ConfigurationManager.OpenMappedExeConfiguration(map, ConfigurationUserLevel.None);

            // We load the config to each "Configuration Section"
            EasyDataMigratorConfig.AppSettings = config.AppSettings;
            EasyDataMigratorConfig.ConnectionStrings = config.ConnectionStrings;
            EasyDataMigratorConfig.CustomVariablesSection = config.GetSection("CustomVariables") as CustomVariablesSection;
            EasyDataMigratorConfig.CustomQueriesSection = config.GetSection("CustomQueries") as CustomQueriesSection;

            // After loading the configuration succesfully above, we proceed to load the configuration of commander
            commander.LoadConf();
        }

        /// <summary>
        /// Function that prints to console the list of saved maps in MapsLocation
        /// </summary>
        private static void ListAppFiles(string fileType, string path, string fileExt)
        {
            string[] TableMaps = Directory.GetFiles(path, "*" + fileExt, SearchOption.TopDirectoryOnly);

            string message = $"Available {fileType} in '{path}':{Environment.NewLine}";
            TableMaps.ForEach(file => 
                        {
                            string t = file.Replace(path, ""); // We remove the path
                            t = t.Replace(fileExt, ""); // We remove the extension
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
                string errorMsg = ex.Message;

                if (ex.InnerException != null)
                    errorMsg += " Error details: " + ex.InnerException.Message;

                logger.PrintNLog(errorMsg, Logger.LogType.ERROR);

                return;
            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.CRITICAL)
            {
                string errorMsg = ex.Message;
                
                if (ex.InnerException != null)
                    errorMsg += " Error details: " + ex.InnerException.Message;

                logger.PrintNLog(errorMsg, Logger.LogType.CRITICAL);

                return;
            }
            catch (Exception ex)
            {
                string errorMsg = ex.Message;

                if (ex.InnerException != null)
                    errorMsg += " Error details: " + ex.InnerException.Message;

                logger.PrintNLog(errorMsg, Logger.LogType.CRITICAL);

                return;
            }

            try
            {
                // We save the map if user wants us to do so
                if (save)
                    commander.Mapper.SaveMaps(SaveToMap, fullPath);
            }
            catch (ArgumentOutOfRangeException ex)
            {
                logger.PrintNLog($"Unable to save to file '{SaveToMap}'. See details below: {Environment.NewLine}" + ex.Message, Logger.LogType.ERROR);
            }
            
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
            static void StartMigration(Logger logger, Commander commander, string mapToUse = null)
            {
                logger.PrintNLog("Migration process started.");

                if (string.IsNullOrWhiteSpace(mapToUse)) // We're not using a saved migration, so we want to know what precision do we have for this automatic migration
                    logger.PrintNLog($"Mapping precision -> TABLES: {commander.Mapper.TableMapPrecision} | FIELDS: {commander.Mapper.FieldMapPrecision}");
                else
                    logger.PrintNLog($"Using saved map --> {mapToUse}.");

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
                    StartMigration(logger, commander, mapToUse);
                }
                else
                {
                    throw new MigrationException("A saved map has not been provided and AutoMap has failed, aborting migration.", MigrationException.ExceptionSeverityLevel.CRITICAL);
                }


            }
            catch (MigrationException ex) when (ex.SeverityLevel == MigrationException.ExceptionSeverityLevel.CRITICAL)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.PrintNLog("Migration process ended with errors.", Logger.LogType.CRITICAL);

                if (commander.OrigConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.OrigConnection.Close();

                if (commander.DestConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.DestConnection.Close();

                return; // A critical exception is fatal so the program cannot continue
            }
            catch (Exception ex) // Pokemon-catch (gotta catch'em all)
            {
                logger.PrintNLog(ex.Message, Logger.LogType.CRITICAL);
                logger.PrintNLog("Migration process ended with errors.", Logger.LogType.CRITICAL);

                if (commander.OrigConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.OrigConnection.Close();

                if (commander.DestConnection.SqlConnection.State == System.Data.ConnectionState.Open)
                    commander.DestConnection.Close();

                return; // An uncaught exception is an unknown severity kind of exception so we are uncertain if we can continue with the execution or not
            }
        }
    }
}
