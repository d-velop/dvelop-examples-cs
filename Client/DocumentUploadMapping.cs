using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    // https://developer.d-velop.de/documentation/dmsap/de/dms-api-126976273.html#DMS-App-DefinierenderParameterzumSpeichern
    public class DocumentUploadMapping
    {
        //public string AlterationText { get; set; }
        public string SourceCategory { get; set; }
        public string SourceId { get; set; }
        public string FileName { get; set; }
        public string ContentLocationUri { get; set; }
        public DocumentSourceProperties SourceProperties { get; set; } = new DocumentSourceProperties();

        public void AddProperty(string key, string value)
        {
            SourceProperties.Properties.Add(new DocumentSourceProperty() { Key = key, Values = new List<string>() { value } });
        }

        public void AddProperties(List<DocumentSearchResultProperty> documentProperties)
        {
            foreach(var property in documentProperties)
            {
                SourceProperties.Properties.Add(new DocumentSourceProperty() { Key = property.Key, Values = new List<string>() { property.Value } });
            }
        }
    }
}
