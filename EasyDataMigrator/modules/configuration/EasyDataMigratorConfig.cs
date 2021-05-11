using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    internal static class EasyDataMigratorConfig
    {
        internal static readonly string DefaultConfigFilePath = ConfigurationManager.OpenExeConfiguration(ConfigurationUserLevel.None).FilePath;
        internal static ConnectionStringsSection ConnectionStrings { get; set; }
        internal static AppSettingsSection AppSettings { get; set; }
        internal static CustomVariablesSection CustomVariablesSection { get; set; }
        internal static CustomQueriesSection CustomQueriesSection { get; set; }
      
    }
}
