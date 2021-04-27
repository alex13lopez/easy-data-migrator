using System;
using System.Configuration;

namespace EasyDataMigrator.Modules.Configuration
{

    public class Query : ConfigurationElement
    {
        private string _sql;
        private string _id;
        private QueryConnection _connection;
        private QueryExecutionTime _executionTime;
        private QueryType _type;
        private string _storeIn;
        private int _executionOrder;

        public Query(string _ID)
        {
            ID = _ID;
            InitQuery();
        }

        public Query()
        {
            InitQuery();
        }

        public enum QueryType
        {
            Read,
            Execute,
            Null
        }

        public enum QueryExecutionTime
        {
            BeforeMigration, // It will be executed Before starting the migration of tables
            AfterMigration,  // It will be executed After ending the migration of tables
            BeforeTableMigration,  // It will be executed Before each table migration
            AfterTableMigration, // It will be executed After each table migration
            Null
        }

        public enum QueryConnection
        {
            OriginConnection,
            DestinationConnection,
            Null
        }

        [ConfigurationProperty("connection", IsRequired = true)]
        public QueryConnection Connection
        {
            get
            {
                if (_connection == QueryConnection.Null)
                    _connection = (QueryConnection)this["connection"];

                return _connection;
            }

            private set
            {
                _connection = value;
            }
        }

        [ConfigurationProperty("sqlCommand", IsRequired = true)]
        public string Sql
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_sql))
                    _sql = this["sqlCommand"] as string;

                return _sql;
            }

            set
            {
                _sql = value;
            }
        }

        [ConfigurationProperty("type", IsRequired = true, DefaultValue = QueryType.Execute)]
        public QueryType Type
        {
            get
            {
                if (_type == QueryType.Null)
                    _type = (QueryType)this["type"];

                return _type;
            }

            private set
            {
                _type = value;
            }
        }

        [ConfigurationProperty("executionTime", IsRequired = true)]
        public QueryExecutionTime ExecutionTime
        {
            get
            {
                if (_executionTime == QueryExecutionTime.Null)
                    _executionTime = (QueryExecutionTime)this["executionTime"];

                return _executionTime;
            }

            private set
            {
                _executionTime = value;
            }
        }

        [ConfigurationProperty("id", IsRequired = true, IsKey = true)]
        public string ID
        {
            get
            {
                if (string.IsNullOrWhiteSpace(_id))
                    _id = this["id"] as string;

                return _id;
            }

            private set
            {
                _id = value;
            }
        }

        [ConfigurationProperty("storeIn")]
        public string StoreIn
        {
            get
            {
                if (_storeIn == null)
                    _storeIn = this["storeIn"] as string;

                return _storeIn;
            }
            private set
            {
                _storeIn = value;
            }
        }

        [ConfigurationProperty("executionOrder", IsRequired = true, DefaultValue = 0)]
        public int ExecutionOrder
        {
            get
            {
                if (_executionOrder == -1)
                    _executionOrder = (int)this["executionOrder"];

                return _executionOrder;
            }

            private set
            {
                _executionOrder = value;
            }
        }

        public string OriginalID { get; set; }

        public Query Clone()
        {
            Query clone = new();
            clone.Connection = Connection;
            clone.Sql = Sql;
            clone.Type = Type;
            clone.ExecutionTime = ExecutionTime;
            clone.ID = ID + "_clone";
            clone.StoreIn = StoreIn;
            clone.ExecutionOrder = ExecutionOrder;
            clone.OriginalID = ID;
            return clone;
        }

        private void InitQuery()
        {
            _executionOrder = -1;
            _executionTime = QueryExecutionTime.Null;
            _type = QueryType.Null;           
        }
    }
}
