using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
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
                //object o = this[nameof(Variables)];
                //return o as Variables;
                return (Variables)this[nameof(Variables)];
            }
        }      
    }
}
