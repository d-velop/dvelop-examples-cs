using DMSAPISamples.Client;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DMSAPISamples
{
    class Program
    {
        static void Main(string[] args)
        {
            //These samples refer to the RESTful interfaces of the d.ecs apps DMSApp and IdentityProviderApp which 
            //are contained in d.3one. Cloud and on-premises instances can be used both.

            //First of all: These samples uses current api versions. Please use the d.velop cloud or download current 
            //versions for on-premises instances.
            //https://portal.d-velop.de/products/de/d-3ecm-services/d-velop-software-manager
            //https://portal.d-velop.de/products/de/d-3ecm-core-components/d-3one


            //Follow these steps before running the code:

            //ONE: you need the base uri of your d.3one instance. To get an instance in the d.velop cloud and to 
            //choose a base uri, click here: https://store.d-velop.de/9/d.velop-documents
            var baseURI = @"https://xxxxxxxxxxx.d-velop.cloud";

            //TWO: get an api key for your user in your d.3one instance: In d.one, click on tile "IdentityProvider", 
            //then on the "fingerprint icon" on the right.
            var apiKey = @"xxxxxxxxxxxxxx";

            //THREE: get a repository id from your d.3one instance: In d.3one, click on tile "Search", then select 
            //the repository from the combobox on the top, copy the repository id form the browser URL
            var repoId = @"00000000-0000-0000-0000-000000000000";

            //FOUR: grant user rights: In d.3one click on tile "Usermanagement", select your user and assign the user group 
            //"Kundenakte Vollzugriff". Then log off and sign in.

            //FIVE (optional): Provide your own source or proceed with default source "Basisdokumentarten" contained 
            //in d.velop documents and step 6.

            //SIX: create a mapping between source and content of your d.velop cloud: In d.3one, click on tile "Mappings" and then on 
            //"create new". Select the source (default: "Basisdokumentarten") and map categories and properties.
            //Category: "Schriftverkehr Kunde" -> "Schriftverkehr Kunde"
            //Properties: "Betreff" -> "Betreff (100048)", "Kunden-Nummer" -> "Kunden-Nummer (100124)", "Kunden-Name1" -> "Kunden-Name1 (100147)",
            //"Beleg-Datum" -> "Beleg-Datum (100046)", "Dateiname" -> "Dateiname (nur lesend)"

            //SEVEN: copy sample files from project folder "sample files" to local directory and set filePath to this directory
            var filePath = @"C:\Temp\sample files";

            //EIGHT: run this code.

            //url parameters for search sample
            //var searchFor = "{\"belegdatum\":[\"2018-03-11\"]}";
            //json metadata for document upload sample 
            var uploadMappingFile = Path.Combine(filePath, "upload.json");

            //upload file and download path
            var uploadFile = Path.Combine(filePath, "upload.pdf");
            var downloadFilePath = Path.Combine(filePath, "download");

            // category with your test documents
            var categoryKey = "my_category";

            //Important: Tls 1.0 is no longer supported! Please use the next line of code for compatibilty settings or 
            //select the most recent .NET Framework for this project!
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            // initialize client
            DocumentClient client = new DocumentClient(baseURI, apiKey);

            // select current repository
            client.RepositoryId = repoId;

            // get available document categories from source
            DocumentSource source = client.GetSourceInfo();
            DocumentCategory category = source.Categories.Where(c => c.Key == categoryKey).First();

            // adapt upload mapping to requirements of valid source
            DocumentUploadMapping uploadMapping = JsonConvert.DeserializeObject<DocumentUploadMapping>(File.ReadAllText(uploadMappingFile));
            uploadMapping.SourceId = source.Id;
            uploadMapping.SourceCategory = category.Key;
            CompleteProperties(source, uploadMapping);

            try
            {
                //upload file as a whole or chunked
                var documentLink = client.UploadFile(uploadFile, uploadMapping).Result;
                // var documentLink = UploadFileChunked(baseURI, sessionId, repoId, uploadFile, uploadMappingFile).Result;
                //show uploaded document in d.3one
                System.Diagnostics.Process.Start(baseURI + documentLink);
                //get download url and document properties (returned properties are defined in the source mapping, see above)
                var documentInfo = client.GetDocumentInfo(documentLink);
                //download document
                bool downloaded = client.DownloadDocument(documentInfo, downloadFilePath).Result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Cannot upload document. {ex.Message}");
            }

            //search the new document - should find empty search result, if upload failed
            string searchForKey = source.Properties[0].Key;
            string searchForValue = uploadMapping.SourceProperties.Properties.Where(p => p.Key == searchForKey).FirstOrDefault().Values[0];
            string searchFor = $"{{\"{searchForKey}\":[\"{searchForValue}\"]}}";
            var result = client.SearchDocument(source.Id, category.Key, searchFor);
            Console.WriteLine(result);

            //search all documents in the category
            result = client.SearchDocument(source.Id, category.Key);
            Console.WriteLine(result);

            // get document list from search result
            var documents = ParseSearchResult(result);

            if (documents.Count > 0)
            {
                // edit first document from search result
                var document = documents[0];
                EditDocument(source, document);

                // upload changes
                string updateResponse = client.PutDocumentInfo(document, source.Id);

                if (string.IsNullOrWhiteSpace(updateResponse))
                {
                    Console.WriteLine("Document updated");
                }
                else
                {
                    Console.WriteLine($"Update document failed: '{updateResponse}'");
                }
            }

            Console.ReadKey();
        }

        private static List<DocumentSearchResult> ParseSearchResult(string documentsJson)
        {
            JObject documents = JsonConvert.DeserializeObject<JObject>(documentsJson);
            IEnumerable<JToken> items = documents["items"].Children();
            List<DocumentSearchResult> result = new List<DocumentSearchResult>();

            foreach (var itemToken in items)
            {
                var item = itemToken.ToObject<DocumentSearchResult>();
                result.Add(item);
                Console.WriteLine($"Found document '{item.Id}'");
            }

            return result;
        }

        private static void EditDocument(DocumentSource source, DocumentSearchResult document)
        {
            // get string properties
            var stringPropertyKeys = source.Properties.Where(p => p.Type == "String").Select(p => p.Key);

            // change value of first string property
            DocumentSearchResultProperty property = document.SourceProperties.Where(p => stringPropertyKeys.Contains(p.Key)).FirstOrDefault();
            property.Value += $" TEST {DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss")}";
        }

        /// <summary>
        /// Adds dummy values for missing required properties to the document mapping.
        /// </summary>
        private static void CompleteProperties(DocumentSource source, DocumentUploadMapping mapping)
        {
            foreach (var property in source.Properties)
            {
                // property is well defined
                if (!string.IsNullOrWhiteSpace(property.Type))
                {
                    // property is missing in upload mapping
                    if (!mapping.SourceProperties.Properties.Where(p => p.Key == property.Key).Any())
                    {
                        // add property
                        if (property.Type.ToLower() == "string")
                        {
                            mapping.AddProperty(property.Key, $"Test {property.DisplayName}");
                        }
                        else if (property.Type.ToLower().StartsWith("date"))
                        {
                            mapping.AddProperty(property.Key, DateTime.UtcNow.ToString("yyyy-MM-dd"));
                        }
                        else
                        {
                            mapping.AddProperty(property.Key, "0");
                        }
                    }
                }
            }
        }
    }
}
