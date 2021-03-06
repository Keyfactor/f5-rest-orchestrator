using Keyfactor.Logging;
using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using System;
using System.Collections.Generic;

namespace Keyfactor.Extensions.Orchestrator.F5Orchestrator.Bundle
{
    public class Inventory : InventoryBase
    {
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
                F5Client f5 = new F5Client(config.CertificateStoreDetails, config.ServerUsername, config.ServerPassword, config.UseSSL, null, config.LastInventory) { F5Version = base.F5Version, IgnoreSSLWarning = base.IgnoreSSLWarning };

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Getting inventory for CA Bundle '{config.CertificateStoreDetails.StorePath}'");
                inventory = f5.GetCABundleInventory();

                LogHandlerCommon.Debug(logger, JobConfig.CertificateStoreDetails, $"Submitting {inventory?.Count} inventory entries for CA Bundle '{config.CertificateStoreDetails.StorePath}'");
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
