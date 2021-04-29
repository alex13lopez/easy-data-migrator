using System;
using System.Collections.Generic;
using System.Configuration;


namespace EasyDataMigrator.Modules.Configuration
{
    /// <summary>
    /// Custom Configuration collection of Variables.
    /// </summary>
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
            return new Variable(elementName);
        }

        protected override object GetElementKey(ConfigurationElement element)
        {
            return ((Variable)element).Name;
        }

        public Variable Find(Predicate<Variable> match)
        {
            foreach (Variable variable in this)
            {
                if (match.Invoke(variable))
                    return variable;
            }

            return null;
        }

        public List<Variable> FindAll(Predicate<Variable> match)
        {
            List<Variable> foundVars = new();

            foreach (Variable variable in this)
            {
                if (match.Invoke(variable))
                    foundVars.Add(variable);
            }

            return foundVars;
        }

        public void ForEach(Action<Variable> action)
        {
            foreach (Variable var in this)
            {
                action.Invoke(var);
            }
        }
    }
}
