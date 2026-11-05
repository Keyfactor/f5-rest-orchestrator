// Copyright 2023 Keyfactor
// Licensed under the Apache License, Version 2.0 (the "License"); you may
// not use this file except in compliance with the License.  You may obtain a
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless
// required by applicable law or agreed to in writing, software distributed
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for
// thespecific language governing permissions and limitations under the
// License.
using Keyfactor.Logging;
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json.Linq;
using Org.BouncyCastle.Crypto;
using Org.BouncyCastle.OpenSsl;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using Org.BouncyCastle.X509;
using System;
using System.Collections.Generic;
using System.IO;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    // Reenrollment (On Device Key Generation).  The key and CSR are generated on the F5 under a temporary name, the CSR is
    //  signed by Keyfactor Command, and the resulting certificate is combined with the key into a PKCS12 which is then
    //  installed under the real alias using the same logic as the store type's Management Add job.
    public abstract class ReenrollmentBase : F5JobBase, IReenrollmentJobExtension
    {
        // Only used to transport the key/certificate between this orchestrator and the F5, it is never persisted
        private const string TRANSIENT_PFX_PASSWORD = "12345";
        private const int TEMP_ALIAS_LENGTH = 5;

        protected ILogger logger;

        protected ReenrollmentJobConfiguration JobConfig { get; set; }
        protected JObject StoreProperties { get; set; }
        protected bool IgnoreSSLWarning { get; set; }
        protected bool UseTokenAuth { get; set; }

        public string ExtensionName => "Keyfactor.Extensions.Orchestrator.F5Orchestrator.Reenrollment";

        // The partition the certificate is installed in
        protected abstract string GetPartition(F5Client f5);

        // Installs the PKCS12 (base64) as the real alias, performing any binding done by the store type's Management Add job
        protected abstract void InstallCertificate(F5Client f5, string partition, string alias, string b64Pfx, bool certificateExists);

        public JobResult ProcessJob(ReenrollmentJobConfiguration config, SubmitReenrollmentCSR submitReenrollment)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }

            LogHandlerCommon.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");
            JobConfig = config;
            F5Client f5 = null;
            string tempAlias = null;

            try
            {
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePassword, logger);
                ParseStoreProperties();

                string alias = config.Alias;
                if (string.IsNullOrEmpty(alias)) { throw new Exception("Alias is required for reenrollment."); }
                string keyType = MapKeyType(GetJobProperty("keyType"));
                if (!int.TryParse(GetJobProperty("keySize"), out int keySize)) { throw new Exception("A valid key size is required for reenrollment."); }

                f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, TRANSIENT_PFX_PASSWORD, IgnoreSSLWarning, UseTokenAuth, null);
                WaitForPrimaryNode(f5);
                ValidateF5Release(logger, config.CertificateStoreDetails, f5);

                string partition = GetPartition(f5);
                bool certificateExists = f5.CertificateExists(partition, alias);
                if (certificateExists && !config.Overwrite)
                {
                    throw new Exception($"An entry named '{alias}' exists and 'overwrite' was not selected");
                }

                tempAlias = GenerateTempAlias();
                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, $"Generating key and CSR on the F5 using temporary alias '{tempAlias}'");
                f5.CreateTempKey(tempAlias, keyType, keySize);
                string csr = f5.CreateTempCSR(BuildCSRRequest(tempAlias));

                X509Certificate2 certificate = submitReenrollment.Invoke(csr);
                if (certificate == null) { throw new Exception("Error retrieving certificate for CSR: certificate not returned."); }

                string b64Pfx = BuildPkcs12(f5.GetTempKey(tempAlias), certificate, alias);

                InstallCertificate(f5, partition, alias, b64Pfx, certificateExists);

                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (SyncException ex)
            {
                LogHandlerCommon.Error(logger, config.CertificateStoreDetails, ExceptionHandler.FlattenExceptionMessages(ex, "Warning performing device group sync: "));
                return new JobResult { Result = OrchestratorJobStatusJobResult.Warning, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Certificate successfully reenrolled, but device group sync failed. ") };
            }
            catch (BindException ex)
            {
                LogHandlerCommon.Error(logger, config.CertificateStoreDetails, ExceptionHandler.FlattenExceptionMessages(ex, "Warning performing SSL profile binding: "));
                return new JobResult { Result = OrchestratorJobStatusJobResult.Warning, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Certificate successfully reenrolled, but one or more SSL profiles could not be bound. ") };
            }
            catch (Exception ex)
            {
                LogHandlerCommon.Error(logger, config.CertificateStoreDetails, ExceptionHandler.FlattenExceptionMessages(ex, "Error performing Reenrollment"));
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the reenrollment operation.") };
            }
            finally
            {
                if (f5 != null)
                {
                    if (tempAlias != null)
                        f5.DeleteTempKeyAndCSR(tempAlias);

                    if (UseTokenAuth)
                        f5.RemoveToken();
                }
                LogHandlerCommon.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }

        protected virtual void ParseStoreProperties()
        {
            StoreProperties = JObject.Parse(JobConfig.CertificateStoreDetails.Properties.ToString());
            IgnoreSSLWarning = GetStoreBool("IgnoreSSLWarning");
            UseTokenAuth = GetStoreBool("UseTokenAuth");
        }

        // Same behavior as Management: when required, check the primary node is active, retrying up to the configured maximum
        private void WaitForPrimaryNode(F5Client f5)
        {
            if (!GetStoreBool("PrimaryNodeOnlineRequired"))
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "The primary node is not required to be active");
                return;
            }

            string primaryNode = GetStoreString("PrimaryNode");
            if (string.IsNullOrEmpty(primaryNode)) { throw new Exception("Missing job property string: PrimaryNode"); }
            if (!int.TryParse(GetStoreString("PrimaryNodeCheckRetryMax"), out int retryMax)) { retryMax = 3; }
            if (!int.TryParse(GetStoreString("PrimaryNodeCheckRetryWaitSecs"), out int retryWaitSecs)) { retryWaitSecs = 120; }

            f5.PrimaryNode = primaryNode;
            for (int attempt = 1; !f5.PrimaryNodeActive(); attempt++)
            {
                LogHandlerCommon.Warn(logger, JobConfig.CertificateStoreDetails, $"The primary node: '{primaryNode}' is not active on try '{attempt}' of '{retryMax}'");
                if (attempt >= retryMax)
                {
                    throw new Exception($"The primary node: '{primaryNode}' is not active and the maximum number of retries '{retryMax}' has been reached");
                }

                LogHandlerCommon.Warn(logger, JobConfig.CertificateStoreDetails, $"Waiting for '{retryWaitSecs}' seconds before checking if '{primaryNode}' is active");
                Thread.Sleep(retryWaitSecs * 1000);
            }
        }

        protected bool GetStoreBool(string name)
        {
            return bool.TryParse(StoreProperties[name]?.ToString(), out bool value) && value;
        }

        protected string GetStoreString(string name)
        {
            return StoreProperties[name]?.ToString() ?? string.Empty;
        }

        protected string GetJobProperty(string name)
        {
            return JobConfig.JobProperties != null && JobConfig.JobProperties.TryGetValue(name, out object value) && value != null ? value.ToString() : string.Empty;
        }

        private static string MapKeyType(string keyType)
        {
            switch (keyType?.Trim().ToUpperInvariant())
            {
                case "RSA":
                    return "rsa-private";
                case "EC":
                case "ECC":
                    return "ec-private";
                default:
                    throw new Exception($"Unsupported key type '{keyType}'.  Supported key types are RSA, EC and ECC.");
            }
        }

        private static string GenerateTempAlias()
        {
            const string firstChars = "abcdefghijklmnopqrstuvwxyz";
            const string chars = firstChars + "0123456789";

            // Start with a letter so the name is always a valid F5 object name
            StringBuilder alias = new StringBuilder(TEMP_ALIAS_LENGTH);
            alias.Append(firstChars[RandomNumberGenerator.GetInt32(firstChars.Length)]);
            for (int i = 1; i < TEMP_ALIAS_LENGTH; i++)
                alias.Append(chars[RandomNumberGenerator.GetInt32(chars.Length)]);

            return alias.ToString();
        }

        private F5CryptoCSRRequest BuildCSRRequest(string tempAlias)
        {
            // Subject is supplied by Command as a comma delimited DN, e.g. "CN=host.domain.com,O=Org,OU=Unit,L=City,ST=State,C=US"
            Dictionary<string, string> subject = new Dictionary<string, string>();
            foreach (string part in GetJobProperty("subjectText").Split(',', StringSplitOptions.RemoveEmptyEntries))
            {
                string[] pair = part.Split('=', 2, StringSplitOptions.TrimEntries);
                if (pair.Length == 2)
                    subject[pair[0].ToUpperInvariant()] = pair[1];
            }

            List<string> sans = new List<string>();
            foreach (KeyValuePair<string, string[]> san in JobConfig.SANs ?? new Dictionary<string, string[]>())
            {
                string type = san.Key.Replace("ip4", "ip", StringComparison.OrdinalIgnoreCase).Replace("ip6", "ip", StringComparison.OrdinalIgnoreCase).Replace("upn", "uri", StringComparison.OrdinalIgnoreCase);
                foreach (string value in san.Value)
                    sans.Add($"{type}:{value}");
            }

            string GetSubjectValue(string key) => subject.TryGetValue(key, out string value) ? value : null;

            return new F5CryptoCSRRequest
            {
                name = tempAlias,
                key = tempAlias,
                commonName = GetSubjectValue("CN"),
                country = GetSubjectValue("C"),
                state = GetSubjectValue("ST"),
                city = GetSubjectValue("L"),
                organization = GetSubjectValue("O"),
                ou = GetSubjectValue("OU"),
                subjectAlternativeName = sans.Count > 0 ? string.Join(",", sans) : null
            };
        }

        private static string BuildPkcs12(string keyPem, X509Certificate2 certificate, string alias)
        {
            // The F5 key file may be preceded by other PEM blocks (e.g. EC PARAMETERS), so isolate the private key block
            Match keyBlock = Regex.Match(keyPem, @"-----BEGIN (?<type>[A-Z ]*PRIVATE KEY)-----.*?-----END \k<type>-----", RegexOptions.Singleline);
            if (!keyBlock.Success) { throw new Exception("Unable to find the private key generated on the F5 device."); }

            AsymmetricKeyParameter privateKey;
            using (PemReader reader = new PemReader(new StringReader(keyBlock.Value)))
            {
                object keyObject = reader.ReadObject();
                privateKey = keyObject is AsymmetricCipherKeyPair keyPair ? keyPair.Private : keyObject as AsymmetricKeyParameter;
            }
            if (privateKey == null || !privateKey.IsPrivate) { throw new Exception("Unable to parse the private key generated on the F5 device."); }

            Org.BouncyCastle.X509.X509Certificate bcCertificate = new X509CertificateParser().ReadCertificate(certificate.RawData);

            Pkcs12Store store = new Pkcs12StoreBuilder().Build();
            store.SetKeyEntry(alias, new AsymmetricKeyEntry(privateKey), new[] { new X509CertificateEntry(bcCertificate) });

            using (MemoryStream ms = new MemoryStream())
            {
                store.Save(ms, TRANSIENT_PFX_PASSWORD.ToCharArray(), new SecureRandom());
                return Convert.ToBase64String(ms.ToArray());
            }
        }
    }
}
