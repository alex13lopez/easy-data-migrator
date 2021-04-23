using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{    

    public class Query : ConfigurationElement
    {        
        public Query(string _ID)
        {
            ID = _ID;
        }

        public Query()
        {

        }

        public enum QueryType
        {
            Read,
            Execute,
            NonValid
        }

        public enum QueryExecutionTime
        {
            BeforeMigration, // It will be executed Before starting the migration of tables
            AfterMigration,  // It will be executed After ending the migration of tables
            BeforeTableMigration,  // It will be executed Before each table migration
            AfterTableMigration, // It will be executed After each table migration
        }

        public enum QueryConnection
        {
            OriginConnection,
            DestinationConnection,
            NonValid
        }

        [ConfigurationProperty("connection", IsRequired = true)]
        public QueryConnection Connection 
        {
            get 
            {
                return (QueryConnection)this["connection"];                               
            }
        }

        [ConfigurationProperty("sqlCommand", IsRequired = true)]
        public string Sql 
        { 
            get
            {
                return this["sqlCommand"] as string;
            }
        }

        [ConfigurationProperty("type", IsRequired = true, DefaultValue = QueryType.Execute)]
        public QueryType Type 
        { 
            get 
            {
                return (QueryType)this["type"];                
            } 
        }

        [ConfigurationProperty("executionTime", IsRequired = true)]
        public QueryExecutionTime ExecutionTime { get; set; }

        [ConfigurationProperty("id", IsRequired = true, IsKey = true)]
        public string ID 
        { 
            get
            {
                return this["id"] as string;
            }

            private set
            {
                this["id"] = value;
            }
        }

        [ConfigurationProperty("storeIn")]
        public string StoreIn
        {
            get
            {
                return this["storeIn"] as string;
            }
        }
    }
}
