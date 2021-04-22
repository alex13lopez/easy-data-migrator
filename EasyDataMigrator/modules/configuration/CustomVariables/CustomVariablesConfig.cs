using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    public class CustomVariablesConfig : ConfigurationSection
    {
        public static CustomVariablesConfig GetConfig()
        {
            return (CustomVariablesConfig)System.Configuration.ConfigurationManager.GetSection("CustomVariables") ?? new CustomVariablesConfig();
        }

        [System.Configuration.ConfigurationProperty("Variables")]
        [ConfigurationCollection(typeof(Variables), AddItemName = "Variable")]
        public Variables Variables
        {
            get
            {
                object o = this["Variables"];
                return o as Variables;
            }
        }
    }
}
