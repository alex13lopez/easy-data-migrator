using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    public class Variable : ConfigurationElement
    {
        private Type _type;
        private string _name;
        private string _value;
        private dynamic _trueValue;

        [ConfigurationProperty("name", IsRequired = true, IsKey = true)]
        public string Name
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_name))
                    _name = this["name"] as string;

                return _name;
            }

            private set
            {
                _name = value;
            }
        }

        [ConfigurationProperty("value",
            DefaultValue = "",
            IsRequired = false)]
        public string Value
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_value))
                    _value = this["value"] as string;

                return _value;
            }

            set
            {
                _value = value;
            }
        }

        public dynamic TrueValue
        {
            get => _trueValue; 
            set
            {
                _trueValue = value;
                
                if (Type != typeof(string))
                    _value = Convert.ToString(value); // We cascade-update the value
                else
                    _value = value;
            }
        }

        [ConfigurationProperty("type",
            DefaultValue = "string",
            IsRequired = true)]
        private string _Type
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
                Type type = _Type switch
                {
                    "int" => typeof(int),
                    "bigint" => typeof(long),
                    "long" => typeof(long),
                    "float" => typeof(float),
                    "decimal" => typeof(decimal),
                    "money" => typeof(decimal),
                    "boolean" => typeof(bool),
                    "bool" => typeof(bool),
                    _ => typeof(string) // If we do not recognize the type we return string by default
                };

                if (_type == null)
                    _type = type;
                
                return _type;
            }
            set => _type = value;
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
