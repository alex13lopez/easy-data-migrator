using System;
using System.Configuration;


namespace EasyDataMigrator.Modules.Configuration
{
    [ConfigurationCollection(typeof(Query), AddItemName = "Query", CollectionType = ConfigurationElementCollectionType.BasicMap)]
    public class Queries : ConfigurationElementCollection
    {
        public Query this[int index]
        {
            get
            {
                return BaseGet(index) as Query;
            }

            set
            {
                if (BaseGet(index) != null)
                    BaseRemoveAt(index);

                BaseAdd(index, value);
            }
        }

        public new Query this[string responseString]
        {
            get => (Query)BaseGet(responseString);
            set
            {
                if (BaseGet(responseString) != null)
                {
                    BaseRemoveAt(BaseIndexOf(BaseGet(responseString)));
                }
                BaseAdd(value);
            }
        }

        protected override ConfigurationElement CreateNewElement() => new Query();

        protected override ConfigurationElement CreateNewElement(string elementName)
        {
            return new Query(elementName);
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Query)element).ID;
        }

    }
}
