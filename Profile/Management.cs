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
using Keyfactor.Orchestrators.Common.Enums;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Extensions.Interfaces;
using Keyfactor.PKI.X509;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography.X509Certificates;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.Profile
{
    public class Management : ManagementBase
    {
        protected string ProfileName { get; set; }
        protected F5ProfileStorePath.ProfileTypeEnum ProfileType { get; set; }
        protected string ProfileEndpoint { get; set; }

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
                && config.OperationType != CertStoreOperationType.Remove
                && config.OperationType != CertStoreOperationType.Create)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}-' Management job expecting 'Add', 'Remove' or 'Create' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
            }

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, config.CertificateStoreDetails.StorePassword, logger);
                base.ParseStoreProperties();

                F5Client f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, config.JobCertificate?.PrivateKeyPassword, IgnoreSSLWarning, UseTokenAuth, config.LastInventory);

                ValidateF5Release(logger, JobConfig.CertificateStoreDetails, f5);

                F5ProfileStorePath profileStorePath = f5.ParseProfileStorePath();
                string partition = profileStorePath.Partition;
                ProfileName = profileStorePath.ProfileName;
                ProfileType = profileStorePath.ProfileType;
                ProfileEndpoint = F5Client.GetProfileEndpoint(ProfileType);
                JobResult warningResult = null;

                switch (config.OperationType)
                {
                    case CertStoreOperationType.Create:
                        LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, $"Create profile '{ProfileName}' in '{config.CertificateStoreDetails.StorePath}'");
                        warningResult = PerformCreateJob(f5, partition);
                        break;
                    case CertStoreOperationType.Add:
                        LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, $"Add entry '{config.JobCertificate.Alias}' to '{config.CertificateStoreDetails.StorePath}'");
                        PerformAddJob(f5, partition, StorePassword);
                        break;
                    default:
                        // Shouldn't get here, but just in case
                        throw new Exception($"Management job expecting 'Add' or 'Create' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
                }

                if (UseTokenAuth)
                    f5.RemoveToken();

                if (warningResult != null)
                {
                    return warningResult;
                }

                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
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

        private JobResult PerformCreateJob(F5Client f5, string partition)
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformCreateJob");

            if (f5.ProfileExists(partition, ProfileEndpoint, ProfileName))
            {
                string message = $"A profile named '{ProfileName}' already exists in partition '{partition}' - no action was taken.";
                LogHandlerCommon.Info(logger, JobConfig.CertificateStoreDetails, message);
                LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformCreateJob");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Warning, JobHistoryId = JobConfig.JobHistoryId, FailureMessage = message };
            }

            if (!string.IsNullOrEmpty(InheritedProfile) && !f5.ProfileExists(partition, ProfileEndpoint, InheritedProfile))
            {
                string message = $"The inherited profile '{InheritedProfile}' does not exist in partition '{partition}' - no action was taken.";
                LogHandlerCommon.Error(logger, JobConfig.CertificateStoreDetails, message);
                LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformCreateJob");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = JobConfig.JobHistoryId, FailureMessage = message };
            }

            f5.CreateProfile(partition, ProfileEndpoint, ProfileName, InheritedProfile);

            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformCreateJob");
            return null;
        }

        private void PerformAddJob(F5Client f5, string partition, string certificatePassword)
        {
            LogHandlerCommon.MethodEntry(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");
            string name = JobConfig.JobCertificate.Alias;
            string newName = string.Empty;

            string certContents = JobConfig.JobCertificate.Contents;
            bool certificateExists = f5.CertificateExists(partition, name);

            if (certificateExists)
            {
                if (!JobConfig.Overwrite) { throw new Exception($"An entry named '{name}' exists and 'overwrite' was not selected"); }

                List<string> boundElsewhere = f5.GetProfilesBoundToCertificate(partition, name, ProfileName);
                if (boundElsewhere.Any())
                {
                    throw new Exception($"The certificate '{name}' is bound to the following other profile(s) and cannot be replaced: {string.Join(", ", boundElsewhere)}");
                }

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Replace entry '{name}' in '{JobConfig.CertificateStoreDetails.StorePath}'");
                newName = f5.ReplaceEntry(partition, name, certContents, certificatePassword, AutoGenerateAlias, AutoGeneratedAliasFormat, KeepSameAliasOnRenewal);
            }
            else
            {
                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"The entry '{name}' does not exist in '{JobConfig.CertificateStoreDetails.StorePath}' and will be added");
                newName = f5.AddEntry(partition, name, certContents, certificatePassword, AutoGenerateAlias, AutoGeneratedAliasFormat);
            }

            LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Binding '{name}' to profile '{ProfileName}'");
            f5.BindCertificateToProfile(partition, ProfileEndpoint, ProfileName, newName, certificatePassword);

            LogHandlerCommon.MethodExit(logger, JobConfig.CertificateStoreDetails, "PerformAddJob");
        }
    }
}
