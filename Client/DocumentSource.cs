using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    public class DocumentSource
    {
        public string Id { get; set; }
        public string DisplayName { get; set; }
        public List<PropertyMapping> Properties { get; set; }
        public List<DocumentCategory> Categories { get; set; }

        public class PropertyMapping
        {
            public string Key { get; set; }
            public string Type { get; set; }
            public string DisplayName { get; set; }
        }
    }
}
