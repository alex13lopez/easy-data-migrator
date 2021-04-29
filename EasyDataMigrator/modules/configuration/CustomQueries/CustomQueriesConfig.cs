﻿using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{
    /// <summary>
    /// Custom configuration section class for Queries.
    /// </summary>
    public class CustomQueriesConfig : ConfigurationSection
    {
        public static CustomQueriesConfig GetConfig()
        {
            return (CustomQueriesConfig)ConfigurationManager.GetSection("CustomQueries") ?? new CustomQueriesConfig();
        }

        [ConfigurationProperty("Queries")]
        [ConfigurationCollection(typeof(Variables), AddItemName = "Query")]
        public Queries Queries
        {
            get
            {
                return (Queries)this[nameof(Queries)];
            }
        }
    }
}
