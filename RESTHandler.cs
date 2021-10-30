using CSS.Common.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading.Tasks;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator
{
    internal class RESTHandler : LoggingClientBase
    {
        public bool UseSSL { get; set; }
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }

        public T Get<T>(string requestUri, string transactionId = "")
            where T : class
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);
                HttpResponseMessage response = null;

                Logger.Trace($"Performing 'Get' operation from '{requestUri}'");
                response = client.GetAsync(requestUri, HttpCompletionOption.ResponseContentRead).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = requestUri;
                    throw restException;
                }

                T result = null;
                try { result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result); }
                catch 
                {
                    Logger.Trace($"Unable to deserialize result: {response.Content.ReadAsStringAsync().Result}");
                    throw;
                }
                Logger.Trace($"Created object from result of type '{result.GetType().ToString()}' from 'Get' operation at '{requestUri}'");

                Logger.MethodExit();
                return result;
            }
        }

        public T Post<T, S>(string requestUri, S requestContent, string transactionId = "")
            where T : class
            where S : class
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);
                HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestContent), Encoding.UTF8, "application/json");

                Logger.Trace($"Performing 'Post' operation of type '{requestContent.GetType().ToString()}' to '{requestUri}'");
                HttpResponseMessage response = client.PostAsync(requestUri, content).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = requestUri;
                    restException.IsPost = true;
                    restException.RequestBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                    throw restException;
                }

                T result = null;
                try { result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result); }
                catch
                {
                    Logger.Trace($"Unable to deserialize result: {response.Content.ReadAsStringAsync().Result}");
                    throw;
                }
                Logger.Trace($"Created object from result of type '{result.GetType().ToString()}' from 'Post' operation to '{requestUri}'");

                Logger.MethodExit();
                return result;
            }
        }

        public T Post<T>(string requestUri, string requestContent, string transactionId = "")
            where T : class
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);
                HttpContent content = new StringContent(requestContent, Encoding.UTF8, "application/json");

                Logger.Trace($"Performing 'Post' operation of type '{requestContent.GetType().ToString()}' to '{requestUri}'");
                HttpResponseMessage response = client.PostAsync(requestUri, content).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = requestUri;
                    restException.IsPost = true;
                    restException.RequestBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                    throw restException;
                }

                T result = null;
                try { result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result); }
                catch 
                {
                    Logger.Trace($"Unable to deserialize result: {response.Content.ReadAsStringAsync().Result}");
                    throw;
                }
                Logger.Trace($"Created object from result of type '{result.GetType().ToString()}' from 'Post' operation to '{requestUri}'");

                Logger.MethodExit();
                return result;
            }
        }

        public void Post<S>(string requestUri, S requestContent, string transactionId = "")
            where S : class
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);
                HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestContent), Encoding.UTF8, "application/json");

                Logger.Trace($"Performing 'Post' operation of type '{requestContent.GetType().ToString()}' to '{requestUri}'");
                HttpResponseMessage response = client.PostAsync(requestUri, content).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = requestUri;
                    restException.IsPost = true;
                    restException.RequestBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                    throw restException;
                }

                Logger.Trace($"'Post' operation to '{requestUri}' succeeded");
                Logger.MethodExit();
            }
        }

        public void Patch<S>(string requestUri, S requestContent, string transactionId = "")
            where S : class
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);
                HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestContent), Encoding.UTF8, "application/json");

                Logger.Trace($"Performing 'Patch' operation of type '{requestContent.GetType().ToString()}' to '{requestUri}'");
                HttpRequestMessage request = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod("PATCH"), requestUri);
                request.Content = content;
                HttpResponseMessage response = client.SendAsync(request).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = requestUri;
                    restException.IsPost = true;
                    restException.RequestBody = Newtonsoft.Json.JsonConvert.SerializeObject(requestContent);
                    throw restException;
                }

                Logger.Trace($"'Patch' operation to '{requestUri}' succeeded");
                Logger.MethodExit();
            }
        }

        public void Delete(string requestUri, string transactionId = "")
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);

                Logger.Trace($"Performing 'Delete' operation to '{requestUri}'");
                HttpResponseMessage response = client.DeleteAsync(requestUri).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = requestUri;
                    throw restException;
                }

                Logger.Trace($"'Post' operation to '{requestUri}' succeeded");
                Logger.MethodExit();
            }
        }

        public string PostBASHCommand(F5BashCommand command, string transactionId = "")
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);

                StringContent data = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(command));
                Logger.Trace($"Posting BASH command: '{command.command}'");
                data.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                HttpResponseMessage response = client.PostAsync("/mgmt/tm/util/bash", data).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = "/mgmt/tm/util/bash";
                    restException.IsPost = true;
                    restException.RequestBody = Newtonsoft.Json.JsonConvert.SerializeObject(command);
                    throw restException;
                }

                string bashResult = response.Content.ReadAsStringAsync().Result;
                F5BashCommand resultCommand = Newtonsoft.Json.JsonConvert.DeserializeObject<F5BashCommand>(bashResult);

                Logger.MethodExit();
                return resultCommand.commandResult;
            }
        }

        public void PostInstallCryptoCommand(F5InstallCommand command, string cryptoType, string transactionId = "")
        {
            Logger.MethodEntry();

            using (HttpClient client = new HttpClient())
            {
                ConfigureHttpClient(client, transactionId);

                StringContent data = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(command));
                data.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                HttpResponseMessage response = client.PostAsync($"/mgmt/tm/sys/crypto/{cryptoType}", data).Result;
                if (!response.IsSuccessStatusCode)
                {
                    F5RESTException restException = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(response.Content.ReadAsStringAsync().Result);
                    restException.RequestString = $"/mgmt/tm/sys/crypto/{cryptoType}";
                    restException.IsPost = true;
                    restException.RequestBody = Newtonsoft.Json.JsonConvert.SerializeObject(command);
                    throw restException;
                }

                Logger.MethodExit();
            }
        }

        public void UploadFile(string filename, byte[] fileBytes)
        {
            Logger.MethodEntry();
            using (System.Net.WebClient webClient = new System.Net.WebClient())
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, sslcert, chain, sslPolicyErrors) => true;
                webClient.Headers.Add("ServerHost", $"{GetProtocol()}{Host}/mgmt/shared/file-transfer/uploads/{filename}");
                webClient.Headers.Add("Content-Type", "application/octet-stream");
                webClient.Headers.Add("Content-Range", $"0-{fileBytes.Length - 1}/{fileBytes.Length}");
                webClient.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes($"{User}:{Password}"))}");

                webClient.UploadData($"{GetProtocol()}{Host}/mgmt/shared/file-transfer/uploads/{filename}", fileBytes);
            }
            Logger.MethodExit();
        }

        private void ConfigureHttpClient(HttpClient client, string transactionId = "")
        {
            System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, sslcert, chain, sslPolicyErrors) => true;
            client.BaseAddress = new Uri($"{GetProtocol()}{Host}");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            client.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{User}:{Password}")));
            if (!string.IsNullOrEmpty(transactionId)) { client.DefaultRequestHeaders.Add("X-F5-REST-Coordination-Id", transactionId); }
        }

        private string GetProtocol()
        {
            return UseSSL ? "https://" : "http://";
        }
    }
}
