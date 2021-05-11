using System;
using System.Data;
using System.Linq;
using System.Data.SqlClient;
using System.Collections.Generic;
using System.Configuration;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.IO;
using System.Text.RegularExpressions;

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

        public void SaveMaps(string fileName, bool fullPath = false)
        {
            string filePath;

            if (!fullPath)
            {
                filePath = ConfigurationManager.AppSettings["MapsLocation"];
            
                if (string.IsNullOrWhiteSpace(filePath))
                    filePath = @".\Maps\" ;
            }
            else
            {
                filePath = Path.GetDirectoryName(fileName);
            }

            bool filenameIsValidated = ValidateFileName(fileName);

            if (filenameIsValidated)
            {
                fileName = @".\Maps\" + fileName + ".tablemaps";
            }

            if (!fullPath && !filenameIsValidated)
                throw new ArgumentOutOfRangeException(nameof(fileName), $"The file name specified is not valid. It can only contain numbers, letters, dashes, underscores and have a maximum length of 200 characters (Extension will be added automatically).{Environment.NewLine}Please if it is a path use -f|--fullpath.");
            else if (fullPath && !ValidateFullPath(fileName))
                throw new ArgumentOutOfRangeException(nameof(fileName), $"The file path specified is not valid. It can only contain numbers, letters, dots, dashes and underscores, colons, slashes, backslashes and have a maximum length of 255 characters (You may or may not use extension for your files).{Environment.NewLine}");            

            byte[] dataBytes;
            JsonSerializerOptions options = new() { WriteIndented = true };

            dataBytes = JsonSerializer.SerializeToUtf8Bytes<List<TableMap>>(_tableMaps, options);
            try
            {
                File.WriteAllBytes(fileName, dataBytes);
            }
            catch (DirectoryNotFoundException)
            {
                Directory.CreateDirectory(filePath);
                SaveMaps(fileName, true); // Here we pass true to fullPath because even though the user provided only a fileName, we converted it to fullpath by adding the default path ".\Maps"
            }
        }

        public static bool ValidateFileName(string fileName)
        {
            // File names can only contain numbers, letters, dashes, underscore and have a maximum length of 200 characters.
            string pattern = @"^([a-zA-Z0-9\-_]){1,200}$";
            return stringIsValid(fileName, pattern); 
        }

        public static bool ValidateFullPath(string fullPath) 
        {
            // File paths can only contain numbers, letters, dashes, underscores, slashes, backslashes, colons and have a maximum length of 255 characters.
            string pattern = @"^([a-zA-Z0-9\-_.:\\/ ]){1,255}$";
            return stringIsValid(fullPath, pattern);
        } 

        public static bool stringIsValid(string toValidate, string validationPattern) => new Regex(validationPattern).IsMatch(toValidate);

        public void LoadMaps(string fileName, bool fullPath = false)
        {
            bool filenameIsValidated = ValidateFileName(fileName);

            if (filenameIsValidated)
            {
                fileName = @".\Maps\" + fileName + ".tablemaps";
            }

            if (!fullPath && !filenameIsValidated)
                throw new ArgumentOutOfRangeException(nameof(fileName), $"The file name specified is not valid. It can only contain numbers, letters, dashes, underscores and have a maximum length of 200 characters (Extension will be added automatically).{Environment.NewLine}Please if it is a path use -f|--fullpath.");
            else if (fullPath && !ValidateFullPath(fileName))
                throw new ArgumentOutOfRangeException(nameof(fileName), $"The file path specified is not valid. It can only contain numbers, letters, dots, dashes and underscores, colons, slashes, backslashes and have a maximum length of 255 characters (You may or may not use extension for your files).{Environment.NewLine}");


            string jsonString = File.ReadAllText(fileName);
            JsonSerializerOptions options = new() { AllowTrailingCommas = true, IncludeFields = true, PropertyNameCaseInsensitive = true };

            _tableMaps = JsonSerializer.Deserialize<List<TableMap>>(jsonString, options);

            return;
        }

    }
}
