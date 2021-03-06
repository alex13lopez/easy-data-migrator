using System.Collections.Generic;

namespace EasyDataMigrator.Modules.Core
{
    /// <summary>
    /// One of the main core classes, this class is the one used to Map two tables with their fields and such.
    /// </summary>
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
       
        private List<FieldMap> _fieldMaps = new();

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
        public List<FieldMap> FieldMaps { get => _fieldMaps; private set => _fieldMaps = value; }


        public TableMap(string originServer, string destinationServer, string originDataBase, string destinationDataBase, string fromTableName, string toTableName, bool useBulkCopy = false, bool destinationTableBusy = false, List<FieldMap> fieldMaps = null)
        {            
            OriginServer = originServer;
            DestinationServer = destinationServer;
            OriginDataBase = originDataBase;
            DestinationDataBase = destinationDataBase;
            FromTableName = fromTableName;
            ToTableName = toTableName;
            FromTable = fromTableName;
            ToTable = toTableName;
            MapId = FromTable + '-' + ToTable;
            UseBulkCopy = useBulkCopy;
            DestinationTableBusy = destinationTableBusy;

            if (fieldMaps != null)
                FieldMaps = fieldMaps;
        }       

        public void AddFieldMap(FieldMap fieldMap) => _fieldMaps.Add(fieldMap);
    }

    /// <summary>
    /// Another one of the main core classes, this class is the one used to Map the fields of the tables.
    /// </summary>
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
