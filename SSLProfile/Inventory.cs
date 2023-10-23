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
using System.Collections.Generic;
using Keyfactor.Orchestrators.Extensions.Interfaces;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.SSLProfile
{
    public class Inventory : InventoryBase
    {
        public Inventory(IPAMSecretResolver resolver)
        {
            _resolver = resolver;
        }

        public override JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            if (logger == null)
            {
                logger = LogHandler.GetClassLogger(this.GetType());
            }
            LogHandlerCommon.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            try
            {
                base.ParseJobProperties();
                SetPAMSecrets(config.ServerUsername, config.ServerPassword, logger);
                F5Client f5 = new F5Client(config.CertificateStoreDetails, ServerUserName, ServerPassword, config.UseSSL, null, IgnoreSSLWarning, UseTokenAuth, config.LastInventory) { F5Version = base.F5Version };

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Getting inventory from '{config.CertificateStoreDetails.StorePath}'");
                inventory = f5.GetSSLProfiles(20);

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Submitting {inventory?.Count} inventory entries for '{config.CertificateStoreDetails.StorePath}'");
                submitInventory.Invoke(inventory);

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the inventory operation.") };
            }
            finally
            {
                LogHandlerCommon.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }
    }
}
