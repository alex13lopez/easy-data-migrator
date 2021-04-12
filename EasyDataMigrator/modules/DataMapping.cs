using System;
using System.Collections.Generic;
using System.Configuration;
using System.Data.SqlClient;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace EasyDataMigrator.modules
{
    public class TableMap
    {
        private string _mapId;
        private string _originTable;
        private string _destinationTable;
        private string _originServer;
        private string _destinationServer;

        // If useBulkCopy is active, we will load the table into memory and then we will copy to destination server
        // This is used for tables that contain a lot of records so we can load them be doing several inserts instead of only one
        private bool _useBulkCopy;

        public string MapId { get => _mapId; private set => _mapId = value; }
        public string OriginServer { get => _originServer; private set => _originServer = value; }
        public string OriginDataBase { get; private set; }
        public string DestinationServer { get => _destinationServer; private set  => _destinationServer = value; }
        public string DestinationDataBase { get; private set; }
        public string FromTable { get => _originTable; private set => _originTable = OriginServer + "." + OriginDataBase + ".dbo." + value; }
        public string ToTable { get => _destinationTable ; private set => _destinationTable  = DestinationServer + "." + DestinationDataBase + ".dbo." + value; }
        public string FromTableName { get; private set; }
        public string ToTableName { get; private set; }        
        public bool UseBulkCopy { get => _useBulkCopy; set => _useBulkCopy = value; }
        public bool DestinationTableBusy { get; private set; }


        public TableMap(string originServer, string destinationServer, string originDB, string destinationDB,string fromTable, string toTable, bool useBulkCopy = false, bool destinationTableBusy = false)
        {            
            OriginServer = originServer;
            DestinationServer = destinationServer;
            OriginDataBase = originDB;
            DestinationDataBase = destinationDB;
            FromTable = fromTable;
            ToTable = toTable;
            FromTableName = fromTable;
            ToTableName = toTable;
            MapId = FromTable + '-' + ToTable;
            _useBulkCopy = useBulkCopy;
            DestinationTableBusy = destinationTableBusy;
        }

        private readonly List<FieldMap> _fieldMaps = new();

        public List<FieldMap> FieldMaps { get => _fieldMaps; }

        public void AddFieldMap(FieldMap fieldMap) => _fieldMaps.Add(fieldMap);

        /// <summary>
        /// Updates the Destination table status
        /// </summary>
        /// <returns>Destination table status</returns>
        public bool UpdateStatus()
        {
            if (!string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["GetTableIdQuery"]) && !string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["GetDestTableStatus"]))
            {
                DbConnector destConnection = new("DestinationConnection");
                string getTableIdSql = ConfigurationManager.AppSettings["GetTableIdQuery"];
                string getTableStatusSql = ConfigurationManager.AppSettings["GetDestTableStatus"];

                getTableIdSql = getTableIdSql.Replace("$TABLENAME", "'" + ToTableName + "'");

                destConnection.Open();
                string tableId = null;

                using (SqlDataReader reader = destConnection.ReadDB(getTableIdSql))
                {

                    if (reader.Read())
                    {
                        tableId = reader.GetString(0);
                    }
                }

                bool busy = true;
                if (tableId != null)
                {
                    getTableStatusSql = getTableStatusSql.Replace("$TABLEID", tableId);

                    using (SqlDataReader reader = destConnection.ReadDB(getTableStatusSql))
                    {
                        if (reader.Read())
                            busy = reader.GetInt32(0) == 1;
                    }
                }

                destConnection.Close();

                DestinationTableBusy = busy;

                return busy;
            }
            else if (string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["GetTableIdQuery"]) || string.IsNullOrWhiteSpace(ConfigurationManager.AppSettings["GetDestTableStatus"]))
            {
                // If there is no way of getting the table status we have no other choice but to assume it is not busy. This is dangerous and conflicts may arise but otherwise nothing will be migrated
                DestinationTableBusy = false;
                return false;
            }

            // If we cannot determine the status of the table, we'll asume it's busy
            DestinationTableBusy = true;
            return true; 
        }
    }

    public class FieldMap
    {
        public string OriginField { get; set; }
        public string DestinationField { get; set; }

        public FieldMap(string origField, string destField)
        {
            OriginField = origField;
            DestinationField = destField;
        }

        public FieldMap() { }

    }
}
