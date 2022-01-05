﻿using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;
using System.Collections.Generic;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.SSLProfile
{
    public class Inventory : InventoryBase
    {
        public override string GetStoreType()
        {
            return "F5-SL-REST";
        }

        public override JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            LogHandler.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            try
            {
                base.ParseJobProperties();
                F5Client f5 = new F5Client(config) { F5Version = base.F5Version };

                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, $"Getting inventory from '{config.CertificateStoreDetails.StorePath}'");
                inventory = f5.GetSSLProfiles(20);

                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, $"Submitting {inventory?.Count} inventory entries for '{config.CertificateStoreDetails.StorePath}'");
                submitInventory.Invoke(inventory);

                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, "Job complete");
                return new JobResult { Result = OrchestratorJobStatusJobResult.Success, JobHistoryId = config.JobHistoryId };
            }
            catch (Exception ex)
            {
                return new JobResult { Result = OrchestratorJobStatusJobResult.Failure, JobHistoryId = config.JobHistoryId, FailureMessage = ExceptionHandler.FlattenExceptionMessages(ex, "Unable to complete the inventory operation.") };
            }
            finally
            {
                LogHandler.MethodExit(logger, config.CertificateStoreDetails, "ProcessJob");
            }
        }
    }
}
