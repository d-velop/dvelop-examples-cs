using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    public class DocumentRepository
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string ServerId { get; set; }
        public string PublicKey { get; set; }
        public string Host { get; set; }
        public string Port { get; set; }
        public string Uri { get; set; }
        public bool IsAvailable { get; set; }
        public bool IsDefault { get; set; }
        public bool IsReachable { get; set; }
        public bool IsExternal { get; set; }

        public override string ToString()
        {
            return $"{Name} ({Id})";
        }
    }
}
