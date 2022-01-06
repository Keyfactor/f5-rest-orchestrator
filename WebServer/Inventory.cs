using Keyfactor.Orchestrators.Extensions;
using Keyfactor.Orchestrators.Common.Enums;
using Newtonsoft.Json;
using System;
using System.Collections.Generic;

namespace Keyfactor.Platform.Extensions.Agents.F5Orchestrator.WebServer
{
    public class Inventory : InventoryBase
    {
        public override string GetStoreType()
        {
            return "F5-WS-REST";
        }

        public override JobResult ProcessJob(InventoryJobConfiguration config, SubmitInventoryUpdate submitInventory)
        {
            LogHandler.MethodEntry(logger, config.CertificateStoreDetails, "ProcessJob");

            // Save the job config for use instead of passing it around
            base.JobConfig = config;

            List<CurrentInventoryItem> inventory = new List<CurrentInventoryItem>();

            try
            {
                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, "Processing job parameters");
                dynamic properties = JsonConvert.DeserializeObject(config.CertificateStoreDetails.Properties.ToString());

                F5Client f5 = new F5Client(config.CertificateStoreDetails, config.ServerUsername, config.ServerPassword, config.UseSSL, null, config.LastInventory);

                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, "Getting the F5 web server device inventory");
                inventory = f5.GetWebServerInventory();

                LogHandler.Debug(logger, JobConfig.CertificateStoreDetails, "Submitting F5 web server inventory");
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
