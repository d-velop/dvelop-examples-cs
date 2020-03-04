using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Threading.Tasks;

namespace DMSAPISamples.Client
{
    /// <summary>
    /// Experimental sample client for the d.velop document management system.
    /// </summary>
    public class DocumentClient : IDisposable
    {
        private const string MEDIA_TYPE_HAL_JSON = "application/hal+json";
        private const string MEDIA_TYPE_OCTET_STREAM = "application/octet-stream";

        private string baseUri = null;
        private string sessionId = null;

        public string RepositoryId { get; set; }

        // HttpClient is intended to be instantiated once per application, rather than per-use.
        // See https://docs.microsoft.com/de-de/dotnet/api/system.net.http.httpclient
        private readonly HttpClient client = new HttpClient();

        private class AuthSessionInfoDto
        {
            public string AuthSessionId { get; set; }
            public DateTime Expire { get; set; }
        }

        public DocumentClient(string baseUri, string apiKey)
        {
            this.baseUri = baseUri;
            client.BaseAddress = new System.Uri(baseUri);

            //using apiKey authentication is recommended! 
            //for all further api calls use the returned sessionId as Bearer-Token!
            Authenticate(apiKey);
        }

        //authenticate with user credentials and basic authentication
        private void Authenticate(string apiKey)
        {
            var linkRelation = "/identityprovider/login";
            var baseRequest = baseUri + linkRelation;
            try
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                client.DefaultRequestHeaders.Add("Accept", MEDIA_TYPE_HAL_JSON);

                var result = client.GetAsync(linkRelation).Result;
                if (result.IsSuccessStatusCode)
                {
                    var authSessionInfo = JsonConvert.DeserializeObject<AuthSessionInfoDto>(result.Content.ReadAsStringAsync().Result);

                    if (null != authSessionInfo.AuthSessionId)
                    {
                        Console.WriteLine("login ok: " + "expires: " + authSessionInfo.Expire + ", sessionId: " + authSessionInfo.AuthSessionId);
                        this.sessionId = authSessionInfo.AuthSessionId;
                    }
                }
                else
                {
                    Console.WriteLine("login failed with status code \"" + result.StatusCode + "\": " + baseRequest);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("login failed with error \"" + ex.Message + "\": " + baseRequest);
            }
        }

        //search document by given metadata
        public string SearchDocument(string source, string category, string properties = null)
        {
            var linkRelation = $"/dms/r/{RepositoryId}/srm";
            var baseRequest = baseUri + linkRelation;
            var searchFor = $"?sourceid={source}&sourcecategories=[\"{category}\"]";

            if (!string.IsNullOrWhiteSpace(properties))
            {
                searchFor += $"&sourceproperties={properties}";
            }

            baseRequest += searchFor;

            var jsonString = GetJson(baseRequest).Result;
            return jsonString;
        }

        public List<DocumentRepository> GetRepositoryInfo()
        {
            var linkRelation = $"/dms/r";
            var baseRequest = baseUri + linkRelation;
            List<DocumentRepository> result = new List<DocumentRepository>();

            using (HttpClient client = new HttpClient())
            {
                var resultJson = GetJson(baseRequest).Result;

                JObject resultObject = JsonConvert.DeserializeObject<JObject>(resultJson);
                IEnumerable<JToken> resultItems = resultObject["repositories"].Children();

                foreach (var resultItem in resultItems)
                {
                    DocumentRepository repository = resultItem.ToObject<DocumentRepository>();
                    result.Add(repository);
                }
            }

            return result;
        }

        public DocumentSource GetSourceInfo()
        {
            var linkRelation = $"/dms/r/{RepositoryId}/source";
            var baseRequest = baseUri + linkRelation;

            using (HttpClient client = new HttpClient())
            {
                var jsonString = GetJson(baseRequest).Result;
                DocumentSource source = JsonConvert.DeserializeObject<DocumentSource>(jsonString);
                return source;
            }
        }

        public string GetDocumentInfo(string documentUri)
        {
            var baseRequest = baseUri + documentUri;
            var jsonString = GetJson(baseRequest).Result;
            return jsonString;
        }

        public string PutDocumentInfo(DocumentSearchResult document, string sourceId)
        {
            var linkRelation = $"/dms/r/{RepositoryId}/o2m/{document.Id}";
            var baseRequest = baseUri + linkRelation;

            DocumentUpdateRequest requestData = new DocumentUpdateRequest()
            {
                AlterationText = "update properties",
                SourceCategory = document.SourceCategories.FirstOrDefault(),
                SourceId = sourceId
            };

            requestData.AddProperties(document.SourceProperties);

            return PutJson(baseRequest, requestData).Result;
        }

        public async Task<string> GetJson(string uri)
        {
            //client.BaseAddress = new Uri(baseUri);
            client.DefaultRequestHeaders.Add("Origin", baseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

            var result = await client.GetAsync(uri).ConfigureAwait(false);
            if (result.IsSuccessStatusCode)
            {
                var jsonString = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                return jsonString;
            }
            else
            {
                string message = result.Content.ReadAsStringAsync().Result;
                throw new Exception(message);
            }
        }

        public async Task<string> PutJson(string uri, object data)
        {
            string content = JsonConvert.SerializeObject(data);
            HttpContent httpContent = new StringContent(content, System.Text.Encoding.UTF8, MEDIA_TYPE_HAL_JSON);

            using (HttpClient writerClient = new HttpClient())
            {
                // for CSRF reasons, use a new HtpClient with request specific base address
                writerClient.BaseAddress = new Uri(baseUri);
                writerClient.DefaultRequestHeaders.Add("Origin", baseUri);
                writerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
                writerClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                var result = await writerClient.PutAsync(uri, httpContent).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    var jsonString = await result.Content.ReadAsStringAsync().ConfigureAwait(false);
                    //return jsonString;
                    return string.Empty;
                }
                else
                {
                    string message = result.Content.ReadAsStringAsync().Result;
                    return message;
                }
            }
        }

