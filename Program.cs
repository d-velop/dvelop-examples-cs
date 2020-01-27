using Newtonsoft.Json;
using System;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DMSAPISamples
{
    class Program
    {
        private static string MEDIA_TYPE_HAL_JSON = "application/hal+json";
        private static string MEDIA_TYPE_OCTET_STREAM = "application/octet-stream";

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
            var baseURI = @"https://xxxxxxxxxx.d-velop.cloud";

            //TWO: get an api key for your user in your d.3one instance: In d.one, click on tile "IdentityProvider", 
            //then on the "fingerprint icon" on the right.
            var apiKey = @"xxxxxxxxxxxxxxxxxxxxxx";

            //THREE: get a repository id from your d.3one instance: In d.3one, click on tile "Search", then select 
            //the repository from the combobox on the top, copy the repository id form the browser URL
            var repoId = @"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";

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
            var filePath = Path.Combine(Path.GetTempPath(), "DMSAPISamples");

            //EIGHT: run this code.

            //url parameters for search sample
            var searchFor = "?sourceid=/dvelop/documents&sourcecategories=[\"schriftverkehrkunde\"]&sourceproperties={\"belegdatum\":[\"2018-03-11\"]}";
            //json metadata for document upload sample 
            var uploadMappingFile = Path.Combine(filePath, "upload.json");

            //upload file and download path
            var uploadFile = Path.Combine(filePath, "upload.pdf");
            var downloadFilePath = Path.Combine(filePath, "download");

            //Important: Tls 1.0 is no longer supported! Please use the next line of code for compatibilty settings or 
            //select the most recent .NET Framework for this project!
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            //using apiKey authentication is recommended! 
            //for all further api calls use the returned sessionId as Bearer-Token!
            var sessionId = Authenticate(baseURI, apiKey);

            if (null != sessionId)
            {
                //upload file as a whole or chunked
                var documentLink = UploadFile(baseURI, sessionId, repoId, uploadFile, uploadMappingFile).Result;
//                var documentLink = UploadFileChunked(baseURI, sessionId, repoId, uploadFile, uploadMappingFile).Result;
                //show uploaded document in d.3one
                System.Diagnostics.Process.Start(baseURI + documentLink);
                //get download url and document properties (returned properties are defined in the source mapping, see above)
                var documentInfo = GetDocumentInfo(baseURI, sessionId, repoId, documentLink).Result;
                //download document
                bool downloaded = DownloadDocument(baseURI, sessionId, repoId, documentInfo, downloadFilePath).Result;
                //search documents
                var result = SearchDocument(baseURI, sessionId, repoId, searchFor).Result;
            }
            Console.ReadKey();
        }

        private class AuthSessionInfoDto
        {
            public string AuthSessionId { get; set; }
            public DateTime Expire { get; set; }
        }

        //authenticate with user credentials and basic authentication
        private static string Authenticate(string baseURI, string apiKey)
        {
            var link_relation = "/identityprovider/login";
            var baseRequest = baseURI + link_relation;
            try
            {
                using (HttpClient client = new HttpClient())
                {
                    client.BaseAddress = new System.Uri(baseRequest);
                    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                    client.DefaultRequestHeaders.Add("Accept", MEDIA_TYPE_HAL_JSON);

                    var result = client.GetAsync(link_relation).Result;
                    if (result.IsSuccessStatusCode)
                    {
                        var authSessionInfo = JsonConvert.DeserializeObject<AuthSessionInfoDto>(result.Content.ReadAsStringAsync().Result);
                         
                        if (null != authSessionInfo.AuthSessionId)
                        {
                            Console.WriteLine("login ok: " + "expires: " + authSessionInfo.Expire + ", sessionId: " + authSessionInfo.AuthSessionId);
                            return authSessionInfo.AuthSessionId;
                        }
                    }
                    else
                    {
                        Console.WriteLine("login failed with status code \"" + result.StatusCode + "\": " + baseRequest);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("login failed with error \"" + ex.Message + "\": " + baseRequest);
            }
            return null;
        }

             
        //search document by given metadata
        private async static Task<string> SearchDocument(string baseURI, string sessionId, string repoId, string searchFor)
        {
            var link_relation = "/dms/r/" + repoId + "/srm";
            var baseRequest = baseURI + link_relation;

            using (HttpClient client = new HttpClient())
            {
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                baseRequest += searchFor;

                var result = await client.GetAsync(baseRequest).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    var jsonString = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine("search ok: " + baseRequest);

                    dynamic jsonResult = JsonConvert.DeserializeObject<object>(jsonString);
                    foreach (var p in jsonResult.items)
                    {
                        Console.WriteLine(p.id + "==== > " + p._links.self.href);
                        foreach (var prop in p.sourceProperties)
                        {
                            Console.WriteLine("   Key = " + prop.key + " value = " + prop.value);
                        }
                    }
                    return String.Empty;
                }
            }
            return String.Empty;
        }

        private async static Task<string> GetDocumentInfo(string baseURI, string sessionId, string repoId, string documentLink)
        {

            var link_relation = documentLink;
            var baseRequest = baseURI + link_relation;

            using (HttpClient client = new HttpClient())
            {
                //get download url
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                var result = await client.GetAsync(baseRequest).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    var jsonString = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    Console.WriteLine("getdocinfo ok: " + baseRequest);

                    return jsonString;
                }
            }
            return null;
        }

        private async static Task<bool> DownloadDocument(string baseURI, string sessionId, string repoId, string jsonDocumentInfo, string downloadFilePath)
        {
            dynamic documentInfo = JsonConvert.DeserializeObject<object>(jsonDocumentInfo);
            var link_relation =  documentInfo._links.mainblobcontent.href;
            var baseRequest = baseURI + link_relation;

            var fileName = "";
            foreach (var prop in documentInfo.sourceProperties)
            {
                if (prop.key == "dateiname")
                {
                    fileName = prop.value;
                    break;
                }
            }


            using (HttpClient client = new HttpClient())
            {
                //get download url
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                Console.WriteLine("start document download");
                var result = await client.GetAsync(baseRequest).ConfigureAwait(false); ;
                if (result.IsSuccessStatusCode)
                {
                    Stream stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    Directory.CreateDirectory(downloadFilePath);
                    string filePath = Path.Combine(downloadFilePath, fileName);
                    using (FileStream fs = new FileStream(filePath, FileMode.Create))
                    {
                        stream.CopyTo(fs);
                        fs.Flush();
                    }
                    Console.WriteLine("document downloaded to " + filePath);
                    return true;
                }
                else
                {
                    Console.WriteLine("document download failed");
                }
            }
            return false;
        }

        //upload file in a whole
        private async static Task<string> UploadFile(string baseURI, string sessionId, string repoId, string filePath, string uploadMappingFile)
        {
            var contentLocationUri = "/dms/r/" + repoId + "/blob/chunk";

            //first: upload file and get an URI (contentLocationUri) as a reference to this file
            contentLocationUri = await UploadFileChunk(baseURI, sessionId, contentLocationUri, filePath).ConfigureAwait(false); ;

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishFileUpload(baseURI, sessionId, repoId, contentLocationUri, filePath, uploadMappingFile).ConfigureAwait(false); ;
            if (null != documentLink)
            {
                return documentLink;
            }
            return string.Empty;
        }

        //upload file in chunks
        private async static Task<string> UploadFileChunked(string baseURI, string sessionId, string repoId, string filePath, string uploadMappingFile)
        {
            var path = Path.GetDirectoryName(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            var contentLocationUri = "/dms/r/" + repoId + "/blob/chunk";
            var index = 0;
            var chunkFilePath = Path.Combine(path, name + index);

            //first: upload file and get an URI (contentLocationUri) as a reference to this file
            while (File.Exists(chunkFilePath))
            {
                contentLocationUri = await UploadFileChunk(baseURI, sessionId, contentLocationUri, chunkFilePath).ConfigureAwait(false); ;
                chunkFilePath = Path.Combine(path, name + ++index);
            }

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishFileUpload(baseURI, sessionId, repoId, contentLocationUri, filePath, uploadMappingFile).ConfigureAwait(false); ;
            if (null != documentLink)
            {
                return documentLink;
            }
            return string.Empty;
        }


        private async static Task<string> UploadFileChunk(string baseURI, string sessionId, string link_relation, string chunkFilePath)
        {
            var baseRequest = baseURI + link_relation;


            using (HttpClient client = new HttpClient())
            {
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new System.Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);

                //set data content type
                StreamContent data = new StreamContent(new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                data.Headers.ContentType = new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_OCTET_STREAM);

                var result = await client.PostAsync(link_relation, data).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    Console.WriteLine("uploadchunk ok: " + baseRequest);
                    var response = result.Headers.Location;
                    if (null != response)
                    {
                        return response.ToString();
                    } else
                    {
                        return link_relation;
                    }
                }
            }
            return String.Empty;
        }


        private static async Task<string> FinishFileUpload(string baseURI, string sessionId, string repoId, string contentLocationUri, string filePath, string uploadMappingFile)
        {
            var link_relation = "/dms/r/" + repoId + "/o2m";
            var baseRequest = baseURI + link_relation;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new System.Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                //read mapping and replace content location uri
                dynamic dynObj = JsonConvert.DeserializeObject(File.ReadAllText(uploadMappingFile));
                dynObj.contentLocationUri = contentLocationUri;
                string output = JsonConvert.SerializeObject(dynObj);

                StringContent data = new StringContent(output);
                data.Headers.ContentType =  new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON);

                var result = await client.PostAsync(link_relation, data).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    Console.WriteLine("upload ok: " + baseRequest);
                    return result.Headers.Location.ToString();
                }
                else
                {
                    Console.WriteLine("upload failed: " + baseRequest);
                    return String.Empty;
                }
            }
        }
    }
}
