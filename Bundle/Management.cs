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
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.Bundle
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
                throw new Exception($"Management job Expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
            }

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, logger);
                base.ParseJobProperties();
                base.PrimaryNodeActive();

                F5Client f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, config.JobCertificate.PrivateKeyPassword, IgnoreSSLWarning, config.LastInventory)
                {
                    PrimaryNode = base.PrimaryNode,
                    F5Version = base.F5Version
                };

                switch (config.OperationType)
                {
                    case CertStoreOperationType.Add:
                        LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Add CA Bundle entry '{config.JobCertificate.Alias}' to '{config.CertificateStoreDetails.StorePath}'");
                        PerformAddJob(f5);
                        break;
                    case CertStoreOperationType.Remove:
                        LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Remove CA Bundle entry '{config.JobCertificate.Alias}' from '{config.CertificateStoreDetails.StorePath}'");
                        PerformRemovalJob(f5);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"Management job expecting 'Add' or 'Remove' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
                }

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId};
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the management operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }

        private void PerformAddJob(F5Client f5)
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");

            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            List<CurrentInventoryItem> inventory = f5.GetCABundleInventory();

            if (inventory.Exists(i => i.Alias.Equals(name, StringComparison.OrdinalIgnoreCase)))
            {
                if (f5.CertificateExists(partition, name)) { LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' exists in the SSL certificate store"); }
                if (!JobConfig.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Replace entry '{name}' in '{JobConfig.CertificateStoreDetails.StorePath}'");
                f5.ReplaceEntry(partition, name, JobConfig.JobCertificate.Contents);
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in the bundle '{JobConfig.CertificateStoreDetails.StorePath}' and will be added");
                f5.AddBundleEntry(JobConfig.CertificateStoreDetails.StorePath, partition, name, JobConfig.JobCertificate.Contents, JobConfig.JobCertificate.Alias, JobConfig.Overwrite);
            }

            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");
        }

        private void PerformRemovalJob(F5Client f5)
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformRemovalJob");

            string name = JobConfig.JobCertificate.Alias;
            string partition = f5.GetPartitionFromStorePath();

            if (f5.EntryExistsInBundle(JobConfig.JobCertificate.Alias))
            {
                if (f5.CertificateExists(partition, name)) { logger.LogDebug($"The entry '{name}' exists in the SSL certificate store"); }

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' exists in the bundle '{JobConfig.CertificateStoreDetails.StorePath}' and will be removed");
                f5.RemoveBundleEntry(JobConfig.CertificateStoreDetails.StorePath, partition, name);
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in the bundle '{JobConfig.CertificateStoreDetails.StorePath}'");
            }

            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformRemovalJob");
        }
    }
}
