using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    /// <summary>
    /// Custom configuration section class for Queries.
    /// </summary>
    public class CustomQueriesSection : ConfigurationSection
    {
        public static CustomQueriesSection GetConfig()
        {
            return (CustomQueriesSection)ConfigurationManager.GetSection("CustomQueries") ?? new CustomQueriesSection();
        }

        [ConfigurationProperty("Queries")]
        [ConfigurationCollection(typeof(Variables), AddItemName = "Query")]
        public Queries Queries
        {
            get
            {
                return (Queries)this[nameof(Queries)];
            }
        }
    }
}
