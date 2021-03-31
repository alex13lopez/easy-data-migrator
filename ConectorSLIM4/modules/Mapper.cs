using System.Linq;
using System.Collections.Generic;
using System.Data.SqlClient;
using System.Configuration;
using System.Data;
using System;

namespace ConectorSLIM4.modules
{
    public class Mapper
    {
        private List<TableMap> _tableMaps = new();

        public List<TableMap> TableMaps { get => _tableMaps; private set => _tableMaps = value; }

        public void TryAutoMapping(DbConnector originConnection, DbConnector destinationConnection, string originPatterMatching = null, string destinationPatternMatching = null, bool excludePatternsFromMatch = true)
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


            CreateTableMaps(originTable,
                            destTable,
                            originConnection.ServerName,
                            destinationConnection.ServerName,
                            originConnection.DataBaseName,
                            destinationConnection.DataBaseName,
                            originPatterMatching,
                            destinationPatternMatching,
                            excludePatternsFromMatch
                            );

            CreateFieldMaps(originTable, destTable);                        
        }

        private float CreateTableMaps(DataTable originTable, DataTable destTable, string originServer, string destinationServer, string originDB, string destinationDB, string originPatterMatching = null, string destinationPatternMatching = null, bool excludePatternsFromMatch = true)
        {
            IEnumerable<string> origTables = originTable.AsEnumerable().Select(r => r.Field<string>("TableName")).Distinct();
            IEnumerable<string> destTables = destTable.AsEnumerable().Select(r => r.Field<string>("TableName")).Distinct();

            int matchedCount = 0, mapCount = 0;                        
            foreach (string oTableName in origTables.ToList())
            {
                foreach (string dTableName in destTables.ToList())
                {
                    string oTableNameF = oTableName, dTableNameF = dTableName;
                    mapCount++;

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
                        _ = FindOrCreateTableMap(new TableMap(originServer, destinationServer, originDB, destinationDB, oTableName, dTableName));
                    }
                }
            }

            return matchedCount / mapCount; // We return the success percentage
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

        private void CreateFieldMaps(DataTable originTable, DataTable destTable)
        {
            
            foreach (TableMap tableMap in _tableMaps)
            {
                List<string> oFields = (from f in originTable.AsEnumerable()
                                       where f.Field<string>("TableName") == tableMap.FromTableName
                                       select f.Field<string>("ColumnName")).ToList();

                List<string> dFields = (from f in destTable.AsEnumerable()
                                        where f.Field<string>("TableName") == tableMap.ToTableName
                                        select f.Field<string>("ColumnName")).ToList();

                foreach (string oField in oFields)
                    foreach (string dField in dFields)
                        if (oField.ToLower() == dField.ToLower()) // We compare names case insensitive
                            tableMap.AddFieldMap(new FieldMap(oField, dField));
            }
            
        }        
    }
}
