using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    public class DocumentSearchResultProperty
    {
        public string Key { get; set; }
        public string Value { get; set; }
        public string DisplayValue { get; set; }
        public bool IsMultiValue { get; set; }

        public override string ToString()
        {
            return $"{Key} = {Value}";
        }
    }
}
