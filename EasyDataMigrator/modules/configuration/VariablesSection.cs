using System;
using System.Configuration;
using System.Collections;

namespace EasyDataMigrator.modules.configuration
{
    class VariablesSection : ConfigurationSection
    {
        [ConfigurationProperty("name",
            DefaultValue = "CustomVariables",
            IsRequired = true,
            IsKey = false)]
        [StringValidator(InvalidCharacters =
            " ~!@#$%^&*()[]{}/;'\"|\\",
            MinLength = 1, MaxLength = 60)]
        public string Name
        {
            get
            {
                return (string) this["name"];
            }

            set 
            {
                this["name"] = value;
            }
        }

        [ConfigurationProperty("CustomVariables",
            IsDefaultCollection = false)]
        public VariablesCollection CustomVariables
        {
            get
            {
                VariablesCollection VarsCollection =
                (VariablesCollection)base["urls"];
                return VarsCollection;
            }
        }

        protected override void DeserializeSection(System.Xml.XmlReader reader)
        {
            base.DeserializeSection(reader);
        }

        protected override string SerializeSection(ConfigurationElement parentElement, string name, ConfigurationSaveMode saveMode)
        {
            string s =
                base.SerializeSection(parentElement,
                name, saveMode);
            return s;
        }
    }
}
