using ConectorSLIM4.modules;
using Microsoft.VisualStudio.TestTools.UnitTesting;
using System;

namespace ConectorSLIM4.UnitTests
{
    [TestClass]
    public class MapperTests
    {
        [TestMethod]
        public void TryAutoMapping_HasMaps()
        {
            // Arrange
            Mapper mapper = new();
            DbConnector origConnection = new("DataDecConnectionString"), destConnection = new("SLIMConnectionString");

            // Act
            mapper.TryAutoMapping(origConnection, destConnection, "SLIM_");

            // Assert
            Assert.IsTrue(mapper.TableMaps.Count > 0);

            foreach (TableMap tableMap in mapper.TableMaps)
            {
                Console.WriteLine($"MapId: {tableMap.MapId}");
                Console.WriteLine($"FromTable: {tableMap.FromTable}");
                Console.WriteLine($"ToTable: {tableMap.ToTable}");
                Console.WriteLine("");
                Console.WriteLine("Mapped fields:");

                Assert.IsTrue(tableMap.FieldMaps.Count > 0);
                tableMap.FieldMaps.ForEach(fieldMap => Console.WriteLine($"FromField: ${fieldMap.OriginField} to DestinationField: {fieldMap.DestinationField}"));

            }
        }
    }
}
