using System;
using ConectorSLIM4.modules;

namespace ConectorSLIM4
{
    class ConectorSLIM
    {
        static void Main(string[] args)
        {
            Mapper mapper = new();
            DbConnector origConnection = new("DataDecConnectionString"), destConnection = new("SLIMConnectionString");

            origConnection.OpenConnection();
            destConnection.OpenConnection();
            mapper.TryAutoMapping(origConnection, destConnection, "SLIM_");
            origConnection.CloseConnection();
            destConnection.CloseConnection();

            foreach (TableMap tableMap in mapper.TableMaps)
            {
                Console.WriteLine($"MapId: {tableMap.MapId}");
                Console.WriteLine($"FromTable: {tableMap.FromTable}");
                Console.WriteLine($"ToTable: {tableMap.ToTable}");
                Console.WriteLine("");
                Console.WriteLine("Mapped fields:");
                tableMap.FieldMaps.ForEach(fieldMap => Console.WriteLine($"FromField: {fieldMap.OriginField} to DestinationField: {fieldMap.DestinationField}"));
                Console.WriteLine("");
            }

            Console.ReadKey();
        }
    }
}
