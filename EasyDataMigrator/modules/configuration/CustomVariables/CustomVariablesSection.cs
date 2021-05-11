using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    /// <summary>
    /// Custom ConfigurationSection for Variables
    /// </summary>
    public class CustomVariablesSection : ConfigurationSection
    {
        public static CustomVariablesSection GetConfig()
        {
            return (CustomVariablesSection)ConfigurationManager.GetSection("CustomVariables") ?? new CustomVariablesSection();
        }

        [ConfigurationProperty("Variables")]
        [ConfigurationCollection(typeof(Variables), AddItemName = "Variable")]
        public Variables Variables
        {
            get
            {
                return (Variables)this[nameof(Variables)];
            }
        }      
    }
}
