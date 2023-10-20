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
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.WebServer
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

            if (config.OperationType != CertStoreOperationType.Add)
            {
                throw new Exception($"'{config.CertificateStoreDetails.ClientMachine}-{config.CertificateStoreDetails.StorePath}' Management job expecting 'Add' job - received '{Enum.GetName(typeof(CertStoreOperationType), config.OperationType)}'");
            }

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            try
            {
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, logger);
                base.ParseJobProperties();
                base.PrimaryNodeActive();

                F5Client f5 = new F5Client(JobConfig.CertificateStoreDetails, ServerUserName, ServerPassword, JobConfig.UseSSL, JobConfig.JobCertificate.PrivateKeyPassword, IgnoreSSLWarning, UseTokenAuth, JobConfig.LastInventory)
                {
                    PrimaryNode = base.PrimaryNode
                };

                LogHandlerCommon.Trace(logger, config.CertificateStoreDetails, "Replacing F5 web server certificate");
                f5.ReplaceWebServerCrt(JobConfig.JobCertificate.Contents);

                LogHandlerCommon.Debug(logger, config.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
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
    }
}
