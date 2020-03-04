using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    public class DocumentLinks
    {
        public Link Self { get; set; }
        public Link Versions { get; set; }
        public Link MainBlobContent { get; set; }
        public Link UpdateWithContent { get; set; }
        public Link Update { get; set; }
        public Link Children { get; set; }


        public void CompeteFrom(DocumentLinks template)
        {
            if (this.Self == null)
            {
                this.Self = template.Self;
            }
            if (this.Versions == null)
            {
                this.Versions = template.Versions;
            }
            if (this.MainBlobContent == null)
            {
                this.MainBlobContent = template.MainBlobContent;
            }
            if (this.UpdateWithContent == null)
            {
                this.UpdateWithContent = template.UpdateWithContent;
            }
            if (this.Update == null)
            {
                this.Update = template.Update;
            }
            if (this.Children == null)
            {
                this.Children = template.Children;
            }
        }

        public class Link
        {
            public string HRef { get; set; }

            public override string ToString()
            {
                return HRef;
            }
        }
    }
}
