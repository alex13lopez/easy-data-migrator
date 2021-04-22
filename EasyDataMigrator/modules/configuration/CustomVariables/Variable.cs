using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    public class Variable : ConfigurationElement
    {
        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get
            {
                return this["name"] as string;
            }

            private set
            {
                this["name"] = value;
            }
        }

        [ConfigurationProperty("value",
            DefaultValue = "",
            IsRequired = false)]
        public string Value
        {
            get
            {
                return this["value"] as string;
            }

            set
            {
                this["value"] = value;
            }
        }

        [ConfigurationProperty("type",
            DefaultValue = "string",
            IsRequired = true)]
        public string Type
        {
            get
            {
                return this["type"] as string;
            }

            set
            {
                this["type"] = value;
            }
        }

        public Variable()
        {

        }

        public Variable(string elementName)
        {
            Name = elementName;
        }
    }
}
