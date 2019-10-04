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
            //Important: Tls 1.0 is no longer supported! Please use the next line of code for compatibilty settings or select the most recent .NET Framework for this project!
            ServicePointManager.SecurityProtocol |= SecurityProtocolType.Tls11 | SecurityProtocolType.Tls12;

            //These samples refer to a d.3one application (d.velop cloud or on-premises). 
            //You can register for a 30day free d.velop cloud here: https://order.d-velop.cloud/register

            //Alternatively you can use the open cloud development system https://tp-dev.d-velop.cloud. To get an user account please contact the d.velop Technology Partner Management.
            //Please note, that any uploaded data is visible to other development partners

            //To run with other d.3one applications or d-velop clouds, please adjust baseURI
            //apiKey or username, password and repoId as well as the metadata provided in the files upload.json and search.txt and the 
            //directory path to local sample files. Then copy the files from the project folder "sample files" to this directory. 
            //The used source mapping ist shown in file source.json.

            //local path to sample files
            var filePath = @"C:\temp";

            //base uri of your target d.3one application. Use the following Uri for the open development system: "https://tp-dev.d-velop.cloud"
            var baseURI = @"https://tp-dev.d-velop.cloud";
            //api key of a valid d.3one account. If not available use basic authentication with username and password as described below 
//            var apiKey = <apiKey>;
            //id of the repository. In instance tp-dev.d-velop.cloud use: "44330b47-508c-58bd-9109-cedd2c31e418"
            var repoId = @"44330b47-508c-58bd-9109-cedd2c31e418";
            //metadata for document upload. In instance tp-dev.d-velop.cloud use the default. Otherwise create your own file according to file "upload.json"
            var uploadMappingFile = filePath + @"\upload.json";
            //metadata for document search. In instance tp-dev.d-velop.cloud use the default. Otherwise create your own file according to file "search.txt"
            var searchStringFile = filePath + @"\search.txt";


            var uploadFile = Path.Combine(filePath, "upload.pdf");
            var downloadFilePath = Path.Combine(filePath, "download");

            //using apiKey authentication is recommended! In this case call replace placeholders for username with empty string "" and password with the apiKey
            //to authenticate with user credentials and base authentication: replace placeholders for username and password 
            var apiKey = AuthenticateBasic(baseURI, "<empty or username>", "<apiKey or password>");

            //for all further api calls use the returned key as Bearer-Token!

            if (null != apiKey)
            {
                //upload chunked document 
                var documentLink = UploadDocument(baseURI, apiKey, repoId, uploadFile, uploadMappingFile).Result;
                //show document in d.3one
                System.Diagnostics.Process.Start(baseURI + documentLink);
                //get download url and document properties (returned properties are defined in the source mapping)
                var documentInfo = GetDocumentInfo(baseURI, apiKey, repoId, documentLink).Result;
                //download document
                bool downloaded = DownloadDocument(baseURI, apiKey, repoId, documentInfo, downloadFilePath).Result;

                //search documents
                var result = SearchDocument(baseURI, apiKey, repoId, searchStringFile).Result;
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

        private async static Task<bool> DownloadDocument(string baseURI, string apiKey, string repoId, string jsonDocumentInfo, string downloadFilePath)
        {
            dynamic documentInfo = JsonConvert.DeserializeObject<object>(jsonDocumentInfo);
            var link_relation =  documentInfo._links.mainblobcontent.href;
            var baseRequest = baseURI + link_relation;

            var fileName = "";
            foreach (var prop in documentInfo.sourceProperties)
            {
                if (prop.key == "filename")
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
                client.DefaultRequestHeaders.Add("Authorization", "Bearer " + apiKey);
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

        //upload documents in one or more chunks
        private async static Task<string> UploadDocument(string baseURI, string apiKey, string repoId, string filePath, string uploadMappingFile)
        {
            var path = Path.GetDirectoryName(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            var contentLocationUri = "/dms/r/" + repoId + "/blob/chunk";
            var index = 0;
            var chunkFilePath = Path.Combine(path, name + index);

            //first: upload file and get an URI (contentLocationUri) as a reference to this file
            Console.WriteLine("start document upload");
            while (File.Exists(chunkFilePath))
            {
                contentLocationUri = await UploadDocumentChunk(baseURI, apiKey, contentLocationUri, chunkFilePath);
                chunkFilePath = Path.Combine(path, name + ++index);
            }

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishChunkedDocumentUpload(baseURI, apiKey, repoId, contentLocationUri, filePath, uploadMappingFile);
            if (null != documentLink)
            {
                Console.WriteLine("finished document upload");
                return documentLink;
            }
            return string.Empty;
        }


        private async static Task<string> UploadDocumentChunk(string baseURI, string apiKey, string link_relation, string chunkFilePath)
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


        private static async Task<string> FinishChunkedDocumentUpload(string baseURI, string apiKey, string repoId, string contentLocationUri, string filePath, string uploadMappingFile)
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
