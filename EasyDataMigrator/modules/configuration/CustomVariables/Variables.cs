using System;
using System.Configuration;


namespace EasyDataMigrator.Modules.Configuration
{
    [ConfigurationCollection(typeof(Variable), AddItemName = "Variable", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class Variables : ConfigurationElementCollection
    {
        public Variable this[int index]
        {
            get
            {
                return BaseGet(index) as Variable;
            }

            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);

                BaseAdd(index, value);
            }
        }

        public new Variable this[string responseString]
        {
            get => (Variable)BaseGet(responseString);
            set
            {
                if (BaseGet(responseString) != null)
                {
                    BaseRemoveAt(BaseIndexOf(BaseGet(responseString)));
                }
                BaseAdd(value);
            }
        }

        protected override ConfigurationElement CreateNewElement() => new Variable();

        protected override ConfigurationElement CreateNewElement(string elementName)
        {
            //return base.CreateNewElement(elementName);
            return new Variable(elementName);
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Variable)element).Name;
        }
    }
}
