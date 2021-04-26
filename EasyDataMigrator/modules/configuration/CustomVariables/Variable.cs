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

        public dynamic TrueValue
        {
            get; set;
        }

        [ConfigurationProperty("type",
            DefaultValue = "string",
            IsRequired = true)]
        protected string _type
        {
            get
            {
                return (string)this["type"];
            }
        }

        public Type Type
        {
            get
            {
                Type type = _type switch
                {
                    "int" => typeof(int),
                    "bigint" => typeof(long),
                    "long" => typeof(long),
                    "float" => typeof(float),
                    "decimal" => typeof(decimal),
                    "money" => typeof(decimal),
                    _ => typeof(string),// If we do not recognize the type we return string by default
                };

                return type;
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
