using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    public class DocumentCategory
    {
        public string Key { get; set; }
        public string DisplayName { get; set; }

        public override string ToString()
        {
            return $"{DisplayName} ({Key})";
        }
    }
}
