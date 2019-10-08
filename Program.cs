using Newtonsoft.Json;
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
        private static string MEDIA_TYPE_HAL_JSON = "application/hal+json";
        private static string MEDIA_TYPE_OCTET_STREAM = "application/octet-stream";

        static void Main(string[] args) 
        {
            //These samples refer to the RESTful interfaces of the d.ecs apps DMSApp and IdentityProviderApp which 
            //are contained in d.3one. Cloud and on-premises instances can be used both.

            //Follow these steps before running the code:

            //ONE: you need the base uri of your d.3one instance. To get an instance in the d.velop cloud and to 
            //choose a base uri, click here: https://store.d-velop.de/9/d.velop-documents
//            var baseURI = @"https://xxxxxxx.d-velop.cloud";
            var baseURI = @"https://test-dbar4.d-velop.cloud";

            //TWO: get an api key for your user in your d.3one instance: In d.one, click on tile "IdentityProvider", 
            //then on the "fingerprint icon" on the right.
//            var apiKey = @"xxxxxxxxxxxxxxxxxx";
            var apiKey = @"BYNhvn8F7Dvs1AjDsfzc6JFiWAd8o5eyMejeO/NIASsRi5CS8ScE78Wwxy1dCRqCCWKrtWHXn8pcViu7V6odcS4g1yZgVnwXQWOFEyp10wc=&_z_A0V5ayCQwWUJbRag_CqV9rDY6Ffznns-BcJUcClCcdagCNXUf6bl7JqwN-vEtPRK2WdcaL5e3Rhz370kikslYf6-Hkm-n";

            //THREE: get a repository id from your d.3one instance: In d.3one, click on tile "Search", then select 
            //the repository from the combobox on the top, copy the repository id form the browser URL
//            var repoId = @"xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx";
            var repoId = @"47f01950-9560-47de-9734-c0afae5fa033";

            //FOUR: grant user rights: In d.3one click on tile "Usermanagement", select your user and assign the user group 
            //"Kundenakte Vollzugriff". Then log off and sign in.

            //FIVE (optional): Provide your own source or proceed with step 6.
            //This sample is based on the default source "Basisdokumentarten" contained in d.velop documents.

            //SIX: create a mapping between source and content of your d.velop cloud: In d.3one, click on tile "Mappings" and then on 
            //"create new". Select the source (default: "Basisdokumentarten") and map categories and properties.
            //Category: "Schriftverkehr Kunde" -> "correspondance customer"
            //Properties: "Betreff" -> "Betreff", "Kunden-Nummer" -> "Customer No", "Kunden-Name1" -> "Kunden-Name1",
            //"Beleg-Datum" -> "Beleg-Datum", "Dateiname" -> "File name"

            //SEVEN: copy sample files from project folder "sample files" to local directory and set filePath to this directory
            var filePath = @"c:\temp";

            //EIGHT: run this code.


            //metadata for document upload. In instance tp-dev.d-velop.cloud use the default. Otherwise create your 
            //own file according to file "upload.json"
            var uploadMappingFile = Path.Combine(filePath, "upload.json");
            //metadata for document search. In instance tp-dev.d-velop.cloud use the default. Otherwise create your 
            //own file according to file "search.txt"
            var searchStringFile = Path.Combine(filePath, "search.txt");
            var uploadFile = Path.Combine(filePath, "upload.pdf");
            var downloadFilePath = Path.Combine(filePath, "download");

            //Important: Tls 1.0 is no longer supported! Please use the next line of code for compatibilty settings or 
            //select the most recent .NET Framework for this project!
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            //using apiKey authentication is recommended! 
            //for all further api calls use the returned sessionId as Bearer-Token!
            var sessionId = AuthenticateBasic(baseURI, "", apiKey);

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
                var result = SearchDocument(baseURI, sessionId, repoId, searchStringFile).Result;
            }
            Console.ReadKey();
        }

        //authenticate with user credentials and basic authentication
        private static string AuthenticateBasic(string baseURI, string username, string password)
        {
            var link_relation = "/identityprovider/login";
            var baseRequest = baseURI + link_relation;
            try
            {
                CookieContainer cookies = new CookieContainer();
                HttpClientHandler handler = new HttpClientHandler();
                handler.CookieContainer = cookies;

                using (HttpClient client = new HttpClient(handler))
                {
                    client.BaseAddress = new System.Uri(baseRequest);
                    client.DefaultRequestHeaders.Add("Authorization", "Basic " + Base64Encode(username + ':' + password));
                    client.DefaultRequestHeaders.Add("Accept", MEDIA_TYPE_HAL_JSON);

                    var parameters = "?basic=true";

                    var result = client.GetAsync(link_relation + parameters).Result;
                    if (result.IsSuccessStatusCode)
                    {
                        Uri uri = new Uri(baseURI);
                        IEnumerable<Cookie> responseCookies = cookies.GetCookies(uri).Cast<Cookie>();
                        string response = null;

                        foreach (Cookie cookie in responseCookies)
                            if ("AuthSessionId".Equals(cookie.Name))
                            {
                                //Important: URL decode the returned value!
                                response = System.Web.HttpUtility.UrlDecode(cookie.Value);
                                break;
                            }
                        if (null != response)
                        {
                            Console.WriteLine("ok: " + baseRequest);
                            return response;
                        }
                    }
                    else
                    {
                        Console.WriteLine("failed with status code \"" + result.StatusCode + "\": " + baseRequest);
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("failed with error \"" + ex.Message + "\": " + baseRequest);
            }
            return null;
        }

             
        //search document by given metadata
        private async static Task<string> SearchDocument(string baseURI, string apiKey, string repoId, string searchStringFile)
        {
            var link_relation = "/dms/r/" + repoId + "/srm";
            var baseRequest = baseURI + link_relation;

            using (HttpClient client = new HttpClient())
            {
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));


                string searchString = File.ReadAllText(searchStringFile);
                baseRequest += searchString;

                var result = await client.GetAsync(baseRequest);
                if (result.IsSuccessStatusCode)
                {
                    var jsonString = await result.Content.ReadAsStringAsync();
                    Console.WriteLine("ok: " + baseRequest);

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

        private async static Task<string> GetDocumentInfo(string baseURI, string apiKey, string repoId, string documentLink)
        {

            var link_relation = documentLink;
            var baseRequest = baseURI + link_relation;

            using (HttpClient client = new HttpClient())
            {
                //get download url
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                Console.WriteLine("get download url");
                var result = await client.GetAsync(baseRequest);
                if (result.IsSuccessStatusCode)
                {
                    var jsonString = await result.Content.ReadAsStringAsync();
                    Console.WriteLine("ok: " + baseRequest);

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
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + sessionId);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                Console.WriteLine("start document download");
                var result = await client.GetAsync(baseRequest);
                if (result.IsSuccessStatusCode)
                {
                    Stream stream = await result.Content.ReadAsStreamAsync();
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
            }
            return false;
        }

        //upload file in a whole
        private async static Task<string> UploadFile(string baseURI, string sessionId, string repoId, string filePath, string uploadMappingFile)
        {
            var contentLocationUri = "/dms/r/" + repoId + "/blob/chunk";

            //first: upload file and get an URI (contentLocationUri) as a reference to this file
            Console.WriteLine("start file upload");
            contentLocationUri = await UploadFileChunk(baseURI, sessionId, contentLocationUri, filePath);

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishFileUpload(baseURI, sessionId, repoId, contentLocationUri, filePath, uploadMappingFile);
            if (null != documentLink)
            {
                Console.WriteLine("finished file upload");
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
            Console.WriteLine("start chunked file upload");
            while (File.Exists(chunkFilePath))
            {
                contentLocationUri = await UploadFileChunk(baseURI, sessionId, contentLocationUri, chunkFilePath);
                chunkFilePath = Path.Combine(path, name + ++index);
            }

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishFileUpload(baseURI, sessionId, repoId, contentLocationUri, filePath, uploadMappingFile);
            if (null != documentLink)
            {
                Console.WriteLine("finished chunked file upload");
                return documentLink;
            }
            return string.Empty;
        }


        private async static Task<string> UploadFileChunk(string baseURI, string apiKey, string link_relation, string chunkFilePath)
        {
            var baseRequest = baseURI + link_relation;


            using (HttpClient client = new HttpClient())
            {
                //set Origin header to avoid conflicts with same origin policy
                client.BaseAddress = new System.Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

                //set data content type
                StreamContent data = new StreamContent(new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                data.Headers.ContentType = new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_OCTET_STREAM);

                var result = await client.PostAsync(link_relation, data);
                if (result.IsSuccessStatusCode)
                {
                    Console.WriteLine("ok: " + baseRequest);
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


        private static async Task<string> FinishFileUpload(string baseURI, string apiKey, string repoId, string contentLocationUri, string filePath, string uploadMappingFile)
        {
            var link_relation = "/dms/r/" + repoId + "/o2m";
            var baseRequest = baseURI + link_relation;

            using (HttpClient client = new HttpClient())
            {
                client.BaseAddress = new System.Uri(baseURI);
                client.DefaultRequestHeaders.Add("Origin", baseURI);
                client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);

                //read mapping and replace content location uri
                dynamic dynObj = JsonConvert.DeserializeObject(File.ReadAllText(uploadMappingFile));
                dynObj.contentLocationUri = contentLocationUri;
                string output = JsonConvert.SerializeObject(dynObj);

                StringContent data = new StringContent(output);
                data.Headers.ContentType =  new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON);

                var result = await client.PostAsync(link_relation, data);
                if (result.IsSuccessStatusCode)
                {
                    Console.WriteLine("ok: " + baseRequest);
                    return result.Headers.Location.ToString();
                }
                else
                {
                    Console.WriteLine("failed: " + baseRequest);
                    return String.Empty;
                }
            }
        }


        private static string Base64Encode(string plainText)
        {
            var plainTextBytes = System.Text.Encoding.UTF8.GetBytes(plainText);
            return System.Convert.ToBase64String(plainTextBytes);
        }

    }
}
