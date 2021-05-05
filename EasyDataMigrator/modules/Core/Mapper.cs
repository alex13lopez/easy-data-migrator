using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System;

namespace EasyDataMigrator.Modules.Core
{
    /// <summary>
    /// Another of the big main core classes. This class is the one in charge of the AutoMapping feature of the program.
    /// </summary>
    public class Mapper
    {
        private List<TableMap> _tableMaps = new();
        public decimal TableMapPrecision { get; private set; }
        public decimal FieldMapPrecision { get; private set; }
        public List<TableMap> TableMaps { get => _tableMaps; private set => _tableMaps = value; }

        public void AutoMap(DbConnector originConnection, DbConnector destinationConnection, string originPatterMatching = null, string destinationPatternMatching = null, bool excludePatternsFromMatch = true)
        {
            string query = ConfigurationManager.AppSettings["GetTablesQuery"];
            SqlDataReader originReader;
            SqlDataReader destinationReader;
            DataTable originTable = new();
            DataTable destTable = new();

            // Data Origin Table
            if (!string.IsNullOrWhiteSpace(originPatterMatching))
                originReader = originConnection.ReadDB(query + $" AND t.Name like '%{originPatterMatching}%'");
            else
                originReader = originConnection.ReadDB(query);            
            
            if (originReader != null)
                originTable.Load(originReader);

            // Data Destination Table
            if (!string.IsNullOrWhiteSpace(destinationPatternMatching))
                destinationReader = destinationConnection.ReadDB(query + $" AND t.Name like '%{destinationPatternMatching}%'");
            else
                destinationReader = destinationConnection.ReadDB(query);

            if (destinationReader != null)
                destTable.Load(destinationReader);


            TableMapPrecision = CreateTableMaps(originTable,
                                                destTable,
                                                originConnection.ServerName,
                                                destinationConnection.ServerName,
                                                originConnection.DataBaseName,
                                                destinationConnection.DataBaseName,
                                                originPatterMatching,
                                                destinationPatternMatching,
                                                excludePatternsFromMatch
                                                );

            FieldMapPrecision = CreateFieldMaps(originTable, destTable);
        }

        private decimal CreateTableMaps(DataTable originTable, DataTable destTable, string originServer, string destinationServer, string originDB, string destinationDB, string originPatterMatching = null, string destinationPatternMatching = null, bool excludePatternsFromMatch = true)
        {
            IEnumerable<string> origTables = originTable.AsEnumerable().Select(r => r.Field<string>("TableName")).Distinct();
            IEnumerable<string> destTables = destTable.AsEnumerable().Select(r => r.Field<string>("TableName")).Distinct();

            int matchedCount = 0, mapCount = 0;                        
            foreach (string oTableName in origTables.ToList())
            {
                mapCount++;

                foreach (string dTableName in destTables.ToList())
                {
                    string oTableNameF = oTableName, dTableNameF = dTableName;                    

                    if (excludePatternsFromMatch)
                    {
                        if (!string.IsNullOrWhiteSpace(originPatterMatching))
                            oTableNameF = oTableNameF.Replace(originPatterMatching, "");

                        if (!string.IsNullOrWhiteSpace(destinationPatternMatching))
                            dTableNameF = dTableNameF.Replace(destinationPatternMatching, "");
                    }

                    if (oTableNameF == dTableNameF)
                    {
                        matchedCount++;
                        bool useBulkCopy = ConfigurationManager.AppSettings["UseBulkCopyTables"].Contains(oTableName);

                        _ = FindOrCreateTableMap(new TableMap(originServer, destinationServer, originDB, destinationDB, oTableName, dTableName, useBulkCopy));
                    }
                }
            }            

            return mapCount > 0 ? Math.Round(((decimal)matchedCount / (decimal)mapCount), 2) * 100 : 0; // We return the success percentage
        }

        internal void SaveMap(string saveToMap)
        {
            throw new NotImplementedException();
        }

        private TableMap FindOrCreateTableMap(TableMap tableMap)
        {
            TableMap tMap = _tableMaps.Find(t => t.MapId == tableMap.MapId);

            if (tMap == null)
            {
                _tableMaps.Add(tableMap);
                tMap = _tableMaps.Find(t => t.MapId == tableMap.MapId);                
            }            

            return tMap;
        }

        internal void LoadMap(string mapToUse)
        {
            throw new NotImplementedException();
        }

        private decimal CreateFieldMaps(DataTable originTable, DataTable destTable)
        {
            int matchedCount = 0, mapCount = 0;

            foreach (TableMap tableMap in _tableMaps)
            {
                List<string> oFields = (from f in originTable.AsEnumerable()
                                       where f.Field<string>("TableName") == tableMap.FromTableName
                                       select f.Field<string>("ColumnName")).ToList();

                List<string> dFields = (from f in destTable.AsEnumerable()
                                        where f.Field<string>("TableName") == tableMap.ToTableName
                                        select f.Field<string>("ColumnName")).ToList();

                foreach (string oField in oFields)
                {
                    mapCount++;
                    foreach (string dField in dFields)
                    {                        
                        if (oField.ToLower() == dField.ToLower()) // We compare names case insensitive
                        {
                            tableMap.AddFieldMap(new FieldMap(oField, dField));
                            matchedCount++;
                        }
                    }
                }
            }

            return mapCount > 0 ? Math.Round(((decimal)matchedCount / (decimal)mapCount), 2) * 100 : 0; // We return the success percentage
        }        
    }
}
