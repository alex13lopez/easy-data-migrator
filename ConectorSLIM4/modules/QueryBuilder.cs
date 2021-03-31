using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConectorSLIM4.modules
{
    static class QueryBuilder
    {
        public static string Insert(TableMap TableMap)
        {
            // Insert Into destination
            string query = "INSERT INTO " + TableMap.ToTable + "(";
            TableMap.FieldMaps.ForEach(map => query += map.DestinationField + ",");
            query = query.Remove(query.Length - 1); // We remove last comma
            query += ") ";

            // We select the data to insert from origin
            query += Select(TableMap);

            return query;
        }

        public static string Delete(TableMap TableMap) => "DELETE FROM " + TableMap.ToTable;

        public static string Select(TableMap TableMap)
        {
            // We select the data to insert from origin
            string query  = "SELECT ";
            TableMap.FieldMaps.ForEach(map => query += map.OriginField + ",");
            query = query.Remove(query.Length - 1); // We remove last comma
            query += " FROM " + TableMap.FromTable;

            return query;
        }
    }
}
