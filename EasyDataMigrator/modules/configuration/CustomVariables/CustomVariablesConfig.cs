using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    /// <summary>
    /// Custom ConfigurationSection for Variables
    /// </summary>
    public class CustomVariablesConfig : ConfigurationSection
    {
        public static CustomVariablesConfig GetConfig()
        {
            return (CustomVariablesConfig)ConfigurationManager.GetSection("CustomVariables") ?? new CustomVariablesConfig();
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