        public async Task<bool> DownloadDocument(string jsonDocumentInfo, string downloadFilePath)
        {
            dynamic documentInfo = JsonConvert.DeserializeObject<object>(jsonDocumentInfo);
            var link_relation = documentInfo._links.mainblobcontent.href;
            var baseRequest = baseUri + link_relation;

            var fileName = "";
            foreach (var prop in documentInfo.sourceProperties)
            {
                if (prop.key == "dateiname")
                {
                    fileName = prop.value;
                    break;
                }
            }

            //get download url
            //set Origin header to avoid conflicts with same origin policy
            //client.BaseAddress = new Uri(baseUri);
            client.DefaultRequestHeaders.Add("Origin", baseUri);
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
            client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

            Console.WriteLine("start document download");
            var result = await client.GetAsync(baseRequest).ConfigureAwait(false); ;
            if (result.IsSuccessStatusCode)
            {
                using (Stream stream = await result.Content.ReadAsStreamAsync().ConfigureAwait(false))
                {
                    Directory.CreateDirectory(downloadFilePath);
                    string filePath = Path.Combine(downloadFilePath, fileName);
                    using (FileStream fs = new FileStream(filePath, FileMode.Create))
                    {
                        stream.CopyTo(fs);
                        fs.Flush();
                    }

                    Console.WriteLine("document downloaded to " + filePath);
                }
                return true;
            }
            else
            {
                Console.WriteLine("document download failed");
            }

            return false;
        }

        //upload file in a whole
        public async Task<string> UploadFile(string filePath, DocumentUploadMapping mapping)
        {
            //first: upload file and get an URI (contentLocationUri) as a reference to this file
            var contentLocationUri = await UploadFileChunk(filePath).ConfigureAwait(false);

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishFileUpload(contentLocationUri, mapping).ConfigureAwait(false);
            if (null != documentLink)
            {
                return documentLink;
            }
            return string.Empty;
        }

        //upload file in chunks
        private async Task<string> UploadFileChunked(string baseURI, string sessionId, string repoId, string filePath, DocumentUploadMapping mapping)
        {
            var path = Path.GetDirectoryName(filePath);
            var name = Path.GetFileNameWithoutExtension(filePath);
            var contentLocationUri = "/dms/r/" + repoId + "/blob/chunk";
            var index = 0;
            var chunkFilePath = Path.Combine(path, name + index);

            //first: upload file and get an URI (contentLocationUri) as a reference to this file
            while (File.Exists(chunkFilePath))
            {
                contentLocationUri = await UploadFileChunk(chunkFilePath).ConfigureAwait(false);
                chunkFilePath = Path.Combine(path, name + ++index);
            }

            //second: upload metadata with reference URI (contentLocationUri).
            var documentLink = await FinishFileUpload(contentLocationUri, mapping).ConfigureAwait(false); ;
            if (null != documentLink)
            {
                return documentLink;
            }
            return string.Empty;
        }

        private async Task<string> UploadFileChunk(string chunkFilePath)
        {
            var linkRelation = $"/dms/r/{RepositoryId}/blob/chunk/";
            var baseRequest = baseUri + linkRelation;

            using (HttpClient writerClient = new HttpClient())
            {
                //set Origin header to avoid conflicts with same origin policy
                writerClient.BaseAddress = new System.Uri(baseUri);
                writerClient.DefaultRequestHeaders.Add("Origin", baseUri);
                writerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);

                //set data content type
                using (FileStream file = new FileStream(chunkFilePath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    StreamContent data = new StreamContent(file);
                    data.Headers.ContentType = new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_OCTET_STREAM);

                    var result = await writerClient.PostAsync(baseRequest, data).ConfigureAwait(false);
                    if (result.IsSuccessStatusCode)
                    {
                        var response = result.Headers.Location;
                        if (null != response)
                        {
                            return response.ToString();
                        }
                        else
                        {
                            string message = result.Content.ReadAsStringAsync().Result;
                            throw new Exception(message);
                        }
                    }
                }
            }

            return String.Empty;
        }

        private async Task<string> FinishFileUpload(string contentLocationUri, DocumentUploadMapping mapping)
        {
            var linkRelation = $"/dms/r/{RepositoryId}/o2m";
            var baseRequest = baseUri + linkRelation;

            using (HttpClient writerClient = new HttpClient())
            {
                writerClient.BaseAddress = new System.Uri(baseUri);
                writerClient.DefaultRequestHeaders.Add("Origin", baseUri);
                writerClient.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", sessionId);
                writerClient.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON));

                //replace content location uri
                mapping.ContentLocationUri = contentLocationUri;
                string output = JsonConvert.SerializeObject(mapping);

                StringContent data = new StringContent(output);
                data.Headers.ContentType = new MediaTypeWithQualityHeaderValue(MEDIA_TYPE_HAL_JSON);

                var result = await writerClient.PostAsync(baseRequest, data).ConfigureAwait(false);
                if (result.IsSuccessStatusCode)
                {
                    return result.Headers.Location.ToString();
                }
                else
                {
                    string message = result.Content.ReadAsStringAsync().Result;
                    Console.WriteLine(message);
                    throw new Exception(message);
                }
            }
        }

        public void Dispose()
        {
            client.Dispose();
        }
    }
}
