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
using System;
using System.Collections.Generic;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using System.Threading;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator
{
    // Reenrollment (On Device Key Generation).  The key and CSR are generated on the F5, the CSR is signed by Keyfactor Command
    //  and the returned certificate is installed to pair with the key.  When an existing alias is renewed (Overwrite), everything
    //  is created under a new timestamped alias and the profiles bound to the old alias are moved to it.
    public abstract class ReenrollmentBase : F5JobBase, IReenrollmentJobExtension
    {

        protected ILogger logger;

        protected ReenrollmentJobConfiguration JobConfig { get; set; }
        protected JObject StoreProperties { get; set; }
        protected bool IgnoreSSLWarning { get; set; }
        protected bool UseTokenAuth { get; set; }

        public string ExtensionName => "Keyfactor.Extensions.Orchestrator.F5Orchestrator.Reenrollment";

        // The partition the certificate is installed in
        protected abstract string GetPartition(F5Client f5);

        // Store type specific work once the certificate is installed (e.g. binding a new certificate, device group sync)
        protected abstract void AfterCertificateInstalled(F5Client f5, string partition, string alias, bool certificateExisted);

        public virtual JobResult ProcessJob(ReenrollmentJobConfiguration config, SubmitReenrollmentCSR submitReenrollment)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }

            LogHandlerCommon.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");
            JobConfig = config;
            F5Client f5 = null;
            string newAlias = null;
            bool csrCreated = false;
            bool keyCreated = false;
            bool certificateInstalled = false;

            try
            {
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePassword, logger);
                ParseStoreProperties();

                string alias = config.Alias;
                if (string.IsNullOrEmpty(alias)) { throw new Exception("Alias is required for reenrollment."); }
                string keyType = MapKeyType(GetJobProperty("keyType"));
                if (!int.TryParse(GetJobProperty("keySize"), out int keySize)) { throw new Exception("A valid key size is required for reenrollment."); }

                f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, null, IgnoreSSLWarning, UseTokenAuth, null);
                WaitForPrimaryNode(f5);
                ValidateF5Release(logger, config.CertificateStoreDetails, f5);

                string partition = GetPartition(f5);
                bool certificateExists = f5.CertificateExists(partition, alias);
                if (certificateExists && !config.Overwrite)
                {
                    throw new Exception($"An entry named '{alias}' exists and 'overwrite' was not selected");
                }

                // A renewal is installed under a new, timestamped alias so the existing certificate and key are left untouched
                //  until the profiles bound to them have been moved over to the new certificate
                newAlias = certificateExists ? $"{alias}_{DateTime.Now:yyyyMMddHHmmss}" : alias;

                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, $"Generating key and CSR on the F5 using alias '{newAlias}'");
                f5.CreateKey(newAlias, keyType, keySize, partition);
                keyCreated = true;
                csrCreated = true;
                string csr = f5.CreateCSR(BuildCSRRequest(newAlias, partition));

                X509Certificate2 certificate = submitReenrollment.Invoke(csr);
                if (certificate == null) { throw new Exception("Error retrieving certificate for CSR: certificate not returned."); }

                f5.AddCertificate(Encoding.ASCII.GetBytes(certificate.ExportCertificatePem()), partition, newAlias);
                certificateInstalled = true;

                if (certificateExists)
                    f5.RebindProfiles(partition, alias, newAlias);

                AfterCertificateInstalled(f5, partition, newAlias, certificateExists);

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
                    // The CSR is never needed again.  The key is only removed if no certificate was installed for it.
                    if (csrCreated)
                        f5.DeleteCSR(newAlias);
                    if (keyCreated && !certificateInstalled)
                        f5.DeleteKey(newAlias);

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

        private F5CryptoCSRRequest BuildCSRRequest(string alias, string partition)
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
                name = alias,
                key = alias,
                partition = partition,
                commonName = GetSubjectValue("CN"),
                country = GetSubjectValue("C"),
                state = GetSubjectValue("ST"),
                city = GetSubjectValue("L"),
                organization = GetSubjectValue("O"),
                ou = GetSubjectValue("OU"),
                emailAddress = GetSubjectValue("E"),
                subjectAlternativeName = sans.Count > 0 ? string.Join(",", sans) : null
            };
        }
    }
}
