using Newtonsoft.Json;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    public class DocumentSearchResult
    {
        public string Id { get; set; }

        [JsonProperty("_links")]
        public DocumentLinks Links { get; set; }
        public List<DocumentSearchResultProperty> SourceProperties { get; set; }
        public List<string> SourceCategories { get; set; }

        public override string ToString()
        {
            return Id;
        }
    }
}
