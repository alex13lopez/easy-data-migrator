using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConectorSLIM4.modules
{
    public class TableMap
    {
        private string _mapId;
        private string _originTable;
        private string _destinationTable;

        public string MapId { get => _mapId; set => _mapId = value; }
        public string FromTable { get => _originTable; set => _originTable = value; }
        public string ToTable { get => _destinationTable ; set => _destinationTable  = value; }        

        private readonly List<FieldMap> _fieldMaps = new();

        public List<FieldMap> FieldMaps { get => _fieldMaps; }

        public void AddFieldMap(FieldMap fieldMap)
        {
            FieldMap fullFieldName = new()
            {
                OriginField = fieldMap.OriginField.Contains(_originTable) ? fieldMap.OriginField : _originTable + "." + fieldMap.OriginField,
                DestinationField = fieldMap.DestinationField.Contains(_destinationTable) ? fieldMap.DestinationField : _destinationTable + "." + fieldMap.DestinationField,
            };

            _fieldMaps.Add(fullFieldName);
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
