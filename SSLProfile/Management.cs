// Copyright 2023 Keyfactor                                                   
// Licensed under the Apache License, Version 2.0 (the "License"); you may    
// not use this file except in compliance with the License.  You may obtain a 
// copy of the License at http://www.apache.org/licenses/LICENSE-2.0.  Unless 
// required by applicable law or agreed to in writing, software distributed   
// under the License is distributed on an "AS IS" BASIS, WITHOUT WARRANTIES   
// OR CONDITIONS OF ANY KIND, either express or implied. See the License for  
// thespecific language governing permissions and limitations under the       
// License. 
﻿using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Org.BouncyCastle.Pkcs;
using Org.BouncyCastle.Security;
using System.Text;
using System.IO;
using Newtonsoft.Json;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.SSLProfile
{
    public class Management : ManagementBase
    {

        public Management(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public override JobResult ProcessJob(ManagementJobConfiguration config)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }

            LogHandlerCommon.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");

            if (config.OperationType != CertStoreOperationType.Add
                && config.OperationType != CertStoreOperationType.Remove)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}-' Management job expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
            }
            
            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePassword, logger);
                string sslProfiles = JobConfig.JobProperties.ContainsKey("SSLProfiles") && JobConfig.JobProperties["SSLProfiles"]  != null ? JobConfig.JobProperties["SSLProfiles"].ToString() : string.Empty;
                base.ParseStoreProperties();
                base.PrimaryNodeActive();

                F5Client f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, config.JobCertificate.PrivateKeyPassword, IgnoreSSLWarning, UseTokenAuth, config.LastInventory)
                {
                    PrimaryNode = base.PrimaryNode
                };

                ValidateF5Release(logger, JobConfig.CertificateStoreDetails, f5);

                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, $"Add entry '{config.JobCertificate.Alias}' to '{config.CertificateStoreDetails.StorePath}'");
                        bool certificateExists = PerformAddJob(f5, StorePassword, RemoveChain);
                        if (!certificateExists && !string.IsNullOrEmpty(sslProfiles))
                            BindCertificateToSSLProfiles(f5, config.JobCertificate.Alias, sslProfiles);
                        break;
                    case CertStoreOperationType.Remove:
                        LogHandlerCommon.Trace(logger, config.CertificateStoreDetails, $"Remove entry '{config.JobCertificate.Alias}' from '{config.CertificateStoreDetails.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"Management job expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
                }

                if (UseTokenAuth)
                    f5.RemoveToken();

                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (BindException ex)
            {
                LogHandlerCommon.Error(logger, config.CertificateStoreDetails, ExceptionHandler.FlattenExceptionMessages(ex, $"Warning performing SSL profile binding: "));
                return new JobResult { Result = OrchestratorJobStatusJobResult.Warning, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Certificate successfully added, but one or more SSL profiles could not be bound. ") };
            }
            catch (Exception ex)
            {
                LogHandlerCommon.Error(logger, config.CertificateStoreDetails, ExceptionHandler.FlattenExceptionMessages(ex, $"Error performing Management {config.OperationType.ToString()}"));
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }

        private bool PerformAddJob(F5Client f5, string certificatePassword, bool removeChain)
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");
            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            string certContents = !string.IsNullOrEmpty(JobConfig.JobCertificate.PrivateKeyPassword) && removeChain ? RemoveCertificateChainFromPfx() : JobConfig.JobCertificate.Contents;
            bool certificateExists = f5.CertificateExists(partition, name);

            if (certificateExists)
            {
                if (!JobConfig.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Replace entry '{name}' in '{JobConfig.CertificateStoreDetails.StorePath}'");
                f5.ReplaceEntry(partition, name, certContents, certificatePassword);
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}' and will be added");
                f5.AddEntry(partition, name, certContents, certificatePassword);
            }
            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");

            return certificateExists;
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformRemovalJob");
            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.CertificateExists(partition, name))
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' exists in '{JobConfig.CertificateStoreDetails.StorePath}' and will be removed");
                f5.RemoveEntry(partition, name);
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}'");
            }
            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformRemovalJob");
        }

        private string RemoveCertificateChainFromPfx()
        {
            string rtnValue = string.Empty;
            char[] password = JobConfig.JobCertificate.PrivateKeyPassword.ToCharArray();

            Pkcs12StoreBuilder storeBuilder = new Pkcs12StoreBuilder();
            Pkcs12Store store = storeBuilder.Build();
            store.Load(new MemoryStream(Convert.FromBase64String(JobConfig.JobCertificate.Contents)), password);

            // Find the key entry (private key and its associated certificate)
            string alias = null;
            foreach (string currentAlias in store.Aliases)
            {
                if (store.IsKeyEntry(currentAlias))
                {
                    alias = currentAlias;
                    break;
                }
            }

            if (alias == null)
                throw new Exception("No private key entry found in PFX.");

            // Extract the private key and its associated certificate
            AsymmetricKeyEntry keyEntry = store.GetKey(alias);
            X509CertificateEntry certEntry = store.GetCertificate(alias);

            // Create a new PKCS#12 store with only the main certificate and private key
            Pkcs12Store newStore = storeBuilder.Build();
            newStore.SetKeyEntry(alias, keyEntry, new[] { certEntry });

            // Save the new PFX to a byte array
            using (MemoryStream ms = new MemoryStream())
            {
                newStore.Save(ms, password, new SecureRandom());
                rtnValue = Convert.ToBase64String(ms.ToArray());
            }

            return rtnValue;
        }

        private void BindCertificateToSSLProfiles(F5Client f5, string alias, string sslProfiles)
        {
            bool hasError = false;
            string errorMessages = string.Empty;

            foreach (string sslProfile in sslProfiles.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                try
                {
                    f5.BindCertificate(alias, sslProfile);
                }
                catch (Exception ex)
                {
                    hasError = true;
                    errorMessages += ExceptionHandler.FlattenExceptionMessages(ex, $"Error binding {sslProfile}: ");
                }
            }

            if (hasError)
            {
                throw new BindException(errorMessages);
            }
        }

        public class BindException : Exception
        {
            public BindException(string message) : base(message) { }
        }
    }
}
