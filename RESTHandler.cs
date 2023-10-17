// Copyright 2023 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    internal class RESTHandler
    {
        protected ILogger logger;
        public bool UseSSL { get; set; }
        public string Host { get; set; }
        public string User { get; set; }
        public string Password { get; set; }
        public string Token { get; set; }
        public bool IgnoreSSLWarning { get; set; }

        public RESTHandler(string host, string user, string password, bool useSSL, bool ignoreSSLWarning)
        {
            logger = Keyfactor.Logging.LogHandler.GetClassLogger(this.GetType());
            Host = host;
            UseSSL = useSSL;
            User = user;
            Password = password;
            IgnoreSSLWarning = ignoreSSLWarning;
        }

        public T Get<T>(string requestUri, string transactionId = "")
            where T : class
        {
            logger.LogTrace("Entered Get method");

            using (HttpClient client = new HttpClient(GetHttpClientHandler()))
            {
                ConfigureHttpClient(client, transactionId);
                HttpResponseMessage response = null;

                logger.LogTrace($"Performing 'Get' operation from '{requestUri}'");
                response = client.GetAsync(requestUri, HttpCompletionOption.ResponseContentRead).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw ProcessFailureResponse(response.StatusCode,
                        response.Content.ReadAsStringAsync().Result,
                        requestUri);
                }

                T result = null;
                try { result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result); }
                catch 
                {
                    logger.LogTrace($"Unable to deserialize result: {response.Content.ReadAsStringAsync().Result}");
                    throw;
                }
                logger.LogTrace($"Created object from result of type '{result.GetType().ToString()}' from 'Get' operation at '{requestUri}'");

                logger.LogTrace("Leaving Get method");
                return result;
            }
        }

        public T Post<T>(string requestUri, string requestContent, string transactionId = "")
            where T : class
        {
            logger.LogTrace("Entered Post method");

            using (HttpClient client = new HttpClient(GetHttpClientHandler()))
            {
                ConfigureHttpClient(client, transactionId);
                HttpContent content = new StringContent(requestContent, Encoding.UTF8, "application/json");

                logger.LogTrace($"Performing 'Post' operation of type '{requestContent.GetType().ToString()}' to '{requestUri}'");
                HttpResponseMessage response = client.PostAsync(requestUri, content).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw ProcessFailureResponse(response.StatusCode,
                        response.Content.ReadAsStringAsync().Result,
                        requestUri,
                        true,
                        Newtonsoft.Json.JsonConvert.SerializeObject(requestContent));
                }

                T result = null;
                try { result = Newtonsoft.Json.JsonConvert.DeserializeObject<T>(response.Content.ReadAsStringAsync().Result); }
                catch 
                {
                    logger.LogTrace($"Unable to deserialize result: {response.Content.ReadAsStringAsync().Result}");
                    throw;
                }
                logger.LogTrace($"Created object from result of type '{result.GetType().ToString()}' from 'Post' operation to '{requestUri}'");

                logger.LogTrace("Leaving Post method");
                return result;
            }
        }

        public void Patch<S>(string requestUri, S requestContent, string transactionId = "")
            where S : class
        {
            logger.LogTrace("Entered Patch method");

            using (HttpClient client = new HttpClient(GetHttpClientHandler()))
            {
                ConfigureHttpClient(client, transactionId);
                HttpContent content = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(requestContent), Encoding.UTF8, "application/json");

                logger.LogTrace($"Performing 'Patch' operation of type '{requestContent.GetType().ToString()}' to '{requestUri}'");
                HttpRequestMessage request = new System.Net.Http.HttpRequestMessage(new System.Net.Http.HttpMethod("PATCH"), requestUri);
                request.Content = content;
                HttpResponseMessage response = client.SendAsync(request).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw ProcessFailureResponse(response.StatusCode,
                        response.Content.ReadAsStringAsync().Result,
                        requestUri,
                        true,
                        Newtonsoft.Json.JsonConvert.SerializeObject(requestContent));
                }

                logger.LogTrace($"'Patch' operation to '{requestUri}' succeeded");
                logger.LogTrace("Leaving Patch method");
            }
        }

        public void Delete(string requestUri, string transactionId = "")
        {
            logger.LogTrace("Entered Delete method");

            using (HttpClient client = new HttpClient(GetHttpClientHandler()))
            {
                ConfigureHttpClient(client, transactionId);

                logger.LogTrace($"Performing 'Delete' operation to '{requestUri}'");
                HttpResponseMessage response = client.DeleteAsync(requestUri).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw ProcessFailureResponse(response.StatusCode,
                        response.Content.ReadAsStringAsync().Result,
                        requestUri);
                }

                logger.LogTrace($"'Post' operation to '{requestUri}' succeeded");
                logger.LogTrace("Leaving Delete method");
            }
        }

        public string PostBASHCommand(F5BashCommand command, string transactionId = "")
        {
            logger.LogTrace("Entered PostBASHCommand method");

            using (HttpClient client = new HttpClient(GetHttpClientHandler()))
            {
                ConfigureHttpClient(client, transactionId);

                StringContent data = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(command));
                logger.LogTrace($"Posting BASH command: '{command.command}'");
                data.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                HttpResponseMessage response = client.PostAsync("/mgmt/tm/util/bash", data).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw ProcessFailureResponse(response.StatusCode,
                        response.Content.ReadAsStringAsync().Result,
                        "/mgmt/tm/util/bash",
                        true,
                        Newtonsoft.Json.JsonConvert.SerializeObject(command));
                }

                string bashResult = response.Content.ReadAsStringAsync().Result;
                F5BashCommand resultCommand = Newtonsoft.Json.JsonConvert.DeserializeObject<F5BashCommand>(bashResult);

                logger.LogTrace("Leaving PostBASHCommand method");
                return resultCommand.commandResult;
            }
        }

        public void PostInstallCryptoCommand(F5InstallCommand command, string cryptoType, string transactionId = "")
        {
            logger.LogTrace("Entered PostInstallCryptoCommand method");

            using (HttpClient client = new HttpClient(GetHttpClientHandler()))
            {
                ConfigureHttpClient(client, transactionId);

                StringContent data = new StringContent(Newtonsoft.Json.JsonConvert.SerializeObject(command));
                data.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/json");
                HttpResponseMessage response = client.PostAsync($"/mgmt/tm/sys/crypto/{cryptoType}", data).Result;
                if (!response.IsSuccessStatusCode)
                {
                    throw ProcessFailureResponse(response.StatusCode,
                        response.Content.ReadAsStringAsync().Result,
                        $"/mgmt/tm/sys/crypto/{cryptoType}",
                        true,
                        Newtonsoft.Json.JsonConvert.SerializeObject(command));
                }

                logger.LogTrace("Leaving PostInstallCryptoCommand method");
            }
        }

        private F5RESTException ProcessFailureResponse(System.Net.HttpStatusCode status, string content, string request)
        {
            return ProcessFailureResponse(status, content, request, false, string.Empty);
        }

        private F5RESTException ProcessFailureResponse(System.Net.HttpStatusCode status, string content, string request, bool isPost, string requestBody)
        {
            F5RESTException exc = new F5RESTException()
            {
                code = (int)status,
                message = "An error response was returned",
                IsPost = isPost,
                RequestBody = requestBody,
                RequestString = request
            };

            // This is not the most elegant manner of handling this, but it is not feasible to determine
            //  the response codes that would result in an HTML-type message vs an F5 exception.
            // Try to process the common codes
            try
            {
                // Log the actual contents of the response
                logger.LogError($"F5 iControl REST API returned an error code {status.ToString()}: {content}");

                switch (status)
                {
                    case System.Net.HttpStatusCode.NotFound:
                        exc.message = "The requested resource was not found";
                        break;
                    case System.Net.HttpStatusCode.Unauthorized:
                        exc.message = "The account supplied is not permitted access or authorization failed";
                        break;
                    case System.Net.HttpStatusCode.Forbidden:
                        exc.message = "The account supplied is not permitted access or authorization failed";
                        break;
                    case System.Net.HttpStatusCode.BadRequest:
                        // Can be de-serialized
                        exc = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(content);
                        break;
                    case System.Net.HttpStatusCode.InternalServerError:
                        // Can be de-serialized
                        exc = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(content);
                        break;
                    case System.Net.HttpStatusCode.ServiceUnavailable:
                        // Can be de-serialized
                        exc = Newtonsoft.Json.JsonConvert.DeserializeObject<F5RESTException>(content);
                        break;
                }
            }
            catch (Exception)
            {
                exc.message = $"Unable to process the failure code from F5. The contents of the response are recorded in the orchestrator log file.";
            }

            return exc;
        }

        public void UploadFile(string filename, byte[] fileBytes)
        {
            logger.LogTrace("Entered UploadFile method");
            using (System.Net.WebClient webClient = new System.Net.WebClient())
            {
                System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, sslcert, chain, sslPolicyErrors) => true;
                webClient.Headers.Add("ServerHost", $"{GetProtocol()}{Host}/mgmt/shared/file-transfer/uploads/{filename}");
                webClient.Headers.Add("Content-Type", "application/octet-stream");
                webClient.Headers.Add("Content-Range", $"0-{fileBytes.Length - 1}/{fileBytes.Length}");
                if (Token == null)
                {
                    webClient.Headers.Add("Authorization", $"Basic {Convert.ToBase64String(Encoding.ASCII.GetBytes($"{User}:{Password}"))}");
                }
                else
                {
                    webClient.Headers.Add("X-F5-Auth-Token", Token);
                }

                webClient.UploadData($"{GetProtocol()}{Host}/mgmt/shared/file-transfer/uploads/{filename}", fileBytes);
            }
            logger.LogTrace("Leaving UploadFile method");
        }

        private void ConfigureHttpClient(HttpClient client, string transactionId = "")
        {
            //System.Net.ServicePointManager.ServerCertificateValidationCallback += (sender, sslcert, chain, sslPolicyErrors) => true;
            client.BaseAddress = new Uri($"{GetProtocol()}{Host}");
            client.DefaultRequestHeaders.Accept.Clear();
            client.DefaultRequestHeaders.Accept.Add(new System.Net.Http.Headers.MediaTypeWithQualityHeaderValue("application/json"));
            if (Token == null)
            {
                client.DefaultRequestHeaders.Authorization =
                    new System.Net.Http.Headers.AuthenticationHeaderValue("Basic", Convert.ToBase64String(Encoding.ASCII.GetBytes($"{User}:{Password}")));
            }
            else
            {
                client.DefaultRequestHeaders.Add("X-F5-Auth-Token", Token);
            }
            if (!string.IsNullOrEmpty(transactionId)) { client.DefaultRequestHeaders.Add("X-F5-REST-Coordination-Id", transactionId); }
        }

        private string GetProtocol()
        {
            return UseSSL ? "https://" : "http://";
        }

        private HttpClientHandler GetHttpClientHandler()
        {
            HttpClientHandler handler = new HttpClientHandler();
            if (IgnoreSSLWarning) { handler.ServerCertificateCustomValidationCallback = (message, cert, chain, errors) => { return true; }; }

            return handler;
        }
    }
}
